using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Wiretap.Models;
using Wiretap.Services;
using Wiretap.ViewModels;

namespace Wiretap
{
    public sealed partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel { get; }
        private AppWindow? _appWindow;

        // Services
        private IMessageService? _messageService;
        private IVisualEffectsService? _visualEffectsService;
        private IDialogService? _dialogService;

        public MainWindow()
        {
            InitializeComponent();
            
            ViewModel = new MainWindowViewModel();
            ViewModel.SetDispatcher(DispatcherQueue);
            
            // Initialize services
            InitializeServices();
            
            // Subscribe to ViewModel events
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.MessageReceived += OnMessageReceived;
            ViewModel.ListenerMessageReceived += OnListenerMessageReceived;

            // Set up window
            SetupWindow();

            // Enable keyboard events on the main grid
            Content.Focus(FocusState.Programmatic);
        }

        private void InitializeServices()
        {
            // Initialize service container
            ServiceContainer.Instance.Initialize(ViewModel, DispatcherQueue);
            
            _messageService = ServiceContainer.Instance.MessageService;
            _visualEffectsService = ServiceContainer.Instance.VisualEffectsService;
            _dialogService = ServiceContainer.Instance.DialogService;

            // Subscribe to service events
            if (_messageService != null)
            {
                _messageService.MessageReceived += OnMessageReceived;
            }
        }

        private void SetupWindow()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            
            // Center the window on screen
            CenterWindowOnScreen();
            
            _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 1280;
                presenter.PreferredMinimumHeight = 720;
            }
        }

        private void CenterWindowOnScreen()
        {
            if (_appWindow == null)
            {
                return;
            }

            // Get the display area of the current monitor
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            
            // Calculate center position
            var windowWidth = 1280;
            var windowHeight = 720;
            var x = (workArea.Width - windowWidth) / 2 + workArea.X;
            var y = (workArea.Height - windowHeight) / 2 + workArea.Y;
            
            // Position and resize the window
            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, windowWidth, windowHeight));
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // ViewModel property changes are now handled by individual views
        }

        private void OnMessageReceived(object? sender, EventArgs e)
        {
            _visualEffectsService?.StartStatusBlink();
        }

        private void OnListenerMessageReceived(object? sender, ListenerMessageEventArgs e)
        {
            _visualEffectsService?.StartListenerBlink(e.Listener);
        }

        // Event handlers for view communication
        private async void OnAddListenerRequested(object? sender, EventArgs e)
        {
            if (_dialogService == null)
            {
                return;
            }

            var result = await _dialogService.ShowAddListenerDialogAsync(this.Content.XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                var listener = (_dialogService as DialogService)?.LastCreatedListener;
                if (listener != null)
                {
                    ViewModel.AddListener(listener);
                }
            }
        }

        private async void OnSettingsRequested(object? sender, EventArgs e)
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowSettingsDialogAsync(this);
            }
        }

        private async void OnRemoveListenerRequested(object? sender, BaseListener listener)
        {
            if (_dialogService == null || listener == null)
            {
                return;
            }

            var result = await _dialogService.ShowRemoveListenerConfirmationAsync(listener.Name, this.Content.XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.RemoveListener(listener);
            }
        }

        private void OnToggleListenerRequested(object? sender, BaseListener listener)
        {
            if (listener != null)
            {
                if (listener.IsListening)
                {
                    ViewModel.StopListener(listener);
                }
                else
                {
                    ViewModel.StartListener(listener);
                }
            }
        }

        private void OnPlayStopRequested(object? sender, EventArgs e)
        {
            ViewModel.TogglePauseResume();
        }

        private void OnScrollToTopRequested(object? sender, EventArgs e)
        {
            // Handled by MessagesView
        }

        private void OnClearMessagesRequested(object? sender, EventArgs e)
        {
            // Handled by MessagesView
        }

        private void OnMessageSelected(object? sender, IncomingMessage message)
        {
            // Additional handling for message selection if needed
        }

        // Simple event handlers that don't need implementation
        private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e) { }
    }
}
