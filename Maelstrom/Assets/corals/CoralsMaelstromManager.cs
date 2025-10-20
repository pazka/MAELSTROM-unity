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
            currentMaelstrom = CommonMaelstrom.UpdateMaelstrom((float)currentNegativeSentiment / (float)maxNegativeSentiment);

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
    }
}
