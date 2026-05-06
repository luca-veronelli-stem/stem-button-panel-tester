using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Infrastructure.Dictionary.Dtos;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary;

/// <summary>
/// On-disk envelope for the cached dictionary at
/// <c>%LOCALAPPDATA%\Stem.ButtonPanel.Tester\dictionary.json</c>.
/// Internal to <see cref="JsonFileDictionaryCache"/>.
/// </summary>
internal sealed class DictionaryCacheEnvelope
{
    /// <summary>Increments on incompatible envelope changes. v1 = current.</summary>
    [JsonPropertyName("schema_version")]
    [JsonRequired]
    public int SchemaVersion { get; set; }

    /// <summary>Wall-clock at the original successful API response.</summary>
    [JsonPropertyName("fetched_at")]
    [JsonRequired]
    public DateTimeOffset FetchedAt { get; set; }

    [JsonPropertyName("dictionary")]
    [JsonRequired]
    public DictionaryPayload Dictionary { get; set; } = new();

    public ButtonPanelDictionary ToDomain() => new(
        Dictionary.SchemaVersion,
        Dictionary.GeneratedAt,
        Dictionary.PanelTypes.Select(p => p.ToDomain()).ToFSharpList());

    public static DictionaryCacheEnvelope FromDomain(ButtonPanelDictionary domain, DateTimeOffset fetchedAt)
        => new()
        {
            SchemaVersion = 1,
            FetchedAt = fetchedAt,
            Dictionary = DictionaryPayload.FromDomain(domain),
        };
}

internal sealed class DictionaryPayload
{
    [JsonPropertyName("schema_version")]
    [JsonRequired]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("generated_at")]
    [JsonRequired]
    public DateTimeOffset GeneratedAt { get; set; }

    [JsonPropertyName("panel_types")]
    [JsonRequired]
    public List<PanelTypePayload> PanelTypes { get; set; } = [];

    public static DictionaryPayload FromDomain(ButtonPanelDictionary domain) => new()
    {
        SchemaVersion = domain.SchemaVersion,
        GeneratedAt = domain.GeneratedAt,
        PanelTypes = domain.PanelTypes.Select(PanelTypePayload.FromDomain).ToList(),
    };
}

internal sealed class PanelTypePayload
{
    [JsonPropertyName("id")]
    [JsonRequired]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("variables")]
    [JsonRequired]
    public List<VariablePayload> Variables { get; set; } = [];

    public PanelType ToDomain() => new(Id, DisplayName, Variables.Select(v => v.ToDomain()).ToFSharpList());

    public static PanelTypePayload FromDomain(PanelType domain) => new()
    {
        Id = domain.Id,
        DisplayName = domain.DisplayName,
        Variables = domain.Variables.Select(VariablePayload.FromDomain).ToList(),
    };
}

internal sealed class VariablePayload
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

    public static VariablePayload FromDomain(Variable domain) => new()
    {
        Name = domain.Name,
        Type = domain.Type,
        Address = domain.Address,
        Scaling = domain.Scaling,
        Unit = domain.Unit,
    };
}
