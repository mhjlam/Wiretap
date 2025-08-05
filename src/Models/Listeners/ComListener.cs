using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wiretap.Models.Listeners
{
	public partial class ComListener : BaseListener
	{
		private int _dataBits = 8;
		private int _baudRate = 9600;

		private string _portName = "";

		private ComParity _parity = ComParity.None;
		private ComStopBits _stopBits = ComStopBits.One;

		private SerialPort? _serialPort;
		private CancellationTokenSource? _cancellationTokenSource;

		public override ListenerProtocol Protocol => ListenerProtocol.COM;

		public ComListener()
		{
			// Set default port name to first available port
			var availablePorts = GetAvailableComPorts();
			if (availablePorts.Count > 0)
			{
				_portName = availablePorts[0];
			}
			else
			{
				_portName = "COM1"; // Fallback
			}

			// Generate initial name after port name is set
			OnNamePropertyChanged();
		}

		[Required]
		[Display(Name = "Port Name", Description = "COM/USB port name")]
		public string PortName
		{
			get => _portName;
			set
			{
				if (_portName != value)
				{
					_portName = value;
					OnPropertyChanged(nameof(PortName));
					OnPropertyChanged(nameof(DisplayInfo));
					OnNamePropertyChanged(); // Update name when port changes
				}
			}
		}

		[Required]
		[Range(300, 921600)]
		[Display(Name = "Baud Rate", Description = "Communication speed")]
		public int BaudRate
		{
			get => _baudRate;
			set
			{
				if (_baudRate != value)
				{
					_baudRate = value;
					OnPropertyChanged(nameof(BaudRate));
					OnPropertyChanged(nameof(DisplayInfo));
				}
			}
		}

		[Range(5, 8)]
		[Display(Name = "Data Bits", Description = "Number of data bits (5-8)")]
		public int DataBits
		{
			get => _dataBits;
			set
			{
				if (_dataBits != value)
				{
					_dataBits = value;
					OnPropertyChanged(nameof(DataBits));
					OnPropertyChanged(nameof(DisplayInfo));
				}
			}
		}

		[Display(Name = "Parity", Description = "Parity checking")]
		public ComParity Parity
		{
			get => _parity;
			set
			{
				if (_parity != value)
				{
					_parity = value;
					OnPropertyChanged(nameof(Parity));
					OnPropertyChanged(nameof(DisplayInfo));
				}
			}
		}

		[Display(Name = "Stop Bits", Description = "Number of stop bits")]
		public ComStopBits StopBits
		{
			get => _stopBits;
			set
			{
				if (_stopBits != value)
				{
					_stopBits = value;
					OnPropertyChanged(nameof(StopBits));
					OnPropertyChanged(nameof(DisplayInfo));
				}
			}
		}

		protected override string GetIdentifierFromListener() => PortName;

		public static List<string> GetAvailableComPorts()
		{
			try
			{
				var ports = new List<string>();

				// Method 1: Standard .NET method
				var standardPorts = SerialPort.GetPortNames().ToList();
				ports.AddRange(standardPorts);

				// Method 2: Registry-based detection for virtual ports
				try
				{
					using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
					if (key != null)
					{
						foreach (string valueName in key.GetValueNames())
						{
							var portName = key.GetValue(valueName)?.ToString();
							if (!string.IsNullOrEmpty(portName) && !ports.Contains(portName))
							{
								ports.Add(portName);
							}
						}
					}
				}
				catch
				{
					// Registry access failed, continue with standard method
				}

				// Method 3: WMI query for additional COM ports
				try
				{
					using var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_SerialPort");
					using var collection = searcher.Get();

					foreach (System.Management.ManagementObject port in collection.Cast<ManagementObject>())
					{
						var deviceId = port["DeviceID"]?.ToString();
						if (!string.IsNullOrEmpty(deviceId) && !ports.Contains(deviceId))
						{
							ports.Add(deviceId);
						}
					}
				}
				catch
				{
					// WMI query failed, continue with what we have
				}

				// REMOVED Method 4: Force include known virtual COM ports
				// This was causing virtual ports to persist even after cleanup
				// Virtual ports should only appear if they're properly registered via Methods 1-3

				return [.. ports.Distinct().OrderBy(x => x)];
			}
			catch
			{
				// Fallback to basic detection
				try
				{
					return [.. SerialPort.GetPortNames().OrderBy(x => x)];
				}
				catch
				{
					return [];
				}
			}
		}

		/// <summary>
		/// Event fired when COM ports are refreshed - allows UI to update
		/// </summary>
		public static event EventHandler? ComPortsRefreshed;

		/// <summary>
		/// Refreshes the available COM ports and triggers property change notifications
		/// This allows the UI to update without restarting the application
		/// </summary>
		public static void RefreshAvailableComPorts()
		{
			// Force a fresh detection by calling the method
			var refreshedPorts = GetAvailableComPorts();

			// Optional: Log the refresh for debugging
			System.Diagnostics.Debug.WriteLine($"COM ports refreshed: {string.Join(", ", refreshedPorts)}");

			// Fire event to notify UI components
			ComPortsRefreshed?.Invoke(null, EventArgs.Empty);
		}

		public override bool IsValid()
		{
			var availablePorts = GetAvailableComPorts();
			return !string.IsNullOrWhiteSpace(PortName) && availablePorts.Contains(PortName) && BaudRate >= 300 && BaudRate <= 921600 && DataBits >= 5 && DataBits <= 8;
		}

		public override string GetDisplayInfo()
		{
			return $"COM - {PortName} ({BaudRate}, {DataBits}{Parity.ToString()[0]}{(StopBits == ComStopBits.OnePointFive ? "1.5" : StopBits == ComStopBits.Two ? "2" : "1")})";
		}

		// Helper methods to convert custom enums to System.IO.Ports enums
		private static Parity ConvertToParity(ComParity comParity)
		{
			return comParity switch
			{
				ComParity.None => System.IO.Ports.Parity.None,
				ComParity.Odd => System.IO.Ports.Parity.Odd,
				ComParity.Even => System.IO.Ports.Parity.Even,
				ComParity.Mark => System.IO.Ports.Parity.Mark,
				ComParity.Space => System.IO.Ports.Parity.Space,
				_ => System.IO.Ports.Parity.None
			};
		}

		private static StopBits ConvertToStopBits(ComStopBits comStopBits)
		{
			return comStopBits switch
			{
				ComStopBits.One => System.IO.Ports.StopBits.One,
				ComStopBits.OnePointFive => System.IO.Ports.StopBits.OnePointFive,
				ComStopBits.Two => System.IO.Ports.StopBits.Two,
				_ => System.IO.Ports.StopBits.One
			};
		}

		public override async Task<ListenerOperationResult> StartListeningAsync()
		{
			if (IsListening)
			{
				return ListenerOperationResult.CreateSuccess();
			}

			await Task.CompletedTask; // Suppress CS1998 warning

			try
			{
				// Use conversion methods instead of direct casting
				_serialPort = new SerialPort(PortName, BaudRate, ConvertToParity(Parity), DataBits, ConvertToStopBits(StopBits))
				{
					// Configure timeouts for better responsiveness
					ReadTimeout = 500,  // 500ms timeout for reads
					WriteTimeout = 1000, // 1 second timeout for writes

					// Configure buffering
					ReadBufferSize = 4096,
					WriteBufferSize = 2048
				};

				_cancellationTokenSource = new CancellationTokenSource();

				_serialPort.Open();

				IsListening = true;
				Status = ListenerStatus.Enabled;

				// Show connection status only if enabled in settings
				if (ShouldShowConnectionStatus())
				{
					OnMessageReceived($"COM port {PortName} opened");
				}

				// Start listening for data
				_ = Task.Run(async () => await ListenForSerialDataAsync(_cancellationTokenSource.Token));

				return ListenerOperationResult.CreateSuccess();
			}
			catch (Exception ex)
			{
				Status = ListenerStatus.Error;
				return ListenerOperationResult.CreateFailure($"Failed to start COM listener: {ex.Message}", ex);
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

				if (_serialPort != null)
				{
					if (_serialPort.IsOpen)
					{
						_serialPort.Close();

						// Show disconnection status only if enabled in settings
						if (ShouldShowConnectionStatus())
						{
							OnMessageReceived($"COM port {PortName} closed");
						}
					}
					_serialPort.Dispose();
					_serialPort = null;
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
				return ListenerOperationResult.CreateFailure($"Failed to stop COM listener: {ex.Message}", ex);
			}
		}

		private async Task ListenForSerialDataAsync(CancellationToken cancellationToken)
		{
			var buffer = new StringBuilder();

			try
			{
				while (!cancellationToken.IsCancellationRequested && _serialPort != null && _serialPort.IsOpen)
				{
					try
					{
						// Check for available data first
						int bytesToRead = _serialPort.BytesToRead;

						if (bytesToRead > 0)
						{
							// Read available bytes
							byte[] readBuffer = new byte[bytesToRead];
							int bytesRead = _serialPort.Read(readBuffer, 0, bytesToRead);

							if (bytesRead > 0)
							{
								string data = Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
								buffer.Append(data);

								// Process any complete lines in the buffer
								ProcessBufferedData(buffer);
							}
						}
						else
						{
							// No bulk data available, try reading single byte with timeout
							try
							{
								int singleByte = _serialPort.ReadByte();
								if (singleByte != -1)
								{
									buffer.Append((char)singleByte);
									ProcessBufferedData(buffer);
								}
							}
							catch (TimeoutException)
							{
								// Timeout is expected when no data is available
								// Flush buffer if we have incomplete data after timeout
								if (buffer.Length > 0)
								{
									var data = buffer.ToString().Trim();
									if (!string.IsNullOrEmpty(data))
									{
										OnMessageReceived($"port {PortName}|{data}");
									}
									buffer.Clear();
								}

								// Small delay to prevent busy waiting
								await Task.Delay(10, cancellationToken);
							}
						}
					}
					catch (InvalidOperationException) when (_serialPort != null && !_serialPort.IsOpen)
					{
						// Port was closed, exit loop
						if (ShouldShowConnectionStatus() && !cancellationToken.IsCancellationRequested)
						{
							OnMessageReceived($"COM port {PortName} connection lost");
						}
						break;
					}
					catch (Exception) when (!cancellationToken.IsCancellationRequested)
					{
						// Handle other exceptions but don't spam the logs
						await Task.Delay(100, cancellationToken); // Wait before retrying
					}
				}
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				Status = ListenerStatus.Error;
				if (ShouldShowConnectionStatus())
				{
					OnMessageReceived($"COM port {PortName} error: {ex.Message}");
				}
			}
		}

		private void ProcessBufferedData(StringBuilder buffer)
		{
			string bufferContent = buffer.ToString();

			// Look for line endings (CR, LF, or CRLF)
			var lines = bufferContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

			if (lines.Length > 0)
			{
				// Check if the buffer ends with a line terminator
				bool endsWithTerminator = bufferContent.EndsWith('\r') || bufferContent.EndsWith('\n');

				if (endsWithTerminator)
				{
					// Process all lines and clear buffer
					foreach (var line in lines)
					{
						var trimmedLine = line.Trim();
						if (!string.IsNullOrEmpty(trimmedLine))
						{
							OnMessageReceived($"port {PortName}|{trimmedLine}");
						}
					}
					buffer.Clear();
				}
				else if (lines.Length > 1)
				{
					// Process all complete lines except the last one (which might be incomplete)
					for (int i = 0; i < lines.Length - 1; i++)
					{
						var trimmedLine = lines[i].Trim();
						if (!string.IsNullOrEmpty(trimmedLine))
						{
							OnMessageReceived($"port {PortName}|{trimmedLine}");
						}
					}

					// Keep the last incomplete line in the buffer
					buffer.Clear();
					buffer.Append(lines[^1]);
				}
				// Otherwise: single incomplete line, keep it in buffer
			}
		}
	}
}
