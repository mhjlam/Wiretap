using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wiretap.Models;

namespace Wiretap.Models.Listeners
{
    public class TcpListener : NetworkListenerBase
    {
        private int _port = 9090; // Changed default to avoid conflict with UDP
        private readonly string _bindAddress = "127.0.0.1";
        private System.Net.Sockets.TcpListener? _tcpListener;

        public override ListenerProtocol Protocol => ListenerProtocol.TCP;

        public TcpListener()
        {
            // Generate initial name after port is set
            OnNamePropertyChanged();
        }

        [Required]
        [Range(1, 65535)]
        [Display(Name = "Port", Description = "TCP port to listen on")]
        public int Port
        {
            get => _port;
            set
            {
                if (_port != value)
                {
                    _port = value;
                    OnPropertyChanged(nameof(Port));
                    OnPropertyChanged(nameof(DisplayInfo));
                    OnNamePropertyChanged(); // Update name when port changes
                }
            }
        }

        // Remove the setter to make it read-only (not editable in UI)
        public string BindAddress => _bindAddress;

        protected override string GetIdentifierFromListener() => Port.ToString();
        protected override int GetPortFromListener() => Port;

        public override bool IsValid()
        {
            return Port > 0 && Port <= 65535;
        }

        public override string GetDisplayInfo()
        {
            return $"TCP - {BindAddress}:{Port}";
        }

        protected override async Task StartNetworkListeningAsync()
        {
            await Task.CompletedTask; // Suppress CS1998 warning
            
            // Bind to Any to receive connections to 127.0.0.1, but show 127.0.0.1 in UI
            var endpoint = new IPEndPoint(IPAddress.Any, Port);
            
            _tcpListener = new System.Net.Sockets.TcpListener(endpoint);
            _tcpListener.Start(); // Use default backlog
        }

        protected override async Task StopNetworkResourcesAsync()
        {
            if (_tcpListener != null)
            {
                try
                {
                    // Use a background task to stop the TCP listener to avoid blocking
                    await Task.Run(() =>
                    {
                        try
                        {
                            _tcpListener.Stop();
                        }
                        catch (SocketException)
                        {
                            // Expected - socket may be in use
                        }
                        catch (ObjectDisposedException)
                        {
                            // Expected - may already be disposed
                        }
                    });
                }
                catch
                {
                    // Ignore cleanup errors
                }
                finally
                {
                    _tcpListener = null;
                }
            }
        }

        protected override async Task ListenForDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _tcpListener != null && IsListening)
                {
                    try
                    {
                        var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                        
                        // Safely get client endpoint
                        string clientEndpoint;
                        try
                        {
                            clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                        }
                        catch
                        {
                            clientEndpoint = "Unknown";
                        }
                        
                        // Show connection established message if enabled
                        if (ShouldShowConnectionStatus())
                        {
                            OnMessageReceived($"Client connected ({clientEndpoint})");
                        }
                        
                        // Handle client in separate task - don't await to allow multiple concurrent connections
                        _ = Task.Run(async () => await HandleClientAsync(tcpClient, cancellationToken))
                            .ContinueWith(task =>
                            {
                                if (task.IsFaulted && !cancellationToken.IsCancellationRequested)
                                {
                                    OnMessageReceived($"Unhandled client task error: {task.Exception?.GetBaseException().Message}");
                                }
                            }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Socket was disposed - exit gracefully
                        break;
                    }
                    catch (SocketException) when (cancellationToken.IsCancellationRequested || !IsListening)
                    {
                        // Expected during shutdown
                        break;
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsListening)
                    {
                        // Log the error but continue listening for new connections
                        OnMessageReceived($"Error accepting client connection: {ex.Message}");
                        
                        // Brief delay to prevent tight error loops
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested || !IsListening)
            {
                // Expected during shutdown
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested || !IsListening)
            {
                // Expected during shutdown
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsListening)
            {
                Status = ListenerStatus.Error;
                OnMessageReceived($"TCP Listener error: {ex.Message}");
                
                // Try to restart the listener after a delay
                await Task.Delay(5000, cancellationToken);
                if (!cancellationToken.IsCancellationRequested && IsListening)
                {
                    OnMessageReceived("Attempting to restart TCP listener...");
                    _ = Task.Run(async () => await ListenForDataAsync(cancellationToken));
                }
            }
            finally
            {
                // Only update status if we're still supposed to be the active instance
                if (IsListening)
                {
                    IsListening = false;
                    Status = ListenerStatus.Disabled;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string? clientEndpoint = null;
            try
            {
                // Store the remote endpoint early while the connection is still valid
                try
                {
                    clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                }
                catch
                {
                    clientEndpoint = "Unknown";
                }
                
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                {
                    string? line;
                    while (!cancellationToken.IsCancellationRequested && 
                           client.Connected && 
                           (line = await reader.ReadLineAsync()) != null)
                    {
                        // Send message with endpoint as source info
                        OnMessageReceived($"from {clientEndpoint}|{line}");
                    }
                }
                
                // Show disconnection message if enabled and connection ended gracefully
                if (ShouldShowConnectionStatus() && !cancellationToken.IsCancellationRequested)
                {
                    OnMessageReceived($"Client disconnected ({clientEndpoint})");
                }
            }
            catch (IOException ioEx) when (!cancellationToken.IsCancellationRequested)
            {
                // Network I/O errors are common when clients disconnect abruptly
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"Client connection lost ({clientEndpoint}): {ioEx.Message}");
                }
            }
            catch (SocketException sockEx) when (!cancellationToken.IsCancellationRequested)
            {
                // Socket errors are also common during disconnections
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"Client socket error ({clientEndpoint}): {sockEx.Message}");
                }
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // This is expected during shutdown, don't log it
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Any other unexpected errors
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"Client error ({clientEndpoint}): {ex.Message}");
                }
            }
        }
    }
}