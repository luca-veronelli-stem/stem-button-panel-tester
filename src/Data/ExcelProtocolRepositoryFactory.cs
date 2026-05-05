using Core.Interfaces.Data;

namespace Data
{
    /// <summary>
    /// Implementazione di IProtocolRepositoryFactory che utilizza la versione cached del repository
    /// per evitare blocking durante i test quando il repository viene creato.
    /// </summary>
    public class ExcelProtocolRepositoryFactory : IProtocolRepositoryFactory
    {
        private readonly IExcelRepository _excelRepository;
        private readonly string _excelFilePath;

        public ExcelProtocolRepositoryFactory(IExcelRepository excelRepository, string excelFilePath)
        {
            _excelRepository = excelRepository ?? throw new ArgumentNullException(nameof(excelRepository));
            _excelFilePath = !string.IsNullOrWhiteSpace(excelFilePath) ? excelFilePath : throw new ArgumentException("File path cannot be null or empty.", nameof(excelFilePath));
        }

        public IProtocolRepository Create(uint recipientId)
        {
            // Use cached version to avoid blocking on Excel load
            return new CachedExcelProtocolRepository(_excelRepository, _excelFilePath, recipientId);
        }

        /// <summary>
        /// Pre-carica i dati del protocollo per un dato recipientId.
        /// Deve essere chiamato all'avvio dell'applicazione per evitare blocking durante i test.
        /// </summary>
        public async Task PreloadAsync(uint recipientId)
        {
            await CachedExcelProtocolRepository.PreloadAsync(_excelRepository, _excelFilePath, recipientId).ConfigureAwait(false);
        }
    }
}
