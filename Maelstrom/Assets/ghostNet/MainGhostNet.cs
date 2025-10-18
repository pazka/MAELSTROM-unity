
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Main script for the GhostNet visualization in Unity
    /// </summary>
    public class MainGhostNet : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private Vector2 screenSize = new Vector2(1920, 1080);
        [SerializeField] private float loopDuration = 600.0f; // seconds

        [Header("Object Pool Settings")]
        [SerializeField] private GhostNetPointPool ghostNetPool;
        [SerializeField] private int maxActiveObjects = 1000;

        [Header("Data Settings")]
        [SerializeField] private GhostNetDataLoader dataLoader;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private float debugUpdateInterval = 5.0f;

        // Data management
        private GhostNetDataPoint[] _data;
        private int _currentDataIndex = 0;
        private float _normalizedDisplayDuration; // One day in normalized data space
        private Queue<GhostNetDisplayObject> _activeObjects = new Queue<GhostNetDisplayObject>();
        private Queue<GhostNetDisplayObject> _inactiveObjects = new Queue<GhostNetDisplayObject>();
        private System.Random _random = new System.Random();
        private DateTime _currentDisplayedDate = DateTime.MinValue;

        // Object recycling
        private List<GhostNetDisplayObject> _displayObjectPool = new List<GhostNetDisplayObject>();
        private int _nextPoolIndex = 0;

        // Timing
        private float _currentTime = 0.0f;
        private float _lastDebugTime = 0.0f;
        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "GhostNetsScene")
            {
                return;
            }

            if (dataLoader == null)
            {
                throw new System.Exception("GhostNetDataLoader not found! Please assign a GhostNetDataLoader component.");
            }

            if (ghostNetPool == null)
            {
                throw new System.Exception("GhostNetPointPool not found! Please assign a GhostNetPointPool component.");
            }

            // Wait for data to load
            if (dataLoader.IsDataLoaded)
            {
                InitializeData();
            }
            else
            {
                Debug.Log("Waiting for ghostNet data to load...");
            }
        }

        private void Update()
        {
            if (!dataLoader.IsDataLoaded) return;

            _currentTime += Time.deltaTime;

            // Process data and manage display objects
            ProcessDataAndManageObjects();

            // Debug output
            if (showDebugInfo && _currentTime - _lastDebugTime >= debugUpdateInterval)
            {
                LogDebugInfo();
                _lastDebugTime = _currentTime;
            }
        }

        private void InitializeData()
        {
            _data = dataLoader.Data;
            _normalizedDisplayDuration = dataLoader.GetNormalizedDuration(TimeSpan.FromDays(1));

            // Pre-create DisplayObject pool
            InitializeDisplayObjectPool();

            Debug.Log($"Initialized ghostNet with {_data.Length} data points");
            Debug.Log($"One day in normalized data space: {_normalizedDisplayDuration:F6}");
            Debug.Log($"DisplayObject pool initialized with {_displayObjectPool.Count} objects");
        }

        private void InitializeDisplayObjectPool()
        {
            // Create a pool of DisplayObjects based on maxActiveObjects
            int poolSize = Math.Max(maxActiveObjects, 1000); // At least 1000 objects

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

            Debug.Log($"Created {_displayObjectPool.Count} GhostNetDisplayObjects in pool");
        }

        private void ProcessDataAndManageObjects()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;

            // First, recycle objects that are too old
            RecycleOldObjects(normalizedCurrentTime);

            // Then, activate objects for new data points
            ActivateObjectsForNewData(normalizedCurrentTime);

            // Update all active objects
            UpdateActiveObjects();
        }

        private void RecycleOldObjects(float normalizedCurrentTime)
        {
            // Move old objects from active to inactive queue
            while (_activeObjects.Count > 0)
            {
                GhostNetDisplayObject obj = _activeObjects.Peek();
                float objectAge = normalizedCurrentTime - obj.creationTime;

                if (objectAge >= _normalizedDisplayDuration)
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

        private void ActivateObjectsForNewData(float normalizedCurrentTime)
        {
            // Activate objects for new data points
            while (_currentDataIndex < _data.Length && _activeObjects.Count < maxActiveObjects)
            {
                GhostNetDataPoint dataPoint = _data[_currentDataIndex];

                // Check if this data point should be displayed at current time
                if (dataPoint.normalizedDate <= normalizedCurrentTime)
                {
                    GhostNetDisplayObject displayObject = GetRecycledDisplayObject(dataPoint);
                    if (displayObject != null)
                    {
                        _activeObjects.Enqueue(displayObject);
                        _currentDisplayedDate = dataPoint.date;
                    }
                    _currentDataIndex++;
                }
                else
                {
                    // Data point is in the future, wait
                    break;
                }
            }

            // If we've reached the end of data, loop back to start
            if (_currentDataIndex >= _data.Length)
            {
                _currentDataIndex = 0;
            }
        }

        private void UpdateActiveObjects()
        {
            // Update all active objects
            foreach (var obj in _activeObjects)
            {
                if (obj != null)
                {
                    obj.Update(Time.deltaTime);
                }
            }
        }

        private GhostNetDisplayObject GetRecycledDisplayObject(GhostNetDataPoint dataPoint)
        {
            // Try to get an inactive object from the pool
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

            if (displayObject == null)
            {
                Debug.LogWarning("No available GhostNetDisplayObjects in pool");
                return null;
            }

            // Configure the recycled object
            ConfigureDisplayObject(displayObject, dataPoint);
            return displayObject;
        }

        private void ConfigureDisplayObject(GhostNetDisplayObject displayObject, GhostNetDataPoint dataPoint)
        {
            // Activate the DisplayObject
            displayObject.SetEnabled(true);

            // Random position on screen
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(0, screenSize.x),
                UnityEngine.Random.Range(0, screenSize.y)
            );

            // Velocity based on tweet count (normalized)
            float velocityScale = 50 - dataPoint.normalizedNbTweets * 30; // 20 to 50 pixels per second
            Vector2 velocity = new Vector2(
                (UnityEngine.Random.value - 0.5f) * velocityScale,
                (UnityEngine.Random.value - 0.5f) * velocityScale
            );

            // Initialize the DisplayObject
            displayObject.Initialize(position, velocity, screenSize, dataPoint);
            displayObject.creationTime = _currentTime / loopDuration; // Store normalized creation time
        }

        private void RecycleDisplayObject(GhostNetDisplayObject displayObject)
        {
            if (displayObject != null)
            {
                // Reset the DisplayObject for reuse
                displayObject.Reset();

                // Deactivate the DisplayObject
                displayObject.SetEnabled(false);

                // Add to inactive queue for quick reuse
                _inactiveObjects.Enqueue(displayObject);
            }
        }

        private void ReturnGhostNetDisplayObject(GhostNetDisplayObject displayObject)
        {
            if (displayObject != null && displayObject.GetGameObject() != null)
            {
                // Reset the DisplayObject for reuse
                displayObject.Reset();

                // Deactivate the DisplayObject
                displayObject.SetEnabled(false);

                // Return the GameObject to the pool
                ghostNetPool.ReturnObject(displayObject.GetGameObject());
            }
        }

        private void LogDebugInfo()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;
            Debug.Log($"Time: {_currentTime:F1}s, Normalized: {normalizedCurrentTime:F6}, " +
                     $"Active Objects: {_activeObjects.Count}, Data Index: {_currentDataIndex}/{_data.Length}");

            // Log recycling stats
            Debug.Log($"Recycling Stats - Active: {_activeObjects.Count}, " +
                     $"Inactive Queue: {_inactiveObjects.Count}, " +
                     $"Pool Size: {_displayObjectPool.Count}");

            if (_currentDataIndex < _data.Length)
            {
                GhostNetDataPoint currentDataPoint = _data[_currentDataIndex];
                Debug.Log($"  Next data point: {currentDataPoint.date:yyyy-MM-dd HH:mm:ss}, " +
                         $"Tweets: {currentDataPoint.nb_tweets}, Followers: {currentDataPoint.followers_count}, Normalized: {currentDataPoint.normalizedDate:F6}");
            }

            // Log current date being displayed
            if (_activeObjects.Count > 0)
            {
                Debug.Log($"  CURRENT DATE DISPLAYED: {_currentDisplayedDate:yyyy-MM-dd HH:mm:ss}");
            }
        }

        // Public methods for external control
        public void SetScreenSize(Vector2 newScreenSize)
        {
            screenSize = newScreenSize;
        }

        public void SetLoopDuration(float newLoopDuration)
        {
            loopDuration = newLoopDuration;
        }

        public int GetActiveObjectCount()
        {
            return _activeObjects.Count;
        }

        public float GetCurrentTime()
        {
            return _currentTime;
        }

        public DateTime GetCurrentDisplayedDate()
        {
            return _currentDisplayedDate;
        }

        public int GetInactiveObjectCount()
        {
            return _inactiveObjects.Count;
        }

        public int GetDisplayObjectPoolSize()
        {
            return _displayObjectPool.Count;
        }

        private void OnDestroy()
        {
            // Clean up all active objects
            while (_activeObjects.Count > 0)
            {
                GhostNetDisplayObject obj = _activeObjects.Dequeue();
                if (obj != null)
                {
                    ReturnGhostNetDisplayObject(obj);
                }
            }

            // Clean up inactive objects
            while (_inactiveObjects.Count > 0)
            {
                GhostNetDisplayObject obj = _inactiveObjects.Dequeue();
                if (obj != null)
                {
                    ReturnGhostNetDisplayObject(obj);
                }
            }

            // Clean up display object pool
            foreach (var obj in _displayObjectPool)
            {
                if (obj != null)
                {
                    ReturnGhostNetDisplayObject(obj);
                }
            }
            _displayObjectPool.Clear();

            Debug.Log($"[GHOSTNET_MAIN] Cleanup completed - Pool size: {_displayObjectPool.Count}");
        }

    }
}
