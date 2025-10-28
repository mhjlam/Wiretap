using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Wiretap.Controls
{
	public sealed partial class ScrollToTopButton : UserControl
	{
		public event RoutedEventHandler? Click;

		private Button _innerButton = null!;

		public ScrollToTopButton()
		{
			CreateButton();

			ToolTipService.SetToolTip(this, "Scroll to Top");
		}

		private void CreateButton()
		{
			// Create a circular button with SymbolIcon - matching other buttons
			_innerButton = new Button
			{
				Width = 36,
				Height = 36,
				CornerRadius = new CornerRadius(18),
				Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
				BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
				BorderThickness = new Thickness(0),
				Padding = new Thickness(0)
			};

			// Create Up icon with proper white color and centered (using Up instead of ChevronUpMed)
			var symbolIcon = new SymbolIcon(Symbol.Up)
			{
				Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};

			_innerButton.Content = symbolIcon;
			_innerButton.Click += (s, e) => Click?.Invoke(this, e);

			this.Content = _innerButton;
			
			// Add hover effects that match other buttons
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
