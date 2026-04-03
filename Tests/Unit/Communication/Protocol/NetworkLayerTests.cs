using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Tests.Helpers;

namespace Tests.Unit.Communication.Protocol
{
    /// <summary>
    /// Unit tests for the NetworkLayer class.
    /// Tests chunk creation, splitting logic, and NetInfo correctness.
    /// </summary>
    public class NetworkLayerTests
    {
        #region Create Tests

        public class CreateMethod
        {
            [Fact]
            public void WithValidInput_CreatesPackets()
            {
                byte[] transportPacket = [0x01, 0x02, 0x03, 0x04, 0x05];

                var layer = NetworkLayer.Create(0x12345678, transportPacket, chunkSize: 10);

                Assert.NotEmpty(layer.NetworkPackets);
            }

            [Fact]
            public void WithNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    NetworkLayer.Create(0, null!, 10));
            }

            [Theory]
            [InlineData(0)]
            [InlineData(-1)]
            [InlineData(-100)]
            public void WithInvalidChunkSize_ThrowsArgumentOutOfRangeException(int chunkSize)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    NetworkLayer.Create(0, [0x01], chunkSize));
            }

            [Fact]
            public void WithEmptyTransportPacket_CreatesNoPackets()
            {
                var layer = NetworkLayer.Create(0, [], chunkSize: 10);

                Assert.Empty(layer.NetworkPackets);
            }
        }

        #endregion

        #region Single Chunk Tests

        public class SingleChunkScenarios
        {
            [Fact]
            public void DataFitsInOneChunk_CreatesSinglePacket()
            {
                byte[] transportPacket = [0x01, 0x02, 0x03, 0x04, 0x05];

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize: 10);

                Assert.Single(layer.NetworkPackets);
                Assert.Equal(transportPacket, layer.NetworkPackets[0].Chunk);
            }

            [Fact]
            public void SingleChunk_HasCorrectNetInfo()
            {
                var layer = NetworkLayer.Create(0, [0x01, 0x02, 0x03], chunkSize: 10);

                var netInfo = NetInfo.FromBytes(layer.NetworkPackets[0].NetInfo);

                Assert.Equal(0, netInfo.RemainingChunks);
                Assert.False(netInfo.SetLength);
                Assert.InRange(netInfo.PacketId, ProtocolConfig.MinPacketId, ProtocolConfig.MaxPacketId);
                Assert.Equal(ProtocolVersion.V1, netInfo.Version);
            }

            [Fact]
            public void SingleChunk_HasCorrectRecipientId()
            {
                uint recipientId = 0xDEADBEEF;

                var layer = NetworkLayer.Create(recipientId, [0x01, 0x02, 0x03], chunkSize: 10);

                Assert.Equal(recipientId, layer.NetworkPackets[0].Id);
            }
        }

        #endregion

        #region Multi-Chunk Tests

        public class MultiChunkScenarios
        {
            [Theory]
            [InlineData(10, 3, 4)]    // 10 bytes / 3 = 4 chunks (3+3+3+1)
            [InlineData(12, 3, 4)]    // 12 bytes / 3 = 4 chunks (3+3+3+3)
            [InlineData(12, 4, 3)]    // 12 bytes / 4 = 3 chunks (4+4+4)
            [InlineData(5, 1, 5)]     // 5 bytes / 1 = 5 chunks
            public void CreatesCorrectNumberOfChunks(int dataSize, int chunkSize, int expectedChunks)
            {
                byte[] transportPacket = new byte[dataSize];

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize);

                Assert.Equal(expectedChunks, layer.NetworkPackets.Count);
            }

            [Fact]
            public void RemainingChunks_DecreaseCorrectly()
            {
                byte[] transportPacket = new byte[12];

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize: 3);

                for (int i = 0; i < layer.NetworkPackets.Count; i++)
                {
                    var netInfo = NetInfo.FromBytes(layer.NetworkPackets[i].NetInfo);
                    int expectedRemaining = layer.NetworkPackets.Count - i - 1;
                    Assert.Equal(expectedRemaining, netInfo.RemainingChunks);
                }
            }

            [Fact]
            public void AllChunks_ShareSamePacketId()
            {
                var layer = NetworkLayer.Create(0, new byte[20], chunkSize: 3);

                var firstPacketId = NetInfo.FromBytes(layer.NetworkPackets[0].NetInfo).PacketId;

                Assert.All(layer.NetworkPackets, p =>
                {
                    var netInfo = NetInfo.FromBytes(p.NetInfo);
                    Assert.Equal(firstPacketId, netInfo.PacketId);
                });
            }

            [Fact]
            public void AllChunks_ShareSameRecipientId()
            {
                uint recipientId = 0x12345678;

                var layer = NetworkLayer.Create(recipientId, new byte[20], chunkSize: 3);

                Assert.All(layer.NetworkPackets, p => Assert.Equal(recipientId, p.Id));
            }

            [Fact]
            public void CombinedChunks_EqualOriginalData()
            {
                byte[] transportPacket = ProtocolTestBuilders.CreateSequentialPayload(25);

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize: 4);

                var combined = layer.NetworkPackets.SelectMany(p => p.Chunk).ToArray();
                Assert.Equal(transportPacket, combined);
            }

            [Fact]
            public void LastChunk_SmallerWhenNotExactMultiple()
            {
                byte[] transportPacket = new byte[10];

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize: 3);

                Assert.Equal(4, layer.NetworkPackets.Count);
                Assert.Equal(3, layer.NetworkPackets[0].Chunk.Length);
                Assert.Equal(3, layer.NetworkPackets[1].Chunk.Length);
                Assert.Equal(3, layer.NetworkPackets[2].Chunk.Length);
                Assert.Single(layer.NetworkPackets[3].Chunk);
            }

            [Fact]
            public void ExactMultiple_AllChunksSameSize()
            {
                byte[] transportPacket = new byte[12];

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize: 4);

                Assert.Equal(3, layer.NetworkPackets.Count);
                Assert.All(layer.NetworkPackets, p => Assert.Equal(4, p.Chunk.Length));
            }
        }

        #endregion

        #region Packet ID Tests

        public class PacketIdBehavior
        {
            [Fact]
            public void PacketId_WithinValidRange()
            {
                var layer = NetworkLayer.Create(0, [0x01, 0x02, 0x03], chunkSize: 10);

                var netInfo = NetInfo.FromBytes(layer.NetworkPackets[0].NetInfo);

                Assert.InRange(netInfo.PacketId, ProtocolConfig.MinPacketId, ProtocolConfig.MaxPacketId);
            }

            [Fact]
            public void MultipleLayers_CyclePacketIds()
            {
                var packetIds = new HashSet<int>();

                for (int i = 0; i < 10; i++)
                {
                    var layer = NetworkLayer.Create(0, [0x01, 0x02, 0x03], chunkSize: 10);
                    var netInfo = NetInfo.FromBytes(layer.NetworkPackets[0].NetInfo);
                    packetIds.Add(netInfo.PacketId);
                }

                // Should cycle through at least 2 different IDs
                Assert.True(packetIds.Count > 1);
                Assert.All(packetIds, id =>
                    Assert.InRange(id, ProtocolConfig.MinPacketId, ProtocolConfig.MaxPacketId));
            }
        }

        #endregion

        #region Edge Cases

        public class EdgeCases
        {
            [Fact]
            public void SingleByteData_SingleChunk()
            {
                var layer = NetworkLayer.Create(0, [0xFF], chunkSize: 1);

                Assert.Single(layer.NetworkPackets);
                Assert.Equal([0xFF], layer.NetworkPackets[0].Chunk);
            }

            [Fact]
            public void ChunkSizeOne_CreatesPacketPerByte()
            {
                byte[] transportPacket = [0x01, 0x02, 0x03, 0x04, 0x05];

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize: 1);

                Assert.Equal(5, layer.NetworkPackets.Count);
                for (int i = 0; i < 5; i++)
                {
                    Assert.Single(layer.NetworkPackets[i].Chunk);
                    Assert.Equal(transportPacket[i], layer.NetworkPackets[i].Chunk[0]);
                }
            }

            [Theory]
            [InlineData(0u)]
            [InlineData(1u)]
            [InlineData(0x7FFFFFFFu)]
            [InlineData(0xFFFFFFFFu)]
            public void AllRecipientIdRanges_Work(uint recipientId)
            {
                var layer = NetworkLayer.Create(recipientId, [0x01, 0x02, 0x03], chunkSize: 10);

                Assert.All(layer.NetworkPackets, p => Assert.Equal(recipientId, p.Id));
            }

            [Fact]
            public void LargeData_PreservesIntegrity()
            {
                byte[] transportPacket = ProtocolTestBuilders.CreateRandomPayload(8000);

                var layer = NetworkLayer.Create(0, transportPacket, chunkSize: 8);

                var combined = layer.NetworkPackets.SelectMany(p => p.Chunk).ToArray();
                Assert.Equal(transportPacket, combined);
            }

            [Fact]
            public void NetworkPacketChunk_HasCorrectStructure()
            {
                uint recipientId = 0x12345678;
                byte[] transportPacket = [0x01, 0x02, 0x03, 0x04, 0x05];

                var layer = NetworkLayer.Create(recipientId, transportPacket, chunkSize: 10);
                var packet = layer.NetworkPackets[0];

                Assert.Equal(2, packet.NetInfo.Length);
                Assert.Equal(recipientId, packet.Id);
                Assert.Equal(transportPacket.Length, packet.Chunk.Length);
            }
        }

        #endregion
    }
}
