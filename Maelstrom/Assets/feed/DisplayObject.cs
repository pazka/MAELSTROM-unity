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
        private bool isEnabled = false;
        private Vector2 velocity;

        public float creationTime = 0.0f;

        public DisplayObject(GameObject pointDisplay)
        {
            gameObject = pointDisplay;
            renderer = pointDisplay.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new System.Exception("Renderer not found on point display");
            }
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public void Initialize(Vector2 position, Vector2 velocity, Vector2 screenSize, Vector2 pixelSize)
        {
            this.velocity = velocity;
            gameObject.transform.position = position;
            gameObject.transform.localScale = pixelSize;
        }

        public void Update(float deltaTime)
        {
            if (gameObject != null)
            {
                gameObject.transform.position += new Vector3(velocity.x, velocity.y, 0) * deltaTime;
            }
        }

        public void Reset()
        {
            if (gameObject != null)
            {
                gameObject.transform.position = Vector3.zero;
                gameObject.transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Set shader properties
        /// </summary>
        public void SetShaderProperty(string propertyName, float value)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetFloat(propertyName, value);
            }
        }

        public void SetShaderProperty(string propertyName, Vector2 value)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetVector(propertyName, value);
            }
        }

        public void SetShaderProperty(string propertyName, Vector3 value)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetVector(propertyName, value);
            }
        }

        public void SetShaderProperty(string propertyName, Vector4 value)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetVector(propertyName, value);
            }
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

    }
}
