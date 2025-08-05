using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Wiretap.Common;
using Wiretap.Models;
using Wiretap.Models.Listeners;
using Wiretap.Services;

namespace Wiretap.ViewModels
{
    public class ListenerMessageEventArgs : EventArgs
    {
        public BaseListener Listener { get; }
        public string Source { get; }
        public string Message { get; }
        
        public ListenerMessageEventArgs(BaseListener listener, string source, string message)
        {
            Listener = listener;
            Source = source;
            Message = message;
        }
    }

    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        private BaseListener? _selectedListener;
        private IncomingMessage? _selectedMessage;
        private DispatcherQueue? _dispatcherQueue;
        private bool _isPaused = false;
        private IListenerService? _listenerService;

        public ObservableCollection<IncomingMessage> IncomingMessages { get; }
        public ObservableCollection<BaseListener> ActiveListeners { get; }

        // Commands for MVVM binding
        public ICommand AddListenerCommand { get; }
        public ICommand RemoveListenerCommand { get; }
        public ICommand StartListenerCommand { get; }
        public ICommand StopListenerCommand { get; }
        public ICommand TogglePauseResumeCommand { get; }
        public ICommand ClearMessagesCommand { get; }
        public ICommand ScrollToTopCommand { get; }

        public BaseListener? SelectedListener
        {
            get => _selectedListener;
            set
            {
                if (_selectedListener != value)
                {
                    _selectedListener = value;
                    OnPropertyChanged(nameof(SelectedListener));
                    OnPropertyChanged(nameof(CanRemoveListener));
                    ((RelayCommand)RemoveListenerCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public IncomingMessage? SelectedMessage
        {
            get => _selectedMessage;
            set
            {
                if (_selectedMessage != value)
                {
                    _selectedMessage = value;
                    OnPropertyChanged(nameof(SelectedMessage));
                }
            }
        }

        public bool CanRemoveListener => SelectedListener != null;

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged(nameof(IsPaused));
                    OnPropertyChanged(nameof(PauseResumeButtonText));
                }
            }
        }

        public string PauseResumeButtonText => IsPaused ? "Resume" : "Pause";

        // Event for message blinking
        public event EventHandler? MessageReceived;
        public event EventHandler<ListenerMessageEventArgs>? ListenerMessageReceived;
        
        public MainWindowViewModel()
        {
            IncomingMessages = [];
            ActiveListeners = [];

            // Initialize commands - delegate to service methods
            AddListenerCommand = new RelayCommand<BaseListener>(AddListener, CanAddListener);
            RemoveListenerCommand = new RelayCommand<BaseListener>(RemoveListener, CanRemoveListenerExecute);
            StartListenerCommand = new RelayCommand<BaseListener>(StartListener, CanStartListener);
            StopListenerCommand = new RelayCommand<BaseListener>(StopListener, CanStopListener);
            TogglePauseResumeCommand = new RelayCommand(_ => TogglePauseResume());
            ClearMessagesCommand = new RelayCommand(_ => ClearMessages());
            ScrollToTopCommand = new RelayCommand(_ => ScrollToTop());

            // Store the dispatcher queue for thread-safe UI updates
            try
            {
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                _dispatcherQueue = null;
            }
        }

        public void SetListenerService(IListenerService listenerService)
        {
            _listenerService = listenerService;
            _listenerService.ListenerMessageReceived += OnServiceListenerMessageReceived;
        }

        // Public methods for MainWindow to call
        public void AddListener(BaseListener? listener)
        {
            if (listener != null)
            {
                _listenerService?.AddListener(listener);
            }
        }

        public void RemoveListener(BaseListener? listener)
        {
            if (listener != null)
            {
                _listenerService?.RemoveListener(listener);
                if (SelectedListener == listener)
                {
                    SelectedListener = null;
                }
            }
        }

        public void StartListener(BaseListener? listener)
        {
            if (listener != null)
            {
                _listenerService?.StartListener(listener);
            }
        }

        public void StopListener(BaseListener? listener)
        {
            if (listener != null)
            {
                _listenerService?.StopListener(listener);
            }
        }

        public void TogglePauseResume()
        {
            _listenerService?.TogglePauseResume();
            IsPaused = !IsPaused; // Update local state
        }

        private bool CanAddListener(BaseListener? listener)
        {
            return listener?.IsValid() == true && (_listenerService?.IsListenerUnique(listener) ?? false);
        }

        private bool CanRemoveListenerExecute(BaseListener? listener)
        {
            return listener != null;
        }

        private bool CanStartListener(BaseListener? listener)
        {
            return listener != null && !listener.IsListening && !IsPaused;
        }

        private bool CanStopListener(BaseListener? listener)
        {
            return listener?.IsListening == true;
        }

        private void ScrollToTop()
        {
            ScrollToTopRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? ScrollToTopRequested;

        public void AddDefaultListener()
        {
            // Check if we already have a UDP listener on port 8080
            if (ActiveListeners.Any(l => l is UdpListener udp && udp.Port == 8080))
            {
                return;
            }
            
            // Create and add default UDP listener
            var udpListener = new UdpListener { Port = 8080 };
            _listenerService?.AddListener(udpListener);
        }

        public void SetDispatcher(DispatcherQueue dispatcher)
        {
            _dispatcherQueue = dispatcher;
        }

        public void AddMessage(string source, string message)
        {
            var incomingMessage = new IncomingMessage(source, message);
            
            try
            {
                // Use a simpler approach - always try to dispatch if we have a dispatcher
                if (_dispatcherQueue != null)
                {
                    var success = _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        try
                        {
                            IncomingMessages.Insert(0, incomingMessage); // Add to top for latest first
                            
                            // Trigger message blink
                            MessageReceived?.Invoke(this, EventArgs.Empty);
                        }
                        catch
                        {
                            // Silently handle UI update errors
                        }
                    });
                    
                    if (!success)
                    {
                        // Fallback if dispatcher failed
                        try
                        {
                            IncomingMessages.Insert(0, incomingMessage);
                            MessageReceived?.Invoke(this, EventArgs.Empty);
                        }
                        catch
                        {
                            // Silently handle fallback errors
                        }
                    }
                }
                else
                {
                    // No dispatcher available, try direct add
                    IncomingMessages.Insert(0, incomingMessage);
                    MessageReceived?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                // Silently handle all errors
            }
        }

        public void ClearMessages()
        {
            IncomingMessages.Clear();
            SelectedMessage = null;
        }

        private void OnServiceListenerMessageReceived(object? sender, ListenerMessageEventArgs e)
        {
            try
            {
                // Trigger listener-specific event for UI blinking
                ListenerMessageReceived?.Invoke(this, e);
                
                AddMessage(e.Source, e.Message);
            }
            catch
            {
                // Try to call AddMessage directly without the UI event
                try
                {
                    AddMessage(e.Source, e.Message);
                }
                catch
                {
                    // Silently handle all failures
                }
            }
        }

        public bool IsListenerUnique(BaseListener newListener)
        {
            return _listenerService?.IsListenerUnique(newListener) ?? false;
        }
    }
}
