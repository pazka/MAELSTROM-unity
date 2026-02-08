using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Runs a headless simulation of the Feed maelstrom for data export.
    /// </summary>
    public class FeedSimulator
    {
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONINFORMATION = 0x00000040;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        /// <summary>
        ///     Runs the simulation and exports results to CSV.
        /// </summary>
        public void RunSimulation(
            FeedDataPoint[] data,
            FeedDataBound bounds,
            int loopDuration,
            int targetLoops = 2,
            int targetFps = 30)
        {
            AppLogger.Log($"[FeedSimulator] Starting simulation: {targetLoops} loops at {targetFps} FPS");

            CommonMaelstrom.Reset();

            var maelstromManager = new FeedMaelstromManager();
            maelstromManager.RegisterDataBounds(data);
            maelstromManager.Reset();

            var timeController = new FeedTimeController(loopDuration);

            var frameRecords = new List<FrameRecord>();
            var deltaTime = 1f / targetFps;
            var totalSimulationTime = loopDuration * targetLoops;
            var totalFrames = totalSimulationTime * targetFps;
            var frameIndex = 0;

            AppLogger.Log($"[FeedSimulator] Total frames to simulate: {totalFrames}");

            while (timeController.LoopCount < targetLoops)
            {
                timeController.AdvanceTime(deltaTime);

                var dataPointsToProcess = timeController.GetNbDataPointsToProcess(data);

                for (var i = 0; i < dataPointsToProcess; i++)
                {
                    var dataIdx = timeController.GetDataIndexForProcess(i);
                    if (dataIdx < data.Length)
                    {
                        var dataPoint = data[dataIdx];
                        maelstromManager.RegisterData(dataPoint, true);
                        timeController.MarkDataProcessed(data[dataIdx]);
                    }
                }


                maelstromManager.Update(true);

                var record = new FrameRecord
                {
                    FrameIndex = frameIndex,
                    SimulatedTime = timeController.CurrentTime,
                    NormalizedTime = timeController.CurrentNormalizedTime,
                    CurrentDate = timeController.CurrentDisplayedDate,
                    TargetMaelstrom = CommonMaelstrom.GetTargetMaelstrom(),
                    CurrentMaelstrom = CommonMaelstrom.GetCurrentMaelstrom(),
                    CurrentRatio = CommonMaelstrom.GetCurrentRatio()
                };
                frameRecords.Add(record);

                frameIndex++;

                if (frameIndex % 1000 == 0)
                    AppLogger.Log(
                        $"[FeedSimulator] Progress: {frameIndex}/{totalFrames} frames ({100f * frameIndex / totalFrames:F1}%)");
            }

            AppLogger.Log($"[FeedSimulator] Simulation complete. {frameRecords.Count} frames recorded.");

            ExportToCsv(frameRecords);

            ShowCompletionPopup();
        }

        private void ExportToCsv(List<FrameRecord> records)
        {
            try
            {
                var fileName = $"feed_simulation_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine(
                        "frameIndex\tsimulatedTime\tnormalizedTime\tcurrentDate\ttargetMaelstrom\tcurrentMaelstrom\tcurrentRatio");

                    foreach (var record in records)
                        writer.WriteLine(
                            $"{record.FrameIndex}\t" +
                            $"{record.SimulatedTime:F3}\t" +
                            $"{record.NormalizedTime:F6}\t" +
                            $"{record.CurrentDate:yyyy-MM-dd}\t" +
                            $"{record.TargetMaelstrom:F6}\t" +
                            $"{record.CurrentMaelstrom:F6}\t" +
                            $"{record.CurrentRatio:F6}");
                }

                AppLogger.Log($"[FeedSimulator] Results exported to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[FeedSimulator] Failed to export CSV: {ex.Message}");
            }
        }

        private void ShowCompletionPopup()
        {
            AppLogger.Log("[FeedSimulator] Simulation done!");

#if UNITY_EDITOR
            EditorUtility.DisplayDialog("Simulation Complete", "Feed simulation done!", "OK");
#else
            try
            {
                MessageBox(IntPtr.Zero, "Feed simulation done!", "Feed Simulation", MB_OK | MB_ICONINFORMATION);
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"[FeedSimulator] Could not show popup: {ex.Message}");
            }
#endif
        }

        public struct FrameRecord
        {
            public int FrameIndex;
            public float SimulatedTime;
            public float NormalizedTime;
            public DateTime CurrentDate;
            public float TargetMaelstrom;
            public float CurrentMaelstrom;
            public float CurrentRatio;
        }
    }
}