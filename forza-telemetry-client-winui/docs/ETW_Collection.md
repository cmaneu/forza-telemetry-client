# ETW Event Collection Guide

## Collecting ETW Events

### Using PerfView (Recommended)
```powershell
# Start collection
PerfView collect -OnlyProviders:*ForzaTelemetry-ETW -CircularMB:100 -NoGui

# Stop with Ctrl+C when done
```

### Using WPA (Windows Performance Analyzer)
1. Create a custom profile for ForzaTelemetry-ETW provider
2. Start trace session
3. Analyze in WPA

### Using logman (Built-in Windows)
```cmd
# Create trace session
logman create trace ForzaTelemetry -p "ForzaTelemetry-ETW" -o telemetry.etl

# Start trace
logman start ForzaTelemetry

# Stop trace
logman stop ForzaTelemetry

# Delete trace session
logman delete ForzaTelemetry
```

## Analyzing Events

### Convert ETL to CSV
```powershell
tracerpt telemetry.etl -o telemetry.csv -of CSV
```

### Real-time monitoring
```powershell
# Use WPA or PerfView for real-time event viewing
```

## Performance Tips
- ETW has near-zero overhead when no consumers are attached
- Use EventLevel.Verbose sparingly for high-frequency data
- Consider using circular buffers for long-running sessions