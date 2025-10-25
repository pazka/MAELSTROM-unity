using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Manages the maelstrom value based on Feed retweet data behavior
    /// </summary>
    public class FeedMaelstromManager
    {
        private FeedDataBound dataBounds;
        private bool boundsRegistered = false;
        private float currentMaelstrom = 0f;
        private DateTime currentDate;
        private int currentRetweetCount = 0;
        private int minRetweetCount = int.MaxValue;
        private int maxRetweetCount = 0;

        /// <summary>
        /// Register data bounds during initial data loading to understand the data shape
        /// </summary>
        public void RegisterDataBounds(FeedDataPoint[] data)
        {
            DateTime tmpDate = DateTime.MinValue;
            int tmpRetweetCount = 0;

            foreach (var dataPoint in data)
            {
                var isNewDay = tmpDate != dataPoint.date.Date;

                if (isNewDay)
                {
                    if (tmpDate != DateTime.MinValue) // Skip first iteration
                    {
                        if (tmpRetweetCount < minRetweetCount) minRetweetCount = tmpRetweetCount;
                        if (tmpRetweetCount > maxRetweetCount) maxRetweetCount = tmpRetweetCount;
                    }
                    tmpRetweetCount = 0;
                    tmpDate = dataPoint.date.Date;
                }

                tmpRetweetCount += dataPoint.retweetCount;
            }

            // Handle the last day
            if (tmpRetweetCount < minRetweetCount) minRetweetCount = tmpRetweetCount;
            if (tmpRetweetCount > maxRetweetCount) maxRetweetCount = tmpRetweetCount;

            boundsRegistered = true;

            Debug.Log($"Feed Maelstrom bounds registered - Min retweets: {minRetweetCount}, Max retweets: {maxRetweetCount}");
        }

        /// <summary>
        /// Register individual data points for real-time processing
        /// </summary>
        public void RegisterData(FeedDataPoint data)
        {
            DateTime currentDate = data.date.Date;
            var isNewDay = currentDate != this.currentDate;

            if (isNewDay)
            {
                this.currentDate = currentDate;
                this.currentRetweetCount = 0;
            }

            this.currentRetweetCount += data.retweetCount;

            currentMaelstrom = CommonMaelstrom.UpdateMaelstrom((float)currentRetweetCount / (float)maxRetweetCount);

        }

        /// <summary>
        /// Get the current maelstrom value
        /// </summary>
        public float GetCurrentMaelstrom()
        {
            return currentMaelstrom;
        }

        /// <summary>
        /// Get the current retweet count for the day
        /// </summary>
        public int GetCurrentRetweetCount()
        {
            return currentRetweetCount;
        }

        /// <summary>
        /// Get the minimum retweet count across all days
        /// </summary>
        public int GetMinRetweetCount()
        {
            return minRetweetCount;
        }

        /// <summary>
        /// Get the maximum retweet count across all days
        /// </summary>
        public int GetMaxRetweetCount()
        {
            return maxRetweetCount;
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
        public void SimulateAndDumpDailyMaelstrom(FeedDataPoint[] data)
        {
            if (!boundsRegistered)
            {
                Debug.LogError("Cannot simulate maelstrom: bounds not registered");
                return;
            }

            try
            {
                // Create a temporary maelstrom manager for simulation
                var simulationMaelstrom = new FeedMaelstromManager();
                simulationMaelstrom.RegisterDataBounds(data);

                // Sort data chronologically
                var sortedData = data.OrderBy(dp => dp.date).ToArray();

                // Store maelstrom values for each data point
                var maelstromResults = new List<(DateTime date, int retweetCount, float maelstromValue)>();

                // Process each data point chronologically
                foreach (var dataPoint in sortedData)
                {
                    simulationMaelstrom.RegisterData(dataPoint);
                    
                    // Store the maelstrom value after processing this data point
                    maelstromResults.Add((
                        dataPoint.date,
                        dataPoint.retweetCount,
                        simulationMaelstrom.GetCurrentMaelstrom()
                    ));
                }

                string fileName = $"feed_maelstrom_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date;retweetCount;maelstromValue");

                    // Write data for each data point
                    foreach (var result in maelstromResults)
                    {
                        writer.WriteLine($"{result.date:yyyy-MM-dd HH:mm:ss};{result.retweetCount};{result.maelstromValue:F6}");
                    }
                }

                Debug.Log($"Feed maelstrom results dumped to: {filePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to simulate and dump Feed maelstrom results: {ex.Message}");
            }
        }
    }
}
