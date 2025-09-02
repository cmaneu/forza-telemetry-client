using ForzaBridge.Model;
using ForzaTelemetryClient.Logging;
using Microsoft.Identity.Client.TelemetryCore.TelemetryClient;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Globalization.DateTimeFormatting;
using Windows.Media.Devices.Core;

namespace ForzaBridge.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {

        private float prevDistanceTraveled = 0;
        private DispatcherTimer statusTimer;
        


        private TelemetryDataSled telemetryDataSled;
        private TelemetryDataDash telemetryDataDash;



        //public TelemetryBasic TelemetryData
        public TelemetryDataSled TelemetryDataSled
        {
            get => telemetryDataSled;
            set
            {
                if (telemetryDataSled != value)
                {
                    telemetryDataSled = value;
                    RPMAngle = ConvertRPMToAngle((int)telemetryDataSled.CurrentEngineRpm);
                    OnPropertyChanged(nameof(TelemetryDataSled));
                    OnPropertyChanged(nameof(RPMAngle));
                    OnPropertyChanged(nameof(IsRaceStopped));


                    // The game is paused or stopped and the distance traveled is 0
                    // Assume the session is over.  To ensure this only triggers once, we need to check the previous state
                    if (telemetryDataSled.IsRaceOn == 0) 
                    {
                        // If the previous distance traveled was greater than 0 and the current distance traveled is 0
                        // Asssume we've just ended the race (or entered Pause)
                        if ((prevDistanceTraveled > 0) && (telemetryDataDash.DistanceTraveled == 0))
                        {
                            // Assume the session is over
                            prevDistanceTraveled = 0;
                            SafeLogger.LogSessionOver(Session?.SessionId);
                            SessionOverEvent?.Invoke(this, EventArgs.Empty);
                        }
                      
                    }

                    prevDistanceTraveled = telemetryDataDash?.DistanceTraveled ?? 0;

                }
            }
        }

        public TelemetryDataDash TelemetryDataDash
        {
            get => telemetryDataDash;

            set
            {
                if (telemetryDataDash != value)
                {
                    telemetryDataDash = value;
                    SpeedKPH = ConvertSpeedToKPH((int)telemetryDataDash.Speed);
                    FormattedCurrentLap = FormatLap(telemetryDataDash.CurrentLap);
                    FormattedLastLap = FormatLap(telemetryDataDash.LastLap);
                    telemetryDataDash.LapNumber ++;

                    OnPropertyChanged(nameof(TelemetryDataDash));
                    OnPropertyChanged(nameof(SpeedKPH));  
                    OnPropertyChanged(nameof(FormattedCurrentLap));
                    OnPropertyChanged(nameof(FormattedLastLap));

                }
            }
        }

        public bool IsRaceStopped
        {
            get
            {
                if (telemetryDataSled?.IsRaceOn == 1)
                {
                    return false;
                }
                else
                {
                    return true;
                }            
            }
        }



        private Session session;
        public Session Session
        {
            get => session;
            set
            {
                session = value;
                OnPropertyChanged(nameof(Session));  // Alert the UI once the session has changed
                model.UpdateSession(session);  // Update the model with the session details
            }
        }

        public string FormattedCurrentLap { get; set; }
        public string FormattedLastLap { get; set; }
        private string FormatLap(float value)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(value);
            return string.Format("{0:D2}:{1:D2}:{2:D3}", timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
        }

        public double RPMAngle { get; set; }
        private double ConvertRPMToAngle(int value)
        {
            // Convert RPM to angle
            double angle = 28;
            if (value <= 0) { angle = 28; }
            else if (value >= 10000) { angle = 270; }
            else { angle = (value / 41.32) + 28; }
            return angle;
        }

        public int SpeedKPH { get; set; }
        private int ConvertSpeedToKPH(int value)
        {
            // Convert speed in meters per second to KPH
            int speed = 0;
            speed = (int)Math.Round(value * 3.6);
            return speed;
        }


        private TelemetryModel model;
        public MainViewModel()
        {
            session = new Session("Anonymous", "", "");
            model = new TelemetryModel();

            // Initialise the model with a new session
            model.UpdateSession(session);

            // Subscribe to the telemetry received events
            model.SledTelemetryReceieved += (sled) => TelemetryDataSled = sled;
            model.DashTelemetryReceieved += (dash) => TelemetryDataDash = dash;
            
            // Subscribe to EventHub status changes
            model.EventHubStatusChanged += OnEventHubStatusChanged;
            
            // Start the status timer
            StartStatusTimer();

            Debug.WriteLine("MainViewModel created");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event EventHandler SessionOverEvent;

        // EventHub status properties
        public string EventHubStatus 
        { 
            get
            {
                if (!model.IsEventHubConfigured)
                    return "Not configured";
                
                if (!model.HasSentEvents)
                    return "Connected";
                
                var timeSinceLastSend = DateTime.UtcNow - model.LastEventSentTime;
                return timeSinceLastSend.TotalMinutes <= 1 ? "Sending" : "Idle";
            }
        }

        public string EventHubAddress => model?.EventHubAddress ?? "Not configured";

        private void StartStatusTimer()
        {
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(10); // Update every 10 seconds
            statusTimer.Tick += (sender, e) => 
            {
                OnPropertyChanged(nameof(EventHubStatus));
            };
            statusTimer.Start();
        }

        private void OnEventHubStatusChanged()
        {
            OnPropertyChanged(nameof(EventHubStatus));
            OnPropertyChanged(nameof(EventHubAddress));
        }


    }
}
