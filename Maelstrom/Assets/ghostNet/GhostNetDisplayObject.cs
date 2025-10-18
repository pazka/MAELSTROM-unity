using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Handles the visual display of GhostNet objects using shaders
    /// </summary>
    public class GhostNetDisplayObject
    {
        private GameObject gameObject;
        private Material material;
        private bool isEnabled = false;
        private Vector2 velocity;
        private GhostNetDataPoint dataPoint;

        public float creationTime = 0.0f;

        public GhostNetDisplayObject(GameObject ghostNetObject)
        {
            gameObject = ghostNetObject;
            material = ghostNetObject.GetComponent<Renderer>().material;
            if (material == null)
            {
                throw new System.Exception("Material not found on ghost net object");
            }
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public void Initialize(Vector2 position, Vector2 velocity, Vector2 screenSize, GhostNetDataPoint dataPoint)
        {
            this.velocity = velocity;
            this.dataPoint = dataPoint;
            gameObject.transform.position = position;

            // Set shader properties based on data point
            SetOpacity(dataPoint.dayNormPos, dataPoint.dayNormNeu, dataPoint.dayNormNeg);
        }

        public void Update(float deltaTime)
        {
            gameObject.transform.position += new Vector3(velocity.x, velocity.y, 0) * deltaTime;
        }

        public void Reset()
        {
            gameObject.transform.position = Vector3.zero;
            gameObject.transform.localScale = Vector3.one;
            dataPoint = default;
        }

        /// <summary>
        /// Set opacity for positive, neutral, and negative ghost nets
        /// </summary>
        public void SetOpacity(float alphaPos, float alphaNeu, float alphaNeg)
        {
            if (material != null)
            {
                material.SetFloat("_OpacityPos", alphaPos);
                material.SetFloat("_OpacityNeu", alphaNeu);
                material.SetFloat("_OpacityNeg", alphaNeg);
            }
        }

        /// <summary>
        /// Set shader properties
        /// </summary>
        public void SetShaderProperty(string propertyName, float value)
        {
            if (material != null)
            {
                material.SetFloat(propertyName, value);
            }
        }

        public void SetShaderProperty(string propertyName, Vector2 value)
        {
            if (material != null)
            {
                material.SetVector(propertyName, value);
            }
        }

        public void SetShaderProperty(string propertyName, Vector3 value)
        {
            if (material != null)
            {
                material.SetVector(propertyName, value);
            }
        }

        public void SetShaderProperty(string propertyName, Vector4 value)
        {
            if (material != null)
            {
                material.SetVector(propertyName, value);
            }
        }

        /// <summary>
        /// Enable or disable this display object
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            gameObject.SetActive(enabled);
        }

        public bool IsEnabled => isEnabled;
        public GhostNetDataPoint DataPoint => dataPoint;
    }
}
