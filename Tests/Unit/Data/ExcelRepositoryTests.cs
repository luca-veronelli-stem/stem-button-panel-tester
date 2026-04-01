using ClosedXML.Excel;
using Data;

namespace Tests.Unit.Data
{
    public class ExcelRepositoryTests
    {
        // SUT System Under Test
        private readonly ExcelRepository _repository = new();
        // Path del file Dizionari STEM.xlsx
        private readonly string RealExcelFilePath = Path.Combine("Resources", "StemDictionaries.xlsx");

        // Verifica che GetProtocolDatatAsync lanci l'eccezione se stream nulla
        [Fact]
        public async Task GetProtocolDataAsync_Stream_NullStream_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.GetProtocolDataAsync(null!));
        }

        // Verifica che GetProtocolDatatAsync ritorni dati vuoti se stream vuota
        [Fact]
        public async Task GetProtocolDataAsync_Stream_EmptyStream_ReturnsEmptyData()
        {
            using var emptyStream = new MemoryStream();
            var result = await _repository.GetProtocolDataAsync(emptyStream);

            Assert.NotNull(result);
            Assert.Empty(result.Addresses);
            Assert.Empty(result.Commands);
        }

        // Verifica che GetProtocolDatatAsync estragga Indirizzi e Comandi se stream valido
        [Fact]
        public async Task GetProtocolDataAsync_Stream_ValidData_ExtractsAddressesAndCommands()
        {
            using var stream = LoadRealExcelStream();

            var result = await _repository.GetProtocolDataAsync(stream);

            Assert.Equal(37, result.Addresses.Count); // Real count from file
            Assert.Equal("Computer", result.Addresses[0].Machine);
            Assert.Equal("Madre", result.Addresses[0].Board);
            Assert.Equal("0x00000101", result.Addresses[0].Address);

            Assert.Equal("SHERPA SLIM", result.Addresses[1].Machine);
            Assert.Equal("Azionamento", result.Addresses[1].Board);
            Assert.Equal("0x00010041", result.Addresses[1].Address);

            Assert.Equal("EDEN BS8", result.Addresses[36].Machine);
            Assert.Equal("Tastiera3", result.Addresses[36].Board);
            Assert.Equal("0x000C0103", result.Addresses[36].Address);

            Assert.Equal(65, result.Commands.Count); // Real count from file
            Assert.Equal("Versione protocollo", result.Commands[0].Name);
            Assert.Equal("0", result.Commands[0].CmdH);
            Assert.Equal("0", result.Commands[0].CmdL);

            Assert.Equal("Versione protocollo risposta ", result.Commands[1].Name);
            Assert.Equal("80", result.Commands[1].CmdH);
            Assert.Equal("0", result.Commands[1].CmdL);

            Assert.Equal("Up/Down da telecomando", result.Commands[64].Name);
            Assert.Equal("0", result.Commands[64].CmdH);
            Assert.Equal("28", result.Commands[64].CmdL);
        }

        // Verifica che GetProtocolDatatAsync lanci eccezione corretta se worksheet mancante
        [Fact]
        public async Task GetProtocolDataAsync_Stream_MissingSheet_ThrowsInvalidOperationException()
        {
            using var workbook = new XLWorkbook();
            workbook.AddWorksheet("Dummy"); // Add dummy sheet to allow saving (fix for empty workbook exception)
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.GetProtocolDataAsync(stream));
            Assert.Contains("Error accessing sheet", ex.Message);
        }

        // Verifica che GetProtocolDataFromFileAsync lanci eccezione se path sbagliato
        [Theory]
        [InlineData("", "File path cannot be null or empty.")]
        [InlineData("nonexistent.xlsx", "Could not find file")]
        public async Task GetProtocolDataFromFileAsync_InvalidPath_ThrowsException(string filePath, string expectedMessage)
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _repository.GetProtocolDataFromFileAsync(filePath));
            Assert.Contains(expectedMessage, ex.Message);
        }

        // Verifica che GetProtocolDataFromFileAsync estragga i dati se file valido
        [Fact]
        public async Task GetProtocolDataFromFileAsync_ValidFile_ExtractsData()
        {
            var result = await _repository.GetProtocolDataFromFileAsync(RealExcelFilePath);

            Assert.Equal(37, result.Addresses.Count);
            Assert.Equal("Computer", result.Addresses[0].Machine);
            Assert.Equal("Madre", result.Addresses[0].Board);
            Assert.Equal("0x00000101", result.Addresses[0].Address);

            Assert.Equal("SHERPA SLIM", result.Addresses[1].Machine);
            Assert.Equal("Azionamento", result.Addresses[1].Board);
            Assert.Equal("0x00010041", result.Addresses[1].Address);

            Assert.Equal("EDEN BS8", result.Addresses[36].Machine);
            Assert.Equal("Tastiera3", result.Addresses[36].Board);
            Assert.Equal("0x000C0103", result.Addresses[36].Address);

            Assert.Equal(65, result.Commands.Count); // Real count from file
            Assert.Equal("Versione protocollo", result.Commands[0].Name);
            Assert.Equal("0", result.Commands[0].CmdH);
            Assert.Equal("0", result.Commands[0].CmdL);

            Assert.Equal("Versione protocollo risposta ", result.Commands[1].Name);
            Assert.Equal("80", result.Commands[1].CmdH);
            Assert.Equal("0", result.Commands[1].CmdL);

            Assert.Equal("Up/Down da telecomando", result.Commands[64].Name);
            Assert.Equal("0", result.Commands[64].CmdH);
            Assert.Equal("28", result.Commands[64].CmdL);
        }

        // Verifica che GetDictionaryAsync lanci l'eccezione se stream nulla
        [Fact]
        public async Task GetDictionaryAsync_Stream_NullStream_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.GetDictionaryAsync(null!, 123));
        }

        // Verifica che GetDictionaryAsync ritorni dati vuoti se stream vuota
        [Fact]
        public async Task GetDictionaryAsync_Stream_EmptyStream_ReturnsEmptyVariables()
        {
            using var emptyStream = new MemoryStream();
            var result = await _repository.GetDictionaryAsync(emptyStream, 123);

            Assert.NotNull(result);
            Assert.Empty(result.Variables);
        }

        // Verifica che GetDictionaryAsync estragga variabili se stream valida e Id corretto
        [Fact]
        public async Task GetDictionaryAsync_Stream_ValidDataWithMatchingId_ExtractsVariables()
        {
            using var stream = LoadRealExcelStream();

            var result = await _repository.GetDictionaryAsync(stream, 65601); // 0x00010041 decimal = 65601

            Assert.Equal(79, result.Variables.Count);
            Assert.Equal("Firmware macchina", result.Variables[0].Name);
            Assert.Equal("0", result.Variables[0].AddrH);
            Assert.Equal("0", result.Variables[0].AddrL);
            Assert.Equal("uint16_t ", result.Variables[0].DataType);

            Assert.Equal("Firmware scheda", result.Variables[1].Name);
            Assert.Equal("0", result.Variables[1].AddrH);
            Assert.Equal("1", result.Variables[1].AddrL);
            Assert.Equal("uint16_t ", result.Variables[1].DataType);

            Assert.Equal("Modello", result.Variables[2].Name);
            Assert.Equal("0", result.Variables[2].AddrH);
            Assert.Equal("2", result.Variables[2].AddrL);
            Assert.Equal("String[20]", result.Variables[2].DataType);
        }

        // Verifica che GetDictionaryAsync ritorni dati vuoti Id non viene matchato
        [Fact]
        public async Task GetDictionaryAsync_Stream_NoMatchingId_ReturnsEmptyVariables()
        {
            using var stream = LoadRealExcelStream();

            var result = await _repository.GetDictionaryAsync(stream, 999); // Non-matching ID

            Assert.Empty(result.Variables);
        }

        // Verifica che GetDictionaryAsync salti le celle di colore sbagliato
        [Fact]
        public async Task GetDictionaryAsync_Stream_NoHighlightedRows_SkipsUnhighlighted()
        {
            using var workbook = CreateWorkbookFromRealData(noHighlight: true);
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var result = await _repository.GetDictionaryAsync(stream, 65601);

            Assert.Empty(result.Variables); // No highlighted rows
        }

        // Verifica che GetDictionaryFromFileAsync lanci l'eccezione se path invalido
        [Theory]
        [InlineData("", 123, "File path cannot be null or empty.")]
        public async Task GetDictionaryFromFileAsync_InvalidPath_ThrowsException(string filePath, uint recipientId, string expectedMessage)
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _repository.GetDictionaryFromFileAsync(filePath, recipientId));
            Assert.Contains(expectedMessage, ex.Message);
        }

        // Helper: Load the real Excel file as stream (assume file in test dir)
        private Stream LoadRealExcelStream()
        {
            return File.OpenRead(RealExcelFilePath);
        }

        // Helper for noHighlight test (subset, as before)
        private XLWorkbook CreateWorkbookFromRealData(bool noHighlight = false)
        {
            var wb = new XLWorkbook();

            // "Indirizzo protocollo stem" with real sample addresses (subset for test efficiency)
            var addrWs = wb.AddWorksheet("Indirizzo protocollo stem");
            addrWs.Cell("A1").Value = "Macchina";
            addrWs.Cell("C1").Value = "Codice Scheda";
            addrWs.Cell("G1").Value = "Indirizzo";

            addrWs.Cell("A2").Value = "Computer";
            addrWs.Cell("C2").Value = "Madre";
            addrWs.Cell("G2").Value = "0x00000101";

            addrWs.Cell("A3").Value = "SHERPA SLIM";
            addrWs.Cell("C3").Value = "Azionamento";
            addrWs.Cell("G3").Value = "0x00010041";

            addrWs.Cell("A4").Value = "EDEN BS8";
            addrWs.Cell("C4").Value = "Tastiera3";
            addrWs.Cell("G4").Value = "0x000C0103";

            // "COMANDI" with real sample commands (subset)
            var cmdWs = wb.AddWorksheet("COMANDI");
            cmdWs.Cell("A1").Value = "NOME COMANDO";
            cmdWs.Cell("B1").Value = "CODICE CMD H";
            cmdWs.Cell("C1").Value = "CODICE CMD L";

            cmdWs.Cell("A2").Value = "Versione protocollo";
            cmdWs.Cell("B2").Value = "";
            cmdWs.Cell("C2").Value = "";

            cmdWs.Cell("A3").Value = "Versione protocollo risposta ";
            cmdWs.Cell("B3").Value = "80";
            cmdWs.Cell("C3").Value = "";

            cmdWs.Cell("A4").Value = "Up/Down da telecomando";
            cmdWs.Cell("B4").Value = "";
            cmdWs.Cell("C4").Value = "28";

            // Sample device sheet for variables, e.g., "DATI LOGICI SHERPA SLIM" with real ID and variables (subset)
            var varWs = wb.AddWorksheet("DATI LOGICI SHERPA SLIM");

            // Row 2 ID
            varWs.Cell("B2").Value = "0x00010041"; // Real example

            // Headers row 4
            varWs.Cell("A4").Value = "NOME";
            varWs.Cell("B4").Value = "INDIRIZZO H";
            varWs.Cell("C4").Value = "INDIRIZZO L";
            varWs.Cell("D4").Value = "TIPO";

            // Sample variables from row 5
            var row5 = varWs.Row(5);
            row5.Cell("A").Value = "Firmware macchina";
            row5.Cell("B").Value = "0";
            row5.Cell("C").Value = "0";
            row5.Cell("D").Value = "uint16_t ";
            if (!noHighlight) row5.Cell("A").Style.Fill.BackgroundColor = XLColor.FromArgb(-7155632);

            var row6 = varWs.Row(6);
            row6.Cell("A").Value = "Firmware scheda";
            row6.Cell("B").Value = "0";
            row6.Cell("C").Value = "1";
            row6.Cell("D").Value = "uint16_t ";
            if (!noHighlight) row6.Cell("A").Style.Fill.BackgroundColor = XLColor.FromArgb(-7155632);

            var row7 = varWs.Row(7);
            row7.Cell("A").Value = "Modello";
            row7.Cell("B").Value = "0";
            row7.Cell("C").Value = "2";
            row7.Cell("D").Value = "String[20]";
            if (!noHighlight) row7.Cell("A").Style.Fill.BackgroundColor = XLColor.FromArgb(-7155632);

            // Add more if needed for full count, but subset for test speed
            // For real 86, could add loop, but for test, assert on subset or mock count

            return wb;
        }
    }
}
