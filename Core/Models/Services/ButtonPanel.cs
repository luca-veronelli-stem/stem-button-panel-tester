using Core.Enums;

namespace Core.Models.Services
{
    // Modello che rappresenta una pulsantiera con le sue caratteristiche
    public class ButtonPanel
    {
        public ButtonPanelType Type { get; set; }
        public int ButtonCount { get; set; }
        public bool HasLed { get; set; }
        // Tutte le pulsantiere hanno il buzzer, questo campo è per estensibilità futura
        public bool HasBuzzer { get; set; } = true;
        public string[] Buttons { get; set; } = [];
        public List<byte> ButtonMasks { get; set; } = [];

        /// <summary>
        /// Variable IDs that can be used by this panel to report button status.
        /// Format: high byte + low byte (e.g., 0x803E means bytes 0x80, 0x3E in the payload).
        /// Button panels can use either 0x8000 or 0x803E to report button presses.
        /// </summary>
        public ushort[] ButtonStatusVariableIds { get; set; } = [0x8000, 0x803E];

        // Metodo factory per creare una pulsantiera in base al tipo
        public static ButtonPanel GetByType(ButtonPanelType type)
        {
            return type switch
            {
                // La pulsantiera di tipo DIS0025205 (Optimus-XP) ha 4 pulsanti senza LED
                ButtonPanelType.DIS0025205 => new ButtonPanel
                {
                    Type = type,
                    ButtonCount = 4,
                    HasLed = false,
                    Buttons = GetButtonsByType(type),
                    ButtonMasks = [0x04, 0x10, 0x02, 0x20],
                    ButtonStatusVariableIds = [0x8000, 0x803E] // Can use either variable ID
                },
                // DIS0026166 (R3 LXP) - uses different button status variable ID
                ButtonPanelType.DIS0026166 => new ButtonPanel
                {
                    Type = type,
                    ButtonCount = 8,
                    HasLed = true,
                    Buttons = GetButtonsByType(type),
                    ButtonMasks = [0x40, 0x04, 0x08, 0x10, 0x80, 0x02, 0x01, 0x20],
                    ButtonStatusVariableIds = [0x8000, 0x803E] // Can use either variable ID
                },
                // DIS0026182 (R3 LXP+) - likely uses same as R3 LXP
                ButtonPanelType.DIS0026182 => new ButtonPanel
                {
                    Type = type,
                    ButtonCount = 8,
                    HasLed = true,
                    Buttons = GetButtonsByType(type),
                    ButtonMasks = [0x40, 0x04, 0x08, 0x10, 0x80, 0x02, 0x01, 0x20],
                    ButtonStatusVariableIds = [0x8000, 0x803E] // Can use either variable ID
                },
                // Le altre pulsantiere (Eden) hanno tutte 8 pulsanti con LED
                _ => new ButtonPanel
                {
                    Type = type,
                    ButtonCount = 8,
                    HasLed = true,
                    Buttons = GetButtonsByType(type),
                    ButtonMasks = [0x40, 0x04, 0x08, 0x10, 0x80, 0x02, 0x01, 0x20],
                    ButtonStatusVariableIds = [0x8000, 0x803E] // Can use either variable ID
                }
            };
        }

        private static string[] GetButtonsByType(ButtonPanelType type)
        {
            return type switch
            {
                ButtonPanelType.DIS0025205 => Enum.GetNames(typeof(OptimusButtons)),
                ButtonPanelType.DIS0026166 => Enum.GetNames(typeof(R3LXPButtons)),
                _ => Enum.GetNames(typeof(EdenButtons)),
            };
        }
    }
}
