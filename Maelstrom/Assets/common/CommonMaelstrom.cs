using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Maelstrom.Unity
{
    public static class CommonMaelstrom
    {
        public enum RoleId
        {
            Debug = 0,
            DeadComunities = 1,
            GhostNet = 2,
            Feed = 3
        }

        private static float HIGH_MAELSTROM_THRESHOLD = 0.99f;
        private static readonly float MEDIUM_MAELSTROM_THRESHOLD = 0.80f;

        private static float currentMaelstrom;
        private static float targetMaelstrom;
        private static readonly Queue<float> maelstromHistory = new();

        // Network Integration
        private static bool _isInitialized;
        private static PureDataConnector _pureData;

        // Maelstrom-specific network state
        private static readonly ConcurrentDictionary<RoleId, float> externalMaelstrom =
            new();

        private static float localMaelstrom;
        private static RoleId localRoleId; // 1=corals,2=ghostNet,3=feed

        private static int updateCount;
        private static double netRnd;
        public static string[] RoleKeys = { "debug", "deadComunities", "ghostNet", "feed" };
        public static RoleId[] RoleIds = { RoleId.Debug, RoleId.DeadComunities, RoleId.Feed, RoleId.GhostNet };

        public static string RoleToKey(RoleId role)
        {
            return RoleKeys[(int)role] ?? "???";
        }

        public static RoleId GetLocalRoleId()
        {
            return _isInitialized ? localRoleId : RoleId.Debug;
        }

        /// <summary>
        ///     Initialize the network service with the specified role
        /// </summary>
        /// <param name="roleId">1=corals, 2=ghostNet, 3=feed</param>
        public static void InitializeWithPureData(RoleId roleId, PureDataConnector pureData)
        {
            if (_isInitialized) return;

            _pureData = pureData;
            Initialize(roleId);
        }

        /// <summary>
        ///     Initialize the network service with the specified role
        /// </summary>
        /// <param name="roleId">1=corals, 2=ghostNet, 3=feed</param>
        public static void Initialize(RoleId roleId)
        {
            if (_isInitialized) return;

            localRoleId = roleId;
            _isInitialized = true;

            NetworkManager.Instance.ListenNetwork<FloatData>(DataTag.CurrentMaelstromValue,
                HandleMaelstromDataReceived);

            AppLogger.Log($"Network service initialized for role: {roleId}");
        }

        /// <summary>
        ///     Cleanup network service resources
        /// </summary>
        public static void Cleanup()
        {
            if (_isInitialized)
            {
                try
                {
                    NetworkManager.Instance?.UnListenNetwork<FloatData>(DataTag.CurrentMaelstromValue,
                        HandleMaelstromDataReceived);
                }
                catch
                {
                }

                try
                {
                    NetworkManager.Instance?.Dispose();
                }
                catch
                {
                }

                _isInitialized = false;
                externalMaelstrom.Clear();
            }
        }

        /// <summary>
        ///     Returns external maelstrom values as array
        /// </summary>
        public static float[] GetExternalMaelstroms()
        {
            if (!_isInitialized) return new float[] { };

            var values = externalMaelstrom.Values;
            var result = new float[values.Count];
            var i = 0;
            foreach (var v in values) result[i++] = Clamp01(v);
            return result;
        }

        /// <summary>
        ///     Returns all current maelstrom keys and their values (including local)
        /// </summary>
        public static IReadOnlyDictionary<RoleId, float> GetAllMaelstroms()
        {
            var allMaelstroms = new Dictionary<RoleId, float>(externalMaelstrom);

            allMaelstroms[localRoleId] = localMaelstrom;

            return allMaelstroms;
        }

        private static void HandleMaelstromDataReceived(FloatData data)
        {
            if (data == null) return;

            try
            {
                if (data.RoleId == localRoleId) return;

                var extVal = Clamp01(data.Value);
                externalMaelstrom[data.RoleId] = extVal;
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"Error handling maelstrom data: {ex.Message}");
            }
        }

        private static void PublishCurrentMaelstrom(float maelstrom)
        {
            if (!_isInitialized || localRoleId == RoleId.Debug) return;

            localMaelstrom = Clamp01(maelstrom);

            try
            {
                NetworkManager.Instance.SendNetwork(DataTag.TargetMaelstromValue,
                    new FloatData(localRoleId, targetMaelstrom));
                NetworkManager.Instance.SendNetwork(DataTag.CurrentMaelstromValue,
                    new FloatData(localRoleId, localMaelstrom));
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error publishing maelstrom: {ex.Message}");
            }
        }

        public static float UpdateMaelstrom(float currentRatio, float speedModifier = 1.0f, bool isCoral = false)
        {
            var rnd = new Random();
            var externalMaelstroms = GetExternalMaelstroms();
            var externalMaelstrom = externalMaelstroms.Length > 0
                ? externalMaelstroms.Sum() / externalMaelstroms.Length
                : 0f;

            // Check if any previous maelstrom values were above 0.7
            var hasHighPreviousValues = maelstromHistory.Any(value => value >= 0.6f);
            var closeToTarget = Math.Abs(targetMaelstrom - currentMaelstrom) < 0.002f;

            if (closeToTarget)
            {
                netRnd = rnd.NextDouble();
                if (currentRatio > 0.3 && externalMaelstrom > 0.5 && !hasHighPreviousValues)
                    targetMaelstrom = 1;
                // AppLogger.Log($"BIG Mal({netRnd}) : {targetMaelstrom}/{currentMaelstrom}");
                else if (currentRatio > 0.3 && netRnd >= MEDIUM_MAELSTROM_THRESHOLD)
                    targetMaelstrom = 0.7f;
                // AppLogger.Log($"MID Mal({netRnd}) : {targetMaelstrom}/{currentMaelstrom}");
                else
                    targetMaelstrom = Mathf.Lerp(currentMaelstrom, currentRatio, 0.1f);
                //  AppLogger.Log($"Maelstrom Tgt/Crt : {currentRatio}, {targetMaelstrom}/{currentMaelstrom}, extValue : ({externalMaelstrom})");
            }

            // Use inertia only if previous values were above 0.7
            var lerpSpeed = (hasHighPreviousValues ? 0.001f : 0.01f) * speedModifier;
            currentMaelstrom = Mathf.Lerp(currentMaelstrom, targetMaelstrom, lerpSpeed);

            // Store current maelstrom in history (keep max 100 values)
            maelstromHistory.Enqueue(targetMaelstrom);
            if (maelstromHistory.Count > 100) maelstromHistory.Dequeue();

            if (updateCount++ > 60) updateCount = 0;

            PublishCurrentMaelstrom(Clamp01(currentMaelstrom));

            if (updateCount == 0)
            {
                var allMaelstroms = GetAllMaelstroms();
                if (_pureData)
                    foreach (var kvp in allMaelstroms)
                        _pureData.SendOscMessage(RoleToKey(kvp.Key), kvp.Value);
            }

            try
            {
                NetworkManager.Instance?.ProcessCallbacks();
            }
            catch
            {
            }


            return currentMaelstrom;
        }

        /// <summary>
        ///     Process network callbacks on the main thread. Call this from Update if not using UpdateMaelstrom.
        /// </summary>
        public static void ProcessNetworkCallbacks()
        {
            if (_isInitialized)
                try
                {
                    NetworkManager.Instance?.ProcessCallbacks();
                }
                catch
                {
                }
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}