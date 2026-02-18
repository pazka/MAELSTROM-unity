using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maelstrom.Unity
{
    public static class Config
    {
        private static Dictionary<string, JToken> _data;
        private static Dictionary<(string, Type), object> _typedCache;
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            _loaded = true;
            _typedCache = new Dictionary<(string, Type), object>();
            _data = new Dictionary<string, JToken>();

            var path = Path.Combine(Application.streamingAssetsPath, "config.json");

            if (!File.Exists(path))
            {
                AppLogger.LogWarning($"config.json not found at {path}");
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var obj = JObject.Parse(json);

                foreach (var property in obj.Properties())
                {
                    _data[property.Name] = property.Value;
                }

                AppLogger.Log($"Config loaded at {path}");
            }
            catch (Exception e)
            {
                AppLogger.LogError($"Failed to load config: {e.Message}");
            }
        }

        public static T Get<T>(string key, T defaultValue = default)
        {
            EnsureLoaded();

            var cacheKey = (key, typeof(T));
            if (_typedCache.TryGetValue(cacheKey, out var cached))
                return (T)cached;

            if (!_data.TryGetValue(key, out var token))
                return defaultValue;

            try
            {
                var result = token.ToObject<T>();
                _typedCache[cacheKey] = result;
                return result;
            }
            catch (Exception e)
            {
                AppLogger.LogWarning(
                    $"Config key '{key}' could not be converted to {typeof(T).Name}, using default. {e.Message}");
                return defaultValue;
            }
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

        public static void Reload()
        {
            _loaded = false;
            _typedCache?.Clear();
            EnsureLoaded();
        }
    }
}
