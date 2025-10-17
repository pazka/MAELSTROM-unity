using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Scriptable component that passes shader properties to the material of the attached GameObject
    /// Works in both play mode and edit mode for live shader editing
    /// </summary>
    [ExecuteInEditMode]
    public class ShaderProp : MonoBehaviour
    {
        [System.Serializable]
        public class ShaderProperty
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

        [Header("Shader Properties")]
        [SerializeField] private ShaderProperty[] shaderProperties = new ShaderProperty[0];

        [Header("Target Settings")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool applyInEditMode = true;

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyShaderProperties();
            }
        }

        private void OnValidate()
        {
            if (applyInEditMode && !Application.isPlaying)
            {
                ApplyShaderProperties();
            }
        }

        private void Update()
        {
            // // Only apply in edit mode if enabled
            // if (applyInEditMode && !Application.isPlaying)
            // {
            //     ApplyShaderProperties();
            // }
        }

        /// <summary>
        /// Apply all configured shader properties to the material of this GameObject
        /// </summary>
        public void ApplyShaderProperties()
        {
            ApplyToGameObject(gameObject);
        }

        /// <summary>
        /// Apply shader properties to a specific GameObject
        /// </summary>
        private void ApplyToGameObject(GameObject target)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                if (Application.isPlaying)
                {
                    throw new System.Exception($"No Renderer component found on {target.name}");
                }
                else
                {
                    Debug.LogWarning($"No Renderer component found on {target.name}");
                    return;
                }
            }

            Material material;
            if (Application.isPlaying)
            {
                material = renderer.material;
            }
            else
            {
                material = renderer.sharedMaterial;
            }

            if (material == null)
            {
                if (Application.isPlaying)
                {
                    throw new System.Exception($"No Material found on {target.name}");
                }
                else
                {
                    Debug.LogWarning($"No Material found on {target.name}");
                    return;
                }
            }

            foreach (ShaderProperty prop in shaderProperties)
            {
                if (string.IsNullOrEmpty(prop.propertyName))
                    continue;

                try
                {
                    Debug.Log($"Applying property {prop.type} {prop.propertyName},  {prop.floatValue}");
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
                    Debug.LogError($"Failed to set shader property '{prop.propertyName}' on {target.name}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Add a new shader property at runtime
        /// </summary>
        public void AddShaderProperty(string propertyName, PropertyType type, object value)
        {
            ShaderProperty newProp = new ShaderProperty
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

            // Resize array and add new property
            System.Array.Resize(ref shaderProperties, shaderProperties.Length + 1);
            shaderProperties[shaderProperties.Length - 1] = newProp;
        }

        /// <summary>
        /// Set a shader property value at runtime
        /// </summary>
        public void SetShaderProperty(string propertyName, float value)
        {
            SetPropertyValue(propertyName, PropertyType.Float, value);
        }

        public void SetShaderProperty(string propertyName, Vector2 value)
        {
            SetPropertyValue(propertyName, PropertyType.Vector2, value);
        }

        public void SetShaderProperty(string propertyName, Vector3 value)
        {
            SetPropertyValue(propertyName, PropertyType.Vector3, value);
        }

        public void SetShaderProperty(string propertyName, Vector4 value)
        {
            SetPropertyValue(propertyName, PropertyType.Vector4, value);
        }

        public void SetShaderProperty(string propertyName, Color value)
        {
            SetPropertyValue(propertyName, PropertyType.Color, value);
        }

        private void SetPropertyValue(string propertyName, PropertyType type, object value)
        {
            // Find existing property
            for (int i = 0; i < shaderProperties.Length; i++)
            {
                if (shaderProperties[i].propertyName == propertyName)
                {
                    shaderProperties[i].type = type;
                    switch (type)
                    {
                        case PropertyType.Float:
                            shaderProperties[i].floatValue = (float)value;
                            break;
                        case PropertyType.Vector2:
                            shaderProperties[i].vector2Value = (Vector2)value;
                            break;
                        case PropertyType.Vector3:
                            shaderProperties[i].vector3Value = (Vector3)value;
                            break;
                        case PropertyType.Vector4:
                            shaderProperties[i].vector4Value = (Vector4)value;
                            break;
                        case PropertyType.Color:
                            shaderProperties[i].colorValue = (Color)value;
                            break;
                    }
                    return;
                }
            }

            // Property not found, add it
            AddShaderProperty(propertyName, type, value);
        }

        /// <summary>
        /// Get the material of this GameObject's renderer
        /// </summary>
        public Material GetMaterial()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer == null) return null;

            if (Application.isPlaying)
            {
                return renderer.material;
            }
            else
            {
                return renderer.sharedMaterial;
            }
        }

        /// <summary>
        /// Check if this GameObject has a valid renderer and material
        /// </summary>
        public bool HasValidMaterial()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer == null) return false;

            if (Application.isPlaying)
            {
                return renderer.material != null;
            }
            else
            {
                return renderer.sharedMaterial != null;
            }
        }
    }
}
