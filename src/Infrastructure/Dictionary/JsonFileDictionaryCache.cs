using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary;

/// <summary>
/// Configuration for <see cref="JsonFileDictionaryCache"/>.
/// </summary>
public sealed class DictionaryCacheOptions
{
    public const string SectionName = "DictionaryCache";

    /// <summary>
    /// Directory for the cache file. Defaults to
    /// <c>%LOCALAPPDATA%\Stem.ButtonPanel.Tester</c> when null.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>File name within <see cref="Directory"/>. Defaults to <c>dictionary.json</c>.</summary>
    public string FileName { get; set; } = "dictionary.json";
}

/// <summary>
/// Hook used by <see cref="JsonFileDictionaryCache"/> tests to inject a fault
/// between the .tmp write and the rename — verifying atomicity (T051).
/// Production composition leaves the default no-op.
/// </summary>
public interface ICacheWriteFaultInjector
{
    /// <summary>Called after the .tmp is written but before the rename.</summary>
    Task BeforeRenameAsync();
}

internal sealed class NoOpCacheWriteFaultInjector : ICacheWriteFaultInjector
{
    public Task BeforeRenameAsync() => Task.CompletedTask;
}

/// <summary>
/// On-disk JSON cache <see cref="IDictionaryProvider"/>. Atomic writes via
/// .tmp + rename (FR-002); schema-drift / corruption surface as
/// <see cref="FetchFailureReason.CacheUnreadable"/> (FR-010).
/// </summary>
public sealed class JsonFileDictionaryCache : IDictionaryProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly DictionaryCacheOptions _options;
    private readonly ILogger<JsonFileDictionaryCache> _logger;
    private readonly ICacheWriteFaultInjector _faultInjector;
    private readonly object _writeLock = new();

    public JsonFileDictionaryCache(
        IOptions<DictionaryCacheOptions> options,
        ILogger<JsonFileDictionaryCache> logger,
        ICacheWriteFaultInjector? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _faultInjector = faultInjector ?? new NoOpCacheWriteFaultInjector();
    }

    private string CacheDirectory => _options.Directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Stem.ButtonPanel.Tester");

    private string CachePath => Path.Combine(CacheDirectory, _options.FileName);
    private string TempPath => CachePath + ".tmp";

    public async Task<DictionaryFetchResult> FetchAsync(CancellationToken ct)
    {
        string path = CachePath;

        if (!File.Exists(path))
        {
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.CacheAbsent,
                FSharpOption<string>.Some(path));
        }

        DictionaryCacheEnvelope? envelope;
        try
        {
            await using FileStream stream = File.OpenRead(path);
            envelope = await JsonSerializer
                .DeserializeAsync<DictionaryCacheEnvelope>(stream, JsonOptions, ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Cache file unreadable: JSON parse failed.");
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.CacheUnreadable,
                FSharpOption<string>.Some(ex.Message));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Cache file unreadable: IO error.");
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.CacheUnreadable,
                FSharpOption<string>.Some(ex.Message));
        }

        if (envelope is null)
        {
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.CacheUnreadable,
                FSharpOption<string>.Some("envelope deserialised to null"));
        }

        if (envelope.SchemaVersion != 1)
        {
            _logger.LogWarning("Cache schema_version {SchemaVersion} unsupported (expected 1).", envelope.SchemaVersion);
            return DictionaryFetchResult.NewFailed(
                FetchFailureReason.CacheUnreadable,
                FSharpOption<string>.Some($"schema_version={envelope.SchemaVersion}"));
        }

        return DictionaryFetchResult.NewSuccess(envelope.ToDomain(), envelope.FetchedAt);
    }

    /// <summary>
    /// Writes the dictionary atomically. Concurrent calls serialise on the
    /// instance's write lock; the rename is atomic on NTFS, so concurrent
    /// processes still produce a non-torn final file (the loser's bytes are
    /// discarded).
    /// </summary>
    public async Task WriteAsync(ButtonPanelDictionary dictionary, DateTimeOffset fetchedAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        Directory.CreateDirectory(CacheDirectory);
        var envelope = DictionaryCacheEnvelope.FromDomain(dictionary, fetchedAt);

        // Serialise to a per-call .tmp file (suffix with a random tag so two
        // in-process callers don't trample each other's .tmp before fsync).
        string tempPath = TempPath + "." + Guid.NewGuid().ToString("N");

        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        await _faultInjector.BeforeRenameAsync().ConfigureAwait(false);

        lock (_writeLock)
        {
            File.Move(tempPath, CachePath, overwrite: true);
        }

        _logger.LogInformation("Dictionary cache written to {Path}.", CachePath);
    }
}
