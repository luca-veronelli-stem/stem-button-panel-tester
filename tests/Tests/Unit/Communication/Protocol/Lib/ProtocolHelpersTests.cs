using Communication.Protocol.Lib;

namespace Tests.Unit.Communication.Protocol.Lib
{
    /// <summary>
    /// Test unitari per i metodi statici di utilità ProtocolHelpers.
    /// Verifica le conversioni byte e il calcolo CRC-16 Modbus.
    /// </summary>
    public class ProtocolHelpersTests
    {
        #region ToLittleEndianBytes (ushort)

        [Theory]
        [InlineData(0x0000, new byte[] { 0x00, 0x00 })]
        [InlineData(0x00FF, new byte[] { 0xFF, 0x00 })]
        [InlineData(0xFF00, new byte[] { 0x00, 0xFF })]
        [InlineData(0xFFFF, new byte[] { 0xFF, 0xFF })]
        [InlineData(0x1234, new byte[] { 0x34, 0x12 })]
        [InlineData(0xABCD, new byte[] { 0xCD, 0xAB })]
        public void ToLittleEndianBytes_UShort_ReturnsCorrectBytes(ushort value, byte[] expected)
        {
            // Act
            byte[] result = value.ToLittleEndianBytes();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToLittleEndianBytes_UShort_ReturnsArrayOfLength2()
        {
            // Arrange
            ushort value = 0x1234;

            // Act
            byte[] result = value.ToLittleEndianBytes();

            // Assert
            Assert.Equal(2, result.Length);
        }

        #endregion

        #region ToLittleEndianBytes (uint)

        [Theory]
        [InlineData(0x00000000u, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(0x000000FFu, new byte[] { 0xFF, 0x00, 0x00, 0x00 })]
        [InlineData(0x0000FF00u, new byte[] { 0x00, 0xFF, 0x00, 0x00 })]
        [InlineData(0x00FF0000u, new byte[] { 0x00, 0x00, 0xFF, 0x00 })]
        [InlineData(0xFF000000u, new byte[] { 0x00, 0x00, 0x00, 0xFF })]
        [InlineData(0xFFFFFFFFu, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
        [InlineData(0x12345678u, new byte[] { 0x78, 0x56, 0x34, 0x12 })]
        [InlineData(0xDEADBEEFu, new byte[] { 0xEF, 0xBE, 0xAD, 0xDE })]
        public void ToLittleEndianBytes_UInt_ReturnsCorrectBytes(uint value, byte[] expected)
        {
            // Act
            byte[] result = value.ToLittleEndianBytes();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToLittleEndianBytes_UInt_ReturnsArrayOfLength4()
        {
            // Arrange
            uint value = 0x12345678;

            // Act
            byte[] result = value.ToLittleEndianBytes();

            // Assert
            Assert.Equal(4, result.Length);
        }

        #endregion

        #region ToBigEndianBytes

        [Theory]
        [InlineData(0x0000, new byte[] { 0x00, 0x00 })]
        [InlineData(0x00FF, new byte[] { 0x00, 0xFF })]
        [InlineData(0xFF00, new byte[] { 0xFF, 0x00 })]
        [InlineData(0x1234, new byte[] { 0x12, 0x34 })]
        public void ToBigEndianBytes_UShort_ReturnsCorrectBytes(ushort value, byte[] expected)
        {
            // Act
            byte[] result = value.ToBigEndianBytes();

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0x12345678u, new byte[] { 0x12, 0x34, 0x56, 0x78 })]
        [InlineData(0xDEADBEEFu, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
        public void ToBigEndianBytes_UInt_ReturnsCorrectBytes(uint value, byte[] expected)
        {
            // Act
            byte[] result = value.ToBigEndianBytes();

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region ToUInt16

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00 }, (ushort)0x0000)]
        [InlineData(new byte[] { 0xFF, 0x00 }, (ushort)0x00FF)]
        [InlineData(new byte[] { 0x00, 0xFF }, (ushort)0xFF00)]
        [InlineData(new byte[] { 0xFF, 0xFF }, (ushort)0xFFFF)]
        [InlineData(new byte[] { 0x34, 0x12 }, (ushort)0x1234)]
        [InlineData(new byte[] { 0xCD, 0xAB }, (ushort)0xABCD)]
        public void ToUInt16_ValidBytes_ReturnsCorrectValue(byte[] bytes, ushort expected)
        {
            // Act
            ushort result = bytes.ToUInt16();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToUInt16_NullBytes_ThrowsArgumentException()
        {
            // Arrange
            byte[]? bytes = null;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => bytes!.ToUInt16());
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00 })]
        public void ToUInt16_InvalidLength_ThrowsArgumentException(byte[] bytes)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => bytes.ToUInt16());
        }

        [Fact]
        public void ToUInt16_RoundTrip_PreservesValue()
        {
            // Arrange
            ushort original = 0x1234;

            // Act
            byte[] bytes = original.ToLittleEndianBytes();
            ushort result = bytes.ToUInt16();

            // Assert
            Assert.Equal(original, result);
        }

        #endregion

        #region ToInt32

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0)]
        [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x00 }, 1)]
        [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, -1)]
        [InlineData(new byte[] { 0x78, 0x56, 0x34, 0x12 }, 0x12345678)]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x80 }, int.MinValue)]
        [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }, int.MaxValue)]
        public void ToInt32_ValidBytes_ReturnsCorrectValue(byte[] bytes, int expected)
        {
            // Act
            int result = bytes.ToInt32();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToInt32_NullBytes_ThrowsArgumentException()
        {
            // Arrange
            byte[]? bytes = null;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => bytes!.ToInt32());
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 })]
        public void ToInt32_InvalidLength_ThrowsArgumentException(byte[] bytes)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => bytes.ToInt32());
        }

        #endregion

        #region ToUInt32

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0x00000000u)]
        [InlineData(new byte[] { 0xFF, 0x00, 0x00, 0x00 }, 0x000000FFu)]
        [InlineData(new byte[] { 0x00, 0xFF, 0x00, 0x00 }, 0x0000FF00u)]
        [InlineData(new byte[] { 0x00, 0x00, 0xFF, 0x00 }, 0x00FF0000u)]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0xFF }, 0xFF000000u)]
        [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0xFFFFFFFFu)]
        [InlineData(new byte[] { 0x78, 0x56, 0x34, 0x12 }, 0x12345678u)]
        [InlineData(new byte[] { 0xEF, 0xBE, 0xAD, 0xDE }, 0xDEADBEEFu)]
        public void ToUInt32_ValidBytes_ReturnsCorrectValue(byte[] bytes, uint expected)
        {
            // Act
            uint result = bytes.ToUInt32();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToUInt32_NullBytes_ThrowsArgumentException()
        {
            // Arrange
            byte[]? bytes = null;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => bytes!.ToUInt32());
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 })]
        public void ToUInt32_InvalidLength_ThrowsArgumentException(byte[] bytes)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => bytes.ToUInt32());
        }

        [Fact]
        public void ToUInt32_RoundTrip_PreservesValue()
        {
            // Arrange
            uint original = 0xDEADBEEF;

            // Act
            byte[] bytes = original.ToLittleEndianBytes();
            uint result = bytes.ToUInt32();

            // Assert
            Assert.Equal(original, result);
        }

        #endregion

        #region ReadBigEndian

        [Fact]
        public void ReadUInt16BigEndian_ValidBuffer_ReturnsCorrectValue()
        {
            // Arrange
            byte[] buffer = [0x12, 0x34];

            // Act
            ushort result = ProtocolHelpers.ReadUInt16BigEndian(buffer);

            // Assert
            Assert.Equal((ushort)0x1234, result);
        }

        [Fact]
        public void ReadUInt32BigEndian_ValidBuffer_ReturnsCorrectValue()
        {
            // Arrange
            byte[] buffer = [0x12, 0x34, 0x56, 0x78];

            // Act
            uint result = ProtocolHelpers.ReadUInt32BigEndian(buffer);

            // Assert
            Assert.Equal(0x12345678u, result);
        }

        [Fact]
        public void ReadUInt16BigEndian_WithOffset_ReturnsCorrectValue()
        {
            // Arrange
            byte[] buffer = [0x00, 0x00, 0x12, 0x34];

            // Act
            ushort result = ProtocolHelpers.ReadUInt16BigEndian(buffer, 2);

            // Assert
            Assert.Equal((ushort)0x1234, result);
        }

        #endregion

        #region CalculateCrc

        [Fact]
        public void CalculateCrc_EmptyData_ReturnsInitialValue()
        {
            // Arrange
            byte[] data = [];

            // Act
            ushort result = ProtocolHelpers.CalculateCrc(data);

            // Assert - CRC-16 Modbus con inizializzazione 0xFFFF e nessun dato restituisce 0xFFFF
            Assert.Equal(0xFFFF, result);
        }

        [Fact]
        public void CalculateCrc_NullData_ThrowsArgumentNullException()
        {
            // Arrange
            byte[]? data = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ProtocolHelpers.CalculateCrc(data!));
        }

        [Fact]
        public void CalculateCrc_SingleByte_ReturnsCorrectCrc()
        {
            // Arrange
            byte[] data = [0x31]; // ASCII '1'

            // Act
            ushort result = ProtocolHelpers.CalculateCrc(data);

            // Assert - Il CRC deve essere diverso dal valore iniziale
            Assert.NotEqual(0xFFFF, result);
        }

        [Fact]
        public void CalculateCrc_SameData_ReturnsSameCrc()
        {
            // Arrange
            byte[] data = [1, 2, 3, 4, 5];

            // Act
            ushort result1 = ProtocolHelpers.CalculateCrc(data);
            ushort result2 = ProtocolHelpers.CalculateCrc(data);

            // Assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void CalculateCrc_DifferentData_ReturnsDifferentCrc()
        {
            // Arrange
            byte[] data1 = [1, 2, 3, 4, 5];
            byte[] data2 = [1, 2, 3, 4, 6];

            // Act
            ushort result1 = ProtocolHelpers.CalculateCrc(data1);
            ushort result2 = ProtocolHelpers.CalculateCrc(data2);

            // Assert
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        public void CalculateCrc_KnownTestVector_ReturnsExpectedValue()
        {
            // Arrange - Vettore di test standard CRC-16 Modbus: "123456789"
            // CRC-16 Modbus (init 0xFFFF, poly 0xA001) per "123456789" = 0x4B37
            byte[] data = "123456789"u8.ToArray();

            // Act
            ushort result = ProtocolHelpers.CalculateCrc(data);

            // Assert
            Assert.Equal(0x4B37, result);
        }

        [Fact]
        public void CalculateCrc_LargeData_CompletesSuccessfully()
        {
            // Arrange
            byte[] data = new byte[10000];
            Random.Shared.NextBytes(data);

            // Act
            ushort result = ProtocolHelpers.CalculateCrc(data);

            // Assert - Verifica solo che completi e restituisca un valore valido
            Assert.True(result >= 0 && result <= 0xFFFF);
        }

        [Fact]
        public void CalculateCrc_OrderMatters()
        {
            // Arrange
            byte[] data1 = [0x01, 0x02];
            byte[] data2 = [0x02, 0x01];

            // Act
            ushort result1 = ProtocolHelpers.CalculateCrc(data1);
            ushort result2 = ProtocolHelpers.CalculateCrc(data2);

            // Assert
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        public void CalculateCrc_SpanOverload_ReturnsSameAsArrayOverload()
        {
            // Arrange
            byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05];

            // Act
            ushort resultArray = ProtocolHelpers.CalculateCrc(data);
            ushort resultSpan = ProtocolHelpers.CalculateCrc(data.AsSpan());

            // Assert
            Assert.Equal(resultArray, resultSpan);
        }

        #endregion
    }
}
