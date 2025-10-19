using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Configuration display entry for Unity inspector
    /// </summary>
    [System.Serializable]
    public class ConfigDisplayEntry
    {
        public string key;
        public string value;
        public string type;

        public ConfigDisplayEntry(string key, object value)
        {
            this.key = key;
            this.value = value?.ToString() ?? "null";
            this.type = value?.GetType().Name ?? "null";
        }
    }

    /// <summary>
    /// Generic configuration system with key-value access
    /// </summary>
    public class Config : MonoBehaviour
    {
        [Header("Configuration Display")]
        [SerializeField] private bool showConfigInInspector = true;
        [SerializeField] private bool autoRefresh = true;

        [Header("Loaded Configuration")]
        [SerializeField] private List<ConfigDisplayEntry> configEntries = new List<ConfigDisplayEntry>();

        private Dictionary<string, object> _config = new Dictionary<string, object>();
        private bool _isLoaded = false;
        private string _configPath;

        private void OnValidate()
        {
            Initialize();
        }
        /// <summary>
        /// Initialize the configuration system
        /// </summary>
        public void Awake()
        {
            Initialize();
        }
        /// <summary>
        /// Initialize the configuration system
        /// </summary>
        public void Initialize()
        {
            _configPath = Path.Combine(Application.dataPath, "config.json");
            Debug.Log($"Config path: {_configPath}");
            LoadConfig();
        }

        /// <summary>
        /// Parse JSON string to Dictionary<string, object>
        /// </summary>
        private Dictionary<string, object> ParseJsonToDictionary(string jsonContent)
        {
            var result = new Dictionary<string, object>();

            try
            {
                // Simple JSON parser for basic key-value pairs
                jsonContent = jsonContent.Trim();
                if (jsonContent.StartsWith("{") && jsonContent.EndsWith("}"))
                {
                    jsonContent = jsonContent.Substring(1, jsonContent.Length - 2); // Remove { and }

                    var pairs = jsonContent.Split(',');
                    foreach (var pair in pairs)
                    {
                        var trimmedPair = pair.Trim();
                        if (string.IsNullOrEmpty(trimmedPair)) continue;

                        var colonIndex = trimmedPair.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            var key = trimmedPair.Substring(0, colonIndex).Trim().Trim('"');
                            var valueStr = trimmedPair.Substring(colonIndex + 1).Trim();

                            // Parse value based on its format
                            object value = ParseJsonValue(valueStr);
                            result[key] = value;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing JSON: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Parse a JSON value string to appropriate object type
        /// </summary>
        private object ParseJsonValue(string valueStr)
        {
            valueStr = valueStr.Trim();

            // Remove quotes for strings
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
            {
                return valueStr.Substring(1, valueStr.Length - 2);
            }

            // Parse numbers
            if (int.TryParse(valueStr, out int intValue))
            {
                return intValue;
            }

            if (float.TryParse(valueStr, out float floatValue))
            {
                return floatValue;
            }

            // Parse booleans
            if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Parse null
            if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Default to string if nothing else matches
            return valueStr;
        }

        /// <summary>
        /// Load configuration from config.json file
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string jsonContent = File.ReadAllText(_configPath);

                    if (!string.IsNullOrEmpty(jsonContent.Trim()))
                    {
                        // Parse JSON manually since Unity's JsonUtility doesn't support Dictionary<string, object>
                        _config = ParseJsonToDictionary(jsonContent);
                    }
                }
                else
                {
                    Debug.Log("config.json not found, using empty configuration");
                    //create the file 
                    File.WriteAllText(_configPath, "{}");
                    _config = new Dictionary<string, object>();
                    _isLoaded = true;
                    Debug.Log("Configuration file created with empty configuration");
                    return;
                }

                _isLoaded = true;
                UpdateInspectorDisplay();
                Debug.Log($"Configuration loaded with {_config.Count} entries");
                Debug.Log($"Configuration: {JsonUtility.ToJson(_config, true)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading configuration: {e.Message}");
                _config = new Dictionary<string, object>();
                _isLoaded = true;
            }
        }

        /// <summary>
        /// Get a configuration value by key
        /// </summary>
        public T Get<T>(string key, T defaultValue = default(T))
        {
            if (!_isLoaded)
            {
                Initialize();
            }

            if (_config.TryGetValue(key, out object value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    Debug.LogWarning($"Failed to convert config value for key '{key}' to type {typeof(T).Name}, using default value");
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Set a configuration value by key
        /// </summary>
        public void Set<T>(string key, T value)
        {
            if (!_isLoaded)
            {
                Initialize();
            }

            _config[key] = value;

            if (autoRefresh)
            {
                UpdateInspectorDisplay();
            }
        }

        /// <summary>
        /// Check if a key exists in the configuration
        /// </summary>
        public bool HasKey(string key)
        {
            if (!_isLoaded)
            {
                Initialize();
            }

            return _config.ContainsKey(key);
        }

        /// <summary>
        /// Get all configuration keys
        /// </summary>
        public string[] GetAllKeys()
        {
            if (!_isLoaded)
            {
                Initialize();
            }

            var keys = new string[_config.Count];
            _config.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>
        /// Remove a configuration key
        /// </summary>
        public bool RemoveKey(string key)
        {
            if (!_isLoaded)
            {
                Initialize();
            }

            return _config.Remove(key);
        }

        /// <summary>
        /// Clear all configuration
        /// </summary>
        public void Clear()
        {
            if (!_isLoaded)
            {
                Initialize();
            }

            _config.Clear();
        }

        /// <summary>
        /// Save current configuration to config.json
        /// </summary>
        public void Save()
        {
            try
            {
                if (_configPath == null)
                {
                    _configPath = Path.Combine(Application.dataPath, "config.json");
                }

                string jsonContent = JsonUtility.ToJson(new SerializableDictionary(_config), true);
                File.WriteAllText(_configPath, jsonContent);
                Debug.Log("Configuration saved successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving configuration: {e.Message}");
            }
        }

        /// <summary>
        /// Reload configuration from file
        /// </summary>
        public void Reload()
        {
            _isLoaded = false;
            LoadConfig();
        }

        /// <summary>
        /// Check if configuration is loaded
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// Get the number of configuration entries
        /// </summary>
        public int Count => _isLoaded ? _config.Count : 0;

        /// <summary>
        /// Update the inspector display with current configuration
        /// </summary>
        private void UpdateInspectorDisplay()
        {
            if (!showConfigInInspector) return;

            configEntries.Clear();
            foreach (var kvp in _config)
            {
                configEntries.Add(new ConfigDisplayEntry(kvp.Key, kvp.Value));
            }
        }

        /// <summary>
        /// Refresh the inspector display
        /// </summary>
        [ContextMenu("Refresh Display")]
        public void RefreshDisplay()
        {
            UpdateInspectorDisplay();
        }

        /// <summary>
        /// Reload configuration from file
        /// </summary>
        [ContextMenu("Reload Config")]
        public void ReloadConfig()
        {
            Reload();
        }

        /// <summary>
        /// Save configuration to file
        /// </summary>
        [ContextMenu("Save Config")]
        public void SaveConfig()
        {
            Save();
        }
    }

    /// <summary>
    /// Serializable dictionary for JSON serialization
    /// </summary>
    [Serializable]
    public class SerializableDictionary
    {
        [SerializeField] private List<ConfigEntry> entries = new List<ConfigEntry>();

        public SerializableDictionary(Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                entries.Add(new ConfigEntry { key = kvp.Key, value = kvp.Value });
            }
        }

        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            foreach (var entry in entries)
            {
                dict[entry.key] = entry.value;
            }
            return dict;
        }
    }

    /// <summary>
    /// Configuration entry for serialization
    /// </summary>
    [Serializable]
    public class ConfigEntry
    {
        public string key;
        public object value;
    }

}