using System;
using System.Collections.Generic;
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
        private float currentMaelstrom = 0f;
        private DateTime currentDate;
        private float currentNegativeSentiment = 0f;
        private float minNegativeSentiment = float.MaxValue;
        private float maxNegativeSentiment = 0f;
        private const float HIGH_MAELSTROM_THRESHOLD = 0.99f;
        private const float MEDIUM_MAELSTROM_THRESHOLD = 0.94f;

        private float targetMaelstrom;

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
            UpdateMaelstrom();

            this.currentNegativeSentiment = data.neg;
        }

        public void UpdateMaelstrom()
        {
            var rnd = new System.Random();
            var currentRatio = (float)currentNegativeSentiment / (float)maxNegativeSentiment;
            var newMaelstrom = currentMaelstrom;

            if (rnd.NextDouble() >= HIGH_MAELSTROM_THRESHOLD)
            {
                targetMaelstrom = 1;
            }
            else if (rnd.NextDouble() >= MEDIUM_MAELSTROM_THRESHOLD)
            {
                targetMaelstrom = 0.7f;
            }
            else if (targetMaelstrom < 0.7 || (currentMaelstrom - targetMaelstrom) < 0.02)
            {
                targetMaelstrom = Mathf.Lerp(currentRatio, currentMaelstrom, 0.01f);
            }

            currentMaelstrom = Mathf.Lerp(currentMaelstrom, targetMaelstrom, 0.4f);
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
    }
}
