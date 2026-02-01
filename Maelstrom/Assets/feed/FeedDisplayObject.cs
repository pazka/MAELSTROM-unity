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
        private readonly Vector2 circleCenter = new(0, 540); // Center of first circle (1920x1080)
        private readonly float circleRadius = 900; // Radius of each circle
        public float createdGameTime;

        public float creationTime = 0.0f;
        private readonly GameObject gameObject;
        private readonly Material material;
        public float normalizedCreationTime;
        private readonly Renderer renderer;
        private Vector2 velocity;


        public FeedDisplayObject(GameObject pointDisplay)
        {
            gameObject = pointDisplay;
            renderer = pointDisplay.GetComponent<Renderer>();
            material = renderer.material;
            if (renderer == null) throw new Exception("Renderer not found on point display");
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

            this.DataPoint = dataPoint;
            this.normalizedCreationTime = normalizedCreationTime;
            createdGameTime = Time.time;

            // Random position within chosen circle
            var position = GetRandomPositionInCircle(circleCenter, circleRadius);

            // Velocity based on retweet count (normalized)
            var velocityScale = 200 + dataPoint.normalizedRetweetCount * 50;
            velocity = new Vector2(
                (Random.value - 0.5f) * velocityScale,
                (Random.value - 0.5f) * velocityScale
            );

            // Size based on retweet count (normalized)
            var sizeScale = 25 + dataPoint.normalizedRetweetCount * 300; // 25 to 175 pixels
            var pixelSize = new Vector2(sizeScale, sizeScale);

            // Set initial position and scale
            gameObject.transform.position = position;
            gameObject.transform.localScale = pixelSize;
        }

        public void Update(float deltaTime, float maelstrom)
        {
            if (gameObject != null)
            {
                var currentPosition = gameObject.transform.position;
                var newPosition = currentPosition + new Vector3(velocity.x, velocity.y, 0) * deltaTime * 5f * maelstrom;

                // Check if object has moved outside current circle
                var distanceFromCenter = Vector2.Distance(new Vector2(newPosition.x, newPosition.y), circleCenter);

                if (distanceFromCenter > circleRadius)
                {
                    velocity = -velocity;
                    newPosition = currentPosition + new Vector3(velocity.x, velocity.y, 0) * deltaTime * 5f * maelstrom;
                }

                // Normal movement within circle
                gameObject.transform.position = newPosition;

                material.SetColor("_Color", new Color(1 - maelstrom, 1 - maelstrom, 1));
            }
        }

        private void Reset()
        {
            if (gameObject != null)
            {
                gameObject.transform.position = Vector3.zero;
                gameObject.transform.localScale = Vector3.one;
            }

            DataPoint = default;
            velocity = Vector2.zero;
        }

        /// <summary>
        ///     Get a random position within a circle
        /// </summary>
        private Vector2 GetRandomPositionInCircle(Vector2 center, float radius)
        {
            // Generate random angle and distance
            var angle = Random.Range(0f, 2f * Mathf.PI);
            var distance = Random.Range(radius / 5, radius);

            return center + new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );
        }


        /// <summary>
        ///     Enable or disable this display object
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            if (gameObject != null) gameObject.SetActive(enabled);
        }
    }
}