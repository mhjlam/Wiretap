using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Wiretap.Common;
using Wiretap.Models;
using Wiretap.Models.Listeners;

namespace Wiretap.ViewModels
{
    public class AddListenerViewModel : NotifyPropertyChangedBase
    {
        private ListenerProtocol _selectedProtocol = ListenerProtocol.UDP;
        private BaseListener? _currentListener;

        public List<ListenerProtocol> AvailableProtocols { get; }
        public List<PropertyInfo> CurrentProperties { get; private set; }

        public ListenerProtocol SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                _selectedProtocol = value;
                CreateListenerForProtocol();
                OnPropertyChanged(nameof(SelectedProtocol));
                OnPropertyChanged(nameof(CurrentListener));
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public BaseListener? CurrentListener
        {
            get => _currentListener;
            private set
            {
                _currentListener = value;
                UpdateProperties();
                OnPropertyChanged(nameof(CurrentListener));
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public bool IsValid => CurrentListener?.IsValid() == true;

        public AddListenerViewModel()
        {
            try
            {
                AvailableProtocols = Enum.GetValues<ListenerProtocol>().ToList();
                CurrentProperties = [];
                CreateListenerForProtocol();
            }
            catch (Exception ex)
            {
                // Handle any errors during initialization
                System.Diagnostics.Debug.WriteLine($"Error initializing AddListenerViewModel: {ex}");
                AvailableProtocols = [ListenerProtocol.UDP, ListenerProtocol.TCP];
                CurrentProperties = [];
                CurrentListener = null;
            }
        }

        private void CreateListenerForProtocol()
        {
            try
            {
                CurrentListener = SelectedProtocol switch
                {
                    ListenerProtocol.UDP => new UdpListener(),
                    ListenerProtocol.TCP => new TcpListener(),
                    ListenerProtocol.COM => new ComListener(),
                    ListenerProtocol.Pipe => new PipeListener(),
                    ListenerProtocol.USB => new UsbListener(),
                    _ => throw new NotSupportedException($"Protocol {SelectedProtocol} is not supported")
                };

                if (CurrentListener != null)
                {
                    CurrentListener.PropertyChanged += (s, e) => OnPropertyChanged(nameof(IsValid));
                }
            }
            catch (Exception ex)
            {
                // Log the error and set CurrentListener to null
                System.Diagnostics.Debug.WriteLine($"Error creating listener for protocol {SelectedProtocol}: {ex}");
                CurrentListener = null;
            }
        }

        private void UpdateProperties()
        {
            try
            {
                if (CurrentListener == null)
                {
                    CurrentProperties = [];
                    return;
                }

                var type = CurrentListener.GetType();
                CurrentProperties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && 
                               p.Name != nameof(BaseListener.Protocol) && 
                               p.Name != nameof(BaseListener.Status) &&
                               p.Name != nameof(BaseListener.IsListening) &&
                               p.Name != nameof(BaseListener.DisplayInfo) &&
                               p.Name != nameof(BaseListener.Name) && // Hide Name since it's auto-generated
                               p.Name != "BindAddress") // Hide BindAddress since it's now fixed
                    .OrderBy(p => p.PropertyType == typeof(string) ? 0 : 1)];

                OnPropertyChanged(nameof(CurrentProperties));
            }
            catch (Exception ex)
            {
                // Handle any errors during property reflection
                System.Diagnostics.Debug.WriteLine($"Error updating properties: {ex}");
                CurrentProperties = [];
                OnPropertyChanged(nameof(CurrentProperties));
            }
        }

        public static string GetPropertyDisplayName(PropertyInfo property)
        {
            var displayAttribute = property.GetCustomAttribute<DisplayAttribute>();
            return displayAttribute?.Name ?? property.Name;
        }

        public static string GetPropertyDescription(PropertyInfo property)
        {
            var displayAttribute = property.GetCustomAttribute<DisplayAttribute>();
            return displayAttribute?.Description ?? string.Empty;
        }

        public static bool IsPropertyRequired(PropertyInfo property)
        {
            return property.GetCustomAttribute<RequiredAttribute>() != null;
        }

        public static RangeAttribute? GetPropertyRange(PropertyInfo property)
        {
            return property.GetCustomAttribute<RangeAttribute>();
        }

        public static List<string> GetAvailableComPorts()
        {
            // Use the enhanced detection from ComListener instead of basic SerialPort.GetPortNames()
            return ComListener.GetAvailableComPorts();
        }

        /// <summary>
        /// Refreshes the COM port list and triggers UI updates
        /// Call this method after setting up virtual COM ports
        /// </summary>
        public static void RefreshComPorts()
        {
            // Trigger a refresh in the ComListener
            ComListener.RefreshAvailableComPorts();
            
            // Note: UI updates will be handled by the ComPortsRefreshed event
            // that components can subscribe to for automatic refresh
        }

        public static List<int> GetValidBaudRates()
        {
            return [
                300,    600,    1200,
                2400,   4800,   9600,
                14400,  19200,  28800,
                38400,  57600,  115200,
                230400, 460800, 921600
            ];
        }

        public static List<int> GetValidDataBits()
        {
            return [5, 6, 7, 8];
        }

        public static List<UsbDeviceInfo> GetAvailableUsbDevices()
        {
            // Use the same cached approach as UsbListener to avoid duplication
            return UsbListener.GetAvailableUsbDevices();
        }

        // Add async method for refreshing USB devices
        public static async Task<List<UsbDeviceInfo>> GetAvailableUsbDevicesAsync()
        {
            return await UsbListener.GetAvailableUsbDevicesAsync();
        }
    }
}
