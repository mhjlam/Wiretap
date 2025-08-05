using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Wiretap.Models;
using Wiretap.Models.Listeners;
using Wiretap.ViewModels;

namespace Wiretap.Services
{
    public class ListenerService : IListenerService
    {
        private readonly ObservableCollection<BaseListener> _activeListeners;
        private bool _isPaused;

        public event EventHandler<ListenerMessageEventArgs>? ListenerMessageReceived;

        public ListenerService(ObservableCollection<BaseListener> activeListeners)
        {
            _activeListeners = activeListeners ?? throw new ArgumentNullException(nameof(activeListeners));
        }

        public void AddListener(BaseListener listener)
        {
            if (listener == null || !listener.IsValid())
            {
                return;
            }

            if (IsListenerUnique(listener))
            {
                listener.MessageReceived += OnListenerMessageReceived;
                _activeListeners.Add(listener);

                if (!_isPaused)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await listener.StartListeningAsync();
                        }
                        catch
                        {
                            // Silently handle listener start errors
                        }
                    });
                }
            }
        }

        public void RemoveListener(BaseListener listener)
        {
            if (listener != null)
            {
                if (listener.IsListening)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await listener.StopListeningAsync();
                        }
                        catch
                        {
                            // Silently handle stop errors
                        }
                    });
                }

                listener.MessageReceived -= OnListenerMessageReceived;
                _activeListeners.Remove(listener);
            }
        }

        public void StartListener(BaseListener listener)
        {
            if (listener == null || listener.IsListening || _isPaused)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await listener.StartListeningAsync();
                }
                catch
                {
                    // Silently handle listener start errors
                }
            });
        }

        public void StopListener(BaseListener listener)
        {
            if (listener == null || !listener.IsListening)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await listener.StopListeningAsync();
                }
                catch
                {
                    // Silently handle stop errors
                }
            });
        }

        public void PauseAllListeners()
        {
            _isPaused = true;

            foreach (var listener in _activeListeners.Where(l => l.IsListening).ToList())
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await listener.StopListeningAsync();
                    }
                    catch
                    {
                        // Silently handle stop errors
                    }
                });
            }
        }

        public void ResumeAllListeners()
        {
            _isPaused = false;

            foreach (var listener in _activeListeners.Where(l => !l.IsListening).ToList())
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await listener.StartListeningAsync();
                    }
                    catch
                    {
                        // Silently handle listener resume errors
                    }
                });
            }
        }

        public void TogglePauseResume()
        {
            if (_isPaused)
            {
                ResumeAllListeners();
            }
            else
            {
                PauseAllListeners();
            }
        }

        public bool IsListenerUnique(BaseListener newListener)
        {
            // Check for name uniqueness (case-insensitive)
            if (_activeListeners.Any(existing => existing.Name.Equals(newListener.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Check for endpoint conflicts
            return !_activeListeners.Any(existing => AreListenersConflicting(existing, newListener));
        }

        private void OnListenerMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            var listener = sender as BaseListener;
            if (listener != null)
            {
                ListenerMessageReceived?.Invoke(this, new ListenerMessageEventArgs(listener, e.Source, e.Message));
            }
        }

        private bool AreListenersConflicting(BaseListener existing, BaseListener newListener)
        {
            if (existing.Protocol != newListener.Protocol)
            {
                return false;
            }

            return existing.Protocol switch
            {
                ListenerProtocol.UDP =>
                    existing is UdpListener existingUdp &&
                    newListener is UdpListener newUdp &&
                    existingUdp.Port == newUdp.Port &&
                    existingUdp.BindAddress.Equals(newUdp.BindAddress, StringComparison.OrdinalIgnoreCase),
                ListenerProtocol.TCP =>
                    existing is TcpListener existingTcp &&
                    newListener is TcpListener newTcp &&
                    existingTcp.Port == newTcp.Port &&
                    existingTcp.BindAddress.Equals(newTcp.BindAddress, StringComparison.OrdinalIgnoreCase),
                ListenerProtocol.COM =>
                    existing is ComListener existingCom &&
                    newListener is ComListener newCom &&
                    existingCom.PortName.Equals(newCom.PortName, StringComparison.OrdinalIgnoreCase),
                ListenerProtocol.Pipe =>
                    existing is PipeListener existingPipe &&
                    newListener is PipeListener newPipe &&
                    existingPipe.PipeName.Equals(newPipe.PipeName, StringComparison.OrdinalIgnoreCase),
                ListenerProtocol.USB =>
                    existing is UsbListener existingUsb &&
                    newListener is UsbListener newUsb &&
                    existingUsb.DeviceId.Equals(newUsb.DeviceId, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}