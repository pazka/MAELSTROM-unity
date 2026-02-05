using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        private static readonly float HIGH_MAELSTROM_THRESHOLD = 0.99f;
        private static readonly float MEDIUM_MAELSTROM_THRESHOLD = 0.94f;


        private static float currentMaelstrom;
        private static float targetMaelstrom;

        private static readonly Queue<float> targetMaelstromHistory = new();

        // Network Integration
        private static bool _isInitialized;
        private static PureDataConnector _pureData;

        // Maelstrom-specific network state
        private static readonly ConcurrentDictionary<RoleId, float> externalMaelstroms =
            new();

        private static RoleId localRoleId; // 1=corals,2=ghostNet,3=feed

        private static int updateCount;
        public static string[] RoleKeys = { "debug", "deadComunities", "ghostNet", "feed" };
        public static RoleId[] RoleIds = { RoleId.Debug, RoleId.DeadComunities, RoleId.Feed, RoleId.GhostNet };

        private static readonly Dictionary<RoleId, bool> overrides = new()
        {
            { RoleId.Debug, false },
            { RoleId.DeadComunities, false },
            { RoleId.GhostNet, false },
            { RoleId.Feed, false }
        };

        private static void setTarget(float val)
        {
            if (!overrides[localRoleId]) targetMaelstrom = val;
        }


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
                HandleExternalMaelstromDataReceived);
            NetworkManager.Instance.ListenNetwork<FloatData>(DataTag.OverrideTargetMaelstrom,
                HandleOverrideMaelstromReceived);

            AppLogger.Log($"Network service initialized for role: {roleId}");
        }


        /// <summary>
        ///     Returns external maelstrom values as array
        /// </summary>
        public static float[] GetExternalMaelstroms()
        {
            if (!_isInitialized) return new float[] { };

            var values = externalMaelstroms.Values;
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
            var allMaelstroms = new Dictionary<RoleId, float>(externalMaelstroms);

            allMaelstroms[localRoleId] = currentMaelstrom;

            return allMaelstroms;
        }

        private static void HandleOverrideMaelstromReceived(FloatData data)
        {
            if (data.Value > 0.01)
                overrides[data.RoleId] = true;
            else
                overrides[data.RoleId] = false;

            if (data.RoleId == localRoleId) targetMaelstrom = data.Value;
            else externalMaelstroms[data.RoleId] = data.Value;
        }

        private static void HandleExternalMaelstromDataReceived(FloatData data)
        {
            if (data == null) return;

            try
            {
                if (data.RoleId == localRoleId) return;

                externalMaelstroms[data.RoleId] = data.Value;
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"Error handling maelstrom data: {ex.Message}");
            }
        }

        private static void BroadcastCurrentMaelstrom()
        {
            if (!_isInitialized || localRoleId == RoleId.Debug) return;

            try
            {
                NetworkManager.Instance.SendNetwork(DataTag.TargetMaelstromValue,
                    new FloatData(localRoleId, targetMaelstrom));
                NetworkManager.Instance.SendNetwork(DataTag.CurrentMaelstromValue,
                    new FloatData(localRoleId, currentMaelstrom));
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error publishing maelstrom: {ex.Message}");
            }

            if (updateCount++ > 60) updateCount = 0;

            try
            {
                if (updateCount == 0)
                {
                    var allMaelstroms = GetAllMaelstroms();
                    if (_pureData)
                        foreach (var kvp in allMaelstroms)
                            _pureData.SendOscMessage(RoleToKey(kvp.Key), kvp.Value);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error send OSC DATA: {ex.Message}");
            }
        }


        private static void BroadcastCurrentValues(float currentRatio)
        {
            if (!_isInitialized || localRoleId == RoleId.Debug) return;

            try
            {
                NetworkManager.Instance.SendNetwork(DataTag.CurrentRatio,
                    new FloatData(localRoleId, currentRatio));
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error send Network ratio: {ex.Message}");
            }
        }

        public static float UpdateMaelstrom(float currentRatio, double netRnd = 0f)
        {
            var externalMaelstromsValues = GetExternalMaelstroms();
            var externalMaestrom = externalMaelstromsValues.Length > 0
                ? externalMaelstromsValues.Sum() / externalMaelstromsValues.Length
                : 0f;
            var influencedRnd = netRnd + externalMaestrom * 0.3;

            var closeToTarget = Math.Abs(targetMaelstrom - currentMaelstrom) < 0.002f;
            if (closeToTarget)
            {
                if (influencedRnd >= HIGH_MAELSTROM_THRESHOLD)
                {
                    setTarget(1f);
                    AppLogger.Log(
                        $"BIG Rnd(${netRnd:F3}/Ext(${externalMaestrom:F2})/ => Inf(${influencedRnd:F2}) > ${HIGH_MAELSTROM_THRESHOLD}) ");
                }
                else if (influencedRnd >= MEDIUM_MAELSTROM_THRESHOLD)
                {
                    setTarget(0.7f);
                    AppLogger.Log(
                        $"BIG Rnd(${netRnd:F3}/Ext(${externalMaestrom:F2})/ => Inf(${influencedRnd:F2}) > ${MEDIUM_MAELSTROM_THRESHOLD}) ");
                }
                else
                {
                    setTarget(currentRatio + externalMaestrom * 0.3f);
                    AppLogger.Log(
                        $"Maelstrom crt{currentRatio:F2}, tgt{targetMaelstrom:F2}, ext({externalMaestrom:F2}) rdn:{netRnd:F2}");
                }
            }

            BroadcastCurrentValues(currentRatio);
            return currentMaelstrom;
        }


        public static float ProgressMaelstrom(float speedModifier = 1.0f)
        {
            // Check if any previous maelstrom values were above 0.6
            var hasHighPreviousValues = targetMaelstromHistory.Any(value => value >= 0.6f);

            // Use inertia only if previous values were above 0.6
            var lerpSpeed = (hasHighPreviousValues ? 0.001f : 0.01f) * speedModifier;
            currentMaelstrom = Mathf.Lerp(currentMaelstrom, targetMaelstrom, lerpSpeed);

            // Store current maelstrom in history (keep max 100 values)
            targetMaelstromHistory.Enqueue(targetMaelstrom);
            if (targetMaelstromHistory.Count > 100) targetMaelstromHistory.Dequeue();

            BroadcastCurrentMaelstrom();

            return currentMaelstrom;
        }


        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}