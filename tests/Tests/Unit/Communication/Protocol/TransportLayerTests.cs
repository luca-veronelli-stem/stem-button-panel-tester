using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Tests.Helpers;

namespace Tests.Unit.Communication.Protocol
{
    /// <summary>
    /// Unit tests for the TransportLayer class.
    /// Tests packet creation, parsing, CRC validation, and round-trip integrity.
    /// </summary>
    public class TransportLayerTests
    {
        private const int HeaderSize = 7;  // 1 crypt + 4 sender + 2 lPack
        private const int CrcSize = 2;

        #region Create Tests

        public class CreateMethod
        {
            [Fact]
            public void WithValidInput_BuildsCorrectPacket()
            {
                byte[] appPacket = [0x01, 0x02, 0x03];

                var layer = TransportLayer.Create(CryptType.None, 0x12345678, appPacket);

                Assert.Equal(HeaderSize + appPacket.Length + CrcSize, layer.TransportPacket.Length);
                Assert.Equal(appPacket, layer.ApplicationPacket);
                Assert.True(layer.IsValid);
            }

            [Fact]
            public void WithNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    TransportLayer.Create(CryptType.None, 0, null!));
            }

            [Fact]
            public void HeaderContainsCorrectFields()
            {
                uint senderId = 0x12345678;
                byte[] appPacket = [0x01, 0x02, 0x03];

                var layer = TransportLayer.Create(CryptType.None, senderId, appPacket);
                byte[] header = layer.TransportHeader;

                Assert.Equal(HeaderSize, header.Length);
                Assert.Equal((byte)CryptType.None, header[0]);
                ProtocolAssertions.AssertBigEndianUInt32(header, 1, senderId, "SenderId");
                ProtocolAssertions.AssertBigEndianUInt16(header, 5, (ushort)appPacket.Length, "LPack");
            }

            [Fact]
            public void CrcIsComputedAndAppended()
            {
                byte[] appPacket = [0x01, 0x02, 0x03];

                var layer = TransportLayer.Create(CryptType.None, 0, appPacket);

                byte[] dataForCrc = layer.TransportHeader.Concat(appPacket).ToArray();
                ushort expectedCrc = ProtocolHelpers.CalculateCrc(dataForCrc);
                byte[] crcBytes = layer.TransportPacket[^CrcSize..];
                ushort actualCrc = (ushort)((crcBytes[0] << 8) | crcBytes[1]);

                Assert.Equal(expectedCrc, actualCrc);
            }

            [Fact]
            public void WithEmptyAppPacket_Succeeds()
            {
                var layer = TransportLayer.Create(CryptType.None, 0, []);

                Assert.Equal(HeaderSize + CrcSize, layer.TransportPacket.Length);
                Assert.Empty(layer.ApplicationPacket);
                Assert.True(layer.IsValid);
            }

            [Theory]
            [InlineData(0u)]
            [InlineData(1u)]
            [InlineData(0x7FFFFFFFu)]
            [InlineData(0xFFFFFFFFu)]
            public void AllSenderIdRanges_EncodedCorrectly(uint senderId)
            {
                var layer = TransportLayer.Create(CryptType.None, senderId, [0x01]);

                ProtocolAssertions.AssertBigEndianUInt32(layer.TransportHeader, 1, senderId, "SenderId");
            }
        }

        #endregion

        #region Parse Tests

        public class ParseMethod
        {
            [Fact]
            public void WithValidPacket_ExtractsFields()
            {
                byte[] appPacket = [0x01, 0x02, 0x03];
                var created = TransportLayer.Create(CryptType.None, 0x12345678, appPacket);

                var parsed = TransportLayer.Parse(created.TransportPacket);

                Assert.Equal(appPacket, parsed.ApplicationPacket);
                Assert.Equal(0x12345678u, parsed.SenderId);
                Assert.True(parsed.IsValid);
            }

            [Fact]
            public void WithNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() => TransportLayer.Parse(null!));
            }

            [Fact]
            public void WithTooShortPacket_ThrowsProtocolException()
            {
                Assert.Throws<ProtocolException>(() =>
                    TransportLayer.Parse(new byte[HeaderSize - 1]));
            }

            [Fact]
            public void WithLPackMismatch_ThrowsProtocolException()
            {
                // Create header claiming 255 bytes but only provide 3 + CRC
                byte[] header = new byte[HeaderSize];
                header[5] = 0x00;  // LPack high
                header[6] = 0xFF;  // LPack low = 255

                byte[] invalidPacket = [.. header, 0x01, 0x02, 0x03, 0x00, 0x00];

                Assert.Throws<ProtocolException>(() => TransportLayer.Parse(invalidPacket));
            }
        }

        #endregion

        #region CRC Validation Tests

        public class CrcValidation
        {
            [Fact]
            public void ValidPacket_IsValidTrue()
            {
                var layer = TransportLayer.Create(CryptType.None, 0, [0x01, 0x02, 0x03]);

                var parsed = TransportLayer.Parse(layer.TransportPacket);

                Assert.True(parsed.IsValid);
                Assert.Null(parsed.ValidationError);
            }

            [Fact]
            public void CorruptedCrc_IsValidFalse()
            {
                var layer = TransportLayer.Create(CryptType.None, 0, [0x01, 0x02, 0x03]);
                byte[] corrupted = layer.TransportPacket.WithCorruptedCrc();

                var parsed = TransportLayer.Parse(corrupted);

                Assert.False(parsed.IsValid);
                Assert.NotNull(parsed.ValidationError);
            }

            [Fact]
            public void CorruptedData_IsValidFalse()
            {
                var layer = TransportLayer.Create(CryptType.None, 0, [0x01, 0x02, 0x03]);
                byte[] corrupted = layer.TransportPacket.WithBitFlip(HeaderSize);

                var parsed = TransportLayer.Parse(corrupted);

                Assert.False(parsed.IsValid);
            }

            [Theory]
            [InlineData(0)]  // First CRC byte
            [InlineData(1)]  // Second CRC byte
            public void AnyCorruptedCrcByte_IsValidFalse(int crcByteIndex)
            {
                var layer = TransportLayer.Create(CryptType.None, 0, [0x01, 0x02, 0x03]);
                byte[] packet = layer.TransportPacket.ToArray();
                int crcOffset = HeaderSize + 3;  // After header + app packet
                packet[crcOffset + crcByteIndex] ^= 0x01;

                var parsed = TransportLayer.Parse(packet);

                Assert.False(parsed.IsValid);
            }

            [Fact]
            public void SingleBitFlip_Detected()
            {
                var layer = TransportLayer.Create(CryptType.None, 0, [0x01, 0x02, 0x03, 0x04, 0x05]);
                byte[] corrupted = layer.TransportPacket.WithBitFlip(HeaderSize + 2, 0x01);

                var parsed = TransportLayer.Parse(corrupted);

                Assert.False(parsed.IsValid);
            }
        }

        #endregion

        #region Round-Trip Tests

        public class RoundTripTests
        {
            [Fact]
            public void CreateThenParse_PreservesData()
            {
                byte[] appPacket = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC];
                uint senderId = 0xDEADBEEF;

                var created = TransportLayer.Create(CryptType.None, senderId, appPacket);
                var parsed = TransportLayer.Parse(created.TransportPacket);

                Assert.Equal(appPacket, parsed.ApplicationPacket);
                Assert.Equal(senderId, parsed.SenderId);
                Assert.True(parsed.IsValid);
            }

            [Fact]
            public void EmptyAppPacket_PreservesStructure()
            {
                var created = TransportLayer.Create(CryptType.None, 0, []);
                var parsed = TransportLayer.Parse(created.TransportPacket);

                Assert.Empty(parsed.ApplicationPacket);
                Assert.True(parsed.IsValid);
            }

            [Fact]
            public void LargeAppPacket_PreservesAllBytes()
            {
                byte[] appPacket = ProtocolTestBuilders.CreateRandomPayload(1000);

                var created = TransportLayer.Create(CryptType.None, 0x12345678, appPacket);
                var parsed = TransportLayer.Parse(created.TransportPacket);

                Assert.Equal(appPacket, parsed.ApplicationPacket);
                Assert.True(parsed.IsValid);
            }

            [Fact]
            public void AllCryptTypes_Supported()
            {
                foreach (CryptType cryptType in Enum.GetValues<CryptType>())
                {
                    byte[] appPacket = [0x01, 0x02, 0x03];

                    var created = TransportLayer.Create(cryptType, 0, appPacket);
                    var parsed = TransportLayer.Parse(created.TransportPacket);

                    Assert.Equal(cryptType, parsed.CryptFlag);
                    Assert.Equal(appPacket, parsed.ApplicationPacket);
                    Assert.True(parsed.IsValid);
                }
            }

            [Fact]
            public void AllByteValues_PreservedIntegrity()
            {
                byte[] appPacket = ProtocolTestBuilders.CreateAllByteValuesPayload();

                var created = TransportLayer.Create(CryptType.None, 0xFFFFFFFF, appPacket);
                var parsed = TransportLayer.Parse(created.TransportPacket);

                Assert.Equal(appPacket, parsed.ApplicationPacket);
                Assert.True(parsed.IsValid);
            }
        }

        #endregion

        #region Edge Cases

        public class EdgeCases
        {
            [Fact]
            public void MaxLengthAppPacket_HandlesCorrectly()
            {
                byte[] appPacket = new byte[ushort.MaxValue];

                var layer = TransportLayer.Create(CryptType.None, 0, appPacket);

                Assert.Equal(ushort.MaxValue, layer.PacketLength);
            }

            [Fact]
            public void TransportPacket_CorrectTotalLength()
            {
                byte[] appPacket = [0x01, 0x02, 0x03];

                var layer = TransportLayer.Create(CryptType.None, 0, appPacket);

                Assert.Equal(HeaderSize + appPacket.Length + CrcSize, layer.TransportPacket.Length);
            }
        }

        #endregion
    }
}
