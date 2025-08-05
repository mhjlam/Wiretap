using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Wiretap.Extensions
{
    /// <summary>
    /// Extension methods for easier access to application resources
    /// </summary>
    public static class ResourceExtensions
    {
        /// <summary>
        /// Gets a brush resource from the current application
        /// </summary>
        public static Brush? GetBrush(this Application app, string key)
        {
            return app.Resources.TryGetValue(key, out var resource) ? resource as Brush : null;
        }

        /// <summary>
        /// Gets a brush resource from the current application (static helper)
        /// </summary>
        public static Brush? GetBrush(string key)
        {
            return Application.Current?.Resources.TryGetValue(key, out var resource) == true ? resource as Brush : null;
        }

        /// <summary>
        /// Gets a themed brush with fallback
        /// </summary>
        public static Brush GetBrushOrDefault(string key, Brush fallback)
        {
            return GetBrush(key) ?? fallback;
        }

        /// <summary>
        /// Gets any resource type from the current application
        /// </summary>
        public static T? GetResource<T>(string key) where T : class
        {
            return Application.Current?.Resources.TryGetValue(key, out var resource) == true ? resource as T : null;
        }
    }
}