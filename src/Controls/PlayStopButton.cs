using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Wiretap.Controls
{
    public sealed partial class PlayStopButton : UserControl
    {
        public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
            nameof(IsPlaying), 
            typeof(bool), 
            typeof(PlayStopButton), 
            new PropertyMetadata(false, OnIsPlayingChanged)
        );

        public static readonly DependencyProperty ButtonSizeProperty = DependencyProperty.Register(
            nameof(ButtonSize), 
            typeof(double), 
            typeof(PlayStopButton), 
            new PropertyMetadata(18.0, OnButtonSizeChanged)
        );

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public double ButtonSize
        {
            get => (double)GetValue(ButtonSizeProperty);
            set => SetValue(ButtonSizeProperty, value);
        }

        public event RoutedEventHandler? Click;

        private Button _innerButton = null!;
        private SymbolIcon _symbolIcon = null!;
        private Viewbox _viewbox = null!;

        public PlayStopButton()
        {
            CreateButton();
        }

        private void CreateButton()
        {
            // Create button with default size (will be updated by property)
            _innerButton = new Button
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };

            // Create symbol icon with proper centering
            _symbolIcon = new SymbolIcon
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Create viewbox for scaling
            _viewbox = new Viewbox
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = _symbolIcon
            };

            _innerButton.Content = _viewbox;
            _innerButton.Click += (s, e) => Click?.Invoke(this, e);

            this.Content = _innerButton;
            
            UpdateButtonSize();
            UpdateIcon();
        }

        private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PlayStopButton button)
            {
                button.UpdateIcon();
            }
        }

        private static void OnButtonSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PlayStopButton button)
            {
                button.UpdateButtonSize();
            }
        }

        private void UpdateButtonSize()
        {
            var size = ButtonSize;
            _innerButton.Width = size;
            _innerButton.Height = size;
            _innerButton.CornerRadius = new CornerRadius(size / 2);
            
            // Scale icon proportionally: small buttons (18) use 10, larger buttons use more
            var iconSize = size * (10.0 / 18.0) + 2; // This gives us 10 for size 18, 16 for size 24
            _viewbox.Width = iconSize;
            _viewbox.Height = iconSize;
        }

        private void UpdateIcon()
        {
            if (IsPlaying)
            {
                // Show Play icon when listeners are active/running (green)
                _symbolIcon.Symbol = Symbol.Play;
                _symbolIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            else
            {
                // Show Pause icon when listeners are paused/stopped (orange)
                _symbolIcon.Symbol = Symbol.Pause;
                _symbolIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
            }
        }
    }
}
