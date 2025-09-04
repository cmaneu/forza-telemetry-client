using ForzaBridge.Model;
using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

namespace ForzaTelemetryClient.Logging
{
    /// <summary>
    /// Defensive wrapper for logging operations to ensure exceptions in logging
    /// never affect the main application flow
    /// </summary>
    public static class SafeLogger
    {
        private static readonly TelemetryEventSource EventSource = TelemetryEventSource.Log;
        private static volatile bool _isEnabled = true;
        private static volatile int _failureCount = 0;
        private const int MaxFailures = 10;

        /// <summary>
        /// Globally enable/disable logging
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled && _failureCount < MaxFailures;
            set => _isEnabled = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogTelemetryReceived(int packetSize, long timestamp)
        {
            if (!IsEnabled) return;
            
            try
            {
                EventSource.TelemetryPacketReceived(packetSize, timestamp);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogTelemetryProcessed(int packetId, double processingTimeMs)
        {
            if (!IsEnabled) return;
            
            try
            {
                EventSource.TelemetryPacketProcessed(packetId, processingTimeMs);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        public static void LogConnectionEstablished(string endpoint, int port)
        {
            if (!IsEnabled) return;
            
            try
            {
                EventSource.ConnectionEstablished(endpoint, port);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        public static void LogConnectionLost(string reason)
        {
            if (!IsEnabled) return;
            
            try
            {
                EventSource.ConnectionLost(reason);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        public static void LogError(string error, string details = null)
        {
            if (!IsEnabled) return;
            
            try
            {
                EventSource.DataProcessingError(error, details);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        public static void LogPerformanceMetric(string metricName, double value, string unit = "ms")
        {
            if (!IsEnabled) return;
            
            try
            {
                EventSource.PerformanceMetric(metricName, value, unit);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        public static void LogSessionStart(string sessionId, DateTime startTime, string gameVersion)
        {
            if (!IsEnabled) return;
            
            try
            {
                EventSource.LogSessionInfo(sessionId, startTime, gameVersion);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }


        internal static void LogSessionOver(string? sessionId)
        {
            if (!IsEnabled) return;

            try
            {
                EventSource.SessionEnded(sessionId,DateTime.Now);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        public static void LogConfigurationLoaded(
            string filePath,
            string ip,
            int port,
            string dataMode,
            int dataRate,
            string eventHubNamespace,
            string eventHubName,
            string eventEncoding,
            string cloudEventEncoding)
        {
            if (!IsEnabled) return;

            try
            {
                var summary =
                    $"ip={ip};port={port};dataMode={dataMode};dataRate={dataRate};" +
                    $"ehNamespace={(eventHubNamespace ?? "n/a")};ehName={(eventHubName ?? "n/a")};" +
                    $"eventEncoding={eventEncoding};cloudEventEncoding={cloudEventEncoding}";
                EventSource.ConfigurationLoaded(filePath, summary);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        // New: Log unhandled exceptions with full details
        public static void LogUnhandledException(Exception ex, bool handled)
        {
            if (!IsEnabled || ex == null) return;

            try
            {
                EventSource.UnhandledException(
                    ex.GetType().FullName,
                    ex.HResult,
                    ex.Message,
                    ex.StackTrace,
                    ex.Source,
                    ex.TargetSite?.ToString(),
                    ex.InnerException?.GetType().FullName,
                    ex.InnerException?.Message,
                    handled);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        // New: Log XAML binding failures
        public static void LogBindingFailed(string message)
        {
            if (!IsEnabled) return;

            try
            {
                EventSource.BindingFailed(message);
            }
            catch
            {
                HandleLoggingFailure();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HandleLoggingFailure()
        {
            // Increment failure count and disable logging if too many failures
            // This prevents logging issues from continuously affecting performance
            System.Threading.Interlocked.Increment(ref _failureCount);
            
            if (_failureCount >= MaxFailures)
            {
                _isEnabled = false;
                Debug.WriteLine($"Logging disabled after {MaxFailures} failures");
            }
        }

        /// <summary>
        /// Reset the failure counter (useful for testing or recovery scenarios)
        /// </summary>
        public static void ResetFailureCounter()
        {
            _failureCount = 0;
            _isEnabled = true;
        }

        internal static void LogStartingListening(DataMode dataMode, int dataRate)
        {
            EventSource.StartListening(dataMode.ToString(), dataRate);
        }

        internal static void LogListeningStarted(IPAddress ipAddress, int port)
        {
            EventSource.ListeningStarted(ipAddress.ToString(), port);
        }

        internal static void LogTelemetrySent(string sessionId, string name, string effectiveCarId, string effectiveLapId, long startTS, long endTS)
        {
            EventSource.TelemetrySent(sessionId, name, effectiveCarId, effectiveLapId, startTS, endTS);
        }
    }
}