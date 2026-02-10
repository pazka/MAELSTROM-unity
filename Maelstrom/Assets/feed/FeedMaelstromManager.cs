using System;
using Random = System.Random;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Manages the maelstrom value based on Feed retweet data behavior
    /// </summary>
    public class FeedMaelstromManager
    {
        private readonly Random rnd = new();
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
            currentMaelstrom = CommonMaelstrom.ProgressMaelstrom(1f, silent);
        }

        /// <summary>
        ///     Get the current maelstrom value
        /// </summary>
        public float GetCurrentMaelstrom()
        {
            return currentMaelstrom;
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
    }
}