
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
// GhostNetDisplayObjectPool is a separate class for managing object pooling

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
        [SerializeField] private GhostNetDisplayObjectPool displayObjectPool;

        [Header("Data Settings")]
        [SerializeField] private GhostNetDataLoader dataLoader;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private float debugUpdateInterval = 5.0f;

        // Data management
        private GhostNetDataPoint[] _data;
        private int _currentDataIndex = 0;
        private float _normalizedDisplayDuration; // One day in normalized data space
        private System.Random _random = new System.Random();
        private DateTime _currentDisplayedDate = DateTime.MinValue;

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
            _normalizedDisplayDuration = dataLoader.GetNormalizedDuration(TimeSpan.FromDays(2));

            // Initialize DisplayObject pool
            displayObjectPool.Initialize(ghostNetPool, screenSize);

            Debug.Log($"Initialized ghostNet with {_data.Length} data points");
            Debug.Log($"One day in normalized data space: {_normalizedDisplayDuration:F6}");
            Debug.Log($"DisplayObject pool initialized with {displayObjectPool.GetPoolSize()} objects");
        }


        private void ProcessDataAndManageObjects()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;

            // First, recycle objects that are too old
            displayObjectPool.RecycleOldObjects(normalizedCurrentTime, _normalizedDisplayDuration);

            // Then, activate objects for new data points
            ActivateObjectsForNewData(normalizedCurrentTime);

            // Update all active objects
            displayObjectPool.UpdateActiveObjects();
        }



        private void ActivateObjectsForNewData(float normalizedCurrentTime)
        {
            // Activate objects for new data points
            while (_currentDataIndex < _data.Length)
            {
                GhostNetDataPoint dataPoint = _data[_currentDataIndex];

                // Check if this data point should be displayed at current time
                if (dataPoint.normalizedDate <= normalizedCurrentTime)
                {
                    // Let the pool handle all activation logic
                    displayObjectPool.ActivateDataPoint(dataPoint, normalizedCurrentTime);
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
        }


        private void LogDebugInfo()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;
            Debug.Log($"Time: {_currentTime:F1}s, Normalized: {normalizedCurrentTime:F6}, " +
                     $"Active Objects: {displayObjectPool.GetActiveObjectCount()}, Data Index: {_currentDataIndex}/{_data.Length}");

            // Log recycling stats
            Debug.Log($"Recycling Stats - Active: {displayObjectPool.GetActiveObjectCount()}, " +
                     $"Inactive Queue: {displayObjectPool.GetInactiveObjectCount()}, " +
                     $"Pool Size: {displayObjectPool.GetPoolSize()}");

            if (_currentDataIndex < _data.Length)
            {
                GhostNetDataPoint currentDataPoint = _data[_currentDataIndex];
                Debug.Log($"  Next data point: {currentDataPoint.date:yyyy-MM-dd HH:mm:ss}, " +
                         $"Tweets: {currentDataPoint.nb_tweets}, Followers: {currentDataPoint.followers_count}, Normalized: {currentDataPoint.normalizedDate:F6}");
            }

            // Log current date being displayed
            if (displayObjectPool.GetActiveObjectCount() > 0)
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
            // Clean up all objects using the static pool
            displayObjectPool.ClearPool();

            Debug.Log($"[GHOSTNET_MAIN] Cleanup completed - Pool size: {displayObjectPool.GetPoolSize()}");
        }

    }
}
