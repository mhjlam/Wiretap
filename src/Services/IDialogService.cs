using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Wiretap.Models;

namespace Wiretap.Services
{
    public interface IDialogService
    {
        Task<ContentDialogResult> ShowAddListenerDialogAsync(XamlRoot xamlRoot);
        Task<ContentDialogResult> ShowRemoveListenerConfirmationAsync(string listenerName, XamlRoot xamlRoot);
        Task<bool> ShowSettingsDialogAsync(Window owner);
        BaseListener? LastCreatedListener { get; }
    }
}