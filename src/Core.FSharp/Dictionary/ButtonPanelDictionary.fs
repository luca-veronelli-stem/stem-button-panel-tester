namespace Stem.ButtonPanel.Tester.Core.Dictionary

open System

/// One variable belonging to a panel type. Mirrors the API contract's
/// `panel_types[i].variables[j]` shape (see contracts/dictionary-api.md).
type Variable = {
    Name: string
    Type: string
    Address: int
    Scaling: float
    Unit: string
}

/// One panel-type entry, e.g. `BP-12-A`. Mirrors the API contract's
/// `panel_types[i]` shape.
type PanelType = {
    Id: string
    DisplayName: string
    Variables: Variable list
}

/// The loaded button-panel dictionary. Populated from a successful API fetch
/// (HttpDictionaryClient) or from the most-recent cache (JsonFileDictionaryCache);
/// downstream consumers do not distinguish source.
type ButtonPanelDictionary = {
    SchemaVersion: int
    GeneratedAt: DateTimeOffset
    PanelTypes: PanelType list
}
