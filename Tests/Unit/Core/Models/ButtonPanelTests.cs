using Core.Enums;
using Core.Models.Services;

namespace Tests.Unit.Core.Models
{
    /// <summary>
    /// Unit tests for ButtonPanel model and factory method.
    /// Tests panel configuration, button mappings, and LED support.
    /// </summary>
    public class ButtonPanelTests
    {
        #region GetByType Factory Tests

        [Fact]
        public void GetByType_DIS0023789_Returns8ButtonPanelWithLed()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0023789);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0023789, panel.Type);
            Assert.Equal(8, panel.ButtonCount);
            Assert.True(panel.HasLed);
            Assert.True(panel.HasBuzzer);
            Assert.Equal(8, panel.Buttons.Length);
            Assert.Equal(8, panel.ButtonMasks.Count);
        }

        [Fact]
        public void GetByType_DIS0025205_Returns4ButtonPanelWithoutLed()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0025205);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0025205, panel.Type);
            Assert.Equal(4, panel.ButtonCount);
            Assert.False(panel.HasLed);
            Assert.True(panel.HasBuzzer);
            Assert.Equal(4, panel.Buttons.Length);
            Assert.Equal(4, panel.ButtonMasks.Count);
        }

        [Fact]
        public void GetByType_DIS0026166_Returns8ButtonPanelWithLed()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0026166);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0026166, panel.Type);
            Assert.Equal(8, panel.ButtonCount);
            Assert.True(panel.HasLed);
            Assert.True(panel.HasBuzzer);
            Assert.Equal(8, panel.Buttons.Length);
            Assert.Equal(8, panel.ButtonMasks.Count);
        }

        [Fact]
        public void GetByType_DIS0026182_Returns8ButtonPanelWithLed()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0026182);

            // Assert
            Assert.Equal(ButtonPanelType.DIS0026182, panel.Type);
            Assert.Equal(8, panel.ButtonCount);
            Assert.True(panel.HasLed);
            Assert.True(panel.HasBuzzer);
            Assert.Equal(8, panel.Buttons.Length);
            Assert.Equal(8, panel.ButtonMasks.Count);
        }

        #endregion

        #region Button Names Tests

        [Fact]
        public void GetByType_DIS0023789_HasEdenButtons()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0023789);

            // Assert - Eden buttons
            Assert.Contains("Stop", panel.Buttons);
            Assert.Contains("Horizontal", panel.Buttons);
            Assert.Contains("Suspension", panel.Buttons);
            Assert.Contains("Up", panel.Buttons);
            Assert.Contains("Lights", panel.Buttons);
            Assert.Contains("HeadDown", panel.Buttons);
            Assert.Contains("HeadUp", panel.Buttons);
            Assert.Contains("Down", panel.Buttons);
        }

        [Fact]
        public void GetByType_DIS0025205_HasOptimusButtons()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0025205);

            // Assert - Optimus buttons
            Assert.Contains("Suspension", panel.Buttons);
            Assert.Contains("Up", panel.Buttons);
            Assert.Contains("Lights", panel.Buttons);
            Assert.Contains("Down", panel.Buttons);
            Assert.Equal(4, panel.Buttons.Length);
        }

        [Fact]
        public void GetByType_DIS0026166_HasR3LXPButtons()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0026166);

            // Assert - R3LXP buttons
            Assert.Contains("Stop", panel.Buttons);
            Assert.Contains("Up", panel.Buttons);
            Assert.Contains("HeadUp", panel.Buttons);
            Assert.Contains("FeetUp", panel.Buttons);
            Assert.Contains("Lights", panel.Buttons);
            Assert.Contains("Down", panel.Buttons);
            Assert.Contains("HeadDown", panel.Buttons);
            Assert.Contains("FeetDown", panel.Buttons);
        }

        [Fact]
        public void GetByType_DIS0026182_HasEdenButtons()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0026182);

            // Assert - Eden buttons (default case)
            Assert.Contains("Stop", panel.Buttons);
            Assert.Contains("Horizontal", panel.Buttons);
        }

        #endregion

        #region Button Masks Tests

        [Fact]
        public void GetByType_DIS0025205_HasCorrectButtonMasks()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0025205);

            // Assert - Optimus masks: [0x04, 0x10, 0x02, 0x20]
            Assert.Equal(4, panel.ButtonMasks.Count);
            Assert.Equal(0x04, panel.ButtonMasks[0]);
            Assert.Equal(0x10, panel.ButtonMasks[1]);
            Assert.Equal(0x02, panel.ButtonMasks[2]);
            Assert.Equal(0x20, panel.ButtonMasks[3]);
        }

        [Fact]
        public void GetByType_8ButtonPanel_HasCorrectButtonMasks()
        {
            // Act
            var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0023789);

            // Assert - 8-button masks: [0x40, 0x04, 0x08, 0x10, 0x80, 0x02, 0x01, 0x20]
            Assert.Equal(8, panel.ButtonMasks.Count);
            Assert.Equal(0x40, panel.ButtonMasks[0]);
            Assert.Equal(0x04, panel.ButtonMasks[1]);
            Assert.Equal(0x08, panel.ButtonMasks[2]);
            Assert.Equal(0x10, panel.ButtonMasks[3]);
            Assert.Equal(0x80, panel.ButtonMasks[4]);
            Assert.Equal(0x02, panel.ButtonMasks[5]);
            Assert.Equal(0x01, panel.ButtonMasks[6]);
            Assert.Equal(0x20, panel.ButtonMasks[7]);
        }

        [Fact]
        public void GetByType_AllPanels_HaveUniqueMasks()
        {
            // Act & Assert for each panel type
            foreach (ButtonPanelType panelType in Enum.GetValues<ButtonPanelType>())
            {
                var panel = ButtonPanel.GetByType(panelType);
                var distinctMasks = panel.ButtonMasks.Distinct().ToList();

                Assert.Equal(panel.ButtonMasks.Count, distinctMasks.Count);
            }
        }

        #endregion

        #region Consistency Tests

        [Theory]
        [InlineData(ButtonPanelType.DIS0023789)]
        [InlineData(ButtonPanelType.DIS0025205)]
        [InlineData(ButtonPanelType.DIS0026166)]
        [InlineData(ButtonPanelType.DIS0026182)]
        public void GetByType_AllPanelTypes_HaveConsistentCounts(ButtonPanelType panelType)
        {
            // Act
            var panel = ButtonPanel.GetByType(panelType);

            // Assert - ButtonCount matches array and list lengths
            Assert.Equal(panel.ButtonCount, panel.Buttons.Length);
            Assert.Equal(panel.ButtonCount, panel.ButtonMasks.Count);
        }

        [Theory]
        [InlineData(ButtonPanelType.DIS0023789)]
        [InlineData(ButtonPanelType.DIS0025205)]
        [InlineData(ButtonPanelType.DIS0026166)]
        [InlineData(ButtonPanelType.DIS0026182)]
        public void GetByType_AllPanelTypes_HaveBuzzer(ButtonPanelType panelType)
        {
            // Act
            var panel = ButtonPanel.GetByType(panelType);

            // Assert - All panels have buzzer
            Assert.True(panel.HasBuzzer);
        }

        [Theory]
        [InlineData(ButtonPanelType.DIS0023789)]
        [InlineData(ButtonPanelType.DIS0025205)]
        [InlineData(ButtonPanelType.DIS0026166)]
        [InlineData(ButtonPanelType.DIS0026182)]
        public void GetByType_AllPanelTypes_HaveNonEmptyButtons(ButtonPanelType panelType)
        {
            // Act
            var panel = ButtonPanel.GetByType(panelType);

            // Assert - All buttons have non-empty names
            Assert.All(panel.Buttons, button => Assert.False(string.IsNullOrEmpty(button)));
        }

        [Theory]
        [InlineData(ButtonPanelType.DIS0023789)]
        [InlineData(ButtonPanelType.DIS0025205)]
        [InlineData(ButtonPanelType.DIS0026166)]
        [InlineData(ButtonPanelType.DIS0026182)]
        public void GetByType_AllPanelTypes_HaveNonZeroMasks(ButtonPanelType panelType)
        {
            // Act
            var panel = ButtonPanel.GetByType(panelType);

            // Assert - All masks are non-zero
            Assert.All(panel.ButtonMasks, mask => Assert.NotEqual(0, mask));
        }

        #endregion

        #region Default Values Tests

        [Fact]
        public void ButtonPanel_DefaultConstructor_HasDefaultValues()
        {
            // Act
            var panel = new ButtonPanel();

            // Assert
            Assert.Equal(default, panel.Type);
            Assert.Equal(0, panel.ButtonCount);
            Assert.False(panel.HasLed);
            Assert.True(panel.HasBuzzer); // Default is true
            Assert.Empty(panel.Buttons);
            Assert.Empty(panel.ButtonMasks);
        }

        [Fact]
        public void ButtonPanel_CanSetProperties()
        {
            // Arrange
            var panel = new ButtonPanel
            {
                Type = ButtonPanelType.DIS0023789,
                ButtonCount = 4,
                HasLed = true,
                HasBuzzer = false,
                Buttons = ["A", "B", "C", "D"],
                ButtonMasks = [0x01, 0x02, 0x03, 0x04]
            };

            // Assert
            Assert.Equal(ButtonPanelType.DIS0023789, panel.Type);
            Assert.Equal(4, panel.ButtonCount);
            Assert.True(panel.HasLed);
            Assert.False(panel.HasBuzzer);
            Assert.Equal(4, panel.Buttons.Length);
            Assert.Equal(4, panel.ButtonMasks.Count);
        }

        #endregion
    }
}
