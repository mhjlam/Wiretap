using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Wiretap.Helpers;
using Wiretap.Models;
using Wiretap.Services;
using Wiretap.ViewModels;

namespace Wiretap.Views
{
    public sealed partial class MessagesView : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(MainWindowViewModel), typeof(MessagesView), null);

        public MainWindowViewModel ViewModel
        {
            get => (MainWindowViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private bool _userScrolled = false;
        private IMessageService? _messageService;

        // Events for communication with parent window
        public event EventHandler? ScrollToTopRequested;
        public event EventHandler? ClearMessagesRequested;
        public event EventHandler<IncomingMessage>? MessageSelected;

        public MessagesView()
        {
            this.InitializeComponent();
            this.Loaded += MessagesView_Loaded;
        }

        private void MessagesView_Loaded(object sender, RoutedEventArgs e)
        {
            // Get message service from service container
            _messageService = ServiceContainer.Instance.MessageService;
            
            if (_messageService is MessageService msgService)
            {
                msgService.SetUIReferences(MessagesListView);
            }

            // Set up visual effects service with status indicator reference
            var visualEffectsService = ServiceContainer.Instance.VisualEffectsService;
            if (visualEffectsService is VisualEffectsService visualService)
            {
                visualService.SetStatusIndicator(StatusIndicator);
            }

            AttachScrollViewerEvents();
            
            // Subscribe to ViewModel events if available
            if (ViewModel != null)
            {
                ViewModel.IncomingMessages.CollectionChanged += IncomingMessages_CollectionChanged;
            }
        }

        public void SetStatusIndicator(Microsoft.UI.Xaml.Shapes.Ellipse statusIndicator)
        {
            // This method allows external setting of status indicator reference
            // for blinking effects
        }

        private void MessagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var currentSelection = MessagesListView.SelectedItem as IncomingMessage;
            
            if (currentSelection != null)
            {
                // Copy message content to clipboard
                try
                {
                    var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetText(currentSelection.Message);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                }
                catch
                {
                    // Silently handle clipboard errors
                }
                
                // Notify parent and start flash effect
                MessageSelected?.Invoke(this, currentSelection);
                _messageService?.StartMessageFlash(currentSelection);
            }
        }

        private void ScrollToTop_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.IncomingMessages.Count > 0)
            {
                var newestMessage = ViewModel.IncomingMessages[0];
                MessagesListView.ScrollIntoView(newestMessage, ScrollIntoViewAlignment.Leading);
            }
            _userScrolled = false;
            ScrollToTopRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ClearMessages_Click(object sender, RoutedEventArgs e)
        {
            _userScrolled = false;
            _messageService?.ClearMessages();
            
            if (MessagesListView.SelectedItem != null)
            {
                MessagesListView.SelectedItem = null;
            }
            
            ClearMessagesRequested?.Invoke(this, EventArgs.Empty);
        }

        private void MessageContent_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                var messageData = grid.DataContext as IncomingMessage;
                if (messageData != ViewModel?.SelectedMessage)
                {
                    grid.Background = ThemeHelper.SubtleFillColorSecondaryBrush;
                }
            }
        }

        private void MessageContent_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.Background = ThemeHelper.Transparent;
            }
        }

        private void IncomingMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && !_userScrolled)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (ViewModel?.IncomingMessages.Count > 0)
                        {
                            var newestMessage = ViewModel.IncomingMessages[0];
                            MessagesListView.ScrollIntoView(newestMessage, ScrollIntoViewAlignment.Leading);
                        }
                    }
                    catch
                    {
                        // Silently handle scroll errors
                    }
                });
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                _userScrolled = false;
                if (ViewModel != null)
                {
                    ViewModel.SelectedMessage = null;
                }
            }
        }

        private void AttachScrollViewerEvents()
        {
            var scrollViewer = FindChildOfType<ScrollViewer>(MessagesListView);
            if (scrollViewer != null && scrollViewer.Tag == null)
            {
                scrollViewer.Tag = "EventsAttached";
                scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            }
        }

        private void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!e.IsIntermediate)
            {
                _userScrolled = true;
            }
        }

        private T? FindChildOfType<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }

                var descendant = FindChildOfType<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        // Simple event handlers
        private void MessagesListView_Tapped(object sender, TappedRoutedEventArgs e) { }
        private void MessagesListView_KeyDown(object sender, KeyRoutedEventArgs e) { }
        private void MessagesListView_RightTapped(object sender, RightTappedRoutedEventArgs e) 
        { 
            e.Handled = true; 
        }
        private void MessagesListView_Loaded(object sender, RoutedEventArgs e) 
        { 
            AttachScrollViewerEvents(); 
        }
    }
}