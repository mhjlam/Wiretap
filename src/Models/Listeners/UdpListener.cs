using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wiretap.Models;

namespace Wiretap.Models.Listeners
{
    public class UdpListener : NetworkListenerBase
    {
        private int _port = 8080;
        private readonly string _bindAddress = "127.0.0.1";
        private UdpClient? _udpClient;

        public override ListenerProtocol Protocol => ListenerProtocol.UDP;

        public UdpListener()
        {
            // Generate initial name after port is set
            OnNamePropertyChanged();
        }

        [Required]
        [Range(1, 65535)]
        [Display(Name = "Port", Description = "UDP port to listen on")]
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
            return $"UDP - {BindAddress}:{Port}";
        }

        protected override async Task StartNetworkListeningAsync()
        {
            await Task.CompletedTask; // Suppress CS1998 warning
            
            // Bind to the specific address shown in the UI
            var endpoint = new IPEndPoint(IPAddress.Parse(BindAddress), Port);
            _udpClient = new UdpClient(endpoint);
        }

        protected override async Task StopNetworkResourcesAsync()
        {
            if (_udpClient != null)
            {
                try
                {
                    // Use a background task to close the UDP client to avoid blocking
                    await Task.Run(() =>
                    {
                        try
                        {
                            _udpClient.Close();
                        }
                        catch (SocketException)
                        {
                            // Expected - socket may be in use
                        }
                        catch (ObjectDisposedException)
                        {
                            // Expected - may already be disposed
                        }
                        
                        try
                        {
                            _udpClient.Dispose();
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
                    _udpClient = null;
                }
            }
        }

        protected override async Task ListenForDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null && IsListening)
                {
                    try
                    {
                        // Check for cancellation before each receive operation
                        if (cancellationToken.IsCancellationRequested || !IsListening)
                        {
                            break;
                        }
                        
                        // Directly use ReceiveAsync - let socket exceptions handle the cancellation
                        var result = await _udpClient.ReceiveAsync();
                        var message = Encoding.UTF8.GetString(result.Buffer);
                        
                        // Send message with endpoint as source info
                        OnMessageReceived($"from {result.RemoteEndPoint}|{message}");
                    }
                    catch (ObjectDisposedException)
                    {
                        // Socket was disposed - exit gracefully
                        break;
                    }
                    catch (SocketException sockEx)
                    {
                        // Check if this is due to cancellation/shutdown
                        if (cancellationToken.IsCancellationRequested || !IsListening)
                        {
                            // Expected during shutdown
                            break;
                        }
                        else
                        {
                            // Actual socket error during normal operation
                            Status = ListenerStatus.Error;
                            OnMessageReceived($"UDP Socket Error: {sockEx.Message}");
                            break;
                        }
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsListening)
                    {
                        // For other exceptions, log but continue listening if not shutting down
                        OnMessageReceived($"UDP Error: {ex.Message}");
                        // Brief delay to prevent tight error loops
                        try
                        {
                            await Task.Delay(1000, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsListening)
            {
                Status = ListenerStatus.Error;
                var errorMessage = $"UDP Error: {ex.Message}";
                try
                {
                    OnMessageReceived(errorMessage);
                }
                catch
                {
                    // Silently handle error message failures
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
    }
}