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

            var size = 5 + 5 + dataPoint.normalizedFollowersCount * 50;
            gameObject.transform.localScale = new Vector3(size, size, 0);

        }

        /// <summary>
        /// Initialize the display object from a data point (handles all behavior mapping internally)
        /// </summary>
        public void InitializeFromDataPoint(GhostNetDataPoint dataPoint, Vector2 screenSize, float creationTime)
        {
            this.dataPoint = dataPoint;
            this.creationTime = creationTime;

            // Random position on screen
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(-screenSize.x / 2, screenSize.x),
                UnityEngine.Random.Range(-screenSize.y / 2, screenSize.y)
            );
            gameObject.transform.position = position;

            // Velocity based on tweet count (normalized)
            var oneAccountVelocity = 20 + dataPoint.daynormalizedNbTweets * 50;
            var velocity = dataPoint.isAggregated ? 2 : oneAccountVelocity;

            this.velocity = new Vector2(
                (UnityEngine.Random.value - 0.5f) * velocity,
                (UnityEngine.Random.value - 0.5f) * velocity
            );

            // Size based on followers count
            var oneAccountSize = 20 + dataPoint.normalizedFollowersCount * 50;
            var size = dataPoint.isAggregated ? 2 : oneAccountSize;
            gameObject.transform.localScale = new Vector3(size, size, 0);
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
