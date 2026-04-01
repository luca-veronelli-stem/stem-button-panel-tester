using Communication;
using Communication.Protocol;
using Core.Enums;
using Core.Interfaces.Communication;
using Core.Interfaces.Infrastructure;
using Core.Interfaces.Services;
using Core.Models.Communication;
using Core.Models.Services;
using Data;
using Moq;
using Services;

namespace Tests.EndToEnd.Services
{
    /// <summary>
    /// End-to-end tests for ButtonPanelTestService.
    /// These tests use all real components (services, protocol manager, repositories, CAN manager)
    /// and only mock the view (user interactions) and the hardware adapter (ICanAdapter).
    /// </summary>
    public class ButtonPanelTestServiceE2ETests : IAsyncLifetime
    {
        private readonly Mock<ICanAdapter> _mockAdapter;
        private readonly Mock<IBaptizeService> _mockBaptizeService;
        private readonly CanCommunicationManager _canManager;
        private readonly StemProtocolManager _protocolManager;
        private readonly CommunicationService _communicationService;
        private readonly ExcelRepository _excelRepository;
        private readonly string _excelFilePath;
        private readonly ButtonPanelTestService _sut;

        // Test state
        private ButtonPanel? _simulatedPanel;
        private int _currentButtonIndex;
        private HashSet<int> _skipButtonIndices = [];
        private readonly object _lockObj = new();
        private CancellationTokenSource? _buttonSimulationCts;
        private bool _failConnection;
        private readonly List<(uint ArbitrationId, byte[] Data)> _sentMessages = [];
        private static int _packetId = 1;

        public ButtonPanelTestServiceE2ETests()
        {
            // Set up mock for the CAN adapter
            _mockAdapter = new Mock<ICanAdapter>();
            _mockBaptizeService = new Mock<IBaptizeService>();

            // Create real CAN communication manager with mocked adapter
            _canManager = new CanCommunicationManager(_mockAdapter.Object);

            // Create real communication service
            _protocolManager = new StemProtocolManager();
            var managerFactory = new TestCanManagerFactory(_canManager);
            _communicationService = new CommunicationService(_protocolManager, managerFactory);

            // Create real Excel repository
            _excelRepository = new ExcelRepository();
            _excelFilePath = Path.Combine("Resources", "StemDictionaries.xlsx");

            // Create real protocol repository for the first panel type
            var factory = new ExcelProtocolRepositoryFactory(_excelRepository, _excelFilePath);
            var protocolRepository = factory.Create(GetRecipientIdForPanel(ButtonPanelType.DIS0023789));

            _sut = new ButtonPanelTestService(
                _communicationService,
                _mockBaptizeService.Object,
                protocolRepository,
                null, // logger
                TimeSpan.FromMilliseconds(500)); // Short timeout for tests
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            _buttonSimulationCts?.Cancel();
            _buttonSimulationCts?.Dispose();
            await _canManager.DisposeAsync();
        }

        #region Test Setup Helpers

        private void ConfigureForFullTest(ButtonPanel panel, int[]? skipButtonIndices = null)
        {
            _simulatedPanel = panel;
            _currentButtonIndex = 0;
            _skipButtonIndices = skipButtonIndices?.ToHashSet() ?? [];
            _sentMessages.Clear();
            _failConnection = false;

            // Configure mock adapter for successful connection
            _mockAdapter.Setup(a => a.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    if (_failConnection) return false;
                    _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, true);
                    return true;
                });

            _mockAdapter.Setup(a => a.DisconnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, false));

            _mockAdapter.SetupGet(a => a.IsConnected).Returns(() => !_failConnection);

            // Configure Send to capture messages and simulate responses
            _mockAdapter.Setup(a => a.Send(It.IsAny<uint>(), It.IsAny<byte[]>(), It.IsAny<bool>()))
                .Callback<uint, byte[], bool>((arbId, data, _) =>
                {
                    _sentMessages.Add((arbId, data.ToArray()));
                    // Simulate command response asynchronously
                    Task.Run(async () =>
                    {
                        await Task.Delay(10);
                        SimulateCommandResponse();
                    });
                })
                .ReturnsAsync(true);

            // Start button simulation
            SetupButtonSimulation();
        }

        private void SetupButtonSimulation()
        {
            _buttonSimulationCts?.Cancel();
            _buttonSimulationCts?.Dispose();
            _buttonSimulationCts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Initial delay

                while (!_buttonSimulationCts.Token.IsCancellationRequested && _simulatedPanel != null)
                {
                    int buttonIndex;
                    lock (_lockObj)
                    {
                        buttonIndex = _currentButtonIndex;
                        if (buttonIndex >= _simulatedPanel.ButtonMasks.Count)
                            return;
                        _currentButtonIndex++;
                    }

                    if (!_skipButtonIndices.Contains(buttonIndex))
                    {
                        var buttonMask = _simulatedPanel.ButtonMasks[buttonIndex];
                        // Payload format from button panel: [0x00, 0x02, 0x80, 0x3E, buttonMask]
                        var appPayload = new byte[] { 0x00, 0x02, 0x80, 0x3E, buttonMask };
                        SimulateReceivedPacket(appPayload);

                        try
                        {
                            await Task.Delay(50, _buttonSimulationCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                    else
                    {
                        try
                        {
                            await Task.Delay(550, _buttonSimulationCts.Token); // Wait for timeout
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
            }, _buttonSimulationCts.Token);
        }

        private void SimulateCommandResponse()
        {
            var appPayload = new byte[] { 0x01, 0x00, 0x00, 0x00 }; // ACK response
            SimulateReceivedPacket(appPayload);
        }

        private void SimulateReceivedPacket(byte[] appPayload)
        {
            var rawPacket = BuildProtocolPacket(appPayload);
            var packet = new CanPacket(0x100, false, rawPacket, (ulong)DateTime.UtcNow.Ticks);
            _mockAdapter.Raise(a => a.PacketReceived += null, _mockAdapter.Object, packet);
        }

        private void Reset()
        {
            _buttonSimulationCts?.Cancel();
            _buttonSimulationCts?.Dispose();
            _buttonSimulationCts = null;
            _sentMessages.Clear();
            _failConnection = false;
            _simulatedPanel = null;
            _currentButtonIndex = 0;
            _skipButtonIndices.Clear();
        }

        private static byte[] BuildProtocolPacket(byte[] appPayload)
        {
            byte cryptType = 0x00;
            uint senderId = 0;
            ushort lPack = (ushort)appPayload.Length;

            // Build transport header with big-endian values (matching original protocol)
            var transportHeader = new List<byte> { cryptType };
            transportHeader.AddRange(ToBigEndianBytes(senderId));
            transportHeader.AddRange(ToBigEndianBytes(lPack));

            // CRC is calculated over header + appPayload
            var dataForCrc = transportHeader.Concat(appPayload).ToArray();
            ushort crcValue = CalculateCrc16(dataForCrc);
            // CRC stored as big-endian (matching real hardware)
            var crcBytes = ToBigEndianBytes(crcValue);

            var transportPacket = new List<byte>();
            transportPacket.AddRange(transportHeader);
            transportPacket.AddRange(appPayload);
            transportPacket.AddRange(crcBytes);

            // NetInfo uses little-endian (BitConverter.GetBytes behavior)
            int packetId = Interlocked.Increment(ref _packetId) % 7 + 1;
            ushort netInfoValue = (ushort)(
                (0 << 6) |
                (0 << 5) |
                (packetId << 2) |
                0
            );
            var netInfoBytes = ToLittleEndianBytes(netInfoValue);

            var rawPacket = new List<byte>();
            rawPacket.AddRange(netInfoBytes);
            rawPacket.AddRange(transportPacket);

            return [.. rawPacket];
        }

        private static byte[] ToLittleEndianBytes(ushort value) =>
            [(byte)value, (byte)(value >> 8)];

        private static byte[] ToBigEndianBytes(ushort value) =>
            [(byte)(value >> 8), (byte)value];

        private static byte[] ToBigEndianBytes(uint value) =>
            [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

        private static ushort CalculateCrc16(byte[] data)
        {
            // Use CRC-16 Modbus algorithm (same as ProtocolHelpers.CalculateCrc)
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }

        private void SetupProtocolRepositoryForPanel(ButtonPanelType panelType)
        {
            var recipientId = GetRecipientIdForPanel(panelType);
            var factory = new ExcelProtocolRepositoryFactory(_excelRepository, _excelFilePath);
            var repository = factory.Create(recipientId);
            _sut.SetProtocolRepository(repository);
        }

        private static uint GetRecipientIdForPanel(ButtonPanelType panelType) => panelType switch
        {
            ButtonPanelType.DIS0023789 => 0x00030101,
            ButtonPanelType.DIS0025205 => 0x000A0101,
            ButtonPanelType.DIS0026166 => 0x000B0101,
            ButtonPanelType.DIS0026182 => 0x000C0101,
            _ => 0x00000000
        };

        #endregion

        #region Full Workflow E2E Tests

        /// <summary>
        /// End-to-end test: Complete workflow for 8-button panel with LED using real CAN manager.
        /// </summary>
        [Fact]
        public async Task E2E_CompleteWorkflow_8ButtonPanelWithLed_AllTestsPass()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            ConfigureForFullTest(panel);

            var userInteractions = new List<string>();
            Func<string, Task> mockUserPrompt = msg =>
            {
                userInteractions.Add($"PROMPT: {msg}");
                return Task.CompletedTask;
            };
            Func<string, Task<bool>> mockUserConfirm = msg =>
            {
                userInteractions.Add($"CONFIRM: {msg}");
                return Task.FromResult(true);
            };

            var buttonCallbacks = new List<(int index, bool passed)>();
            Action<int, bool> onButtonResult = (i, p) => buttonCallbacks.Add((i, p));

            // Act
            var results = await _sut.TestAllAsync(
                panelType,
                mockUserConfirm,
                mockUserPrompt,
                null,
                onButtonResult);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Passed, $"Test {r.TestType} failed: {r.Message}"));

            Assert.Equal(panel.ButtonCount, buttonCallbacks.Count);
            Assert.All(buttonCallbacks, b => Assert.True(b.passed));

            Assert.Equal(panel.ButtonCount, userInteractions.Count(i => i.StartsWith("PROMPT:")));
            Assert.Equal(6, userInteractions.Count(i => i.StartsWith("CONFIRM:"))); // 5 LED + 1 buzzer

            // Verify adapter was used
            _mockAdapter.Verify(a => a.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            Assert.NotEmpty(_sentMessages);
        }

        /// <summary>
        /// End-to-end test: Complete workflow for 4-button panel without LED.
        /// </summary>
        [Fact]
        public async Task E2E_CompleteWorkflow_4ButtonPanelNoLed_ButtonsAndBuzzerOnly()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0025205;
            var panel = ButtonPanel.GetByType(panelType);
            Assert.False(panel.HasLed);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            var confirmCalls = new List<string>();
            Func<string, Task<bool>> mockUserConfirm = msg =>
            {
                confirmCalls.Add(msg);
                return Task.FromResult(true);
            };
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(ButtonPanelTestType.Buttons, results[0].TestType);
            Assert.Equal(ButtonPanelTestType.Buzzer, results[1].TestType);
            Assert.All(results, r => Assert.True(r.Passed));

            Assert.Single(confirmCalls);
            Assert.Contains("buzzer", confirmCalls[0], StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// End-to-end test: Panel types with available protocol data complete workflow.
        /// </summary>
        [Theory]
        [InlineData(ButtonPanelType.DIS0023789, 8, true)]
        [InlineData(ButtonPanelType.DIS0025205, 4, false)]
        [InlineData(ButtonPanelType.DIS0026166, 8, true)]
        public async Task E2E_PanelTypesWithProtocolData_CompleteWorkflowSucceeds(
            ButtonPanelType panelType, int expectedButtonCount, bool hasLed)
        {
            // Arrange
            var panel = ButtonPanel.GetByType(panelType);
            Assert.Equal(expectedButtonCount, panel.ButtonCount);
            Assert.Equal(hasLed, panel.HasLed);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            Func<string, Task<bool>> mockUserConfirm = _ => Task.FromResult(true);
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            var buttonResults = new List<bool>();
            Action<int, bool> onButtonResult = (_, p) => buttonResults.Add(p);

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, onButtonResult);

            // Assert
            var expectedTestCount = hasLed ? 3 : 2;
            Assert.Equal(expectedTestCount, results.Count);
            Assert.All(results, r => Assert.True(r.Passed, $"Test {r.TestType} failed: {r.Message}"));
            Assert.Equal(expectedButtonCount, buttonResults.Count);
            Assert.All(buttonResults, Assert.True);
        }

        #endregion

        #region Button Test E2E Tests

        /// <summary>
        /// End-to-end test: Button timeout with real CAN manager.
        /// </summary>
        [Fact]
        public async Task E2E_ButtonTimeout_ReportsFailureWithRealCanManager()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel, skipButtonIndices: [3]);

            var buttonResults = new List<(int index, bool passed)>();
            Action<int, bool> onButtonResult = (i, p) => buttonResults.Add((i, p));

            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;
            Func<string, Task<bool>> mockUserConfirm = _ => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, onButtonResult);

            // Assert
            var buttonResult = results.First(r => r.TestType == ButtonPanelTestType.Buttons);
            Assert.False(buttonResult.Passed);
            Assert.Contains("FALLITO", buttonResult.Message);

            var failedButton = buttonResults.First(r => r.index == 3);
            Assert.False(failedButton.passed);
            Assert.Equal(7, buttonResults.Count(r => r.passed));
        }

        /// <summary>
        /// End-to-end test: Button press detection uses real CAN manager and protocol stack.
        /// </summary>
        [Fact]
        public async Task E2E_ButtonPress_RealCanManagerAndProtocolDecodingWorks()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0026166;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            var decodedCommands = new List<byte[]>();
            _communicationService.CommandDecoded += (_, e) => decodedCommands.Add(e.Payload.ToArray());

            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;
            Func<string, Task<bool>> mockUserConfirm = _ => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            var buttonResult = results.First(r => r.TestType == ButtonPanelTestType.Buttons);
            Assert.True(buttonResult.Passed);

            Assert.NotEmpty(decodedCommands);
            var buttonCommands = decodedCommands.Where(c => c.Length >= 5 && c[0] == 0x00 && c[1] == 0x02).ToList();
            Assert.Equal(panel.ButtonCount, buttonCommands.Count);
        }

        #endregion

        #region LED Test E2E Tests

        /// <summary>
        /// End-to-end test: LED commands go through real CAN manager.
        /// </summary>
        [Fact]
        public async Task E2E_LedTest_UsesRealCanManager()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            int confirmCount = 0;
            Func<string, Task<bool>> mockUserConfirm = _ =>
            {
                confirmCount++;
                return Task.FromResult(true);
            };
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            var ledResult = results.First(r => r.TestType == ButtonPanelTestType.Led);
            Assert.True(ledResult.Passed);
            Assert.True(confirmCount >= 5);

            // Verify Send was called on adapter
            _mockAdapter.Verify(a => a.Send(It.IsAny<uint>(), It.IsAny<byte[]>(), It.IsAny<bool>()), Times.AtLeast(6));
        }

        /// <summary>
        /// End-to-end test: LED partial failure when user rejects some confirmations.
        /// </summary>
        [Fact]
        public async Task E2E_LedTest_PartialFailure_UserRejectsSome()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            int confirmCount = 0;
            Func<string, Task<bool>> mockUserConfirm = _ =>
            {
                confirmCount++;
                return Task.FromResult(confirmCount != 1 && confirmCount != 3);
            };
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            var ledResult = results.First(r => r.TestType == ButtonPanelTestType.Led);
            Assert.False(ledResult.Passed);
            Assert.False(ledResult.Interrupted);
            Assert.Contains("PASSATO", ledResult.Message);
            Assert.Contains("FALLITO", ledResult.Message);
        }

        #endregion

        #region Buzzer Test E2E Tests

        /// <summary>
        /// End-to-end test: Buzzer command goes through real CAN manager.
        /// </summary>
        [Fact]
        public async Task E2E_BuzzerTest_UsesRealCanManager()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            int confirmCount = 0;
            Func<string, Task<bool>> mockUserConfirm = _ =>
            {
                confirmCount++;
                return Task.FromResult(true);
            };
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            var buzzerResult = results.First(r => r.TestType == ButtonPanelTestType.Buzzer);
            Assert.True(buzzerResult.Passed);
            Assert.Contains("PASSATO", buzzerResult.Message);
            Assert.NotEmpty(_sentMessages);
        }

        /// <summary>
        /// End-to-end test: Buzzer failure when user doesn't hear it.
        /// </summary>
        [Fact]
        public async Task E2E_BuzzerTest_UserDoesNotHear_ReportsFailure()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            int confirmCount = 0;
            Func<string, Task<bool>> mockUserConfirm = _ =>
            {
                confirmCount++;
                return Task.FromResult(confirmCount <= 5); // Fail buzzer (6th)
            };
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            var buzzerResult = results.First(r => r.TestType == ButtonPanelTestType.Buzzer);
            Assert.False(buzzerResult.Passed);
            Assert.Contains("FALLITO", buzzerResult.Message);
        }

        #endregion

        #region Error Handling E2E Tests

        /// <summary>
        /// End-to-end test: CAN connection failure is handled gracefully.
        /// </summary>
        [Fact]
        public async Task E2E_CanConnectionFailure_ReturnsErrorResult()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            Reset();
            _failConnection = true;

            _mockAdapter.Setup(a => a.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, false);
                    return false;
                });

            _mockAdapter.SetupGet(a => a.IsConnected).Returns(false);

            Func<string, Task<bool>> mockUserConfirm = _ => Task.FromResult(true);
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            Assert.Single(results);
            Assert.False(results[0].Passed);
            Assert.Contains("Impossibile impostare il canale", results[0].Message);
        }

        /// <summary>
        /// End-to-end test: Cancellation during button test stops workflow.
        /// </summary>
        [Fact]
        public async Task E2E_Cancellation_StopsWorkflowGracefully()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            using var cts = new CancellationTokenSource();
            int promptCount = 0;
            Func<string, Task> mockUserPrompt = _ =>
            {
                promptCount++;
                if (promptCount >= 3)
                    cts.Cancel();
                return Task.CompletedTask;
            };
            Func<string, Task<bool>> mockUserConfirm = _ => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(
                panelType,
                mockUserConfirm,
                mockUserPrompt,
                null,
                null,
                cts.Token);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Interrupted);
            Assert.False(results[0].Passed);
        }

        /// <summary>
        /// End-to-end test: Missing protocol variables are handled gracefully.
        /// </summary>
        [Fact]
        public async Task E2E_MissingProtocolVariables_ReturnsErrorResult()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0026182;
            var panel = ButtonPanel.GetByType(panelType);

            SetupProtocolRepositoryForPanel(panelType);
            Reset();
            ConfigureForFullTest(panel);

            Func<string, Task<bool>> mockUserConfirm = _ => Task.FromResult(true);
            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);

            // Assert
            var buttonResult = results.FirstOrDefault(r => r.TestType == ButtonPanelTestType.Buttons);
            Assert.NotNull(buttonResult);
            Assert.True(buttonResult.Passed);

            var ledResult = results.FirstOrDefault(r => r.TestType == ButtonPanelTestType.Led);
            Assert.NotNull(ledResult);
            Assert.False(ledResult.Passed);
            Assert.Contains("not found", ledResult.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Sequential Operations E2E Tests

        /// <summary>
        /// End-to-end test: Multiple panels tested sequentially with real CAN manager.
        /// </summary>
        [Fact]
        public async Task E2E_SequentialPanelTests_RealCanManager()
        {
            // Arrange
            var panelTypes = new[]
            {
                ButtonPanelType.DIS0023789,
                ButtonPanelType.DIS0025205,
                ButtonPanelType.DIS0026166
            };

            Func<string, Task> mockUserPrompt = _ => Task.CompletedTask;
            Func<string, Task<bool>> mockUserConfirm = _ => Task.FromResult(true);

            var allResults = new List<List<ButtonPanelTestResult>>();

            // Act
            foreach (var panelType in panelTypes)
            {
                var panel = ButtonPanel.GetByType(panelType);
                SetupProtocolRepositoryForPanel(panelType);
                Reset();
                ConfigureForFullTest(panel);

                var results = await _sut.TestAllAsync(panelType, mockUserConfirm, mockUserPrompt, null, null);
                allResults.Add(results);
            }

            // Assert
            Assert.Equal(3, allResults.Count);

            Assert.Equal(3, allResults[0].Count);
            Assert.All(allResults[0], r => Assert.True(r.Passed));

            Assert.Equal(2, allResults[1].Count);
            Assert.All(allResults[1], r => Assert.True(r.Passed));

            Assert.Equal(3, allResults[2].Count);
            Assert.All(allResults[2], r => Assert.True(r.Passed));
        }

        #endregion
    }

    #region Factory for CAN Communication Manager

    /// <summary>
    /// Factory that returns the provided CanCommunicationManager for CAN channel.
    /// </summary>
    internal class TestCanManagerFactory : ICommunicationManagerFactory
    {
        private readonly CanCommunicationManager _manager;

        public TestCanManagerFactory(CanCommunicationManager manager)
        {
            _manager = manager;
        }

        public ICommunicationManager Create(CommunicationChannel channel)
        {
            if (channel == CommunicationChannel.Can)
                return _manager;
            throw new NotSupportedException($"Channel {channel} not supported in E2E tests");
        }
    }

    #endregion
}
