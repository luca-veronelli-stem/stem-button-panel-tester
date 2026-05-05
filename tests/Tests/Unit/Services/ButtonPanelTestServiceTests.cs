using Core.Enums;
using Core.Interfaces.Data;
using Core.Interfaces.Services;
using Core.Models;
using Core.Models.Services;
using Core.Results;
using Moq;
using Services;
using System.Reflection;

namespace Tests.Unit.Services
{
    /// <summary>
    /// Test unitari di ButtonPanelTestService.
    /// </summary>
    public class ButtonPanelTestServiceTests
    {
        private readonly Mock<ICommunicationService> _mockCommunicationService;
        private readonly Mock<IBaptizeService> _mockBaptizeService;
        private readonly Mock<IProtocolRepository> _mockProtocolRepository;

        public ButtonPanelTestServiceTests()
        {
            _mockCommunicationService = new Mock<ICommunicationService>();
            _mockBaptizeService = new Mock<IBaptizeService>();
            _mockProtocolRepository = new Mock<IProtocolRepository>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_Throws_When_CommunicationService_Null()
        {
            ICommunicationService nullCommunicationService = null!;

            var exception = Assert.Throws<ArgumentNullException>(() =>
                new ButtonPanelTestService(nullCommunicationService, _mockBaptizeService.Object, _mockProtocolRepository.Object));

            Assert.Equal("communicationService", exception.ParamName);
        }

        [Fact]
        public void Constructor_Throws_When_BaptizeService_Null()
        {
            IBaptizeService nullBaptizeService = null!;

            var exception = Assert.Throws<ArgumentNullException>(() =>
                new ButtonPanelTestService(_mockCommunicationService.Object, nullBaptizeService, _mockProtocolRepository.Object));

            Assert.Equal("baptizeService", exception.ParamName);
        }

        [Fact]
        public void Constructor_Throws_When_ProtocolRepository_Null()
        {
            IProtocolRepository nullProtocolRepository = null!;

            var exception = Assert.Throws<ArgumentNullException>(() =>
                new ButtonPanelTestService(_mockCommunicationService.Object, _mockBaptizeService.Object, nullProtocolRepository));

            Assert.Equal("protocolRepository", exception.ParamName);
        }

        [Fact]
        public void Constructor_Succeeds_With_Valid_Dependencies()
        {
            ButtonPanelTestService sut = null!;
            var exception = Record.Exception(() =>
            {
                sut = new ButtonPanelTestService(
                    _mockCommunicationService.Object,
                    _mockBaptizeService.Object,
                    _mockProtocolRepository.Object);
            });

            Assert.Null(exception);
            Assert.NotNull(sut);
        }

        #endregion

        #region SetProtocolRepository Tests

        [Fact]
        public void SetProtocolRepository_Throws_When_Repository_Null()
        {
            var sut = new ButtonPanelTestService(_mockCommunicationService.Object, _mockBaptizeService.Object, _mockProtocolRepository.Object);

            var exception = Assert.Throws<ArgumentNullException>(() => sut.SetProtocolRepository(null!));

            Assert.Equal("repository", exception.ParamName);
        }

        [Fact]
        public void SetProtocolRepository_Updates_Repository()
        {
            var sut = new ButtonPanelTestService(_mockCommunicationService.Object, _mockBaptizeService.Object, _mockProtocolRepository.Object);
            var newMockRepo = new Mock<IProtocolRepository>();

            sut.SetProtocolRepository(newMockRepo.Object);

            var protocolField = typeof(ButtonPanelTestService)
                .GetField("_protocolRepository", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(protocolField);
            var updatedRepo = protocolField.GetValue(sut);
            Assert.Same(newMockRepo.Object, updatedRepo);
        }

        #endregion

        #region TestAllAsync Tests

        [Fact]
        public async Task TestAllAsync_Successful_Full_Test_All_Pass()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupRepositoryMocks();
            SetupCommunicationMocks();

            int currentButtonIndex = 0;
            object lockObj = new();
            var promptSignal = new SemaphoreSlim(0);

            _mockCommunicationService.SetupAdd(m => m.CommandDecoded += It.IsAny<EventHandler<AppLayerDecoderEventArgs>>())
                .Callback<EventHandler<AppLayerDecoderEventArgs>>(handler =>
                {
                    _ = Task.Run(async () =>
                    {
                        // Wait until userPrompt is called (service is ready for button press)
                        if (!await promptSignal.WaitAsync(2000))
                            return;

                        await Task.Delay(10);

                        int buttonIndex;
                        lock (lockObj)
                        {
                            buttonIndex = currentButtonIndex;
                            if (buttonIndex < panel.ButtonMasks.Count)
                            {
                                currentButtonIndex++;
                            }
                        }

                        if (buttonIndex < panel.ButtonMasks.Count)
                        {
                            byte buttonMask = panel.ButtonMasks[buttonIndex];
                            var buttonPressPayload = new byte[] { 0x00, 0x02, 0x80, 0x3E, buttonMask };
                            var args = new AppLayerDecoderEventArgs(buttonPressPayload);
                            _mockCommunicationService.Raise(m => m.CommandDecoded += null, this, args);
                        }
                    });
                });

            Task<bool> userConfirm(string _) => Task.FromResult(true);
            Task userPrompt(string _) { promptSignal.Release(); return Task.CompletedTask; }

            var sut = new ButtonPanelTestService(
                _mockCommunicationService.Object,
                _mockBaptizeService.Object,
                _mockProtocolRepository.Object,
                null,
                TimeSpan.FromMilliseconds(500));

            // Act
            var results = await sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Passed));
        }

        [Fact]
        public async Task TestAllAsync_Fails_On_Comm_Setup()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;

            SetupRepositoryMocks();

            _mockCommunicationService.Setup(comm => comm.SetActiveChannelAsync(CommunicationChannel.Can, "250", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Failure(ErrorCodes.ConnectionFailed, "Failed to connect to channel Can."));

            static Task<bool> userConfirm(string _) => Task.FromResult(true);
            static Task userPrompt(string _) => Task.CompletedTask;

            var sut = new ButtonPanelTestService(
                _mockCommunicationService.Object,
                _mockBaptizeService.Object,
                _mockProtocolRepository.Object,
                null,
                TimeSpan.FromMilliseconds(100));

            // Act
            var results = await sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);

            // Assert
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal(ButtonPanelTestType.Complete, results[0].TestType);
            Assert.False(results[0].Passed);
        }

        [Fact]
        public async Task TestAllAsync_Interrupted_At_Buttons()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;

            SetupRepositoryMocks();
            SetupCommunicationMocks();

            using var cts = new CancellationTokenSource();

            Task userPrompt(string _)
            {
                cts.Cancel();
                return Task.CompletedTask;
            }

            Task<bool> userConfirm(string _) => Task.FromResult(true);

            var sut = new ButtonPanelTestService(
                _mockCommunicationService.Object,
                _mockBaptizeService.Object,
                _mockProtocolRepository.Object,
                null,
                TimeSpan.FromMilliseconds(100));

            // Act
            var results = await sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null, cts.Token);

            // Assert
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.True(results[0].Interrupted || !results[0].Passed);
        }

        #endregion

        #region TestButtonsAsync Tests

        [Fact]
        [Trait("Category", TestCategories.FlakyOnCi)]
        public async Task TestButtonsAsync_All_Buttons_Pass()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupCommunicationMocks();

            int currentButtonIndex = 0;
            object lockObj = new();
            var promptSignal = new SemaphoreSlim(0);

            // When the service subscribes to CommandDecoded, wait for prompt signal then send button press
            _mockCommunicationService.SetupAdd(m => m.CommandDecoded += It.IsAny<EventHandler<AppLayerDecoderEventArgs>>())
                .Callback<EventHandler<AppLayerDecoderEventArgs>>(handler =>
                {
                    _ = Task.Run(async () =>
                    {
                        // Wait until userPrompt is called (service is ready for button press)
                        if (!await promptSignal.WaitAsync(2000))
                            return;

                        await Task.Delay(10);

                        int buttonIndex;
                        lock (lockObj)
                        {
                            buttonIndex = currentButtonIndex;
                            if (buttonIndex < panel.ButtonMasks.Count)
                            {
                                currentButtonIndex++;
                            }
                        }

                        if (buttonIndex < panel.ButtonMasks.Count)
                        {
                            byte buttonMask = panel.ButtonMasks[buttonIndex];
                            var buttonPressPayload = new byte[] { 0x00, 0x02, 0x80, 0x3E, buttonMask };
                            var args = new AppLayerDecoderEventArgs(buttonPressPayload);
                            _mockCommunicationService.Raise(m => m.CommandDecoded += null, this, args);
                        }
                    });
                });

            Task userPrompt(string _) { promptSignal.Release(); return Task.CompletedTask; }

            var sut = new ButtonPanelTestService(
                _mockCommunicationService.Object,
                _mockBaptizeService.Object,
                _mockProtocolRepository.Object,
                null,
                TimeSpan.FromMilliseconds(500));

            // Act
            var result = await sut.TestButtonsAsync(panelType, userPrompt, null, null);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Passed);
            Assert.False(result.Interrupted);
        }

        [Fact]
        public async Task TestButtonsAsync_Interrupted_By_User()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;

            SetupCommunicationMocks();

            static Task userPrompt(string _) => throw new OperationCanceledException();

            var sut = new ButtonPanelTestService(
                _mockCommunicationService.Object,
                _mockBaptizeService.Object,
                _mockProtocolRepository.Object,
                null,
                TimeSpan.FromMilliseconds(100));

            // Act
            var result = await sut.TestButtonsAsync(panelType, userPrompt, null, null);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Interrupted);
            Assert.False(result.Passed);
        }

        #endregion

        #region TestLedAsync Tests

        [Fact]
        public async Task TestLedAsync_Passes_With_All_Confirms_True()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;

            SetupRepositoryMocks();
            SetupCommunicationMocks();

            static Task<bool> userConfirm(string _) => Task.FromResult(true);

            var sut = new ButtonPanelTestService(
                _mockCommunicationService.Object,
                _mockBaptizeService.Object,
                _mockProtocolRepository.Object);

            // Act
            var result = await sut.TestLedAsync(panelType, userConfirm);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Passed);
            Assert.False(result.Interrupted);
        }

        [Fact]
        public async Task TestLedAsync_No_Led_On_Panel_Skips_Test()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0025205;
            var panel = ButtonPanel.GetByType(panelType);

            Assert.False(panel.HasLed);

            static Task<bool> userConfirm(string _) => Task.FromResult(true);

            var sut = new ButtonPanelTestService(
                _mockCommunicationService.Object,
                _mockBaptizeService.Object,
                _mockProtocolRepository.Object);

            // Act
            var result = await sut.TestLedAsync(panelType, userConfirm);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Passed);
            Assert.Contains("Skipped", result.Message);
        }

        #endregion

        #region Helper Methods

        private void SetupRepositoryMocks()
        {
            _mockProtocolRepository.Setup(repo => repo.GetCommand("Scrivi variabile logica")).Returns((ushort)0x0100);
            _mockProtocolRepository.Setup(repo => repo.GetVariable("Comando Led Verde")).Returns((ushort)0x0002);
            _mockProtocolRepository.Setup(repo => repo.GetVariable("Comando Led Rosso")).Returns((ushort)0x0003);
            _mockProtocolRepository.Setup(repo => repo.GetVariable("Comando Buzzer")).Returns((ushort)0x0004);
            _mockProtocolRepository.Setup(repo => repo.GetValue("ON")).Returns([0x00, 0x00, 0x00, 0x80]);
            _mockProtocolRepository.Setup(repo => repo.GetValue("OFF")).Returns([0x00, 0x00, 0x00, 0x00]);
            _mockProtocolRepository.Setup(repo => repo.GetValue("SINGLE_BLINK")).Returns([0x00, 0xFF, 0x80, 0x61]);
        }

        private void SetupCommunicationMocks()
        {
            _mockCommunicationService.Setup(comm => comm.SetActiveChannelAsync(
                CommunicationChannel.Can,
                "250",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());

            _mockCommunicationService.Setup(comm => comm.IsChannelConnected())
                .Returns(true);

            _mockCommunicationService.Setup(comm => comm.SendCommandAsync(
                It.IsAny<ushort>(),
                It.IsAny<byte[]>(),
                It.IsAny<bool>(),
                It.IsAny<Func<byte[], bool>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<byte[]>.Success([]));
        }

        #endregion
    }
}
