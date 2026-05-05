namespace Services.Lib
{
    /// <summary>
    /// Stati possibili della macchina a stati per il test delle pulsantiere.
    /// </summary>
    public enum ButtonPanelTestState
    {
        /// <summary>
        /// Stato iniziale - in attesa di avvio test.
        /// </summary>
        Idle,

        /// <summary>
        /// Inizializzazione della comunicazione CAN.
        /// </summary>
        Initializing,

        /// <summary>
        /// In attesa della pressione di un pulsante.
        /// </summary>
        AwaitingButtonPress,

        /// <summary>
        /// Registrazione del risultato del pulsante.
        /// </summary>
        RecordingButtonResult,

        /// <summary>
        /// Test dei LED in corso.
        /// </summary>
        TestingLed,

        /// <summary>
        /// Test del buzzer in corso.
        /// </summary>
        TestingBuzzer,

        /// <summary>
        /// Test completato con successo.
        /// </summary>
        Completed,

        /// <summary>
        /// Test interrotto dall'utente.
        /// </summary>
        Interrupted,

        /// <summary>
        /// Errore durante il test.
        /// </summary>
        Error
    }
}
