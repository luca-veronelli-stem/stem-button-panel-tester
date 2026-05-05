using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Core.Models.Communication;

namespace Tests.Helpers
{
    /// <summary>
    /// Factory methods for building protocol layer objects in tests.
    /// Provides a fluent API for creating test fixtures with sensible defaults.
    /// </summary>
    public static class ProtocolTestBuilders
    {
        #region Application Layer

        /// <summary>
        /// Creates an ApplicationLayer with default values for testing.
        /// </summary>
        public static ApplicationLayer CreateApplicationLayer(
            byte cmdInit = 0x01,
            byte cmdOpt = 0x02,
            byte[]? payload = null)
        {
            return ApplicationLayer.Create(cmdInit, cmdOpt, payload ?? [0x03, 0x04, 0x05]);
        }

        /// <summary>
        /// Creates an ApplicationLayer from a 16-bit command.
        /// </summary>
        public static ApplicationLayer CreateApplicationLayer(
            ushort command,
            byte[]? payload = null)
        {
            return ApplicationLayer.Create(command, payload ?? []);
        }

        /// <summary>
        /// Creates a raw application packet (header + payload) for parsing tests.
        /// </summary>
        public static byte[] CreateRawApplicationPacket(
            byte cmdInit = 0x01,
            byte cmdOpt = 0x02,
            params byte[] payload)
        {
            return [cmdInit, cmdOpt, .. payload];
        }

        #endregion

        #region Transport Layer

        /// <summary>
        /// Creates a TransportLayer with default values for testing.
        /// </summary>
        public static TransportLayer CreateTransportLayer(
            CryptType cryptFlag = CryptType.None,
            uint senderId = 0x12345678,
            byte[]? applicationPacket = null)
        {
            applicationPacket ??= CreateApplicationLayer().ApplicationPacket;
            return TransportLayer.Create(cryptFlag, senderId, applicationPacket);
        }

        /// <summary>
        /// Creates a TransportLayer with corrupted CRC for error testing.
        /// </summary>
        public static byte[] CreateCorruptedTransportPacket(
            CryptType cryptFlag = CryptType.None,
            uint senderId = 0,
            byte[]? applicationPacket = null,
            bool corruptCrc = true,
            bool corruptData = false)
        {
            applicationPacket ??= [0x01, 0x02, 0x03];
            var layer = TransportLayer.Create(cryptFlag, senderId, applicationPacket);
            var packet = layer.TransportPacket.ToArray();

            if (corruptCrc)
            {
                packet[^1] ^= 0xFF;
            }

            if (corruptData && packet.Length > ProtocolConfig.TransportHeaderLength)
            {
                packet[ProtocolConfig.TransportHeaderLength] ^= 0xFF;
            }

            return packet;
        }

        #endregion

        #region Network Layer

        /// <summary>
        /// Creates a NetworkLayer with default values for testing.
        /// </summary>
        public static NetworkLayer CreateNetworkLayer(
            uint recipientId = 0x12345678,
            byte[]? transportPacket = null,
            int chunkSize = 8)
        {
            transportPacket ??= CreateTransportLayer().TransportPacket;
            return NetworkLayer.Create(recipientId, transportPacket, chunkSize);
        }

        /// <summary>
        /// Creates network packet chunks for multi-chunk scenarios.
        /// </summary>
        public static IReadOnlyList<NetworkPacketChunk> CreateMultiChunkPackets(
            int payloadSize = 50,
            int chunkSize = 6,
            uint recipientId = 0x12345678,
            uint senderId = 0x87654321,
            ushort command = 0x0102)
        {
            byte[] payload = CreateSequentialPayload(payloadSize);
            var appLayer = ApplicationLayer.Create(command, payload);
            var transportLayer = TransportLayer.Create(CryptType.None, senderId, appLayer.ApplicationPacket);
            var networkLayer = NetworkLayer.Create(recipientId, transportLayer.TransportPacket, chunkSize);
            return networkLayer.NetworkPackets;
        }

        #endregion

        #region NetInfo

        /// <summary>
        /// Creates a NetInfo structure with default values.
        /// </summary>
        public static NetInfo CreateNetInfo(
            int remainingChunks = 0,
            bool setLength = false,
            int packetId = 1,
            ProtocolVersion version = ProtocolVersion.V1)
        {
            return new NetInfo(remainingChunks, setLength, packetId, version);
        }

        /// <summary>
        /// Creates raw NetInfo bytes with an invalid packet ID.
        /// </summary>
        public static byte[] CreateInvalidNetInfoBytes(int invalidPacketId = 8)
        {
            // NetInfo format: (remainingChunks << 6) | (setLength << 5) | (packetId << 2) | version
            ushort netInfoValue = (ushort)((0 << 6) | (0 << 5) | (invalidPacketId << 2) | 0);
            return [(byte)netInfoValue, (byte)(netInfoValue >> 8)];
        }

        #endregion

        #region Payload Generators

        /// <summary>
        /// Creates a sequential byte payload (0, 1, 2, ...).
        /// </summary>
        public static byte[] CreateSequentialPayload(int size)
        {
            return Enumerable.Range(0, size).Select(i => (byte)(i % 256)).ToArray();
        }

        /// <summary>
        /// Creates a payload containing all possible byte values (0-255).
        /// </summary>
        public static byte[] CreateAllByteValuesPayload()
        {
            byte[] payload = new byte[256];
            for (int i = 0; i < 256; i++)
                payload[i] = (byte)i;
            return payload;
        }

        /// <summary>
        /// Creates a random payload of the specified size.
        /// </summary>
        public static byte[] CreateRandomPayload(int size)
        {
            byte[] payload = new byte[size];
            Random.Shared.NextBytes(payload);
            return payload;
        }

        #endregion
    }
}
