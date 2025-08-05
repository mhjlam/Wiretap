using System;
using Wiretap.Models;
using Wiretap.ViewModels;

namespace Wiretap.Services
{
    public interface IListenerService
    {
        event EventHandler<ListenerMessageEventArgs>? ListenerMessageReceived;
        
        void AddListener(BaseListener listener);
        void RemoveListener(BaseListener listener);
        void StartListener(BaseListener listener);
        void StopListener(BaseListener listener);
        void PauseAllListeners();
        void ResumeAllListeners();
        void TogglePauseResume();
        bool IsListenerUnique(BaseListener listener);
    }
}