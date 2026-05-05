using System.Collections.Immutable;
using System.Reflection;
using Core.Interfaces.Data;
using Core.Models.Data;
using Data;
using Moq;

namespace Tests.Unit.Data
{
    /// <summary>
    /// Unit tests for ExcelStemProtocolRepository.
    /// Tests command, variable, and value retrieval with mocked Excel data.
    /// </summary>
    public class ExcelStemProtocolRepositoryTests
    {
        private readonly Mock<IExcelRepository> _mockExcelRepository;
        private const string TestFilePath = "test.xlsx";
        private const uint TestRecipientId = 0x00030101;

        public ExcelStemProtocolRepositoryTests()
        {
            _mockExcelRepository = new Mock<IExcelRepository>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullExcelRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                CreateRepository(null!, TestFilePath, TestRecipientId));

            Assert.IsType<ArgumentNullException>(exception.InnerException);
            Assert.Equal("excelRepository", ((ArgumentNullException)exception.InnerException!).ParamName);
        }

        [Fact]
        public void Constructor_NullFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                CreateRepository(_mockExcelRepository.Object, null!, TestRecipientId));

            Assert.IsType<ArgumentException>(exception.InnerException);
            Assert.Equal("excelFilePath", ((ArgumentException)exception.InnerException!).ParamName);
        }

        [Fact]
        public void Constructor_EmptyFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                CreateRepository(_mockExcelRepository.Object, "", TestRecipientId));

            Assert.IsType<ArgumentException>(exception.InnerException);
            Assert.Equal("excelFilePath", ((ArgumentException)exception.InnerException!).ParamName);
        }

        [Fact]
        public void Constructor_WhitespaceFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                CreateRepository(_mockExcelRepository.Object, "   ", TestRecipientId));

            Assert.IsType<ArgumentException>(exception.InnerException);
            Assert.Equal("excelFilePath", ((ArgumentException)exception.InnerException!).ParamName);
        }

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            // Arrange
            SetupEmptyMocks();

            // Act
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Assert
            Assert.NotNull(repository);
        }

        #endregion

        #region GetCommand Tests

        [Fact]
        public void GetCommand_ValidCommand_ReturnsCorrectValue()
        {
            // Arrange
            var commands = new List<StemCommandData>
            {
                new("Scrivi variabile logica", "01", "00")
            };
            SetupProtocolDataMock(commands);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            ushort result = repository.GetCommand("Scrivi variabile logica");

            // Assert
            Assert.Equal(0x0100, result);
        }

        [Fact]
        public void GetCommand_CaseInsensitive_ReturnsCorrectValue()
        {
            // Arrange
            var commands = new List<StemCommandData>
            {
                new("TestCommand", "AB", "CD")
            };
            SetupProtocolDataMock(commands);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act - Use different case than the original command name
            ushort result = repository.GetCommand("TESTCOMMAND");

            // Assert
            Assert.Equal(0xABCD, result);
        }

        [Fact]
        public void GetCommand_SameCase_ReturnsCorrectValue()
        {
            // Arrange
            var commands = new List<StemCommandData>
            {
                new("TestCommand", "AB", "CD")
            };
            SetupProtocolDataMock(commands);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act - Use exact same case as the original command name
            ushort result = repository.GetCommand("TestCommand");

            // Assert
            Assert.Equal(0xABCD, result);
        }

        [Fact]
        public void GetCommand_NotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            var commands = new List<StemCommandData>
            {
                new("ExistingCommand", "01", "00")
            };
            SetupProtocolDataMock(commands);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() =>
                repository.GetCommand("NonExistentCommand"));

            Assert.Contains("NonExistentCommand", exception.Message);
        }

        [Fact]
        public void GetCommand_NullName_ThrowsArgumentException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => repository.GetCommand(null!));
        }

        [Fact]
        public void GetCommand_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => repository.GetCommand(""));
        }

        [Fact]
        public void GetCommand_WhitespaceName_ThrowsArgumentException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => repository.GetCommand("   "));
        }

        [Fact]
        public void GetCommand_MultipleCommands_ReturnsCorrectOne()
        {
            // Arrange
            var commands = new List<StemCommandData>
            {
                new("Command1", "01", "00"),
                new("Command2", "02", "00"),
                new("Command3", "03", "00")
            };
            SetupProtocolDataMock(commands);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            ushort result = repository.GetCommand("Command2");

            // Assert
            Assert.Equal(0x0200, result);
        }

        #endregion

        #region GetVariable Tests

        [Fact]
        public void GetVariable_ValidVariable_ReturnsCorrectValue()
        {
            // Arrange
            var variables = new List<StemVariableData>
            {
                new("Comando Led Verde", "00", "02", "INT")
            };
            SetupDictionaryMock(variables);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            ushort result = repository.GetVariable("Comando Led Verde");

            // Assert
            Assert.Equal(0x0002, result);
        }

        [Fact]
        public void GetVariable_CaseInsensitive_ReturnsCorrectValue()
        {
            // Arrange
            var variables = new List<StemVariableData>
            {
                new("TestVariable", "AB", "CD", "INT")
            };
            SetupDictionaryMock(variables);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act - Use different case than the original variable name
            ushort result = repository.GetVariable("TESTVARIABLE");

            // Assert
            Assert.Equal(0xABCD, result);
        }

        [Fact]
        public void GetVariable_SameCase_ReturnsCorrectValue()
        {
            // Arrange
            var variables = new List<StemVariableData>
            {
                new("TestVariable", "AB", "CD", "INT")
            };
            SetupDictionaryMock(variables);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act - Use exact same case as the original variable name
            ushort result = repository.GetVariable("TestVariable");

            // Assert
            Assert.Equal(0xABCD, result);
        }

        [Fact]
        public void GetVariable_NotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            var variables = new List<StemVariableData>
            {
                new("ExistingVariable", "00", "01", "INT")
            };
            SetupDictionaryMock(variables);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() =>
                repository.GetVariable("NonExistentVariable"));

            Assert.Contains("NonExistentVariable", exception.Message);
        }

        [Fact]
        public void GetVariable_NullName_ThrowsArgumentException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => repository.GetVariable(null!));
        }

        [Fact]
        public void GetVariable_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => repository.GetVariable(""));
        }

        #endregion

        #region GetValue Tests

        [Fact]
        public void GetValue_ON_ReturnsCorrectBytes()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            byte[] result = repository.GetValue("ON");

            // Assert
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x80 }, result);
        }

        [Fact]
        public void GetValue_OFF_ReturnsCorrectBytes()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            byte[] result = repository.GetValue("OFF");

            // Assert
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, result);
        }

        [Fact]
        public void GetValue_SINGLE_BLINK_ReturnsCorrectBytes()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            byte[] result = repository.GetValue("SINGLE_BLINK");

            // Assert
            Assert.Equal(new byte[] { 0x00, 0xFF, 0x80, 0x61 }, result);
        }

        [Fact]
        public void GetValue_CaseInsensitive_ReturnsCorrectBytes()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            byte[] onResult = repository.GetValue("on");
            byte[] offResult = repository.GetValue("Off");

            // Assert
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x80 }, onResult);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, offResult);
        }

        [Fact]
        public void GetValue_NotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() =>
                repository.GetValue("UNKNOWN_VALUE"));

            Assert.Contains("UNKNOWN_VALUE", exception.Message);
        }

        [Fact]
        public void GetValue_NullName_ThrowsArgumentException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => repository.GetValue(null!));
        }

        [Fact]
        public void GetValue_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            SetupEmptyMocks();
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => repository.GetValue(""));
        }

        #endregion

        #region Lazy Loading Tests

        [Fact]
        public void GetCommand_CalledMultipleTimes_LoadsOnlyOnce()
        {
            // Arrange
            var commands = new List<StemCommandData>
            {
                new("Command1", "01", "00"),
                new("Command2", "02", "00")
            };
            SetupProtocolDataMock(commands);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            repository.GetCommand("Command1");
            repository.GetCommand("Command2");
            repository.GetCommand("Command1");

            // Assert - Excel file should only be read once due to lazy loading
            _mockExcelRepository.Verify(r =>
                r.GetProtocolDataFromFileAsync(TestFilePath), Times.Once);
        }

        [Fact]
        public void GetVariable_CalledMultipleTimes_LoadsOnlyOnce()
        {
            // Arrange
            var variables = new List<StemVariableData>
            {
                new("Var1", "00", "01", "INT"),
                new("Var2", "00", "02", "INT")
            };
            SetupDictionaryMock(variables);
            IProtocolRepository repository = CreateRepository(_mockExcelRepository.Object, TestFilePath, TestRecipientId);

            // Act
            repository.GetVariable("Var1");
            repository.GetVariable("Var2");
            repository.GetVariable("Var1");

            // Assert - Excel file should only be read once due to lazy loading
            _mockExcelRepository.Verify(r =>
                r.GetDictionaryFromFileAsync(TestFilePath, TestRecipientId), Times.Once);
        }

        #endregion

        #region Helper Methods

        private static IProtocolRepository CreateRepository(
            IExcelRepository excelRepository,
            string filePath,
            uint recipientId)
        {
            // Use reflection to create internal class
            Type? type = typeof(ExcelProtocolRepositoryFactory).Assembly
                .GetType("Data.ExcelStemProtocolRepository");

            return (IProtocolRepository)Activator.CreateInstance(
                type!,
                excelRepository,
                filePath,
                recipientId)!;
        }

        private void SetupEmptyMocks()
        {
            _mockExcelRepository.Setup(r => r.GetProtocolDataFromFileAsync(It.IsAny<string>()))
                .ReturnsAsync(new StemProtocolData
                {
                    Commands = ImmutableList<StemCommandData>.Empty,
                    Addresses = ImmutableList<StemRowData>.Empty
                });

            _mockExcelRepository.Setup(r => r.GetDictionaryFromFileAsync(It.IsAny<string>(), It.IsAny<uint>()))
                .ReturnsAsync(new StemProtocolData
                {
                    Variables = ImmutableList<StemVariableData>.Empty
                });
        }

        private void SetupProtocolDataMock(List<StemCommandData> commands)
        {
            _mockExcelRepository.Setup(r => r.GetProtocolDataFromFileAsync(TestFilePath))
                .ReturnsAsync(new StemProtocolData
                {
                    Commands = commands.ToImmutableList(),
                    Addresses = ImmutableList<StemRowData>.Empty
                });

            _mockExcelRepository.Setup(r => r.GetDictionaryFromFileAsync(It.IsAny<string>(), It.IsAny<uint>()))
                .ReturnsAsync(new StemProtocolData { Variables = ImmutableList<StemVariableData>.Empty });
        }

        private void SetupDictionaryMock(List<StemVariableData> variables)
        {
            _mockExcelRepository.Setup(r => r.GetProtocolDataFromFileAsync(It.IsAny<string>()))
                .ReturnsAsync(new StemProtocolData
                {
                    Commands = ImmutableList<StemCommandData>.Empty,
                    Addresses = ImmutableList<StemRowData>.Empty
                });

            _mockExcelRepository.Setup(r => r.GetDictionaryFromFileAsync(TestFilePath, TestRecipientId))
                .ReturnsAsync(new StemProtocolData
                {
                    Variables = variables.ToImmutableList()
                });
        }

        #endregion
    }
}
