using Communication;
using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Core.Interfaces.Infrastructure;
using Core.Models.Communication;
using Moq;

namespace Tests.Unit.Communication
{
    /// <summary>
    /// Unit tests per CanCommunicationManager.
    /// Mock ICanAdapter per isolare i tests.
    /// </summary>
    public class CanCommunicationManagerTests
    {
        private readonly Mock<ICanAdapter> _mockAdapter;
        private readonly CanCommunicationManager _manager;

        public CanCommunicationManagerTests()
        {
            _mockAdapter = new Mock<ICanAdapter>();
            _manager = new CanCommunicationManager(_mockAdapter.Object);
        }

        #region Helper Methods

        /// <summary>
        /// Creates a valid protocol chunk with NetInfo header that the reassembler can process.
        /// </summary>
        private static byte[] CreateValidProtocolChunk(byte marker = 0x42)
        {
            // Create a minimal valid transport packet
            var appPacket = ApplicationLayer.Create(marker, 0x00, []).ApplicationPacket;
            var transportLayer = TransportLayer.Create(CryptType.None, 0, appPacket);
            var transportPacket = transportLayer.TransportPacket;

            // Wrap in NetInfo (remainingChunks=0 means single complete chunk, packetId=1)
            var netInfo = new NetInfo(0, false, 1, ProtocolVersion.V1);
            return [.. netInfo.ToBytes(), .. transportPacket];
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_ThrowsIfAdapterNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CanCommunicationManager(null!));
        }

        [Fact]
        public void Constructor_SubscribesToAdapterEvents()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();

            // Act
            var manager = new CanCommunicationManager(mockAdapter.Object);

            // Assert - Verify event subscriptions by raising events
            bool connectionEventReceived = false;
            bool packetEventReceived = false;
            manager.ConnectionStatusChanged += (_, _) => connectionEventReceived = true;
            manager.PacketReceived += (_, _) => packetEventReceived = true;

            mockAdapter.Raise(a => a.ConnectionStatusChanged += null, mockAdapter.Object, true);

            // Use a valid protocol chunk that the reassembler can process
            var validChunk = CreateValidProtocolChunk();
            mockAdapter.Raise(a => a.PacketReceived += null, mockAdapter.Object,
                new CanPacket(0x123, false, validChunk, 0));

            Assert.True(connectionEventReceived);
            Assert.True(packetEventReceived);
        }

        #endregion

        #region MaxPacketSize Tests

        [Fact]
        public void MaxPacketSize_Returns8()
        {
            // Assert
            Assert.Equal(8, _manager.MaxPacketSize);
        }

        #endregion

        #region ConnectAsync Tests

        [Fact]
        public async Task ConnectAsync_CallsAdapterAndRaisesEvent()
        {
            // Arrange
            _mockAdapter.Setup(a => a.ConnectAsync("250000", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            bool eventRaised = false;
            _manager.ConnectionStatusChanged += (_, connected) => eventRaised = connected;

            // Act
            bool result = await _manager.ConnectAsync("250000");
            _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, true);

            // Assert
            Assert.True(result);
            Assert.True(eventRaised);
            Assert.True(_manager.IsConnected);
            _mockAdapter.Verify(a => a.ConnectAsync("250000", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConnectAsync_Failure_ReturnsFalseAndRaisesEvent()
        {
            // Arrange
            _mockAdapter.Setup(a => a.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            bool eventRaised = false;
            _manager.ConnectionStatusChanged += (_, connected) => eventRaised = !connected;

            // Act
            bool result = await _manager.ConnectAsync("invalid");
            _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, false);

            // Assert
            Assert.False(result);
            Assert.True(eventRaised);
            Assert.False(_manager.IsConnected);
        }

        [Fact]
        public async Task ConnectAsync_WithCancellationToken_PropagatesToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            _mockAdapter.Setup(a => a.ConnectAsync(It.IsAny<string>(), cts.Token)).ReturnsAsync(true);

            // Act
            await _manager.ConnectAsync("250", cts.Token);

            // Assert
            _mockAdapter.Verify(a => a.ConnectAsync("250", cts.Token), Times.Once);
        }

        [Fact]
        public async Task ConnectAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _manager.ConnectAsync("250", cts.Token));
        }

        #endregion

        #region DisconnectAsync Tests

        [Fact]
        public async Task DisconnectAsync_CallsAdapterAndRaisesEvent()
        {
            // Arrange
            bool eventRaised = false;
            _manager.ConnectionStatusChanged += (_, connected) => eventRaised = !connected;

            // Act
            await _manager.DisconnectAsync();
            _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, false);

            // Assert
            Assert.True(eventRaised);
            Assert.False(_manager.IsConnected);
            _mockAdapter.Verify(a => a.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DisconnectAsync_WithCancellationToken_PropagatesToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act
            await _manager.DisconnectAsync(cts.Token);

            // Assert
            _mockAdapter.Verify(a => a.DisconnectAsync(cts.Token), Times.Once);
        }

        [Fact]
        public async Task DisconnectAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _manager.DisconnectAsync(cts.Token));
        }

        #endregion

        #region SendAsync Tests

        [Fact]
        public async Task SendAsync_WithValidArbitrationId_CallsAdapter()
        {
            // Arrange
            byte[] data = [0x01, 0x02, 0x03];
            uint arbitrationId = 0x123;
            _mockAdapter.Setup(a => a.Send(arbitrationId, data, true)).ReturnsAsync(true);

            // Act
            bool result = await _manager.SendAsync(data, arbitrationId);

            // Assert
            Assert.True(result);
            _mockAdapter.Verify(a => a.Send(arbitrationId, data, true), Times.Once);
        }

        [Fact]
        public async Task SendAsync_NullArbitrationId_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] data = [0x01, 0x02];

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _manager.SendAsync(data, null));

            Assert.Equal("arbitrationId", exception.ParamName);
        }

        [Fact]
        public async Task SendAsync_StandardId_AlwaysUsesExtended()
        {
            // Arrange
            byte[] data = [0x01];
            uint standardId = 0x7FF; // Maximum standard ID (11 bits)
            // Il protocollo STEM usa SEMPRE Extended ID, anche per ID < 0x7FF
            _mockAdapter.Setup(a => a.Send(standardId, data, true)).ReturnsAsync(true);

            // Act
            await _manager.SendAsync(data, standardId);

            // Assert - Extended flag always true per protocollo STEM
            _mockAdapter.Verify(a => a.Send(standardId, data, true), Times.Once);
        }

        [Fact]
        public async Task SendAsync_ExtendedId_SetsExtendedTrue()
        {
            // Arrange
            byte[] data = [0x01];
            uint extendedId = 0x800; // First extended ID (> 0x7FF)
            _mockAdapter.Setup(a => a.Send(extendedId, data, true)).ReturnsAsync(true);

            // Act
            await _manager.SendAsync(data, extendedId);

            // Assert - Extended flag should be true for extended IDs
            _mockAdapter.Verify(a => a.Send(extendedId, data, true), Times.Once);
        }

        [Fact]
        public async Task SendAsync_MaxExtendedId_SetsExtendedTrue()
        {
            // Arrange
            byte[] data = [0x01];
            uint maxExtendedId = 0x1FFFFFFF; // Maximum 29-bit extended ID
            _mockAdapter.Setup(a => a.Send(maxExtendedId, data, true)).ReturnsAsync(true);

            // Act
            await _manager.SendAsync(data, maxExtendedId);

            // Assert
            _mockAdapter.Verify(a => a.Send(maxExtendedId, data, true), Times.Once);
        }

        [Fact]
        public async Task SendAsync_AdapterReturnsFalse_ReturnsFalse()
        {
            // Arrange
            byte[] data = [0x01];
            uint arbitrationId = 0x123;
            _mockAdapter.Setup(a => a.Send(arbitrationId, data, true)).ReturnsAsync(false);

            // Act
            bool result = await _manager.SendAsync(data, arbitrationId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SendAsync_EmptyData_Succeeds()
        {
            // Arrange
            byte[] data = [];
            uint arbitrationId = 0x123;
            _mockAdapter.Setup(a => a.Send(arbitrationId, data, true)).ReturnsAsync(true);

            // Act
            bool result = await _manager.SendAsync(data, arbitrationId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendAsync_MaxSizeData_Succeeds()
        {
            // Arrange
            byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]; // 8 bytes
            uint arbitrationId = 0x123;
            _mockAdapter.Setup(a => a.Send(arbitrationId, data, true)).ReturnsAsync(true);

            // Act
            bool result = await _manager.SendAsync(data, arbitrationId);

            // Assert
            Assert.True(result);
            Assert.Equal(8, data.Length);
        }

        #endregion

        #region Event Propagation Tests

        [Fact]
        public void AdapterPacketReceived_PropagatesToManager()
        {
            // Arrange
            byte[]? receivedData = null;
            _manager.PacketReceived += (_, data) => receivedData = data;

            // Create a valid protocol chunk that the reassembler can process
            var validChunk = CreateValidProtocolChunk(0x42);
            var testPacket = new CanPacket(0x123, false, validChunk, 0);

            // Act
            _mockAdapter.Raise(a => a.PacketReceived += null, _mockAdapter.Object, testPacket);

            // Assert
            Assert.NotNull(receivedData);
            // The reassembler extracts the transport packet (without NetInfo header)
            Assert.True(receivedData.Length > 0);
        }

        [Fact]
        public void AdapterConnectionStatusChanged_PropagatesToManager()
        {
            // Arrange
            bool? connectionStatus = null;
            _manager.ConnectionStatusChanged += (_, connected) => connectionStatus = connected;

            // Act
            _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, true);

            // Assert
            Assert.True(connectionStatus);
            Assert.True(_manager.IsConnected);
        }

        [Fact]
        public void AdapterConnectionStatusChanged_False_UpdatesIsConnected()
        {
            // Arrange
            _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, true);
            Assert.True(_manager.IsConnected);

            // Act
            _mockAdapter.Raise(a => a.ConnectionStatusChanged += null, _mockAdapter.Object, false);

            // Assert
            Assert.False(_manager.IsConnected);
        }

        #endregion

        #region DisposeAsync Tests

        [Fact]
        public async Task DisposeAsync_UnsubscribesFromEvents()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();
            var manager = new CanCommunicationManager(mockAdapter.Object);
            bool eventReceived = false;
            manager.ConnectionStatusChanged += (_, _) => eventReceived = true;

            // Act
            await manager.DisposeAsync();

            // Raise event after dispose - handler should still work for manager's own event
            // but adapter events should be unsubscribed
            mockAdapter.Raise(a => a.ConnectionStatusChanged += null, mockAdapter.Object, true);

            // Assert - Event should not propagate after dispose
            Assert.False(eventReceived);
        }

        [Fact]
        public async Task DisposeAsync_CallsAdapterDisposeAsync()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();
            var manager = new CanCommunicationManager(mockAdapter.Object);

            // Act
            await manager.DisposeAsync();

            // Assert
            mockAdapter.Verify(a => a.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_CanBeCalledMultipleTimes()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();
            var manager = new CanCommunicationManager(mockAdapter.Object);

            // Act & Assert - Should not throw
            await manager.DisposeAsync();
            await manager.DisposeAsync();

            // Verify adapter dispose was called (may be multiple times depending on implementation)
            mockAdapter.Verify(a => a.DisposeAsync(), Times.AtLeast(1));
        }

        #endregion
    }
}
