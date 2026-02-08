using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Runs a headless simulation of the Corals maelstrom for data export.
    /// </summary>
    public class CoralsSimulator
    {
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONINFORMATION = 0x00000040;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        /// <summary>
        ///     Runs the simulation and exports results to CSV.
        /// </summary>
        public void RunSimulation(
            CoralDataPoint[] data,
            CoralDataBound bounds,
            int loopDuration,
            int targetLoops = 2,
            int targetFps = 30)
        {
            AppLogger.Log($"[CoralsSimulator] Starting simulation: {targetLoops} loops at {targetFps} FPS");

            CommonMaelstrom.Reset();

            var maelstromManager = new CoralsMaelstromManager();
            maelstromManager.RegisterDataBounds(data);
            maelstromManager.Reset();

            var timeController = new CoralsTimeController(loopDuration);

            var frameRecords = new List<FrameRecord>();
            var deltaTime = 1f / targetFps;
            var totalSimulationTime = loopDuration * targetLoops;
            var totalFrames = totalSimulationTime * targetFps;
            var frameIndex = 0;

            AppLogger.Log($"[CoralsSimulator] Total frames to simulate: {totalFrames}");

            while (timeController.LoopCount < targetLoops)
            {
                var looped = timeController.AdvanceTime(deltaTime);

                if (looped)
                {
                    maelstromManager.Reset();
                    CommonMaelstrom.Reset();
                }

                timeController.FindInterpolationIndices(data);

                if (timeController.ShouldProcessNewDataPoint())
                {
                    var dataIdx = timeController.GetDataIndexToProcess(data);
                    if (dataIdx < data.Length) maelstromManager.RegisterData(data[dataIdx], true);
                    timeController.MarkDataProcessed();
                }
                else
                {
                    maelstromManager.Update(true);
                }

                var (alphaPos, alphaNeu, alphaNeg) = timeController.InterpolateAlphas(data);

                var currentDate = DateTime.MinValue;
                var dataIndex = timeController.GetDataIndexToProcess(data);
                if (dataIndex < data.Length)
                    currentDate = data[dataIndex].date;

                var record = new FrameRecord
                {
                    FrameIndex = frameIndex,
                    SimulatedTime = timeController.CurrentTime,
                    NormalizedTime = timeController.CurrentNormalizedTime,
                    CurrentDate = currentDate,
                    TargetMaelstrom = CommonMaelstrom.GetTargetMaelstrom(),
                    CurrentMaelstrom = CommonMaelstrom.GetCurrentMaelstrom(),
                    CurrentRatio = CommonMaelstrom.GetCurrentRatio(),
                    AlphaPos = alphaPos,
                    AlphaNeu = alphaNeu,
                    AlphaNeg = alphaNeg
                };
                frameRecords.Add(record);

                frameIndex++;

                if (frameIndex % 1000 == 0)
                    AppLogger.Log(
                        $"[CoralsSimulator] Progress: {frameIndex}/{totalFrames} frames ({100f * frameIndex / totalFrames:F1}%)");
            }

            AppLogger.Log($"[CoralsSimulator] Simulation complete. {frameRecords.Count} frames recorded.");

            ExportToCsv(frameRecords);

            ShowCompletionPopup();
        }

        private void ExportToCsv(List<FrameRecord> records)
        {
            try
            {
                var fileName = $"corals_simulation_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine(
                        "frameIndex\tsimulatedTime\tnormalizedTime\tcurrentDate\ttargetMaelstrom\tcurrentMaelstrom\tcurrentRatio\talphaPos\talphaNeu\talphaNeg");

                    foreach (var record in records)
                        writer.WriteLine(
                            $"{record.FrameIndex}\t" +
                            $"{record.SimulatedTime:F3}\t" +
                            $"{record.NormalizedTime:F6}\t" +
                            $"{record.CurrentDate:yyyy-MM-dd}\t" +
                            $"{record.TargetMaelstrom:F6}\t" +
                            $"{record.CurrentMaelstrom:F6}\t" +
                            $"{record.CurrentRatio:F6}\t" +
                            $"{record.AlphaPos:F6}\t" +
                            $"{record.AlphaNeu:F6}\t" +
                            $"{record.AlphaNeg:F6}");
                }

                AppLogger.Log($"[CoralsSimulator] Results exported to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[CoralsSimulator] Failed to export CSV: {ex.Message}");
            }
        }

        private void ShowCompletionPopup()
        {
            AppLogger.Log("[CoralsSimulator] Simulation done!");

#if UNITY_EDITOR
            EditorUtility.DisplayDialog("Simulation Complete", "Corals simulation done!", "OK");
#else
            try
            {
                MessageBox(IntPtr.Zero, "Corals simulation done!", "Corals Simulation", MB_OK | MB_ICONINFORMATION);
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"[CoralsSimulator] Could not show popup: {ex.Message}");
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
            public float AlphaPos;
            public float AlphaNeu;
            public float AlphaNeg;
        }
    }
}