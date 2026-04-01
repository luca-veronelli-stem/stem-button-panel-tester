using Core.Interfaces.Data;
using Data;
using System.Reflection;

namespace Tests.Integration.Data
{
    /// <summary>
    /// Integration tests for ExcelStemProtocolRepository.
    /// These tests verify data fetching from a real Excel file.
    /// </summary>
    public class ExcelStemProtocolRepositoryIntegrationTests
    {
        private const uint TestRecipientId = 0x00030101;
        private readonly string _testFilePath;
        private readonly IExcelRepository _excelRepository;

        public ExcelStemProtocolRepositoryIntegrationTests()
        {
            // Construct the path to the test file. Assumes the file is in a TestData folder
            // and is copied to the output directory.
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            _testFilePath = Path.Combine(assemblyLocation, "Resources", "StemDictionaries.xlsx");

            // Use the real ExcelRepository for integration testing
            _excelRepository = new ExcelRepository();
        }

        [Fact]
        public void GetCommand_FromFile_ReturnsCorrectValue()
        {
            // Arrange
            var repository = CreateRepository(_excelRepository, _testFilePath, TestRecipientId);

            // Act
            ushort result = repository.GetCommand("Scrivi variabile logica");

            // Assert
            Assert.Equal(0x0002, result);
        }

        [Fact]
        public void GetVariable_FromFile_ReturnsCorrectValue()
        {
            // Arrange
            var repository = CreateRepository(_excelRepository, _testFilePath, TestRecipientId);

            // Act
            ushort result = repository.GetVariable("Comando Led Verde");

            // Assert
            Assert.Equal(0x8002, result);
        }

        [Fact]
        public void GetCommand_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentFilePath = "non_existent_file.xlsx";
            var repository = CreateRepository(_excelRepository, nonExistentFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => repository.GetCommand("any command"));
        }

        private static IProtocolRepository CreateRepository(
            IExcelRepository excelRepository,
            string filePath,
            uint recipientId)
        {
            // Use reflection to create internal class instance, same as in unit tests
            var type = typeof(ExcelProtocolRepositoryFactory).Assembly
                .GetType("Data.ExcelStemProtocolRepository");

            return (IProtocolRepository)Activator.CreateInstance(
                type!,
                excelRepository,
                filePath,
                recipientId)!;
        }
    }
}
