using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Tests.Helpers;

namespace Tests.Integration.Communication
{
    /// <summary>
    /// Integration tests for StemProtocolManager.
    /// Tests the complete protocol stack with packet processing.
    /// </summary>
    [Trait("Category", TestCategories.Integration)]
    public class StemProtocolManagerIntegrationTests
    {
        #region Round-Trip Tests

        public class RoundTripTests
        {
            [Theory]
            [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
            [InlineData(new byte[] { })]
            [InlineData(new byte[] { 0xFF, 0x00, 0xAA, 0x55 })]
            public void PayloadIsPreserved(byte[] payload)
            {
                using var harness = new ProtocolManagerTestHarness();

                // Build and process single-chunk packet
                var result = harness.RoundTrip(0x0102, payload, chunkSize: 100);

                Assert.Equal(payload, result);
                Assert.Equal(1, harness.DecodeCount);
            }

            [Fact]
            public void LargePayload_BuildsMultipleChunks()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload = ProtocolTestBuilders.CreateSequentialPayload(100);

                // Building creates multiple chunks
                var packets = harness.BuildPackets(0xFFFF, payload, chunkSize: 8);

                Assert.True(packets.Count > 10);

                // Verify the chunks can be reassembled (combined chunks = transport packet)
                var combined = packets.SelectMany(p => p.Chunk).ToArray();
                var expectedApp = ApplicationLayer.Create(0xFFFF, payload);
                var expectedTransport = TransportLayer.Create(CryptType.None, 123, expectedApp.ApplicationPacket);
                Assert.Equal(expectedTransport.TransportPacket, combined);
            }

            [Theory]
            [InlineData(0x0000)]
            [InlineData(0x00FF)]
            [InlineData(0xFF00)]
            [InlineData(0xFFFF)]
            [InlineData(0x1234)]
            public void CommandBytesArePreserved(ushort command)
            {
                using var harness = new ProtocolManagerTestHarness();

                harness.RoundTrip(command, [0x42], chunkSize: 100);

                var evt = harness.LastDecodedEvent!;
                Assert.Equal((byte)(command >> 8), evt.Payload[0]);
                Assert.Equal((byte)(command & 0xFF), evt.Payload[1]);
            }
        }

        #endregion

        #region Protocol Layer Integration

        public class ProtocolLayerIntegration
        {
            [Fact]
            public void AllLayers_IntegratedCorrectly()
            {
                using var harness = new ProtocolManagerTestHarness();
                ushort command = 0x0102;
                byte[] payload = [0xAA, 0xBB, 0xCC];
                uint senderId = 0x11223344;
                uint recipientId = 0x55667788;

                var packets = harness.BuildPackets(command, payload, senderId, recipientId, chunkSize: 100);

                Assert.Single(packets);
                var packet = packets[0];

                // Verify network layer
                var netInfo = NetInfo.FromBytes(packet.NetInfo);
                Assert.Equal(0, netInfo.RemainingChunks);
                Assert.Equal(ProtocolVersion.V1, netInfo.Version);
                Assert.Equal(recipientId, packet.Id);

                // Verify transport layer structure in chunk
                var expectedApp = ApplicationLayer.Create(command, payload);
                var expectedTransport = TransportLayer.Create(CryptType.None, senderId, expectedApp.ApplicationPacket);
                Assert.Equal(expectedTransport.TransportPacket, packet.Chunk);
            }

            [Fact]
            public void CrcValidation_WorksEndToEnd()
            {
                using var harness = new ProtocolManagerTestHarness();
                var packets = harness.BuildPackets(0x0102, [1, 2, 3], chunkSize: 100);

                // Valid packet decodes
                var validResult = harness.ProcessPacket(packets[0]);
                Assert.Equal([1, 2, 3], validResult);

                // Corrupted packet fails
                harness.ClearEvents();
                var transportPacket = packets[0].Chunk.ToArray();
                transportPacket[^1] ^= 0xFF;
                var corruptedResult = harness.Manager.ProcessReceivedPacket(transportPacket);

                Assert.Empty(corruptedResult);
                Assert.Contains("CRC", harness.LastErrorEvent!.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion

        #region Error Handling

        public class ErrorHandling
        {
            [Fact]
            public void CorruptedTransportHeader_RaisesError()
            {
                using var harness = new ProtocolManagerTestHarness();
                var packets = harness.BuildPackets(0x0102, [1, 2, 3], chunkSize: 100);
                var transportPacket = packets[0].Chunk.ToArray();

                // Corrupt LPack field to claim more data than available
                transportPacket[5] = 0xFF;
                transportPacket[6] = 0xFF;

                var result = harness.Manager.ProcessReceivedPacket(transportPacket);

                Assert.Empty(result);
                Assert.True(harness.ErrorCount > 0);
            }

            [Fact]
            public void TooShortPacket_RaisesError()
            {
                using var harness = new ProtocolManagerTestHarness();

                var result = harness.Manager.ProcessReceivedPacket(new byte[5]);

                Assert.Empty(result);
                Assert.True(harness.ErrorCount > 0);
            }
        }

        #endregion

        #region Event Integration

        public class EventIntegration
        {
            [Fact]
            public void CommandDecoded_ContainsFullApplicationPacket()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload = [0x11, 0x22, 0x33, 0x44];

                harness.RoundTrip(0x0A0B, payload, chunkSize: 100);

                var evt = harness.LastDecodedEvent!;
                Assert.Equal(0x0A, evt.Payload[0]);
                Assert.Equal(0x0B, evt.Payload[1]);
                Assert.Equal(payload, evt.Payload[2..]);
            }

            [Fact]
            public void ErrorEvent_ContainsPacketInfo()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] invalidPacket = [0x01, 0x02, 0x03, 0x04];

                harness.Manager.ProcessReceivedPacket(invalidPacket);

                Assert.NotNull(harness.LastErrorEvent);
                Assert.Equal(invalidPacket, harness.LastErrorEvent!.Packet);
            }
        }

        #endregion

        #region Stress Tests

        public class StressTests
        {
            [Fact]
            public void RapidProcessing_NoErrors()
            {
                using var harness = new ProtocolManagerTestHarness();
                const int iterations = 100;

                for (int i = 0; i < iterations; i++)
                {
                    byte[] payload = [(byte)i, (byte)(i + 1), (byte)(i + 2)];
                    harness.RoundTrip((ushort)(0x0100 + i), payload, chunkSize: 100);
                }

                Assert.Equal(iterations, harness.DecodeCount);
                Assert.Equal(0, harness.ErrorCount);
            }

            [Fact]
            public void ManyPayloadSizes_AllProcessedCorrectly()
            {
                using var harness = new ProtocolManagerTestHarness();

                foreach (var size in new[] { 0, 1, 10, 100, 500, 1000 })
                {
                    byte[] payload = ProtocolTestBuilders.CreateSequentialPayload(size);
                    // Use large chunk size to ensure single chunk for ProcessReceivedPacket
                    var result = harness.RoundTrip(0x0102, payload, chunkSize: 2000);
                    Assert.Equal(payload, result);
                }

                Assert.Equal(6, harness.DecodeCount);
                Assert.Equal(0, harness.ErrorCount);
            }
        }

        #endregion

        #region Sequential Processing

        public class SequentialProcessing
        {
            [Fact]
            public void MultiplePackets_ProcessedSequentially()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload1 = [1, 2, 3, 4, 5];
                byte[] payload2 = [6, 7, 8, 9, 10];

                var result1 = harness.RoundTrip(0x0101, payload1, chunkSize: 100);
                var result2 = harness.RoundTrip(0x0202, payload2, chunkSize: 100);

                Assert.Equal(payload1, result1);
                Assert.Equal(payload2, result2);
                Assert.Equal(2, harness.DecodeCount);
            }

            [Fact]
            public void ManySingleChunkPackets_AllProcessed()
            {
                using var harness = new ProtocolManagerTestHarness();
                const int packetCount = 10;

                for (int i = 0; i < packetCount; i++)
                {
                    byte[] payload = ProtocolTestBuilders.CreateSequentialPayload(5);
                    var result = harness.RoundTrip((ushort)(0x0100 + i), payload, chunkSize: 100);
                    Assert.Equal(payload, result);
                }

                Assert.Equal(packetCount, harness.DecodeCount);
            }
        }

        #endregion
    }
}
