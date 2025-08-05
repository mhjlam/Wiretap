using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Wiretap.Models;

namespace Wiretap.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ListenerStatus status)
            {
                return status switch
                {
                    ListenerStatus.Enabled => new SolidColorBrush(Colors.Green),
                    ListenerStatus.Disabled => new SolidColorBrush(Colors.Gray),
                    ListenerStatus.Error => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}