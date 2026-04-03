namespace Core.Enums
{
    // Tipi possibili di pulsantiere
    public enum ButtonPanelType
    {
        DIS0023789,
        DIS0025205,
        DIS0026166,
        DIS0026182
    }

    // Tipi possibili ti test per le pulsantiere
    public enum ButtonPanelTestType
    {
        Complete,
        Buttons,
        Led,
        Buzzer
    }

    // Stati indicatore pulsanti
    public enum IndicatorState
    {
        Idle,
        Waiting,
        Success,
        Failed
    }

    // Pulsanti Eden-XP e Eden BS8
    public enum EdenButtons
    {
        Stop,
        Horizontal,
        Suspension,
        Up,
        Lights,
        HeadDown,
        HeadUp,
        Down
    }

    // Pulsanti R-3L XP
    public enum R3LXPButtons
    {
        Stop,
        Up,
        HeadUp,
        FeetUp,
        Lights,
        Down,
        HeadDown,
        FeetDown
    }

    // Pulsanti Optimus-XP
    public enum OptimusButtons
    {
        Suspension,
        Up,
        Lights,
        Down
    }
}
