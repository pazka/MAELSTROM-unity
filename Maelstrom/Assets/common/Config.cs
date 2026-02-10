using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Static configuration loaded once from StreamingAssets/config.json.
    ///     Editable before/during build; used at runtime on Windows.
    /// </summary>
    public static class Config
    {
        private static Dictionary<string, object> _data;
        private static Dictionary<(string, Type), object> _typedCache;
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            _data = new Dictionary<string, object>();
            _typedCache = new Dictionary<(string, Type), object>();
            _loaded = true;

            var path = Path.Combine(Application.streamingAssetsPath, "config.json");
            if (!File.Exists(path))
            {
                AppLogger.LogWarning($"config.json not found at {path}, using empty config.");
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;
                _data = ParseFlatJson(json);
                AppLogger.Log($"Config loaded at {path}");
                AppLogger.Log($"Config keys: {string.Join(", ", _data.Keys)}");
            }
            catch (Exception e)
            {
                AppLogger.LogError($"Failed to load config: {e.Message}");
            }
        }

        /// <summary>
        ///     Parse flat JSON { "key": value, ... }.
        /// </summary>
        private static Dictionary<string, object> ParseFlatJson(string json)
        {
            var result = new Dictionary<string, object>();
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) return result;

            var i = 1;
            while (i < json.Length)
            {
                i = SkipWhitespace(json, i);
                if (i >= json.Length || json[i] == '}') break;
                if (json[i] != '"')
                {
                    i++;
                    continue;
                }

                var keyStart = i + 1;
                var keyEnd = json.IndexOf('"', keyStart);
                if (keyEnd < 0) break;
                var key = json.Substring(keyStart, keyEnd - keyStart);

                i = SkipWhitespace(json, keyEnd + 1);
                if (i >= json.Length || json[i] != ':')
                {
                    i++;
                    continue;
                }

                i = SkipWhitespace(json, i + 1);
                if (i >= json.Length) break;

                object value;
                var next = ParseValue(json, i, out value);
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
            if (i >= s.Length) return i;

            if (s[i] == '"')
            {
                var start = i + 1;
                var end = start;
                while (end < s.Length)
                {
                    if (s[end] == '\\')
                    {
                        end += 2;
                        continue;
                    }
                    if (s[end] == '"') break;
                    end++;
                }

                value = s.Substring(start, end - start);
                return end + 1;
            }

            if (s[i] == '[')
            {
                var list = new List<object>();
                i = SkipWhitespace(s, i + 1);
                while (i < s.Length && s[i] != ']')
                {
                    object element;
                    i = ParseValue(s, i, out element);
                    if (element != null) list.Add(element);
                    i = SkipWhitespace(s, i);
                    if (i < s.Length && s[i] == ',') i++;
                    i = SkipWhitespace(s, i);
                }

                value = list;
                return i < s.Length ? i + 1 : i;
            }

            var j = i;
            while (j < s.Length && s[j] != ',' && s[j] != '}' && s[j] != ']' && !char.IsWhiteSpace(s[j])) j++;
            var raw = s.Substring(i, j - i).Trim();

            if (raw == "true") { value = true; return j; }
            if (raw == "false") { value = false; return j; }
            if (raw == "null") return j;
            if (int.TryParse(raw, out var vi)) { value = vi; return j; }
            if (float.TryParse(raw, out var vf)) { value = vf; return j; }

            value = raw;
            return j;
        }

        /// <summary>
        ///     Get config value by key, with optional default.
        /// </summary>
        public static T Get<T>(string key, T defaultValue = default)
        {
            EnsureLoaded();

            var cacheKey = (key, typeof(T));
            if (_typedCache.TryGetValue(cacheKey, out var cached))
                return (T)cached;

            if (!_data.TryGetValue(key, out var v) || v == null)
                return defaultValue;

            try
            {
                T result;

                if (v is T t)
                {
                    result = t;
                }
                else if (typeof(T).IsArray && v is List<object> list)
                {
                    var elementType = typeof(T).GetElementType();
                    var array = Array.CreateInstance(elementType, list.Count);
                    for (var i = 0; i < list.Count; i++)
                    {
                        array.SetValue(
                            list[i].GetType() == elementType
                                ? list[i]
                                : Convert.ChangeType(list[i], elementType),
                            i);
                    }
                    result = (T)(object)array;
                }
                else
                {
                    result = (T)Convert.ChangeType(v, typeof(T));
                }

                _typedCache[cacheKey] = result;
                return result;
            }
            catch
            {
                AppLogger.LogWarning($"Config key '{key}' could not be converted to {typeof(T).Name}, using default.");
                return defaultValue;
            }
        }

        /// <summary>
        ///     Get config array by key, converting each element to type T.
        /// </summary>
        public static T[] GetArray<T>(string key, T[] defaultValue = null)
        {
            EnsureLoaded();

            var cacheKey = (key, typeof(T[]));
            if (_typedCache.TryGetValue(cacheKey, out var cached))
                return (T[])cached;

            if (!_data.TryGetValue(key, out var v) || v is not List<object> list)
                return defaultValue ?? Array.Empty<T>();

            var result = new T[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                try
                {
                    result[i] = list[i] is T t
                        ? t
                        : (T)Convert.ChangeType(list[i], typeof(T));
                }
                catch
                {
                    AppLogger.LogWarning($"Config array '{key}' element {i} could not be converted to {typeof(T).Name}.");
                }
            }

            _typedCache[cacheKey] = result;
            return result;
        }

        public static bool HasKey(string key)
        {
            EnsureLoaded();
            return _data.ContainsKey(key);
        }

        public static string[] GetAllKeys()
        {
            EnsureLoaded();
            var keys = new string[_data.Count];
            _data.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>
        ///     Reload config from StreamingAssets/config.json.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _typedCache?.Clear();
            EnsureLoaded();
        }
    }
}
