using Services.Helpers;

namespace Tests.Services.Helpers
{
    /// <summary>
    /// Unit test per PayloadBuilder.
    /// </summary>
    public class PayloadBuilderTests
    {
        [Fact]
        public void BuildWhoAreYouPayload_WithValidInputs_ReturnsCorrectFormat()
        {
            // Arrange
            byte machineType = 0x03;
            ushort firmwareType = 0x0004;
            byte resetFlag = 0x01;

            // Act
            byte[] payload = PayloadBuilder.BuildWhoAreYouPayload(machineType, firmwareType, resetFlag);

            // Assert
            Assert.Equal(4, payload.Length);
            Assert.Equal(0x03, payload[0]); // MACHINE_TYPE
            Assert.Equal(0x00, payload[1]); // FW_TYPE_H (big-endian)
            Assert.Equal(0x04, payload[2]); // FW_TYPE_L
            Assert.Equal(0x01, payload[3]); // RESET_FLAG
        }

        [Fact]
        public void BuildSetAddressPayload_WithValidInputs_ReturnsCorrectFormat()
        {
            // Arrange
            byte[] uuid = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C };
            uint stemAddress = 0x00030101u;

            // Act
            byte[] payload = PayloadBuilder.BuildSetAddressPayload(uuid, stemAddress);

            // Assert
            Assert.Equal(16, payload.Length);

            // Verifica UUID (primi 12 bytes)
            for (int i = 0; i < 12; i++)
            {
                Assert.Equal(uuid[i], payload[i]);
            }

            // Verifica indirizzo STEM (ultimi 4 bytes, big-endian)
            Assert.Equal(0x00, payload[12]); // Byte più significativo
            Assert.Equal(0x03, payload[13]);
            Assert.Equal(0x01, payload[14]);
            Assert.Equal(0x01, payload[15]); // Byte meno significativo
        }

        [Fact]
        public void BuildSetAddressPayload_WithInvalidUuid_ThrowsArgumentException()
        {
            // Arrange
            byte[] invalidUuid = new byte[] { 0x01, 0x02, 0x03 }; // Solo 3 bytes
            uint stemAddress = 0x00030101u;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                PayloadBuilder.BuildSetAddressPayload(invalidUuid, stemAddress));
        }

        [Fact]
        public void BuildWriteVariablePayload_WithValidInputs_ReturnsCorrectFormat()
        {
            // Arrange
            ushort variableId = 0x1234;
            byte[] value = new byte[] { 0xAA, 0xBB };

            // Act
            byte[] payload = PayloadBuilder.BuildWriteVariablePayload(variableId, value);

            // Assert
            Assert.Equal(4, payload.Length);
            Assert.Equal(0x12, payload[0]); // VAR_ID_H
            Assert.Equal(0x34, payload[1]); // VAR_ID_L
            Assert.Equal(0xAA, payload[2]); // VALUE[0]
            Assert.Equal(0xBB, payload[3]); // VALUE[1]
        }

        [Fact]
        public void BuildButtonPressExpectedPayload_WithValidInputs_ReturnsCorrectFormat()
        {
            // Arrange
            ushort buttonStatusVariableId = 0x0102;
            byte buttonMask = 0x08;

            // Act
            byte[] payload = PayloadBuilder.BuildButtonPressExpectedPayload(buttonStatusVariableId, buttonMask);

            // Assert
            Assert.Equal(5, payload.Length);
            Assert.Equal(0x00, payload[0]);
            Assert.Equal(0x02, payload[1]);
            Assert.Equal(0x01, payload[2]); // VAR_ID_H
            Assert.Equal(0x02, payload[3]); // VAR_ID_L
            Assert.Equal(0x08, payload[4]); // BUTTON_MASK
        }

        [Theory]
        [InlineData(0x0000, 0x00, 0x00)]
        [InlineData(0xFFFF, 0xFF, 0xFF)]
        [InlineData(0x1234, 0x12, 0x34)]
        [InlineData(0xABCD, 0xAB, 0xCD)]
        public void BuildWriteVariablePayload_WithVariousVariableIds_ExtractsBytesCorrectly(
            ushort variableId,
            byte expectedHigh,
            byte expectedLow)
        {
            // Arrange
            byte[] value = new byte[] { 0x00 };

            // Act
            byte[] payload = PayloadBuilder.BuildWriteVariablePayload(variableId, value);

            // Assert
            Assert.Equal(expectedHigh, payload[0]);
            Assert.Equal(expectedLow, payload[1]);
        }
    }
}
