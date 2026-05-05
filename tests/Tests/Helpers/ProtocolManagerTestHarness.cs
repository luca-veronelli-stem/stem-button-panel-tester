using Communication.Protocol;
using Core.Models;
using Core.Models.Communication;

namespace Tests.Helpers
{
    /// <summary>
    /// Helper class for testing StemProtocolManager with event capture.
    /// </summary>
    public sealed class ProtocolManagerTestHarness : IDisposable
    {
        private readonly StemProtocolManager _manager;
        private readonly List<AppLayerDecoderEventArgs> _decodedEvents = [];
        private readonly List<ProtocolErrorEventArgs> _errorEvents = [];
        private readonly object _lock = new();

        public ProtocolManagerTestHarness()
        {
            _manager = new StemProtocolManager();
            _manager.CommandDecoded += OnCommandDecoded;
            _manager.ErrorOccurred += OnErrorOccurred;
        }

        /// <summary>
        /// The underlying protocol manager.
        /// </summary>
        public StemProtocolManager Manager => _manager;

        /// <summary>
        /// All decoded command events captured during the test.
        /// </summary>
        public IReadOnlyList<AppLayerDecoderEventArgs> DecodedEvents
        {
            get
            {
                lock (_lock)
                {
                    return _decodedEvents.ToList();
                }
            }
        }

        /// <summary>
        /// All error events captured during the test.
        /// </summary>
        public IReadOnlyList<ProtocolErrorEventArgs> ErrorEvents
        {
            get
            {
                lock (_lock)
                {
                    return _errorEvents.ToList();
                }
            }
        }

        /// <summary>
        /// The last decoded event, or null if none.
        /// </summary>
        public AppLayerDecoderEventArgs? LastDecodedEvent
        {
            get
            {
                lock (_lock)
                {
                    return _decodedEvents.LastOrDefault();
                }
            }
        }

        /// <summary>
        /// The last error event, or null if none.
        /// </summary>
        public ProtocolErrorEventArgs? LastErrorEvent
        {
            get
            {
                lock (_lock)
                {
                    return _errorEvents.LastOrDefault();
                }
            }
        }

        /// <summary>
        /// Number of successful decode events.
        /// </summary>
        public int DecodeCount
        {
            get
            {
                lock (_lock)
                {
                    return _decodedEvents.Count;
                }
            }
        }

        /// <summary>
        /// Number of error events.
        /// </summary>
        public int ErrorCount
        {
            get
            {
                lock (_lock)
                {
                    return _errorEvents.Count;
                }
            }
        }

        /// <summary>
        /// Clears all captured events.
        /// </summary>
        public void ClearEvents()
        {
            lock (_lock)
            {
                _decodedEvents.Clear();
                _errorEvents.Clear();
            }
        }

        /// <summary>
        /// Builds packets using the manager.
        /// </summary>
        public IReadOnlyList<NetworkPacketChunk> BuildPackets(
            ushort command,
            byte[] payload,
            uint senderId = 123,
            uint recipientId = 456,
            int chunkSize = 100)
        {
            return _manager.BuildPackets(command, payload, senderId, recipientId, chunkSize);
        }

        /// <summary>
        /// Processes a packet and returns the decoded payload (if complete).
        /// The StemProtocolManager.ProcessReceivedPacket expects transport packets directly.
        /// </summary>
        public byte[] ProcessPacket(NetworkPacketChunk packet)
        {
            // ProcessReceivedPacket expects transport packet data, not NetInfo + Chunk
            // For single-chunk packets, the chunk IS the complete transport packet
            return _manager.ProcessReceivedPacket(packet.Chunk);
        }

        /// <summary>
        /// Processes all packets in sequence and returns the final decoded payload.
        /// </summary>
        public byte[] ProcessAllPackets(IEnumerable<NetworkPacketChunk> packets)
        {
            byte[] result = [];
            foreach (NetworkPacketChunk packet in packets)
            {
                result = ProcessPacket(packet);
            }
            return result;
        }

        /// <summary>
        /// Performs a full round-trip: build packets, process them, and return the decoded payload.
        /// </summary>
        public byte[] RoundTrip(
            ushort command,
            byte[] payload,
            uint senderId = 123,
            uint recipientId = 456,
            int chunkSize = 100)
        {
            IReadOnlyList<NetworkPacketChunk> packets = BuildPackets(command, payload, senderId, recipientId, chunkSize);
            return ProcessAllPackets(packets);
        }

        /// <summary>
        /// Asserts that no errors occurred (for test convenience).
        /// </summary>
        public void AssertNoErrors()
        {
            if (ErrorCount > 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected no errors, but got {ErrorCount}: {LastErrorEvent?.Message}");
            }
        }

        /// <summary>
        /// Asserts that the queues are empty (no pending packets).
        /// Since the current implementation doesn't have internal queues, this is a no-op.
        /// </summary>
        public void AssertQueuesEmpty()
        {
            // The StemProtocolManager now delegates to layer classes and doesn't maintain
            // internal packet queues. This method is kept for API compatibility.
        }

        private void OnCommandDecoded(object? sender, AppLayerDecoderEventArgs e)
        {
            lock (_lock)
            {
                _decodedEvents.Add(e);
            }
        }

        private void OnErrorOccurred(object? sender, ProtocolErrorEventArgs e)
        {
            lock (_lock)
            {
                _errorEvents.Add(e);
            }
        }

        public void Dispose()
        {
            _manager.CommandDecoded -= OnCommandDecoded;
            _manager.ErrorOccurred -= OnErrorOccurred;
        }
    }
}
