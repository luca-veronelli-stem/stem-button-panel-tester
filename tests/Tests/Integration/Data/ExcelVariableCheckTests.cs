using System.Reflection;
using Core.Interfaces.Data;
using Data;

#pragma warning disable xUnit1026 // panelName usato solo per leggibilità output test

namespace Tests.Integration.Data
{
    /// <summary>
    /// Test to verify which recipientIds have the required LED/Buzzer variables.
    /// </summary>
    [Trait("Category", TestCategories.Integration)]
    public class ExcelVariableCheckTests
    {
        private readonly string _testFilePath;
        private readonly IExcelRepository _excelRepository;

        public ExcelVariableCheckTests()
        {
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            _testFilePath = Path.Combine(assemblyLocation, "Resources", "StemDictionaries.xlsx");
            _excelRepository = new ExcelRepository();
        }

        [Theory]
        [InlineData(0x00030101u, "DIS0023789 - Eden XP")]
        [InlineData(0x000A0101u, "DIS0025205 - Optimus XP")]
        [InlineData(0x000B0101u, "DIS0026166 - R3L XP")]
        [InlineData(0x000C0101u, "DIS0026182 - Eden BS8")]
        public void GetVariable_ComandoLedVerde_ExistsForAllPanelTypes(uint recipientId, string _panelName)
        {
            // Arrange
            IProtocolRepository repository = CreateRepository(_excelRepository, _testFilePath, recipientId);

            // Act & Assert
            Exception exception = Record.Exception(() => repository.GetVariable("Comando Led Verde"));

            Assert.Null(exception);
        }

        [Theory]
        [InlineData(0x00030101u, "DIS0023789 - Eden XP")]
        [InlineData(0x000A0101u, "DIS0025205 - Optimus XP")]
        [InlineData(0x000B0101u, "DIS0026166 - R3L XP")]
        [InlineData(0x000C0101u, "DIS0026182 - Eden BS8")]
        public void GetVariable_ComandoLedRosso_ExistsForAllPanelTypes(uint recipientId, string _panelName)
        {
            // Arrange
            IProtocolRepository repository = CreateRepository(_excelRepository, _testFilePath, recipientId);

            // Act & Assert
            Exception exception = Record.Exception(() => repository.GetVariable("Comando Led Rosso"));

            Assert.Null(exception);
        }

        [Theory]
        [InlineData(0x00030101u, "DIS0023789 - Eden XP")]
        [InlineData(0x000A0101u, "DIS0025205 - Optimus XP")]
        [InlineData(0x000B0101u, "DIS0026166 - R3L XP")]
        [InlineData(0x000C0101u, "DIS0026182 - Eden BS8")]
        public void GetVariable_ComandoBuzzer_ExistsForAllPanelTypes(uint recipientId, string _panelName)
        {
            // Arrange
            IProtocolRepository repository = CreateRepository(_excelRepository, _testFilePath, recipientId);

            // Act & Assert
            Exception exception = Record.Exception(() => repository.GetVariable("Comando Buzzer"));

            Assert.Null(exception);
        }

        private static IProtocolRepository CreateRepository(
            IExcelRepository excelRepository,
            string filePath,
            uint recipientId)
        {
            Type? type = typeof(ExcelProtocolRepositoryFactory).Assembly
                .GetType("Data.ExcelStemProtocolRepository");

            return (IProtocolRepository)Activator.CreateInstance(
                type!,
                excelRepository,
                filePath,
                recipientId)!;
        }
    }
}
