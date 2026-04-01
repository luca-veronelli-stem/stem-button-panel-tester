using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Tests.Helpers;

namespace Tests.Unit.Communication.Protocol
{
    /// <summary>
    /// Unit tests for the ApplicationLayer class.
    /// Tests packet creation, parsing, and round-trip integrity.
    /// </summary>
    public class ApplicationLayerTests
    {
        #region Create Tests

        public class CreateMethod
        {
            [Fact]
            public void WithValidInput_BuildsCorrectPacket()
            {
                // Arrange
                byte cmdInit = 0x01;
                byte cmdOpt = 0x02;
                byte[] payload = [0x03, 0x04, 0x05];

                // Act
                var layer = ApplicationLayer.Create(cmdInit, cmdOpt, payload);

                // Assert
                Assert.Equal(cmdInit, layer.CmdInit);
                Assert.Equal(cmdOpt, layer.CmdOpt);
                Assert.Equal(payload, layer.Data);
                Assert.Equal([cmdInit, cmdOpt], layer.ApplicationHeader);
                Assert.Equal([cmdInit, cmdOpt, .. payload], layer.ApplicationPacket);
            }

            [Fact]
            public void WithEmptyPayload_BuildsHeaderOnly()
            {
                var layer = ApplicationLayer.Create(0xAA, 0xBB, []);

                Assert.Empty(layer.Data);
                Assert.Equal(2, layer.ApplicationPacket.Length);
                Assert.Equal([0xAA, 0xBB], layer.ApplicationPacket);
            }

            [Fact]
            public void WithLargePayload_HandlesCorrectly()
            {
                byte[] payload = ProtocolTestBuilders.CreateSequentialPayload(1000);

                var layer = ApplicationLayer.Create(0x01, 0x02, payload);

                Assert.Equal(1002, layer.ApplicationPacket.Length);
                Assert.Equal(payload, layer.ApplicationPacket[2..]);
            }

            [Theory]
            [InlineData(0x00, 0x00)]
            [InlineData(0x00, 0xFF)]
            [InlineData(0xFF, 0x00)]
            [InlineData(0xFF, 0xFF)]
            public void WithBoundaryCommandBytes_PreservesValues(byte cmdInit, byte cmdOpt)
            {
                var layer = ApplicationLayer.Create(cmdInit, cmdOpt, [0x01]);

                Assert.Equal(cmdInit, layer.CmdInit);
                Assert.Equal(cmdOpt, layer.CmdOpt);
            }

            [Fact]
            public void WithUshortCommand_SplitsCorrectly()
            {
                ushort command = 0x0102;

                var layer = ApplicationLayer.Create(command, [0x03]);

                Assert.Equal(0x01, layer.CmdInit);
                Assert.Equal(0x02, layer.CmdOpt);
                Assert.Equal(command, layer.Command);
            }
        }

        #endregion

        #region Parse Tests

        public class ParseMethod
        {
            [Fact]
            public void WithValidPacket_ExtractsFields()
            {
                byte[] packet = [0x01, 0x02, 0x03, 0x04, 0x05];

                var layer = ApplicationLayer.Parse(packet);

                Assert.Equal(0x01, layer.CmdInit);
                Assert.Equal(0x02, layer.CmdOpt);
                Assert.Equal([0x03, 0x04, 0x05], layer.Data);
            }

            [Fact]
            public void WithHeaderOnly_ReturnsEmptyData()
            {
                var layer = ApplicationLayer.Parse([0xAA, 0xBB]);

                Assert.Equal(0xAA, layer.CmdInit);
                Assert.Equal(0xBB, layer.CmdOpt);
                Assert.Empty(layer.Data);
            }

            [Fact]
            public void WithTooShortPacket_ThrowsProtocolException()
            {
                Assert.Throws<ProtocolException>(() => ApplicationLayer.Parse([0x01]));
            }

            [Fact]
            public void WithEmptyPacket_ThrowsProtocolException()
            {
                Assert.Throws<ProtocolException>(() => ApplicationLayer.Parse([]));
            }

            [Fact]
            public void WithNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() => ApplicationLayer.Parse(null!));
            }
        }

        #endregion

        #region Round-Trip Tests

        public class RoundTripTests
        {
            [Theory]
            [InlineData(0x12, 0x34, new byte[] { 0x56, 0x78 })]
            [InlineData(0xFF, 0x00, new byte[] { })]
            [InlineData(0x00, 0xFF, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 })]
            public void CreateThenParse_PreservesAllData(byte cmdInit, byte cmdOpt, byte[] payload)
            {
                var created = ApplicationLayer.Create(cmdInit, cmdOpt, payload);
                var parsed = ApplicationLayer.Parse(created.ApplicationPacket);

                Assert.Equal(cmdInit, parsed.CmdInit);
                Assert.Equal(cmdOpt, parsed.CmdOpt);
                Assert.Equal(payload, parsed.Data);
            }

            [Fact]
            public void WithAllByteValues_PreservesIntegrity()
            {
                byte[] payload = ProtocolTestBuilders.CreateAllByteValuesPayload();

                var created = ApplicationLayer.Create(0x00, 0xFF, payload);
                var parsed = ApplicationLayer.Parse(created.ApplicationPacket);

                Assert.Equal(payload, parsed.Data);
            }

            [Fact]
            public void WithRandomData_PreservesIntegrity()
            {
                byte[] payload = ProtocolTestBuilders.CreateRandomPayload(500);

                var created = ApplicationLayer.Create(0x01, 0x02, payload);
                var parsed = ApplicationLayer.Parse(created.ApplicationPacket);

                Assert.Equal(payload, parsed.Data);
            }
        }

        #endregion

        #region Edge Cases

        public class EdgeCases
        {
            [Fact]
            public void MaxSizePayload_HandlesCorrectly()
            {
                byte[] payload = new byte[ushort.MaxValue - 2];

                var layer = ApplicationLayer.Create(0x01, 0x02, payload);

                Assert.Equal(ushort.MaxValue, layer.ApplicationPacket.Length);
            }

            [Fact]
            public void CommandProperty_CombinesBytesCorrectly()
            {
                var layer = ApplicationLayer.Create(0xAB, 0xCD, []);

                Assert.Equal(0xABCD, layer.Command);
            }

            [Fact]
            public void ApplicationHeader_AlwaysTwoBytes()
            {
                var layer = ApplicationLayer.Create(0x01, 0x02, new byte[100]);

                Assert.Equal(2, layer.ApplicationHeader.Length);
            }
        }

        #endregion
    }
}
