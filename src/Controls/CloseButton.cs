using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Wiretap.Controls
{
    public sealed partial class CloseButton : UserControl
    {
        public event RoutedEventHandler? Click;

        private Button _innerButton = null!;

        public CloseButton()
        {
            CreateButton();
        }

        private void CreateButton()
        {
            // Create a standard button with SymbolIcon - same size as before but with icon
            _innerButton = new Button
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };

            // Create Cancel icon with proper blue color, centered, and scaled down for small button
            var symbolIcon = new SymbolIcon(Symbol.Cancel)
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Wrap in a Viewbox to scale the icon down for small button
            var viewbox = new Viewbox
            {
                Width = 10,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = symbolIcon
            };

            _innerButton.Content = viewbox;
            _innerButton.Click += (s, e) => Click?.Invoke(this, e);

            this.Content = _innerButton;
        }
    }
}
