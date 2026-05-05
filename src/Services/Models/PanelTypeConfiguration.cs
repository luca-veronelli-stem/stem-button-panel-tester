using Core.Enums;

namespace Services.Models
{
    /// <summary>
    /// Configurazione per un tipo di pannello pulsantiera.
    /// </summary>
    public sealed class PanelTypeConfiguration
    {
        public ButtonPanelType PanelType { get; init; }
        public byte MachineType { get; init; }
        public ushort FirmwareType { get; init; }
        public uint TargetAddress { get; init; }

        private static readonly Dictionary<ButtonPanelType, PanelTypeConfiguration> _configurations = new()
        {
            [ButtonPanelType.DIS0023789] = new PanelTypeConfiguration
            {
                PanelType = ButtonPanelType.DIS0023789,
                MachineType = 0x03,
                FirmwareType = 0x0004,
                TargetAddress = 0x00030101
            },
            [ButtonPanelType.DIS0025205] = new PanelTypeConfiguration
            {
                PanelType = ButtonPanelType.DIS0025205,
                MachineType = 0x0A,
                FirmwareType = 0x0004,
                TargetAddress = 0x000A0101
            },
            [ButtonPanelType.DIS0026166] = new PanelTypeConfiguration
            {
                PanelType = ButtonPanelType.DIS0026166,
                MachineType = 0x0B,
                FirmwareType = 0x0004,
                TargetAddress = 0x000B0101
            },
            [ButtonPanelType.DIS0026182] = new PanelTypeConfiguration
            {
                PanelType = ButtonPanelType.DIS0026182,
                MachineType = 0x0C,
                FirmwareType = 0x0004,
                TargetAddress = 0x000C0101
            }
        };

        /// <summary>
        /// Ottiene la configurazione per un tipo di pannello specifico.
        /// </summary>
        public static PanelTypeConfiguration GetConfiguration(ButtonPanelType panelType)
        {
            return _configurations.TryGetValue(panelType, out PanelTypeConfiguration? config)
                ? config
                : _configurations[ButtonPanelType.DIS0023789]; // Default Eden
        }
    }
}
