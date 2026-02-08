using System;
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
        private int maxAccountCount;
        private int minAccountCount = int.MaxValue;

        /// <summary>
        ///     Register data bounds during initial data loading to understand the data shape
        /// </summary>
        public void RegisterDataBounds(GhostNetDataPoint[] data)
        {
            var tmpDate = data[0].date.Date;
            var tmpAccountCount = 0;

            foreach (var dataPoint in data)
            {
                var isNewDay = tmpDate != dataPoint.date.Date;

                if (isNewDay)
                {
                    if (tmpAccountCount < minAccountCount) minAccountCount = tmpAccountCount;
                    if (tmpAccountCount > maxAccountCount) maxAccountCount = tmpAccountCount;

                    tmpAccountCount = 0;
                    tmpDate = dataPoint.date.Date;
                }

                if (!dataPoint.isAggregated) tmpAccountCount += 1;
            }

            // Handle the last day
            if (tmpAccountCount < minAccountCount) minAccountCount = tmpAccountCount;
            if (tmpAccountCount > maxAccountCount) maxAccountCount = tmpAccountCount;

            maxAccountCount = 2000;
            boundsRegistered = true;
            AppLogger.Log(
                $"GhostNet Maelstrom bounds registered - Min accounts: {minAccountCount}, Max accounts: {maxAccountCount}");
        }

        /// <summary>
        ///     Register individual data points for real-time processing.
        ///     Returns the currentRatio (normalized account count).
        /// </summary>
        public void RegisterData(GhostNetDataPoint data, bool silent = false)
        {
            if (!boundsRegistered) throw new SystemException("no bound to compare maelstrom");

            var newDate = data.date.Date;
            var isNewDay = newDate != currentDate;

            if (!data.isAggregated) currentAccountCount += 1;

            var normalizedAccountCount = currentAccountCount / (float)maxAccountCount;
            // soft log linearization (boosts low values, saturates high ones)
            const float k = 10; // tune: 10 = mild, 30 = strong, 50 = very strong
            var x = CommonMaelstrom.Clamp01(normalizedAccountCount);

            // var linearizedRatio =
            //     (float)(Math.Log10(1 + k * x) / Math.Log10(1 + k));
            var linearizedRatio = x;
            if (isNewDay)
            {
                if (!silent)
                    AppLogger.Log($"Account tweeting:{currentAccountCount}/{maxAccountCount}");

                currentMaelstrom = CommonMaelstrom.UpdateMaelstrom(linearizedRatio, rnd.NextDouble(), silent);

                currentDate = newDate;
                currentAccountCount = 0;
            }
        }

        public void Update(bool silent = false)
        {
            currentMaelstrom = CommonMaelstrom.ProgressMaelstrom(3f, silent);
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
            currentAccountCount = 0;
            currentDate = DateTime.MinValue;
            currentMaelstrom = 0f;
        }
    }
}