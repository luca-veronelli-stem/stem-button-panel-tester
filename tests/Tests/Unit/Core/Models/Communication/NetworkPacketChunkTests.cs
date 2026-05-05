using Core.Models.Communication;

namespace Tests.Unit.Core.Models.Communication
{
    /// <summary>
    /// Unit tests for NetworkPacketChunk record.
    /// Tests record construction, equality, and immutability.
    /// </summary>
    public class NetworkPacketChunkTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_SetsAllProperties()
        {
            // Arrange
            byte[] netInfo = [0x01, 0x02];
            uint id = 0x12345678;
            byte[] chunk = [0x03, 0x04, 0x05];

            // Act
            var packet = new NetworkPacketChunk(netInfo, id, chunk);

            // Assert
            Assert.Equal(netInfo, packet.NetInfo);
            Assert.Equal(id, packet.Id);
            Assert.Equal(chunk, packet.Chunk);
        }

        [Fact]
        public void Constructor_EmptyArrays_Succeeds()
        {
            // Arrange
            byte[] netInfo = [];
            byte[] chunk = [];

            // Act
            var packet = new NetworkPacketChunk(netInfo, 0, chunk);

            // Assert
            Assert.Empty(packet.NetInfo);
            Assert.Empty(packet.Chunk);
            Assert.Equal(0u, packet.Id);
        }

        [Fact]
        public void Constructor_NullArrays_Succeeds()
        {
            // Act
            var packet = new NetworkPacketChunk(null!, 0, null!);

            // Assert
            Assert.Null(packet.NetInfo);
            Assert.Null(packet.Chunk);
        }

        [Fact]
        public void Constructor_LargeArrays_Succeeds()
        {
            // Arrange
            byte[] netInfo = new byte[1000];
            byte[] chunk = new byte[10000];
            Random.Shared.NextBytes(netInfo);
            Random.Shared.NextBytes(chunk);

            // Act
            var packet = new NetworkPacketChunk(netInfo, uint.MaxValue, chunk);

            // Assert
            Assert.Equal(1000, packet.NetInfo.Length);
            Assert.Equal(10000, packet.Chunk.Length);
            Assert.Equal(uint.MaxValue, packet.Id);
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equality_SameValues_AreEqual()
        {
            // Arrange
            byte[] netInfo = [0x01, 0x02];
            uint id = 123;
            byte[] chunk = [0x03, 0x04];

            // Note: Records compare by value for primitive types and by reference for arrays
            var packet1 = new NetworkPacketChunk(netInfo, id, chunk);
            var packet2 = new NetworkPacketChunk(netInfo, id, chunk);

            // Assert - Same array references, so they should be equal
            Assert.Equal(packet1, packet2);
        }

        [Fact]
        public void Equality_DifferentArrayReferences_SameValues_NotEqual()
        {
            // Arrange
            byte[] netInfo1 = [0x01, 0x02];
            byte[] netInfo2 = [0x01, 0x02];
            byte[] chunk1 = [0x03, 0x04];
            byte[] chunk2 = [0x03, 0x04];

            var packet1 = new NetworkPacketChunk(netInfo1, 123, chunk1);
            var packet2 = new NetworkPacketChunk(netInfo2, 123, chunk2);

            // Assert - Different array references, so records are not equal
            Assert.NotEqual(packet1, packet2);
        }

        [Fact]
        public void Equality_DifferentIds_NotEqual()
        {
            // Arrange
            byte[] netInfo = [0x01, 0x02];
            byte[] chunk = [0x03, 0x04];

            var packet1 = new NetworkPacketChunk(netInfo, 123, chunk);
            var packet2 = new NetworkPacketChunk(netInfo, 456, chunk);

            // Assert
            Assert.NotEqual(packet1, packet2);
        }

        #endregion

        #region Record Behavior Tests

        [Fact]
        public void Record_HasValueSemantics()
        {
            // Arrange
            byte[] netInfo = [0x01];
            byte[] chunk = [0x02];
            var packet = new NetworkPacketChunk(netInfo, 100, chunk);

            // Act & Assert - Record should have ToString, GetHashCode, Equals
            Assert.NotNull(packet.ToString());
            Assert.NotEqual(0, packet.GetHashCode());
        }

        [Fact]
        public void Record_Deconstruction_Works()
        {
            // Arrange
            byte[] netInfo = [0x01, 0x02];
            uint id = 999;
            byte[] chunk = [0x03, 0x04, 0x05];
            var packet = new NetworkPacketChunk(netInfo, id, chunk);

            // Act - Deconstruct the record
            (byte[]? deconstructedNetInfo, uint deconstructedId, byte[]? deconstructedChunk) = packet;

            // Assert
            Assert.Equal(netInfo, deconstructedNetInfo);
            Assert.Equal(id, deconstructedId);
            Assert.Equal(chunk, deconstructedChunk);
        }

        [Fact]
        public void Record_With_CreatesNewInstance()
        {
            // Arrange
            byte[] netInfo = [0x01];
            byte[] chunk = [0x02];
            var original = new NetworkPacketChunk(netInfo, 100, chunk);

            // Act - Use 'with' to create modified copy
            NetworkPacketChunk modified = original with { Id = 200 };

            // Assert
            Assert.Equal(100u, original.Id);
            Assert.Equal(200u, modified.Id);
            Assert.Same(original.NetInfo, modified.NetInfo); // Same reference
            Assert.Same(original.Chunk, modified.Chunk); // Same reference
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void Id_ZeroValue_Allowed()
        {
            // Act
            var packet = new NetworkPacketChunk([0x01], 0, [0x02]);

            // Assert
            Assert.Equal(0u, packet.Id);
        }

        [Fact]
        public void Id_MaxValue_Allowed()
        {
            // Act
            var packet = new NetworkPacketChunk([0x01], uint.MaxValue, [0x02]);

            // Assert
            Assert.Equal(uint.MaxValue, packet.Id);
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(0x7FFu)]
        [InlineData(0x800u)]
        [InlineData(0x1FFFFFFFu)]
        [InlineData(uint.MaxValue)]
        public void Id_VariousValues_Succeeds(uint id)
        {
            // Act
            var packet = new NetworkPacketChunk([0x01], id, [0x02]);

            // Assert
            Assert.Equal(id, packet.Id);
        }

        #endregion

        #region Usage Pattern Tests

        [Fact]
        public void CanBeUsedInCollection()
        {
            // Arrange
            var packets = new List<NetworkPacketChunk>
            {
                new([0x01, 0x02], 1, [0x10]),
                new([0x03, 0x04], 2, [0x20]),
                new([0x05, 0x06], 3, [0x30])
            };

            // Act & Assert
            Assert.Equal(3, packets.Count);
            Assert.Equal(1u, packets[0].Id);
            Assert.Equal(2u, packets[1].Id);
            Assert.Equal(3u, packets[2].Id);
        }

        [Fact]
        public void CanBeUsedWithLinq()
        {
            // Arrange
            var packets = new List<NetworkPacketChunk>
            {
                new([0x01], 100, [0x10]),
                new([0x02], 200, [0x20]),
                new([0x03], 300, [0x30])
            };

            // Act
            var filtered = packets.Where(p => p.Id > 150).ToList();
            var ids = packets.Select(p => p.Id).ToList();

            // Assert
            Assert.Equal(2, filtered.Count);
            Assert.Equal([100u, 200u, 300u], ids);
        }

        [Fact]
        public void CanConcatenateNetInfoAndChunk()
        {
            // Arrange
            byte[] netInfo = [0x01, 0x02];
            byte[] chunk = [0x03, 0x04, 0x05];
            var packet = new NetworkPacketChunk(netInfo, 123, chunk);

            // Act - Common pattern: combine NetInfo and Chunk
            byte[] combined = [.. packet.NetInfo, .. packet.Chunk];

            // Assert
            Assert.Equal([0x01, 0x02, 0x03, 0x04, 0x05], combined);
        }

        #endregion
    }
}
