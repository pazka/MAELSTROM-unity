
using System;
using System.Collections.Generic;
using System.Linq;
using Unity;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maelstrom.Unity
{
    public static class CommonMaelstrom
    {
        private static float HIGH_MAELSTROM_THRESHOLD = 0.99f;
        private static float MEDIUM_MAELSTROM_THRESHOLD = 0.80f;

        private static float currentMaelstrom = 0f;
        private static float targetMaelstrom = 0f;
        private static Queue<float> maelstromHistory = new Queue<float>();

        // UDP Network Integration
        private static IMaelstromUdpService _udpService;
        private static bool _isInitialized = false;
        private static PureDataConnector _pureData;

        private static int updateCount = 0;
        private static double netRnd = 0;

        /// <summary>
        /// Initialize the UDP service with the specified role
        /// </summary>
        /// <param name="roleId">1=corals, 2=ghostNet, 3=feed</param>
        public static void InitializeUdpService(ushort roleId, PureDataConnector pureData)
        {
            if (_isInitialized) return;

            _pureData = pureData;
            _udpService = new MaelstromUdpService();
            _udpService.SetLocalRole(roleId);
            _udpService.Start();
            _isInitialized = true;

            Debug.Log($"UDP Service initialized for role: {roleId}");
        }

        /// <summary>
        /// Cleanup UDP service resources
        /// </summary>
        public static void Cleanup()
        {
            if (_isInitialized)
            {
                _udpService?.Dispose();
                _udpService = null;
                _isInitialized = false;
            }
        }

        private static float[] GetExternalMaelstroms()
        {
            if (!_isInitialized) return new float[] { };

            return _udpService.GetExternalMaelstroms();
        }

        public static float UpdateMaelstrom(float currentRatio, float speedModifier = 1.0f, bool isCoral = false)
        {
            var rnd = new System.Random();
            var externalMaelstroms = GetExternalMaelstroms();
            var externalMaelstrom = externalMaelstroms.Length > 0 ? externalMaelstroms.Sum() / externalMaelstroms.Length : 0f;
            if (externalMaelstrom > 0)
            {
                Debug.Log($"Ext.Mal ({externalMaelstrom})");
            }

            // Check if any previous maelstrom values were above 0.7
            bool hasHighPreviousValues = maelstromHistory.Any(value => value >= 0.6f);
            var closeToTarget = Math.Abs(targetMaelstrom - currentMaelstrom) < 0.002f;

            if (closeToTarget)
            {
                netRnd = rnd.NextDouble();
                if (currentRatio > 0.3 && externalMaelstrom > 0.5 && !hasHighPreviousValues)
                {
                    targetMaelstrom = 1;
                    Debug.Log($"BIG Mal({netRnd}) : {targetMaelstrom}/{currentMaelstrom}");
                }
                else if (currentRatio > 0.3 && netRnd >= MEDIUM_MAELSTROM_THRESHOLD)
                {
                    targetMaelstrom = 0.7f;
                    Debug.Log($"MID Mal({netRnd}) : {targetMaelstrom}/{currentMaelstrom}");
                }
                else
                {
                    targetMaelstrom = Mathf.Lerp(currentMaelstrom, currentRatio, 0.1f);
                    Debug.Log($"Maelstrom Tgt/Crt : {currentRatio}, {targetMaelstrom}/{currentMaelstrom}, ext ({externalMaelstrom})");
                }
            }

            // Use inertia only if previous values were above 0.7
            float lerpSpeed = (hasHighPreviousValues ? 0.001f : 0.01f) * speedModifier;
            currentMaelstrom = Mathf.Lerp(currentMaelstrom, targetMaelstrom, lerpSpeed);

            // Store current maelstrom in history (keep max 100 values)
            maelstromHistory.Enqueue(targetMaelstrom);
            if (maelstromHistory.Count > 100)
            {
                maelstromHistory.Dequeue();
            }

            if (_isInitialized)
            {
                if (updateCount++ > 60) updateCount = 0;

                _udpService.PublishCurrenMaelstrom(Clamp01(currentMaelstrom));

                if (updateCount == 0)
                {
                    var allMaelstroms = _udpService.GetAllMaelstroms();
                    foreach (var kvp in allMaelstroms)
                    {
                        _pureData.SendOscMessage(kvp.Key, kvp.Value);
                    }
                }
            }
            return currentMaelstrom;
        }
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    }
}