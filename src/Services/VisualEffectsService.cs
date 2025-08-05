using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wiretap.Helpers;
using Wiretap.Models;

namespace Wiretap.Services
{
    public class VisualEffectsService : IVisualEffectsService
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly Dictionary<BaseListener, Timer> _listenerBlinkTimers = new();
        private Timer? _statusBlinkTimer;
        private bool _isStatusBlinking = false;
        
        // References to UI elements - these would be set by the views
        private Ellipse? _statusIndicator;
        private ListView? _listenersListView;

        public VisualEffectsService(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        }

        public void SetStatusIndicator(Ellipse statusIndicator)
        {
            _statusIndicator = statusIndicator;
        }

        public void SetListenersListView(ListView listenersListView)
        {
            _listenersListView = listenersListView;
        }

        public void StartStatusBlink()
        {
            if (_isStatusBlinking || _statusIndicator == null) return;
            
            _isStatusBlinking = true;
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (_statusIndicator != null)
                {
                    _statusIndicator.Fill = new SolidColorBrush(Colors.Green);
                }
            });
            
            _statusBlinkTimer = new Timer((_) =>
            {
                try
                {
                    _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        try
                        {
                            if (_statusIndicator != null)
                            {
                                _statusIndicator.Fill = new SolidColorBrush(Colors.Red);
                            }
                            _statusBlinkTimer?.Dispose();
                            _statusBlinkTimer = null;
                            _isStatusBlinking = false;
                        }
                        catch
                        {
                            _isStatusBlinking = false;
                        }
                    });
                }
                catch
                {
                    _isStatusBlinking = false;
                }
            }, null, 100, Timeout.Infinite);
        }

        public void StartListenerBlink(BaseListener listener)
        {
            if (_listenersListView == null) return;
            
            if (_listenerBlinkTimers.TryGetValue(listener, out var existingTimer))
            {
                existingTimer.Dispose();
                _listenerBlinkTimers.Remove(listener);
            }
            
            // Ensure the initial green flash happens on the UI thread (fast fade-in)
            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                FlashListenerBorder(listener, true);
            });
            
            Timer? timerRef = null;
            timerRef = new Timer((_) =>
            {
                try
                {
                    _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        try
                        {
                            FlashListenerBorder(listener, false);
                            timerRef?.Dispose();
                            _listenerBlinkTimers.Remove(listener);
                        }
                        catch
                        {
                            // Silently handle UI update errors
                        }
                    });
                }
                catch
                {
                    // Silently handle dispatcher errors
                }
            }, null, 150, Timeout.Infinite); // Increased from 100ms to 150ms for slightly longer flash
            _listenerBlinkTimers[listener] = timerRef;
        }

        public void SetStatusIndicator(SolidColorBrush brush)
        {
            if (_statusIndicator == null) return;
            
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (_statusIndicator != null)
                {
                    _statusIndicator.Fill = brush;
                }
            });
        }

        private void FlashListenerBorder(BaseListener listener, bool useGreen)
        {
            if (_listenersListView == null) return;
            
            // Find the listener's container in the ListView
            var activeListeners = (_listenersListView.ItemsSource as System.Collections.ObjectModel.ObservableCollection<BaseListener>);
            if (activeListeners == null) return;

            for (int i = 0; i < activeListeners.Count; i++)
            {
                if (activeListeners[i] == listener)
                {
                    var container = _listenersListView.ContainerFromIndex(i) as ListViewItem;
                    
                    if (container != null)
                    {
                        var border = FindChildByName(container, "ListenerBorder") as Border;
                        
                        if (border != null)
                        {
                            if (useGreen)
                            {
                                border.BorderBrush = new SolidColorBrush(Colors.Green);
                                border.BorderThickness = new Thickness(2);
                            }
                            else
                            {
                                border.BorderBrush = ThemeHelper.CardStrokeColorDefaultBrush;
                                border.BorderThickness = new Thickness(1);
                            }
                        }
                    }
                    else
                    {
                        // Try to update layout and try again
                        _listenersListView.UpdateLayout();
                        
                        container = _listenersListView.ContainerFromIndex(i) as ListViewItem;
                        if (container != null)
                        {
                            var border = FindChildByName(container, "ListenerBorder") as Border;
                            if (border != null)
                            {
                                if (useGreen)
                                {
                                    border.BorderBrush = new SolidColorBrush(Colors.Green);
                                    border.BorderThickness = new Thickness(2);
                                }
                                else
                                {
                                    border.BorderBrush = ThemeHelper.CardStrokeColorDefaultBrush;
                                    border.BorderThickness = new Thickness(1);
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }

        private DependencyObject? FindChildByName(DependencyObject parent, string name)
        {
            if (parent == null) return null;

            for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement element && element.Name == name)
                {
                    return child;
                }

                var result = FindChildByName(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public void Dispose()
        {
            _statusBlinkTimer?.Dispose();
            
            foreach (var timer in _listenerBlinkTimers.Values)
            {
                timer?.Dispose();
            }
            _listenerBlinkTimers.Clear();
        }
    }
}