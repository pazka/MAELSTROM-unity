
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [SerializeField] private GhostNetDisplayObjectPool displayObjectPool;
        [SerializeField] private Config Config;

        [Header("Data Settings")]
        [SerializeField] private GhostNetDataLoader dataLoader;


        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private float debugUpdateInterval = 5.0f;
        [SerializeField] private GNMaelstromManager maelstrom = new GNMaelstromManager();

        // Data management
        private GhostNetDataPoint[] _data;
        private int _currentDataIndex = 0;
        private float _normalizedDisplayDuration; // One day in normalized data space
        private System.Random _random = new System.Random();
        private DateTime _currentDisplayedDate = DateTime.MinValue;
        private TimeSpan DATA_TTL = TimeSpan.FromDays(3);
        private int loopDuration;

        // Day progression tracking for smooth spawning
        private DateTime _currentDay = DateTime.MinValue;
        private List<GhostNetDataPoint> _currentDayData = new List<GhostNetDataPoint>();
        private int _currentDayDataIndex = 0;
        private float _dayProgress = 0f; // 0 to 1, progress through current day

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

            // Wait for data to load
            if (dataLoader.IsDataLoaded)
            {
                InitializeData();
            }
            else
            {
                Debug.Log("Waiting for ghostNet data to load...");
            }

            loopDuration = Config.Get<int>("loopDuration", 600);
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
            maelstrom.RegisterDataBounds(dataLoader.Data);
            _data = dataLoader.Data;
            _normalizedDisplayDuration = dataLoader.GetNormalizedDuration(DATA_TTL);

            // Initialize DisplayObject pool
            displayObjectPool.Initialize(screenSize);

            Debug.Log($"Initialized ghostNet with {_data.Length} data points");
            Debug.Log($"One day in normalized data space: {_normalizedDisplayDuration:F6}");
            Debug.Log($"DisplayObject pool initialized with {displayObjectPool.GetPoolSize()} objects");
        }


        private void ProcessDataAndManageObjects()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;

            displayObjectPool.RecycleOldObjects(normalizedCurrentTime, _normalizedDisplayDuration);

            ProcessDayProgression(normalizedCurrentTime);

            displayObjectPool.UpdateActiveObjects(maelstrom.GetCurrentMaelstrom());
        }



        private void ProcessDayProgression(float normalizedCurrentTime)
        {
            // Calculate which day we should be processing
            DateTime targetDay = GetDayFromNormalizedTime(normalizedCurrentTime);

            // If we've moved to a new day, load that day's data
            if (targetDay != _currentDay)
            {
                LoadDayData(targetDay);
                _currentDay = targetDay;
                _currentDayDataIndex = 0;
                _dayProgress = 0f;
            }

            // Calculate progress through the current day (0 to 1)
            _dayProgress = GetDayProgress(normalizedCurrentTime);

            // Spawn data points progressively throughout the day
            SpawnDataPointsForCurrentDay(normalizedCurrentTime);
        }

        private DateTime GetDayFromNormalizedTime(float normalizedTime)
        {
            if (!dataLoader.IsDataLoaded || _data.Length == 0) return DateTime.MinValue;

            // Map normalized time to actual date range
            TimeSpan totalSpan = dataLoader.DataBounds.Max.date - dataLoader.DataBounds.Min.date;
            DateTime targetDate = dataLoader.DataBounds.Min.date.AddTicks((long)(totalSpan.Ticks * normalizedTime));
            return targetDate.Date;
        }

        private float GetDayProgress(float normalizedTime)
        {
            if (!dataLoader.IsDataLoaded || _data.Length == 0) return 0f;

            // Calculate progress within the current day
            TimeSpan totalSpan = dataLoader.DataBounds.Max.date - dataLoader.DataBounds.Min.date;
            DateTime targetDate = dataLoader.DataBounds.Min.date.AddTicks((long)(totalSpan.Ticks * normalizedTime));

            // Get the start and end of the current day
            DateTime dayStart = targetDate.Date;
            DateTime dayEnd = dayStart.AddDays(1);

            // Calculate progress within the day (0 to 1)
            if (targetDate < dayStart) return 0f;
            if (targetDate >= dayEnd) return 1f;

            return (float)((targetDate - dayStart).TotalHours / 24.0);
        }

        private void LoadDayData(DateTime targetDay)
        {
            _currentDayData.Clear();

            // Find all data points for the target day
            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i].date.Date == targetDay)
                {
                    _currentDayData.Add(_data[i]);
                }
            }

            // Sort by time of day for proper progression
            _currentDayData.Sort((a, b) => a.date.TimeOfDay.CompareTo(b.date.TimeOfDay));

            Debug.Log($"Loaded {_currentDayData.Count} data points for day {targetDay:yyyy-MM-dd}");
        }

        private void SpawnDataPointsForCurrentDay(float normalizedCurrentTime)
        {
            if (_currentDayData.Count == 0) return;

            // Calculate how many data points should be spawned based on day progress
            int totalPointsForDay = _currentDayData.Count;
            int targetSpawnedCount = Mathf.RoundToInt(_dayProgress * totalPointsForDay);

            // Spawn data points up to the target count
            while (_currentDayDataIndex < targetSpawnedCount && _currentDayDataIndex < _currentDayData.Count)
            {
                GhostNetDataPoint dataPoint = _currentDayData[_currentDayDataIndex];
                displayObjectPool.ActivateDataPoint(dataPoint, normalizedCurrentTime);
                _currentDisplayedDate = dataPoint.date;
                _currentDayDataIndex++;
            }
        }


        private void LogDebugInfo()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;
            Debug.Log($"Time: {_currentTime:F1}s, Normalized: {normalizedCurrentTime:F6}, " +
                     $"Active Objects: {displayObjectPool.GetActiveObjectCount()}, " +
                     $"Current Day: {_currentDay:yyyy-MM-dd}, Day Progress: {_dayProgress:F2}");

            // Log recycling stats
            Debug.Log($"Recycling Stats - Active: {displayObjectPool.GetActiveObjectCount()}, " +
                     $"Inactive Queue: {displayObjectPool.GetInactiveObjectCount()}, " +
                     $"Pool Size: {displayObjectPool.GetPoolSize()}");

            // Log current day data progression
            if (_currentDayData.Count > 0)
            {
                Debug.Log($"  Current Day Data: {_currentDayDataIndex}/{_currentDayData.Count} spawned, " +
                         $"Target: {Mathf.RoundToInt(_dayProgress * _currentDayData.Count)}");

                if (_currentDayDataIndex < _currentDayData.Count)
                {
                    GhostNetDataPoint nextDataPoint = _currentDayData[_currentDayDataIndex];
                    Debug.Log($"  Next data point: {nextDataPoint.date:yyyy-MM-dd HH:mm:ss}, " +
                             $"Tweets: {nextDataPoint.nb_tweets}, Followers: {nextDataPoint.followers_count}");
                }
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

