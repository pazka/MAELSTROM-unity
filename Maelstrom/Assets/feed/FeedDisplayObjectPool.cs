using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Display object pool for managing DisplayObject instances
    /// </summary>
    public class FeedDisplayObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")] [SerializeField]
        private int initialPoolSize = 50000;

        [SerializeField] private int maxActiveObjects = 500000;
        [SerializeField] private int maxPoolSize = 100000; // Maximum total pool size to prevent unlimited growth

        [SerializeField] private PointPool pointPool; // Reference to the point pool for creating new objects

        private readonly Queue<FeedDisplayObject> _activeObjects = new();
        private readonly Queue<FeedDisplayObject> _inactiveObjects = new();

        private Vector2 screenSize;

        /// <summary>
        ///     Check if the pool is initialized
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        ///     Get the maximum number of active objects
        /// </summary>
        public int MaxActiveObjects => maxActiveObjects;

        /// <summary>
        ///     Get the maximum pool size
        /// </summary>
        public int MaxPoolSize => maxPoolSize;

        private void OnDestroy()
        {
            ClearPool();
        }

        /// <summary>
        ///     Initialize the display object pool
        /// </summary>
        public void Initialize(Vector2 screenSize)
        {
            this.screenSize = screenSize;

            if (IsInitialized)
            {
                AppLogger.LogWarning("FeedDisplayObjectPool already initialized");
                return;
            }

            if (pointPool == null)
            {
                AppLogger.LogError("PointPool is null");
                return;
            }

            for (var i = 0; i < initialPoolSize; i++)
            {
                var prefab = pointPool.GetOne();
                if (prefab != null)
                {
                    var displayObject = new FeedDisplayObject(prefab);
                    displayObject.SetEnabled(false); // Start inactive
                    _inactiveObjects.Enqueue(displayObject);
                }
            }

            IsInitialized = true;
            AppLogger.Log($"FeedDisplayObjectPool initialized with {_inactiveObjects.Count} objects");
        }

        /// <summary>
        ///     Create more objects for the pool when needed
        /// </summary>
        private bool CreateMoreObjects()
        {
            if (pointPool == null)
            {
                AppLogger.LogError("PointPool reference is null, cannot create more objects");
                return false;
            }

            var currentPoolSize = _activeObjects.Count + _inactiveObjects.Count;

            // Check if we've reached the maximum pool size
            if (currentPoolSize >= maxPoolSize)
            {
                AppLogger.LogWarning($"Maximum pool size reached: {maxPoolSize}, cannot create more objects");
                return false;
            }

            var objectsToCreate = maxPoolSize - currentPoolSize;
            var createdCount = 0;

            for (var i = 0; i < objectsToCreate; i++)
            {
                var prefab = pointPool.GetOne();
                if (prefab != null)
                {
                    var displayObject = new FeedDisplayObject(prefab);
                    displayObject.SetEnabled(false); // Start inactive
                    _inactiveObjects.Enqueue(displayObject);
                    createdCount++;
                }
                else
                {
                    AppLogger.LogWarning("PointPool returned null, stopping object creation");
                    break;
                }
            }

            if (createdCount > 0)
                AppLogger.Log($"Created {createdCount} new objects, total pool size: {currentPoolSize + createdCount}");

            return createdCount > 0;
        }

        /// <summary>
        ///     Get a recycled display object from the pool
        /// </summary>
        public FeedDisplayObject GetRecycledDisplayObject()
        {
            if (!IsInitialized)
            {
                AppLogger.LogError("FeedDisplayObjectPool not initialized");
                return null;
            }

            FeedDisplayObject displayObject = null;

            // First, try to get from inactive queue
            if (_inactiveObjects.Count > 0)
            {
                displayObject = _inactiveObjects.Dequeue();
            }
            else
            {
                AppLogger.Log(
                    $"No inactive objects left: {_inactiveObjects.Count}, active objects: {_activeObjects.Count}");

                // If no inactive objects, try to create more objects
                if (CreateMoreObjects())
                    // Try to get from the newly created inactive objects
                    if (_inactiveObjects.Count > 0)
                        displayObject = _inactiveObjects.Dequeue();
            }

            if (displayObject == null)
            {
                AppLogger.LogError("No available display objects in pool and cannot create more");
                return null;
            }

            return displayObject;
        }

        /// <summary>
        ///     Activate display objects for a data point (handles the full activation logic)
        /// </summary>
        public void ActivateDataPoint(FeedDataPoint dataPoint, float normalizedCreationTime, float maelstrom = 0f)
        {
            if (!IsInitialized)
            {
                AppLogger.LogError("FeedDisplayObjectPool not initialized");
                return;
            }

            // Check if we can activate more objects
            if (_activeObjects.Count >= maxActiveObjects)
            {
                AppLogger.LogWarning($"Max active objects limit reached: {maxActiveObjects}");
                return;
            }

            var displayObject = GetRecycledDisplayObject();
            if (displayObject == null)
            {
                AppLogger.LogError("No available display objects in pool");
                return;
            }

            // Let the display object handle its own initialization based on data point
            displayObject.InitializeFromDataPoint(dataPoint, screenSize, normalizedCreationTime, maelstrom);
            displayObject.SetEnabled(true);
            _activeObjects.Enqueue(displayObject);
        }

        /// <summary>
        ///     Recycle a display object back to the pool
        /// </summary>
        public void RecycleDisplayObject(FeedDisplayObject displayObject)
        {
            if (displayObject == null) return;

            // Reset the DisplayObject for reuse
            displayObject.SetEnabled(false);

            // Add to inactive queue for quick reuse
            _inactiveObjects.Enqueue(displayObject);
        }

        /// <summary>
        ///     Recycle old objects that exceed the display duration
        /// </summary>
        public void RecycleOldObjects(float normalizedCurrentTime, float normalizedDisplayDuration)
        {
            while (_activeObjects.Count > 0)
            {
                var obj = _activeObjects.Peek();
                var objectAge = normalizedCurrentTime - obj.normalizedCreationTime;

                // Handle loop transitions - if object age is negative or very large, 
                // it means we've looped and this object is from a previous loop
                if (objectAge < 0 || objectAge > 1.0f || objectAge >= normalizedDisplayDuration)
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
        ///     Update all active display objects
        /// </summary>
        public void UpdateActiveObjects(float maelstromValue = 0f)
        {
            foreach (var obj in _activeObjects)
                if (obj != null)
                    obj.Update(Time.deltaTime, maelstromValue);
        }

        /// <summary>
        ///     Get all active objects for external iteration
        /// </summary>
        public Queue<FeedDisplayObject> GetActiveObjects()
        {
            return _activeObjects;
        }

        /// <summary>
        ///     Get the count of active objects
        /// </summary>
        public int GetActiveObjectCount()
        {
            return _activeObjects.Count;
        }

        /// <summary>
        ///     Get the count of inactive objects
        /// </summary>
        public int GetInactiveObjectCount()
        {
            return _inactiveObjects.Count;
        }

        /// <summary>
        ///     Get the total pool size
        /// </summary>
        public int GetPoolSize()
        {
            return _activeObjects.Count + _inactiveObjects.Count;
        }

        /// <summary>
        ///     Clear all objects and reset the pool
        /// </summary>
        public void ClearPool()
        {
            try
            {
                // Clean up all active objects
                while (_activeObjects.Count > 0)
                {
                    var obj = _activeObjects.Dequeue();
                    if (obj != null) obj.SetEnabled(false);
                }

                // Clean up inactive objects
                while (_inactiveObjects.Count > 0)
                {
                    var obj = _inactiveObjects.Dequeue();
                    if (obj != null) obj.SetEnabled(false);
                }

                IsInitialized = false;
                pointPool = null; // Clear the reference

                AppLogger.Log("FeedDisplayObjectPool cleared and reset");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[FEED_DISPLAY_POOL] Error during cleanup: {ex.Message}");
            }
        }

        /// <summary>
        ///     Clear all active objects and move them back to inactive queue
        /// </summary>
        public void ClearAllActiveObjects()
        {
            while (_activeObjects.Count > 0)
            {
                var obj = _activeObjects.Dequeue();
                if (obj != null)
                {
                    obj.SetEnabled(false);
                    _inactiveObjects.Enqueue(obj);
                }
            }

            AppLogger.Log(
                $"Cleared all active objects. Active: {_activeObjects.Count}, Inactive: {_inactiveObjects.Count}");
        }
    }
}