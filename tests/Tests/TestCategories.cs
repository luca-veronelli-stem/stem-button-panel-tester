namespace Tests;

/// <summary>
/// Costanti per le categorie di test utilizzate nei Trait.
/// Usare con [Trait("Category", TestCategories.Unit)] etc.
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Test unitari puri — nessuna dipendenza esterna, eseguibili ovunque.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// Test di integrazione — usano componenti reali ma mockano hardware.
    /// Eseguibili su Linux CI senza hardware.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Test end-to-end — simulano workflow completi con componenti reali.
    /// Eseguibili su Linux CI senza hardware.
    /// </summary>
    public const string EndToEnd = "EndToEnd";

    /// <summary>
    /// Test che richiedono hardware PCAN fisico collegato.
    /// Esclusi da CI, eseguibili solo localmente.
    /// </summary>
    public const string RequiresHardware = "RequiresHardware";

    /// <summary>
    /// Test che richiedono Windows (WinForms, API Windows-only).
    /// Esclusi da CI Linux.
    /// </summary>
    public const string RequiresWindows = "RequiresWindows";

    /// <summary>
    /// Test che falliscono in modo intermittente su CI (timing-sensitive sotto carico).
    /// Esclusi da CI; passano localmente. Tracked in issue #3.
    /// </summary>
    public const string FlakyOnCi = "FlakyOnCi";
}
