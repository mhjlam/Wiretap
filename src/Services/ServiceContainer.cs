using Microsoft.UI.Dispatching;
using System;
using Wiretap.ViewModels;

namespace Wiretap.Services
{
    /// <summary>
    /// Simple service container for dependency injection
    /// </summary>
    public class ServiceContainer
    {
        private static ServiceContainer? _instance;
        private static readonly object _lock = new object();

        public static ServiceContainer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ServiceContainer();
                    }
                }
                return _instance;
            }
        }

        public IMessageService? MessageService { get; private set; }
        public IListenerService? ListenerService { get; private set; }
        public IVisualEffectsService? VisualEffectsService { get; private set; }
        public IDialogService? DialogService { get; private set; }
        public ISettingsService? SettingsService { get; private set; }

        private ServiceContainer() { }

        public void Initialize(MainWindowViewModel viewModel, DispatcherQueue dispatcherQueue)
        {
            MessageService = new MessageService(viewModel, dispatcherQueue);
            ListenerService = new ListenerService(viewModel.ActiveListeners);
            VisualEffectsService = new VisualEffectsService(dispatcherQueue);
            DialogService = new DialogService();
            SettingsService = new SettingsService();

            // Wire up the services to the view model
            viewModel.SetListenerService(ListenerService);
            
            // Add default listener after services are initialized
            try
            {
                viewModel.AddDefaultListener();
            }
            catch
            {
                // Silently handle errors adding default listener
            }
        }

        public void Reset()
        {
            // Dispose services if they implement IDisposable
            if (MessageService is IDisposable msgDisposable)
            { 
                msgDisposable.Dispose();
            }

            if (VisualEffectsService is IDisposable visualDisposable)
            {
                visualDisposable.Dispose();
            }

            MessageService = null;
            ListenerService = null;
            VisualEffectsService = null;
            DialogService = null;
            SettingsService = null;
        }
    }
}