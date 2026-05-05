using Services.Helpers;

namespace Tests.Services.Helpers
{
    /// <summary>
    /// Unit test per ResponseParser.
    /// </summary>
    public class ResponseParserTests
    {
        [Fact]
        public void TryParseWhoAmI_WithValidResponse_ReturnsTrueAndParsesCorrectly()
        {
            // Arrange
            byte[] response = new byte[]
            {
                0x03,                                           // MACHINE_TYPE
                0x00, 0x04,                                     // FW_TYPE (big-endian)
                0x01, 0x02, 0x03, 0x04,                        // UUID0
                0x05, 0x06, 0x07, 0x08,                        // UUID1
                0x09, 0x0A, 0x0B, 0x0C                         // UUID2
            };

            // Act
            bool success = ResponseParser.TryParseWhoAmI(response, out WhoAmIResponse result);

            // Assert
            Assert.True(success);
            Assert.Equal(0x03, result.MachineType);
            Assert.Equal(0x0004, result.FirmwareType);
            Assert.Equal(12, result.Uuid.Length);
            Assert.Equal(0x01, result.Uuid[0]);
            Assert.Equal(0x0C, result.Uuid[11]);
        }

        [Fact]
        public void TryParseWhoAmI_WithTooShortResponse_ReturnsFalse()
        {
            // Arrange
            byte[] response = new byte[] { 0x03, 0x00, 0x04 }; // Solo 3 bytes

            // Act
            bool success = ResponseParser.TryParseWhoAmI(response, out WhoAmIResponse result);

            // Assert
            Assert.False(success);
        }

        [Fact]
        public void TryParseWhoAmI_WithExactly15Bytes_ReturnsTrue()
        {
            // Arrange
            byte[] response = new byte[15];
            response[0] = 0x03;  // MACHINE_TYPE
            response[1] = 0x00;  // FW_TYPE_H
            response[2] = 0x04;  // FW_TYPE_L

            // Act
            bool success = ResponseParser.TryParseWhoAmI(response, out WhoAmIResponse result);

            // Assert
            Assert.True(success);
            Assert.Equal(0x03, result.MachineType);
            Assert.Equal(0x0004, result.FirmwareType);
        }

        [Theory]
        [InlineData(15, true)]
        [InlineData(20, true)]
        [InlineData(100, true)]
        [InlineData(14, false)]
        [InlineData(0, false)]
        public void IsValidResponse_WithVariousLengths_ReturnsExpectedResult(int length, bool expectedValid)
        {
            // Arrange
            byte[] data = new byte[length];
            int minLength = 15;

            // Act
            bool isValid = ResponseParser.IsValidResponse(data, minLength);

            // Assert
            Assert.Equal(expectedValid, isValid);
        }

        [Fact]
        public void IsValidResponse_WithNullData_ReturnsFalse()
        {
            // Act
            bool isValid = ResponseParser.IsValidResponse(null!, 10);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void IsAcknowledgment_WithValidAck_ReturnsTrue()
        {
            // Arrange
            ushort commandId = 0x0023;
            byte[] ackData = new byte[] { 0x80, 0x23 };

            // Act
            bool isAck = ResponseParser.IsAcknowledgment(ackData, commandId);

            // Assert
            Assert.True(isAck);
        }

        [Fact]
        public void IsAcknowledgment_WithInvalidAck_ReturnsFalse()
        {
            // Arrange
            ushort commandId = 0x0023;
            byte[] notAckData = new byte[] { 0x00, 0x23 }; // Non inizia con 0x80

            // Act
            bool isAck = ResponseParser.IsAcknowledgment(notAckData, commandId);

            // Assert
            Assert.False(isAck);
        }

        [Fact]
        public void IsAcknowledgment_WithDifferentCommand_ReturnsFalse()
        {
            // Arrange
            ushort commandId = 0x0023;
            byte[] ackData = new byte[] { 0x80, 0x24 }; // ACK ma per comando diverso

            // Act
            bool isAck = ResponseParser.IsAcknowledgment(ackData, commandId);

            // Assert
            Assert.False(isAck);
        }

        [Fact]
        public void IsAcknowledgment_WithTooShortData_ReturnsFalse()
        {
            // Arrange
            ushort commandId = 0x0023;
            byte[] shortData = new byte[] { 0x80 }; // Solo 1 byte

            // Act
            bool isAck = ResponseParser.IsAcknowledgment(shortData, commandId);

            // Assert
            Assert.False(isAck);
        }

        [Fact]
        public void TryParseWhoAmI_ParsesFirmwareType_BigEndian()
        {
            // Arrange - Verifica che il firmware type sia interpretato in big-endian
            byte[] response = new byte[15];
            response[0] = 0x03;
            response[1] = 0x12;  // FW_TYPE_H
            response[2] = 0x34;  // FW_TYPE_L

            // Act
            bool success = ResponseParser.TryParseWhoAmI(response, out WhoAmIResponse result);

            // Assert
            Assert.True(success);
            Assert.Equal(0x1234, result.FirmwareType); // Big-endian: 0x12 << 8 | 0x34
        }
    }
}
