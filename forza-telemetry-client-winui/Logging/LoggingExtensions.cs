using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ForzaTelemetryClient.Logging
{
    public static class LoggingExtensions
    {
        private static readonly Stopwatch HighResTimer = Stopwatch.StartNew();

        /// <summary>
        /// Logs execution time of a code block with minimal overhead
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDisposable MeasureTime(string operationName)
        {
            return new TimeMeasurement(operationName);
        }

        private sealed class TimeMeasurement : IDisposable
        {
            private readonly string _operationName;
            private readonly long _startTicks;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TimeMeasurement(string operationName)
            {
                _operationName = operationName;
                _startTicks = HighResTimer.ElapsedTicks;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                var elapsedTicks = HighResTimer.ElapsedTicks - _startTicks;
                var elapsedMs = (elapsedTicks * 1000.0) / Stopwatch.Frequency;
                
                SafeLogger.LogPerformanceMetric(_operationName, elapsedMs, "ms");
            }
        }

        /// <summary>
        /// Conditional logging based on verbosity level
        /// </summary>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogDebug(this object source, string message)
        {
            #if DEBUG
            SafeLogger.LogError($"DEBUG: {source?.GetType().Name}", message);
            #endif
        }
    }
}