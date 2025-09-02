using FlatSharp;
using ForzaBridge.Schema;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ForzaBridge.Model;

namespace ForzaBridge.Services
{
    public class TelemetryDumpService : IDisposable
    {
        private readonly string _dumpFilePath;
        private readonly Stopwatch _stopwatch;
        private readonly List<ForzaBridge.Schema.TelemetryRecord> _records;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private long _startTimestamp;

        public TelemetryDumpService(string dumpFilePath)
        {
            _dumpFilePath = dumpFilePath;
            _stopwatch = new Stopwatch();
            _records = new List<ForzaBridge.Schema.TelemetryRecord>();
            _startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void StartDump()
        {
            _stopwatch.Start();
        }

        public void DumpTelemetryData(TelemetryDataSled telemetryData, DataMode dataMode, byte[] rawData)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                var timestamp = _startTimestamp + _stopwatch.ElapsedMilliseconds;
                
                var recordBuilder = new ForzaBridge.Schema.TelemetryRecord.Builder();
                recordBuilder.TimestampMillis = (ulong)timestamp;
                recordBuilder.DataMode = (byte)((dataMode == DataMode.Dash) ? 1 : 0);
                recordBuilder.RawData = rawData.ToArray();

                // Convert telemetry data to FlatBuffer format
                if (dataMode == DataMode.Dash && telemetryData is TelemetryDataDash dashData)
                {
                    recordBuilder.DashData = ConvertToDashFlatBuffer(dashData);
                }
                else
                {
                    recordBuilder.SledData = ConvertToSledFlatBuffer(telemetryData);
                }

                var record = recordBuilder.Build();
                _records.Add(record);
            }
        }

        public async Task SaveDumpAsync()
        {
            if (_disposed) return;

            _stopwatch.Stop();

            var sessionBuilder = new ForzaBridge.Schema.TelemetrySession.Builder();
            sessionBuilder.StartTimestamp = (ulong)_startTimestamp;
            sessionBuilder.Records = _records.ToArray();

            var session = sessionBuilder.Build();
            
            // Serialize to FlatBuffer format
            var buffer = session.SerializeBuffer();
            
            // Write to file
            await File.WriteAllBytesAsync(_dumpFilePath, buffer);
        }

        private ForzaBridge.Schema.TelemetryDataSled ConvertToSledFlatBuffer(TelemetryDataSled source)
        {
            var builder = new ForzaBridge.Schema.TelemetryDataSled.Builder();
            
            builder.IsRaceOn = source.IsRaceOn;
            builder.TimestampMs = source.TimestampMS;
            builder.EngineMaxRpm = source.EngineMaxRpm;
            builder.EngineIdleRpm = source.EngineIdleRpm;
            builder.CurrentEngineRpm = source.CurrentEngineRpm;
            builder.AccelerationX = source.AccelerationX;
            builder.AccelerationY = source.AccelerationY;
            builder.AccelerationZ = source.AccelerationZ;
            builder.VelocityX = source.VelocityX;
            builder.VelocityY = source.VelocityY;
            builder.VelocityZ = source.VelocityZ;
            builder.AngularVelocityX = source.AngularVelocityX;
            builder.AngularVelocityY = source.AngularVelocityY;
            builder.AngularVelocityZ = source.AngularVelocityZ;
            builder.Yaw = source.Yaw;
            builder.Pitch = source.Pitch;
            builder.Roll = source.Roll;
            builder.NormalizedSuspensionTravelFrontLeft = source.NormalizedSuspensionTravelFrontLeft;
            builder.NormalizedSuspensionTravelFrontRight = source.NormalizedSuspensionTravelFrontRight;
            builder.NormalizedSuspensionTravelRearLeft = source.NormalizedSuspensionTravelRearLeft;
            builder.NormalizedSuspensionTravelRearRight = source.NormalizedSuspensionTravelRearRight;
            builder.TireSlipRatioFrontLeft = source.TireSlipRatioFrontLeft;
            builder.TireSlipRatioFrontRight = source.TireSlipRatioFrontRight;
            builder.TireSlipRatioRearLeft = source.TireSlipRatioRearLeft;
            builder.TireSlipRatioRearRight = source.TireSlipRatioRearRight;
            builder.WheelRotationSpeedFrontLeft = source.WheelRotationSpeedFrontLeft;
            builder.WheelRotationSpeedFrontRight = source.WheelRotationSpeedFrontRight;
            builder.WheelRotationSpeedRearLeft = source.WheelRotationSpeedRearLeft;
            builder.WheelRotationSpeedRearRight = source.WheelRotationSpeedRearRight;
            builder.WheelOnRumbleStripFrontLeft = source.WheelOnRumbleStripFrontLeft;
            builder.WheelOnRumbleStripFrontRight = source.WheelOnRumbleStripFrontRight;
            builder.WheelOnRumbleStripRearLeft = source.WheelOnRumbleStripRearLeft;
            builder.WheelOnRumbleStripRearRight = source.WheelOnRumbleStripRearRight;
            builder.WheelInPuddleDepthFrontLeft = source.WheelInPuddleDepthFrontLeft;
            builder.WheelInPuddleDepthFrontRight = source.WheelInPuddleDepthFrontRight;
            builder.WheelInPuddleDepthRearLeft = source.WheelInPuddleDepthRearLeft;
            builder.WheelInPuddleDepthRearRight = source.WheelInPuddleDepthRearRight;
            builder.SurfaceRumbleFrontLeft = source.SurfaceRumbleFrontLeft;
            builder.SurfaceRumbleFrontRight = source.SurfaceRumbleFrontRight;
            builder.SurfaceRumbleRearLeft = source.SurfaceRumbleRearLeft;
            builder.SurfaceRumbleRearRight = source.SurfaceRumbleRearRight;
            builder.TireSlipAngleFrontLeft = source.TireSlipAngleFrontLeft;
            builder.TireSlipAngleFrontRight = source.TireSlipAngleFrontRight;
            builder.TireSlipAngleRearLeft = source.TireSlipAngleRearLeft;
            builder.TireSlipAngleRearRight = source.TireSlipAngleRearRight;
            builder.TireCombinedSlipFrontLeft = source.TireCombinedSlipFrontLeft;
            builder.TireCombinedSlipFrontRight = source.TireCombinedSlipFrontRight;
            builder.TireCombinedSlipRearLeft = source.TireCombinedSlipRearLeft;
            builder.TireCombinedSlipRearRight = source.TireCombinedSlipRearRight;
            builder.SuspensionTravelMetersFrontLeft = source.SuspensionTravelMetersFrontLeft;
            builder.SuspensionTravelMetersFrontRight = source.SuspensionTravelMetersFrontRight;
            builder.SuspensionTravelMetersRearLeft = source.SuspensionTravelMetersRearLeft;
            builder.SuspensionTravelMetersRearRight = source.SuspensionTravelMetersRearRight;
            builder.CarOrdinal = source.CarOrdinal;
            builder.CarClass = source.CarClass;
            builder.CarPerformanceIndex = source.CarPerformanceIndex;
            builder.DrivetrainType = source.DrivetrainType;
            builder.NumCylinders = source.NumCylinders;

            return builder.Build();
        }

        private ForzaBridge.Schema.TelemetryDataDash ConvertToDashFlatBuffer(TelemetryDataDash source)
        {
            var builder = new ForzaBridge.Schema.TelemetryDataDash.Builder();
            
            builder.BaseData = ConvertToSledFlatBuffer(source);
            builder.PositionX = source.PositionX;
            builder.PositionY = source.PositionY;
            builder.PositionZ = source.PositionZ;
            builder.Speed = source.Speed;
            builder.Power = source.Power;
            builder.Torque = source.Torque;
            builder.TireTempFrontLeft = source.TireTempFrontLeft;
            builder.TireTempFrontRight = source.TireTempFrontRight;
            builder.TireTempRearLeft = source.TireTempRearLeft;
            builder.TireTempRearRight = source.TireTempRearRight;
            builder.Boost = source.Boost;
            builder.Fuel = source.Fuel;
            builder.DistanceTraveled = source.DistanceTraveled;
            builder.BestLap = source.BestLap;
            builder.LastLap = source.LastLap;
            builder.CurrentLap = source.CurrentLap;
            builder.CurrentRaceTime = source.CurrentRaceTime;
            builder.LapNumber = source.LapNumber;
            builder.RacePosition = source.RacePosition;
            builder.Accel = source.Accel;
            builder.Brake = source.Brake;
            builder.Clutch = source.Clutch;
            builder.HandBrake = source.HandBrake;
            builder.Gear = source.Gear;
            builder.Steer = source.Steer;
            builder.NormalizedDrivingLine = source.NormalizedDrivingLine;
            builder.NormalizedAiBrakeDifference = source.NormalizedAIBrakeDifference;
            builder.TireWearFrontLeft = source.TireWearFrontLeft;
            builder.TireWearFrontRight = source.TireWearFrontRight;
            builder.TireWearRearLeft = source.TireWearRearLeft;
            builder.TireWearRearRight = source.TireWearRearRight;
            builder.TrackOrdinal = source.TrackOrdinal;

            return builder.Build();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _stopwatch?.Stop();
            _disposed = true;
        }
    }
}