using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Handles the visual display of data objects using shaders
    /// </summary>
    public class FeedDisplayObject
    {
        // Dual circle system
        private readonly Vector3 circleCenter = new(0, 540); // Center of first circle (1920x1080)
        private readonly float circleRadius = 900; // Radius of each circle
        private readonly GameObject gameObject;
        private readonly Material material;
        private readonly int maxPointSize = 200;
        private readonly int minPointSize = 25;
        private readonly Renderer renderer;
        public float createdGameTime;

        public float creationTime = 0.0f;

        private float internalMaelstrom;
        public float normalizedCreationTime;
        private Vector2 pixelMaxVelocity;
        private Vector3 virtualPosition = Vector2.zero;


        public FeedDisplayObject(GameObject pointDisplay)
        {
            gameObject = pointDisplay;
            renderer = pointDisplay.GetComponent<Renderer>();
            material = renderer.material;
            minPointSize = Config.Get("feed_minPointSize", 25);
            maxPointSize = Config.Get("feed_maxPointSize", 200);
            if (!renderer) throw new Exception("Renderer not found on point display");
        }

        public bool IsEnabled { get; private set; }

        public FeedDataPoint DataPoint { get; private set; }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        /// <summary>
        ///     Initialize the display object from a data point (handles all behavior mapping internally)
        /// </summary>
        public void InitializeFromDataPoint(FeedDataPoint dataPoint, Vector2 screenSize, float normalizedCreationTime,
            float maelstrom = 0f)
        {
            Reset();

            DataPoint = dataPoint;
            this.normalizedCreationTime = normalizedCreationTime;
            createdGameTime = Time.time;

            // Random position within chosen circle
            var position = GetRandomPositionInCircle(circleCenter, circleRadius);

            // Velocity based on retweet count (normalized)
            var velocityScale = 200 * (0.4f + 0.6f * maelstrom - 0.4f * dataPoint.normalizedRetweetCount);
            pixelMaxVelocity = new Vector2(
                velocityScale,
                velocityScale
            );

            // Size based on retweet count (normalized)
            var sizeScale = minPointSize + dataPoint.normalizedRetweetCount * maxPointSize;
            var pixelSize = new Vector2(sizeScale, sizeScale);

            // Set initial position and scale
            virtualPosition = position;
            gameObject.transform.position = virtualPosition;
            gameObject.transform.localScale = pixelSize;

            material.SetColor("_Color", new Color(1 - maelstrom, 1 - maelstrom, 1));
            internalMaelstrom = maelstrom;
        }

        public void Update(float deltaTime, float maelstrom)
        {
            if (!gameObject) return;

            var currentPosition = virtualPosition;

            const float perlinScale = 800f;
            var time = Time.time * (0.2f + maelstrom);

            var noiseX = Mathf.PerlinNoise(
                currentPosition.x / perlinScale + time,
                currentPosition.y / perlinScale
            ) * 2f - 1f;

            var noiseY = Mathf.PerlinNoise(
                currentPosition.x / perlinScale,
                currentPosition.y / perlinScale + time
            ) * 2f - 1f;

            var noiseVelocity = new Vector2(noiseX, noiseY);

            var translation = pixelMaxVelocity * noiseVelocity * deltaTime;

            virtualPosition = currentPosition + new Vector3(translation.x, translation.y, 0);
            gameObject.transform.position =
                GetProjectedPositionInCircle(virtualPosition, circleCenter, circleRadius);
            material.SetColor("_Color", new Color(1 - maelstrom, 1 - maelstrom, 1));
        }


        private void Reset()
        {
            if (gameObject)
            {
                gameObject.transform.position = Vector3.zero;
                gameObject.transform.localScale = Vector3.one;
            }

            DataPoint = default;
            pixelMaxVelocity = Vector2.zero;
        }

        /// <summary>
        ///     Get a random position within a circle
        /// </summary>
        private Vector2 GetRandomPositionInCircle(Vector2 center, float radius)
        {
            return center + Random.insideUnitCircle * radius;
        }

        private Vector3 GetProjectedPositionInCircle(
            Vector3 virtualPosition,
            Vector3 circleCenter,
            float circleRadius)
        {
            // Work in XY plane only
            var delta = new Vector2(
                virtualPosition.x - circleCenter.x,
                virtualPosition.y - circleCenter.y
            );

            var distance = delta.magnitude;

            // If exactly at center, return center (preserve original Z)
            if (distance == 0f)
                return new Vector3(circleCenter.x, circleCenter.y, virtualPosition.z);

            // Radial modulo
            var clampedDistance = Mathf.Min(distance, circleRadius);
            var direction = delta / distance;
            var projectedXY = (Vector2)circleCenter + direction * clampedDistance;


            return new Vector3(projectedXY.x, projectedXY.y, virtualPosition.z);
        }


        /// <summary>
        ///     Enable or disable this display object
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            if (gameObject) gameObject.SetActive(enabled);
        }
    }
}