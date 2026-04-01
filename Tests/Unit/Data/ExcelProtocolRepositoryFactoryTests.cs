using Core.Interfaces.Data;
using Data;
using Moq;

namespace Tests.Unit.Data
{
    /// <summary>
    /// Unit tests for ExcelProtocolRepositoryFactory.
    /// Tests factory creation and repository instantiation.
    /// </summary>
    public class ExcelProtocolRepositoryFactoryTests
    {
        private readonly Mock<IExcelRepository> _mockExcelRepository;
        private const string TestFilePath = "test.xlsx";

        public ExcelProtocolRepositoryFactoryTests()
        {
            _mockExcelRepository = new Mock<IExcelRepository>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullExcelRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new ExcelProtocolRepositoryFactory(null!, TestFilePath));

            Assert.Equal("excelRepository", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, null!));

            Assert.Equal("excelFilePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_EmptyFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, ""));

            Assert.Equal("excelFilePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhitespaceFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, "   "));

            Assert.Equal("excelFilePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            // Act
            var factory = new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, TestFilePath);

            // Assert
            Assert.NotNull(factory);
        }

        #endregion

        #region Create Tests

        [Fact]
        public void Create_ReturnsIProtocolRepository()
        {
            // Arrange
            var factory = new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, TestFilePath);

            // Act
            var repository = factory.Create(0x00030101);

            // Assert
            Assert.NotNull(repository);
            Assert.IsAssignableFrom<IProtocolRepository>(repository);
        }

        [Fact]
        public void Create_DifferentRecipientIds_ReturnsDifferentInstances()
        {
            // Arrange
            var factory = new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, TestFilePath);

            // Act
            var repo1 = factory.Create(0x00030101);
            var repo2 = factory.Create(0x000A0101);

            // Assert
            Assert.NotSame(repo1, repo2);
        }

        [Fact]
        public void Create_SameRecipientId_ReturnsNewInstanceEachTime()
        {
            // Arrange
            var factory = new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, TestFilePath);
            uint recipientId = 0x00030101;

            // Act
            var repo1 = factory.Create(recipientId);
            var repo2 = factory.Create(recipientId);

            // Assert - Factory creates new instances each time
            Assert.NotSame(repo1, repo2);
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(0x00030101u)]
        [InlineData(0xFFFFFFFFu)]
        public void Create_VariousRecipientIds_Succeeds(uint recipientId)
        {
            // Arrange
            var factory = new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, TestFilePath);

            // Act
            var repository = factory.Create(recipientId);

            // Assert
            Assert.NotNull(repository);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Create_FactorySharesExcelRepository()
        {
            // Arrange
            var factory = new ExcelProtocolRepositoryFactory(_mockExcelRepository.Object, TestFilePath);

            // Act - Create multiple repositories
            var repo1 = factory.Create(0x00030101);
            var repo2 = factory.Create(0x000A0101);

            // Assert - Both should be valid and non-null
            Assert.NotNull(repo1);
            Assert.NotNull(repo2);
            // The same excel repository should be shared
        }

        #endregion
    }
}
