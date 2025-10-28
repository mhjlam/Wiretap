using System;

namespace Wiretap.Models
{
    /// <summary>
    /// Represents the result of a listener operation
    /// </summary>
    public class ListenerOperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }

        public static ListenerOperationResult CreateSuccess() => new() { 
            Success = true
        };

        public static ListenerOperationResult CreateFailure(string errorMessage, Exception? exception = null) => new() { 
            Success = false, ErrorMessage = errorMessage, Exception = exception
        };
    }
}
