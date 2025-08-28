using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ForzaTelemetryClient.Logging
{
    /// <summary>
    /// Production-ready logger for SysInternals DebugView compatibility
    /// Uses OutputDebugString for minimal overhead debug output
    /// </summary>
    public static class DebugViewLogger
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern void OutputDebugString(string message);

        private static readonly ThreadLocal<StringBuilder> StringBuilderCache = 
            new ThreadLocal<StringBuilder>(() => new StringBuilder(256));

        private static volatile bool _isEnabled = true;
        private static volatile int _failureCount = 0;
        private const int MaxFailures = 10;
        private const string Prefix = "[ForzaTelemetry] ";

        // Production-friendly feature flags
        private static volatile bool _enableHighFrequencyLogging = false;
        private static volatile int _samplingRate = 100; // Log 1 out of N high-freq messages
        private static volatile int _sampleCounter = 0;

        public static bool IsEnabled
        {
            get => _isEnabled && _failureCount < MaxFailures;
            set => _isEnabled = value;
        }

        /// <summary>
        /// Enable/disable high-frequency telemetry logging (off by default in production)
        /// </summary>
        public static bool EnableHighFrequencyLogging
        {
            get => _enableHighFrequencyLogging;
            set => _enableHighFrequencyLogging = value;
        }

        /// <summary>
        /// Sampling rate for high-frequency data (1 = log all, 100 = log 1 in 100)
        /// </summary>
        public static int SamplingRate
        {
            get => _samplingRate;
            set => _samplingRate = Math.Max(1, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogTelemetry(float speed, float rpm, float gear)
        {
            if (!IsEnabled || !_enableHighFrequencyLogging) return;

            // Sample high-frequency data to avoid flooding in production
            if (Interlocked.Increment(ref _sampleCounter) % _samplingRate != 0) return;

            try
            {
                var sb = StringBuilderCache.Value;
                sb.Clear();
                sb.Append(Prefix)
                  .Append("TELEM: Speed=").Append(speed.ToString("F1"))
                  .Append(" RPM=").Append(rpm.ToString("F0"))
                  .Append(" Gear=").Append(gear.ToString("F0"));

                OutputDebugString(sb.ToString());
            }
            catch
            {
                HandleFailure();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogPacket(int size, int sequence)
        {
            if (!IsEnabled) return;

            // Always log packet info in production for connection monitoring
            try
            {
                var sb = StringBuilderCache.Value;
                sb.Clear();
                sb.Append(Prefix)
                  .Append("PACKET: Size=").Append(size)
                  .Append(" Seq=").Append(sequence)
                  .Append(" Time=").Append(DateTime.UtcNow.ToString("HH:mm:ss.fff"));

                OutputDebugString(sb.ToString());
            }
            catch
            {
                HandleFailure();
            }
        }

        public static void LogInfo(string message)
        {
            if (!IsEnabled) return;

            try
            {
                OutputDebugString($"{Prefix}INFO [{DateTime.UtcNow:HH:mm:ss.fff}]: {message}");
            }
            catch
            {
                HandleFailure();
            }
        }

        public static void LogWarning(string message)
        {
            // Always attempt to log warnings in production
            try
            {
                OutputDebugString($"{Prefix}WARN [{DateTime.UtcNow:HH:mm:ss.fff}]: {message}");
            }
            catch
            {
                HandleFailure();
            }
        }

        public static void LogError(string message, Exception ex = null)
        {
            // Always attempt to log errors in production
            try
            {
                var errorMsg = ex != null 
                    ? $"{Prefix}ERROR [{DateTime.UtcNow:HH:mm:ss.fff}]: {message} - {ex.GetType().Name}: {ex.Message}" 
                    : $"{Prefix}ERROR [{DateTime.UtcNow:HH:mm:ss.fff}]: {message}";
                
                OutputDebugString(errorMsg);

                // Log stack trace on separate lines for readability
                if (ex?.StackTrace != null && IsEnabled)
                {
                    var lines = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        OutputDebugString($"{Prefix}  STACK: {line.Trim()}");
                    }
                }
            }
            catch
            {
                HandleFailure();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogTiming(string operation, double milliseconds)
        {
            if (!IsEnabled) return;

            // Only log slow operations in production (configurable threshold)
            const double SLOW_OPERATION_THRESHOLD_MS = 10.0;
            if (milliseconds < SLOW_OPERATION_THRESHOLD_MS) return;

            try
            {
                var sb = StringBuilderCache.Value;
                sb.Clear();
                sb.Append(Prefix)
                  .Append("PERF_SLOW: ").Append(operation)
                  .Append(" = ").Append(milliseconds.ToString("F2"))
                  .Append("ms");

                OutputDebugString(sb.ToString());
            }
            catch
            {
                HandleFailure();
            }
        }

        /// <summary>
        /// Production diagnostics - log system state
        /// </summary>
        public static void LogDiagnostics(string category, string details)
        {
            if (!IsEnabled) return;

            try
            {
                OutputDebugString($"{Prefix}DIAG [{DateTime.UtcNow:HH:mm:ss.fff}] {category}: {details}");
            }
            catch
            {
                HandleFailure();
            }
        }

        /// <summary>
        /// Log critical production metrics
        /// </summary>
        public static void LogMetric(string name, double value, string unit = "")
        {
            if (!IsEnabled) return;

            try
            {
                var unitStr = string.IsNullOrEmpty(unit) ? "" : $" {unit}";
                OutputDebugString($"{Prefix}METRIC: {name}={value:F2}{unitStr}");
            }
            catch
            {
                HandleFailure();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HandleFailure()
        {
            Interlocked.Increment(ref _failureCount);
        }

        public static void ResetFailureCounter()
        {
            _failureCount = 0;
        }

        /// <summary>
        /// Get current logger status for production monitoring
        /// </summary>
        public static string GetStatus()
        {
            return $"DebugView Logger - Enabled: {IsEnabled}, Failures: {_failureCount}/{MaxFailures}, HighFreq: {_enableHighFrequencyLogging}, Sampling: 1/{_samplingRate}";
        }
    }
}