using System;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

namespace ForzaTelemetryClient.Logging
{
    [EventSource(Name = "ForzaTelemetry-ETW")]
    public sealed class TelemetryEventSource : EventSource
    {
        private static readonly Lazy<TelemetryEventSource> Instance = 
            new Lazy<TelemetryEventSource>(() => new TelemetryEventSource());

        public static TelemetryEventSource Log => Instance.Value;

        private TelemetryEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat)
        {
        }

        // Event IDs
        public class Events
        {
            public const int TelemetryPacketReceived = 1;
            public const int TelemetryPacketProcessed = 2;
            public const int ConnectionEstablished = 3;
            public const int ConnectionLost = 4;
            public const int DataProcessingError = 5;
            public const int PerformanceMetric = 6;
            public const int ConfigurationChanged = 7;
            public const int SessionStarted = 8;
            public const int SessionEnded = 9;
            public const int HighFrequencyData = 10;
            public const int ConfigurationLoaded = 11;
            public const int UnhandledException = 12;
            public const int BindingFailed = 13;
        }

        [Event(Events.TelemetryPacketReceived, Level = EventLevel.Verbose, Channel = EventChannel.Analytic)]
        public void TelemetryPacketReceived(int packetSize, long timestamp)
        {
            if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            {
                WriteEvent(Events.TelemetryPacketReceived, packetSize, timestamp);
            }
        }

        [Event(Events.TelemetryPacketProcessed, Level = EventLevel.Verbose, Channel = EventChannel.Analytic)]
        public void TelemetryPacketProcessed(int packetId, double processingTimeMs)
        {
            if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            {
                WriteEvent(Events.TelemetryPacketProcessed, packetId, processingTimeMs);
            }
        }

        [Event(Events.ConnectionEstablished, Level = EventLevel.Informational)]
        public void ConnectionEstablished(string endpoint, int port)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(Events.ConnectionEstablished, endpoint ?? "unknown", port);
            }
        }

        [Event(Events.ConnectionLost, Level = EventLevel.Warning)]
        public void ConnectionLost(string reason)
        {
            if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                WriteEvent(Events.ConnectionLost, reason ?? "unknown");
            }
        }

        [Event(Events.DataProcessingError, Level = EventLevel.Error)]
        public void DataProcessingError(string error, string details)
        {
            if (IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                WriteEvent(Events.DataProcessingError, error ?? "unknown", details ?? "");
            }
        }

        [Event(Events.PerformanceMetric, Level = EventLevel.Informational)]
        public void PerformanceMetric(string metricName, double value, string unit)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(Events.PerformanceMetric, metricName ?? "unknown", value, unit ?? "ms");
            }
        }

        [NonEvent]
        public void LogSessionInfo(string sessionId, DateTime startTime, string gameVersion)
        {
            if (IsEnabled())
            {
                SessionStarted(sessionId, startTime.ToString("O"), gameVersion);
            }
        }

        [Event(Events.SessionStarted, Level = EventLevel.Informational)]
        private void SessionStarted(string sessionId, string startTime, string gameVersion)
        {
            WriteEvent(Events.SessionStarted, sessionId, startTime, gameVersion);
        }

        [Event(Events.SessionEnded, Level = EventLevel.Informational)]
        public void SessionEnded(string sessionId, DateTime endTime)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(Events.SessionEnded, sessionId ?? "unknown", endTime.ToString("O"));
            }
        }

        [Event(Events.ConfigurationLoaded, Level = EventLevel.Informational)]
        public void ConfigurationLoaded(string filePath, string summary)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(Events.ConfigurationLoaded, filePath ?? "unknown", summary ?? "");
            }
        }

        [Event(Events.UnhandledException, Level = EventLevel.Error)]
        public void UnhandledException(
            string exceptionType,
            int hresult,
            string message,
            string stackTrace,
            string source,
            string targetSite,
            string innerExceptionType,
            string innerExceptionMessage,
            bool handled)
        {
            if (IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                WriteEvent(
                    Events.UnhandledException,
                    exceptionType ?? "",
                    hresult,
                    message ?? "",
                    stackTrace ?? "",
                    source ?? "",
                    targetSite ?? "",
                    innerExceptionType ?? "",
                    innerExceptionMessage ?? "",
                    handled);
            }
        }

        [Event(Events.BindingFailed, Level = EventLevel.Warning)]
        public void BindingFailed(string message)
        {
            if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                WriteEvent(Events.BindingFailed, message ?? "");
            }
        }
    }
}