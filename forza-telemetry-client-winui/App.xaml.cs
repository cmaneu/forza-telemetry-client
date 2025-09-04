using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using ForzaTelemetryClient.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace forza_telemetry_client_winui
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static string TelemetryMode { get; private set; } = "Live";
        public static string TelemetryFilePath { get; private set; } = "";

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Parse command line arguments
            ParseCommandLineArguments();

            // Global exception logging
            this.UnhandledException += (sender, e) =>
            {
                SafeLogger.LogUnhandledException(e.Exception, e.Handled);
            };

#if DEBUG && !DISABLE_XAML_GENERATED_BINDING_DEBUG_OUTPUT
            // Log XAML binding failures
            this.DebugSettings.BindingFailed += (sender, e) =>
            {
                SafeLogger.LogBindingFailed(e.Message);
            };
#endif
        }

        private void ParseCommandLineArguments()
        {
            var args = System.Environment.GetCommandLineArgs();
            
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--mode":
                        if (i + 1 < args.Length)
                        {
                            var mode = args[i + 1].ToLower();
                            switch (mode)
                            {
                                case "dump":
                                    TelemetryMode = "Dump";
                                    break;
                                case "replay":
                                    TelemetryMode = "Replay";
                                    break;
                                default:
                                    TelemetryMode = "Live";
                                    break;
                            }
                            i++; // Skip next argument since we consumed it
                        }
                        break;
                    case "--file":
                        if (i + 1 < args.Length)
                        {
                            TelemetryFilePath = args[i + 1];
                            i++; // Skip next argument since we consumed it
                        }
                        break;
                }
            }

            // Set default file path if not specified
            if (string.IsNullOrEmpty(TelemetryFilePath))
            {
                TelemetryFilePath = TelemetryMode == "Dump" 
                    ? $"telemetry_dump_{DateTime.Now:yyyyMMdd_HHmmss}.fbs"
                    : "telemetry_session.fbs";
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window? m_window;
    }
}
