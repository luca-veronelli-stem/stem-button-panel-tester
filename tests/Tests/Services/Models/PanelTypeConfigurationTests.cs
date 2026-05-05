using Core.Enums;
using Services.Models;

namespace Tests.Services.Models
{
    /// <summary>
    /// Unit test per PanelTypeConfiguration.
    /// </summary>
    public class PanelTypeConfigurationTests
    {
        [Fact]
        public void GetConfiguration_ForDIS0023789_ReturnsEdenConfiguration()
        {
            // Act
            var config = PanelTypeConfiguration.GetConfiguration(ButtonPanelType.DIS0023789);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0023789, config.PanelType);
            Assert.Equal(0x03, config.MachineType);
            Assert.Equal(0x0004, config.FirmwareType);
            Assert.Equal(0x00030101u, config.TargetAddress);
        }

        [Fact]
        public void GetConfiguration_ForDIS0025205_ReturnsOptimusConfiguration()
        {
            // Act
            var config = PanelTypeConfiguration.GetConfiguration(ButtonPanelType.DIS0025205);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0025205, config.PanelType);
            Assert.Equal(0x0A, config.MachineType);
            Assert.Equal(0x0004, config.FirmwareType);
            Assert.Equal(0x000A0101u, config.TargetAddress);
        }

        [Fact]
        public void GetConfiguration_ForDIS0026166_ReturnsR3LXPConfiguration()
        {
            // Act
            var config = PanelTypeConfiguration.GetConfiguration(ButtonPanelType.DIS0026166);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0026166, config.PanelType);
            Assert.Equal(0x0B, config.MachineType);
            Assert.Equal(0x0004, config.FirmwareType);
            Assert.Equal(0x000B0101u, config.TargetAddress);
        }

        [Fact]
        public void GetConfiguration_ForDIS0026182_ReturnsR3LXPPlusConfiguration()
        {
            // Act
            var config = PanelTypeConfiguration.GetConfiguration(ButtonPanelType.DIS0026182);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0026182, config.PanelType);
            Assert.Equal(0x0C, config.MachineType);
            Assert.Equal(0x0004, config.FirmwareType);
            Assert.Equal(0x000C0101u, config.TargetAddress);
        }

        [Fact]
        public void GetConfiguration_MultipleCallsForSameType_ReturnsSameInstance()
        {
            // Act
            var config1 = PanelTypeConfiguration.GetConfiguration(ButtonPanelType.DIS0023789);
            var config2 = PanelTypeConfiguration.GetConfiguration(ButtonPanelType.DIS0023789);

            // Assert
            Assert.Same(config1, config2); // Flyweight pattern: stessa istanza
        }

        [Fact]
        public void GetConfiguration_AllPanelTypes_HaveFirmwareType0x0004()
        {
            // Arrange
            ButtonPanelType[] panelTypes = new[]
            {
                ButtonPanelType.DIS0023789,
                ButtonPanelType.DIS0025205,
                ButtonPanelType.DIS0026166,
                ButtonPanelType.DIS0026182
            };

            // Act & Assert
            foreach (ButtonPanelType panelType in panelTypes)
            {
                var config = PanelTypeConfiguration.GetConfiguration(panelType);
                Assert.Equal(0x0004, config.FirmwareType);
            }
        }

        [Fact]
        public void GetConfiguration_AllPanelTypes_HaveUniqueMachineTypes()
        {
            // Arrange
            ButtonPanelType[] panelTypes = new[]
            {
                ButtonPanelType.DIS0023789,
                ButtonPanelType.DIS0025205,
                ButtonPanelType.DIS0026166,
                ButtonPanelType.DIS0026182
            };

            // Act
            var machineTypes = panelTypes
                .Select(pt => PanelTypeConfiguration.GetConfiguration(pt).MachineType)
                .ToList();

            // Assert
            Assert.Equal(machineTypes.Count, machineTypes.Distinct().Count()); // Tutti unici
        }

        [Fact]
        public void GetConfiguration_AllPanelTypes_HaveUniqueTargetAddresses()
        {
            // Arrange
            ButtonPanelType[] panelTypes = new[]
            {
                ButtonPanelType.DIS0023789,
                ButtonPanelType.DIS0025205,
                ButtonPanelType.DIS0026166,
                ButtonPanelType.DIS0026182
            };

            // Act
            var targetAddresses = panelTypes
                .Select(pt => PanelTypeConfiguration.GetConfiguration(pt).TargetAddress)
                .ToList();

            // Assert
            Assert.Equal(targetAddresses.Count, targetAddresses.Distinct().Count()); // Tutti unici
        }

        [Theory]
        [InlineData(ButtonPanelType.DIS0023789, 0x03)]
        [InlineData(ButtonPanelType.DIS0025205, 0x0A)]
        [InlineData(ButtonPanelType.DIS0026166, 0x0B)]
        [InlineData(ButtonPanelType.DIS0026182, 0x0C)]
        public void GetConfiguration_ForVariousPanelTypes_ReturnCorrectMachineType(
            ButtonPanelType panelType,
            byte expectedMachineType)
        {
            // Act
            var config = PanelTypeConfiguration.GetConfiguration(panelType);

            // Assert
            Assert.Equal(expectedMachineType, config.MachineType);
        }
    }
}
