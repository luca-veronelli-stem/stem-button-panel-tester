using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary;

/// <summary>
/// Configuration for <see cref="DpapiCredentialStore"/>.
/// </summary>
public sealed class DpapiCredentialStoreOptions
{
    public const string SectionName = "DpapiCredentialStore";

    /// <summary>Directory for the credential file. Defaults to per-user LocalAppData.</summary>
    public string? Directory { get; set; }

    public string FileName { get; set; } = "credential.bin";
}

/// <summary>
/// Windows-only DPAPI-backed <see cref="IInstallationCredentialStore"/>.
/// Scope is <see cref="DataProtectionScope.CurrentUser"/> (R-2). On every
/// read the file's <see cref="Installation"/> is checked against the live
/// process identity; a <c>(MachineName, UserSid)</c> mismatch triggers
/// <see cref="ClearAsync"/> and reports as "no credential" (CHK007).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore : IInstallationCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    // 16 bytes of supplemental entropy mixed with the OS user-scope key. Constant so the same
    // process can decrypt; not a secret. Defence-in-depth against blob misuse outside this app.
    private static readonly byte[] Entropy =
        Encoding.ASCII.GetBytes("Stem.ButtonPanel.Tester:dict-v1");

    private readonly DpapiCredentialStoreOptions _options;
    private readonly ILogger<DpapiCredentialStore> _logger;
    private readonly object _lock = new();

    public DpapiCredentialStore(
        IOptions<DpapiCredentialStoreOptions> options,
        ILogger<DpapiCredentialStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    private string Directory => _options.Directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Stem.ButtonPanel.Tester");

    private string FilePath => Path.Combine(Directory, _options.FileName);

    public Task<FSharpValueOption<string>> GetApiKeyAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        StoredCredential? cred = TryLoad();
        if (cred is null)
        {
            return Task.FromResult(FSharpValueOption<string>.ValueNone);
        }

        // Mismatch detection per CHK007. Compare on (MachineName, UserSid) only;
        // InstallationId is metadata. (Equivalent to F# `Installation.installationsMatch`,
        // inlined here to avoid F# module-name interop friction.)
        Installation current = CurrentInstallation(cred.Installation.InstallationId);
        if (cred.Installation.MachineName != current.MachineName
            || cred.Installation.UserSid != current.UserSid)
        {
            _logger.LogWarning(
                "Installation mismatch on credential read; clearing (machine={Machine}, user={UserSid}).",
                current.MachineName, current.UserSid);
            ClearInternal();
            return Task.FromResult(FSharpValueOption<string>.ValueNone);
        }

        return Task.FromResult(FSharpValueOption<string>.NewValueSome(cred.ApiKey));
    }

    public Task SetApiKeyAsync(string apiKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        ct.ThrowIfCancellationRequested();

        StoredCredential? existing = TryLoad();
        Installation installation = existing?.Installation ?? CurrentInstallation(Guid.NewGuid());
        var record = new StoredCredential
        {
            ApiKey = apiKey,
            Installation = installation,
        };

        Save(record);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ClearInternal();
        return Task.CompletedTask;
    }

    public Task<FSharpValueOption<Installation>> GetInstallationAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        StoredCredential? cred = TryLoad();
        return Task.FromResult(cred is null
            ? FSharpValueOption<Installation>.ValueNone
            : FSharpValueOption<Installation>.NewValueSome(cred.Installation));
    }

    private void Save(StoredCredential record)
    {
        System.IO.Directory.CreateDirectory(Directory);
        byte[] cleartext = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        byte[] ciphertext = ProtectedData.Protect(cleartext, Entropy, DataProtectionScope.CurrentUser);
        // Atomic-ish write: temp + move.
        string tempPath = FilePath + ".tmp." + Guid.NewGuid().ToString("N");

        lock (_lock)
        {
            File.WriteAllBytes(tempPath, ciphertext);
            File.Move(tempPath, FilePath, overwrite: true);
        }

        Array.Clear(cleartext);
    }

    private StoredCredential? TryLoad()
    {
        string path = FilePath;
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] ciphertext;
        lock (_lock)
        {
            try
            {
                ciphertext = File.ReadAllBytes(path);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "DPAPI credential file unreadable; treating as absent.");
                return null;
            }
        }

        byte[] cleartext;
        try
        {
            cleartext = ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "DPAPI Unprotect failed; treating as absent.");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredCredential>(cleartext, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "DPAPI credential payload was not valid JSON; treating as absent.");
            return null;
        }
        finally
        {
            Array.Clear(cleartext);
        }
    }

    private void ClearInternal()
    {
        string path = FilePath;
        lock (_lock)
        {
            if (!File.Exists(path))
            {
                return;
            }

            // Defence-in-depth: overwrite with random bytes before delete (T057).
            try
            {
                long size = new FileInfo(path).Length;
                byte[] random = RandomNumberGenerator.GetBytes((int)Math.Max(size, 64));
                File.WriteAllBytes(path, random);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not overwrite credential file before delete; deleting anyway.");
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete credential file.");
            }
        }
    }

    private static Installation CurrentInstallation(Guid installationId)
    {
        string sid;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            sid = identity.User?.Value ?? string.Empty;
        }
        catch (Exception)
        {
            sid = string.Empty;
        }

        return new Installation(
            machineName: Environment.MachineName,
            userSid: sid,
            installationId: installationId);
    }

    private sealed class StoredCredential
    {
        [JsonPropertyName("api_key")]
        [JsonRequired]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("installation")]
        [JsonRequired]
        public Installation Installation { get; set; } = null!;
    }
}
