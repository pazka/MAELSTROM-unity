using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Handles the visual display of data objects using shaders
    /// </summary>
    public class DisplayObject
    {
        private GameObject gameObject;
        private Material material;
        private bool isEnabled = false;
        private Vector2 velocity;

        public float creationTime = 0.0f;

        public DisplayObject(GameObject pointDisplay)
        {
            gameObject = pointDisplay;
            material = pointDisplay.GetComponent<Renderer>().material;
            if (material == null)
            {
                throw new System.Exception("Material not found on point display");
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
            gameObject.transform.position += new Vector3(velocity.x, velocity.y, 0) * deltaTime;
        }

        public void Reset()
        {
            gameObject.transform.position = Vector3.zero;
            gameObject.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Set shader properties
        /// </summary>
        public void SetShaderProperty(string propertyName, float value)
        {
            material.SetFloat(propertyName, value);
        }

        public void SetShaderProperty(string propertyName, Vector2 value)
        {
            material.SetVector(propertyName, value);
        }

        public void SetShaderProperty(string propertyName, Vector3 value)
        {
            material.SetVector(propertyName, value);
        }

        public void SetShaderProperty(string propertyName, Vector4 value)
        {
            material.SetVector(propertyName, value);
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

    }
}
