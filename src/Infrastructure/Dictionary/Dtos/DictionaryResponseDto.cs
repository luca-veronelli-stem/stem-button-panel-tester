using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary.Dtos;

/// <summary>
/// Wire DTO for <c>GET /api/dictionaries/{id}/resolved</c> on
/// <c>stem-dictionaries-manager</c>. Response shape is camelCase with a flat
/// list of resolved variables (standard with overrides + dictionary-specific).
/// </summary>
/// <remarks>
/// Stopgap shape (see <c>docs/STOPGAP_API_KEY.md</c>): the speced
/// <c>GET /v1/dictionary</c> endpoint is not implemented server-side, so the
/// runtime calls this endpoint instead and the client assembles a single-
/// PanelType <see cref="ButtonPanelDictionary"/> from the response. The legacy
/// <c>panel_types</c> tree shape from <c>contracts/dictionary-api.md</c> is
/// the design intent and is tracked for re-instatement.
/// </remarks>
internal sealed class DictionaryResponseDto
{
    [JsonPropertyName("id")]
    [JsonRequired]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("variables")]
    [JsonRequired]
    public List<ResolvedVariableDto> Variables { get; set; } = [];

    public ButtonPanelDictionary ToDomain(DateTimeOffset generatedAt)
    {
        var panelType = new PanelType(
            id: Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            displayName: Name,
            variables: Variables.Select(v => v.ToDomain()).ToFSharpList());

        return new ButtonPanelDictionary(
            schemaVersion: 1,
            generatedAt: generatedAt,
            panelTypes: new[] { panelType }.ToFSharpList());
    }
}

internal sealed class ResolvedVariableDto
{
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("addressHigh")]
    [JsonRequired]
    public int AddressHigh { get; set; }

    [JsonPropertyName("addressLow")]
    [JsonRequired]
    public int AddressLow { get; set; }

    [JsonPropertyName("dataType")]
    [JsonRequired]
    public string DataType { get; set; } = string.Empty;

    [JsonPropertyName("access")]
    public string? Access { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("min")]
    public double? Min { get; set; }

    [JsonPropertyName("max")]
    public double? Max { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("isStandard")]
    public bool IsStandard { get; set; }

    public Variable ToDomain() => new(
        name: Name,
        type: DataType,
        address: (AddressHigh << 8) | AddressLow,
        scaling: 1.0,
        unit: Unit ?? string.Empty);
}

internal static class FSharpListExtensions
{
    public static Microsoft.FSharp.Collections.FSharpList<T> ToFSharpList<T>(this IEnumerable<T> source)
        => Microsoft.FSharp.Collections.ListModule.OfSeq(source);
}
