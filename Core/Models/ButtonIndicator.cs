using Core.Enums;

using System.Drawing;

namespace Core.Models
{
    public class ButtonIndicator
    {
        public RectangleF Bounds { get; set; }
        public IndicatorState State { get; set; } = IndicatorState.Idle;
    }
}
