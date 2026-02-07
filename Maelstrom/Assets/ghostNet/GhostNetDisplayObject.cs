using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Handles the visual display of GhostNet objects using shaders
    /// </summary>
    public class GhostNetDisplayObject
    {
        private readonly GameObject gameObject;
        private readonly Material material;
        private float angularVelocity;
        private Vector2 centerPosition;
        public float createdGameTime;

        // Circular motion properties
        private float currentAngle;
        private float ellipseRotationAngle; // Random angle for elliptical rotation
        private bool isMovingOutward = true;

        public float normalizedCreationTime;
        public float random;
        private Vector2 targetRadius;
        private Vector2 velocity;

        public GhostNetDisplayObject(GameObject ghostNetObject)
        {
            gameObject = ghostNetObject;
            material = ghostNetObject.GetComponent<Renderer>().material;
            if (material == null) throw new Exception("Material not found on ghost net object");
        }

        public bool IsEnabled { get; private set; }

        public GhostNetDataPoint DataPoint { get; private set; }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        /// <summary>
        ///     Initialize the display object from a data point (handles all behavior mapping internally)
        /// </summary>
        public void InitializeFromDataPoint(GhostNetDataPoint dataPoint, Vector2 screenSize,
            float normalizedCreationTime, float maelstrom, Vector2 centerPos)
        {
            Reset();

            DataPoint = dataPoint;
            this.normalizedCreationTime = normalizedCreationTime;
            createdGameTime = Time.time;
            random = Random.Range(0, 1000) / 1000f;

            // Set center position from parameter
            centerPosition = centerPos;

            // Initialize circular motion parameters - each object gets its own random motion
            // Random angle for circular motion
            currentAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

            // Random rotation angle for elliptical motion (0 to 180 degrees)
            ellipseRotationAngle = Random.Range(0f, 180f) * Mathf.Deg2Rad;

            // Random starting radius (small circle around center)
            var startRadius = Random.Range(10f, 30f);

            // Start at random position on a small circle around center
            var startPosition = centerPosition + new Vector2(
                Mathf.Cos(currentAngle) * startRadius,
                Mathf.Sin(currentAngle) * startRadius
            );
            gameObject.transform.position = startPosition;

            // Target radius: min 300, max 550, influenced by current maelstrom
            var minRadius = 50f;
            var maxRadius = 1000f;

            var adjustedMaxRadius = maxRadius * (0.5f + maelstrom / 2f);

            targetRadius = new Vector2(Random.Range(minRadius, adjustedMaxRadius),
                Random.Range(minRadius, adjustedMaxRadius));

            // Angular velocity: 30 degrees * velocity in 3 seconds
            // Convert to radians per second with random direction
            var oneAccountVelocity = dataPoint.daynormalizedNbTweets * 5;
            var velocity = dataPoint.isAggregated ? random : random * oneAccountVelocity;
            var baseAngularVelocity = 30f * velocity * Mathf.Deg2Rad / 3f;

            // Random direction (clockwise or counterclockwise)
            var direction = Random.value > 0.5f ? 1f : -1f;
            angularVelocity = baseAngularVelocity * direction * 2 * maelstrom;

            // Ensure object starts from small circle and moves outward
            isMovingOutward = true;

            // Size based on followers count
            var oneAccountSize = 5 * random + 5 + dataPoint.normalizedFollowersCount * 15;
            var size = dataPoint.isAggregated ? 2 : oneAccountSize;
            gameObject.transform.localScale = new Vector3(size, size, 0);
            material.SetColor("_Color", new Color(1 - maelstrom, 1 - maelstrom, 1));
        }

        public void Update(float deltaTime)
        {
            var elapsedTime = (Time.time - createdGameTime) * 1f;

            // Apply maelstrom amplification to angular velocity
            var amplifiedAngularVelocity = angularVelocity * 1f;

            // Add sine/cosine randomness for organic movement
            var timeVariation = elapsedTime * 0.5f; // Slow variation
            var sinVariation = Mathf.Sin(timeVariation + random * Mathf.PI) * 0.3f; // Small variation
            var cosVariation = Mathf.Cos(timeVariation * 0.7f + random * Mathf.PI * 2) * 0.2f; // Different frequency

            // Update circular motion with amplified velocity and randomness
            currentAngle += (amplifiedAngularVelocity + sinVariation) * deltaTime;

            // Calculate current radius (moving outward from center) - now elliptical
            Vector2 currentRadius;
            if (isMovingOutward)
            {
                // Move outward over time (reach target radius in 3 seconds)
                var progress = Mathf.Clamp01(elapsedTime / 3f);

                // Add cosine variation to radius for organic pulsing
                var radiusVariation = Mathf.Cos(timeVariation * 1.2f + random * Mathf.PI) * 20f;
                var targetRadiusWithVariation = targetRadius + new Vector2(radiusVariation, radiusVariation);

                currentRadius = Vector2.Lerp(new Vector2(10f, 10f), targetRadiusWithVariation, progress);

                // Stop moving outward when target is reached
                if (progress >= 1f) isMovingOutward = false;
            }
            else
            {
                // Continue with radius variation even after reaching target
                var radiusVariation = Mathf.Cos(timeVariation * 1.2f + random * Mathf.PI) * 20f;
                currentRadius = targetRadius + new Vector2(radiusVariation, radiusVariation);
            }

            // Calculate position based on rotated elliptical motion with additional randomness
            var finalAngle = currentAngle + cosVariation; // Add cosine variation to angle

            // Calculate elliptical position in local coordinates
            var localEllipticalPosition = new Vector2(
                Mathf.Cos(finalAngle) * currentRadius.x,
                Mathf.Sin(finalAngle) * currentRadius.y
            );

            // Rotate the elliptical position by the ellipse rotation angle
            var cosRot = Mathf.Cos(ellipseRotationAngle);
            var sinRot = Mathf.Sin(ellipseRotationAngle);
            var rotatedPosition = new Vector2(
                localEllipticalPosition.x * cosRot - localEllipticalPosition.y * sinRot,
                localEllipticalPosition.x * sinRot + localEllipticalPosition.y * cosRot
            );

            // Apply to world position
            var circularPosition = centerPosition + rotatedPosition;

            gameObject.transform.position = circularPosition;
            // material.SetColor("_Color", new Color(1 - maelstrom, 1 - maelstrom, 1));
        }

        private void Reset()
        {
            gameObject.transform.position = Vector3.zero;
            gameObject.transform.localScale = Vector3.one;
            DataPoint = default;

            // Reset circular motion properties
            currentAngle = 0f;
            targetRadius = Vector2.zero;
            angularVelocity = 0f;
            centerPosition = Vector2.zero;
            isMovingOutward = true;
            ellipseRotationAngle = 0f;
        }


        /// <summary>
        ///     Set shader properties
        /// </summary>
        public void SetShaderProperty(string propertyName, float value)
        {
            if (material != null) material.SetFloat(propertyName, value);
        }

        public void SetShaderProperty(string propertyName, Vector2 value)
        {
            if (material != null) material.SetVector(propertyName, value);
        }

        public void SetShaderProperty(string propertyName, Vector3 value)
        {
            if (material != null) material.SetVector(propertyName, value);
        }

        public void SetShaderProperty(string propertyName, Vector4 value)
        {
            if (material != null) material.SetVector(propertyName, value);
        }

        /// <summary>
        ///     Enable or disable this display object
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            gameObject.SetActive(enabled);
        }
    }
}