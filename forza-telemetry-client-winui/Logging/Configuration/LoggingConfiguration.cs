using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace ForzaTelemetryClient.Logging.Configuration
{
    /// <summary>
    /// Configuration for logging behavior supporting multiple backends
    /// </summary>
    public class LoggingConfiguration
    {
        public bool IsEnabled { get; set; } = true;
        public EventLevel MinimumLevel { get; set; } = EventLevel.Informational;
        public bool LogHighFrequencyData { get; set; } = false;
        public int MaxBufferSizeMB { get; set; } = 100;
        public HybridLogger.LoggingBackend PreferredBackend { get; set; }

        public LoggingConfiguration()
        {
            // Auto-detect best backend
            PreferredBackend = DetectBestBackend();
        }

        /// <summary>
        /// Auto-detect the most appropriate logging backend
        /// </summary>
        private HybridLogger.LoggingBackend DetectBestBackend()
        {
            // If debugger is attached, prefer DebugView for immediate feedback
            if (Debugger.IsAttached)
            {
                return HybridLogger.LoggingBackend.DebugView;
            }

            // In Release builds, prefer ETW for performance
            #if !DEBUG
            return HybridLogger.LoggingBackend.ETW;
            #else
            // In Debug builds without debugger, use both
            return HybridLogger.LoggingBackend.Both;
            #endif
        }

        /// <summary>
        /// Apply configuration to the logging system
        /// </summary>
        public void Apply()
        {
            SafeLogger.IsEnabled = IsEnabled;
            DebugViewLogger.IsEnabled = IsEnabled;
            HybridLogger.Backend = PreferredBackend;

            // Log configuration for diagnostics
            if (IsEnabled)
            {
                var configMsg = $"Logging configured: Backend={PreferredBackend}, Level={MinimumLevel}, HighFreq={LogHighFrequencyData}";
                DebugViewLogger.LogInfo(configMsg);
            }
        }

        /// <summary>
        /// Create configuration optimized for production
        /// </summary>
        public static LoggingConfiguration Production()
        {
            return new LoggingConfiguration
            {
                IsEnabled = true,
                MinimumLevel = EventLevel.Warning,
                LogHighFrequencyData = false,
                PreferredBackend = HybridLogger.LoggingBackend.ETW
            };
        }

        /// <summary>
        /// Create configuration optimized for development
        /// </summary>
        public static LoggingConfiguration Development()
        {
            return new LoggingConfiguration
            {
                IsEnabled = true,
                MinimumLevel = EventLevel.Verbose,
                LogHighFrequencyData = true,
                PreferredBackend = HybridLogger.LoggingBackend.DebugView
            };
        }

        /// <summary>
        /// Create configuration for performance testing (minimal overhead)
        /// </summary>
        public static LoggingConfiguration PerformanceTesting()
        {
            return new LoggingConfiguration
            {
                IsEnabled = false,
                PreferredBackend = HybridLogger.LoggingBackend.None
            };
        }
    }
}