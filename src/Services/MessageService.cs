using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Threading;
using Wiretap.Helpers;
using Wiretap.Models;
using Wiretap.ViewModels;

namespace Wiretap.Services
{
    public class MessageService : IMessageService
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly Dictionary<IncomingMessage, Timer> _messageFlashTimers = [];
        private readonly Dictionary<IncomingMessage, Storyboard> _messageFadeAnimations = [];
        private ListView? _messagesListView;

        public event EventHandler? MessageReceived;

        public MessageService(MainWindowViewModel viewModel, DispatcherQueue dispatcherQueue)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        }

        public void SetUIReferences(ListView messagesListView)
        {
            _messagesListView = messagesListView;
        }

        public void AddMessage(string source, string message)
        {
            var incomingMessage = new IncomingMessage(source, message);
            
            try
            {
                if (_dispatcherQueue.HasThreadAccess)
                {
                    _viewModel.IncomingMessages.Insert(0, incomingMessage);
                    MessageReceived?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        try
                        {
                            _viewModel.IncomingMessages.Insert(0, incomingMessage);
                            MessageReceived?.Invoke(this, EventArgs.Empty);
                        }
                        catch
                        {
                            // Silently handle UI update errors
                        }
                    });
                }
            }
            catch
            {
                // Silently handle all errors
            }
        }

        public void ClearMessages()
        {
            // Clear any active flash timers
            foreach (var timer in _messageFlashTimers.Values)
            {
                timer?.Dispose();
            }
            _messageFlashTimers.Clear();
            
            // Clear any active fade animations
            foreach (var animation in _messageFadeAnimations.Values)
            {
                animation?.Stop();
            }
            _messageFadeAnimations.Clear();
            
            _viewModel.ClearMessages();
        }

        public void StartMessageFlash(IncomingMessage message)
        {
            // Clean up any existing timer or animation for this message
            if (_messageFlashTimers.TryGetValue(message, out var existingTimer))
            {
                existingTimer.Dispose();
                _messageFlashTimers.Remove(message);
            }
            
            if (_messageFadeAnimations.TryGetValue(message, out var existingAnimation))
            {
                existingAnimation.Stop();
                _messageFadeAnimations.Remove(message);
            }
            
            // Set the message as selected and show the modern flash effect
            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                _viewModel.SelectedMessage = message;
                StartModernFlashEffect(message);
            });
        }

        private void StartModernFlashEffect(IncomingMessage message)
        {
            if (_messagesListView == null) return;

            try
            {
                // Find the message border to animate
                Border? targetBorder = null;
                for (int i = 0; i < _viewModel.IncomingMessages.Count; i++)
                {
                    if (_viewModel.IncomingMessages[i] == message)
                    {
                        var container = _messagesListView.ContainerFromIndex(i) as ListViewItem;
                        if (container != null)
                        {
                            targetBorder = FindChildByName(container, "MessageBorder") as Border;
                            break;
                        }
                    }
                }
                
                if (targetBorder != null)
                {
                    // Create modern subtle flash effect with just pulse animation - no border color change
                    var storyboard = new Storyboard();
                    
                    // Scale animation for subtle pulse effect
                    var scaleTransform = new ScaleTransform { CenterX = 0.5, CenterY = 0.5 };
                    targetBorder.RenderTransform = scaleTransform;
                    
                    var scaleXAnimation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 1.04, // Slightly more pronounced pulse
                        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                        AutoReverse = true,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };
                    
                    var scaleYAnimation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 1.04, // Slightly more pronounced pulse
                        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                        AutoReverse = true,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };
                    
                    Storyboard.SetTarget(scaleXAnimation, scaleTransform);
                    Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");
                    Storyboard.SetTarget(scaleYAnimation, scaleTransform);
                    Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");
                    
                    storyboard.Children.Add(scaleXAnimation);
                    storyboard.Children.Add(scaleYAnimation);
                    
                    // Handle completion
                    storyboard.Completed += (s, e) =>
                    {
                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // Reset the transform
                                targetBorder.RenderTransform = null;
                                
                                // Deselect the message
                                _viewModel.SelectedMessage = null;
                                if (_messagesListView.SelectedItem != null)
                                {
                                    _messagesListView.SelectedItem = null;
                                }
                                
                                // Clean up
                                _messageFadeAnimations.Remove(message);
                                if (_messageFlashTimers.TryGetValue(message, out var timer))
                                {
                                    timer.Dispose();
                                    _messageFlashTimers.Remove(message);
                                }
                            }
                            catch
                            {
                                // Silently handle UI update errors
                            }
                        });
                    };
                    
                    // Store and start the animation
                    _messageFadeAnimations[message] = storyboard;
                    storyboard.Begin();
                }
                else
                {
                    // Fallback - simple timer-based deselection
                    var fallbackTimer = new Timer((_) =>
                    {
                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                _viewModel.SelectedMessage = null;
                                if (_messagesListView.SelectedItem != null)
                                {
                                    _messagesListView.SelectedItem = null;
                                }
                
                                if (_messageFlashTimers.TryGetValue(message, out var timer))
                                {
                                    timer.Dispose();
                                    _messageFlashTimers.Remove(message);
                                }
                            }
                            catch
                            {
                                // Silently handle UI update errors
                            }
                        });
                    }, null, 400, Timeout.Infinite); // Match the animation duration
                    
                    _messageFlashTimers[message] = fallbackTimer;
                }
            }
            catch
            {
                // Silently handle animation errors
            }
        }

        public void UpdateMessageBorderStates()
        {
            if (_messagesListView == null) return;

            _dispatcherQueue.TryEnqueue(() =>
            {
                // Enumerate through all ListViewItems and update their border states
                for (int i = 0; i < _viewModel.IncomingMessages.Count; i++)
                {
                    var container = _messagesListView.ContainerFromIndex(i) as ListViewItem;
                    if (container != null)
                    {
                        var border = FindChildByName(container, "MessageBorder") as Border;
                        if (border != null)
                        {
                            var messageData = border.DataContext as IncomingMessage;
                            
                            // Maintain consistent border thickness to prevent layout shifts
                            if (messageData == _viewModel.SelectedMessage)
                            {
                                border.BorderBrush = ThemeHelper.AccentFillColorDefaultBrush;
                            }
                            else
                            {
                                border.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                            }
                            border.Background = ThemeHelper.LayerFillColorDefaultBrush;
                        }
                    }
                }
            });
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
            foreach (var timer in _messageFlashTimers.Values)
            {
                timer?.Dispose();
            }
            _messageFlashTimers.Clear();
            
            foreach (var animation in _messageFadeAnimations.Values)
            {
                animation?.Stop();
            }
            _messageFadeAnimations.Clear();
        }
    }
}