using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ForzaTelemetryClient.Logging
{
    /// <summary>
    /// Hybrid logger that supports both ETW and DebugView output
    /// Allows runtime switching between logging backends
    /// </summary>
    public static class HybridLogger
    {
        public enum LoggingBackend
        {
            None = 0,
            ETW = 1,
            DebugView = 2,
            Both = 3
        }

        private static LoggingBackend _backend = LoggingBackend.DebugView;
        private static readonly Stopwatch Timer = Stopwatch.StartNew();

        public static LoggingBackend Backend
        {
            get => _backend;
            set => _backend = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogPacketReceived(int size, long timestamp)
        {
            if (_backend == LoggingBackend.None) return;

            if ((_backend & LoggingBackend.ETW) != 0)
            {
                SafeLogger.LogTelemetryReceived(size, timestamp);
            }

            if ((_backend & LoggingBackend.DebugView) != 0)
            {
                DebugViewLogger.LogPacket(size, (int)(timestamp % int.MaxValue));
            }
        }

        public static void LogConnection(string endpoint, int port)
        {
            if (_backend == LoggingBackend.None) return;

            if ((_backend & LoggingBackend.ETW) != 0)
            {
                SafeLogger.LogConnectionEstablished(endpoint, port);
            }

            if ((_backend & LoggingBackend.DebugView) != 0)
            {
                DebugViewLogger.LogInfo($"Connected to {endpoint}:{port}");
            }
        }

        public static void LogError(string error, Exception ex = null)
        {
            if (_backend == LoggingBackend.None) return;

            if ((_backend & LoggingBackend.ETW) != 0)
            {
                SafeLogger.LogError(error, ex?.ToString());
            }

            if ((_backend & LoggingBackend.DebugView) != 0)
            {
                DebugViewLogger.LogError(error, ex);
            }
        }

        public static IDisposable MeasureTime(string operationName)
        {
            if (_backend == LoggingBackend.None) 
                return NoOpDisposable.Instance;

            return new TimeMeasurement(operationName, _backend);
        }

        private sealed class TimeMeasurement : IDisposable
        {
            private readonly string _operationName;
            private readonly long _startTicks;
            private readonly LoggingBackend _backend;

            public TimeMeasurement(string operationName, LoggingBackend backend)
            {
                _operationName = operationName;
                _backend = backend;
                _startTicks = Timer.ElapsedTicks;
            }

            public void Dispose()
            {
                var elapsedTicks = Timer.ElapsedTicks - _startTicks;
                var elapsedMs = (elapsedTicks * 1000.0) / Stopwatch.Frequency;

                if ((_backend & LoggingBackend.ETW) != 0)
                {
                    SafeLogger.LogPerformanceMetric(_operationName, elapsedMs);
                }

                if ((_backend & LoggingBackend.DebugView) != 0)
                {
                    DebugViewLogger.LogTiming(_operationName, elapsedMs);
                }
            }
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new NoOpDisposable();
            public void Dispose() { }
        }
    }
}