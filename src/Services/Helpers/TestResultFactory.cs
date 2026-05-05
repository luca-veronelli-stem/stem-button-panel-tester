using Core.Enums;
using Core.Models.Services;

namespace Services.Helpers
{
    /// <summary>
    /// Helper per creare risultati di test standardizzati.
    /// </summary>
    public static class TestResultFactory
    {
        /// <summary>
        /// Crea un risultato di test con errore.
        /// </summary>
        public static ButtonPanelTestResult CreateError(
            ButtonPanelType panelType,
            ButtonPanelTestType testType,
            string message,
            byte[]? deviceUuid = null)
        {
            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = testType,
                Passed = false,
                Message = message,
                Interrupted = false,
                DeviceUuid = deviceUuid
            };
        }

        /// <summary>
        /// Crea un risultato di test interrotto.
        /// </summary>
        public static ButtonPanelTestResult CreateInterrupted(
            ButtonPanelType panelType,
            ButtonPanelTestType testType,
            byte[]? deviceUuid = null)
        {
            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = testType,
                Passed = false,
                Message = "Test interrotto dall'utente.",
                Interrupted = true,
                DeviceUuid = deviceUuid
            };
        }

        /// <summary>
        /// Crea un risultato di test skippato.
        /// </summary>
        public static ButtonPanelTestResult CreateSkipped(
            ButtonPanelType panelType,
            ButtonPanelTestType testType,
            string reason,
            byte[]? deviceUuid = null)
        {
            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = testType,
                Passed = true,
                Message = $"Skipped: {reason}",
                Interrupted = false,
                DeviceUuid = deviceUuid
            };
        }

        /// <summary>
        /// Crea un risultato di test di successo.
        /// </summary>
        public static ButtonPanelTestResult CreateSuccess(
            ButtonPanelType panelType,
            ButtonPanelTestType testType,
            string message,
            byte[]? deviceUuid = null)
        {
            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = testType,
                Passed = true,
                Message = message,
                Interrupted = false,
                DeviceUuid = deviceUuid
            };
        }
    }
}
