using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary.Dtos;

/// <summary>
/// Wire DTO for <c>GET /v{n}/dictionary</c>. See
/// <c>specs/001-dictionary-from-api/contracts/dictionary-api.md</c>.
/// </summary>
internal sealed class DictionaryResponseDto
{
    [JsonPropertyName("schema_version")]
    [JsonRequired]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("generated_at")]
    [JsonRequired]
    public DateTimeOffset GeneratedAt { get; set; }

    [JsonPropertyName("panel_types")]
    [JsonRequired]
    public List<PanelTypeDto> PanelTypes { get; set; } = [];

    public ButtonPanelDictionary ToDomain() => new(
        SchemaVersion,
        GeneratedAt,
        PanelTypes.Select(p => p.ToDomain()).ToFSharpList());
}

internal sealed class PanelTypeDto
{
    [JsonPropertyName("id")]
    [JsonRequired]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("variables")]
    [JsonRequired]
    public List<VariableDto> Variables { get; set; } = [];

    public PanelType ToDomain() => new(
        Id,
        DisplayName,
        Variables.Select(v => v.ToDomain()).ToFSharpList());
}

internal sealed class VariableDto
{
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    [JsonRequired]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    [JsonRequired]
    public int Address { get; set; }

    [JsonPropertyName("scaling")]
    [JsonRequired]
    public double Scaling { get; set; }

    [JsonPropertyName("unit")]
    [JsonRequired]
    public string Unit { get; set; } = string.Empty;

    public Variable ToDomain() => new(Name, Type, Address, Scaling, Unit);
}

internal static class FSharpListExtensions
{
    public static Microsoft.FSharp.Collections.FSharpList<T> ToFSharpList<T>(this IEnumerable<T> source)
        => Microsoft.FSharp.Collections.ListModule.OfSeq(source);
}
