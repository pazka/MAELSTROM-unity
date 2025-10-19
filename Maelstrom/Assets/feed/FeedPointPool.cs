using System.Collections.Generic;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Generic object pool for GameObjects to avoid frequent instantiation/destruction
    /// </summary>
    public class PointPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject displayObjectPrefab;
        [SerializeField] private int initialPoolSize = 100000;
        [SerializeField] private int maxPoolSize = 1000000;

        private Queue<GameObject> _availableObjects = new Queue<GameObject>();
        private List<GameObject> _allObjects = new List<GameObject>();
        private Transform _poolParent;

        public GameObject DisplayObjectPrefab => displayObjectPrefab;

        private void Awake()
        {
            // Create a parent object to hold all pooled objects
            _poolParent = new GameObject("ObjectPool").transform;
            _poolParent.SetParent(transform);

            // Pre-create initial pool
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewObject();
            }
        }

        private GameObject CreateNewObject()
        {
            if (displayObjectPrefab == null)
            {
                Debug.LogError("DisplayObjectPrefab is not assigned!");
                return null;
            }

            GameObject newObj = Instantiate(displayObjectPrefab, _poolParent);
            newObj.SetActive(false);
            _allObjects.Add(newObj);
            _availableObjects.Enqueue(newObj);
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
            if (_allObjects.Count < maxPoolSize)
            {
                Debug.Log($"Creating new object, total count: {_allObjects.Count}");
                GameObject newObj = CreateNewObject();
                return newObj;
            }

            Debug.LogWarning($"Pool exhausted, total count: {_allObjects.Count}");
            return null;
        }

        /// <summary>
        /// Return a GameObject to the pool
        /// </summary>
        public void ReturnObject(GameObject obj)
        {
            if (obj != null && _allObjects.Contains(obj))
            {
                obj.SetActive(false);
                _availableObjects.Enqueue(obj);
            }
        }

        /// <summary>
        /// Get count of available objects
        /// </summary>
        public int AvailableCount => _availableObjects.Count;

        /// <summary>
        /// Get total count of objects in pool
        /// </summary>
        public int TotalCount => _allObjects.Count;

        /// <summary>
        /// Clear all objects from the pool
        /// </summary>
        public void ClearPool()
        {
            if (_allObjects != null)
            {
                foreach (GameObject obj in _allObjects)
                {
                    if (obj != null)
                    {
                        DestroyImmediate(obj);
                    }
                }
                _allObjects.Clear();
            }

            if (_availableObjects != null)
            {
                _availableObjects.Clear();
            }
        }

        private void OnDestroy()
        {
            try
            {
                ClearPool();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[POINT_POOL] Error during cleanup: {ex.Message}");
            }
        }
    }
}
