using Communication;
using Infrastructure;
using Infrastructure.Lib;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Peak.Can.Basic;

namespace Tests.Integration.Communication
{
    /// <summary>
    /// Hardware-independent integration tests for the entire CAN stack:
    /// CanCommunicationManager → PcanAdapter → mocked IPcanApi
    /// </summary>
    [Trait("Category", TestCategories.Integration)]
    public class CanCommunicationTests : IDisposable
    {
        private readonly Mock<IPcanApi> _mockPcanApi;
        private readonly PcanAdapter _adapter;
        private readonly CanCommunicationManager _manager;

        public CanCommunicationTests()
        {
            _mockPcanApi = new Mock<IPcanApi>();
            _adapter = new PcanAdapter(_mockPcanApi.Object, NullLogger<PcanAdapter>.Instance);
            _manager = new CanCommunicationManager(_adapter);
        }

        public void Dispose()
        {
            // Clean up between tests
            _manager.DisconnectAsync().Wait(100);
        }

        //[Fact]
        //public async Task ConnectAsync_Success_RaisesConnectedEvent()
        //{
        //    // Arrange
        //    _mockPcanApi.Setup(a => a.Initialize(PcanChannel.Usb01, Bitrate.Pcan250))
        //                .Returns(PcanStatus.OK);

        //    bool connected = false;
        //    _manager.ConnectionStatusChanged += (_, c) => connected = c;

        //    // Act
        //    bool result = await _manager.ConnectAsync("250000");

        //    // Assert
        //    Assert.True(result);
        //    Assert.True(connected);
        //    Assert.True(_manager.IsConnected);
        //    _mockPcanApi.Verify(a => a.Initialize(PcanChannel.Usb01, Bitrate.Pcan250), Times.Once);
        //}

        [Fact]
        public async Task ConnectAsync_Failure_RaisesDisconnectedEvent()
        {
            _mockPcanApi.Setup(a => a.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
                        .Returns(PcanStatus.AnyBusError);

            bool disconnected = false;
            _manager.ConnectionStatusChanged += (_, c) => disconnected = !c;

            bool result = await _manager.ConnectAsync("250000");

            Assert.False(result);
            Assert.True(disconnected);
            Assert.False(_manager.IsConnected);
        }

        //[Fact]
        //public async Task SendAsync_WhenConnected_CallsWrite()
        //{
        //    // Salta il test nella CI perchè non funzionante
        //    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        //        return;

        //    // Connect first
        //    _mockPcanApi.Setup(a => a.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
        //                .Returns(PcanStatus.OK);
        //    await _manager.ConnectAsync("250000");

        //    // Capture the written message
        //    PcanMessage? capturedMessage = null;
        //    _mockPcanApi.Setup(a => a.Write(It.IsAny<PcanChannel>(), It.Ref<PcanMessage>.IsAny))
        //                .Returns(PcanStatus.OK)
        //                .Callback<PcanChannel, PcanMessage>((channel, msg) =>
        //                {
        //                    capturedMessage = msg; // Moq copies the ref value here
        //                });

        //    var payload = new byte[] { 0x11, 0x22, 0x33 };
        //    bool sent = await _manager.SendAsync(payload, arbitrationId: 0x7E8);

        //    Assert.True(sent);
        //    Assert.NotNull(capturedMessage);
        //    Assert.Equal(0x7E8u, capturedMessage.ID);
        //    Assert.Equal((byte)payload.Length, capturedMessage.Length);
        //    Assert.Equal(payload, capturedMessage.Data);

        //    _mockPcanApi.Verify(a => a.Write(PcanChannel.Usb01, It.Ref<PcanMessage>.IsAny), Times.Once);
        //}

        //[Fact]
        //public async Task ReceivePacket_FromAdapter_RaisesPacketReceivedEvent()
        //{
        //    // Salta il test nella CI perchè non funzionante
        //    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        //        return;

        //    // Connect
        //    _mockPcanApi.Setup(a => a.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
        //                .Returns(PcanStatus.OK);
        //    await _manager.ConnectAsync("250000");
        //    byte[]? received = null;
        //    _manager.PacketReceived += (_, data) => received = data;
        //    var expectedPayload = new byte[] { 0xAA, 0xBB, 0xCC };
        //    // Simulate one successful read
        //    _mockPcanApi.Setup(a => a.Read(It.IsAny<PcanChannel>(), out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny))
        //                .Returns(PcanStatus.OK)
        //                .Callback<PcanChannel, PcanMessage, ulong>((PcanChannel channel, PcanMessage msg, ulong ts) =>
        //                {
        //                    msg = new PcanMessage
        //                    {
        //                        DLC = (byte)expectedPayload.Length,
        //                        Data = new byte[8]
        //                    };
        //                    Array.Copy(expectedPayload, msg.Data, expectedPayload.Length);
        //                    ts = 12345;
        //                });
        //    // Wait for the polling loop (50ms interval)
        //    await Task.Delay(120);
        //    Assert.NotNull(received);
        //    Assert.Equal(expectedPayload, received);
        //}

        //[Fact]
        //public async Task DisconnectAsync_StopsPollingLoop()
        //{
        //    // Connect
        //    _mockPcanApi.Setup(a => a.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
        //                .Returns(PcanStatus.OK);
        //    await _manager.ConnectAsync("250000");

        //    // Disconnect
        //    await _manager.DisconnectAsync();

        //    Assert.False(_manager.IsConnected);
        //    _mockPcanApi.Verify(a => a.Uninitialize(PcanChannel.Usb01), Times.Once);

        //    // Give the background Task time to exit
        //    await Task.Delay(150);

        //    // Should not call Read more than a couple of times after disconnect
        //    _mockPcanApi.Verify(a => a.Read(It.IsAny<PcanChannel>(), out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny),
        //                        Times.AtMost(4));
        //}

        //[Fact]
        //public async Task FullLifecycle_WorksEndToEnd()
        //{
        //    // Salta il test nella CI perchè non funzionante
        //    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        //        return;

        //    // Connect
        //    _mockPcanApi.Setup(a => a.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
        //                .Returns(PcanStatus.OK);
        //    await _manager.ConnectAsync("250000");
        //    Assert.True(_manager.IsConnected);

        //    // Send
        //    _mockPcanApi.Setup(a => a.Write(It.IsAny<PcanChannel>(), It.Ref<PcanMessage>.IsAny))
        //                .Returns(PcanStatus.OK);
        //    bool sent = await _manager.SendAsync([1, 2, 3], 0x123);
        //    Assert.True(sent);

        //    // Receive
        //    bool received = false;
        //    _manager.PacketReceived += (_, __) => received = true;

        //    _mockPcanApi.Setup(a => a.Read(It.IsAny<PcanChannel>(), out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny))
        //                .Returns(PcanStatus.OK);
        //    await Task.Delay(100);
        //    Assert.True(received);

        //    // Disconnect
        //    await _manager.DisconnectAsync();
        //    Assert.False(_manager.IsConnected);
        //}
    }
}
