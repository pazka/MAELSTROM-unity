using System.Collections.Generic;
using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Display object pool for managing GhostNetDisplayObject instances
    /// </summary>
    public class GhostNetDisplayObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")] [SerializeField]
        private int initialPoolSize = 5000; // Reduced from 50000

        [SerializeField] private int maxActiveObjects = 10000; // Reduced from 100000
        [SerializeField] private int maxPoolSize = 15000; // Reduced from 100000

        [SerializeField]
        private GhostNetPointPool ghostNetPointPool; // Reference to the point pool for creating new objects

        public Vector2 _centerPosition = new(0f, 0f); // Default center position

        private readonly Queue<GhostNetDisplayObject> _inactiveObjects = new();

        private List<GhostNetDisplayObject> _activeObjects = new();

        private Vector2 _screenSize;

        /// <summary>
        ///     Check if the pool is initialized
        /// </summary>
        public bool IsInitialized { get; private set; }


        /// <summary>
        ///     Initialize the display object pool
        /// </summary>
        public void Initialize(Vector2 screenSize, Vector2 centerOffset)
        {
            _screenSize = screenSize;

            _centerPosition = new Vector2(centerOffset[0], centerOffset[1]);
            transform.SetPositionAndRotation(new Vector3(_centerPosition.x, _centerPosition.y),
                Quaternion.identity);

            if (IsInitialized)
            {
                AppLogger.LogWarning("DisplayObjectPool already initialized");
                return;
            }

            if (ghostNetPointPool == null)
            {
                AppLogger.LogError("GhostNetPointPool is null");
                return;
            }

            _activeObjects = new List<GhostNetDisplayObject>(maxActiveObjects);

            for (var i = 0; i < initialPoolSize; i++)
            {
                var prefab = ghostNetPointPool.GetOne();
                if (prefab != null)
                {
                    var displayObject = new GhostNetDisplayObject(prefab);
                    displayObject.SetEnabled(false); // Start inactive
                    _inactiveObjects.Enqueue(displayObject);
                }
            }

            IsInitialized = true;
            AppLogger.Log($"DisplayObjectPool initialized with {_inactiveObjects.Count} objects");
        }

        /// <summary>
        ///     Create more objects for the pool when needed
        /// </summary>
        private bool CreateMoreObjects()
        {
            if (ghostNetPointPool == null)
            {
                AppLogger.LogError("GhostNetPointPool reference is null, cannot create more objects");
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
                var prefab = ghostNetPointPool.GetOne();
                if (prefab != null)
                {
                    var displayObject = new GhostNetDisplayObject(prefab);
                    displayObject.SetEnabled(false); // Start inactive
                    _inactiveObjects.Enqueue(displayObject);
                    createdCount++;
                }
                else
                {
                    AppLogger.LogWarning("GhostNetPointPool returned null, stopping object creation");
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
        public GhostNetDisplayObject GetRecycledDisplayObject()
        {
            if (!IsInitialized)
            {
                AppLogger.LogError("DisplayObjectPool not initialized");
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
                AppLogger.Log(
                    $"no inactive object left : {_inactiveObjects.Count}, active objects : {_activeObjects.Count}");

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
        public void ActivateDataPoint(GhostNetDataPoint dataPoint, float normalizedCreationtime, float currentMaelstrom)
        {
            if (!IsInitialized)
            {
                AppLogger.LogError("DisplayObjectPool not initialized");
                return;
            }

            // Check if we can activate more objects
            if (_activeObjects.Count >= maxActiveObjects)
            {
                AppLogger.LogWarning($"Max active objects limit reached: {maxActiveObjects}");
                return;
            }


            // Check if we've reached the limit
            if (_activeObjects.Count >= maxActiveObjects)
            {
                AppLogger.LogWarning($"Max active objects limit reached during activation: {maxActiveObjects}");
                return;
            }

            var displayObject = GetRecycledDisplayObject();
            if (displayObject == null)
            {
                AppLogger.LogError("No available display objects in pool");
                return;
            }

            // Let the display object handle its own initialization based on data point
            displayObject.InitializeFromDataPoint(dataPoint, _screenSize, normalizedCreationtime, currentMaelstrom,
                _centerPosition);
            displayObject.SetEnabled(true);
            _activeObjects.Add(displayObject);
        }

        /// <summary>
        ///     Recycle a display object back to the pool
        /// </summary>
        public void RecycleDisplayObject(GhostNetDisplayObject displayObject)
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
            var i = 0;
            while (i < _activeObjects.Count)
            {
                var obj = _activeObjects[i];
                if (obj == null)
                {
                    i++;
                    continue;
                }

                var objectAge = normalizedCurrentTime - obj.normalizedCreationTime;

                if (objectAge < 0 || objectAge > 1.0f || objectAge >= normalizedDisplayDuration)
                {
                    // O(1) swap-and-remove-last instead of O(n) RemoveAt
                    var lastIndex = _activeObjects.Count - 1;
                    _activeObjects[i] = _activeObjects[lastIndex];
                    _activeObjects.RemoveAt(lastIndex);
                    RecycleDisplayObject(obj);
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        ///     Update all active display objects
        /// </summary>
        public void UpdateActiveObjects(float maelstrom)
        {
            foreach (var obj in _activeObjects)
                if (obj != null)
                    obj.Update(Time.deltaTime, maelstrom);
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
            // Clean up all active objects
            for (var i = _activeObjects.Count - 1; i >= 0; i--)
            {
                var obj = _activeObjects[i];
                if (obj != null) obj.SetEnabled(false);
            }

            _activeObjects.Clear();

            // Clean up inactive objects
            while (_inactiveObjects.Count > 0)
            {
                var obj = _inactiveObjects.Dequeue();
                if (obj != null) obj.SetEnabled(false);
            }

            IsInitialized = false;
            ghostNetPointPool = null; // Clear the reference

            AppLogger.Log("DisplayObjectPool cleared and reset");
        }
    }
}