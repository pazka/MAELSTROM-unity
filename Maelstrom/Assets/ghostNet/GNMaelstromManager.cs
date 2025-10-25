
using System;
using System.Collections.Generic;
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

    }
}