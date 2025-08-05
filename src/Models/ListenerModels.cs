using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Wiretap.Common;
using Wiretap.Services;

namespace Wiretap.Models
{
    public enum ListenerProtocol
    {
        UDP,
        TCP,
        COM,
        Pipe,
        USB
    }

    public enum ListenerStatus
    {
        Disabled,
        Enabled,
        Error
    }

    public enum ComParity
    {
        None,
        Odd,
        Even,
        Mark,
        Space
    }

    public enum ComStopBits
    {
        One,
        OnePointFive,
        Two
    }

    public abstract class BaseListener : NotifyPropertyChangedBase
    {
        private ListenerStatus _status = ListenerStatus.Disabled;
        private bool _isListening;
        private readonly object _statusLock = new object();

        protected BaseListener()
        {
            // Name will be generated based on properties after they are set
        }

        // Helper method to check if connection status messages should be shown
        protected bool ShouldShowConnectionStatus()
        {
            try
            {
                var settingsService = ServiceContainer.Instance.SettingsService;
                if (settingsService != null)
                {
                    var task = settingsService.LoadSettingsAsync();
                    if (task.Wait(100)) // Short timeout to avoid blocking
                    {
                        return task.Result.Listeners.ShowConnectionStatus;
                    }
                }
                return false; // Default to NOT showing connection status if settings unavailable
            }
            catch
            {
                return false; // Default to NOT showing connection status on error
            }
        }

        // Generate unique name based on properties - not user configurable
        private void UpdateName()
        {
            try
            {
                var newName = Protocol switch
                {
                    ListenerProtocol.UDP => $"UDP:{GetPortFromListener()}",
                    ListenerProtocol.TCP => $"TCP:{GetPortFromListener()}",
                    ListenerProtocol.COM => GetIdentifierFromListener(),
                    ListenerProtocol.Pipe => $"\\\\.\\pipe\\{GetIdentifierFromListener()}",
                    ListenerProtocol.USB => $"USB:{GetIdentifierFromListener()}",
                    _ => "Unknown"
                };

                if (Name != newName)
                {
                    Name = newName;
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(DisplayInfo));
                }
            }
            catch
            {
                // Silently handle name update errors and set a fallback name
                if (string.IsNullOrEmpty(Name))
                {
                    Name = $"{Protocol}_Listener";
                }
            }
        }

        // Abstract methods for getting listener-specific identifiers
        protected abstract string GetIdentifierFromListener();
        protected virtual int GetPortFromListener() => 0;

        // Name is read-only and based on properties
        public string Name { get; private set; } = string.Empty;

        // Call this method when properties that affect the name change
        protected void OnNamePropertyChanged()
        {
            try
            {
                UpdateName();
            }
            catch
            {
                // Silently handle name property change errors
            }
        }

        public abstract ListenerProtocol Protocol { get; }

        public ListenerStatus Status
        {
            get
            {
                lock (_statusLock)
                {
                    return _status;
                }
            }
            set
            {
                lock (_statusLock)
                {
                    if (_status != value)
                    {
                        _status = value;
                        OnPropertyChanged(nameof(Status));
                    }
                }
            }
        }

        // Add property for data binding
        public string DisplayInfo
        {
            get
            {
                try
                {
                    return GetDisplayInfo();
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
        }

        public abstract bool IsValid();
        public abstract string GetDisplayInfo();
        
        // Backend listener management with result pattern
        public abstract Task<ListenerOperationResult> StartListeningAsync();
        public abstract Task<ListenerOperationResult> StopListeningAsync();
        
        public virtual bool IsListening 
        { 
            get => _isListening;
            protected set
            {
                if (_isListening != value)
                {
                    _isListening = value;
                    OnPropertyChanged(nameof(IsListening));
                }
            }
        }
        
        // Event for when messages are received
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        
        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(Name, message));
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public string Source { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public MessageReceivedEventArgs(string source, string message)
        {
            Source = source;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }
}