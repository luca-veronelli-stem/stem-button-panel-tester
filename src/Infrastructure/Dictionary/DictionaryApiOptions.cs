using System;
using System.Text.RegularExpressions;
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
    /// Base URL of the API host. Must be absolute and HTTPS in any environment
    /// where a real credential is used; HTTP is rejected by the validator.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Path-versioning segment (R-9). Defaults to <c>"v1"</c>; a future cutover
    /// becomes <c>"v2"</c> + ship a new client release.
    /// </summary>
    public string MajorVersion { get; set; } = "v1";

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
    private static readonly Regex MajorVersionPattern = new(
        @"^v\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        if (!string.Equals(options.BaseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DictionaryApiOptions.BaseUrl)} must use https.");
        }

        if (!MajorVersionPattern.IsMatch(options.MajorVersion ?? string.Empty))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DictionaryApiOptions.MajorVersion)} must match ^v\\d+$ (got '{options.MajorVersion}').");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > MaxTimeout)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DictionaryApiOptions.Timeout)} must be > 0 and <= {MaxTimeout}.");
        }

        return ValidateOptionsResult.Success;
    }
}
