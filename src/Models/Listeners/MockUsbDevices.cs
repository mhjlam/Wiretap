using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wiretap.Models;

namespace Wiretap.Models.Listeners
{
    /// <summary>
    /// Mock USB devices have been removed to simplify testing.
    /// Use real hardware (Arduino, ESP32, etc.) for USB testing instead.
    /// </summary>
    public static class MockUsbDevices
    {
        public static List<UsbDeviceInfo> GetMockDevices()
        {
            // Mock devices disabled - use real hardware for testing
            return new List<UsbDeviceInfo>();
        }
        
        public static string? GetMockComPortForDevice(string deviceId)
        {
            // Mock device mapping disabled - use real USB devices
            return null;
        }
        
        /// <summary>
        /// Generates realistic device data for testing - kept for reference
        /// </summary>
        public static string GenerateDeviceData(string deviceId, int packetNumber)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            return $"DEVICE_DATA: packet={packetNumber}, time={timestamp}";
        }
    }
}