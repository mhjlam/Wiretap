using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Wiretap.Models;

namespace Wiretap.Services
{
    public interface IVisualEffectsService
    {
        void StartStatusBlink();
        void StartListenerBlink(BaseListener listener);
        void SetStatusIndicator(SolidColorBrush brush);
    }
}