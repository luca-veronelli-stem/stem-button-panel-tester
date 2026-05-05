using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Tests.Helpers;

namespace Tests.Unit.Communication
{
    /// <summary>
    /// Unit tests for StemProtocolManager.
    /// Tests packet building, processing, event handling, and error scenarios.
    /// </summary>
    public class StemProtocolManagerTests
    {
        #region BuildPackets Tests

        public class BuildPacketsMethod
        {
            [Fact]
            public void SingleChunk_ReturnsCorrectPacket()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload = [1, 2, 3];

                var packets = harness.BuildPackets(0x0102, payload, chunkSize: 100);

                Assert.Single(packets);
                var netInfo = NetInfo.FromBytes(packets[0].NetInfo);
                Assert.Equal(0, netInfo.RemainingChunks);
                Assert.InRange(netInfo.PacketId, 1, 7);
            }

            [Fact]
            public void MultiChunk_SplitsCorrectly()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

                var packets = harness.BuildPackets(0x0102, payload, chunkSize: 3);

                // Transport: 7 header + 2 app header + 10 data + 2 CRC = 21 bytes
                // 21 / 3 = 7 chunks
                Assert.Equal(7, packets.Count);

                // Verify remaining chunks decrease
                for (int i = 0; i < packets.Count; i++)
                {
                    var netInfo = NetInfo.FromBytes(packets[i].NetInfo);
                    Assert.Equal(packets.Count - i - 1, netInfo.RemainingChunks);
                }
            }

            [Fact]
            public void EmptyPayload_CreatesMinimalPacket()
            {
                using var harness = new ProtocolManagerTestHarness();

                var packets = harness.BuildPackets(0x0102, [], chunkSize: 100);

                Assert.Single(packets);
            }

            [Theory]
            [InlineData(0)]
            [InlineData(-1)]
            public void InvalidChunkSize_Throws(int chunkSize)
            {
                using var harness = new ProtocolManagerTestHarness();

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    harness.BuildPackets(0x0102, [1], chunkSize: chunkSize));
            }

            [Fact]
            public void NullPayload_Throws()
            {
                using var harness = new ProtocolManagerTestHarness();

                Assert.Throws<ArgumentNullException>(() =>
                    harness.BuildPackets(0x0102, null!));
            }

            [Fact]
            public void LargePayload_CreatesCorrectChunkCount()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload = ProtocolTestBuilders.CreateSequentialPayload(1024);

                var packets = harness.BuildPackets(0x0102, payload, chunkSize: 10);

                // Transport: 7 + 2 + 1024 + 2 = 1035 bytes => 104 chunks
                Assert.Equal(104, packets.Count);

                // Verify data integrity
                var reconstructed = packets.SelectMany(p => p.Chunk).ToArray();
                var expectedAppLayer = ApplicationLayer.Create(0x0102, payload);
                var expectedTransport = TransportLayer.Create(CryptType.None, 123, expectedAppLayer.ApplicationPacket);
                Assert.Equal(expectedTransport.TransportPacket, reconstructed);
            }
        }

        #endregion

        #region ProcessReceivedPacket Tests

        public class ProcessReceivedPacketMethod
        {
            [Fact]
            public void SingleChunk_DecodesSuccessfully()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload = [1, 2, 3];

                var result = harness.RoundTrip(0x0102, payload, chunkSize: 100);

                Assert.Equal(payload, result);
                Assert.Equal(1, harness.DecodeCount);
                Assert.Equal(0, harness.ErrorCount);
            }

            [Fact]
            public void NullPacket_RaisesError()
            {
                using var harness = new ProtocolManagerTestHarness();

                var result = harness.Manager.ProcessReceivedPacket(null);

                Assert.Empty(result);
                Assert.Equal(1, harness.ErrorCount);
                // Error message is in Italian, check for key parts
                Assert.NotNull(harness.LastErrorEvent);
            }

            [Fact]
            public void TooShortPacket_RaisesError()
            {
                using var harness = new ProtocolManagerTestHarness();

                var result = harness.Manager.ProcessReceivedPacket([0x01]);

                Assert.Empty(result);
                Assert.Equal(1, harness.ErrorCount);
            }

            [Fact]
            public void CorruptedCrc_RaisesError()
            {
                using var harness = new ProtocolManagerTestHarness();
                var packets = harness.BuildPackets(0x0102, [1, 2, 3], chunkSize: 100);
                var transportPacket = packets[0].Chunk.ToArray();
                transportPacket[^1] ^= 0xFF;  // Corrupt CRC

                var result = harness.Manager.ProcessReceivedPacket(transportPacket);

                Assert.Empty(result);
                Assert.Equal(1, harness.ErrorCount);
                Assert.NotNull(harness.LastErrorEvent);
                // CRC error message contains "CRC" in Italian too
                Assert.Contains("CRC", harness.LastErrorEvent!.Message, StringComparison.OrdinalIgnoreCase);
            }

            [Fact]
            public void InvalidTransportHeader_RaisesError()
            {
                using var harness = new ProtocolManagerTestHarness();
                // Too short for transport header (needs 7 bytes minimum)
                byte[] invalidPacket = new byte[6];

                var result = harness.Manager.ProcessReceivedPacket(invalidPacket);

                Assert.Empty(result);
                Assert.Equal(1, harness.ErrorCount);
            }
        }

        #endregion

        #region SetLength Handling

        public class SetLengthHandling
        {
            [Fact]
            public void SetLengthChunk_ProcessesCorrectly()
            {
                using var harness = new ProtocolManagerTestHarness();

                // Build transport packet directly
                var appLayer = ApplicationLayer.Create(1, 2, [1, 2, 3]);
                var transportLayer = TransportLayer.Create(CryptType.None, 123, appLayer.ApplicationPacket);

                // Process the transport packet directly
                var result = harness.Manager.ProcessReceivedPacket(transportLayer.TransportPacket);

                Assert.Equal([1, 2, 3], result);
                Assert.Equal(1, harness.DecodeCount);
            }
        }

        #endregion

        #region Event Tests

        public class EventBehavior
        {
            [Fact]
            public void CommandDecoded_ContainsFullPayload()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload = [0x11, 0x22, 0x33];

                harness.RoundTrip(0x0A0B, payload, chunkSize: 100);

                var evt = harness.LastDecodedEvent!;
                Assert.Equal(0x0A, evt.Payload[0]);  // cmdInit
                Assert.Equal(0x0B, evt.Payload[1]);  // cmdOpt
                Assert.Equal(payload, evt.Payload[2..]);
            }

            [Fact]
            public void MultipleErrors_AllCaptured()
            {
                using var harness = new ProtocolManagerTestHarness();

                harness.Manager.ProcessReceivedPacket(null);
                harness.Manager.ProcessReceivedPacket([0x01]);
                harness.Manager.ProcessReceivedPacket([0x01, 0x02, 0x03]);

                Assert.Equal(3, harness.ErrorCount);
            }
        }

        #endregion

        #region Concurrent Processing

        public class ConcurrentProcessing
        {
            [Fact]
            public void SequentialStreams_ProcessIndependently()
            {
                using var harness = new ProtocolManagerTestHarness();
                byte[] payload1 = [1, 2, 3, 4, 5];
                byte[] payload2 = [6, 7, 8, 9, 10];

                // Process first stream
                var result1 = harness.RoundTrip(0x0101, payload1, chunkSize: 100);

                // Process second stream
                var result2 = harness.RoundTrip(0x0202, payload2, chunkSize: 100);

                Assert.Equal(payload1, result1);
                Assert.Equal(payload2, result2);
                Assert.Equal(2, harness.DecodeCount);
            }
        }

        #endregion

        #region Initialization

        public class Initialization
        {
            [Fact]
            public void Constructor_InitializesEmpty()
            {
                using var harness = new ProtocolManagerTestHarness();

                Assert.Equal(0, harness.DecodeCount);
                Assert.Equal(0, harness.ErrorCount);
            }
        }

        #endregion
    }
}
