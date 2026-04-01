using Core.Models.Data;

namespace Core.Interfaces.Data
{
    /// <summary>
    /// Contratto per repository Excel.
    /// Ritrova dati protocollo da uno stream o file path.
    /// </summary>
    public interface IExcelRepository
    {
        Task<StemProtocolData> GetProtocolDataAsync(Stream excelStream);
        Task<StemProtocolData> GetDictionaryAsync(Stream excelStream, uint recipientId);

        // Overload per file paths
        Task<StemProtocolData> GetProtocolDataFromFileAsync(string filePath);
        Task<StemProtocolData> GetDictionaryFromFileAsync(string filePath, uint recipientId);
    }
}
