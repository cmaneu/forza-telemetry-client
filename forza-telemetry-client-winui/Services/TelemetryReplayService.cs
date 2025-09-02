using FlatSharp;
using ForzaBridge.Schema;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ForzaBridge.Model;
using System.Diagnostics;

namespace ForzaBridge.Services
{
    public class TelemetryReplayService : IDisposable
    {
        private readonly string _replayFilePath;
        private bool _disposed = false;
        private CancellationTokenSource _cancellationTokenSource;
        private TelemetrySession _session;

        public event Action<TelemetryDataSled> SledTelemetryReplayed;
        public event Action<TelemetryDataDash> DashTelemetryReplayed;

        public TelemetryReplayService(string replayFilePath)
        {
            _replayFilePath = replayFilePath;
        }

        public async Task LoadReplayFileAsync()
        {
            if (!File.Exists(_replayFilePath))
                throw new FileNotFoundException($"Replay file not found: {_replayFilePath}");

            var buffer = await File.ReadAllBytesAsync(_replayFilePath);
            _session = TelemetrySession.Serializer.Parse(buffer);
        }

        public async Task StartReplayAsync()
        {
            if (_session == null)
                throw new InvalidOperationException("Replay file not loaded. Call LoadReplayFileAsync first.");

            _cancellationTokenSource = new CancellationTokenSource();
            
            await Task.Run(async () => await ReplayDataAsync(_cancellationTokenSource.Token));
        }

        public void StopReplay()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task ReplayDataAsync(CancellationToken cancellationToken)
        {
            var records = _session.Records;
            if (records == null || records.Count == 0)
                return;

            var stopwatch = Stopwatch.StartNew();
            var startTimestamp = (long)_session.StartTimestamp;

            foreach (var record in records)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var recordTimestamp = (long)record.TimestampMillis;
                var relativeTimestamp = recordTimestamp - startTimestamp;
                var currentElapsed = stopwatch.ElapsedMilliseconds;

                // Wait until it's time to replay this record
                if (relativeTimestamp > currentElapsed)
                {
                    var delayMs = (int)(relativeTimestamp - currentElapsed);
                    await Task.Delay(delayMs, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                // Convert FlatBuffer data back to telemetry objects and trigger events
                await ReplayRecord(record);
            }
        }

        private async Task ReplayRecord(TelemetryRecord record)
        {
            await Task.Run(() =>
            {
                if (record.DataMode == 1 && record.DashData != null)
                {
                    var dashData = ConvertFromDashFlatBuffer(record.DashData);
                    DashTelemetryReplayed?.Invoke(dashData);
                }
                else if (record.SledData != null)
                {
                    var sledData = ConvertFromSledFlatBuffer(record.SledData);
                    SledTelemetryReplayed?.Invoke(sledData);
                }
            });
        }

        private TelemetryDataSled ConvertFromSledFlatBuffer(ForzaBridge.Schema.TelemetryDataSled source)
        {
            return new TelemetryDataSled
            {
                IsRaceOn = source.IsRaceOn,
                TimestampMS = source.TimestampMs,
                EngineMaxRpm = source.EngineMaxRpm,
                EngineIdleRpm = source.EngineIdleRpm,
                CurrentEngineRpm = source.CurrentEngineRpm,
                AccelerationX = source.AccelerationX,
                AccelerationY = source.AccelerationY,
                AccelerationZ = source.AccelerationZ,
                VelocityX = source.VelocityX,
                VelocityY = source.VelocityY,
                VelocityZ = source.VelocityZ,
                AngularVelocityX = source.AngularVelocityX,
                AngularVelocityY = source.AngularVelocityY,
                AngularVelocityZ = source.AngularVelocityZ,
                Yaw = source.Yaw,
                Pitch = source.Pitch,
                Roll = source.Roll,
                NormalizedSuspensionTravelFrontLeft = source.NormalizedSuspensionTravelFrontLeft,
                NormalizedSuspensionTravelFrontRight = source.NormalizedSuspensionTravelFrontRight,
                NormalizedSuspensionTravelRearLeft = source.NormalizedSuspensionTravelRearLeft,
                NormalizedSuspensionTravelRearRight = source.NormalizedSuspensionTravelRearRight,
                TireSlipRatioFrontLeft = source.TireSlipRatioFrontLeft,
                TireSlipRatioFrontRight = source.TireSlipRatioFrontRight,
                TireSlipRatioRearLeft = source.TireSlipRatioRearLeft,
                TireSlipRatioRearRight = source.TireSlipRatioRearRight,
                WheelRotationSpeedFrontLeft = source.WheelRotationSpeedFrontLeft,
                WheelRotationSpeedFrontRight = source.WheelRotationSpeedFrontRight,
                WheelRotationSpeedRearLeft = source.WheelRotationSpeedRearLeft,
                WheelRotationSpeedRearRight = source.WheelRotationSpeedRearRight,
                WheelOnRumbleStripFrontLeft = source.WheelOnRumbleStripFrontLeft,
                WheelOnRumbleStripFrontRight = source.WheelOnRumbleStripFrontRight,
                WheelOnRumbleStripRearLeft = source.WheelOnRumbleStripRearLeft,
                WheelOnRumbleStripRearRight = source.WheelOnRumbleStripRearRight,
                WheelInPuddleDepthFrontLeft = source.WheelInPuddleDepthFrontLeft,
                WheelInPuddleDepthFrontRight = source.WheelInPuddleDepthFrontRight,
                WheelInPuddleDepthRearLeft = source.WheelInPuddleDepthRearLeft,
                WheelInPuddleDepthRearRight = source.WheelInPuddleDepthRearRight,
                SurfaceRumbleFrontLeft = source.SurfaceRumbleFrontLeft,
                SurfaceRumbleFrontRight = source.SurfaceRumbleFrontRight,
                SurfaceRumbleRearLeft = source.SurfaceRumbleRearLeft,
                SurfaceRumbleRearRight = source.SurfaceRumbleRearRight,
                TireSlipAngleFrontLeft = source.TireSlipAngleFrontLeft,
                TireSlipAngleFrontRight = source.TireSlipAngleFrontRight,
                TireSlipAngleRearLeft = source.TireSlipAngleRearLeft,
                TireSlipAngleRearRight = source.TireSlipAngleRearRight,
                TireCombinedSlipFrontLeft = source.TireCombinedSlipFrontLeft,
                TireCombinedSlipFrontRight = source.TireCombinedSlipFrontRight,
                TireCombinedSlipRearLeft = source.TireCombinedSlipRearLeft,
                TireCombinedSlipRearRight = source.TireCombinedSlipRearRight,
                SuspensionTravelMetersFrontLeft = source.SuspensionTravelMetersFrontLeft,
                SuspensionTravelMetersFrontRight = source.SuspensionTravelMetersFrontRight,
                SuspensionTravelMetersRearLeft = source.SuspensionTravelMetersRearLeft,
                SuspensionTravelMetersRearRight = source.SuspensionTravelMetersRearRight,
                CarOrdinal = source.CarOrdinal,
                CarClass = source.CarClass,
                CarPerformanceIndex = source.CarPerformanceIndex,
                DrivetrainType = source.DrivetrainType,
                NumCylinders = source.NumCylinders
            };
        }

        private TelemetryDataDash ConvertFromDashFlatBuffer(ForzaBridge.Schema.TelemetryDataDash source)
        {
            var baseData = ConvertFromSledFlatBuffer(source.BaseData);
            
            return new TelemetryDataDash
            {
                // Copy all base properties
                IsRaceOn = baseData.IsRaceOn,
                TimestampMS = baseData.TimestampMS,
                EngineMaxRpm = baseData.EngineMaxRpm,
                EngineIdleRpm = baseData.EngineIdleRpm,
                CurrentEngineRpm = baseData.CurrentEngineRpm,
                AccelerationX = baseData.AccelerationX,
                AccelerationY = baseData.AccelerationY,
                AccelerationZ = baseData.AccelerationZ,
                VelocityX = baseData.VelocityX,
                VelocityY = baseData.VelocityY,
                VelocityZ = baseData.VelocityZ,
                AngularVelocityX = baseData.AngularVelocityX,
                AngularVelocityY = baseData.AngularVelocityY,
                AngularVelocityZ = baseData.AngularVelocityZ,
                Yaw = baseData.Yaw,
                Pitch = baseData.Pitch,
                Roll = baseData.Roll,
                NormalizedSuspensionTravelFrontLeft = baseData.NormalizedSuspensionTravelFrontLeft,
                NormalizedSuspensionTravelFrontRight = baseData.NormalizedSuspensionTravelFrontRight,
                NormalizedSuspensionTravelRearLeft = baseData.NormalizedSuspensionTravelRearLeft,
                NormalizedSuspensionTravelRearRight = baseData.NormalizedSuspensionTravelRearRight,
                TireSlipRatioFrontLeft = baseData.TireSlipRatioFrontLeft,
                TireSlipRatioFrontRight = baseData.TireSlipRatioFrontRight,
                TireSlipRatioRearLeft = baseData.TireSlipRatioRearLeft,
                TireSlipRatioRearRight = baseData.TireSlipRatioRearRight,
                WheelRotationSpeedFrontLeft = baseData.WheelRotationSpeedFrontLeft,
                WheelRotationSpeedFrontRight = baseData.WheelRotationSpeedFrontRight,
                WheelRotationSpeedRearLeft = baseData.WheelRotationSpeedRearLeft,
                WheelRotationSpeedRearRight = baseData.WheelRotationSpeedRearRight,
                WheelOnRumbleStripFrontLeft = baseData.WheelOnRumbleStripFrontLeft,
                WheelOnRumbleStripFrontRight = baseData.WheelOnRumbleStripFrontRight,
                WheelOnRumbleStripRearLeft = baseData.WheelOnRumbleStripRearLeft,
                WheelOnRumbleStripRearRight = baseData.WheelOnRumbleStripRearRight,
                WheelInPuddleDepthFrontLeft = baseData.WheelInPuddleDepthFrontLeft,
                WheelInPuddleDepthFrontRight = baseData.WheelInPuddleDepthFrontRight,
                WheelInPuddleDepthRearLeft = baseData.WheelInPuddleDepthRearLeft,
                WheelInPuddleDepthRearRight = baseData.WheelInPuddleDepthRearRight,
                SurfaceRumbleFrontLeft = baseData.SurfaceRumbleFrontLeft,
                SurfaceRumbleFrontRight = baseData.SurfaceRumbleFrontRight,
                SurfaceRumbleRearLeft = baseData.SurfaceRumbleRearLeft,
                SurfaceRumbleRearRight = baseData.SurfaceRumbleRearRight,
                TireSlipAngleFrontLeft = baseData.TireSlipAngleFrontLeft,
                TireSlipAngleFrontRight = baseData.TireSlipAngleFrontRight,
                TireSlipAngleRearLeft = baseData.TireSlipAngleRearLeft,
                TireSlipAngleRearRight = baseData.TireSlipAngleRearRight,
                TireCombinedSlipFrontLeft = baseData.TireCombinedSlipFrontLeft,
                TireCombinedSlipFrontRight = baseData.TireCombinedSlipFrontRight,
                TireCombinedSlipRearLeft = baseData.TireCombinedSlipRearLeft,
                TireCombinedSlipRearRight = baseData.TireCombinedSlipRearRight,
                SuspensionTravelMetersFrontLeft = baseData.SuspensionTravelMetersFrontLeft,
                SuspensionTravelMetersFrontRight = baseData.SuspensionTravelMetersFrontRight,
                SuspensionTravelMetersRearLeft = baseData.SuspensionTravelMetersRearLeft,
                SuspensionTravelMetersRearRight = baseData.SuspensionTravelMetersRearRight,
                CarOrdinal = baseData.CarOrdinal,
                CarClass = baseData.CarClass,
                CarPerformanceIndex = baseData.CarPerformanceIndex,
                DrivetrainType = baseData.DrivetrainType,
                NumCylinders = baseData.NumCylinders,

                // Add dash-specific properties
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                PositionZ = source.PositionZ,
                Speed = source.Speed,
                Power = source.Power,
                Torque = source.Torque,
                TireTempFrontLeft = source.TireTempFrontLeft,
                TireTempFrontRight = source.TireTempFrontRight,
                TireTempRearLeft = source.TireTempRearLeft,
                TireTempRearRight = source.TireTempRearRight,
                Boost = source.Boost,
                Fuel = source.Fuel,
                DistanceTraveled = source.DistanceTraveled,
                BestLap = source.BestLap,
                LastLap = source.LastLap,
                CurrentLap = source.CurrentLap,
                CurrentRaceTime = source.CurrentRaceTime,
                LapNumber = source.LapNumber,
                RacePosition = source.RacePosition,
                Accel = source.Accel,
                Brake = source.Brake,
                Clutch = source.Clutch,
                HandBrake = source.HandBrake,
                Gear = source.Gear,
                Steer = source.Steer,
                NormalizedDrivingLine = source.NormalizedDrivingLine,
                NormalizedAIBrakeDifference = source.NormalizedAiBrakeDifference,
                TireWearFrontLeft = source.TireWearFrontLeft,
                TireWearFrontRight = source.TireWearFrontRight,
                TireWearRearLeft = source.TireWearRearLeft,
                TireWearRearRight = source.TireWearRearRight,
                TrackOrdinal = source.TrackOrdinal
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopReplay();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}