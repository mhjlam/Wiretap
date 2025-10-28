using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wiretap.Models.Listeners
{
    // Simple USB device definition for our purposes
    public class UsbDeviceInfo
    {
        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class UsbListener : BaseListener
    {
        private string _deviceId = "";
        private string _deviceName = "";
        private CancellationTokenSource? _cancellationTokenSource;
        
        // Enhanced caching with better performance and reliability
        private static readonly ConcurrentDictionary<string, List<UsbDeviceInfo>> _deviceCache = new();
        private static readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5); // Increased cache time
        private static readonly SemaphoreSlim _enumerationSemaphore = new(1, 1);
        
        // Background refresh mechanism
        private static Timer? _backgroundRefreshTimer;
        private static readonly object _timerLock = new object();
        
        // Performance tracking
        private static readonly ConcurrentQueue<TimeSpan> _enumerationTimes = new();
        private static int _consecutiveFailures = 0;
        private static DateTime _lastFailureTime = DateTime.MinValue;

        public override ListenerProtocol Protocol => ListenerProtocol.USB;

        static UsbListener()
        {
            // Initialize background refresh timer
            InitializeBackgroundRefresh();
        }

        public UsbListener()
        {
            // Use cached devices or provide fallback without blocking UI
            var availableDevices = GetCachedUsbDevices();
            if (availableDevices.Count > 0)
            {
                _deviceId = availableDevices[0].DeviceId;
                _deviceName = availableDevices[0].Name;
            }
            else
            {
                _deviceId = "";
                _deviceName = "Loading USB devices...";
                
                // Trigger background enumeration if cache is empty
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var devices = await GetAvailableUsbDevicesAsync();
                        if (devices.Count > 0 && string.IsNullOrEmpty(_deviceId))
                        {
                            DeviceId = devices[0].DeviceId;
                        }
                    }
                    catch
                    {
                        // Update UI to show enumeration failed
                        _deviceName = "USB enumeration failed";
                        OnPropertyChanged(nameof(DeviceName));
                    }
                });
            }
            
            OnNamePropertyChanged();
        }

        [Required]
        [Display(Name = "USB Device", Description = "Select a USB device")]
        public string DeviceId
        {
            get => _deviceId;
            set
            {
                if (_deviceId != value)
                {
                    _deviceId = value;
                    var device = GetCachedUsbDevices().FirstOrDefault(d => d.DeviceId == value);
                    _deviceName = device?.Name ?? "Unknown Device";
                    
                    OnPropertyChanged(nameof(DeviceId));
                    OnPropertyChanged(nameof(DeviceName));
                    OnPropertyChanged(nameof(DisplayInfo));
                    OnNamePropertyChanged();
                }
            }
        }

        public string DeviceName => _deviceName;

        protected override string GetIdentifierFromListener() => DeviceId;

        private static void InitializeBackgroundRefresh()
        {
            lock (_timerLock)
            {
                if (_backgroundRefreshTimer == null)
                {
                    // Refresh cache every 2 minutes in background
                    _backgroundRefreshTimer = new Timer(async _ =>
                    {
                        try
                        {
                            await RefreshCacheInBackground();
                        }
                        catch
                        {
                            // Silently handle background refresh errors
                        }
                    }, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2));
                }
            }
        }

        private static async Task RefreshCacheInBackground()
        {
            // Only refresh if someone is likely to using USB devices
            if (_deviceCache.IsEmpty && DateTime.Now - _lastFailureTime > TimeSpan.FromMinutes(5))
            {
                return;
            }

            // Don't refresh too often if we're having failures
            if (_consecutiveFailures > 3 && DateTime.Now - _lastFailureTime < TimeSpan.FromMinutes(1))
            {
                return;
            }

            try
            {
                await GetAvailableUsbDevicesAsync(useCache: false);
            }
            catch
            {
                // Background refresh failures are silent
            }
        }

        private static List<UsbDeviceInfo> GetCachedUsbDevices()
        {
            const string cacheKey = "usb_devices";
            
            if (_deviceCache.TryGetValue(cacheKey, out var cachedDevices) 
            &&  _cacheTimestamps.TryGetValue(cacheKey, out var timestamp) 
            &&  DateTime.Now - timestamp < CacheExpiry)
            {
                return cachedDevices;
            }
            
            return [];
        }

        public static List<UsbDeviceInfo> GetAvailableUsbDevices()
        {
            var cached = GetCachedUsbDevices();
            if (cached.Count > 0)
            {
                return cached;
            }

            // Use shorter timeout for synchronous calls
            try
            {
                var task = GetAvailableUsbDevicesAsync();
                if (task.Wait(2000)) // Reduced from 5000ms to 2000ms
                {
                    return task.Result;
                }
                else
                {
                    Debug.WriteLine("USB enumeration timed out (sync)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Synchronous USB enumeration failed: {ex.Message}");
            }

            return [];
        }

        public static async Task<List<UsbDeviceInfo>> GetAvailableUsbDevicesAsync(bool useCache = true)
        {
            const string cacheKey = "usb_devices";
            
            // Check cache first if requested
            if (useCache)
            {
                var cached = GetCachedUsbDevices();
                if (cached.Count > 0)
                {
                    return cached;
                }
            }

            // Implement exponential backoff for failures
            if (_consecutiveFailures > 0)
            {
                var backoffTime = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, _consecutiveFailures)));
                if (DateTime.Now - _lastFailureTime < backoffTime)
                {
                    Debug.WriteLine($"USB enumeration skipped due to backoff ({_consecutiveFailures} failures)");
                    return GetCachedUsbDevices(); // Return whatever we have cached
                }
            }

            // Use timeout for semaphore to prevent deadlocks
            if (!await _enumerationSemaphore.WaitAsync(1000))
            {
                Debug.WriteLine("USB enumeration semaphore timeout");
                return GetCachedUsbDevices();
            }

            try
            {
                // Double-check cache after acquiring semaphore
                if (useCache)
                {
                    var cached = GetCachedUsbDevices();
                    if (cached.Count > 0)
                    {
                        return cached;
                    }
                }

                var stopwatch = Stopwatch.StartNew();
                var devices = await EnumerateUsbDevicesWithTimeout();
                
                devices = [.. devices.OrderBy(d => d.Name)];
                
                // Update cache
                _deviceCache[cacheKey] = devices;
                _cacheTimestamps[cacheKey] = DateTime.Now;
                
                // Reset failure counter on success
                _consecutiveFailures = 0;
                
                Debug.WriteLine($"USB enumeration completed in {stopwatch.ElapsedMilliseconds}ms, found {devices.Count} devices");
                return devices;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _lastFailureTime = DateTime.Now;
                
                Debug.WriteLine($"USB enumeration failed (attempt {_consecutiveFailures}): {ex.Message}");
                
                // Still return mock devices even if real enumeration fails
                var mockDevices = MockUsbDevices.GetMockDevices();
                if (mockDevices.Count > 0)
                {
                    _deviceCache[cacheKey] = mockDevices;
                    _cacheTimestamps[cacheKey] = DateTime.Now;
                    return mockDevices;
                }
                
                // Cache empty result with shorter expiry for failures
                _deviceCache[cacheKey] = new List<UsbDeviceInfo>();
                _cacheTimestamps[cacheKey] = DateTime.Now.AddMinutes(-4); // Will expire in 1 minute instead of 5
                
                return [];
            }
            finally
            {
                _enumerationSemaphore.Release();
            }
        }

        private static async Task<List<UsbDeviceInfo>> EnumerateUsbDevicesWithTimeout()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)); // Shorter timeout
            
            return await Task.Run(() =>
            {
                var devices = new List<UsbDeviceInfo>();
                
                // Use more specific and faster WMI query
                var query = "SELECT DeviceID, Name, Description, Service, Status FROM Win32_PnPEntity WHERE " +
                           "(DeviceID LIKE 'USB\\\\VID_%' OR DeviceID LIKE 'USB\\\\ROOT_HUB%') AND " +
                           "Status = 'OK'"; // Only get working devices
                
                using var searcher = new ManagementObjectSearcher(query);
                
                // Configure for better performance
                searcher.Options.Timeout = TimeSpan.FromMilliseconds(2500);
                searcher.Options.ReturnImmediately = true;
                searcher.Options.Rewindable = false;
                searcher.Options.UseAmendedQualifiers = false;
                
                using var searchResults = searcher.Get();
                
                foreach (ManagementObject device in searchResults)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var deviceId = device["DeviceID"]?.ToString();
                        var name = device["Name"]?.ToString();
                        var description = device["Description"]?.ToString();
                        var service = device["Service"]?.ToString();
                        var status = device["Status"]?.ToString();

                        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(name) || status != "OK")
                        {
                            continue;
                        }

                        if (IsSystemDevice(deviceId, name, service))
                        {
                            continue;
                        }

                        devices.Add(new UsbDeviceInfo
                        {
                            DeviceId = deviceId,
                            Name = name,
                            Description = description ?? "USB Device"
                        });
                    }
                    catch
                    {
                        continue; // Skip problematic devices
                    }
                }
                
                return devices;
            }, 
            cts.Token);
        }

        public override bool IsValid()
        {
            var availableDevices = GetAvailableUsbDevices();
            return !string.IsNullOrWhiteSpace(DeviceId) && availableDevices.Any(d => d.DeviceId == DeviceId);
        }

        public override string GetDisplayInfo()
        {
            return $"USB - {DeviceName}";
        }

        public override async Task<ListenerOperationResult> StartListeningAsync()
        {
            if (IsListening)
            {
                return ListenerOperationResult.CreateSuccess();
            }

            try
            {
                var availableDevices = await GetAvailableUsbDevicesAsync();
                var deviceDefinition = availableDevices.FirstOrDefault(d => d.DeviceId == DeviceId);
                if (deviceDefinition == null)
                {
                    return ListenerOperationResult.CreateFailure("USB device not found.");
                }

                _cancellationTokenSource = new CancellationTokenSource();
                
                IsListening = true;
                Status = ListenerStatus.Enabled;
                
                _ = Task.Run(async () => await MonitorUsbDeviceAsync(_cancellationTokenSource.Token));
                
                return ListenerOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                Status = ListenerStatus.Error;
                return ListenerOperationResult.CreateFailure($"Failed to start USB listener: {ex.Message}", ex);
            }
        }

        public override async Task<ListenerOperationResult> StopListeningAsync()
        {
            if (!IsListening)
            {
                return ListenerOperationResult.CreateSuccess();
            }

            await Task.CompletedTask; // Suppress CS1998 warning

            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                IsListening = false;
                Status = ListenerStatus.Disabled;
                
                return ListenerOperationResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                Status = ListenerStatus.Error;
                return ListenerOperationResult.CreateFailure($"Failed to stop USB listener: {ex.Message}", ex);
            }
        }

        private async Task MonitorUsbDeviceAsync(CancellationToken cancellationToken)
        {
            // Check if this is a serial communication device that we can read from
            var device = GetCachedUsbDevices().FirstOrDefault(d => d.DeviceId == DeviceId);
            if (device == null)
            {
                OnMessageReceived($"USB device {DeviceId} not found");
                return;
            }

            // Show connection status if enabled
            if (ShouldShowConnectionStatus())
            {
                OnMessageReceived($"USB device {device.Name} connected");
            }

            try
            {
                // Try to find a corresponding COM port for this USB device
                var comPort = await TryFindComPortForUsbDevice();
                
                if (!string.IsNullOrEmpty(comPort))
                {
                    OnMessageReceived($"USB device mapped to {comPort}");
                    
                    // Create a temporary COM listener to read data from this USB device
                    await MonitorUsbAsComPortAsync(comPort, cancellationToken);
                }
                else
                {
                    // Device doesn't have a COM port interface, just monitor presence
                    OnMessageReceived($"USB device {device.Name} detected (no data interface)");
                    
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Check if device is still present
                        var currentDevices = await GetAvailableUsbDevicesAsync();
                        if (!currentDevices.Any(d => d.DeviceId == DeviceId))
                        {
                            if (ShouldShowConnectionStatus())
                            {
                                OnMessageReceived($"USB device {device.Name} disconnected");
                            }
                            break;
                        }
                        
                        await Task.Delay(5000, cancellationToken); // Check every 5 seconds
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected cancellation
                if (ShouldShowConnectionStatus())
                {
                    OnMessageReceived($"USB device {device?.Name ?? DeviceId} monitoring stopped");
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Status = ListenerStatus.Error;
                OnMessageReceived($"USB Error: {ex.Message}");
            }
        }

        private async Task<string?> TryFindComPortForUsbDevice()
        {
            await Task.CompletedTask; // Suppress async warning
            
            try
            {
                // Check if this is a mock device first
                var mockComPort = MockUsbDevices.GetMockComPortForDevice(DeviceId);
                if (!string.IsNullOrEmpty(mockComPort))
                {
                    return mockComPort;
                }
                
                // Query for COM ports that match our USB device
                var query = "SELECT * FROM Win32_SerialPort";
                using var searcher = new ManagementObjectSearcher(query);
                using var collection = searcher.Get();
                
                foreach (ManagementObject port in collection)
                {
                    try
                    {
                        var pnpDeviceId = port["PNPDeviceID"]?.ToString();
                        if (!string.IsNullOrEmpty(pnpDeviceId) && 
                            pnpDeviceId.Contains(DeviceId.Split('\\')[1])) // Match VID/PID portion
                        {
                            return port["DeviceID"]?.ToString(); // Returns "COM3", "COM4", etc.
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived($"Failed to find COM port for USB device: {ex.Message}");
            }
            
            return null;
        }

        private async Task MonitorUsbAsComPortAsync(string comPort, CancellationToken cancellationToken)
        {
            SerialPort? serialPort = null;
            var buffer = new StringBuilder();
            
            try
            {
				// Create and configure serial port
				serialPort = new SerialPort(comPort, 9600, Parity.None, 8, StopBits.One)
				{
					ReadTimeout = 500
				};
				serialPort.Open();
                
                OnMessageReceived($"Reading data from USB device via {comPort}");
                
                while (!cancellationToken.IsCancellationRequested && serialPort.IsOpen)
                {
                    try
                    {
                        // Check for available data
                        int bytesToRead = serialPort.BytesToRead;
                        
                        if (bytesToRead > 0)
                        {
                            // Read available bytes
                            byte[] readBuffer = new byte[bytesToRead];
                            int bytesRead = serialPort.Read(readBuffer, 0, bytesToRead);
                            
                            if (bytesRead > 0)
                            {
                                string data = System.Text.Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
                                buffer.Append(data);
                                
                                // Process any complete lines
                                ProcessUsbBufferedData(buffer);
                            }
                        }
                        else
                        {
                            // Try reading single byte with timeout
                            try
                            {
                                int singleByte = serialPort.ReadByte();
                                if (singleByte != -1)
                                {
                                    buffer.Append((char)singleByte);
                                    ProcessUsbBufferedData(buffer);
                                }
                            }
                            catch (TimeoutException)
                            {
                                // Flush buffer on timeout if we have data
                                if (buffer.Length > 0)
                                {
                                    var data = buffer.ToString().Trim();
                                    if (!string.IsNullOrEmpty(data))
                                    {
                                        OnMessageReceived($"USB {DeviceId}|{data}");
                                    }
                                    buffer.Clear();
                                }
                                
                                await Task.Delay(10, cancellationToken);
                            }
                        }
                    }
                    catch (InvalidOperationException) when (!serialPort.IsOpen)
                    {
                        OnMessageReceived($"USB device {comPort} disconnected");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived($"USB COM port error: {ex.Message}");
            }
            finally
            {
                serialPort?.Close();
                serialPort?.Dispose();
            }
        }

        private void ProcessUsbBufferedData(StringBuilder buffer)
        {
            string bufferContent = buffer.ToString();
            
            // Look for line endings
            var lines = bufferContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length > 0)
            {
                bool endsWithTerminator = bufferContent.EndsWith('\r') || bufferContent.EndsWith('\n');
                
                if (endsWithTerminator)
                {
                    // Process all lines and clear buffer
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            OnMessageReceived($"USB {DeviceId}|{trimmedLine}");
                        }
                    }
                    buffer.Clear();
                }
                else if (lines.Length > 1)
                {
                    // Process complete lines, keep last incomplete line
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        var trimmedLine = lines[i].Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            OnMessageReceived($"USB {DeviceId}|{trimmedLine}");
                        }
                    }
                    
                    buffer.Clear();
                    buffer.Append(lines[lines.Length - 1]);
                }
            }
        }
        
        // Enhanced device filtering with whitelist/blacklist approach for data-producing devices
        private static bool IsSystemDevice(string deviceId, string name, string? service)
        {
            var deviceIdUpper = deviceId.ToUpperInvariant();
            var nameUpper = name.ToUpperInvariant();
            var serviceUpper = service?.ToUpperInvariant() ?? "";

            // === WHITELIST FIRST: Devices we definitely want to include ===
            
            // Arduino and microcontroller devices (most common for monitoring)
            if (IsArduinoOrMicrocontrollerDevice(deviceIdUpper, nameUpper))
                return false; // Include these devices
            
            // Development boards and embedded systems
            if (IsDevelopmentBoard(deviceIdUpper, nameUpper))
                return false; // Include these devices
                
            // Serial communication devices (CDC, FTDI, etc.)
            if (IsSerialCommunicationDevice(deviceIdUpper, nameUpper, serviceUpper))
                return false; // Include these devices
                
            // Custom/vendor devices likely to be useful for monitoring
            if (IsCustomOrVendorDevice(deviceIdUpper, nameUpper))
                return false; // Include these devices

            // === BLACKLIST: Device types that are never useful for data monitoring ===
            
            // System infrastructure (highest priority exclusions)
            if (deviceIdUpper.Contains("ROOT_HUB") || deviceIdUpper.Contains("USB\\ROOT_HUB") ||
                serviceUpper == "USBHUB" || serviceUpper == "USBHUB3" || serviceUpper == "USB")
                return true;
                
            // Storage devices (drives, memory sticks, etc.)
            if (deviceIdUpper.Contains("USBSTOR\\") || deviceIdUpper.Contains("STORAGE\\") || 
                deviceIdUpper.Contains("SCSI\\") || deviceIdUpper.Contains("DISK\\") ||
                nameUpper.Contains("MASS STORAGE") || nameUpper.Contains("USB DRIVE") ||
                nameUpper.Contains("FLASH DRIVE") || nameUpper.Contains("MEMORY STICK"))
                return true;
                
            // Audio/Video devices (never useful for data monitoring)
            if (nameUpper.Contains("AUDIO") || nameUpper.Contains("MICROPHONE") || 
                nameUpper.Contains("SPEAKER") || nameUpper.Contains("HEADSET") ||
                nameUpper.Contains("WEBCAM") || nameUpper.Contains("CAMERA") ||
                nameUpper.Contains("VIDEO") || nameUpper.Contains("SOUND"))
                return true;
                
            // Input devices (mice, keyboards, game controllers)
            if (nameUpper.Contains("MOUSE") || nameUpper.Contains("KEYBOARD") ||
                nameUpper.Contains("GAMEPAD") || nameUpper.Contains("JOYSTICK") ||
                nameUpper.Contains("CONTROLLER") || nameUpper.Contains("TRACKPAD") ||
                serviceUpper == "HIDUSB" || serviceUpper == "MOUHID" || serviceUpper == "KBDHID")
                return true;
                
            // Network devices (WiFi, Bluetooth, Ethernet adapters)
            if (nameUpper.Contains("WIFI") || nameUpper.Contains("WIRELESS") ||
                nameUpper.Contains("BLUETOOTH") || nameUpper.Contains("ETHERNET") ||
                nameUpper.Contains("NETWORK") || nameUpper.Contains("802.11"))
                return true;
                
            // Composite and generic devices (usually system-level)
            if (nameUpper.Contains("COMPOSITE") || nameUpper.Contains("USB DEVICE") ||
                nameUpper.Contains("GENERIC") || nameUpper.Contains("STANDARD"))
                return true;
                
            // Printers and scanners
            if (nameUpper.Contains("PRINTER") || nameUpper.Contains("SCANNER") ||
                nameUpper.Contains("PRINT") || nameUpper.Contains("SCAN"))
                return true;
                
            // Mobile devices and phones (not typically used for monitoring)
            if (nameUpper.Contains("IPHONE") || nameUpper.Contains("ANDROID") ||
                nameUpper.Contains("MOBILE") || nameUpper.Contains("PHONE"))
                return true;
                
            // Invalid/placeholder devices
            if (deviceIdUpper.Contains("VID_0000") || deviceIdUpper.Contains("PID_0000") ||
                deviceIdUpper.Contains("VID_FFFF") || deviceIdUpper.Contains("PID_FFFF"))
                return true;

            // If we get here, it's an unknown device - be conservative and include it
            // (better to show a potentially useful device than hide it)
            return false;
        }

        private static bool IsArduinoOrMicrocontrollerDevice(string deviceIdUpper, string nameUpper)
        {
            // Arduino boards (various official and compatible boards)
            if (nameUpper.Contains("ARDUINO") || 
                deviceIdUpper.Contains("VID_2341") || // Arduino official VID
                deviceIdUpper.Contains("VID_1B4F") ||  // SparkFun
                deviceIdUpper.Contains("VID_16C0"))    // Van Ooijen Technische Informatica (used by many Arduino clones)
                return true;
                
            // ESP32/ESP8266 development boards
            if (nameUpper.Contains("ESP32") || nameUpper.Contains("ESP8266") ||
                deviceIdUpper.Contains("VID_10C4") ||  // Silicon Labs (ESP32 boards)
                deviceIdUpper.Contains("VID_1A86"))    // QinHeng Electronics (CH340 chip common in ESP boards)
                return true;
                
            // Raspberry Pi Pico and similar
            if (nameUpper.Contains("PICO") || nameUpper.Contains("RASPBERRY PI") ||
                deviceIdUpper.Contains("VID_2E8A"))    // Raspberry Pi Foundation
                return true;
                
            // STM32 development boards
            if (nameUpper.Contains("STM32") || nameUpper.Contains("ST-LINK") ||
                deviceIdUpper.Contains("VID_0483"))    // STMicroelectronics
                return true;
                
            // Teensy boards
            if (nameUpper.Contains("TEENSY") ||
                deviceIdUpper.Contains("VID_16C0"))    // PJRC (Teensy)
                return true;

            return false;
        }

        private static bool IsDevelopmentBoard(string deviceIdUpper, string nameUpper)
        {
            // Development and evaluation boards
            if (nameUpper.Contains("DEV BOARD") || nameUpper.Contains("DEVELOPMENT") ||
                nameUpper.Contains("EVAL BOARD") || nameUpper.Contains("EVALUATION") ||
                nameUpper.Contains("PROTOTYPE") || nameUpper.Contains("BREAKOUT"))
                return true;
                
            // FPGA and DSP boards
            if (nameUpper.Contains("FPGA") || nameUpper.Contains("DSP") ||
                nameUpper.Contains("XILINX") || nameUpper.Contains("ALTERA") ||
                nameUpper.Contains("LATTICE"))
                return true;
                
            // Embedded system boards
            if (nameUpper.Contains("BEAGLEBONE") || nameUpper.Contains("ODROID") ||
                nameUpper.Contains("ORANGE PI") || nameUpper.Contains("BANANA PI"))
                return true;

            return false;
        }

        private static bool IsSerialCommunicationDevice(string deviceIdUpper, string nameUpper, string serviceUpper)
        {
            // CDC (Communication Device Class) - USB-to-Serial devices
            if (nameUpper.Contains("CDC") || nameUpper.Contains("COMMUNICATION") ||
                serviceUpper == "USBSER" || serviceUpper.Contains("SERIAL"))
                return true;
                
            // FTDI USB-to-Serial chips (very common for custom devices)
            if (nameUpper.Contains("FTDI") || nameUpper.Contains("FT232") ||
                nameUpper.Contains("FT245") || deviceIdUpper.Contains("VID_0403"))
                return true;
                
            // Prolific USB-to-Serial chips
            if (nameUpper.Contains("PROLIFIC") || nameUpper.Contains("PL2303") ||
                deviceIdUpper.Contains("VID_067B"))
                return true;
                
            // Silicon Labs CP210x series (common in development boards)
            if (nameUpper.Contains("CP210") || nameUpper.Contains("SILICON LABS") ||
                deviceIdUpper.Contains("VID_10C4"))
                return true;
                
            // CH340/CH341 USB-to-Serial chips (very common in Chinese boards)
            if (nameUpper.Contains("CH340") || nameUpper.Contains("CH341") ||
                deviceIdUpper.Contains("VID_1A86"))
                return true;

            return false;
        }

        private static bool IsCustomOrVendorDevice(string deviceIdUpper, string nameUpper)
        {
            // Sensor devices and measurement equipment
            if (nameUpper.Contains("SENSOR") || nameUpper.Contains("MEASUREMENT") ||
                nameUpper.Contains("LOGGER") || nameUpper.Contains("MONITOR") ||
                nameUpper.Contains("ACQUISITION") || nameUpper.Contains("DAQ"))
                return true;
                
            // Industrial and automation devices
            if (nameUpper.Contains("INDUSTRIAL") || nameUpper.Contains("AUTOMATION") ||
                nameUpper.Contains("CONTROL") || nameUpper.Contains("INTERFACE"))
                return true;
                
            // Test and measurement equipment
            if (nameUpper.Contains("OSCILLOSCOPE") || nameUpper.Contains("MULTIMETER") ||
                nameUpper.Contains("ANALYZER") || nameUpper.Contains("GENERATOR") ||
                nameUpper.Contains("TESTER"))
                return true;
                
            // Programming and debugging tools
            if (nameUpper.Contains("PROGRAMMER") || nameUpper.Contains("DEBUGGER") ||
                nameUpper.Contains("JTAG") || nameUpper.Contains("SWD") ||
                nameUpper.Contains("PROBE"))
                return true;
                
            // Custom device indicators (devices likely to be custom/useful)
            if (nameUpper.Contains("CUSTOM") || nameUpper.Contains("VENDOR") ||
                nameUpper.Contains("PROPRIETARY") || nameUpper.Contains("SPECIALIZED"))
                return true;

            return false;
        }
        
        // Utility method to get performance stats
        public static string GetPerformanceStats()
        {
            if (_enumerationTimes.IsEmpty)
            {
                return "No USB enumerations performed yet";
            }
                
            var times = _enumerationTimes.ToArray();
            var avgMs = times.Select(t => t.TotalMilliseconds).Average();
            var maxMs = times.Select(t => t.TotalMilliseconds).Max();
            
            return $"USB enum avg: {avgMs:F1}ms, max: {maxMs:F1}ms, failures: {_consecutiveFailures}";
        }
    }
}
