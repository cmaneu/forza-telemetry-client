using System;
using System.Diagnostics.Tracing;

namespace ForzaTelemetryClient.Logging.Configuration
{
    /// <summary>
    /// Production-optimized logging configuration with DebugView support
    /// </summary>
    public class ProductionLoggingConfiguration
    {
        public enum ProductionMode
        {
            /// <summary>Minimal logging - errors and critical warnings only</summary>
            Minimal,
            /// <summary>Standard production logging - key events and metrics</summary>
            Standard,
            /// <summary>Diagnostic mode - verbose logging for troubleshooting</summary>
            Diagnostic,
            /// <summary>Performance analysis - timing and metrics focused</summary>
            Performance
        }

        public bool IsEnabled { get; set; } = true;
        public ProductionMode Mode { get; set; } = ProductionMode.Standard;
        public bool UseDebugView { get; set; } = true;
        public bool UseETW { get; set; } = false;
        public int TelemetrySamplingRate { get; set; } = 1000; // Log 1 in 1000 telemetry packets

        /// <summary>
        /// Apply production configuration
        /// </summary>
        public void Apply()
        {
            // Configure based on mode
            switch (Mode)
            {
                case ProductionMode.Minimal:
                    ConfigureMinimal();
                    break;
                case ProductionMode.Standard:
                    ConfigureStandard();
                    break;
                case ProductionMode.Diagnostic:
                    ConfigureDiagnostic();
                    break;
                case ProductionMode.Performance:
                    ConfigurePerformance();
                    break;
            }

            // Log configuration
            DebugViewLogger.LogInfo($"Production logging configured: Mode={Mode}, DebugView={UseDebugView}, ETW={UseETW}");
        }

        private void ConfigureMinimal()
        {
            SafeLogger.IsEnabled = UseETW;
            DebugViewLogger.IsEnabled = UseDebugView;
            DebugViewLogger.EnableHighFrequencyLogging = false;
            
            var backend = HybridLogger.LoggingBackend.None;
            if (UseDebugView) backend |= HybridLogger.LoggingBackend.DebugView;
            if (UseETW) backend |= HybridLogger.LoggingBackend.ETW;
            HybridLogger.Backend = backend;
        }

        private void ConfigureStandard()
        {
            SafeLogger.IsEnabled = UseETW;
            DebugViewLogger.IsEnabled = UseDebugView;
            DebugViewLogger.EnableHighFrequencyLogging = false;
            DebugViewLogger.SamplingRate = TelemetrySamplingRate;
            
            var backend = HybridLogger.LoggingBackend.None;
            if (UseDebugView) backend |= HybridLogger.LoggingBackend.DebugView;
            if (UseETW) backend |= HybridLogger.LoggingBackend.ETW;
            HybridLogger.Backend = backend;
        }

        private void ConfigureDiagnostic()
        {
            SafeLogger.IsEnabled = UseETW;
            DebugViewLogger.IsEnabled = UseDebugView;
            DebugViewLogger.EnableHighFrequencyLogging = true;
            DebugViewLogger.SamplingRate = 100; // More frequent sampling for diagnostics
            
            HybridLogger.Backend = HybridLogger.LoggingBackend.Both;
        }

        private void ConfigurePerformance()
        {
            SafeLogger.IsEnabled = UseETW;
            DebugViewLogger.IsEnabled = UseDebugView;
            DebugViewLogger.EnableHighFrequencyLogging = false;
            DebugViewLogger.SamplingRate = TelemetrySamplingRate;
            
            // For performance analysis, prefer ETW but allow DebugView for quick checks
            var backend = HybridLogger.LoggingBackend.None;
            if (UseDebugView) backend |= HybridLogger.LoggingBackend.DebugView;
            if (UseETW) backend |= HybridLogger.LoggingBackend.ETW;
            HybridLogger.Backend = backend;
        }

        /// <summary>
        /// Create configuration from environment variables for production flexibility
        /// </summary>
        public static ProductionLoggingConfiguration FromEnvironment()
        {
            var config = new ProductionLoggingConfiguration();

            // Read from environment variables
            var modeStr = Environment.GetEnvironmentVariable("FORZA_LOG_MODE");
            if (Enum.TryParse<ProductionMode>(modeStr, out var mode))
            {
                config.Mode = mode;
            }

            var debugViewStr = Environment.GetEnvironmentVariable("FORZA_LOG_DEBUGVIEW");
            if (bool.TryParse(debugViewStr, out var useDebugView))
            {
                config.UseDebugView = useDebugView;
            }

            var etwStr = Environment.GetEnvironmentVariable("FORZA_LOG_ETW");
            if (bool.TryParse(etwStr, out var useETW))
            {
                config.UseETW = useETW;
            }

            var samplingStr = Environment.GetEnvironmentVariable("FORZA_LOG_SAMPLING");
            if (int.TryParse(samplingStr, out var sampling) && sampling > 0)
            {
                config.TelemetrySamplingRate = sampling;
            }

            return config;
        }
    }
}