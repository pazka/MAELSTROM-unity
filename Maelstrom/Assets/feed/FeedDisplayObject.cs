using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Handles the visual display of data objects using shaders
    /// </summary>
    public class FeedDisplayObject
    {
        private GameObject gameObject;
        private Renderer renderer;
        private Material material;
        private bool isEnabled = false;
        private Vector2 velocity;
        private FeedDataPoint dataPoint;

        public float creationTime = 0.0f;
        public float normalizedCreationTime = 0.0f;
        public float createdGameTime = 0.0f;

        // Dual circle system
        private bool isInFirstCircle = true;
        private Vector2 circleCenter1 = new Vector2(0, 0); // Center of first circle (1920x1080)
        private Vector2 circleCenter2 = new Vector2(0, 1080); // Center of second circle (offset y-1080)
        private float circleRadius = 500f; // Radius of each circle

        public FeedDisplayObject(GameObject pointDisplay)
        {
            gameObject = pointDisplay;
            renderer = pointDisplay.GetComponent<Renderer>();
            material = renderer.material;
            if (renderer == null)
            {
                throw new System.Exception("Renderer not found on point display");
            }
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        /// <summary>
        /// Initialize the display object from a data point (handles all behavior mapping internally)
        /// </summary>
        public void InitializeFromDataPoint(FeedDataPoint dataPoint, Vector2 screenSize, float normalizedCreationTime)
        {
            Reset();

            this.dataPoint = dataPoint;
            this.normalizedCreationTime = normalizedCreationTime;
            this.createdGameTime = Time.time;

            // Randomly choose which circle to start in
            isInFirstCircle = UnityEngine.Random.value > 0.5f;

            // Random position within chosen circle
            Vector2 chosenCenter = isInFirstCircle ? circleCenter1 : circleCenter2;
            Vector2 position = GetRandomPositionInCircle(chosenCenter, circleRadius);

            // Velocity based on retweet count (normalized)
            float velocityScale = 150 - dataPoint.normalizedRetweetCount * 120; // 20 to 100 pixels per second
            this.velocity = new Vector2(
                (UnityEngine.Random.value - 0.5f) * velocityScale,
                (UnityEngine.Random.value - 0.5f) * velocityScale
            );

            // Size based on retweet count (normalized)
            float sizeScale = 25 + dataPoint.normalizedRetweetCount * 150; // 25 to 175 pixels
            Vector2 pixelSize = new Vector2(sizeScale, sizeScale);

            // Set initial position and scale
            gameObject.transform.position = position;
            gameObject.transform.localScale = pixelSize;
        }

        public void Update(float deltaTime, float maelstrom)
        {
            if (gameObject != null)
            {
                Vector3 currentPosition = gameObject.transform.position;
                Vector3 newPosition = currentPosition + new Vector3(velocity.x, velocity.y, 0) * deltaTime;

                // Check if object has moved outside current circle
                Vector2 currentCenter = isInFirstCircle ? circleCenter1 : circleCenter2;
                float distanceFromCenter = Vector2.Distance(new Vector2(newPosition.x, newPosition.y), currentCenter);

                if (distanceFromCenter > circleRadius)
                {
                    // Object has crossed circle boundary - wrap to opposite circle maintaining path
                    WrapToOppositeCircle(newPosition, currentCenter);
                }
                else
                {
                    // Normal movement within circle
                    gameObject.transform.position = newPosition;
                }

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
            dataPoint = default;
            velocity = Vector2.zero;
            isInFirstCircle = true; // Will be set properly during initialization
        }

        /// <summary>
        /// Get a random position within a circle
        /// </summary>
        private Vector2 GetRandomPositionInCircle(Vector2 center, float radius)
        {
            // Generate random angle and distance
            float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float distance = UnityEngine.Random.Range(0f, radius);

            return center + new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );
        }

        /// <summary>
        /// Wrap object to opposite circle when it crosses boundary, reversing direction to continue away from old circle
        /// </summary>
        private void WrapToOppositeCircle(Vector3 exitPosition, Vector2 currentCenter)
        {
            // Switch to opposite circle
            isInFirstCircle = !isInFirstCircle;
            Vector2 oppositeCenter = isInFirstCircle ? circleCenter1 : circleCenter2;

            // Calculate direction from current center to exit position
            Vector2 directionFromCenter = new Vector2(exitPosition.x, exitPosition.y) - currentCenter;
            directionFromCenter = directionFromCenter.normalized;

            // Place object at opposite position in the other circle
            Vector2 wrappedPosition = oppositeCenter + directionFromCenter * circleRadius;

            // Reverse the velocity direction so object continues away from the old circle
            velocity = -velocity;

            gameObject.transform.position = new Vector3(wrappedPosition.x, wrappedPosition.y, exitPosition.z);
        }

        /// <summary>
        /// Enable or disable this display object
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            if (gameObject != null)
            {
                gameObject.SetActive(enabled);
            }
        }

        public bool IsEnabled => isEnabled;
        public FeedDataPoint DataPoint => dataPoint;
        public bool IsInFirstCircle => isInFirstCircle;
        public Vector2 CurrentCircleCenter => isInFirstCircle ? circleCenter1 : circleCenter2;

        /// <summary>
        /// Check if the object is currently within any circle (for debugging/visualization purposes)
        /// Note: This doesn't affect movement behavior - objects can move freely and wrap between circles
        /// </summary>
        public bool IsWithinAnyCircle()
        {
            Vector2 currentPos = new Vector2(gameObject.transform.position.x, gameObject.transform.position.y);
            float distanceToCircle1 = Vector2.Distance(currentPos, circleCenter1);
            float distanceToCircle2 = Vector2.Distance(currentPos, circleCenter2);

            return distanceToCircle1 <= circleRadius || distanceToCircle2 <= circleRadius;
        }
    }
}
