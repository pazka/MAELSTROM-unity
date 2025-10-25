
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Manages the maelstrom value based on GhostNet data behavior
    /// </summary>
    public class GNMaelstromManager
    {
        private GhostNetDataBound dataBounds;
        private bool boundsRegistered = false;
        private float currentMaelstrom = 0f;
        private DateTime currentDate;
        private int currentAccountCount = 0;
        private int minAccountCount = int.MaxValue;
        private int maxAccountCount = 0;

        /// <summary>
        /// Register data bounds during initial data loading to understand the data shape
        /// </summary>
        public void RegisterDataBounds(GhostNetDataPoint[] data)
        {
            DateTime tmpDate = DateTime.MinValue;
            int tmpAccountCount = 0;

            foreach (var dataPoint in data)
            {
                var isNewDay = tmpDate != dataPoint.date.Date;

                if (isNewDay)
                {
                    if (tmpDate != DateTime.MinValue ) // Skip first iteration
                    {
                        if (tmpAccountCount < minAccountCount) minAccountCount = tmpAccountCount;
                        if (tmpAccountCount > maxAccountCount) maxAccountCount = tmpAccountCount;
                    }
                    tmpAccountCount = 0;
                    tmpDate = dataPoint.date.Date;
                }

                if (!dataPoint.isAggregated)
                {    
                    tmpAccountCount += 1;   
                }
            }

            // Handle the last day
            if (tmpAccountCount < minAccountCount) minAccountCount = tmpAccountCount;
            if (tmpAccountCount > maxAccountCount) maxAccountCount = tmpAccountCount;

            boundsRegistered = true;
        }

        /// <summary>
        /// Register individual data points for real-time processing
        /// </summary>
        public void RegisterData(GhostNetDataPoint data)
        {
            if (!boundsRegistered)
            {
                throw new SystemException("no bound to compare maelstrom");
            }

            DateTime currentDate = data.date.Date;
            var isNewDay = currentDate != this.currentDate;

            if (isNewDay)
            {
                this.currentDate = currentDate;
                this.currentAccountCount = 0;
            }

            currentMaelstrom = CommonMaelstrom.UpdateMaelstrom((float)currentAccountCount / (float)maxAccountCount);

            if (!data.isAggregated)
            {
                this.currentAccountCount += data.nb_accounts_others;
            }
        }

        /// <summary>
        /// Get the current maelstrom value
        /// </summary>
        public float GetCurrentMaelstrom()
        {
            return currentMaelstrom;
        }

        /// <summary>
        /// Get the current account count for the day
        /// </summary>
        public int GetCurrentAccountCount()
        {
            return currentAccountCount;
        }

        /// <summary>
        /// Check if bounds have been registered
        /// </summary>
        public bool IsBoundsRegistered()
        {
            return boundsRegistered;
        }

        /// <summary>
        /// Process full dataset with RegisterData and dump maelstrom results to CSV
        /// </summary>
        public void SimulateAndDumpDailyMaelstrom(GhostNetDataPoint[] data)
        {
            if (!boundsRegistered)
            {
                Debug.LogError("Cannot simulate maelstrom: bounds not registered");
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
                    maelstromResults.Add((
                        dataPoint.date,
                        dataPoint.nb_accounts_others,
                        simulationMaelstrom.GetCurrentMaelstrom()
                    ));
                }

                string fileName = $"ghostNet_maelstrom_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date;accountCount;maelstromValue");

                    // Write data for each data point
                    foreach (var result in maelstromResults)
                    {
                        writer.WriteLine($"{result.date:yyyy-MM-dd HH:mm:ss};{result.accountCount};{result.maelstromValue:F6}");
                    }
                }

                Debug.Log($"GhostNet maelstrom results dumped to: {filePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to simulate and dump GhostNet maelstrom results: {ex.Message}");
            }
        }

    }
}