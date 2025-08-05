using System;
using Wiretap.Common;

namespace Wiretap.Models
{
	public partial class IncomingMessage : NotifyPropertyChangedBase
    {
        private DateTime _timestamp;
        private string _source = string.Empty;
        private string _message = string.Empty;
        private string _sourceInfo = string.Empty;

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
                OnPropertyChanged(nameof(TimestampFormatted));
            }
        }

        public string TimestampFormatted => Timestamp.ToString("HH:mm:ss.fff");

        public string Source
        {
            get => _source;
            set
            {
                _source = value;
                OnPropertyChanged(nameof(Source));
            }
        }

        public string SourceInfo
        {
            get => _sourceInfo;
            set
            {
                _sourceInfo = value;
                OnPropertyChanged(nameof(SourceInfo));
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public IncomingMessage()
        {
            Timestamp = DateTime.Now;
        }

        public IncomingMessage(string source, string message) : this()
        {
            Source = source;
            
            // Parse message to extract source info if it contains the separator
            if (message.Contains('|'))
            {
                var parts = message.Split('|', 2);
                SourceInfo = parts[0];
                Message = parts[1];
            }
            else
            {
                Message = message;
                SourceInfo = string.Empty;
            }
        }
    }
}