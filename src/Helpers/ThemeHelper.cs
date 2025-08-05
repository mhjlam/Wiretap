using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Wiretap.Helpers
{
    /// <summary>
    /// Helper class for accessing WinUI 3 theme resources
    /// </summary>
    public static class ThemeHelper
    {
        // Common brush resources - cached for better performance
        private static Brush? _textFillColorSecondaryBrush;
        private static Brush? _textFillColorTertiaryBrush;
        private static Brush? _accentFillColorDefaultBrush;
        private static Brush? _layerFillColorDefaultBrush;
        private static Brush? _cardStrokeColorDefaultBrush;
        private static Brush? _subtleFillColorSecondaryBrush;

        public static Brush TextFillColorSecondaryBrush => 
            _textFillColorSecondaryBrush ??= GetBrush("TextFillColorSecondaryBrush") ?? new SolidColorBrush(Colors.Gray);

        public static Brush TextFillColorTertiaryBrush => 
            _textFillColorTertiaryBrush ??= GetBrush("TextFillColorTertiaryBrush") ?? new SolidColorBrush(Colors.LightGray);

        public static Brush AccentFillColorDefaultBrush => 
            _accentFillColorDefaultBrush ??= GetBrush("AccentFillColorDefaultBrush") ?? new SolidColorBrush(Colors.DodgerBlue);

        public static Brush LayerFillColorDefaultBrush => 
            _layerFillColorDefaultBrush ??= GetBrush("LayerFillColorDefaultBrush") ?? new SolidColorBrush(Colors.White);

        public static Brush CardStrokeColorDefaultBrush => 
            _cardStrokeColorDefaultBrush ??= GetBrush("CardStrokeColorDefaultBrush") ?? new SolidColorBrush(Colors.LightGray);

        public static Brush SubtleFillColorSecondaryBrush => 
            _subtleFillColorSecondaryBrush ??= GetBrush("SubtleFillColorSecondaryBrush") ?? new SolidColorBrush(Colors.WhiteSmoke);

        /// <summary>
        /// Gets a brush resource with fallback
        /// </summary>
        private static Brush? GetBrush(string key)
        {
            return Application.Current?.Resources.TryGetValue(key, out var resource) == true ? resource as Brush : null;
        }

        /// <summary>
        /// Clears cached brushes (call when theme changes)
        /// </summary>
        public static void ClearCache()
        {
            _textFillColorSecondaryBrush = null;
            _textFillColorTertiaryBrush = null;
            _accentFillColorDefaultBrush = null;
            _layerFillColorDefaultBrush = null;
            _cardStrokeColorDefaultBrush = null;
            _subtleFillColorSecondaryBrush = null;
        }

        /// <summary>
        /// Creates a transparent brush
        /// </summary>
        public static Brush Transparent => new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }
}