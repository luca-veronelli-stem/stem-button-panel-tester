using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Dictionary.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary;

/// <summary>
/// Live <see cref="IDictionaryProvider"/> against <c>stem-dictionaries-manager</c>.
/// One long-lived <see cref="HttpClient"/> per process (R-3); resilience contract
/// hand-rolled (R-4): single 5s timeout (FR-012), no retries, no circuit breaker.
/// </summary>
public sealed class HttpDictionaryClient : IDictionaryProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IInstallationCredentialStore _credentialStore;
    private readonly DictionaryApiOptions _options;
    private readonly ILogger<HttpDictionaryClient> _logger;
    private readonly TimeProvider _clock;

    public HttpDictionaryClient(
        HttpClient httpClient,
        IInstallationCredentialStore credentialStore,
        IOptions<DictionaryApiOptions> options,
        ILogger<HttpDictionaryClient> logger,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _options = options.Value;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<DictionaryFetchResult> FetchAsync(CancellationToken ct)
    {
        FSharpValueOption<string> apiKey = await _credentialStore
            .GetApiKeyAsync(ct)
            .ConfigureAwait(false);

        if (apiKey.IsValueNone)
        {
            _logger.LogWarning("Dictionary fetch failed: no credential provisioned (SetupIncomplete).");
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.SetupIncomplete,
                FSharpOption<string>.Some("no credential available"));
        }

        Uri requestUri = BuildRequestUri();

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeoutCts = new CancellationTokenSource(_options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token)
                .ConfigureAwait(false);

            return await InterpretAsync(response, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled — surface as cancellation (T036), not Timeout.
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("Dictionary fetch timed out after {Timeout}.", _options.Timeout);
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.Timeout,
                FSharpOption<string>.Some($"deadline {_options.Timeout} exceeded"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Dictionary fetch failed: network unreachable.");
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.NetworkUnreachable,
                FSharpOption<string>.Some(ex.Message));
        }
    }

    private Uri BuildRequestUri()
    {
        Uri baseUrl = _options.BaseUrl ?? throw new InvalidOperationException(
            $"{nameof(DictionaryApiOptions.BaseUrl)} is not configured. Validator should have caught this at startup.");

        string baseString = baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl.AbsoluteUri : baseUrl.AbsoluteUri + "/";
        return new Uri($"{baseString}{_options.MajorVersion}/dictionary");
    }

    private async Task<DictionaryFetchResult> InterpretAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // Distinct auth-failure log level per FR-014.
            _logger.LogError("Dictionary fetch failed: {StatusCode} (credential problem).", (int)response.StatusCode);
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.Unauthorized,
                FSharpOption<string>.Some($"HTTP {(int)response.StatusCode}"));
        }

        if ((int)response.StatusCode >= 500)
        {
            _logger.LogWarning("Dictionary fetch failed: server error {StatusCode}.", (int)response.StatusCode);
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.ServerError,
                FSharpOption<string>.Some($"HTTP {(int)response.StatusCode}"));
        }

        if (!response.IsSuccessStatusCode)
        {
            // 404 falls here — endpoint moved (e.g. /v2 cutover). Treat as malformed
            // for fallback; logs carry the actual code.
            _logger.LogWarning("Dictionary fetch failed: malformed (HTTP {StatusCode}).", (int)response.StatusCode);
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.MalformedPayload,
                FSharpOption<string>.Some($"HTTP {(int)response.StatusCode}"));
        }

        DictionaryResponseDto? dto;
        try
        {
            dto = await response.Content
                .ReadFromJsonAsync<DictionaryResponseDto>(JsonOptions, ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Dictionary fetch failed: malformed payload (JSON parse).");
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.MalformedPayload,
                FSharpOption<string>.Some(ex.Message));
        }

        if (dto is null)
        {
            _logger.LogWarning("Dictionary fetch failed: malformed payload (empty body).");
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.MalformedPayload,
                FSharpOption<string>.Some("empty body"));
        }

        if (dto.SchemaVersion != 1)
        {
            _logger.LogWarning(
                "Dictionary fetch failed: unsupported schema_version {SchemaVersion}.",
                dto.SchemaVersion);
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.MalformedPayload,
                FSharpOption<string>.Some($"schema_version={dto.SchemaVersion}"));
        }

        if (dto.PanelTypes.Count == 0)
        {
            _logger.LogWarning("Dictionary fetch failed: malformed payload (empty panel_types).");
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.MalformedPayload,
                FSharpOption<string>.Some("empty panel_types"));
        }

        ButtonPanelDictionary domain = dto.ToDomain();
        DateTimeOffset fetchedAt = _clock.GetUtcNow();
        _logger.LogInformation(
            "Dictionary fetched: schema_version={SchemaVersion}, panel_types={Count}, generated_at={GeneratedAt:o}.",
            dto.SchemaVersion,
            dto.PanelTypes.Count,
            dto.GeneratedAt);

        return DictionaryFetchResult.NewSuccess(domain, fetchedAt);
    }
}
