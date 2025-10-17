using UnityEngine;
using System.Collections.Generic;

namespace Maelstrom.Unity
{
    [ExecuteInEditMode]
    public class GlobalShaderProp : MonoBehaviour
    {
        [System.Serializable]
        public class GlobalShaderProperty
        {
            public string propertyName;
            public PropertyType type;
            public float floatValue;
            public Vector2 vector2Value;
            public Vector3 vector3Value;
            public Vector4 vector4Value;
            public Color colorValue;
        }

        public enum PropertyType
        {
            Float,
            Vector2,
            Vector3,
            Vector4,
            Color
        }

        [Header("Global Properties")]
        [SerializeField] private GlobalShaderProperty[] globalProperties = new GlobalShaderProperty[0];

        [Header("Target Settings")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool applyInEditMode = true;
        [SerializeField] private bool applyToAllMaterials = true;
        [SerializeField] private Material[] specificMaterials = new Material[0];

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyGlobalProperties();
            }
        }

        private void OnValidate()
        {
            if (applyInEditMode && !Application.isPlaying)
            {
                ApplyGlobalProperties();
            }
        }

        /// <summary>
        /// Apply all global properties to materials in the project
        /// </summary>
        public void ApplyGlobalProperties()
        {
            if (applyToAllMaterials)
            {
                ApplyToAllMaterialsInProject();
            }
            else
            {
                ApplyToSpecificMaterials();
            }
        }

        /// <summary>
        /// Apply global properties to all materials found in the project
        /// </summary>
        private void ApplyToAllMaterialsInProject()
        {
            Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();

            foreach (Material material in allMaterials)
            {
                if (material == null) continue;
                ApplyPropertiesToMaterial(material);
            }

            // Also apply to materials on GameObjects in the scene
            Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.InstanceID);
            foreach (Renderer renderer in allRenderers)
            {
                if (renderer == null) continue;

                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    if (material == null) continue;
                    ApplyPropertiesToMaterial(material);
                }
            }
        }

        /// <summary>
        /// Apply global properties to specific materials only
        /// </summary>
        private void ApplyToSpecificMaterials()
        {
            foreach (Material material in specificMaterials)
            {
                if (material == null) continue;
                ApplyPropertiesToMaterial(material);
            }
        }

        /// <summary>
        /// Apply all global properties to a specific material
        /// </summary>
        private void ApplyPropertiesToMaterial(Material material)
        {
            foreach (GlobalShaderProperty prop in globalProperties)
            {
                if (string.IsNullOrEmpty(prop.propertyName))
                    continue;

                try
                {
                    switch (prop.type)
                    {
                        case PropertyType.Float:
                            material.SetFloat(prop.propertyName, prop.floatValue);
                            break;
                        case PropertyType.Vector2:
                            material.SetVector(prop.propertyName, prop.vector2Value);
                            break;
                        case PropertyType.Vector3:
                            material.SetVector(prop.propertyName, prop.vector3Value);
                            break;
                        case PropertyType.Vector4:
                            material.SetVector(prop.propertyName, prop.vector4Value);
                            break;
                        case PropertyType.Color:
                            material.SetColor(prop.propertyName, prop.colorValue);
                            break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to set global shader property '{prop.propertyName}' on material '{material.name}': {e.Message}");
                }
            }
        }

        /// <summary>
        /// Add a new global shader property at runtime
        /// </summary>
        public void AddGlobalProperty(string propertyName, PropertyType type, object value)
        {
            GlobalShaderProperty newProp = new GlobalShaderProperty
            {
                propertyName = propertyName,
                type = type
            };

            switch (type)
            {
                case PropertyType.Float:
                    newProp.floatValue = (float)value;
                    break;
                case PropertyType.Vector2:
                    newProp.vector2Value = (Vector2)value;
                    break;
                case PropertyType.Vector3:
                    newProp.vector3Value = (Vector3)value;
                    break;
                case PropertyType.Vector4:
                    newProp.vector4Value = (Vector4)value;
                    break;
                case PropertyType.Color:
                    newProp.colorValue = (Color)value;
                    break;
            }

            System.Array.Resize(ref globalProperties, globalProperties.Length + 1);
            globalProperties[globalProperties.Length - 1] = newProp;
        }

        /// <summary>
        /// Set a global shader property value at runtime
        /// </summary>
        public void SetGlobalProperty(string propertyName, float value)
        {
            SetPropertyValue(propertyName, PropertyType.Float, value);
        }

        public void SetGlobalProperty(string propertyName, Vector2 value)
        {
            SetPropertyValue(propertyName, PropertyType.Vector2, value);
        }

        public void SetGlobalProperty(string propertyName, Vector3 value)
        {
            SetPropertyValue(propertyName, PropertyType.Vector3, value);
        }

        public void SetGlobalProperty(string propertyName, Vector4 value)
        {
            SetPropertyValue(propertyName, PropertyType.Vector4, value);
        }

        public void SetGlobalProperty(string propertyName, Color value)
        {
            SetPropertyValue(propertyName, PropertyType.Color, value);
        }

        private void SetPropertyValue(string propertyName, PropertyType type, object value)
        {
            for (int i = 0; i < globalProperties.Length; i++)
            {
                if (globalProperties[i].propertyName == propertyName)
                {
                    globalProperties[i].type = type;
                    switch (type)
                    {
                        case PropertyType.Float:
                            globalProperties[i].floatValue = (float)value;
                            break;
                        case PropertyType.Vector2:
                            globalProperties[i].vector2Value = (Vector2)value;
                            break;
                        case PropertyType.Vector3:
                            globalProperties[i].vector3Value = (Vector3)value;
                            break;
                        case PropertyType.Vector4:
                            globalProperties[i].vector4Value = (Vector4)value;
                            break;
                        case PropertyType.Color:
                            globalProperties[i].colorValue = (Color)value;
                            break;
                    }
                    return;
                }
            }

            AddGlobalProperty(propertyName, type, value);
        }

        /// <summary>
        /// Get all global properties as a dictionary for external access
        /// </summary>
        public Dictionary<string, object> GetGlobalProperties()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            foreach (GlobalShaderProperty prop in globalProperties)
            {
                if (string.IsNullOrEmpty(prop.propertyName))
                    continue;

                switch (prop.type)
                {
                    case PropertyType.Float:
                        properties[prop.propertyName] = prop.floatValue;
                        break;
                    case PropertyType.Vector2:
                        properties[prop.propertyName] = prop.vector2Value;
                        break;
                    case PropertyType.Vector3:
                        properties[prop.propertyName] = prop.vector3Value;
                        break;
                    case PropertyType.Vector4:
                        properties[prop.propertyName] = prop.vector4Value;
                        break;
                    case PropertyType.Color:
                        properties[prop.propertyName] = prop.colorValue;
                        break;
                }
            }

            return properties;
        }

        /// <summary>
        /// Clear all global properties
        /// </summary>
        public void ClearGlobalProperties()
        {
            globalProperties = new GlobalShaderProperty[0];
        }
    }
}
