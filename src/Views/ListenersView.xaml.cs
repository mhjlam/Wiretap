using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Wiretap.Controls;
using Wiretap.Helpers;
using Wiretap.Models;
using Wiretap.Services;
using Wiretap.ViewModels;

namespace Wiretap.Views
{
    public sealed partial class ListenersView : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(MainWindowViewModel), typeof(ListenersView), null);

        public MainWindowViewModel ViewModel
        {
            get => (MainWindowViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private IVisualEffectsService? _visualEffectsService;

        // Events for communication with parent window
        public event EventHandler? AddListenerRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler<BaseListener>? RemoveListenerRequested;
        public event EventHandler<BaseListener>? ToggleListenerRequested;
        public event EventHandler? PlayStopRequested;

        public ListenersView()
        {
            this.InitializeComponent();
            this.Loaded += ListenersView_Loaded;
        }

        private void ListenersView_Loaded(object sender, RoutedEventArgs e)
        {
            // Get visual effects service from service container
            _visualEffectsService = ServiceContainer.Instance.VisualEffectsService;
            
            if (_visualEffectsService is VisualEffectsService visualService)
            {
                visualService.SetListenersListView(ListenersListView);
            }

            UpdatePlayStopButton();
            
            // Subscribe to ViewModel events if available
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsPaused))
            {
                UpdatePlayStopButton();
            }
        }

        private void UpdatePlayStopButton()
        {
            if (PlayStopButton != null && ViewModel != null)
            {
                PlayStopButton.IsPlaying = !ViewModel.IsPaused;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddListenerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void RemoveListenerButton_Click(object sender, RoutedEventArgs e)
        {
            BaseListener? listener = null;
            
            if (sender is CloseButton closeButton && closeButton.Tag is BaseListener closeListener)
            {
                listener = closeListener;
            }
            else if (sender is Button button && button.Tag is BaseListener buttonListener)
            {
                listener = buttonListener;
            }

            if (listener != null)
            {
                RemoveListenerRequested?.Invoke(this, listener);
            }
        }

        private void ToggleListenerButton_Click(object sender, RoutedEventArgs e)
        {
            BaseListener? listener = null;
            
            if (sender is PlayStopButton playStopButton && playStopButton.Tag is BaseListener playStopListener)
            {
                listener = playStopListener;
            }
            else if (sender is Button button && button.Tag is BaseListener buttonListener)
            {
                listener = buttonListener;
            }

            if (listener != null)
            {
                ToggleListenerRequested?.Invoke(this, listener);
            }
        }

        private void PlayStopButton_Click(object sender, RoutedEventArgs e)
        {
            PlayStopRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ListenerBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = ThemeHelper.SubtleFillColorSecondaryBrush;
            }
        }

        private void ListenerBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = ThemeHelper.LayerFillColorDefaultBrush;
            }
        }

        // Simple event handlers
        private void ListenersListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e) { }
        private void ListenersListView_DragItemsCompleted(object sender, DragItemsCompletedEventArgs e) { }
    }
}