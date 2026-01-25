using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Static configuration loaded once from StreamingAssets/config.json.
    /// Editable before/during build; used at runtime on Windows.
    /// </summary>
    public static class Config
    {
        private static Dictionary<string, object> _data;
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            _data = new Dictionary<string, object>();
            _loaded = true;

            string path = Path.Combine(Application.streamingAssetsPath, "config.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"config.json not found at {path}, using empty config.");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;
                _data = ParseFlatJson(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load config: {e.Message}");
            }
        }

        /// <summary>
        /// Parse flat JSON { "key": value, ... }. JsonUtility does not support Dictionary, so we use a minimal parser.
        /// </summary>
        private static Dictionary<string, object> ParseFlatJson(string json)
        {
            var result = new Dictionary<string, object>();
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) return result;

            int i = 1;
            while (i < json.Length)
            {
                i = SkipWhitespace(json, i);
                if (i >= json.Length || json[i] == '}') break;
                if (json[i] != '"') { i++; continue; }

                int keyStart = i + 1;
                int keyEnd = json.IndexOf('"', keyStart);
                if (keyEnd < 0) break;
                string key = json.Substring(keyStart, keyEnd - keyStart);

                i = SkipWhitespace(json, keyEnd + 1);
                if (i >= json.Length || json[i] != ':') { i++; continue; }
                i = SkipWhitespace(json, i + 1);
                if (i >= json.Length) break;

                object value;
                int next = ParseValue(json, i, out value);
                if (next > i) result[key] = value;
                i = next;
                i = SkipWhitespace(json, i);
                if (i < json.Length && json[i] == ',') i++;
            }
            return result;
        }

        private static int SkipWhitespace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        private static int ParseValue(string s, int i, out object value)
        {
            value = null;
            if (s[i] == '"')
            {
                int start = i + 1;
                int end = start;
                while (end < s.Length)
                {
                    if (s[end] == '\\') { end += 2; continue; }
                    if (s[end] == '"') break;
                    end++;
                }
                value = s.Substring(start, end - start);
                return end + 1;
            }
            int j = i;
            while (j < s.Length && s[j] != ',' && s[j] != '}' && !char.IsWhiteSpace(s[j])) j++;
            string raw = s.Substring(i, j - i).Trim();
            if (raw == "true") { value = true; return j; }
            if (raw == "false") { value = false; return j; }
            if (raw == "null") return j;
            if (int.TryParse(raw, out int vi)) { value = vi; return j; }
            if (float.TryParse(raw, out float vf)) { value = vf; return j; }
            value = raw;
            return j;
        }

        /// <summary>
        /// Get config value by key, with optional default.
        /// </summary>
        public static T Get<T>(string key, T defaultValue = default)
        {
            EnsureLoaded();
            if (!_data.TryGetValue(key, out object v)) return defaultValue;
            if (v == null) return defaultValue;
            if (v is T t) return t;
            try
            {
                return (T)Convert.ChangeType(v, typeof(T));
            }
            catch
            {
                Debug.LogWarning($"Config key '{key}' could not be converted to {typeof(T).Name}, using default.");
                return defaultValue;
            }
        }

        /// <summary>
        /// True if the key exists in config.
        /// </summary>
        public static bool HasKey(string key)
        {
            EnsureLoaded();
            return _data.ContainsKey(key);
        }

        /// <summary>
        /// All config keys.
        /// </summary>
        public static string[] GetAllKeys()
        {
            EnsureLoaded();
            var keys = new string[_data.Count];
            _data.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>
        /// Reload config from StreamingAssets/config.json.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            EnsureLoaded();
        }
    }
}
