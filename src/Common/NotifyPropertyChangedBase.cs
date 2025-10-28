using Microsoft.UI.Dispatching;
using System.ComponentModel;
using System.Threading;

namespace Wiretap.Common
{
    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        private DispatcherQueue? _dispatcherQueue;
        private readonly ThreadLocal<bool> _isRaisingPropertyChanged = new(() => false);

        protected NotifyPropertyChangedBase()
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private DispatcherQueue? GetDispatcherQueue()
        {
            if (_dispatcherQueue == null)
            {
                try
                {
                    _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                }
                catch
                {
                    // If we can't get the dispatcher queue, that's okay
                    // We'll just invoke directly
                    _dispatcherQueue = null;
                }
            }
            return _dispatcherQueue;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            // Prevent infinite loops by checking if we're already raising PropertyChanged
            if (_isRaisingPropertyChanged.Value)
            {
                return;
            }

            try
            {
                _isRaisingPropertyChanged.Value = true;
                
                var dispatcher = GetDispatcherQueue();
                
                if (dispatcher?.HasThreadAccess == true)
                {
                    // We're on the UI thread, safe to invoke directly
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
                else if (dispatcher != null)
                {
                    // We're on a background thread, dispatch to UI thread
                    dispatcher.TryEnqueue(() =>
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    });
                }
                else
                {
                    // No dispatcher available, invoke directly (fallback)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
            }
            finally
            {
                _isRaisingPropertyChanged.Value = false;
            }
        }
    }
}
