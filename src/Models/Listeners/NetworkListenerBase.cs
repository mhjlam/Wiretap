using System;
using System.Threading;
using System.Threading.Tasks;
using Wiretap.Models;

namespace Wiretap.Models.Listeners
{
    /// <summary>
    /// Base class for network-based listeners (TCP/UDP) to eliminate duplicate cleanup code
    /// </summary>
    public abstract class NetworkListenerBase : BaseListener
    {
        protected CancellationTokenSource? _cancellationTokenSource;
        
        protected abstract Task StartNetworkListeningAsync();
        protected abstract Task StopNetworkResourcesAsync();
        protected abstract Task ListenForDataAsync(CancellationToken cancellationToken);

        public override async Task<ListenerOperationResult> StartListeningAsync()
        {
            if (IsListening) 
                return ListenerOperationResult.CreateSuccess();

            try
            {
                await StartNetworkListeningAsync();
                _cancellationTokenSource = new CancellationTokenSource();
                
                IsListening = true;
                Status = ListenerStatus.Enabled;
                
                // Start listening in background
                _ = Task.Run(async () => await ListenForDataAsync(_cancellationTokenSource.Token));
                return ListenerOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                Status = ListenerStatus.Error;
                return ListenerOperationResult.CreateFailure($"Failed to start {Protocol} listener: {ex.Message}", ex);
            }
        }

        public override async Task<ListenerOperationResult> StopListeningAsync()
        {
            if (!IsListening) 
                return ListenerOperationResult.CreateSuccess();

            try
            {
                // Signal cancellation to background tasks first
                _cancellationTokenSource?.Cancel();
                
                // Give background tasks time to exit gracefully
                await Task.Delay(200);
                
                // Store reference to avoid null reference during cleanup
                var cancellationSourceToDispose = _cancellationTokenSource;
                
                // Clear references first
                _cancellationTokenSource = null;
                
                // Set status (after background task should have exited)
                IsListening = false;
                Status = ListenerStatus.Disabled;
                
                // Stop network resources
                await StopNetworkResourcesAsync();
                
                // Dispose cancellation token source
                if (cancellationSourceToDispose != null)
                {
                    try
                    {
                        cancellationSourceToDispose.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected if already disposed
                    }
                }
                
                return ListenerOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                Status = ListenerStatus.Error;
                return ListenerOperationResult.CreateFailure($"Failed to stop {Protocol} listener: {ex.Message}", ex);
            }
        }
    }
}