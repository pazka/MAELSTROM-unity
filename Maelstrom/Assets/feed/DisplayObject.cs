using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Handles the visual display of data objects using shaders
    /// </summary>
    public class DisplayObject
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

        public DisplayObject(GameObject pointDisplay)
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

            // Random position on screen
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(0, screenSize.x),
                UnityEngine.Random.Range(0, screenSize.y)
            );

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
                gameObject.transform.position += new Vector3(velocity.x, velocity.y, 0) * deltaTime;

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
    }
}
