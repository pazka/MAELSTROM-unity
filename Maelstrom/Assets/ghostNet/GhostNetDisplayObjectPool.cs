using System.Collections.Generic;
using UnityEngine;
using System;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Display object pool for managing GhostNetDisplayObject instances
    /// </summary>
    public class GhostNetDisplayObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 10000;
        [SerializeField] private int maxActiveObjects = 100000;
        [SerializeField] private int maxPoolSize = 500000; // Maximum total pool size to prevent unlimited growth
        [SerializeField] private int expansionSize = 1000; // How many objects to create when expanding the pool

        private List<GhostNetDisplayObject> _displayObjectPool = new List<GhostNetDisplayObject>();
        private Queue<GhostNetDisplayObject> _activeObjects = new Queue<GhostNetDisplayObject>();
        private Queue<GhostNetDisplayObject> _inactiveObjects = new Queue<GhostNetDisplayObject>();
        private int _nextPoolIndex = 0;
        private bool _isInitialized = false;

        private Vector2 screenSize;
        private GhostNetPointPool _ghostNetPool; // Reference to the point pool for creating new objects

        /// <summary>
        /// Initialize the display object pool
        /// </summary>
        public void Initialize(GhostNetPointPool ghostNetPool, Vector2 screenSize)
        {
            this.screenSize = screenSize;
            this._ghostNetPool = ghostNetPool; // Store reference for dynamic creation

            if (_isInitialized)
            {
                Debug.LogWarning("DisplayObjectPool already initialized");
                return;
            }

            if (ghostNetPool == null)
            {
                Debug.LogError("GhostNetPointPool is null");
                return;
            }

            int poolSize = Mathf.Max(initialPoolSize, 1000); // At least 1000 objects

            for (int i = 0; i < poolSize; i++)
            {
                GameObject prefab = ghostNetPool.GetOne();
                if (prefab != null)
                {
                    GhostNetDisplayObject displayObject = new GhostNetDisplayObject(prefab);
                    displayObject.SetEnabled(false); // Start inactive
                    _displayObjectPool.Add(displayObject);
                }
            }

            _isInitialized = true;
            Debug.Log($"DisplayObjectPool initialized with {_displayObjectPool.Count} objects");
        }

        /// <summary>
        /// Create more objects for the pool when needed
        /// </summary>
        private bool CreateMoreObjects()
        {
            if (_ghostNetPool == null)
            {
                Debug.LogError("GhostNetPointPool reference is null, cannot create more objects");
                return false;
            }

            // Check if we've reached the maximum pool size
            if (_displayObjectPool.Count >= maxPoolSize)
            {
                Debug.LogWarning($"Maximum pool size reached: {maxPoolSize}, cannot create more objects");
                return false;
            }

            int objectsToCreate = Mathf.Min(expansionSize, maxPoolSize - _displayObjectPool.Count);
            int createdCount = 0;

            for (int i = 0; i < objectsToCreate; i++)
            {
                GameObject prefab = _ghostNetPool.GetOne();
                if (prefab != null)
                {
                    GhostNetDisplayObject displayObject = new GhostNetDisplayObject(prefab);
                    displayObject.SetEnabled(false); // Start inactive
                    _displayObjectPool.Add(displayObject);
                    createdCount++;
                }
                else
                {
                    Debug.LogWarning("GhostNetPointPool returned null, stopping object creation");
                    break;
                }
            }

            if (createdCount > 0)
            {
                Debug.Log($"Created {createdCount} new objects, total pool size: {_displayObjectPool.Count}");
            }

            return createdCount > 0;
        }

        /// <summary>
        /// Get a recycled display object from the pool
        /// </summary>
        public GhostNetDisplayObject GetRecycledDisplayObject()
        {
            if (!_isInitialized)
            {
                Debug.LogError("DisplayObjectPool not initialized");
                return null;
            }

            GhostNetDisplayObject displayObject = null;

            // First, try to get from inactive queue
            if (_inactiveObjects.Count > 0)
            {
                displayObject = _inactiveObjects.Dequeue();
            }
            else
            {
                // If no inactive objects, find an unused object from the pool
                for (int i = 0; i < _displayObjectPool.Count; i++)
                {
                    int index = (_nextPoolIndex + i) % _displayObjectPool.Count;
                    GhostNetDisplayObject obj = _displayObjectPool[index];

                    if (!obj.IsEnabled)
                    {
                        displayObject = obj;
                        _nextPoolIndex = (index + 1) % _displayObjectPool.Count;
                        break;
                    }
                }
            }

            // If still no object available, try to create more objects
            if (displayObject == null)
            {
                if (CreateMoreObjects())
                {
                    // Try again to find an unused object after creating more
                    for (int i = 0; i < _displayObjectPool.Count; i++)
                    {
                        int index = (_nextPoolIndex + i) % _displayObjectPool.Count;
                        GhostNetDisplayObject obj = _displayObjectPool[index];

                        if (!obj.IsEnabled)
                        {
                            displayObject = obj;
                            _nextPoolIndex = (index + 1) % _displayObjectPool.Count;
                            break;
                        }
                    }
                }
            }

            if (displayObject == null)
            {
                Debug.LogError("No available display objects in pool and cannot create more");
                return null;
            }

            return displayObject;
        }

        /// <summary>
        /// Activate display objects for a data point (handles the full activation logic)
        /// </summary>
        public void ActivateDataPoint(GhostNetDataPoint dataPoint, float creationTime)
        {
            if (!_isInitialized)
            {
                Debug.LogError("DisplayObjectPool not initialized");
                return;
            }

            // Check if we can activate more objects
            if (_activeObjects.Count >= maxActiveObjects)
            {
                Debug.LogWarning($"Max active objects limit reached: {maxActiveObjects}");
                return;
            }

            // Use nb_accounts_others if available, otherwise default to 1
            int accountsToDisplay = dataPoint.nb_accounts_others > 0 ? dataPoint.nb_accounts_others : 1;

            for (var i = 0; i < accountsToDisplay; i++)
            {
                // Check if we've reached the limit
                if (_activeObjects.Count >= maxActiveObjects)
                {
                    Debug.LogWarning($"Max active objects limit reached during activation: {maxActiveObjects}");
                    break;
                }

                GhostNetDisplayObject displayObject = GetRecycledDisplayObject();
                if (displayObject == null)
                {
                    Debug.LogError("No available display objects in pool");
                    break;
                }

                // Let the display object handle its own initialization based on data point
                displayObject.InitializeFromDataPoint(dataPoint, screenSize, creationTime);
                displayObject.SetEnabled(true);
                _activeObjects.Enqueue(displayObject);
            }
        }

        /// <summary>
        /// Recycle a display object back to the pool
        /// </summary>
        public void RecycleDisplayObject(GhostNetDisplayObject displayObject)
        {
            if (displayObject == null) return;

            // Reset the DisplayObject for reuse
            displayObject.Reset();
            displayObject.SetEnabled(false);

            // Add to inactive queue for quick reuse
            _inactiveObjects.Enqueue(displayObject);
        }

        /// <summary>
        /// Recycle old objects that exceed the display duration
        /// </summary>
        public void RecycleOldObjects(float normalizedCurrentTime, float normalizedDisplayDuration)
        {
            while (_activeObjects.Count > 0)
            {
                GhostNetDisplayObject obj = _activeObjects.Peek();
                float objectAge = normalizedCurrentTime - obj.creationTime;

                if (objectAge >= normalizedDisplayDuration)
                {
                    _activeObjects.Dequeue();
                    RecycleDisplayObject(obj);
                }
                else
                {
                    // Since data is ordered, if this object is not old enough, 
                    // none of the remaining objects will be either
                    break;
                }
            }
        }

        /// <summary>
        /// Update all active display objects
        /// </summary>
        public void UpdateActiveObjects()
        {
            foreach (var obj in _activeObjects)
            {
                if (obj != null)
                {
                    obj.Update(Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Get all active objects for external iteration
        /// </summary>
        public Queue<GhostNetDisplayObject> GetActiveObjects()
        {
            return _activeObjects;
        }

        /// <summary>
        /// Get the count of active objects
        /// </summary>
        public int GetActiveObjectCount()
        {
            return _activeObjects.Count;
        }

        /// <summary>
        /// Get the count of inactive objects
        /// </summary>
        public int GetInactiveObjectCount()
        {
            return _inactiveObjects.Count;
        }

        /// <summary>
        /// Get the total pool size
        /// </summary>
        public int GetPoolSize()
        {
            return _displayObjectPool.Count;
        }

        /// <summary>
        /// Clear all objects and reset the pool
        /// </summary>
        public void ClearPool()
        {
            // Clean up all active objects
            while (_activeObjects.Count > 0)
            {
                GhostNetDisplayObject obj = _activeObjects.Dequeue();
                if (obj != null)
                {
                    obj.Reset();
                    obj.SetEnabled(false);
                }
            }

            // Clean up inactive objects
            while (_inactiveObjects.Count > 0)
            {
                GhostNetDisplayObject obj = _inactiveObjects.Dequeue();
                if (obj != null)
                {
                    obj.Reset();
                    obj.SetEnabled(false);
                }
            }

            // Clean up display object pool
            foreach (var obj in _displayObjectPool)
            {
                if (obj != null)
                {
                    obj.Reset();
                    obj.SetEnabled(false);
                }
            }

            _displayObjectPool.Clear();
            _isInitialized = false;
            _ghostNetPool = null; // Clear the reference

            Debug.Log("DisplayObjectPool cleared and reset");
        }

        /// <summary>
        /// Check if the pool is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Get the maximum number of active objects
        /// </summary>
        public int MaxActiveObjects => maxActiveObjects;

        /// <summary>
        /// Get the maximum pool size
        /// </summary>
        public int MaxPoolSize => maxPoolSize;
    }
}
