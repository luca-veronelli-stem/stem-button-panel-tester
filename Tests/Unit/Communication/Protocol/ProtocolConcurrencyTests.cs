using Communication.Protocol;
using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using System.Collections.Concurrent;
using Tests.Helpers;

namespace Tests.Unit.Communication.Protocol
{
    /// <summary>
    /// Thread-safety and concurrent processing tests for the protocol stack.
    /// These tests verify that protocol components behave correctly under multi-threaded access.
    /// </summary>
    public class ProtocolConcurrencyTests
    {
        #region NetworkLayer Thread Safety

        public class NetworkLayerConcurrency
        {
            [Fact]
            public async Task Create_ConcurrentCalls_ProducesValidPacketIds()
            {
                // Arrange
                const int taskCount = 100;
                var packetIds = new ConcurrentBag<int>();
                var tasks = new List<Task>();

                // Act - Create many network layers concurrently
                for (int i = 0; i < taskCount; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        var layer = NetworkLayer.Create(0, [0x01, 0x02, 0x03], chunkSize: 10);
                        var netInfo = NetInfo.FromBytes(layer.NetworkPackets[0].NetInfo);
                        packetIds.Add(netInfo.PacketId);
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert - All packet IDs should be valid (1-7)
                Assert.Equal(taskCount, packetIds.Count);
                Assert.All(packetIds, id =>
                    Assert.InRange(id, ProtocolConfig.MinPacketId, ProtocolConfig.MaxPacketId));
            }

            [Fact]
            public async Task Create_ConcurrentCalls_AllPacketsValid()
            {
                // Arrange
                const int taskCount = 50;
                var allPackets = new ConcurrentBag<List<byte[]>>();

                // Act
                var tasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(() =>
                {
                    byte[] payload = ProtocolTestBuilders.CreateSequentialPayload(100 + i);
                    var appLayer = ApplicationLayer.Create(0x01, 0x02, payload);
                    var transportLayer = TransportLayer.Create(CryptType.None, (uint)i, appLayer.ApplicationPacket);
                    var networkLayer = NetworkLayer.Create((uint)i, transportLayer.TransportPacket, chunkSize: 8);

                    var chunks = networkLayer.NetworkPackets.Select(p => p.Chunk).ToList();
                    allPackets.Add(chunks);
                }));

                await Task.WhenAll(tasks);

                // Assert - All network layers created valid chunks
                Assert.Equal(taskCount, allPackets.Count);
                foreach (var chunks in allPackets)
                {
                    Assert.NotEmpty(chunks);
                    Assert.All(chunks, c => Assert.NotEmpty(c));
                }
            }
        }

        #endregion

        #region NetworkLayerReassembler Thread Safety

        public class ReassemblerConcurrency : IDisposable
        {
            private readonly NetworkLayerReassembler _reassembler;
            private readonly ConcurrentBag<byte[]> _reassembledPackets;

            public ReassemblerConcurrency()
            {
                _reassembler = new NetworkLayerReassembler();
                _reassembledPackets = [];
                _reassembler.PacketReassembled += p => _reassembledPackets.Add(p);
            }

            public void Dispose() => _reassembler.Dispose();

            [Fact]
            public async Task ProcessReceivedChunk_ConcurrentSingleChunks_AllReassembled()
            {
                // Arrange
                const int packetCount = 100;
                var expectedPackets = new ConcurrentBag<byte[]>();

                // Act - Send many single-chunk packets concurrently
                var tasks = Enumerable.Range(0, packetCount).Select(i => Task.Run(() =>
                {
                    var packet = CreateTransportPacket((byte)(i % 256));
                    expectedPackets.Add(packet);

                    int packetId = (i % 7) + 1; // Cycle through valid IDs
                    var chunk = CreateChunkWithNetInfo(packet, remainingChunks: 0, packetId: packetId);
                    _reassembler.ProcessReceivedChunk(chunk);
                }));

                await Task.WhenAll(tasks);

                // Small delay to allow all events to fire
                await Task.Delay(50);

                // Assert - All packets should be reassembled
                Assert.Equal(packetCount, _reassembledPackets.Count);
            }

            [Fact]
            public async Task ProcessReceivedChunk_ConcurrentMultiChunkStreams_AllReassembled()
            {
                // Arrange - 7 concurrent streams (one per valid packet ID)
                const int streamCount = 7;
                var expectedPackets = new ConcurrentDictionary<int, byte[]>();

                // Act - Each stream uses a different packet ID
                var tasks = Enumerable.Range(1, streamCount).Select(packetId => Task.Run(() =>
                {
                    var packet = CreateLargerTransportPacket(50 + packetId * 10);
                    expectedPackets[packetId] = packet;

                    var chunks = SplitIntoChunks(packet, chunkSize: 8, packetId: packetId);
                    foreach (var chunk in chunks)
                    {
                        _reassembler.ProcessReceivedChunk(chunk);
                        Thread.Sleep(1); // Small delay to increase interleaving
                    }
                }));

                await Task.WhenAll(tasks);
                await Task.Delay(50);

                // Assert
                Assert.Equal(streamCount, _reassembledPackets.Count);
                foreach (var expected in expectedPackets.Values)
                {
                    Assert.Contains(_reassembledPackets, p => p.SequenceEqual(expected));
                }
            }

            [Fact]
            public async Task ClearReassemblyState_WhileProcessing_NoExceptions()
            {
                // Arrange
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var exceptions = new ConcurrentBag<Exception>();

                // Act - Concurrent processing and clearing
                var processingTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var packet = CreateTransportPacket(0x42);
                            var chunk = CreateChunkWithNetInfo(packet, 0, 1);
                            _reassembler.ProcessReceivedChunk(chunk);
                            await Task.Delay(1);
                        }
                        catch (ObjectDisposedException) { break; }
                        catch (Exception ex) { exceptions.Add(ex); }
                    }
                });

                var clearingTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            _reassembler.ClearReassemblyState();
                            await Task.Delay(10);
                        }
                        catch (ObjectDisposedException) { break; }
                        catch (Exception ex) { exceptions.Add(ex); }
                    }
                });

                await Task.WhenAll(processingTask, clearingTask);

                // Assert - No unexpected exceptions
                Assert.Empty(exceptions);
            }
        }

        #endregion

        #region StemProtocolManager Thread Safety

        public class ProtocolManagerConcurrency
        {
            [Fact]
            public async Task BuildPackets_ConcurrentCalls_AllSucceed()
            {
                // Arrange
                var manager = new StemProtocolManager();
                const int taskCount = 100;
                var allResults = new ConcurrentBag<int>();

                // Act
                var tasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(() =>
                {
                    byte[] payload = ProtocolTestBuilders.CreateSequentialPayload(10 + i);
                    var packets = manager.BuildPackets((ushort)(0x0100 + i), payload, (uint)i, (uint)i, 8);
                    allResults.Add(packets.Count);
                }));

                await Task.WhenAll(tasks);

                // Assert
                Assert.Equal(taskCount, allResults.Count);
                Assert.All(allResults, count => Assert.True(count > 0));
            }

            [Fact]
            public async Task ProcessReceivedPacket_ConcurrentCalls_AllEventsRaised()
            {
                // Arrange
                var manager = new StemProtocolManager();
                var decodedPayloads = new ConcurrentBag<byte[]>();
                var errorCount = 0;

                manager.CommandDecoded += (_, e) => decodedPayloads.Add(e.Payload);
                manager.ErrorOccurred += (_, _) => Interlocked.Increment(ref errorCount);

                const int taskCount = 100;

                // Act
                var tasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(() =>
                {
                    byte[] payload = [(byte)i, (byte)(i + 1)];
                    var appLayer = ApplicationLayer.Create(0x01, 0x02, payload);
                    var transportLayer = TransportLayer.Create(CryptType.None, (uint)i, appLayer.ApplicationPacket);
                    manager.ProcessReceivedPacket(transportLayer.TransportPacket);
                }));

                await Task.WhenAll(tasks);

                // Assert
                Assert.Equal(taskCount, decodedPayloads.Count);
                Assert.Equal(0, errorCount);
            }

            [Fact]
            public async Task MixedOperations_ConcurrentBuildAndProcess_NoExceptions()
            {
                // Arrange
                var manager = new StemProtocolManager();
                var exceptions = new ConcurrentBag<Exception>();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                // Act - Concurrent building and processing
                var buildTask = Task.Run(async () =>
                {
                    int i = 0;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            byte[] payload = [(byte)(i++ % 256)];
                            manager.BuildPackets(0x0102, payload, 1, 2, 8);
                        }
                        catch (Exception ex) { exceptions.Add(ex); }
                        await Task.Delay(1);
                    }
                });

                var processTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var appLayer = ApplicationLayer.Create(0x01, 0x02, [0x03]);
                            var transportLayer = TransportLayer.Create(CryptType.None, 0, appLayer.ApplicationPacket);
                            manager.ProcessReceivedPacket(transportLayer.TransportPacket);
                        }
                        catch (Exception ex) { exceptions.Add(ex); }
                        await Task.Delay(1);
                    }
                });

                await Task.WhenAll(buildTask, processTask);

                // Assert
                Assert.Empty(exceptions);
            }
        }

        #endregion

        #region ProtocolConfig.GetNextPacketId Thread Safety

        public class PacketIdGeneratorConcurrency
        {
            [Fact]
            public async Task GetNextPacketId_HighContention_AllIdsValid()
            {
                // Arrange
                const int taskCount = 1000;
                var packetIds = new ConcurrentBag<int>();
                int sharedCounter = 0;

                // Act - High contention on packet ID generation
                var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(() =>
                {
                    int id = ProtocolConfig.GetNextPacketId(ref sharedCounter);
                    packetIds.Add(id);
                }));

                await Task.WhenAll(tasks);

                // Assert - All IDs should be valid
                Assert.Equal(taskCount, packetIds.Count);
                Assert.All(packetIds, id =>
                    Assert.InRange(id, ProtocolConfig.MinPacketId, ProtocolConfig.MaxPacketId));
            }

            [Fact]
            public async Task GetNextPacketId_ParallelAccess_NoMissedUpdates()
            {
                // Arrange
                const int iterations = 100;
                const int threadsPerIteration = 10;
                int counter = 0;
                var allIds = new ConcurrentBag<int>();

                // Act
                var tasks = Enumerable.Range(0, iterations).Select(_ =>
                    Task.WhenAll(Enumerable.Range(0, threadsPerIteration).Select(_ => Task.Run(() =>
                    {
                        int id = ProtocolConfig.GetNextPacketId(ref counter);
                        allIds.Add(id);
                    }))));

                await Task.WhenAll(tasks);

                // Assert - Should have generated exactly the expected number of IDs
                Assert.Equal(iterations * threadsPerIteration, allIds.Count);
            }
        }

        #endregion

        #region Protocol Layer Thread Safety

        public class LayerConcurrency
        {
            [Fact]
            public async Task ApplicationLayer_ConcurrentCreate_AllValid()
            {
                const int taskCount = 100;
                var layers = new ConcurrentBag<ApplicationLayer>();

                var tasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(() =>
                {
                    var layer = ApplicationLayer.Create((byte)i, (byte)(i + 1), [(byte)(i + 2)]);
                    layers.Add(layer);
                }));

                await Task.WhenAll(tasks);

                Assert.Equal(taskCount, layers.Count);
                Assert.All(layers, l => Assert.NotNull(l.ApplicationPacket));
            }

            [Fact]
            public async Task TransportLayer_ConcurrentCreate_AllValid()
            {
                const int taskCount = 100;
                var layers = new ConcurrentBag<TransportLayer>();

                var tasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(() =>
                {
                    byte[] appPacket = [0x01, 0x02, (byte)i];
                    var layer = TransportLayer.Create(CryptType.None, (uint)i, appPacket);
                    layers.Add(layer);
                }));

                await Task.WhenAll(tasks);

                Assert.Equal(taskCount, layers.Count);
                Assert.All(layers, l =>
                {
                    Assert.True(l.IsValid);
                    Assert.NotEmpty(l.TransportPacket);
                });
            }

            [Fact]
            public async Task TransportLayer_ConcurrentParse_AllValid()
            {
                // Arrange - Create transport packets
                var transportPackets = Enumerable.Range(0, 100)
                    .Select(i =>
                    {
                        byte[] appPacket = [0x01, 0x02, (byte)i];
                        return TransportLayer.Create(CryptType.None, (uint)i, appPacket).TransportPacket;
                    })
                    .ToList();

                var parsedLayers = new ConcurrentBag<TransportLayer>();

                // Act
                var tasks = transportPackets.Select(packet => Task.Run(() =>
                {
                    var layer = TransportLayer.Parse(packet);
                    parsedLayers.Add(layer);
                }));

                await Task.WhenAll(tasks);

                // Assert
                Assert.Equal(100, parsedLayers.Count);
                Assert.All(parsedLayers, l => Assert.True(l.IsValid));
            }
        }

        #endregion

        #region Helper Methods

        private static byte[] CreateTransportPacket(byte marker)
        {
            var appPacket = ApplicationLayer.Create(marker, 0x00, []).ApplicationPacket;
            return TransportLayer.Create(CryptType.None, 0, appPacket).TransportPacket;
        }

        private static byte[] CreateLargerTransportPacket(int payloadSize)
        {
            var payload = ProtocolTestBuilders.CreateSequentialPayload(payloadSize);
            var appPacket = ApplicationLayer.Create(0x01, 0x02, payload).ApplicationPacket;
            return TransportLayer.Create(CryptType.None, 0x12345678, appPacket).TransportPacket;
        }

        private static byte[] CreateChunkWithNetInfo(byte[] data, int remainingChunks, int packetId)
        {
            var netInfo = new NetInfo(remainingChunks, false, packetId, ProtocolVersion.V1);
            return [.. netInfo.ToBytes(), .. data];
        }

        private static List<byte[]> SplitIntoChunks(byte[] data, int chunkSize, int packetId)
        {
            var chunks = new List<byte[]>();
            int numChunks = (data.Length + chunkSize - 1) / chunkSize;

            for (int i = 0; i < numChunks; i++)
            {
                int offset = i * chunkSize;
                int length = Math.Min(chunkSize, data.Length - offset);
                byte[] chunkData = data.AsSpan(offset, length).ToArray();
                int remaining = numChunks - i - 1;
                chunks.Add(CreateChunkWithNetInfo(chunkData, remaining, packetId));
            }

            return chunks;
        }

        #endregion
    }
}
