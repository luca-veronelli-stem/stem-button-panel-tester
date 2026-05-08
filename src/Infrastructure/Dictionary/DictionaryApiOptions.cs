using System;
using Microsoft.Extensions.Options;

namespace Infrastructure.Dictionary;

/// <summary>
/// Strongly-typed config bound from <c>appsettings.json</c> for the live
/// <c>stem-dictionaries-manager</c> client. The composition root injects
/// <see cref="IOptions{TOptions}"/> instances into <c>HttpDictionaryClient</c>.
/// </summary>
public sealed class DictionaryApiOptions
{
    /// <summary>Section name in <c>appsettings.json</c>.</summary>
    public const string SectionName = "Dictionary";

    /// <summary>
    /// Base URL of the API host. Must be absolute. HTTPS in production; HTTP
    /// is allowed for local dev (e.g. <c>http://localhost:5062</c>) — the
    /// stopgap path already exposes the API key in plaintext on disk, so the
    /// extra wire-level protection from HTTPS is moot in dev.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Identifier of the <c>stem-dictionaries-manager</c> dictionary that the
    /// runtime fetches from <c>GET /api/dictionaries/{id}/resolved</c>. Defaults
    /// to <c>2</c> ("Pulsantiere"), the button-panel dictionary.
    /// </summary>
    public int DictionaryId { get; set; } = 2;

    /// <summary>
    /// End-to-end timeout for one fetch attempt (FR-012). Default 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Validates <see cref="DictionaryApiOptions"/> at startup. Failures throw on
/// first <see cref="IOptions{T}.Value"/> access.
/// </summary>
public sealed class DictionaryApiOptionsValidator : IValidateOptions<DictionaryApiOptions>
{
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(30);

    public ValidateOptionsResult Validate(string? name, DictionaryApiOptions options)
    {
        if (options.BaseUrl is null)
        {
            return ValidateOptionsResult.Fail($"{nameof(DictionaryApiOptions.BaseUrl)} is required.");
        }

        if (!options.BaseUrl.IsAbsoluteUri)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DictionaryApiOptions.BaseUrl)} must be absolute.");
        }

        if (!string.Equals(options.BaseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && !string.Equals(options.BaseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DictionaryApiOptions.BaseUrl)} must use http or https.");
        }

        if (options.DictionaryId <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DictionaryApiOptions.DictionaryId)} must be > 0 (got {options.DictionaryId}).");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > MaxTimeout)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DictionaryApiOptions.Timeout)} must be > 0 and <= {MaxTimeout}.");
        }

        return ValidateOptionsResult.Success;
    }
}
