using Communication.Protocol.Lib;
using Core.Enums;

namespace Tests.Unit.Communication.Protocol.Lib
{
    /// <summary>
    /// Unit tests for NetInfo record.
    /// Tests encoding to bytes and decoding from bytes.
    /// </summary>
    public class NetInfoTests
    {
        #region ToBytes Tests

        [Fact]
        public void ToBytes_ValidNetInfo_ReturnsCorrectBytes()
        {
            // Arrange - RemainingChunks=0, SetLength=false, PacketId=1, Version=V1(1)
            var netInfo = new NetInfo(0, false, 1, ProtocolVersion.V1);

            // Act
            byte[] result = netInfo.ToBytes();

            // Assert
            // Format: (RemainingChunks << 6) | (SetLength << 5) | (PacketId << 2) | Version
            // (0 << 6) | (0 << 5) | (1 << 2) | 1 = 0x05 (V1 = 1, not 0)
            Assert.Equal(2, result.Length);
            Assert.Equal(0x05, result[0]);
            Assert.Equal(0x00, result[1]);
        }

        [Fact]
        public void ToBytes_WithSetLength_SetsCorrectBit()
        {
            // Arrange
            var netInfo = new NetInfo(0, true, 1, ProtocolVersion.V1);

            // Act
            byte[] result = netInfo.ToBytes();

            // Assert
            // (0 << 6) | (1 << 5) | (1 << 2) | 1 = 0x25 (V1 = 1)
            Assert.Equal(0x25, result[0]);
        }

        [Fact]
        public void ToBytes_WithRemainingChunks_SetsCorrectBits()
        {
            // Arrange - 5 remaining chunks
            var netInfo = new NetInfo(5, false, 1, ProtocolVersion.V1);

            // Act
            byte[] result = netInfo.ToBytes();

            // Assert
            // (5 << 6) | (0 << 5) | (1 << 2) | 1 = 0x45 + overflow
            ushort expected = 5 << 6 | 0 << 5 | 1 << 2 | (byte)ProtocolVersion.V1;
            Assert.Equal((byte)expected, result[0]);
            Assert.Equal((byte)(expected >> 8), result[1]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void ToBytes_AllValidPacketIds_Succeeds(int packetId)
        {
            // Arrange
            var netInfo = new NetInfo(0, false, packetId, ProtocolVersion.V1);

            // Act
            byte[] result = netInfo.ToBytes();

            // Assert
            Assert.Equal(2, result.Length);
            // Verify packet ID is encoded correctly
            var decoded = NetInfo.FromBytes(result);
            Assert.Equal(packetId, decoded.PacketId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        [InlineData(-1)]
        [InlineData(100)]
        public void ToBytes_InvalidPacketId_ThrowsArgumentOutOfRangeException(int packetId)
        {
            // Arrange
            var netInfo = new NetInfo(0, false, packetId, ProtocolVersion.V1);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => netInfo.ToBytes());
        }

        [Fact]
        public void ToBytes_LargeRemainingChunks_EncodesCorrectly()
        {
            // Arrange - Maximum 10 bits for remaining chunks (0-1023)
            var netInfo = new NetInfo(1023, false, 1, ProtocolVersion.V1);

            // Act
            byte[] result = netInfo.ToBytes();

            // Assert
            var decoded = NetInfo.FromBytes(result);
            Assert.Equal(1023, decoded.RemainingChunks);
        }

        #endregion

        #region FromBytes Tests

        [Fact]
        public void FromBytes_ValidBytes_ReturnsCorrectNetInfo()
        {
            // Arrange - NetInfo with PacketId=1, no remaining, no setLength, V1
            // (0 << 6) | (0 << 5) | (1 << 2) | 1 = 0x05
            byte[] bytes = [0x05, 0x00];

            // Act
            var result = NetInfo.FromBytes(bytes);

            // Assert
            Assert.Equal(0, result.RemainingChunks);
            Assert.False(result.SetLength);
            Assert.Equal(1, result.PacketId);
            Assert.Equal(ProtocolVersion.V1, result.Version);
        }

        [Fact]
        public void FromBytes_WithSetLength_ParsesCorrectly()
        {
            // Arrange - SetLength bit set: (1 << 5) | (1 << 2) | 1 = 0x25
            byte[] bytes = [0x25, 0x00];

            // Act
            var result = NetInfo.FromBytes(bytes);

            // Assert
            Assert.True(result.SetLength);
            Assert.Equal(1, result.PacketId);
            Assert.Equal(ProtocolVersion.V1, result.Version);
        }

        [Fact]
        public void FromBytes_WithRemainingChunks_ParsesCorrectly()
        {
            // Arrange - 3 remaining chunks: (3 << 6) | (1 << 2) | 1 = 0xC5
            byte[] bytes = [0xC5, 0x00];

            // Act
            var result = NetInfo.FromBytes(bytes);

            // Assert
            Assert.Equal(3, result.RemainingChunks);
            Assert.Equal(1, result.PacketId);
            Assert.Equal(ProtocolVersion.V1, result.Version);
        }

        [Theory]
        [InlineData(new byte[] { 0x05, 0x00 }, 1)] // (1 << 2) | 1 = 0x05
        [InlineData(new byte[] { 0x09, 0x00 }, 2)] // (2 << 2) | 1 = 0x09
        [InlineData(new byte[] { 0x0D, 0x00 }, 3)] // (3 << 2) | 1 = 0x0D
        [InlineData(new byte[] { 0x11, 0x00 }, 4)] // (4 << 2) | 1 = 0x11
        [InlineData(new byte[] { 0x15, 0x00 }, 5)] // (5 << 2) | 1 = 0x15
        [InlineData(new byte[] { 0x19, 0x00 }, 6)] // (6 << 2) | 1 = 0x19
        [InlineData(new byte[] { 0x1D, 0x00 }, 7)] // (7 << 2) | 1 = 0x1D
        public void FromBytes_AllValidPacketIds_ParsesCorrectly(byte[] bytes, int expectedPacketId)
        {
            // Act
            var result = NetInfo.FromBytes(bytes);

            // Assert
            Assert.Equal(expectedPacketId, result.PacketId);
            Assert.Equal(ProtocolVersion.V1, result.Version);
        }

        [Fact]
        public void FromBytes_NullBytes_ThrowsArgumentNullException()
        {
            // Arrange
            byte[]? bytes = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => NetInfo.FromBytes(bytes!));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00 })]
        public void FromBytes_InvalidLength_ThrowsArgumentException(byte[] bytes)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => NetInfo.FromBytes(bytes));
        }

        [Theory]
        [InlineData(new byte[] { 0x01, 0x00 })] // PacketId = 0 (with V1)
        [InlineData(new byte[] { 0x21, 0x00 })] // PacketId = 8 (with V1)
        public void FromBytes_InvalidPacketId_ThrowsProtocolException(byte[] bytes)
        {
            // Act & Assert
            Assert.Throws<ProtocolException>(() => NetInfo.FromBytes(bytes));
        }

        #endregion

        #region Round-Trip Tests

        [Theory]
        [InlineData(0, false, 1, ProtocolVersion.V1)]
        [InlineData(5, false, 3, ProtocolVersion.V1)]
        [InlineData(10, true, 7, ProtocolVersion.V1)]
        [InlineData(100, false, 4, ProtocolVersion.V1)]
        [InlineData(1023, true, 5, ProtocolVersion.V1)]
        public void RoundTrip_ToBytes_FromBytes_PreservesValues(
            int remainingChunks, bool setLength, int packetId, ProtocolVersion version)
        {
            // Arrange
            var original = new NetInfo(remainingChunks, setLength, packetId, version);

            // Act
            byte[] bytes = original.ToBytes();
            var result = NetInfo.FromBytes(bytes);

            // Assert
            Assert.Equal(original.RemainingChunks, result.RemainingChunks);
            Assert.Equal(original.SetLength, result.SetLength);
            Assert.Equal(original.PacketId, result.PacketId);
            Assert.Equal(original.Version, result.Version);
        }

        [Fact]
        public void RoundTrip_AllValidPacketIds_PreserveValues()
        {
            for (int packetId = ProtocolConfig.MinPacketId; packetId <= ProtocolConfig.MaxPacketId; packetId++)
            {
                // Arrange
                var original = new NetInfo(0, false, packetId, ProtocolVersion.V1);

                // Act
                byte[] bytes = original.ToBytes();
                var result = NetInfo.FromBytes(bytes);

                // Assert
                Assert.Equal(packetId, result.PacketId);
            }
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void NetInfo_IsRecord_SupportsEquality()
        {
            // Arrange
            var netInfo1 = new NetInfo(0, false, 1, ProtocolVersion.V1);
            var netInfo2 = new NetInfo(0, false, 1, ProtocolVersion.V1);
            var netInfo3 = new NetInfo(1, false, 1, ProtocolVersion.V1);

            // Assert
            Assert.Equal(netInfo1, netInfo2);
            Assert.NotEqual(netInfo1, netInfo3);
        }

        [Fact]
        public void NetInfo_ToString_ReturnsReadableFormat()
        {
            // Arrange
            var netInfo = new NetInfo(5, true, 3, ProtocolVersion.V1);

            // Act
            string result = netInfo.ToString();

            // Assert - Record automatically generates ToString with property values
            Assert.Contains("5", result);
            Assert.Contains("True", result);
            Assert.Contains("3", result);
        }

        #endregion
    }
}
