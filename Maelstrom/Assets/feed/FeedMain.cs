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

        [Header("Object Pool Settings")]
        [SerializeField] private FeedDisplayObjectPool displayObjectPool;

        [Header("Data Settings")]
        [SerializeField] private FeedDataLoader dataLoader;

        [SerializeField] private Config config;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private float debugUpdateInterval = 5.0f;
        [SerializeField] private FeedMaelstromManager maelstrom = new FeedMaelstromManager();

        // Data management
        private FeedDataPoint[] _data;
        private int _currentDataIndex = 0;
        private float _normalizedDisplayDuration; // One week in normalized data space
        private System.Random _random = new System.Random();
        private DateTime _currentDisplayedDate = DateTime.MinValue;

        [SerializeField] private PureDataConnector pureDataConnector;
        // Timing
        private float _currentTime = 0.0f;
        private float _lastDebugTime = 0.0f;
        private float loopDuration;

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "FeedScene")
            {
                gameObject.SetActive(false);
                return;
            }

            if (Display.displays.Length > 1)
                Display.displays[1].Activate();

            // Initialize UDP service for feed role

            loopDuration = config.Get("loopDuration", 600);

            if (dataLoader == null)
            {
                throw new System.Exception("DataLoader not found! Please assign a DataLoader component.");
            }

            if (displayObjectPool == null)
            {
                throw new System.Exception("FeedDisplayObjectPool not found! Please assign a FeedDisplayObjectPool component.");
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

            CommonMaelstrom.InitializeUdpService(3, pureDataConnector); // 3 = feed
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

            // Initialize Maelstrom manager with data bounds
            maelstrom.RegisterDataBounds(_data);

            // Initialize DisplayObject pool
            displayObjectPool.Initialize(screenSize);

            Debug.Log($"Initialized with {_data.Length} data points");
            Debug.Log($"One week in normalized data space: {_normalizedDisplayDuration:F6}");
            Debug.Log($"DisplayObject pool initialized with {displayObjectPool.GetPoolSize()} objects");
        }


        private void ProcessDataAndManageObjects()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;

            // First, recycle objects that are too old
            displayObjectPool.RecycleOldObjects(normalizedCurrentTime, _normalizedDisplayDuration);

            // Then, activate objects for new data points
            ActivateObjectsForNewData(normalizedCurrentTime);

            // Publish current feed maelstrom to network
            float localMaelstrom = maelstrom.GetCurrentMaelstrom();

            displayObjectPool.UpdateActiveObjects(localMaelstrom);
        }

        private void ActivateObjectsForNewData(float normalizedCurrentTime)
        {
            // Activate objects for new data points
            while (_currentDataIndex < _data.Length && displayObjectPool.GetActiveObjectCount() < displayObjectPool.MaxActiveObjects)
            {
                FeedDataPoint dataPoint = _data[_currentDataIndex];

                // Check if this data point should be displayed at current time
                if (dataPoint.normalizedDate <= normalizedCurrentTime)
                {
                    // Register data with maelstrom manager for daily retweet counting
                    maelstrom.RegisterData(dataPoint);

                    displayObjectPool.ActivateDataPoint(dataPoint, normalizedCurrentTime,maelstrom.GetCurrentMaelstrom());
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
                // Reset the time to start a new loop
                _currentTime = 0.0f;
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
                FeedDataPoint currentDataPoint = _data[_currentDataIndex];
                Debug.Log($"  Next data point: {currentDataPoint.date:yyyy-MM-dd HH:mm:ss}, " +
                         $"Retweets: {currentDataPoint.retweetCount}, Normalized: {currentDataPoint.normalizedDate:F6}");
            }

            // Log current date being displayed
            if (displayObjectPool.GetActiveObjectCount() > 0)
            {
                Debug.Log($"  CURRENT DATE DISPLAYED: {_currentDisplayedDate:yyyy-MM-dd HH:mm:ss}");
            }

            // Log maelstrom information
            Debug.Log($"  MAELSTROM: {maelstrom.GetCurrentMaelstrom():F3}, " +
                     $"Current Day Retweets: {maelstrom.GetCurrentRetweetCount()}, " +
                     $"Bounds: {maelstrom.GetMinRetweetCount()}-{maelstrom.GetMaxRetweetCount()}");
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
            return displayObjectPool.GetActiveObjectCount();
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
            return displayObjectPool.GetInactiveObjectCount();
        }

        public int GetDisplayObjectPoolSize()
        {
            return displayObjectPool.GetPoolSize();
        }

        public float GetCurrentMaelstrom()
        {
            return maelstrom.GetCurrentMaelstrom();
        }

        public int GetCurrentDayRetweetCount()
        {
            return maelstrom.GetCurrentRetweetCount();
        }

        private void OnDisable()
        {
            // Called when the object is disabled or when exiting play mode
            // This helps prevent crashes when stopping in the editor
            try
            {
                // Clear all active objects when disabling
                if (displayObjectPool != null)
                {
                    displayObjectPool.ClearAllActiveObjects();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FEED_MAIN] Error during OnDisable: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            try
            {
                // Clean up all objects using the display object pool
                if (displayObjectPool != null)
                {
                    displayObjectPool.ClearPool();
                }

                // Clean up UDP service
                CommonMaelstrom.Cleanup();

                Debug.Log($"[FEED_MAIN] Cleanup completed - Pool size: {displayObjectPool?.GetPoolSize() ?? 0}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FEED_MAIN] Error during cleanup: {ex.Message}");
            }
        }
    }
}
