
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int MAELSTROM_INERTIA_DAYS = 7;
        private const float HIGH_MAELSTROM_THRESHOLD = 0.9f;
        private const float MEDIUM_MAELSTROM_THRESHOLD = 0.7f;
        private const float HIGH_MAELSTROM_PERCENTILE = 0.99f; // 1% of the time
        private const float MEDIUM_MAELSTROM_PERCENTILE = 0.94f; // 6% of the time

        // Historical data for 7-day inertia calculation
        private Queue<int> historicalAccountCounts = new Queue<int>();
        private Queue<DateTime> historicalDates = new Queue<DateTime>();

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
                    if (tmpDate != DateTime.MinValue) // Skip first iteration
                    {
                        if (tmpAccountCount < minAccountCount) minAccountCount = tmpAccountCount;
                        if (tmpAccountCount > maxAccountCount) maxAccountCount = tmpAccountCount;
                    }
                    tmpAccountCount = 0;
                    tmpDate = dataPoint.date.Date;
                }

                tmpAccountCount += dataPoint.nb_accounts_others;
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
            DateTime currentDate = data.date.Date;
            var isNewDay = currentDate != this.currentDate;

            if (isNewDay)
            {
                UpdateMaesltrom();
                this.currentDate = currentDate;
                this.currentAccountCount = 0;
            }

            this.currentAccountCount += data.nb_accounts_others;
        }

        /// <summary>
        /// Update the maelstrom value based on recent data with 7-day inertia
        /// </summary>
        public void UpdateMaesltrom()
        {
            if (!boundsRegistered) return;

            // Add current day's account count to historical data
            historicalAccountCounts.Enqueue(currentAccountCount);
            historicalDates.Enqueue(currentDate);

            // Remove data older than 7 days
            while (historicalDates.Count > 0 &&
                   (currentDate - historicalDates.Peek()).TotalDays > MAELSTROM_INERTIA_DAYS)
            {
                historicalAccountCounts.Dequeue();
                historicalDates.Dequeue();
            }

            if (historicalAccountCounts.Count == 0) return;

            // Calculate average account count over the 7-day period
            float averageAccountCount = (float)historicalAccountCounts.Average();

            // Calculate maelstrom based on how current day compares to historical average
            float accountCountRange = maxAccountCount - minAccountCount;
            if (accountCountRange <= 0) return;

            // Normalize current account count relative to historical range
            float normalizedCurrentCount = (currentAccountCount - minAccountCount) / accountCountRange;
            float normalizedAverageCount = (averageAccountCount - minAccountCount) / accountCountRange;

            // Calculate maelstrom intensity based on deviation from average
            float deviation = Math.Abs(normalizedCurrentCount - normalizedAverageCount);

            // Apply percentile-based thresholds
            if (normalizedCurrentCount >= HIGH_MAELSTROM_PERCENTILE)
            {
                currentMaelstrom = HIGH_MAELSTROM_THRESHOLD + (normalizedCurrentCount - HIGH_MAELSTROM_PERCENTILE) * 0.1f;
            }
            else if (normalizedCurrentCount >= MEDIUM_MAELSTROM_PERCENTILE)
            {
                currentMaelstrom = MEDIUM_MAELSTROM_THRESHOLD +
                    (normalizedCurrentCount - MEDIUM_MAELSTROM_PERCENTILE) /
                    (HIGH_MAELSTROM_PERCENTILE - MEDIUM_MAELSTROM_PERCENTILE) *
                    (HIGH_MAELSTROM_THRESHOLD - MEDIUM_MAELSTROM_THRESHOLD);
            }
            else
            {
                currentMaelstrom = normalizedCurrentCount * MEDIUM_MAELSTROM_THRESHOLD / MEDIUM_MAELSTROM_PERCENTILE;
            }

            // Apply inertia smoothing - blend with previous maelstrom value
            if (historicalAccountCounts.Count > 1)
            {
                float inertiaFactor = 1.0f / historicalAccountCounts.Count;
                float previousMaelstrom = currentMaelstrom;
                currentMaelstrom = previousMaelstrom * (1 - inertiaFactor) + currentMaelstrom * inertiaFactor;
            }

            // Clamp to valid range
            currentMaelstrom = Math.Max(0f, Math.Min(1f, currentMaelstrom));
            Console.WriteLine($"Maelstrom Ghostnet {currentMaelstrom}");
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

    }
}