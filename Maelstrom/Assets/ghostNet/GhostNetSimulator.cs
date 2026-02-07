using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Runs a headless simulation of the GhostNet maelstrom for data export.
    /// </summary>
    public class GhostNetSimulator
    {
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONINFORMATION = 0x00000040;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        /// <summary>
        ///     Runs the simulation and exports results to CSV.
        /// </summary>
        public void RunSimulation(
            GhostNetDataPoint[] data,
            GhostNetDataBound bounds,
            int loopDuration,
            int targetLoops = 2,
            int targetFps = 30)
        {
            AppLogger.Log($"[Simulator] Starting simulation: {targetLoops} loops at {targetFps} FPS");

            CommonMaelstrom.Reset();

            var maelstromManager = new GNMaelstromManager();
            maelstromManager.RegisterDataBounds(data);
            maelstromManager.Reset();

            var dataRangeByDate = GhostNetTimeController.BuildDataRangeByDateIndex(data);
            var timeController = new GhostNetTimeController(
                loopDuration,
                bounds.Min.date,
                bounds.Max.date,
                dataRangeByDate);

            var frameRecords = new List<FrameRecord>();
            var deltaTime = 1f / targetFps;
            var totalSimulationTime = loopDuration * targetLoops;
            var totalFrames = totalSimulationTime * targetFps;
            var frameIndex = 0;

            AppLogger.Log($"[Simulator] Total frames to simulate: {totalFrames}");

            while (timeController.LoopCount < targetLoops)
            {
                timeController.AdvanceTime(deltaTime);

                var dataPointsToSpawn = timeController.ProcessFrame();

                for (var i = 0; i < dataPointsToSpawn; i++)
                {
                    var dataIdx = timeController.GetDataIndexForSpawn(i);
                    if (dataIdx < data.Length)
                    {
                        var dataPoint = data[dataIdx];
                        if (!dataPoint.isAggregated) maelstromManager.RegisterData(dataPoint, true);
                    }
                }

                timeController.MarkDataPointsSpawned(dataPointsToSpawn);

                maelstromManager.Update(true);

                var record = new FrameRecord
                {
                    FrameIndex = frameIndex,
                    SimulatedTime = timeController.CurrentTime,
                    NormalizedTime = timeController.CurrentNormalizedTime,
                    CurrentDay = timeController.CurrentDay,
                    TargetMaelstrom = CommonMaelstrom.GetTargetMaelstrom(),
                    CurrentMaelstrom = CommonMaelstrom.GetCurrentMaelstrom(),
                    CurrentRatio = maelstromManager.GetCurrentRatio()
                };
                frameRecords.Add(record);

                frameIndex++;

                if (frameIndex % 1000 == 0)
                    AppLogger.Log(
                        $"[Simulator] Progress: {frameIndex}/{totalFrames} frames ({100f * frameIndex / totalFrames:F1}%)");
            }

            AppLogger.Log($"[Simulator] Simulation complete. {frameRecords.Count} frames recorded.");

            ExportToCsv(frameRecords);

            ShowCompletionPopup();
        }

        private void ExportToCsv(List<FrameRecord> records)
        {
            try
            {
                var fileName = $"ghostNet_simulation_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine(
                        "frameIndex\tsimulatedTime\tnormalizedTime\tcurrentDay\ttargetMaelstrom\tcurrentMaelstrom\tcurrentRatio");

                    foreach (var record in records)
                        writer.WriteLine(
                            $"{record.FrameIndex}\t" +
                            $"{record.SimulatedTime:F3}\t" +
                            $"{record.NormalizedTime:F6}\t" +
                            $"{record.CurrentDay:yyyy-MM-dd}\t" +
                            $"{record.TargetMaelstrom:F6}\t" +
                            $"{record.CurrentMaelstrom:F6}\t" +
                            $"{record.CurrentRatio:F6}");
                }

                AppLogger.Log($"[Simulator] Results exported to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[Simulator] Failed to export CSV: {ex.Message}");
            }
        }

        private void ShowCompletionPopup()
        {
            AppLogger.Log("[Simulator] Simulation done!");

#if UNITY_EDITOR
            EditorUtility.DisplayDialog("Simulation Complete", "Simulation done!", "OK");
#else
            try
            {
                MessageBox(IntPtr.Zero, "Simulation done!", "GhostNet Simulation", MB_OK | MB_ICONINFORMATION);
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"[Simulator] Could not show popup: {ex.Message}");
            }
#endif
        }

        public struct FrameRecord
        {
            public int FrameIndex;
            public float SimulatedTime;
            public float NormalizedTime;
            public DateTime CurrentDay;
            public float TargetMaelstrom;
            public float CurrentMaelstrom;
            public float CurrentRatio;
        }
    }
}