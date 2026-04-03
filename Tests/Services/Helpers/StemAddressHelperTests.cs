using Services.Helpers;

namespace Tests.Services.Helpers
{
    /// <summary>
    /// Unit test per StemAddressHelper.
    /// </summary>
    public class StemAddressHelperTests
    {
        [Fact]
        public void CalculateAddress_WithValidInputs_ReturnsCorrectAddress()
        {
            // Arrange
            byte machineType = 0x03;
            ushort firmwareType = 0x0004;
            byte boardNumber = 0x01;

            // Act
            uint address = StemAddressHelper.CalculateAddress(machineType, firmwareType, boardNumber);

            // Assert
            Assert.Equal(0x00030101u, address);
        }

        [Fact]
        public void CalculateAddress_WithZeroMachine_ReturnsCorrectAddress()
        {
            // Arrange
            byte machineType = 0x00;
            ushort firmwareType = 0x0004;
            byte boardNumber = 0x01;

            // Act
            uint address = StemAddressHelper.CalculateAddress(machineType, firmwareType, boardNumber);

            // Assert
            Assert.Equal(0x00000101u, address);
        }

        [Fact]
        public void ExtractMachineType_WithValidAddress_ReturnsCorrectValue()
        {
            // Arrange
            uint address = 0x00030101u;

            // Act
            byte machineType = StemAddressHelper.ExtractMachineType(address);

            // Assert
            Assert.Equal(0x03, machineType);
        }

        [Fact]
        public void ExtractFirmwareType_WithValidAddress_ReturnsCorrectValue()
        {
            // Arrange
            uint address = 0x00030101u;

            // Act
            ushort firmwareType = StemAddressHelper.ExtractFirmwareType(address);

            // Assert
            Assert.Equal(0x0004, firmwareType);
        }

        [Fact]
        public void ExtractBoardNumber_WithValidAddress_ReturnsCorrectValue()
        {
            // Arrange
            uint address = 0x00030101u;

            // Act
            byte boardNumber = StemAddressHelper.ExtractBoardNumber(address);

            // Assert
            Assert.Equal(0x01, boardNumber);
        }

        [Theory]
        [InlineData(0x03, 0x0004, 0x01, 0x00030101u)]  // Eden
        [InlineData(0x0A, 0x0004, 0x01, 0x000A0101u)]  // Optimus
        [InlineData(0x0B, 0x0004, 0x01, 0x000B0101u)]  // R3-LXP
        [InlineData(0x0C, 0x0004, 0x01, 0x000C0101u)]  // R3-LXP+
        [InlineData(0x00, 0x0004, 0xFF, 0x0000013Fu)]  // Test temporaneo con 0xFF (0xFF & 0x3F = 0x3F)
        public void CalculateAddress_WithVariousInputs_ReturnsExpectedAddresses(
            byte machineType,
            ushort firmwareType,
            byte boardNumber,
            uint expectedAddress)
        {
            // Act
            uint address = StemAddressHelper.CalculateAddress(machineType, firmwareType, boardNumber);

            // Assert
            Assert.Equal(expectedAddress, address);
        }

        [Fact]
        public void RoundTrip_CalculateAndExtract_PreservesValues()
        {
            // Arrange
            byte originalMachine = 0x0A;
            ushort originalFirmware = 0x0004;
            byte originalBoard = 0x15;

            // Act
            uint address = StemAddressHelper.CalculateAddress(originalMachine, originalFirmware, originalBoard);
            byte extractedMachine = StemAddressHelper.ExtractMachineType(address);
            ushort extractedFirmware = StemAddressHelper.ExtractFirmwareType(address);
            byte extractedBoard = StemAddressHelper.ExtractBoardNumber(address);

            // Assert
            Assert.Equal(originalMachine, extractedMachine);
            Assert.Equal(originalFirmware, extractedFirmware);
            Assert.Equal(originalBoard, extractedBoard);
        }
    }
}
