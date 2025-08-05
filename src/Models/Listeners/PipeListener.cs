using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wiretap.Models.Listeners
{
    public class PipeListener : BaseListener
    {
        private string _pipeName = "MyPipe";
        private List<NamedPipeServerStream>? _pipeServers;
        private CancellationTokenSource? _cancellationTokenSource;

        public override ListenerProtocol Protocol => ListenerProtocol.Pipe;

        public PipeListener()
        {
            // Generate initial name after pipe name is set
            OnNamePropertyChanged();
        }

        [Required]
        [Display(Name = "Pipe Name", Description = "Name of the named pipe")]
        public string PipeName
        {
            get => _pipeName;
            set
            {
                if (_pipeName != value)
                {
                    _pipeName = value;
                    OnPropertyChanged(nameof(PipeName));
                    OnPropertyChanged(nameof(DisplayInfo));
                    OnNamePropertyChanged(); // Update name when pipe name changes
                }
            }
        }

        protected override string GetIdentifierFromListener() => PipeName;

        public override bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(PipeName);
        }

        public override string GetDisplayInfo()
        {
            return $"Pipe - \\\\.\\pipe\\{PipeName}";
        }

        public override async Task<ListenerOperationResult> StartListeningAsync()
        {
            if (IsListening) 
                return ListenerOperationResult.CreateSuccess();

            await Task.CompletedTask; // Suppress CS1998 warning

            try
            {
                _pipeServers = new List<NamedPipeServerStream>();
                _cancellationTokenSource = new CancellationTokenSource();
                
                IsListening = true;
                Status = ListenerStatus.Enabled;
                
                // Start listening for connections continuously
                _ = Task.Run(async () => await StartPipeListeningAsync(_cancellationTokenSource.Token));
                
                return ListenerOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                Status = ListenerStatus.Error;
                return ListenerOperationResult.CreateFailure($"Failed to start Pipe listener: {ex.Message}", ex);
            }
        }

        public override async Task<ListenerOperationResult> StopListeningAsync()
        {
            if (!IsListening) 
                return ListenerOperationResult.CreateSuccess();

            await Task.CompletedTask; // Suppress CS1998 warning

            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_pipeServers != null)
                {
                    foreach (var pipe in _pipeServers.ToList())
                    {
                        try
                        {
                            if (pipe.IsConnected)
                            {
                                pipe.Disconnect();
                            }
                            pipe.Close();
                            pipe.Dispose();
                        }
                        catch
                        {
                            // Silently handle pipe cleanup errors
                        }
                    }
                    _pipeServers.Clear();
                    _pipeServers = null;
                }
                
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                IsListening = false;
                Status = ListenerStatus.Disabled;
                return ListenerOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                Status = ListenerStatus.Error;
                return ListenerOperationResult.CreateFailure($"Failed to stop Pipe listener: {ex.Message}", ex);
            }
        }

        private async Task StartPipeListeningAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Create a new pipe server instance - use just the pipe name, not the full path
                    var pipeServer = new NamedPipeServerStream(
                        PipeName, 
                        PipeDirection.InOut, 
                        1, // Maximum 1 instance at a time
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    
                    _pipeServers?.Add(pipeServer);
                    
                    // Wait for a client to connect
                    await WaitForConnectionAsync(pipeServer, cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    Status = ListenerStatus.Error;
                    OnMessageReceived($"Pipe listener error: {ex.Message}");
                    
                    // Wait a bit before trying to create a new pipe instance
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task WaitForConnectionAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
        {
            try
            {
                // Wait for a client to connect
                await pipeServer.WaitForConnectionAsync(cancellationToken);
                
                // Show connection established message if enabled
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"Pipe client connected to \\\\.\\.\\pipe\\{PipeName}");
                }
                
                // Handle the connected client in a separate task
                await HandlePipeClientAsync(pipeServer, cancellationToken);
                
                // Show disconnection message if enabled
                if (ShouldShowConnectionStatus() && !cancellationToken.IsCancellationRequested)
                {
                    OnMessageReceived($"Pipe client disconnected from \\\\.\\.\\pipe\\{PipeName}");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"Pipe connection error: {ex.Message}");
                }
            }
            finally
            {
                // Clean up this pipe server instance
                try
                {
                    if (pipeServer.IsConnected)
                    {
                        pipeServer.Disconnect();
                    }
                    pipeServer.Dispose();
                    _pipeServers?.Remove(pipeServer);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cleaning up pipe: {ex.Message}");
                }
            }
        }

        private async Task HandlePipeClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
        {
            try
            {
                using (var reader = new StreamReader(pipeServer))
                {
                    string? line;
                    while (!cancellationToken.IsCancellationRequested && pipeServer.IsConnected &&
                           (line = await reader.ReadLineAsync(cancellationToken)) != null)
                    {
                        // Send message with pipe info as source info
                        OnMessageReceived($"pipe {PipeName}|{line}");
                    }
                }
            }
            catch (IOException ioEx) when (!cancellationToken.IsCancellationRequested)
            {
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"Pipe client connection lost: {ioEx.Message}");
                }
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"Pipe client error: {ex.Message}");
                }
            }
        }
    }
}