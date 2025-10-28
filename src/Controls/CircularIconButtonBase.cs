using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Wiretap.Controls
{
    /// <summary>
    /// Base class for circular icon buttons to eliminate code duplication
    /// </summary>
    public abstract class CircularIconButtonBase : UserControl
    {
        public event RoutedEventHandler? Click;

        private Button _innerButton = null!;
        protected abstract Symbol IconSymbol { get; }
        protected virtual double ButtonSize => 36;
        protected virtual new double CornerRadius => 18; // Use 'new' to hide inherited member
        protected virtual bool HasHoverEffects => true;

        protected CircularIconButtonBase()
        {
            CreateButton();
        }

        private void CreateButton()
        {
            _innerButton = new Button
            {
                Width = ButtonSize,
                Height = ButtonSize,
                CornerRadius = new CornerRadius(CornerRadius),
                Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };

            var symbolIcon = new SymbolIcon(IconSymbol)
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _innerButton.Content = symbolIcon;
            _innerButton.Click += (s, e) => Click?.Invoke(this, e);

            this.Content = _innerButton;
            
            if (HasHoverEffects)
            {
                AddHoverEffects();
            }
        }

        private void AddHoverEffects()
        {
            _innerButton.PointerEntered += (s, e) =>
            {
                _innerButton.Background = new SolidColorBrush(Microsoft.UI.Colors.RoyalBlue);
            };
            
            _innerButton.PointerExited += (s, e) =>
            {
                _innerButton.Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            };
        }
    }
}