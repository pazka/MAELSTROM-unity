using System;
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

        // Circular motion properties
        private float currentAngle;
        private float targetRadius;
        private float angularVelocity;
        private Vector2 centerPosition;
        private bool isMovingOutward = true;

        public float normalizedCreationTime = 0.0f;
        public float createdGameTime = 0.0f;
        public float random = 0.0f;

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

        /// <summary>
        /// Initialize the display object from a data point (handles all behavior mapping internally)
        /// </summary>
        public void InitializeFromDataPoint(GhostNetDataPoint dataPoint, Vector2 screenSize, float normalizedCreationTime)
        {
            Reset();

            this.dataPoint = dataPoint;
            this.normalizedCreationTime = normalizedCreationTime;
            this.createdGameTime = Time.time;
            this.random = UnityEngine.Random.Range(0, 1000) / 1000f;

            // Set center position (center of screen)
            centerPosition = Vector2.zero;

            // Initialize circular motion parameters - each object gets its own random motion
            // Random angle for circular motion
            currentAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;

            // Random starting radius (small circle around center)
            float startRadius = UnityEngine.Random.Range(10f, 30f);

            // Start at random position on a small circle around center
            Vector2 startPosition = centerPosition + new Vector2(
                Mathf.Cos(currentAngle) * startRadius,
                Mathf.Sin(currentAngle) * startRadius
            );
            gameObject.transform.position = startPosition;

            // Target radius: min 50, max 500 (independent of maelstrom)
            float minRadius = 50f;
            float maxRadius = 500f;
            targetRadius = UnityEngine.Random.Range(minRadius, maxRadius);

            // Angular velocity: 30 degrees * velocity in 3 seconds
            // Convert to radians per second with random direction
            var oneAccountVelocity = dataPoint.daynormalizedNbTweets * 5;
            var velocity = dataPoint.isAggregated ? random : random * oneAccountVelocity;
            float baseAngularVelocity = (30f * velocity * Mathf.Deg2Rad) / 3f;

            // Random direction (clockwise or counterclockwise)
            float direction = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            angularVelocity = baseAngularVelocity * direction;

            // Ensure object starts from small circle and moves outward
            isMovingOutward = true;

            // Size based on followers count
            var oneAccountSize = 10 * random + 10 + dataPoint.normalizedFollowersCount * 50;
            var size = dataPoint.isAggregated ? 2 : oneAccountSize;
            gameObject.transform.localScale = new Vector3(size, size, 0);
        }

        public void Update(float deltaTime, float maelstrom)
        {
            float elapsedTime = (Time.time - createdGameTime) * 0.1f;

            // Apply maelstrom amplification to angular velocity
            float amplifiedAngularVelocity = angularVelocity * (1f + maelstrom);

            // Add sine/cosine randomness for organic movement
            float timeVariation = elapsedTime * 0.5f; // Slow variation
            float sinVariation = Mathf.Sin(timeVariation + random * Mathf.PI) * 0.3f; // Small variation
            float cosVariation = Mathf.Cos(timeVariation * 0.7f + random * Mathf.PI * 2) * 0.2f; // Different frequency

            // Update circular motion with amplified velocity and randomness
            currentAngle += (amplifiedAngularVelocity + sinVariation) * deltaTime;

            // Calculate current radius (moving outward from center)
            float currentRadius = 0f;
            if (isMovingOutward)
            {
                // Move outward over time (reach target radius in 3 seconds)
                float progress = Mathf.Clamp01(elapsedTime / 3f);

                // Add cosine variation to radius for organic pulsing
                float radiusVariation = Mathf.Cos(timeVariation * 1.2f + random * Mathf.PI) * 20f;
                float targetRadiusWithVariation = targetRadius + radiusVariation + (maelstrom * 200);

                currentRadius = Mathf.Lerp(10f, targetRadiusWithVariation, progress);

                // Stop moving outward when target is reached
                if (progress >= 1f)
                {
                    isMovingOutward = false;
                }
            }
            else
            {
                // Continue with radius variation even after reaching target
                float radiusVariation = Mathf.Cos(timeVariation * 1.2f + random * Mathf.PI) * 20f;
                currentRadius = targetRadius + radiusVariation + (maelstrom * 200);
            }

            // Calculate position based on circular motion with additional randomness
            float finalAngle = currentAngle + cosVariation; // Add cosine variation to angle
            Vector2 circularPosition = centerPosition + new Vector2(
                Mathf.Cos(finalAngle) * currentRadius,
                Mathf.Sin(finalAngle) * currentRadius
            );

            gameObject.transform.position = circularPosition;
            material.SetColor("_Color", new Color(1 - maelstrom, 1 - maelstrom, 1));
        }

        private void Reset()
        {
            gameObject.transform.position = Vector3.zero;
            gameObject.transform.localScale = Vector3.one;
            dataPoint = default;

            // Reset circular motion properties
            currentAngle = 0f;
            targetRadius = 0f;
            angularVelocity = 0f;
            centerPosition = Vector2.zero;
            isMovingOutward = true;
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
