using System;
using System.Collections.Generic;
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

            currentMaelstrom = CommonMaelstrom.UpdateMaelstrom((float)currentRetweetCount / (float)maxRetweetCount);

            this.currentRetweetCount += data.retweetCount;
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
    }
}
