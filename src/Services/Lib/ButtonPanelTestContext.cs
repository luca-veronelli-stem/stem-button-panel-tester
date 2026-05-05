using Core.Enums;

namespace Services.Lib
{
    /// <summary>
    /// Contesto della macchina a stati per il test delle pulsantiere.
    /// Contiene tutti i dati necessari durante l'esecuzione del test.
    /// </summary>
    public class ButtonPanelTestContext
    {
        /// <summary>
        /// Tipo di pulsantiera in test.
        /// </summary>
        public ButtonPanelType PanelType { get; set; }

        /// <summary>
        /// Tipo di test selezionato.
        /// </summary>
        public ButtonPanelTestType TestType { get; set; }

        /// <summary>
        /// Indice del pulsante corrente (0-based).
        /// </summary>
        public int CurrentButtonIndex { get; set; }

        /// <summary>
        /// Numero totale di pulsanti da testare.
        /// </summary>
        public int TotalButtons { get; set; }

        /// <summary>
        /// Indica se la pulsantiera ha LED.
        /// </summary>
        public bool HasLed { get; set; }

        /// <summary>
        /// Indica se tutti i pulsanti sono stati testati con successo.
        /// </summary>
        public bool AllButtonsPassed { get; set; } = true;

        /// <summary>
        /// Indica se il test LED è passato.
        /// </summary>
        public bool LedTestPassed { get; set; } = true;

        /// <summary>
        /// Indica se il test buzzer è passato.
        /// </summary>
        public bool BuzzerTestPassed { get; set; }

        /// <summary>
        /// Messaggio di errore in caso di fallimento.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Timestamp di inizio del test.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Timestamp di fine del test.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Risultati dettagliati per ogni pulsante.
        /// </summary>
        public List<bool> ButtonResults { get; } = [];

        /// <summary>
        /// Sub-stato del test LED (quale fase del test LED).
        /// </summary>
        public LedTestPhase CurrentLedPhase { get; set; }

        /// <summary>
        /// Resetta il contesto per un nuovo test.
        /// </summary>
        public void Reset()
        {
            CurrentButtonIndex = 0;
            AllButtonsPassed = true;
            LedTestPassed = true;
            BuzzerTestPassed = false;
            ErrorMessage = null;
            StartTime = DateTime.UtcNow;
            EndTime = null;
            ButtonResults.Clear();
            CurrentLedPhase = LedTestPhase.GreenOn;
        }
    }

    /// <summary>
    /// Fasi del test LED.
    /// </summary>
    public enum LedTestPhase
    {
        GreenOn,
        GreenOff,
        RedOn,
        RedOff,
        BothOn,
        Complete
    }
}
