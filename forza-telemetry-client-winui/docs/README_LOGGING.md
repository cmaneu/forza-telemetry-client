# Forza Telemetry Client - Logging Infrastructure

## Overview
This logging infrastructure provides high-performance, defensive logging for the Forza Telemetry Client using Event Tracing for Windows (ETW).

## Key Features

### 🛡️ Defensive Design
- All logging operations are wrapped in try-catch blocks
- Automatic disabling after repeated failures
- Zero impact on main application flow if logging fails

### ⚡ High Performance
- ETW-based for minimal overhead
- Near-zero cost when no listeners attached
- Optimized for 60+ Hz telemetry data
- Aggressive inlining for hot paths

### 📊 Structured Logging
- Strongly-typed events
- Consistent event schema
- Built-in performance metrics
- Hierarchical event levels

## Usage

### Basic Integration
```csharp
using ForzaTelemetryClient.Logging;

// Log connection events
SafeLogger.LogConnectionEstablished("127.0.0.1", 5300);

// Measure operation timing
using (LoggingExtensions.MeasureTime("DatabaseQuery"))
{
    // Your code here
}
```

### Configuration
```csharp
var config = new LoggingConfiguration
{
    IsEnabled = true,
    MinimumLevel = EventLevel.Informational,
    LogHighFrequencyData = true
};
config.Apply();
```

## ETW Collection

### Quick Start
```powershell
# Collect events
PerfView collect -OnlyProviders:*ForzaTelemetry-ETW -CircularMB:100

# Analyze in real-time
PerfView userCommand Listen ForzaTelemetry-ETW
```

## Troubleshooting

### No events appearing
- Verify ETW provider is registered: `logman query providers | findstr Forza`
- Check Windows Event Log permissions
- Ensure running with appropriate privileges

### High CPU usage
- Reduce logging verbosity
- Disable high-frequency data logging
- Check for ETW buffer overruns

### Auto-disabled logging
- Check `SafeLogger.IsEnabled` status
- Call `SafeLogger.ResetFailureCounter()` to re-enable
- Review exception logs for root cause