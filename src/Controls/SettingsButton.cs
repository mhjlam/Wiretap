using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Wiretap.Controls
{
    public sealed partial class SettingsButton : Button
    {
        public SettingsButton()
        {
            // Match the style of other buttons (PlusButton/ClearButton)
            Width = 36;
            Height = 36;
            CornerRadius = new CornerRadius(18);
            Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            BorderThickness = new Thickness(0);
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Padding = new Thickness(0);
            
            // Create settings icon (gear with proper white color and centered)
            var symbolIcon = new SymbolIcon(Symbol.Setting)
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            Content = symbolIcon;
            
            ToolTipService.SetToolTip(this, "Settings");
            
            // Add hover effects that match other buttons
            PointerEntered += (s, e) =>
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.RoyalBlue);
            };
            
            PointerExited += (s, e) =>
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            };
        }
    }
}
