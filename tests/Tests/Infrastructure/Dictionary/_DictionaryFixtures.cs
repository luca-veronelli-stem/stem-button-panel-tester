using System;
using Microsoft.FSharp.Collections;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

/// <summary>
/// Canonical 200 OK body for <c>GET /api/dictionaries/{id}/resolved</c> on
/// <c>stem-dictionaries-manager</c> (stopgap shape — see
/// <c>docs/STOPGAP_API_KEY.md</c>). Test scenarios modify slices of this;
/// keep the source-of-truth here.
/// </summary>
internal static class DictionaryFixtures
{
    /// <summary>
    /// Domain shape mirrored from <see cref="SuccessBody"/> so that
    /// orchestration tests can assert without re-parsing the JSON. Reflects
    /// the stopgap mapping: one PanelType named after the server-side
    /// dictionary; <c>Scaling = 1.0</c>; <c>Address = (high &lt;&lt; 8) | low</c>.
    /// </summary>
    public static ButtonPanelDictionary Sample(DateTimeOffset? fetchedAt = null)
        => new(
            schemaVersion: 1,
            generatedAt: fetchedAt ?? DateTimeOffset.Parse(
                "2026-05-08T10:00:00.000Z",
                System.Globalization.CultureInfo.InvariantCulture),
            panelTypes: ListModule.OfArray(new[]
            {
                new PanelType(
                    id: "2",
                    displayName: "Pulsantiere",
                    variables: ListModule.OfArray(new[]
                    {
                        new Variable(
                            name: "Foto Tasti",
                            type: "UInt8",
                            address: (128 << 8) | 0,
                            scaling: 1.0,
                            unit: string.Empty),
                    })),
            }));

    public const string SuccessBody = """
    {
      "id": 2,
      "name": "Pulsantiere",
      "description": "Dizionario variabili logiche per tastiere esterne STEM",
      "variables": [
        {
          "name": "Foto Tasti",
          "addressHigh": 128,
          "addressLow": 0,
          "dataType": "UInt8",
          "access": "ReadOnly",
          "description": "Variabile logica gestita dalla tastiera esterna",
          "isStandard": false
        }
      ]
    }
    """;
}
