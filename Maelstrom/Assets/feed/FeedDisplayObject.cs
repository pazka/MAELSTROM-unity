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
        private Vector2 circleCenter = new Vector2(0, 540); // Center of first circle (1920x1080)
        private float circleRadius = 950; // Radius of each circle

        public bool IsEnabled => isEnabled;
        public FeedDataPoint DataPoint => dataPoint;


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
        public void InitializeFromDataPoint(FeedDataPoint dataPoint, Vector2 screenSize, float normalizedCreationTime,float maelstrom = 0f)
        {
            Reset();

            this.dataPoint = dataPoint;
            this.normalizedCreationTime = normalizedCreationTime;
            this.createdGameTime = Time.time;

            // Random position within chosen circle
            Vector2 position = GetRandomPositionInCircle(circleCenter, circleRadius);

            // Velocity based on retweet count (normalized)
            float velocityScale = 200 + dataPoint.normalizedRetweetCount * 50;
            this.velocity = new Vector2(
                (UnityEngine.Random.value - 0.5f) * velocityScale,
                (UnityEngine.Random.value - 0.5f) * velocityScale
            );

            // Size based on retweet count (normalized)
            float sizeScale = 25 + dataPoint.normalizedRetweetCount * 300; // 25 to 175 pixels
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
                Vector3 newPosition = currentPosition + new Vector3(velocity.x, velocity.y, 0) * deltaTime * 5f * maelstrom;

                // Check if object has moved outside current circle
                float distanceFromCenter = Vector2.Distance(new Vector2(newPosition.x, newPosition.y), circleCenter);

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
            dataPoint = default;
            velocity = Vector2.zero;
        }

        /// <summary>
        /// Get a random position within a circle
        /// </summary>
        private Vector2 GetRandomPositionInCircle(Vector2 center, float radius)
        {
            // Generate random angle and distance
            float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float distance = UnityEngine.Random.Range(radius/5, radius);

            return center + new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );
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
    }
}
