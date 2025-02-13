using Azure.Messaging.EventHubs;
using CloudNative.CloudEvents.SystemTextJson;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client.TelemetryCore.TelemetryClient;
using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Printing.Workflow;
using static forza_telemetry_client_winui.App;
using Vasters.ForzaBridge.ProducerData.ForzaMotorsport.Telemetry;
using Vasters.ForzaBridge.Producer.ForzaMotorsport.Telemetry;
using Azure.Identity;
using Azure.Messaging.EventHubs.Producer;
using Azure;

namespace ForzaBridge.Model
{

    public partial class TelemetryModel
    {
        private IConfiguration Configuration { get; set; }
        //private static string _sessionId { get; set; }
        //private int prevRaceStatus = -1;
        private Session session;

        public event Action<TelemetryDataSled> SledTelemetryReceieved;
        public event Action<TelemetryDataDash> DashTelemetryReceieved;


        public TelemetryModel() {
            InitializeTelemetryAsync();
        }

        private async void InitializeTelemetryAsync()
        {

            LoadConfiguration();

            // Create a default session
            //Session = new Session("default",null,null);

            await StartListening();
        }


        public void UpdateSession(Session newSession)
        {
            session = newSession;
        }

        // Loads the configuration file from the appsettings.json file
        private void LoadConfiguration()
        {
            string configFilePath = AppContext.BaseDirectory + "/config/appsettings.json"; // Path to your configuration file

            if (!File.Exists(configFilePath))
            {
                throw new FileNotFoundException($"Configuration file '{configFilePath}' not found.");
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(configFilePath, optional: false, reloadOnChange: true);

            Configuration = builder.Build();
        }


        public async Task StartListening()
        {

            var ipAddress = IPAddress.Parse(Configuration["Settings:ipAddress"]);
            var port = int.Parse(Configuration["Settings:port"]);
            var dataMode = Enum.Parse<DataMode>(Configuration["Settings:dataMode"]);
            var dataRate = int.Parse(Configuration["Settings:dataRate"]);
            var eventHubNamespace = Configuration["Settings:eventHubNamespace"];
            var eventHubName = Configuration["Settings:eventHubName"];
            var eventHubConnectionString = Configuration["Settings:eventHubConnectionString"];
            var eventEncoding = Enum.Parse<EventDataEncoding>(Configuration["Settings:dataEncoding"]);
            var cloudEventEncoding = Enum.Parse<CloudEventEncoding>(Configuration["Settings:cloudEventEncoding"]);
            var tenantId = Configuration["Settings:tenantId"];
            var carId = Configuration["Settings:carId"];



#nullable enable
            string? eventHubPolicyName = null;
            string? eventHubPolicyKey = null;
#nullable disable

            // Parse the Event Hub Connection string
            if (eventHubConnectionString != null)
            {
                var csProps = EventHubsConnectionStringProperties.Parse(eventHubConnectionString);
                if (csProps.FullyQualifiedNamespace != null && eventHubNamespace == null) eventHubNamespace = csProps.Endpoint.Host;
                if (csProps.EventHubName != null && eventHubName == null) eventHubName = csProps.EventHubName;
                if (csProps.SharedAccessKeyName != null) eventHubPolicyName = csProps.SharedAccessKeyName;
                if (csProps.SharedAccessKey != null) eventHubPolicyKey = csProps.SharedAccessKey;
            }

            if (eventHubNamespace == null || eventHubName == null)
            {
                Debug.WriteLine("Please provide the Event Hub namespace and name.");
                return;
            }

            if (dataRate > 60)
            {
                Debug.WriteLine("Data rate must be 60 Hz or less");
                return;
            }

            var eventEncodingContentType =
                    (eventEncoding == EventDataEncoding.Json) ? "application/json" :
                    (eventEncoding == EventDataEncoding.AvroBinary) ? "application/vnd.apache.avro+avro" :
                    (eventEncoding == EventDataEncoding.AvroJson) ? "application/vnd.apache.avro+json" :
                    (eventEncoding == EventDataEncoding.JsonGzip) ? "application/json+gzip" :
                    (eventEncoding == EventDataEncoding.AvroBinaryGzip) ? "application/vnd.apache.avro+avro+gzip" :
                    (eventEncoding == EventDataEncoding.AvroJsonGzip) ? "application/vnd.apache.avro+json+gzip" :
                    throw new NotSupportedException($"Unsupported encoding {eventEncoding}");

            Debug.WriteLine($"Starting ForzaBridge with data mode {dataMode} at {dataRate} Hz");

            // Set up the UDP client
            var udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(ipAddress, port));

            Debug.WriteLine($"Listening for Forza Motorsports telemetry on {ipAddress}:{port}");

            CloudEventFormatter? formatter = null;
            if (cloudEventEncoding == CloudEventEncoding.JsonStructured)
            {
                formatter = new JsonEventFormatter();
            }
            else if (cloudEventEncoding == CloudEventEncoding.AvroStructured)
            {
                formatter = new CloudNative.CloudEvents.Avro.AvroEventFormatter();
            }

            EventHubProducerClient producerClient =
                    (eventHubConnectionString != null && eventHubConnectionString.Contains("UseDevelopmentEmulator=true")) ?
                    new EventHubProducerClient(eventHubConnectionString, eventHubName) :
                    (eventHubPolicyName != null && eventHubPolicyKey != null) ?
                        new EventHubProducerClient(eventHubNamespace, eventHubName, new AzureNamedKeyCredential(eventHubPolicyName, eventHubPolicyKey)) :
                        new EventHubProducerClient(eventHubNamespace, eventHubName, new DefaultAzureCredential());
            TelemetryProducer telemetryProducer = new TelemetryProducer(producerClient);

            Debug.WriteLine($"Sending telemetry to Event Hub {eventHubNamespace}/{eventHubName}, event encoding {eventEncodingContentType}");

            // This is the base offset for all timestamps produced based on the stopwatch timer. We'll get the time and zero out the msecs.
            // We don't care about being absolultely in sync with the wall clock, but we care about the relative time between events.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            var startTimeEpoch = new DateTimeOffset(
                startTime.Year, startTime.Month, startTime.Day,
                startTime.Hour, startTime.Minute, startTime.Second,
                startTime.Millisecond, startTime.Offset).ToUnixTimeMilliseconds();  // Added milliseconds

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var lastSend = stopwatch.ElapsedMilliseconds;

            // Initialize the lapId
            int lapId = 0;
            //int priorLapId = 0;

            Dictionary<ChannelType, List<double>> channelData = InitializeChannelData();
            var sledDataSize = typeof(TelemetryDataSled).GetFields().Length * 4;
            var lapEpoch = startTimeEpoch;



            while (true)
            {

                try
                {

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    var receivedData = await udpClient.ReceiveAsync();
                    var effectiveDataMode = (dataMode == DataMode.Automatic) ?
                        (receivedData.Buffer.Length <= sledDataSize ? DataMode.Sled : DataMode.Dash) : dataMode;
                    TelemetryDataSled telemetryData = (effectiveDataMode == DataMode.Dash) ?
                        ParseTelemetryData<TelemetryDataDash>(receivedData.Buffer, effectiveDataMode) :
                        ParseTelemetryData<TelemetryDataSled>(receivedData.Buffer, effectiveDataMode);

                    var timestamp = stopwatch.ElapsedMilliseconds;
                    // Normalize the timestamp to the start time
                    var normalizedTimestamp = startTimeEpoch + timestamp;

                    // Trigger an event when the telemetry data is received
                    SledTelemetryReceieved?.Invoke(telemetryData);

                    channelData[ChannelType.TimestampMS].Add(telemetryData.TimestampMS);
                    channelData[ChannelType.AccelerationX].Add(telemetryData.AccelerationX);
                    channelData[ChannelType.AccelerationY].Add(telemetryData.AccelerationY);
                    channelData[ChannelType.AccelerationZ].Add(telemetryData.AccelerationZ);
                    channelData[ChannelType.VelocityX].Add(telemetryData.VelocityX);
                    channelData[ChannelType.VelocityY].Add(telemetryData.VelocityY);
                    channelData[ChannelType.VelocityZ].Add(telemetryData.VelocityZ);
                    channelData[ChannelType.AngularVelocityX].Add(telemetryData.AngularVelocityX);
                    channelData[ChannelType.AngularVelocityY].Add(telemetryData.AngularVelocityY);
                    channelData[ChannelType.AngularVelocityZ].Add(telemetryData.AngularVelocityZ);
                    channelData[ChannelType.Yaw].Add(telemetryData.Yaw);
                    channelData[ChannelType.Pitch].Add(telemetryData.Pitch);
                    channelData[ChannelType.Roll].Add(telemetryData.Roll);
                    channelData[ChannelType.NormalizedSuspensionTravelFrontLeft].Add(telemetryData.NormalizedSuspensionTravelFrontLeft);
                    channelData[ChannelType.NormalizedSuspensionTravelFrontRight].Add(telemetryData.NormalizedSuspensionTravelFrontRight);
                    channelData[ChannelType.NormalizedSuspensionTravelRearLeft].Add(telemetryData.NormalizedSuspensionTravelRearLeft);
                    channelData[ChannelType.NormalizedSuspensionTravelRearRight].Add(telemetryData.NormalizedSuspensionTravelRearRight);
                    channelData[ChannelType.TireSlipRatioFrontLeft].Add(telemetryData.TireSlipRatioFrontLeft);
                    channelData[ChannelType.TireSlipRatioFrontRight].Add(telemetryData.TireSlipRatioFrontRight);
                    channelData[ChannelType.TireSlipRatioRearLeft].Add(telemetryData.TireSlipRatioRearLeft);
                    channelData[ChannelType.TireSlipRatioRearRight].Add(telemetryData.TireSlipRatioRearRight);
                    channelData[ChannelType.WheelRotationSpeedFrontLeft].Add(telemetryData.WheelRotationSpeedFrontLeft);
                    channelData[ChannelType.WheelRotationSpeedFrontRight].Add(telemetryData.WheelRotationSpeedFrontRight);
                    channelData[ChannelType.WheelRotationSpeedRearLeft].Add(telemetryData.WheelRotationSpeedRearLeft);
                    channelData[ChannelType.WheelRotationSpeedRearRight].Add(telemetryData.WheelRotationSpeedRearRight);
                    channelData[ChannelType.SurfaceRumbleFrontLeft].Add(telemetryData.SurfaceRumbleFrontLeft);
                    channelData[ChannelType.SurfaceRumbleFrontRight].Add(telemetryData.SurfaceRumbleFrontRight);
                    channelData[ChannelType.SurfaceRumbleRearLeft].Add(telemetryData.SurfaceRumbleRearLeft);
                    channelData[ChannelType.SurfaceRumbleRearRight].Add(telemetryData.SurfaceRumbleRearRight);
                    channelData[ChannelType.TireSlipAngleFrontLeft].Add(telemetryData.TireSlipAngleFrontLeft);
                    channelData[ChannelType.TireSlipAngleFrontRight].Add(telemetryData.TireSlipAngleFrontRight);
                    channelData[ChannelType.TireSlipAngleRearLeft].Add(telemetryData.TireSlipAngleRearLeft);
                    channelData[ChannelType.TireSlipAngleRearRight].Add(telemetryData.TireSlipAngleRearRight);
                    channelData[ChannelType.TireCombinedSlipFrontLeft].Add(telemetryData.TireCombinedSlipFrontLeft);
                    channelData[ChannelType.TireCombinedSlipFrontRight].Add(telemetryData.TireCombinedSlipFrontRight);
                    channelData[ChannelType.TireCombinedSlipRearLeft].Add(telemetryData.TireCombinedSlipRearLeft);
                    channelData[ChannelType.TireCombinedSlipRearRight].Add(telemetryData.TireCombinedSlipRearRight);
                    channelData[ChannelType.SuspensionTravelMetersFrontLeft].Add(telemetryData.SuspensionTravelMetersFrontLeft);
                    channelData[ChannelType.SuspensionTravelMetersFrontRight].Add(telemetryData.SuspensionTravelMetersFrontRight);
                    channelData[ChannelType.SuspensionTravelMetersRearLeft].Add(telemetryData.SuspensionTravelMetersRearLeft);
                    channelData[ChannelType.SuspensionTravelMetersRearRight].Add(telemetryData.SuspensionTravelMetersRearRight);

                    if (telemetryData is TelemetryDataDash)
                    {


                        var dash = (TelemetryDataDash)telemetryData;

                        // Trigger an even when the dash data is received
                        DashTelemetryReceieved?.Invoke(dash);


                        channelData[ChannelType.PositionX].Add(dash.PositionX);
                        channelData[ChannelType.PositionY].Add(dash.PositionY);
                        channelData[ChannelType.PositionZ].Add(dash.PositionZ);
                        channelData[ChannelType.Speed].Add(dash.Speed);
                        channelData[ChannelType.Power].Add(dash.Power);
                        channelData[ChannelType.Torque].Add(dash.Torque);
                        channelData[ChannelType.TireTempFrontLeft].Add(dash.TireTempFrontLeft);
                        channelData[ChannelType.TireTempFrontRight].Add(dash.TireTempFrontRight);
                        channelData[ChannelType.TireTempRearLeft].Add(dash.TireTempRearLeft);
                        channelData[ChannelType.TireTempRearRight].Add(dash.TireTempRearRight);
                        channelData[ChannelType.Boost].Add(dash.Boost);
                        channelData[ChannelType.Fuel].Add(dash.Fuel);
                        channelData[ChannelType.DistanceTraveled].Add(dash.DistanceTraveled);
                        channelData[ChannelType.RacePosition].Add(dash.RacePosition);
                        channelData[ChannelType.Accel].Add(dash.Accel);
                        channelData[ChannelType.Brake].Add(dash.Brake);
                        channelData[ChannelType.Clutch].Add(dash.Clutch);
                        channelData[ChannelType.HandBrake].Add(dash.HandBrake);
                        channelData[ChannelType.Gear].Add(dash.Gear);
                        channelData[ChannelType.Steer].Add(dash.Steer);
                        channelData[ChannelType.NormalizedDrivingLine].Add(dash.NormalizedDrivingLine);
                        channelData[ChannelType.NormalizedAIBrakeDifference].Add(dash.NormalizedAIBrakeDifference);
                        channelData[ChannelType.TireWearFrontLeft].Add(dash.TireWearFrontLeft);
                        channelData[ChannelType.TireWearFrontRight].Add(dash.TireWearFrontRight);
                        channelData[ChannelType.TireWearRearLeft].Add(dash.TireWearRearLeft);
                        channelData[ChannelType.TireWearRearRight].Add(dash.TireWearRearRight);
                        channelData[ChannelType.CurrentLapTimer].Add(dash.CurrentLap);

                        lapId = dash.LapNumber;

                        // This won't trigger on the final lap so comment out for now
                        /*
                        if (priorLapId != lapId)
                        {
                            priorLapId = lapId;
                            Console.WriteLine($"lap: {lapId}");
                            var startTS = lapEpoch;
                            var endTS = normalizedTimestamp;
                            var effectiveLapId = lapId.ToString();
                            var effectiveCarId = (carId != null) ? carId : $"{telemetryData.CarOrdinal}:{telemetryData.CarClass}:{telemetryData.CarPerformanceIndex}";
                            //_ = Task.Run(async () => await SendLapSignal(telemetryProducer, startTS, endTS, tenantId, effectiveCarId, session.SessionId, effectiveLapId, eventEncodingContentType, formatter));
                            lapEpoch = endTS;
                        }
                        */

                        //-------------------------------------------
                        // Construct a small class to hold UI telemetry data
                        //TelemetryBasic telemetry = new TelemetryBasic();
                        //uiTelemetry.IsRaceOn = true;
                        //uiTelemetry.CurrentEngineRpm = (int)telemetryData.CurrentEngineRpm;
                        //uiTelemetry.SpeedMPH = (int)Math.Round(dash.Speed * 2.23694);
                        //uiTelemetry.SpeedKPH = (int)Math.Round(dash.Speed * 3.6);
                        //uiTelemetry.SelectedGear = dash.Gear;
                        //uiTelemetry.LapNumber = dash.LapNumber;
                        //uiTelemetry.CurrentLap = dash.CurrentLap;
                        //uiTelemetry.LastLap = dash.LastLap;
                        //uiTelemetry.BestLap = dash.BestLap;
                        //uiTelemetry.DistanceTraveled = dash.DistanceTraveled;

                        //currentBestLap = dash.BestLap;
                        //currentLastLap = dash.LastLap;
                        //currentLap = dash.CurrentLap;

                        // alert when the new data is recieved
                        //TelemetryReceieved?.Invoke(uiTelemetry);
                        //--------------------------------------------


                    }

                    if (telemetryData.IsRaceOn == 0)
                    {
                        continue;
                    }
                    else if (timestamp - lastSend >= (1000 / dataRate) - 1)
                    {
                        var capturedChannelData = channelData;
                        channelData = InitializeChannelData();
                        Debug.WriteLine($"sessionId: {session.SessionId}, lap: {lapId}, msec: {timestamp - lastSend}, recs: {capturedChannelData[ChannelType.AccelerationX].Count}");
                        var startTS = lastSend + startTimeEpoch;
                        var endTS = timestamp + startTimeEpoch;
                        var effectiveLapId = lapId.ToString();
                        var effectiveCarId = (carId != null) ? carId : $"{telemetryData.CarOrdinal}:{telemetryData.CarClass}:{telemetryData.CarPerformanceIndex}";
                        lastSend = timestamp;
                        _ = Task.Run(async () => await SendChannelData(telemetryProducer, capturedChannelData, startTS, endTS, tenantId, effectiveCarId, session.SessionId, session.Name, session.Email, session.Telephone, effectiveLapId, eventEncodingContentType, formatter));
                    }

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error: {ex.Message}");
                }

            }
        }


        public enum EventDataEncoding
        {
            Json,
            AvroBinary,
            AvroJson,
            JsonGzip,
            AvroBinaryGzip,
            AvroJsonGzip
        }
        public enum CloudEventEncoding
        {
            Binary,
            JsonStructured,
            AvroStructured
        }

        private static Dictionary<ChannelType, List<double>> InitializeChannelData()
        {
            var channelData = new Dictionary<ChannelType, List<double>>();
            channelData.Add(ChannelType.TimestampMS, new List<double>());
            channelData.Add(ChannelType.AccelerationX, new List<double>());
            channelData.Add(ChannelType.AccelerationY, new List<double>());
            channelData.Add(ChannelType.AccelerationZ, new List<double>());
            channelData.Add(ChannelType.VelocityX, new List<double>());
            channelData.Add(ChannelType.VelocityY, new List<double>());
            channelData.Add(ChannelType.VelocityZ, new List<double>());
            channelData.Add(ChannelType.AngularVelocityX, new List<double>());
            channelData.Add(ChannelType.AngularVelocityY, new List<double>());
            channelData.Add(ChannelType.AngularVelocityZ, new List<double>());
            channelData.Add(ChannelType.Yaw, new List<double>());
            channelData.Add(ChannelType.Pitch, new List<double>());
            channelData.Add(ChannelType.Roll, new List<double>());
            channelData.Add(ChannelType.NormalizedSuspensionTravelFrontLeft, new List<double>());
            channelData.Add(ChannelType.NormalizedSuspensionTravelFrontRight, new List<double>());
            channelData.Add(ChannelType.NormalizedSuspensionTravelRearLeft, new List<double>());
            channelData.Add(ChannelType.NormalizedSuspensionTravelRearRight, new List<double>());
            channelData.Add(ChannelType.TireSlipRatioFrontLeft, new List<double>());
            channelData.Add(ChannelType.TireSlipRatioFrontRight, new List<double>());
            channelData.Add(ChannelType.TireSlipRatioRearLeft, new List<double>());
            channelData.Add(ChannelType.TireSlipRatioRearRight, new List<double>());
            channelData.Add(ChannelType.WheelRotationSpeedFrontLeft, new List<double>());
            channelData.Add(ChannelType.WheelRotationSpeedFrontRight, new List<double>());
            channelData.Add(ChannelType.WheelRotationSpeedRearLeft, new List<double>());
            channelData.Add(ChannelType.WheelRotationSpeedRearRight, new List<double>());
            channelData.Add(ChannelType.SurfaceRumbleFrontLeft, new List<double>());
            channelData.Add(ChannelType.SurfaceRumbleFrontRight, new List<double>());
            channelData.Add(ChannelType.SurfaceRumbleRearLeft, new List<double>());
            channelData.Add(ChannelType.SurfaceRumbleRearRight, new List<double>());
            channelData.Add(ChannelType.TireSlipAngleFrontLeft, new List<double>());
            channelData.Add(ChannelType.TireSlipAngleFrontRight, new List<double>());
            channelData.Add(ChannelType.TireSlipAngleRearLeft, new List<double>());
            channelData.Add(ChannelType.TireSlipAngleRearRight, new List<double>());
            channelData.Add(ChannelType.TireCombinedSlipFrontLeft, new List<double>());
            channelData.Add(ChannelType.TireCombinedSlipFrontRight, new List<double>());
            channelData.Add(ChannelType.TireCombinedSlipRearLeft, new List<double>());
            channelData.Add(ChannelType.TireCombinedSlipRearRight, new List<double>());
            channelData.Add(ChannelType.SuspensionTravelMetersFrontLeft, new List<double>());
            channelData.Add(ChannelType.SuspensionTravelMetersFrontRight, new List<double>());
            channelData.Add(ChannelType.SuspensionTravelMetersRearLeft, new List<double>());
            channelData.Add(ChannelType.SuspensionTravelMetersRearRight, new List<double>());
            channelData.Add(ChannelType.PositionX, new List<double>());
            channelData.Add(ChannelType.PositionY, new List<double>());
            channelData.Add(ChannelType.PositionZ, new List<double>());
            channelData.Add(ChannelType.Speed, new List<double>());
            channelData.Add(ChannelType.Power, new List<double>());
            channelData.Add(ChannelType.Torque, new List<double>());
            channelData.Add(ChannelType.TireTempFrontLeft, new List<double>());
            channelData.Add(ChannelType.TireTempFrontRight, new List<double>());
            channelData.Add(ChannelType.TireTempRearLeft, new List<double>());
            channelData.Add(ChannelType.TireTempRearRight, new List<double>());
            channelData.Add(ChannelType.Boost, new List<double>());
            channelData.Add(ChannelType.Fuel, new List<double>());
            channelData.Add(ChannelType.DistanceTraveled, new List<double>());
            channelData.Add(ChannelType.RacePosition, new List<double>());
            channelData.Add(ChannelType.Accel, new List<double>());
            channelData.Add(ChannelType.Brake, new List<double>());
            channelData.Add(ChannelType.Clutch, new List<double>());
            channelData.Add(ChannelType.HandBrake, new List<double>());
            channelData.Add(ChannelType.Gear, new List<double>());
            channelData.Add(ChannelType.Steer, new List<double>());
            channelData.Add(ChannelType.NormalizedDrivingLine, new List<double>());
            channelData.Add(ChannelType.NormalizedAIBrakeDifference, new List<double>());
            channelData.Add(ChannelType.TireWearFrontLeft, new List<double>());
            channelData.Add(ChannelType.TireWearFrontRight, new List<double>());
            channelData.Add(ChannelType.TireWearRearLeft, new List<double>());
            channelData.Add(ChannelType.TireWearRearRight, new List<double>());
            channelData.Add(ChannelType.CurrentLapTimer, new List<double>());

            return channelData;
        }

        private static T ParseTelemetryData<T>(byte[] data, DataMode dataMode) where T : TelemetryDataSled
        {
            // Implement the logic to parse the telemetry data from the byte array
            // and return the parsed telemetry data object. Base this on the Forza Motorsports docs:
            // https://support.forzamotorsport.net/hc/en-us/articles/21742934024211-Forza-Motorsport-Data-Out-Documentation

            TelemetryDataSled telemetryData = (dataMode == DataMode.Sled) ? new TelemetryDataSled() : new TelemetryDataDash();
            // decode the data
            var stream = new System.IO.MemoryStream(data);
            var reader = new System.IO.BinaryReader(stream);
            telemetryData.IsRaceOn = reader.ReadInt32();
            telemetryData.TimestampMS = reader.ReadUInt32();
            telemetryData.EngineMaxRpm = reader.ReadSingle();
            telemetryData.EngineIdleRpm = reader.ReadSingle();
            telemetryData.CurrentEngineRpm = reader.ReadSingle();
            telemetryData.AccelerationX = reader.ReadSingle();
            telemetryData.AccelerationY = reader.ReadSingle();
            telemetryData.AccelerationZ = reader.ReadSingle();
            telemetryData.VelocityX = reader.ReadSingle();
            telemetryData.VelocityY = reader.ReadSingle();
            telemetryData.VelocityZ = reader.ReadSingle();
            telemetryData.AngularVelocityX = reader.ReadSingle();
            telemetryData.AngularVelocityY = reader.ReadSingle();
            telemetryData.AngularVelocityZ = reader.ReadSingle();
            telemetryData.Yaw = reader.ReadSingle();
            telemetryData.Pitch = reader.ReadSingle();
            telemetryData.Roll = reader.ReadSingle();
            telemetryData.NormalizedSuspensionTravelFrontLeft = reader.ReadSingle();
            telemetryData.NormalizedSuspensionTravelFrontRight = reader.ReadSingle();
            telemetryData.NormalizedSuspensionTravelRearLeft = reader.ReadSingle();
            telemetryData.NormalizedSuspensionTravelRearRight = reader.ReadSingle();
            telemetryData.TireSlipRatioFrontLeft = reader.ReadSingle();
            telemetryData.TireSlipRatioFrontRight = reader.ReadSingle();
            telemetryData.TireSlipRatioRearLeft = reader.ReadSingle();
            telemetryData.TireSlipRatioRearRight = reader.ReadSingle();
            telemetryData.WheelRotationSpeedFrontLeft = reader.ReadSingle();
            telemetryData.WheelRotationSpeedFrontRight = reader.ReadSingle();
            telemetryData.WheelRotationSpeedRearLeft = reader.ReadSingle();
            telemetryData.WheelRotationSpeedRearRight = reader.ReadSingle();
            telemetryData.WheelOnRumbleStripFrontLeft = reader.ReadInt32();
            telemetryData.WheelOnRumbleStripFrontRight = reader.ReadInt32();
            telemetryData.WheelOnRumbleStripRearLeft = reader.ReadInt32();
            telemetryData.WheelOnRumbleStripRearRight = reader.ReadInt32();
            telemetryData.WheelInPuddleDepthFrontLeft = reader.ReadSingle();
            telemetryData.WheelInPuddleDepthFrontRight = reader.ReadSingle();
            telemetryData.WheelInPuddleDepthRearLeft = reader.ReadSingle();
            telemetryData.WheelInPuddleDepthRearRight = reader.ReadSingle();
            telemetryData.SurfaceRumbleFrontLeft = reader.ReadSingle();
            telemetryData.SurfaceRumbleFrontRight = reader.ReadSingle();
            telemetryData.SurfaceRumbleRearLeft = reader.ReadSingle();
            telemetryData.SurfaceRumbleRearRight = reader.ReadSingle();
            telemetryData.TireSlipAngleFrontLeft = reader.ReadSingle();
            telemetryData.TireSlipAngleFrontRight = reader.ReadSingle();
            telemetryData.TireSlipAngleRearLeft = reader.ReadSingle();
            telemetryData.TireSlipAngleRearRight = reader.ReadSingle();
            telemetryData.TireCombinedSlipFrontLeft = reader.ReadSingle();
            telemetryData.TireCombinedSlipFrontRight = reader.ReadSingle();
            telemetryData.TireCombinedSlipRearLeft = reader.ReadSingle();
            telemetryData.TireCombinedSlipRearRight = reader.ReadSingle();
            telemetryData.SuspensionTravelMetersFrontLeft = reader.ReadSingle();
            telemetryData.SuspensionTravelMetersFrontRight = reader.ReadSingle();
            telemetryData.SuspensionTravelMetersRearLeft = reader.ReadSingle();
            telemetryData.SuspensionTravelMetersRearRight = reader.ReadSingle();
            telemetryData.CarOrdinal = reader.ReadInt32();
            telemetryData.CarClass = reader.ReadInt32();
            telemetryData.CarPerformanceIndex = reader.ReadInt32();
            telemetryData.DrivetrainType = reader.ReadInt32();
            telemetryData.NumCylinders = reader.ReadInt32();
            if (telemetryData is TelemetryDataDash)
            {
                TelemetryDataDash dash = (TelemetryDataDash)telemetryData;
                dash.PositionX = reader.ReadSingle();
                dash.PositionY = reader.ReadSingle();
                dash.PositionZ = reader.ReadSingle();
                dash.Speed = reader.ReadSingle();
                dash.Power = reader.ReadSingle();
                dash.Torque = reader.ReadSingle();
                dash.TireTempFrontLeft = reader.ReadSingle();
                dash.TireTempFrontRight = reader.ReadSingle();
                dash.TireTempRearLeft = reader.ReadSingle();
                dash.TireTempRearRight = reader.ReadSingle();
                dash.Boost = reader.ReadSingle();
                dash.Fuel = reader.ReadSingle();
                dash.DistanceTraveled = reader.ReadSingle();
                dash.BestLap = reader.ReadSingle();
                dash.LastLap = reader.ReadSingle();
                dash.CurrentLap = reader.ReadSingle();
                dash.CurrentRaceTime = reader.ReadSingle();
                dash.LapNumber = reader.ReadUInt16();
                dash.RacePosition = reader.ReadByte();
                dash.Accel = reader.ReadByte();
                dash.Brake = reader.ReadByte();
                dash.Clutch = reader.ReadByte();
                dash.HandBrake = reader.ReadByte();
                dash.Gear = reader.ReadByte();
                dash.Steer = reader.ReadSByte();
                dash.NormalizedDrivingLine = reader.ReadSByte();
                dash.NormalizedAIBrakeDifference = reader.ReadSByte();
                dash.TireWearFrontLeft = reader.ReadSingle();
                dash.TireWearFrontRight = reader.ReadSingle();
                dash.TireWearRearLeft = reader.ReadSingle();
                dash.TireWearRearRight = reader.ReadSingle();
                dash.TrackOrdinal = reader.ReadInt32();
            }
            return (T)telemetryData;
        }

        private static async Task SendLapSignal(TelemetryProducer producerClient, long lastSend, long timestamp,
                                                string tenantId, string carId, string sessionId,
                                                string lapId, string contentType, CloudEventFormatter? formatter)
        {
            await producerClient.SendLapSignalAsync(
                new LapSignal
                {
                    CarId = carId,
                    SessionId = sessionId,
                    LapId = lapId,
                    Timespan = new LapTimespan()
                    {
                        StartTS = lastSend,
                        EndTS = timestamp,
                    }
                },
                tenantId, carId, sessionId, contentType, formatter);
            Debug.WriteLine($"Sent lap signal event for car {carId}, lap {lapId}");
        }

        private static async Task SendChannelData(TelemetryProducer producerClient, Dictionary<ChannelType, List<double>> capturedChannelData,
                                                  long startTS, long endTS, string tenantId, string carId, string sessionId, string sessionName, string email, string telephone,
                                                  string lapId, string contentType, CloudEventFormatter? formatter)
        {
            int totalEventCount = 0;
            Dictionary<ChannelType, List<Channel>> channels = new Dictionary<ChannelType, List<Channel>>();
            foreach (var channelData in capturedChannelData)
            {
                if (channelData.Value.Count == 0)
                {
                    continue;
                }
                if (!channels.ContainsKey(channelData.Key))
                {
                    channels.Add(channelData.Key, new List<Channel>());
                }
                channels[channelData.Key].Add(new Channel
                {
                    ChannelId = channelData.Key,
                    CarId = carId,
                    SessionId = sessionId,
                    SessionName = sessionName,
                    Email = email,
                    Telephone = telephone,
                    LapId = lapId,
                    SampleCount = channelData.Value.Count,
                    Frequency = (int)(channelData.Value.Count / ((endTS - startTS) / 1000.0)),
                    Timespan = new BatchTimespan
                    {
                        StartTS = startTS,
                        EndTS = endTS
                    },
                    Data = channelData.Value
                });
            }
            foreach (var channel in channels)
            {
                var values = channel.Value.ToArray();
                totalEventCount += values.Length;
                await producerClient.SendChannelBatchAsync(values, tenantId, carId, channel.Key.ToString(), contentType, formatter);
            }
            Debug.WriteLine($"Sent {totalEventCount} channel events for car {carId}");
        }
    }
}



