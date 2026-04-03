using Core.Models;

namespace Tests.Unit.Core.Models
{
    /// <summary>
    /// Unit tests for EventArgs classes in Core.Models.
    /// Tests AppLayerDecoderEventArgs, ProtocolErrorEventArgs, and CommunicationErrorEventArgs.
    /// </summary>
    public class EventArgsTests
    {
        #region AppLayerDecoderEventArgs Tests

        [Fact]
        public void AppLayerDecoderEventArgs_Constructor_SetsPayload()
        {
            // Arrange
            byte[] payload = [0x01, 0x02, 0x03];

            // Act
            var args = new AppLayerDecoderEventArgs(payload);

            // Assert
            Assert.Equal(payload, args.Payload);
        }

        [Fact]
        public void AppLayerDecoderEventArgs_EmptyPayload_Succeeds()
        {
            // Arrange
            byte[] payload = [];

            // Act
            var args = new AppLayerDecoderEventArgs(payload);

            // Assert
            Assert.Empty(args.Payload);
        }

        [Fact]
        public void AppLayerDecoderEventArgs_NullPayload_Succeeds()
        {
            // Arrange
            byte[]? payload = null;

            // Act
            var args = new AppLayerDecoderEventArgs(payload!);

            // Assert
            Assert.Null(args.Payload);
        }

        [Fact]
        public void AppLayerDecoderEventArgs_LargePayload_Succeeds()
        {
            // Arrange
            byte[] payload = new byte[10000];
            Random.Shared.NextBytes(payload);

            // Act
            var args = new AppLayerDecoderEventArgs(payload);

            // Assert
            Assert.Equal(10000, args.Payload.Length);
            Assert.Equal(payload, args.Payload);
        }

        [Fact]
        public void AppLayerDecoderEventArgs_InheritsFromEventArgs()
        {
            // Arrange & Act
            var args = new AppLayerDecoderEventArgs([0x01]);

            // Assert
            Assert.IsAssignableFrom<EventArgs>(args);
        }

        #endregion

        #region ProtocolErrorEventArgs Tests

        [Fact]
        public void ProtocolErrorEventArgs_Constructor_SetsMessage()
        {
            // Arrange
            string message = "Test error message";

            // Act
            var args = new ProtocolErrorEventArgs(message);

            // Assert
            Assert.Equal(message, args.Message);
            Assert.Null(args.Packet);
        }

        [Fact]
        public void ProtocolErrorEventArgs_ConstructorWithPacket_SetsBoth()
        {
            // Arrange
            string message = "Test error message";
            byte[] packet = [0x01, 0x02, 0x03];

            // Act
            var args = new ProtocolErrorEventArgs(message, packet);

            // Assert
            Assert.Equal(message, args.Message);
            Assert.Equal(packet, args.Packet);
        }

        [Fact]
        public void ProtocolErrorEventArgs_NullPacket_IsAllowed()
        {
            // Arrange
            string message = "Error without packet";

            // Act
            var args = new ProtocolErrorEventArgs(message, null);

            // Assert
            Assert.Equal(message, args.Message);
            Assert.Null(args.Packet);
        }

        [Fact]
        public void ProtocolErrorEventArgs_EmptyMessage_IsAllowed()
        {
            // Arrange
            string message = "";

            // Act
            var args = new ProtocolErrorEventArgs(message);

            // Assert
            Assert.Equal("", args.Message);
        }

        [Fact]
        public void ProtocolErrorEventArgs_InheritsFromEventArgs()
        {
            // Arrange & Act
            var args = new ProtocolErrorEventArgs("error");

            // Assert
            Assert.IsAssignableFrom<EventArgs>(args);
        }

        #endregion

        #region CommunicationErrorEventArgs Tests

        [Fact]
        public void CommunicationErrorEventArgs_Constructor_SetsMessage()
        {
            // Arrange
            string message = "Communication error";

            // Act
            var args = new CommunicationErrorEventArgs(message);

            // Assert
            Assert.Equal(message, args.Message);
        }

        [Fact]
        public void CommunicationErrorEventArgs_EmptyMessage_IsAllowed()
        {
            // Arrange
            string message = "";

            // Act
            var args = new CommunicationErrorEventArgs(message);

            // Assert
            Assert.Equal("", args.Message);
        }

        [Fact]
        public void CommunicationErrorEventArgs_NullMessage_IsAllowed()
        {
            // Arrange
            string? message = null;

            // Act
            var args = new CommunicationErrorEventArgs(message!);

            // Assert
            Assert.Null(args.Message);
        }

        [Fact]
        public void CommunicationErrorEventArgs_InheritsFromEventArgs()
        {
            // Arrange & Act
            var args = new CommunicationErrorEventArgs("error");

            // Assert
            Assert.IsAssignableFrom<EventArgs>(args);
        }

        [Fact]
        public void CommunicationErrorEventArgs_LongMessage_Succeeds()
        {
            // Arrange
            string message = new string('x', 10000);

            // Act
            var args = new CommunicationErrorEventArgs(message);

            // Assert
            Assert.Equal(10000, args.Message.Length);
        }

        #endregion

        #region Event Usage Tests

        [Fact]
        public void AppLayerDecoderEventArgs_CanBeUsedInEvent()
        {
            // Arrange
            AppLayerDecoderEventArgs? capturedArgs = null;
            EventHandler<AppLayerDecoderEventArgs> handler = (sender, args) => capturedArgs = args;
            byte[] payload = [0x01, 0x02];

            // Act
            handler.Invoke(this, new AppLayerDecoderEventArgs(payload));

            // Assert
            Assert.NotNull(capturedArgs);
            Assert.Equal(payload, capturedArgs.Payload);
        }

        [Fact]
        public void ProtocolErrorEventArgs_CanBeUsedInEvent()
        {
            // Arrange
            ProtocolErrorEventArgs? capturedArgs = null;
            EventHandler<ProtocolErrorEventArgs> handler = (sender, args) => capturedArgs = args;
            byte[] packet = [0x01];

            // Act
            handler.Invoke(this, new ProtocolErrorEventArgs("error", packet));

            // Assert
            Assert.NotNull(capturedArgs);
            Assert.Equal("error", capturedArgs.Message);
            Assert.Equal(packet, capturedArgs.Packet);
        }

        [Fact]
        public void CommunicationErrorEventArgs_CanBeUsedInEvent()
        {
            // Arrange
            CommunicationErrorEventArgs? capturedArgs = null;
            EventHandler<CommunicationErrorEventArgs> handler = (sender, args) => capturedArgs = args;

            // Act
            handler.Invoke(this, new CommunicationErrorEventArgs("comm error"));

            // Assert
            Assert.NotNull(capturedArgs);
            Assert.Equal("comm error", capturedArgs.Message);
        }

        #endregion
    }
}
