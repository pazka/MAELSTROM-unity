
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
        private const float HIGH_MAELSTROM_THRESHOLD = 0.99f;
        private const float MEDIUM_MAELSTROM_THRESHOLD = 0.94f;

        private float targetMaelstrom;

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

        public void UpdateMaesltrom()
        {
            var rnd = new System.Random();
            var currentRatio = (float)currentAccountCount / (float)maxAccountCount;
            var newMaelstrom = currentMaelstrom;

            if (rnd.NextDouble() >= HIGH_MAELSTROM_THRESHOLD)
            {
                targetMaelstrom = 1;
                Debug.Log($"Maelstrom Ghostnet {currentMaelstrom}");
            }
            else if (rnd.NextDouble() >= MEDIUM_MAELSTROM_THRESHOLD)
            {
                targetMaelstrom = 0.7f;
                Debug.Log($"Semi-Maelstrom Ghostnet {currentMaelstrom}");
            }
            else if (targetMaelstrom < 0.7 || (currentMaelstrom - targetMaelstrom) < 0.02)
            {
                targetMaelstrom = (float)(currentRatio + currentMaelstrom * 0.3);
            }

            currentMaelstrom = Mathf.Lerp(currentMaelstrom, targetMaelstrom, 0.5f);
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