using System;
using Microsoft.FSharp.Collections;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

/// <summary>
/// Canonical 200 OK body from contracts/dictionary-api.md. Test scenarios
/// modify slices of this; keep the source-of-truth here.
/// </summary>
internal static class DictionaryFixtures
{
    public static ButtonPanelDictionary Sample(int schemaVersion = 1)
        => new(
            schemaVersion: schemaVersion,
            generatedAt: DateTimeOffset.Parse("2026-05-06T11:23:45.000Z", System.Globalization.CultureInfo.InvariantCulture),
            panelTypes: ListModule.OfArray(new[]
            {
                new PanelType(
                    id: "BP-12-A",
                    displayName: "Button Panel 12 (variant A)",
                    variables: ListModule.OfArray(new[]
                    {
                        new Variable(
                            name: "voltage_input",
                            type: "uint16",
                            address: 4097,
                            scaling: 0.01,
                            unit: "V"),
                    })),
            }));

    public const string SuccessBody = """
    {
      "schema_version": 1,
      "generated_at": "2026-05-06T11:23:45.000Z",
      "panel_types": [
        {
          "id": "BP-12-A",
          "display_name": "Button Panel 12 (variant A)",
          "variables": [
            {
              "name": "voltage_input",
              "type": "uint16",
              "address": 4097,
              "scaling": 0.01,
              "unit": "V"
            }
          ]
        }
      ]
    }
    """;
}
