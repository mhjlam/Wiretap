# Test.ps1 - Wiretap Listener Testing Tool
param(
    [string]$Protocol = "",
    [string]$Target = "",
    [string]$Message = "Test message from PowerShell",
    [int]$Count = 5,
    [int]$Interval = 2000,
    [switch]$Interactive,
    [switch]$ListAll,
    [switch]$Help
)

Write-Host "Wiretap Listener Testing Tool" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan

# Available protocols and their default targets
$ProtocolInfo = @{
    "TCP" = @{
        DefaultTarget = "127.0.0.1:9090"
        Description = "TCP Socket (IP:Port)"
        Setup = "Ready to use - no setup required"
    }
    "UDP" = @{
        DefaultTarget = "127.0.0.1:8080"
        Description = "UDP Socket (IP:Port)"
        Setup = "Ready to use - no setup required"
    }
    "COM" = @{
        DefaultTarget = "COM1"
        Description = "Serial COM Port"
        Setup = "Use VSPE for virtual ports or real hardware"
    }
    "PIPE" = @{
        DefaultTarget = "TestPipe"
        Description = "Named Pipe"
        Setup = "Ready to use - built into Windows"
    }
    "USB" = @{
        DefaultTarget = "Real"
        Description = "USB Device (requires real hardware)"
        Setup = "Connect Arduino, ESP32, or similar device"
    }
}

if ($Help -or ($Protocol -eq "" -and !$Interactive -and !$ListAll)) {
    Write-Host ""
    Write-Host "Usage Examples:" -ForegroundColor Yellow
    Write-Host "  .\Test.ps1 -Protocol TCP -Target '127.0.0.1:9090'" -ForegroundColor White
    Write-Host "  .\Test.ps1 -Protocol UDP -Target '127.0.0.1:8080'" -ForegroundColor White
    Write-Host "  .\Test.ps1 -Protocol COM -Target 'COM3'" -ForegroundColor White
    Write-Host "  .\Test.ps1 -Protocol PIPE -Target 'TestPipe'" -ForegroundColor White
    Write-Host "  .\Test.ps1 -Protocol USB -Target 'Real'" -ForegroundColor White
    Write-Host "  .\Test.ps1 -Interactive" -ForegroundColor White
    Write-Host "  .\Test.ps1 -ListAll" -ForegroundColor White
    Write-Host ""
    Write-Host "Parameters:" -ForegroundColor Cyan
    Write-Host "  -Protocol    : TCP, UDP, COM, PIPE, USB" -ForegroundColor White
    Write-Host "  -Target      : Connection target (IP:Port, COMx, PipeName, etc.)" -ForegroundColor White
    Write-Host "  -Message     : Custom test message" -ForegroundColor White
    Write-Host "  -Count       : Number of messages to send (default: 5)" -ForegroundColor White
    Write-Host "  -Interval    : Delay between messages in ms (default: 2000)" -ForegroundColor White
    Write-Host "  -Interactive : Interactive mode with menu" -ForegroundColor White
    Write-Host "  -ListAll     : List available targets for all protocols" -ForegroundColor White
    Write-Host ""
    Write-Host "Setup Instructions:" -ForegroundColor Yellow
    Write-Host "  TCP/UDP : Ready to use immediately" -ForegroundColor Green
    Write-Host "  PIPE    : Built into Windows, no setup needed" -ForegroundColor Green
    Write-Host "  COM     : Download VSPE for virtual ports (free)" -ForegroundColor Yellow
    Write-Host "            https://www.eterlogic.com/Products.VSPE.html" -ForegroundColor Gray
    Write-Host "  USB     : Connect real hardware (Arduino, ESP32, etc.)" -ForegroundColor Yellow
    return
}

if ($ListAll) {
    Write-Host ""
    Write-Host "Available Targets for All Protocols:" -ForegroundColor Yellow
    
    # TCP/UDP - show local IPs
    Write-Host ""
    Write-Host "TCP/UDP Targets (Ready to use):" -ForegroundColor Green
    try {
        $localIPs = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -ne "127.0.0.1" -and $_.PrefixOrigin -eq "Dhcp" } | Select-Object -First 3
        Write-Host "   * 127.0.0.1:9090 (localhost TCP)" -ForegroundColor White
        Write-Host "   * 127.0.0.1:8080 (localhost UDP)" -ForegroundColor White
        foreach ($ip in $localIPs) {
            Write-Host "   * $($ip.IPAddress):9090 (TCP)" -ForegroundColor Gray
            Write-Host "   * $($ip.IPAddress):8080 (UDP)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "   * 127.0.0.1:9090 (TCP)" -ForegroundColor White
        Write-Host "   * 127.0.0.1:8080 (UDP)" -ForegroundColor White
    }
    
    # COM Ports
    Write-Host ""
    Write-Host "COM Port Targets:" -ForegroundColor Green
    try {
        $comPorts = [System.IO.Ports.SerialPort]::GetPortNames() | Sort-Object
        if ($comPorts) {
            foreach ($port in $comPorts) {
                Write-Host "   * $port" -ForegroundColor White
            }
            Write-Host ""
            Write-Host "   For virtual COM ports, use VSPE:" -ForegroundColor Yellow
            Write-Host "   https://www.eterlogic.com/Products.VSPE.html" -ForegroundColor Gray
        } else {
            Write-Host "   No COM ports detected" -ForegroundColor Gray
            Write-Host "   Install VSPE or connect serial hardware" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "   Error detecting COM ports" -ForegroundColor Red
    }
    
    # Named Pipes
    Write-Host ""
    Write-Host "Named Pipe Targets (Ready to use):" -ForegroundColor Green
    Write-Host "   * TestPipe (default)" -ForegroundColor White
    Write-Host "   * MyAppPipe (example)" -ForegroundColor Gray
    Write-Host "   * DataPipe (example)" -ForegroundColor Gray
    
    # USB Devices
    Write-Host ""
    Write-Host "USB Targets:" -ForegroundColor Green
    Write-Host "   * Connect real USB devices (Arduino, ESP32, etc.)" -ForegroundColor White
    Write-Host "   * Devices appear automatically in Wiretap application" -ForegroundColor Gray
    
    return
}

function Send-TCPMessage {
    param([string]$target, [string]$message)
    
    if ($target -notmatch '^(.+):(\d+)$') {
        Write-Host "ERROR: Invalid TCP target format. Use IP:Port (e.g., 127.0.0.1:9090)" -ForegroundColor Red
        return $false
    }
    
    $ip = $matches[1]
    $port = [int]$matches[2]
    
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.Connect($ip, $port)
        
        $stream = $tcpClient.GetStream()
        $data = [System.Text.Encoding]::UTF8.GetBytes("$message`r`n")
        $stream.Write($data, 0, $data.Length)
        
        $tcpClient.Close()
        Write-Host "SUCCESS: TCP sent to $target" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "ERROR: TCP failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Send-UDPMessage {
    param([string]$target, [string]$message)
    
    if ($target -notmatch '^(.+):(\d+)$') {
        Write-Host "ERROR: Invalid UDP target format. Use IP:Port (e.g., 127.0.0.1:8080)" -ForegroundColor Red
        return $false
    }
    
    $ip = $matches[1]
    $port = [int]$matches[2]
    
    try {
        $udpClient = New-Object System.Net.Sockets.UdpClient
        $data = [System.Text.Encoding]::UTF8.GetBytes($message)
        $result = $udpClient.Send($data, $data.Length, $ip, $port)
        $udpClient.Close()
        
        Write-Host "SUCCESS: UDP sent to $target ($result bytes)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "ERROR: UDP failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Send-COMMessage {
    param([string]$target, [string]$message)
    
    try {
        $port = New-Object System.IO.Ports.SerialPort $target, 9600
        $port.Open()
        $port.WriteLine($message)
        $port.Close()
        
        Write-Host "SUCCESS: COM sent to $target" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "ERROR: COM failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "TIP: Use VSPE for virtual COM ports or connect real hardware" -ForegroundColor Yellow
        return $false
    }
}

function Send-PipeMessage {
    param([string]$target, [string]$message)
    
    try {
        $pipeName = "\\.\pipe\$target"
        $pipeClient = New-Object System.IO.Pipes.NamedPipeClientStream(".", $target, [System.IO.Pipes.PipeDirection]::Out)
        $pipeClient.Connect(2000) # 2 second timeout
        
        $writer = New-Object System.IO.StreamWriter($pipeClient)
        $writer.WriteLine($message)
        $writer.Flush()
        
        $writer.Close()
        $pipeClient.Close()
        
        Write-Host "SUCCESS: PIPE sent to $target" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "ERROR: PIPE failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Send-USBMessage {
    param([string]$target, [string]$message)
    
    Write-Host "USB Device Testing:" -ForegroundColor Cyan
    Write-Host "USB testing requires real hardware:" -ForegroundColor Yellow
    Write-Host "   1. Connect Arduino, ESP32, or similar USB device" -ForegroundColor White
    Write-Host "   2. Start your Wiretap application" -ForegroundColor White
    Write-Host "   3. Add a USB listener and select your device" -ForegroundColor White
    Write-Host "   4. Start the listener to monitor USB device activity" -ForegroundColor White
    Write-Host ""
    Write-Host "Recommended hardware for testing:" -ForegroundColor Cyan
    Write-Host "   * Arduino Uno/Nano (~$8-20)" -ForegroundColor White
    Write-Host "   * ESP32 Dev Board (~$8-15)" -ForegroundColor White
    Write-Host "   * FTDI USB-to-Serial adapter (~$5-15)" -ForegroundColor White
    Write-Host ""
    Write-Host "SUCCESS: USB testing guidance provided" -ForegroundColor Green
    return $true
}

function Test-Protocol {
    param([string]$protocol, [string]$target, [string]$message, [int]$count, [int]$interval)
    
    Write-Host ""
    Write-Host "Testing $protocol Protocol" -ForegroundColor Cyan
    Write-Host "Target: $target" -ForegroundColor Gray
    Write-Host "Message: $message" -ForegroundColor Gray
    Write-Host "Count: $count, Interval: $interval ms" -ForegroundColor Gray
    Write-Host ""
    
    $successCount = 0
    
    for ($i = 1; $i -le $count; $i++) {
        $timestamp = Get-Date -Format "HH:mm:ss.fff"
        $testMessage = "$message [$i/$count] at $timestamp"
        
        Write-Host "SENDING [$i/$count] " -NoNewline -ForegroundColor Blue
        
        $success = switch ($protocol.ToUpper()) {
            "TCP" { Send-TCPMessage $target $testMessage }
            "UDP" { Send-UDPMessage $target $testMessage }
            "COM" { Send-COMMessage $target $testMessage }
            "PIPE" { Send-PipeMessage $target $testMessage }
            "USB" { Send-USBMessage $target $testMessage }
            default { 
                Write-Host "ERROR: Unknown protocol: $protocol" -ForegroundColor Red
                $false
            }
        }
        
        if ($success) { $successCount++ }
        
        if ($i -lt $count) {
            Start-Sleep -Milliseconds $interval
        }
    }
    
    Write-Host ""
    Write-Host "Results: $successCount/$count successful" -ForegroundColor $(if ($successCount -eq $count) { "Green" } else { "Yellow" })
    
    if ($successCount -gt 0 -and $protocol.ToUpper() -ne "USB") {
        Write-Host "Check your Wiretap application for received messages!" -ForegroundColor Cyan
    }
}

function Start-InteractiveMode {
    Write-Host ""
    Write-Host "Interactive Mode" -ForegroundColor Yellow
    Write-Host ""
    
    # Show protocol options
    Write-Host "Available Protocols:" -ForegroundColor Cyan
    $protocols = @("TCP", "UDP", "COM", "PIPE", "USB")
    for ($i = 0; $i -lt $protocols.Count; $i++) {
        $info = $ProtocolInfo[$protocols[$i]]
        Write-Host "  $($i + 1). $($protocols[$i]) - $($info.Description)" -ForegroundColor White
        Write-Host "      Setup: $($info.Setup)" -ForegroundColor Gray
    }
    
    Write-Host ""
    $choice = Read-Host "Select protocol (1-5)"
    
    if ($choice -match '^[1-5]$') {
        $selectedProtocol = $protocols[[int]$choice - 1]
        $info = $ProtocolInfo[$selectedProtocol]
        
        Write-Host ""
        Write-Host "Selected: $selectedProtocol" -ForegroundColor Green
        Write-Host "Default target: $($info.DefaultTarget)" -ForegroundColor Gray
        Write-Host "Setup: $($info.Setup)" -ForegroundColor Gray
        
        $targetInput = Read-Host "Enter target (or press Enter for default)"
        $selectedTarget = if ($targetInput) { $targetInput } else { $info.DefaultTarget }
        
        $messageInput = Read-Host "Enter message (or press Enter for default)"
        $selectedMessage = if ($messageInput) { $messageInput } else { $Message }
        
        Test-Protocol $selectedProtocol $selectedTarget $selectedMessage $Count $Interval
    } else {
        Write-Host "ERROR: Invalid selection" -ForegroundColor Red
    }
}

# Main execution
if ($Interactive) {
    Start-InteractiveMode
} elseif ($Protocol) {
    if ($ProtocolInfo.ContainsKey($Protocol.ToUpper())) {
        $selectedTarget = if ($Target) { $Target } else { $ProtocolInfo[$Protocol.ToUpper()].DefaultTarget }
        Test-Protocol $Protocol.ToUpper() $selectedTarget $Message $Count $Interval
    } else {
        Write-Host "ERROR: Unknown protocol: $Protocol" -ForegroundColor Red
        Write-Host "Available protocols: TCP, UDP, COM, PIPE, USB" -ForegroundColor Yellow
    }
} else {
    Write-Host "ERROR: No protocol specified. Use -Protocol, -Interactive, or -ListAll" -ForegroundColor Red
    Write-Host "Run with -Help for usage examples" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Quick Reference:" -ForegroundColor Cyan
Write-Host "  TCP:  .\Test.ps1 -Protocol TCP -Target '127.0.0.1:9090'" -ForegroundColor White
Write-Host "  UDP:  .\Test.ps1 -Protocol UDP -Target '127.0.0.1:8080'" -ForegroundColor White
Write-Host "  COM:  .\Test.ps1 -Protocol COM -Target 'COM3'" -ForegroundColor White
Write-Host "  PIPE: .\Test.ps1 -Protocol PIPE -Target 'TestPipe'" -ForegroundColor White
Write-Host "  USB:  Connect real hardware and use Wiretap app directly" -ForegroundColor White