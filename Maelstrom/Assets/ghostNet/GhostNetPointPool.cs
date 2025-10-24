using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Object pool for GhostNet GameObjects to avoid frequent instantiation/destruction
    /// </summary>
    public class GhostNetPointPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject ghostNetPrefab;
        [SerializeField] private int initialPoolSize = 2000; // Reduced from 10000
        [SerializeField] private int maxPoolSize = 5000; // Reduced from 100000

        private Queue<GameObject> _availableObjects = new Queue<GameObject>();
        private Transform _poolParent;
        private int _totalObjectCount = 0;

        public GameObject GhostNetPrefab => ghostNetPrefab;

        private void Awake()
        {

            if (SceneManager.GetActiveScene().name != "GhostNetsScene")
            {
                return;
            }

            // Create a parent object to hold all pooled objects
            _poolParent = new GameObject("GhostNetPool").transform;
            _poolParent.SetParent(transform);

            // Pre-create initial pool
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewObject();
            }
        }

        private GameObject CreateNewObject()
        {
            if (ghostNetPrefab == null)
            {
                Debug.LogError("GhostNetPrefab is not assigned!");
                return null;
            }

            GameObject newObj = Instantiate(ghostNetPrefab, _poolParent);
            newObj.SetActive(false);
            _availableObjects.Enqueue(newObj);
            _totalObjectCount++;
            return newObj;
        }

        /// <summary>
        /// Get a GameObject from the pool. Creates new ones if needed up to maxPoolSize
        /// </summary>
        public GameObject GetOne()
        {
            if (_availableObjects.Count > 0)
            {
                GameObject obj = _availableObjects.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            // No available objects, create new one if under max limit
            if (_totalObjectCount < maxPoolSize)
            {
                GameObject newObj = CreateNewObject();
                if (newObj != null)
                {
                    newObj.SetActive(true);
                }
                return newObj;
            }

            Debug.LogWarning($"GhostNet pool exhausted, total available: {_availableObjects.Count}");
            return null;
        }

        /// <summary>
        /// Get count of available objects
        /// </summary>
        public int AvailableCount => _availableObjects.Count;

        /// <summary>
        /// Get total count of objects in pool
        /// </summary>
        public int TotalCount => _totalObjectCount;

        /// <summary>
        /// Clear all objects from the pool
        /// </summary>
        public void ClearPool()
        {
            // Destroy all objects in the available queue
            while (_availableObjects.Count > 0)
            {
                GameObject obj = _availableObjects.Dequeue();
                if (obj != null)
                {
                    DestroyImmediate(obj);
                }
            }

            // Reset counters
            _totalObjectCount = 0;
        }

        private void OnDestroy()
        {
            ClearPool();
        }
    }
}
