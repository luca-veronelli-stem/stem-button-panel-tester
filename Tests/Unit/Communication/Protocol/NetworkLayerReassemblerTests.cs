using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Tests.Helpers;

namespace Tests.Unit.Communication.Protocol
{
    /// <summary>
    /// Unit tests for NetworkLayerReassembler.
    /// Tests chunk reassembly, event handling, and edge cases.
    /// </summary>
    public class NetworkLayerReassemblerTests : IDisposable
    {
        private readonly NetworkLayerReassembler _reassembler;
        private readonly List<byte[]> _reassembledPackets;
        private readonly List<string> _diagnosticMessages;

        public NetworkLayerReassemblerTests()
        {
            _reassembler = new NetworkLayerReassembler();
            _reassembledPackets = [];
            _diagnosticMessages = [];

            _reassembler.PacketReassembled += packet => _reassembledPackets.Add(packet);
            _reassembler.DiagnosticMessage += msg => _diagnosticMessages.Add(msg);
        }

        public void Dispose()
        {
            _reassembler.Dispose();
        }

        #region Single Chunk Tests

        public class SingleChunkScenarios : NetworkLayerReassemblerTests
        {
            [Fact]
            public void SingleChunk_RaisesPacketReassembled()
            {
                // Arrange - Create a valid transport packet
                var transportPacket = CreateMinimalTransportPacket();
                var chunk = CreateChunkWithNetInfo(transportPacket, remainingChunks: 0, packetId: 1);

                // Act
                _reassembler.ProcessReceivedChunk(chunk);

                // Assert
                Assert.Single(_reassembledPackets);
                Assert.Equal(transportPacket, _reassembledPackets[0]);
            }

            [Fact]
            public void SingleChunk_WithDifferentPacketIds_AllReassembled()
            {
                for (int packetId = 1; packetId <= 7; packetId++)
                {
                    var transportPacket = CreateMinimalTransportPacket((byte)packetId);
                    var chunk = CreateChunkWithNetInfo(transportPacket, remainingChunks: 0, packetId: packetId);

                    _reassembler.ProcessReceivedChunk(chunk);
                }

                Assert.Equal(7, _reassembledPackets.Count);
            }
        }

        #endregion

        #region Multi-Chunk Reassembly Tests

        public class MultiChunkReassembly : NetworkLayerReassemblerTests
        {
            [Fact]
            public void TwoChunks_ReassemblesCorrectly()
            {
                // Arrange - Split transport packet into 2 chunks
                var transportPacket = CreateMinimalTransportPacket();
                int splitPoint = transportPacket.Length / 2;
                byte[] part1 = transportPacket[..splitPoint];
                byte[] part2 = transportPacket[splitPoint..];

                var chunk1 = CreateChunkWithNetInfo(part1, remainingChunks: 1, packetId: 1);
                var chunk2 = CreateChunkWithNetInfo(part2, remainingChunks: 0, packetId: 1);

                // Act
                _reassembler.ProcessReceivedChunk(chunk1);
                Assert.Empty(_reassembledPackets); // Not complete yet

                _reassembler.ProcessReceivedChunk(chunk2);

                // Assert
                Assert.Single(_reassembledPackets);
                Assert.Equal(transportPacket, _reassembledPackets[0]);
            }

            [Fact]
            public void ThreeChunks_ReassemblesCorrectly()
            {
                // Arrange
                var transportPacket = CreateLargerTransportPacket(30);
                var chunks = SplitIntoChunks(transportPacket, chunkSize: 10, packetId: 2);

                // Act
                foreach (var chunk in chunks)
                {
                    _reassembler.ProcessReceivedChunk(chunk);
                }

                // Assert
                Assert.Single(_reassembledPackets);
                Assert.Equal(transportPacket, _reassembledPackets[0]);
            }

            [Fact]
            public void ManyChunks_ReassemblesCorrectly()
            {
                // Arrange
                var transportPacket = CreateLargerTransportPacket(100);
                var chunks = SplitIntoChunks(transportPacket, chunkSize: 8, packetId: 3);

                // Act
                foreach (var chunk in chunks)
                {
                    _reassembler.ProcessReceivedChunk(chunk);
                }

                // Assert
                Assert.Single(_reassembledPackets);
                Assert.Equal(transportPacket, _reassembledPackets[0]);
            }

            [Fact]
            public void IncompleteSequence_DoesNotRaiseEvent()
            {
                // Arrange - Send only first chunk of multi-chunk sequence
                var transportPacket = CreateLargerTransportPacket(20);
                var chunks = SplitIntoChunks(transportPacket, chunkSize: 8, packetId: 4);

                // Act - Only send first chunk
                _reassembler.ProcessReceivedChunk(chunks[0]);

                // Assert
                Assert.Empty(_reassembledPackets);
            }
        }

        #endregion

        #region SetLength Flag Tests

        public class SetLengthHandling : NetworkLayerReassemblerTests
        {
            [Fact]
            public void SetLength_ExtractsCorrectLength()
            {
                // Arrange - Create packet with length prefix
                var transportPacket = CreateMinimalTransportPacket();
                ushort length = (ushort)transportPacket.Length;
                byte[] withLength = [.. BitConverter.GetBytes(length), .. transportPacket];

                var chunk = CreateChunkWithNetInfo(withLength, remainingChunks: 0, packetId: 1, setLength: true);

                // Act
                _reassembler.ProcessReceivedChunk(chunk);

                // Assert
                Assert.Single(_reassembledPackets);
                Assert.Equal(transportPacket, _reassembledPackets[0]);
            }

            [Fact]
            public void SetLength_SingleChunk_ExtractsCorrectly()
            {
                // Arrange - Single chunk with SetLength flag
                var transportPacket = CreateMinimalTransportPacket();
                ushort length = (ushort)transportPacket.Length;
                byte[] withLength = [.. BitConverter.GetBytes(length), .. transportPacket];

                var chunk = CreateChunkWithNetInfo(withLength, remainingChunks: 0, packetId: 2, setLength: true);

                // Act
                _reassembler.ProcessReceivedChunk(chunk);

                // Assert
                Assert.Single(_reassembledPackets);
                Assert.Equal(transportPacket, _reassembledPackets[0]);
            }

            [Fact]
            public void SetLength_ResetsBufferForNewSequence()
            {
                // Arrange - First send incomplete sequence
                var packet1 = CreateLargerTransportPacket(20);
                var incompleteChunks = SplitIntoChunks(packet1, chunkSize: 8, packetId: 1);
                _reassembler.ProcessReceivedChunk(incompleteChunks[0]); // Only first chunk

                // Now send new sequence with same packetId but SetLength flag
                var packet2 = CreateMinimalTransportPacket();
                ushort length = (ushort)packet2.Length;
                byte[] withLength = [.. BitConverter.GetBytes(length), .. packet2];
                var newChunk = CreateChunkWithNetInfo(withLength, remainingChunks: 0, packetId: 1, setLength: true);

                // Act
                _reassembler.ProcessReceivedChunk(newChunk);

                // Assert - Should get the new packet, not corrupted by old chunks
                Assert.Single(_reassembledPackets);
                Assert.Equal(packet2, _reassembledPackets[0]);
            }
        }

        #endregion

        #region Error Handling Tests

        public class ErrorHandling : NetworkLayerReassemblerTests
        {
            [Fact]
            public void NullChunk_IsIgnored()
            {
                _reassembler.ProcessReceivedChunk(null!);

                Assert.Empty(_reassembledPackets);
                Assert.Contains(_diagnosticMessages, m => m.Contains("lunghezza insufficiente"));
            }

            [Fact]
            public void EmptyChunk_IsIgnored()
            {
                _reassembler.ProcessReceivedChunk([]);

                Assert.Empty(_reassembledPackets);
            }

            [Fact]
            public void TooShortChunk_IsIgnored()
            {
                _reassembler.ProcessReceivedChunk([0x01]); // Only 1 byte, needs 2 for NetInfo

                Assert.Empty(_reassembledPackets);
            }

            [Fact]
            public void InvalidNetInfo_IsIgnored()
            {
                // Create NetInfo with invalid PacketId (0 or 8+)
                byte[] invalidChunk = [0x01, 0x00, 0x01, 0x02, 0x03]; // PacketId = 0

                _reassembler.ProcessReceivedChunk(invalidChunk);

                Assert.Empty(_reassembledPackets);
                Assert.Contains(_diagnosticMessages, m => m.Contains("Errore parsing NetInfo"));
            }

            [Fact]
            public void TooShortReassembledPacket_IsDiscarded()
            {
                // Create chunk with data smaller than MinTransportPacketLength
                byte[] tooShort = new byte[ProtocolConfig.MinTransportPacketLength - 1];
                var chunk = CreateChunkWithNetInfo(tooShort, remainingChunks: 0, packetId: 1);

                _reassembler.ProcessReceivedChunk(chunk);

                Assert.Empty(_reassembledPackets);
                Assert.Contains(_diagnosticMessages, m => m.Contains("scartato"));
            }
        }

        #endregion

        #region Multiple Concurrent Streams Tests

        public class ConcurrentStreams : NetworkLayerReassemblerTests
        {
            [Fact]
            public void DifferentPacketIds_ReassembleIndependently()
            {
                // Arrange - Two interleaved streams with different packet IDs
                var packet1 = CreateMinimalTransportPacket(0x11);
                var packet2 = CreateMinimalTransportPacket(0x22);

                var chunks1 = SplitIntoChunks(packet1, chunkSize: 6, packetId: 1);
                var chunks2 = SplitIntoChunks(packet2, chunkSize: 6, packetId: 2);

                // Act - Interleave chunks
                for (int i = 0; i < Math.Max(chunks1.Count, chunks2.Count); i++)
                {
                    if (i < chunks1.Count) _reassembler.ProcessReceivedChunk(chunks1[i]);
                    if (i < chunks2.Count) _reassembler.ProcessReceivedChunk(chunks2[i]);
                }

                // Assert
                Assert.Equal(2, _reassembledPackets.Count);
                Assert.Contains(_reassembledPackets, p => p.SequenceEqual(packet1));
                Assert.Contains(_reassembledPackets, p => p.SequenceEqual(packet2));
            }

            [Fact]
            public void ThreeConcurrentStreams_AllReassemble()
            {
                // Arrange - Use 3 different packet IDs simultaneously
                var packets = new List<byte[]>();
                var allChunks = new List<List<byte[]>>();

                for (int packetId = 1; packetId <= 3; packetId++)
                {
                    var packet = CreateMinimalTransportPacket((byte)(0x10 + packetId));
                    packets.Add(packet);
                    var chunks = SplitIntoChunks(packet, chunkSize: 6, packetId: packetId);
                    allChunks.Add(chunks);
                }

                // Interleave by sending one chunk from each stream at a time
                int maxChunks = allChunks.Max(c => c.Count);
                for (int i = 0; i < maxChunks; i++)
                {
                    foreach (var chunks in allChunks)
                    {
                        if (i < chunks.Count)
                        {
                            _reassembler.ProcessReceivedChunk(chunks[i]);
                        }
                    }
                }

                // Assert
                Assert.Equal(3, _reassembledPackets.Count);
                foreach (var expectedPacket in packets)
                {
                    Assert.Contains(_reassembledPackets, p => p.SequenceEqual(expectedPacket));
                }
            }
        }

        #endregion

        #region State Management Tests

        public class StateManagement : NetworkLayerReassemblerTests
        {
            [Fact]
            public void ClearReassemblyState_RemovesPendingChunks()
            {
                // Arrange - Start a multi-chunk sequence
                var packet = CreateLargerTransportPacket(20);
                var chunks = SplitIntoChunks(packet, chunkSize: 8, packetId: 1);
                _reassembler.ProcessReceivedChunk(chunks[0]); // Only first chunk

                // Act
                _reassembler.ClearReassemblyState();

                // Now start a new sequence with same packet ID (but remaining=0 means single chunk)
                // The cleared state means if we send remaining chunks, they won't combine properly
                // Let's verify by sending a NEW complete packet instead
                var newPacket = CreateMinimalTransportPacket(0x99);
                var newChunk = CreateChunkWithNetInfo(newPacket, remainingChunks: 0, packetId: 1);
                _reassembler.ProcessReceivedChunk(newChunk);

                // Assert - Should get only the new packet, not a corrupted mix
                Assert.Single(_reassembledPackets);
                Assert.Equal(newPacket, _reassembledPackets[0]);
            }

            [Fact]
            public void AfterDispose_ThrowsObjectDisposedException()
            {
                _reassembler.Dispose();

                Assert.Throws<ObjectDisposedException>(() =>
                    _reassembler.ProcessReceivedChunk(CreateChunkWithNetInfo(
                        CreateMinimalTransportPacket(), 0, 1)));
            }

            [Fact]
            public void DoubleDispose_DoesNotThrow()
            {
                _reassembler.Dispose();
                _reassembler.Dispose(); // Should not throw
            }

            [Fact]
            public void PacketIdReuse_StartsNewSequence()
            {
                // Arrange - Complete first packet
                var packet1 = CreateMinimalTransportPacket(0x11);
                var chunk1 = CreateChunkWithNetInfo(packet1, remainingChunks: 0, packetId: 1);
                _reassembler.ProcessReceivedChunk(chunk1);

                // Complete second packet with same ID
                var packet2 = CreateMinimalTransportPacket(0x22);
                var chunk2 = CreateChunkWithNetInfo(packet2, remainingChunks: 0, packetId: 1);
                _reassembler.ProcessReceivedChunk(chunk2);

                // Assert
                Assert.Equal(2, _reassembledPackets.Count);
                Assert.Equal(packet1, _reassembledPackets[0]);
                Assert.Equal(packet2, _reassembledPackets[1]);
            }
        }

        #endregion

        #region Diagnostic Events Tests

        public class DiagnosticEvents : NetworkLayerReassemblerTests
        {
            [Fact]
            public void ProcessChunk_RaisesDiagnosticMessages()
            {
                var chunk = CreateChunkWithNetInfo(CreateMinimalTransportPacket(), 0, 1);

                _reassembler.ProcessReceivedChunk(chunk);

                Assert.NotEmpty(_diagnosticMessages);
                Assert.Contains(_diagnosticMessages, m => m.Contains("RX chunk"));
                Assert.Contains(_diagnosticMessages, m => m.Contains("NetInfo"));
            }

            [Fact]
            public void CompletePacket_LogsCompletion()
            {
                var chunk = CreateChunkWithNetInfo(CreateMinimalTransportPacket(), 0, 1);

                _reassembler.ProcessReceivedChunk(chunk);

                Assert.Contains(_diagnosticMessages, m => m.Contains("completo"));
            }
        }

        #endregion

        #region Helper Methods

        private static byte[] CreateMinimalTransportPacket(byte marker = 0x42)
        {
            // Create a minimal valid transport packet
            // Header (7) + App header (2) + CRC (2) = 11 bytes minimum
            var appPacket = ApplicationLayer.Create(marker, 0x00, []).ApplicationPacket;
            var transportLayer = TransportLayer.Create(CryptType.None, 0, appPacket);
            return transportLayer.TransportPacket;
        }

        private static byte[] CreateLargerTransportPacket(int payloadSize)
        {
            var payload = ProtocolTestBuilders.CreateSequentialPayload(payloadSize);
            var appPacket = ApplicationLayer.Create(0x01, 0x02, payload).ApplicationPacket;
            var transportLayer = TransportLayer.Create(CryptType.None, 0x12345678, appPacket);
            return transportLayer.TransportPacket;
        }

        private static byte[] CreateChunkWithNetInfo(
            byte[] data,
            int remainingChunks,
            int packetId,
            bool setLength = false)
        {
            var netInfo = new NetInfo(remainingChunks, setLength, packetId, ProtocolVersion.V1);
            return [.. netInfo.ToBytes(), .. data];
        }

        private static List<byte[]> SplitIntoChunks(
            byte[] data,
            int chunkSize,
            int packetId,
            bool setLengthOnFirst = false)
        {
            var chunks = new List<byte[]>();
            int numChunks = (data.Length + chunkSize - 1) / chunkSize;

            for (int i = 0; i < numChunks; i++)
            {
                int offset = i * chunkSize;
                int length = Math.Min(chunkSize, data.Length - offset);
                byte[] chunkData = data.AsSpan(offset, length).ToArray();

                int remaining = numChunks - i - 1;
                bool setLength = setLengthOnFirst && i == 0;

                chunks.Add(CreateChunkWithNetInfo(chunkData, remaining, packetId, setLength));
            }

            return chunks;
        }

        #endregion
    }
}
