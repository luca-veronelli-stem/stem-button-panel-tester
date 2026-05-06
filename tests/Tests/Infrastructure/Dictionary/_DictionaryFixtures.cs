namespace Tests.Infrastructure.Dictionary;

/// <summary>
/// Canonical 200 OK body from contracts/dictionary-api.md. Test scenarios
/// modify slices of this; keep the source-of-truth here.
/// </summary>
internal static class DictionaryFixtures
{
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
