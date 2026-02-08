using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Manages the maelstrom value based on Feed retweet data behavior
    /// </summary>
    public class FeedMaelstromManager
    {
        private readonly Random rnd = new();
        private bool boundsRegistered;
        private DateTime currentDate;
        private float currentMaelstrom;
        private int currentRetweetCount;
        private FeedDataBound dataBounds;
        private int maxRetweetCount;
        private int minRetweetCount = int.MaxValue;


        /// <summary>
        ///     Register data bounds during initial data loading to understand the data shape
        /// </summary>
        public void RegisterDataBounds(FeedDataPoint[] data)
        {
            var tmpDate = DateTime.MinValue;
            var tmpRetweetCount = 0;

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

            AppLogger.Log(
                $"Feed Maelstrom bounds registered - Min retweets: {minRetweetCount}, Max retweets: {maxRetweetCount}");
        }

        /// <summary>
        ///     Register individual data points for real-time processing.
        ///     Returns the currentRatio (normalized retweet count).
        /// </summary>
        public void RegisterData(FeedDataPoint data, bool silent = false)
        {
            var newDate = data.date.Date;
            var isNewDay = newDate != currentDate;

            currentRetweetCount += data.retweetCount;
            var normalizedRetweetCount = currentRetweetCount / (float)maxRetweetCount;

            if (isNewDay)
            {
                if (!silent)
                    AppLogger.Log(
                        $"DATA:{normalizedRetweetCount:F2}, RT:{currentRetweetCount}/{maxRetweetCount}");

                currentMaelstrom = CommonMaelstrom.UpdateMaelstrom(normalizedRetweetCount, rnd.NextDouble(), silent);

                currentDate = newDate;
                currentRetweetCount = 0;
            }
        }

        public void Update(bool silent = false)
        {
            currentMaelstrom = CommonMaelstrom.ProgressMaelstrom(silent: silent);
        }

        /// <summary>
        ///     Get the current maelstrom value
        /// </summary>
        public float GetCurrentMaelstrom()
        {
            return currentMaelstrom;
        }

        /// <summary>
        ///     Get the current retweet count for the day
        /// </summary>
        public int GetCurrentRetweetCount()
        {
            return currentRetweetCount;
        }

        /// <summary>
        ///     Get the minimum retweet count across all days
        /// </summary>
        public int GetMinRetweetCount()
        {
            return minRetweetCount;
        }

        /// <summary>
        ///     Get the maximum retweet count across all days
        /// </summary>
        public int GetMaxRetweetCount()
        {
            return maxRetweetCount;
        }

        /// <summary>
        ///     Check if bounds have been registered
        /// </summary>
        public bool IsBoundsRegistered()
        {
            return boundsRegistered;
        }

        /// <summary>
        ///     Reset the maelstrom manager state for clean simulation runs
        /// </summary>
        public void Reset()
        {
            currentRetweetCount = 0;
            currentDate = DateTime.MinValue;
            currentMaelstrom = 0f;
        }

        /// <summary>
        ///     Process full dataset with RegisterData and dump maelstrom results to CSV
        /// </summary>
        public void SimulateAndDumpDailyMaelstrom(FeedDataPoint[] data)
        {
            if (!boundsRegistered)
            {
                AppLogger.LogError("Cannot simulate maelstrom: bounds not registered");
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

                    for (var i = 0; i < 1000; i++) simulationMaelstrom.Update();
                    // Store the maelstrom value after processing this data point
                    maelstromResults.Add((
                        dataPoint.date,
                        dataPoint.retweetCount,
                        simulationMaelstrom.GetCurrentMaelstrom()
                    ));
                }

                var fileName = $"feed_maelstrom_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (var writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date;retweetCount;maelstromValue");

                    // Write data for each data point
                    foreach (var result in maelstromResults)
                        writer.WriteLine(
                            $"{result.date:yyyy-MM-dd HH:mm:ss};{result.retweetCount};{result.maelstromValue:F6}");
                }

                AppLogger.Log($"Feed maelstrom results dumped to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Failed to simulate and dump Feed maelstrom results: {ex.Message}");
            }
        }
    }
}