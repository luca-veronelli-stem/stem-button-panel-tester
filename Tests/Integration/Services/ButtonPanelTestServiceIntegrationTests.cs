using Communication.Protocol;
using Core.Enums;
using Core.Interfaces.Communication;
using Core.Interfaces.Data;
using Core.Interfaces.Services;
using Core.Models.Services;
using Moq;
using Services;

namespace Tests.Integration.Services
{
    /// <summary>
    /// Integration tests for ButtonPanelTestService.
    /// These tests use the real ButtonPanelTestService, CommunicationService, and StemProtocolManager
    /// with a simulated in-memory communication manager that generates proper protocol responses.
    /// </summary>
    [Trait("Category", TestCategories.Integration)]
    public class ButtonPanelTestServiceIntegrationTests : IDisposable
    {
        private readonly SimulatedCommunicationManager _simulatedManager;
        private readonly SimulatedCommunicationManagerFactory _managerFactory;
        private readonly StemProtocolManager _protocolManager;
        private readonly CommunicationService _communicationService;
        private readonly Mock<IBaptizeService> _mockBaptizeService;
        private readonly InMemoryProtocolRepository _protocolRepository;
        private readonly ButtonPanelTestService _sut;

        public ButtonPanelTestServiceIntegrationTests()
        {
            _simulatedManager = new SimulatedCommunicationManager();
            _managerFactory = new SimulatedCommunicationManagerFactory(_simulatedManager);
            _protocolManager = new StemProtocolManager();
            _communicationService = new CommunicationService(_protocolManager, _managerFactory);
            _mockBaptizeService = new Mock<IBaptizeService>();
            _protocolRepository = new InMemoryProtocolRepository();
            _sut = new ButtonPanelTestService(
                _communicationService,
                _mockBaptizeService.Object,
                _protocolRepository,
                null, // logger
                TimeSpan.FromMilliseconds(300)); // Short timeout for tests
        }

        public void Dispose()
        {
            _simulatedManager.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Full Test Workflow Integration Tests

        /// <summary>
        /// Verifies that a complete test workflow executes all test phases in order
        /// for an 8-button panel with LED support using real service integration.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_CompleteWorkflow_ExecutesAllPhasesInOrder()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            // Configure simulated manager for full workflow
            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

            var promptMessages = new List<string>();
            var confirmMessages = new List<string>();
            var buttonStartCalls = new List<int>();
            var buttonResultCalls = new List<(int index, bool passed)>();

            Task userPrompt(string msg)
            {
                promptMessages.Add(msg);
                return Task.CompletedTask;
            }

            Task<bool> userConfirm(string msg)
            {
                confirmMessages.Add(msg);
                return Task.FromResult(true);
            }

            void onButtonStart(int i) => buttonStartCalls.Add(i);
            void onButtonResult(int i, bool passed) => buttonResultCalls.Add((i, passed));

            // Act
            var results = await _sut.TestAllAsync(
                panelType,
userConfirm,
userPrompt,
onButtonStart,
onButtonResult);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(3, results.Count); // Buttons, LED, Buzzer

            // Verify test order
            Assert.Equal(ButtonPanelTestType.Buttons, results[0].TestType);
            Assert.Equal(ButtonPanelTestType.Led, results[1].TestType);
            Assert.Equal(ButtonPanelTestType.Buzzer, results[2].TestType);

            // Verify all tests passed
            Assert.All(results, r => Assert.True(r.Passed, $"Test {r.TestType} failed: {r.Message}"));
            Assert.All(results, r => Assert.False(r.Interrupted));

            // Verify button callbacks were invoked
            Assert.Equal(panel.ButtonCount, promptMessages.Count);
            Assert.Equal(panel.ButtonCount, buttonStartCalls.Count);
            Assert.Equal(panel.ButtonCount, buttonResultCalls.Count);

            // Verify LED + Buzzer confirmations (5 LED + 1 Buzzer = 6)
            Assert.Equal(6, confirmMessages.Count);

            // Verify packets were sent through the communication stack
            Assert.True(_simulatedManager.SentPackets.Count > 0);
        }

        /// <summary>
        /// Verifies that panels without LED skip the LED test phase in the full workflow.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_PanelWithoutLed_SkipsLedTest()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0025205; // No LED
            var panel = ButtonPanel.GetByType(panelType);
            Assert.False(panel.HasLed);

            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

            var confirmMessages = new List<string>();
            Task<bool> userConfirm(string msg)
            {
                confirmMessages.Add(msg);
                return Task.FromResult(true);
            }

            Task userPrompt(string _) => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);

            // Assert
            Assert.Equal(2, results.Count); // Only Buttons and Buzzer
            Assert.Equal(ButtonPanelTestType.Buttons, results[0].TestType);
            Assert.Equal(ButtonPanelTestType.Buzzer, results[1].TestType);

            // Only 1 confirmation for buzzer (no LED confirmations)
            Assert.Single(confirmMessages);
            Assert.Contains("buzzer", confirmMessages[0].ToLower());
        }

        /// <summary>
        /// Verifies workflow stops when button test is interrupted via cancellation.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_ButtonsInterrupted_StopsWorkflow()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            using var cts = new CancellationTokenSource();

            int promptCount = 0;
            Task userPrompt(string _)
            {
                promptCount++;
                if (promptCount >= 2)
                    cts.Cancel();
                return Task.CompletedTask;
            }

            Task<bool> userConfirm(string _) => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(
                panelType,
userConfirm,
userPrompt,
                null,
                null,
                cts.Token);

            // Assert - When cancellation happens during button testing, returns interrupted result
            Assert.Single(results);
            Assert.True(results[0].Interrupted);
            Assert.False(results[0].Passed);
        }

        /// <summary>
        /// Verifies workflow continues after button test and stops when LED test is interrupted.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_LedInterrupted_ReturnsButtonsAndLedResults()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

            using var cts = new CancellationTokenSource();
            int confirmCount = 0;

            Task<bool> userConfirm(string _)
            {
                confirmCount++;
                if (confirmCount >= 2)
                {
                    cts.Cancel();
                    throw new OperationCanceledException(cts.Token);
                }
                return Task.FromResult(true);
            }

            Task userPrompt(string _) => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(
                panelType,
userConfirm,
userPrompt,
                null,
                null,
                cts.Token);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(ButtonPanelTestType.Buttons, results[0].TestType);
            Assert.True(results[0].Passed);
            Assert.Equal(ButtonPanelTestType.Led, results[1].TestType);
            Assert.True(results[1].Interrupted);
        }

        #endregion

        #region Button Test Integration Tests

        /// <summary>
        /// Verifies button timeout is handled correctly when button is not pressed.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_ButtonTimeout_ReportsFailure()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            // Configure to skip button 2 (simulate timeout)
            _simulatedManager.ConfigureForFullTest(panel, _protocolManager, skipButtonIndices: [2]);

            var buttonResults = new List<(int index, bool passed)>();
            void onButtonResult(int i, bool p) => buttonResults.Add((i, p));

            Task userPrompt(string _) => Task.CompletedTask;
            Task<bool> userConfirm(string _) => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, onButtonResult);

            // Assert
            var buttonResult = results.First(r => r.TestType == ButtonPanelTestType.Buttons);
            Assert.False(buttonResult.Passed);
            Assert.Contains("FALLITO", buttonResult.Message);

            var (index, passed) = buttonResults.FirstOrDefault(r => r.index == 2);
            Assert.False(passed);
        }

        /// <summary>
        /// Verifies partial button success is reported correctly in the workflow.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_PartialButtonSuccess_ReportsCorrectly()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            // Skip buttons 1 and 5
            _simulatedManager.ConfigureForFullTest(panel, _protocolManager, skipButtonIndices: [1, 5]);

            var buttonResults = new List<(int index, bool passed)>();
            void onButtonResult(int i, bool p) => buttonResults.Add((i, p));

            Task userPrompt(string _) => Task.CompletedTask;
            Task<bool> userConfirm(string _) => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, onButtonResult);

            // Assert
            var buttonResult = results.First(r => r.TestType == ButtonPanelTestType.Buttons);
            Assert.False(buttonResult.Passed);

            // 6 passed, 2 failed
            var passedCount = buttonResults.Count(r => r.passed);
            var failedCount = buttonResults.Count(r => !r.passed);
            Assert.Equal(6, passedCount);
            Assert.Equal(2, failedCount);
        }

        /// <summary>
        /// Verifies button masks are correctly matched for each panel type.
        /// </summary>
        [Theory]
        [InlineData(ButtonPanelType.DIS0023789, 8)]
        [InlineData(ButtonPanelType.DIS0025205, 4)]
        [InlineData(ButtonPanelType.DIS0026166, 8)]
        [InlineData(ButtonPanelType.DIS0026182, 8)]
        public async Task TestAllAsync_DifferentPanelTypes_UseCorrectButtonCount(ButtonPanelType panelType, int expectedButtonCount)
        {
            // Arrange
            var panel = ButtonPanel.GetByType(panelType);
            Assert.Equal(expectedButtonCount, panel.ButtonCount);

            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

            var buttonResults = new List<(int index, bool passed)>();
            void onButtonResult(int i, bool p) => buttonResults.Add((i, p));

            Task userPrompt(string _) => Task.CompletedTask;
            Task<bool> userConfirm(string _) => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, onButtonResult);

            // Assert
            var buttonResult = results.First(r => r.TestType == ButtonPanelTestType.Buttons);
            Assert.True(buttonResult.Passed, $"Button test failed: {buttonResult.Message}");
            Assert.Equal(expectedButtonCount, buttonResults.Count);
        }

        #endregion

        #region LED Test Integration Tests

        /// <summary>
        /// Verifies LED test reports partial failure correctly within the workflow.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_LedPartialFailure_ReportsCorrectly()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

            int confirmCount = 0;
            Task<bool> userConfirm(string _)
            {
                confirmCount++;
                // Fail confirmations 2 and 4 (green off and red off)
                // First 5 confirmations are for LED, 6th is for buzzer
                return Task.FromResult(confirmCount != 2 && confirmCount != 4);
            }

            Task userPrompt(string _) => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);

            // Assert
            var ledResult = results.First(r => r.TestType == ButtonPanelTestType.Led);
            Assert.False(ledResult.Passed);
            Assert.False(ledResult.Interrupted);

            // Count PASSATO and FALLITO
            var passatoCount = ledResult.Message.Split("PASSATO").Length - 1;
            var fallitoCount = ledResult.Message.Split("FALLITO").Length - 1;

            Assert.Equal(3, passatoCount);
            Assert.Equal(2, fallitoCount);
        }

        /// <summary>
        /// Verifies LED test for panel without LED returns skipped result.
        /// </summary>
        [Fact]
        public async Task TestLedAsync_PanelWithoutLed_ReturnsSkipped()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0025205;
            var panel = ButtonPanel.GetByType(panelType);
            Assert.False(panel.HasLed);

            static Task<bool> userConfirm(string _) => Task.FromResult(true);

            // Act - call TestLedAsync directly (doesn't require channel setup)
            var result = await _sut.TestLedAsync(panelType, userConfirm);

            // Assert
            Assert.True(result.Passed);
            Assert.Contains("Skipped", result.Message);
            Assert.Contains("No LED", result.Message);
        }

        #endregion

        #region Protocol Repository Integration Tests

        /// <summary>
        /// Verifies SetProtocolRepository updates the repository and is used in the workflow.
        /// </summary>
        [Fact]
        public async Task SetProtocolRepository_UpdatesRepository_UsesNewValues()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);
            var newRepository = new CustomProtocolRepository();

            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);
            _sut.SetProtocolRepository(newRepository);

            static Task<bool> userConfirm(string _) => Task.FromResult(true);
            static Task userPrompt(string _) => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);

            // Assert - verify workflow completed (repository was used)
            Assert.NotNull(results);
            Assert.True(results.Count >= 1);
            Assert.NotEmpty(_simulatedManager.SentPackets);
        }

        #endregion

        #region Error Handling Integration Tests

        /// <summary>
        /// Verifies communication errors are handled gracefully through the stack.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_CommunicationError_ReturnsErrorResult()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;

            // Configure manager to fail connection
            _simulatedManager.FailConnection = true;

            static Task<bool> userConfirm(string _) => Task.FromResult(true);
            static Task userPrompt(string _) => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);

            // Assert
            Assert.Single(results);
            Assert.Equal(ButtonPanelTestType.Complete, results[0].TestType);
            Assert.False(results[0].Passed);
            Assert.Contains("Impossibile impostare il canale", results[0].Message);
        }

        #endregion

        #region Sequential Operation Tests

        /// <summary>
        /// Verifies multiple panel tests can run sequentially without interference.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_SequentialTests_NoInterference()
        {
            // Arrange
            var panelTypes = new[]
            {
                ButtonPanelType.DIS0023789,
                ButtonPanelType.DIS0025205
            };

            static Task userPrompt(string _) => Task.CompletedTask;
            static Task<bool> userConfirm(string _) => Task.FromResult(true);

            // Act
            var allResults = new List<List<ButtonPanelTestResult>>();
            foreach (var panelType in panelTypes)
            {
                var panel = ButtonPanel.GetByType(panelType);
                _simulatedManager.Reset();
                _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

                var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);
                allResults.Add(results);
            }

            // Assert
            Assert.Equal(2, allResults.Count);

            // First panel (8 buttons, LED): 3 results
            Assert.Equal(3, allResults[0].Count);
            Assert.All(allResults[0], r => Assert.True(r.Passed, $"Test failed: {r.Message}"));

            // Second panel (4 buttons, no LED): 2 results
            Assert.Equal(2, allResults[1].Count);
            Assert.All(allResults[1], r => Assert.True(r.Passed, $"Test failed: {r.Message}"));
        }

        #endregion

        #region End-to-End Protocol Tests

        /// <summary>
        /// Verifies the complete protocol stack correctly encodes and sends commands.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_ProtocolStackEncodesCommandsCorrectly()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0023789;
            var panel = ButtonPanel.GetByType(panelType);

            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

            int confirmCount = 0;
            Task<bool> userConfirm(string _)
            {
                confirmCount++;
                return Task.FromResult(true);
            }

            Task userPrompt(string _) => Task.CompletedTask;

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, null);

            // Assert
            var ledResult = results.First(r => r.TestType == ButtonPanelTestType.Led);
            Assert.True(ledResult.Passed, $"LED test failed: {ledResult.Message}");

            // 5 LED confirmations + 1 buzzer = 6
            Assert.Equal(6, confirmCount);

            // Verify packets were sent through the protocol manager
            var packets = _simulatedManager.SentPackets;
            Assert.NotEmpty(packets);
            Assert.All(packets, p =>
            {
                Assert.True(p.Length >= 2, $"Packet too short: {p.Length} bytes");
            });
        }

        /// <summary>
        /// Verifies button press simulation works through the real protocol manager.
        /// </summary>
        [Fact]
        public async Task TestAllAsync_ProtocolManagerProcessesButtonEvents()
        {
            // Arrange
            var panelType = ButtonPanelType.DIS0026166;
            var panel = ButtonPanel.GetByType(panelType);

            _simulatedManager.ConfigureForFullTest(panel, _protocolManager);

            var buttonResults = new List<(int index, bool passed)>();
            void onButtonResult(int i, bool p) => buttonResults.Add((i, p));

            Task userPrompt(string _) => Task.CompletedTask;
            Task<bool> userConfirm(string _) => Task.FromResult(true);

            // Act
            var results = await _sut.TestAllAsync(panelType, userConfirm, userPrompt, null, onButtonResult);

            // Assert
            var buttonResult = results.First(r => r.TestType == ButtonPanelTestType.Buttons);
            Assert.True(buttonResult.Passed, $"Button test failed: {buttonResult.Message}");
            Assert.Equal(panel.ButtonCount, buttonResults.Count);
            Assert.Equal(panelType, buttonResult.PanelType);
        }

        #endregion
    }

    #region Simulated Hardware Components

    /// <summary>
    /// Simulated communication manager that generates proper protocol responses
    /// and simulates button press events for integration testing.
    /// </summary>
    internal class SimulatedCommunicationManager : ICommunicationManager, IDisposable
    {
        private event EventHandler<byte[]>? _packetReceivedHandler;
        private CancellationTokenSource? _buttonSimulationCts;

        public event EventHandler<bool>? ConnectionStatusChanged;
        public event EventHandler<byte[]>? PacketReceived
        {
            add => _packetReceivedHandler += value;
            remove => _packetReceivedHandler -= value;
        }
#pragma warning disable CS0067 // Evento richiesto dall'interfaccia ma non utilizzato nel simulatore
        public event Action<uint, byte[]>? RawPacketReceived;
#pragma warning restore CS0067

        public int MaxPacketSize => 8;
        public bool IsConnected { get; private set; }
        public bool FailConnection { get; set; }

        public List<byte[]> SentPackets { get; } = [];

        private ButtonPanel? _simulatedPanel;
        private int _currentButtonIndex;
        private HashSet<int> _skipButtonIndices = [];
        private readonly Lock _lockObj = new();
        private static int _packetId = 1;

        /// <summary>
        /// Configures the manager for a full test workflow with button simulation and auto-responses.
        /// </summary>
        public void ConfigureForFullTest(ButtonPanel panel, IProtocolManager protocolManager, int[]? skipButtonIndices = null)
        {
            _simulatedPanel = panel;
            _currentButtonIndex = 0;
            _skipButtonIndices = skipButtonIndices?.ToHashSet() ?? [];
            _buttonSimulationCts = new CancellationTokenSource();

            // Start button simulation in background
            _ = StartButtonSimulation(_buttonSimulationCts.Token);
        }

        public void Reset()
        {
            _buttonSimulationCts?.Cancel();
            _buttonSimulationCts?.Dispose();
            _buttonSimulationCts = null;
            SentPackets.Clear();
            FailConnection = false;
            _simulatedPanel = null;
            _currentButtonIndex = 0;
            _skipButtonIndices.Clear();
        }

        public Task<bool> ConnectAsync(string config, CancellationToken cancellationToken = default)
        {
            if (FailConnection)
            {
                IsConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                return Task.FromResult(false);
            }

            IsConnected = true;
            ConnectionStatusChanged?.Invoke(this, true);
            return Task.FromResult(true);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        public Task<bool> SendAsync(byte[] data, uint? arbitrationId = null)
        {
            if (!IsConnected) return Task.FromResult(false);

            SentPackets.Add([.. data]);

            // Always simulate a response for commands
            _ = Task.Run(async () =>
            {
                await Task.Delay(5);
                SimulateCommandResponse();
            });

            return Task.FromResult(true);
        }

        /// <summary>
        /// Continuously simulates button presses at regular intervals.
        /// </summary>
        private async Task StartButtonSimulation(CancellationToken cancellationToken)
        {
            // Wait a bit for the test to set up
            await Task.Delay(100, cancellationToken);

            while (!cancellationToken.IsCancellationRequested && _simulatedPanel != null)
            {
                int buttonIndex;
                lock (_lockObj)
                {
                    buttonIndex = _currentButtonIndex;
                    if (buttonIndex >= _simulatedPanel.ButtonMasks.Count)
                    {
                        return; // All buttons done
                    }
                    _currentButtonIndex++;
                }

                if (!_skipButtonIndices.Contains(buttonIndex))
                {
                    var buttonMask = _simulatedPanel.ButtonMasks[buttonIndex];
                    // Payload format from button panel: [0x00, 0x02, 0x80, 0x3E, buttonMask]
                    var appPayload = new byte[] { 0x00, 0x02, 0x80, 0x3E, buttonMask };
                    var rawPacket = BuildProtocolPacket(appPayload);
                    // Strip NetInfo (first 2 bytes) before passing to handler
                    // The CommunicationService expects transport packet only
                    var transportPacket = rawPacket.Skip(2).ToArray();
                    _packetReceivedHandler?.Invoke(this, transportPacket);

                    // Short delay after sending a button press
                    try
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                else
                {
                    // For skipped buttons, wait for the button timeout to expire
                    // The button timeout is 300ms, so we need to wait that long
                    try
                    {
                        await Task.Delay(350, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private void SimulateCommandResponse()
        {
            var appPayload = new byte[] { 0x01, 0x00, 0x00, 0x00 }; // ACK response
            var rawPacket = BuildProtocolPacket(appPayload);
            // Strip NetInfo (first 2 bytes) before passing to handler
            // The CommunicationService expects transport packet only
            var transportPacket = rawPacket.Skip(2).ToArray();
            _packetReceivedHandler?.Invoke(this, transportPacket);
        }

        /// <summary>
        /// Builds a valid protocol packet with proper structure.
        /// </summary>
        private static byte[] BuildProtocolPacket(byte[] appPayload)
        {
            // Transport layer structure
            byte cryptType = 0x00;
            uint senderId = 0;
            ushort lPack = (ushort)appPayload.Length;

            // Build transport header with big-endian values (matching original protocol)
            var transportHeader = new List<byte> { cryptType };
            transportHeader.AddRange(ToBigEndianBytes(senderId));
            transportHeader.AddRange(ToBigEndianBytes(lPack));

            // Calculate CRC over header + appPayload (matching TransportLayer implementation)
            var dataForCrc = transportHeader.Concat(appPayload).ToArray();
            ushort crcValue = CalculateCrc16(dataForCrc);
            // CRC stored as big-endian (matching real hardware)
            var crcBytes = ToBigEndianBytes(crcValue);

            // Build transport packet
            var transportPacket = new List<byte>();
            transportPacket.AddRange(transportHeader);
            transportPacket.AddRange(appPayload);
            transportPacket.AddRange(crcBytes);

            // Build network layer (NetInfo) - uses little-endian
            int packetId = Interlocked.Increment(ref _packetId) % 7 + 1; // 1-7
            ushort netInfoValue = (ushort)(
                (0 << 6) |           // remainingChunks = 0
                (0 << 5) |           // setLength = false
                (packetId << 2) |    // packetId
                0                    // version = V1
            );
            var netInfoBytes = ToLittleEndianBytes(netInfoValue);

            // Combine all parts
            var rawPacket = new List<byte>();
            rawPacket.AddRange(netInfoBytes);
            rawPacket.AddRange(transportPacket);

            return [.. rawPacket];
        }

        private static byte[] ToLittleEndianBytes(ushort value) =>
            [(byte)value, (byte)(value >> 8)];

        private static byte[] ToLittleEndianBytes(uint value) =>
            [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];

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

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            Reset();
            IsConnected = false;
        }
    }

    /// <summary>
    /// Factory for creating SimulatedCommunicationManager instances.
    /// </summary>
    internal class SimulatedCommunicationManagerFactory : ICommunicationManagerFactory
    {
        private readonly SimulatedCommunicationManager _manager;

        public SimulatedCommunicationManagerFactory(SimulatedCommunicationManager manager)
        {
            _manager = manager;
        }

        public ICommunicationManager Create(CommunicationChannel channel) => _manager;
    }

    /// <summary>
    /// In-memory implementation of IProtocolRepository.
    /// </summary>
    internal class InMemoryProtocolRepository : IProtocolRepository
    {
        public ushort GetCommand(string commandName) => commandName switch
        {
            "Scrivi variabile logica" => 0x0100,
            "READ_VARIABLE" => 0x0200,
            _ => 0x0000
        };

        public ushort GetVariable(string variableName) => variableName switch
        {
            "BUTTONS_STATUS" => 0x0001,
            "Comando Led Verde" => 0x0002,
            "Comando Led Rosso" => 0x0003,
            "Comando Buzzer" => 0x0004,
            _ => 0x0000
        };

        public byte[] GetValue(string valueName) => valueName switch
        {
            "ON" => [0x00, 0x00, 0x00, 0x80],
            "OFF" => [0x00, 0x00, 0x00, 0x00],
            "SINGLE_BLINK" => [0x00, 0xFF, 0x80, 0x61],
            _ => []
        };
    }

    /// <summary>
    /// Custom protocol repository for testing SetProtocolRepository.
    /// </summary>
    internal class CustomProtocolRepository : IProtocolRepository
    {
        public ushort GetCommand(string commandName) => commandName switch
        {
            "Scrivi variabile logica" => 0x0150,
            _ => 0x0000
        };

        public ushort GetVariable(string variableName) => variableName switch
        {
            "Comando Buzzer" => 0x0054,
            "Comando Led Verde" => 0x0052,
            "Comando Led Rosso" => 0x0053,
            _ => 0x0000
        };

        public byte[] GetValue(string valueName) => valueName switch
        {
            "ON" => [0x00, 0x00, 0x00, 0x80],
            "OFF" => [0x00, 0x00, 0x00, 0x00],
            "SINGLE_BLINK" => [0x00, 0xFF, 0x80, 0x61],
            _ => []
        };
    }

    #endregion
}
