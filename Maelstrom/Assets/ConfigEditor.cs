using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Maelstrom.Unity
{
    [CustomEditor(typeof(Config))]
    public class ConfigEditor : Editor
    {
        private Config config;
        private Vector2 scrollPosition;

        private void OnEnable()
        {
            config = (Config)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration Management", EditorStyles.boldLabel);

            // Configuration buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Config"))
            {
                config.ReloadConfig();
            }
            if (GUILayout.Button("Save Config"))
            {
                config.SaveConfig();
            }
            if (GUILayout.Button("Refresh Display"))
            {
                config.RefreshDisplay();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Display configuration entries
            if (config.Count > 0)
            {
                EditorGUILayout.LabelField($"Configuration Entries ({config.Count}):", EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

                var configEntries = GetConfigEntries();
                foreach (var entry in configEntries)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.key, GUILayout.Width(150));
                    EditorGUILayout.LabelField(entry.value, GUILayout.Width(200));
                    EditorGUILayout.LabelField(entry.type, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No configuration entries loaded. The config.json file is empty or doesn't exist.", MessageType.Info);
            }

            // Add new configuration entry
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add New Entry:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Test Entry"))
            {
                config.Set("test.key", "test value");
                config.Set("test.number", 42);
                config.Set("test.boolean", true);
            }
            if (GUILayout.Button("Clear All"))
            {
                config.Clear();
                config.RefreshDisplay();
            }
            EditorGUILayout.EndHorizontal();
        }

        private List<ConfigDisplayEntry> GetConfigEntries()
        {
            // Use reflection to access the private configEntries field
            var field = typeof(Config).GetField("configEntries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                return (List<ConfigDisplayEntry>)field.GetValue(config);
            }

            return new List<ConfigDisplayEntry>();
        }
    }
}
