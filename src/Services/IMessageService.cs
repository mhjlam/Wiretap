using System;
using Wiretap.Models;
using Wiretap.ViewModels;

namespace Wiretap.Services
{
    public interface IMessageService
    {
        event EventHandler? MessageReceived;
        
        void AddMessage(string source, string message);
        void ClearMessages();
        void StartMessageFlash(IncomingMessage message);
        void UpdateMessageBorderStates();
    }
}