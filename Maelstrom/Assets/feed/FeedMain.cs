using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Main script for the Feed visualization in Unity
    /// </summary>
    public class FeedMain : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private Vector2 screenSize = new Vector2(1920, 1080);
        [SerializeField] private float loopDuration = 600.0f; // seconds

        [Header("Object Pool Settings")]
        [SerializeField] private PointPool pointPool;
        [SerializeField] private int maxActiveObjects = 1000;

        [Header("Data Settings")]
        [SerializeField] private FeedDataLoader dataLoader;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private float debugUpdateInterval = 5.0f;

        // Data management
        private FeedDataPoint[] _data;
        private int _currentDataIndex = 0;
        private float _normalizedDisplayDuration; // One week in normalized data space
        private Queue<DisplayObject> _activeObjects = new Queue<DisplayObject>();
        private System.Random _random = new System.Random();
        private DateTime _currentDisplayedDate = DateTime.MinValue;

        // Timing
        private float _currentTime = 0.0f;
        private float _lastDebugTime = 0.0f;

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "FeedScene")
            {
                return;
            }

            if (dataLoader == null)
            {
                throw new System.Exception("DataLoader not found! Please assign a DataLoader component.");
            }

            if (pointPool == null)
            {
                throw new System.Exception("ObjectPool not found! Please assign an ObjectPool component.");
            }

            // Wait for data to load
            if (dataLoader.IsDataLoaded)
            {
                InitializeData();
            }
            else
            {
                Debug.Log("Waiting for data to load...");
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
            _normalizedDisplayDuration = dataLoader.GetNormalizedDuration(TimeSpan.FromDays(7));

            Debug.Log($"Initialized with {_data.Length} data points");
            Debug.Log($"One week in normalized data space: {_normalizedDisplayDuration:F6}");
        }

        private void ProcessDataAndManageObjects()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;

            // Add objects
            while (_currentDataIndex < _data.Length && _activeObjects.Count < maxActiveObjects)
            {
                FeedDataPoint dataPoint = _data[_currentDataIndex];

                // Check if this data point should be displayed at current time
                if (dataPoint.normalizedDate <= normalizedCurrentTime)
                {
                    DisplayObject displayObject = CreateRandomDisplayObject(dataPoint);
                    if (displayObject == null)
                    {
                        throw new System.Exception("Failed to create display object");
                    }

                    _activeObjects.Enqueue(displayObject);

                    // Update current displayed date
                    _currentDisplayedDate = dataPoint.date;


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

            // Remove objects
            while (_activeObjects.Count > 0)
            {
                DisplayObject obj = _activeObjects.Peek();
                float objectAge = normalizedCurrentTime - obj.creationTime;
                if (objectAge >= _normalizedDisplayDuration)
                {
                    _activeObjects.Dequeue();
                    ReturnDisplayObject(obj);
                }
                else
                {
                    // Since data is ordered, if this object is not old enough, 
                    // none of the remaining objects will be either
                    break;
                }
            }

            //uupdate active objects
            foreach (var obj in _activeObjects)
            {
                obj.Update(Time.deltaTime);
            }
        }

        private DisplayObject CreateRandomDisplayObject(FeedDataPoint dataPoint)
        {
            // Get a GameObject from the pool
            GameObject prefab = pointPool.GetOne();
            if (prefab == null)
            {
                Debug.LogWarning("Object pool exhausted, cannot create new DisplayObject");
                return null;
            }

            // Get or create DisplayObject component
            DisplayObject displayObject = new DisplayObject(prefab);

            // Activate the DisplayObject
            displayObject.SetEnabled(true);

            // Random position on screen
            Vector2 position = new Vector2(
                UnityEngine.Random.Range(0, screenSize.x),
                UnityEngine.Random.Range(0, screenSize.y)
            );

            // Velocity based on retweet count (normalized)
            float velocityScale = 150 - dataPoint.normalizedRetweetCount * 120; // 20 to 100 pixels per second
            Vector2 velocity = new Vector2(
                (UnityEngine.Random.value - 0.5f) * velocityScale,
                (UnityEngine.Random.value - 0.5f) * velocityScale
            );

            // Size based on retweet count (normalized)
            float sizeScale = 25 + dataPoint.normalizedRetweetCount * 150; // 25 to 175 pixels
            Vector2 pixelSize = new Vector2(sizeScale, sizeScale);

            // Initialize the DisplayObject
            displayObject.Initialize(position, velocity, screenSize, pixelSize);
            displayObject.creationTime = _currentTime / loopDuration; // Store normalized creation time

            return displayObject;
        }

        private void ReturnDisplayObject(DisplayObject displayObject)
        {
            if (displayObject != null && displayObject.GetGameObject() != null)
            {
                // Reset the DisplayObject for reuse
                displayObject.Reset();

                // Deactivate the DisplayObject
                displayObject.SetEnabled(false);

                // Return the GameObject to the pool
                pointPool.ReturnObject(displayObject.GetGameObject());
            }
        }

        private void LogDebugInfo()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;
            Debug.Log($"Time: {_currentTime:F1}s, Normalized: {normalizedCurrentTime:F6}, " +
                     $"Active Objects: {_activeObjects.Count}, Data Index: {_currentDataIndex}/{_data.Length}");

            if (_currentDataIndex < _data.Length)
            {
                FeedDataPoint currentDataPoint = _data[_currentDataIndex];
                Debug.Log($"  Next data point: {currentDataPoint.date:yyyy-MM-dd HH:mm:ss}, " +
                         $"Retweets: {currentDataPoint.retweetCount}, Normalized: {currentDataPoint.normalizedDate:F6}");
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

        private void OnDestroy()
        {
            // Clean up all active objects
            while (_activeObjects.Count > 0)
            {
                DisplayObject obj = _activeObjects.Dequeue();
                if (obj != null)
                {
                    ReturnDisplayObject(obj);
                }
            }
        }
    }
}
