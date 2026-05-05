using System.Collections.Immutable;
using ClosedXML.Excel;
using Core.Interfaces.Data;
using Core.Models.Data;

namespace Data
{
    /// <summary>
    /// Implementazione di IExcelRepository.
    /// Consente l'estrazione, tramite file path o stream di 
    /// comandi, indirizzi e variabili del protocollo STEM
    /// </summary>
    public class ExcelRepository : IExcelRepository
    {
        public async Task<StemProtocolData> GetProtocolDataAsync(Stream excelStream)
        {
            ArgumentNullException.ThrowIfNull(excelStream);
            excelStream.Position = 0;

            if (excelStream.Length == 0)
            {
                return new StemProtocolData
                {
                    Addresses = [],
                    Commands = []
                };
            }

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(excelStream);
                var addresses = ExtractAddresses(workbook).ToImmutableList();
                var commands = ExtractCommands(workbook).ToImmutableList();

                return new StemProtocolData { Addresses = addresses, Commands = commands };
            });
        }

        public async Task<StemProtocolData> GetDictionaryAsync(Stream excelStream, uint recipientId)
        {
            ArgumentNullException.ThrowIfNull(excelStream);
            excelStream.Position = 0;

            if (excelStream.Length == 0)
            {
                return new StemProtocolData { Variables = [] };
            }

            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(excelStream);
                var variables = ExtractVariables(workbook, recipientId).ToImmutableList();

                return new StemProtocolData { Variables = variables };
            });
        }

        public async Task<StemProtocolData> GetProtocolDataFromFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            using FileStream fileStream = File.OpenRead(filePath);
            return await GetProtocolDataAsync(fileStream);
        }

        public async Task<StemProtocolData> GetDictionaryFromFileAsync(string filePath, uint recipientId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            using FileStream fileStream = File.OpenRead(filePath);
            return await GetDictionaryAsync(fileStream, recipientId);
        }

        // Helper: estrai gli indirizzi dal workbook
        private static IEnumerable<StemRowData> ExtractAddresses(XLWorkbook workbook)
        {
            // Prova a trovare il worksheet degli indirizzi
            IXLWorksheet worksheet;
            try
            {
                worksheet = workbook.Worksheet("Indirizzo protocollo stem");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error accessing sheet 'Indirizzo protocollo stem': {ex.Message}", ex);
            }

            // Salta header e itera nelle righe sottostanti
            foreach (IXLRow? row in worksheet.RowsUsed().Skip(1))
            {
                string machine = row.Cell("A").GetValue<string>();
                string board = row.Cell("C").GetValue<string>();
                string address = row.Cell("G").GetValue<string>();

                if (!string.IsNullOrWhiteSpace(machine) && !string.IsNullOrWhiteSpace(board) && !string.IsNullOrWhiteSpace(address))
                {
                    yield return new StemRowData(machine, board, address);
                }
            }
        }

        // Helper: estrai comandi dal workbook
        private static IEnumerable<StemCommandData> ExtractCommands(XLWorkbook workbook)
        {
            // Prova a trovare il worksheet dei comandi
            IXLWorksheet worksheet;
            try
            {
                worksheet = workbook.Worksheet("COMANDI");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error accessing sheet 'COMANDI': {ex.Message}", ex);
            }

            // Salta header e itera nelle righe sottostanti
            foreach (IXLRow? row in worksheet.RowsUsed().Skip(1))
            {
                string name = row.Cell(1).GetValue<string>();
                string cmdH = row.Cell(2).GetValue<string>();
                string cmdL = row.Cell(3).GetValue<string>();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return new StemCommandData(name, cmdH, cmdL);
                }
            }
        }

        // Helper: estrai variabili dal workbook
        private static IEnumerable<StemVariableData> ExtractVariables(XLWorkbook workbook, uint recipientId)
        {
            foreach (IXLWorksheet worksheet in workbook.Worksheets)
            {
                // Controlla che ci sia il recipientId
                bool idMatched = false;
                foreach (IXLCell? cell in worksheet.Row(2).CellsUsed())
                {
                    string cellStr = cell.GetString();
                    if (cellStr.Length <= 2)
                    {
                        continue;
                    }

                    if (int.TryParse(cellStr.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out int cellValue) && (uint)cellValue == recipientId)
                    {
                        idMatched = true;
                        break;
                    }
                }

                // Salta il foglio se non c'è
                if (!idMatched)
                {
                    continue;
                }

                // Salta header e itera nelle righe sottostanti
                foreach (IXLRow? row in worksheet.RowsUsed().Skip(4))
                {
                    XLColor fillColor = row.Cell("A").Style.Fill.BackgroundColor;
                    if (fillColor.ColorType != XLColorType.Theme && fillColor.Color.ToArgb() == -7155632)
                    {
                        string name = row.Cell("A").GetValue<string>();
                        string addrH = row.Cell("B").GetValue<string>();
                        string addrL = row.Cell("C").GetValue<string>();
                        string dataType = row.Cell("D").GetValue<string>();

                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(addrH) && !string.IsNullOrWhiteSpace(addrL) && !string.IsNullOrWhiteSpace(dataType))
                        {
                            yield return new StemVariableData(name, addrH, addrL, dataType);
                        }
                    }
                }
            }
        }
    }
}
