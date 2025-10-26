using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Manages the maelstrom value based on Corals negative sentiment data behavior
    /// </summary>
    public class CoralsMaelstromManager
    {
        private CoralDataBound dataBounds;
        private bool boundsRegistered = false;
        private DateTime currentDate;
        private float currentNegativeSentiment = 0f;
        private float minNegativeSentiment = float.MaxValue;
        private float maxNegativeSentiment = 0f;
        private float currentMaelstrom = 0f;


        /// <summary>
        /// Register data bounds during initial data loading to understand the data shape
        /// </summary>
        public void RegisterDataBounds(CoralDataPoint[] data)
        {
            foreach (var dataPoint in data)
            {
                if (dataPoint.neg < minNegativeSentiment) minNegativeSentiment = dataPoint.neg;
                if (dataPoint.neg > maxNegativeSentiment) maxNegativeSentiment = dataPoint.neg;
            }

            boundsRegistered = true;
        }

        /// <summary>
        /// Register individual data points for real-time processing
        /// </summary>
        public void RegisterData(CoralDataPoint data)
        {
            if (!boundsRegistered)
            {
                throw new SystemException("no bound to compare maelstrom");
            }
            currentMaelstrom = CommonMaelstrom.UpdateMaelstrom((float)data.neg / (float)maxNegativeSentiment, 10f);

            this.currentNegativeSentiment = data.neg;
        }


        /// <summary>
        /// Get the current maelstrom value
        /// </summary>
        public float GetCurrentMaelstrom()
        {
            return currentMaelstrom;
        }

        /// <summary>
        /// Get the current negative sentiment value for the day
        /// </summary>
        public float GetCurrentNegativeSentiment()
        {
            return currentNegativeSentiment;
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
        public void SimulateAndDumpDailyMaelstrom(CoralDataPoint[] data)
        {
            if (!boundsRegistered)
            {
                Debug.LogError("Cannot simulate maelstrom: bounds not registered");
                return;
            }

            try
            {
                // Create a temporary maelstrom manager for simulation
                var simulationMaelstrom = new CoralsMaelstromManager();
                simulationMaelstrom.RegisterDataBounds(data);

                // Sort data chronologically
                var sortedData = data.OrderBy(dp => dp.date).ToArray();

                // Store maelstrom values for each data point
                var maelstromResults = new List<(DateTime date, float negativeSentiment, float maelstromValue)>();

                // Process each data point chronologically
                foreach (var dataPoint in sortedData)
                {
                    simulationMaelstrom.RegisterData(dataPoint);

                    // Store the maelstrom value after processing this data point
                    maelstromResults.Add((
                        dataPoint.date,
                        dataPoint.neg,
                        simulationMaelstrom.GetCurrentMaelstrom()
                    ));
                }

                string fileName = $"corals_maelstrom_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date;negativeSentiment;maelstromValue");

                    // Write data for each data point
                    foreach (var result in maelstromResults)
                    {
                        writer.WriteLine($"{result.date:yyyy-MM-dd HH:mm:ss};{result.negativeSentiment:F6};{result.maelstromValue:F6}");
                    }
                }

                Debug.Log($"Corals maelstrom results dumped to: {filePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to simulate and dump Corals maelstrom results: {ex.Message}");
            }
        }
    }
}
