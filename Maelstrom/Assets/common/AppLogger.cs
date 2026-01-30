using System;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Logs to Console, Unity Debug, and sends over the network (DataTag.Logs) for central display.
    /// </summary>
    public static class AppLogger
    {
        private const int MaxNetworkMessageLength = 200;
        private static bool _sendingLog;

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var line = $"[Log] {message}";
            Console.WriteLine(line);
#if UNITY_EDITOR || UNITY_STANDALONE
            Debug.Log(message);
#endif
            SendOverNetwork(line);
        }

        public static void LogWarning(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var line = $"[Warn] {message}";
            Console.WriteLine(line);
#if UNITY_EDITOR || UNITY_STANDALONE
            Debug.LogWarning(message);
#endif
            SendOverNetwork(line);
        }

        public static void LogError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var line = $"[Err] {message}";
            Console.WriteLine(line);
#if UNITY_EDITOR || UNITY_STANDALONE
            Debug.LogError(message);
#endif
            SendOverNetwork(line);
        }

        private static void SendOverNetwork(string line)
        {
            if (_sendingLog) return;
            try
            {
                var nm = NetworkManager.Instance;
                if (nm == null) return;

                _sendingLog = true;
                var truncated = line.Length <= MaxNetworkMessageLength
                    ? line
                    : line.Substring(0, MaxNetworkMessageLength - 3) + "...";
                var roleId = CommonMaelstrom.GetLocalRoleId();
                nm.SendNetwork(DataTag.Logs, new TextData(roleId, truncated));
            }
            catch (Exception)
            {
                _sendingLog = false;
                Console.WriteLine("[AppLogger] Failed to send log over network");
                return;
            }

            _sendingLog = false;
        }
    }
}
