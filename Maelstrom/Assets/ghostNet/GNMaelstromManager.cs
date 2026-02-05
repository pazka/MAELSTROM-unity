using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Manages the maelstrom value based on GhostNet data behavior
    /// </summary>
    public class GNMaelstromManager
    {
        private readonly Random rnd = new();
        private bool boundsRegistered;
        private int currentAccountCount;
        private DateTime currentDate;
        private float currentMaelstrom;
        private GhostNetDataBound dataBounds;
        private int maxAccountCount;
        private int minAccountCount = int.MaxValue;

        /// <summary>
        ///     Register data bounds during initial data loading to understand the data shape
        /// </summary>
        public void RegisterDataBounds(GhostNetDataPoint[] data)
        {
            var tmpDate = DateTime.MinValue;
            var tmpAccountCount = 0;

            foreach (var dataPoint in data)
            {
                var isNewDay = tmpDate != dataPoint.date.Date;

                if (isNewDay)
                {
                    if (tmpDate != DateTime.MinValue) // Skip first iteration
                    {
                        if (tmpAccountCount < minAccountCount) minAccountCount = tmpAccountCount;
                        if (tmpAccountCount > maxAccountCount) maxAccountCount = tmpAccountCount;
                    }

                    tmpAccountCount = 0;
                    tmpDate = dataPoint.date.Date;
                }

                if (!dataPoint.isAggregated) tmpAccountCount += 1;
            }

            // Handle the last day
            if (tmpAccountCount < minAccountCount) minAccountCount = tmpAccountCount;
            if (tmpAccountCount > maxAccountCount) maxAccountCount = tmpAccountCount;

            boundsRegistered = true;
            AppLogger.Log(
                $"GhostNet Maelstrom bounds registered - Min accounts: {minAccountCount}, Max accounts: {maxAccountCount}");
        }

        /// <summary>
        ///     Register individual data points for real-time processing
        /// </summary>
        public void RegisterData(GhostNetDataPoint data)
        {
            if (!boundsRegistered) throw new SystemException("no bound to compare maelstrom");

            var newDate = data.date.Date;
            var isNewDay = newDate != currentDate;

            //aggregated data is processed by particle system
            if (!data.isAggregated) currentAccountCount += 1;

            var normalizedAccountCount = currentAccountCount / (float)maxAccountCount;

            if (isNewDay)
            {
                AppLogger.Log($"Account tweeting:{currentAccountCount}/{maxAccountCount}");
                currentMaelstrom = CommonMaelstrom.UpdateMaelstrom(normalizedAccountCount, rnd.NextDouble());

                currentDate = newDate;
                currentAccountCount = 0;
            }
        }

        public void Update()
        {
            currentMaelstrom = CommonMaelstrom.ProgressMaelstrom();
        }

        /// <summary>
        ///     Get the current maelstrom value
        /// </summary>
        public float GetCurrentMaelstrom()
        {
            return currentMaelstrom;
        }

        /// <summary>
        ///     Process full dataset with RegisterData and dump maelstrom results to CSV
        /// </summary>
        public void SimulateAndDumpDailyMaelstrom(GhostNetDataPoint[] data)
        {
            if (!boundsRegistered)
            {
                AppLogger.LogError("Cannot simulate maelstrom: bounds not registered");
                return;
            }

            try
            {
                // Create a temporary maelstrom manager for simulation
                var simulationMaelstrom = new GNMaelstromManager();
                simulationMaelstrom.RegisterDataBounds(data);

                // Sort data chronologically
                var sortedData = data.OrderBy(dp => dp.date).ToArray();

                // Store maelstrom values for each data point
                var maelstromResults = new List<(DateTime date, int accountCount, float maelstromValue)>();

                // Process each data point chronologically
                foreach (var dataPoint in sortedData)
                {
                    simulationMaelstrom.RegisterData(dataPoint);

                    // Store the maelstrom value after processing this data point
                    for (var i = 0; i < 1000; i++) simulationMaelstrom.Update();
                    maelstromResults.Add((
                        dataPoint.date,
                        dataPoint.nb_accounts_others,
                        simulationMaelstrom.GetCurrentMaelstrom()
                    ));
                }

                var fileName = $"ghostNet_maelstrom_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (var writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date;accountCount;maelstromValue");

                    // Write data for each data point
                    foreach (var result in maelstromResults)
                        writer.WriteLine(
                            $"{result.date:yyyy-MM-dd HH:mm:ss};{result.accountCount};{result.maelstromValue:F6}");
                }

                AppLogger.Log($"GhostNet maelstrom results dumped to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Failed to simulate and dump GhostNet maelstrom results: {ex.Message}");
            }
        }
    }
}