using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Main script for the GhostNet visualization in Unity
    /// </summary>
    public class MainGhostNet : MonoBehaviour
    {
        [Header("Display Settings")] [SerializeField]
        private Vector2 screenSize = new(1920, 1080);

        [SerializeField] private GhostNetDisplayObjectPool displayObjectPool;
        [SerializeField] private ParticleSystem particles;

        [Header("Data Settings")] [SerializeField]
        private GhostNetDataLoader dataLoader;


        [Header("Debug")] [SerializeField] private bool showDebugInfo = true;

        [SerializeField] private float debugUpdateInterval = 5.0f;

        // Frame rate limiting for spawning
        [Header("Performance Settings")] [SerializeField]
        private int maxObjectsPerFrame = 50; // Limit objects spawned per frame

        [SerializeField] private int maxObjectsPerSecond = 1000; // Limit objects spawned per second

        [SerializeField] private PureDataConnector pureDataConnector;
        private readonly List<GhostNetDataPoint> _currentDayData = new();
        private readonly TimeSpan DATA_TTL = TimeSpan.FromDays(3);
        private readonly GNMaelstromManager maelstrom = new();

        // Day progression tracking for smooth spawning
        private DateTime _currentDay = DateTime.MinValue;
        private int _currentDayDataIndex;

        private DateTime _currentDisplayedDate = DateTime.MinValue;

        // Timing
        private float _currentTime;

        // Data management
        private GhostNetDataPoint[] _data;
        private int _dataIndex; // Track current position in data for looping
        private float _dayProgress; // 0 to 1, progress through current day
        private float _lastDebugTime;
        private float _lastSecondReset;
        private float _normalizedDisplayDuration; // One day in normalized data space
        private int _objectsSpawnedThisFrame;
        private int _objectsSpawnedThisSecond;
        private Random _random = new();
        private int loopDuration;

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "GhostNetsScene")
            {
                gameObject.SetActive(false);
                return;
            }


            if (dataLoader == null)
                throw new Exception("GhostNetDataLoader not found! Please assign a GhostNetDataLoader component.");

            // Wait for data to load
            if (dataLoader.IsDataLoaded)
                InitializeData();
            else
                Debug.Log("Waiting for ghostNet data to load...");

            loopDuration = Config.Get("loopDuration", 1200);
            NetworkManager.Instance.Initialize(5002, new[] { 5000, 5001, 5003 });
            CommonMaelstrom.InitializeWithPureData(CommonMaelstrom.RoleId.GhostNet, pureDataConnector);
        }

        private void Update()
        {
            if (!dataLoader.IsDataLoaded) return;

            _currentTime += Time.deltaTime;

            // Reset frame counters
            _objectsSpawnedThisFrame = 0;

            // Reset per-second counter
            if (_currentTime - _lastSecondReset >= 1.0f)
            {
                _objectsSpawnedThisSecond = 0;
                _lastSecondReset = _currentTime;
            }

            // Process data and manage display objects
            ProcessDataAndManageObjects();

            // Debug output
            if (showDebugInfo && _currentTime - _lastDebugTime >= debugUpdateInterval)
            {
                LogDebugInfo();
                _lastDebugTime = _currentTime;
            }
        }

        private void OnDestroy()
        {
            // Clean up all objects using the static pool
            displayObjectPool.ClearPool();

            // Clean up UDP service
            CommonMaelstrom.Cleanup();

            Debug.Log($"[GHOSTNET_MAIN] Cleanup completed - Pool size: {displayObjectPool.GetPoolSize()}");
        }

        private void InitializeData()
        {
            maelstrom.RegisterDataBounds(dataLoader.Data);
            _data = dataLoader.Data;
            _normalizedDisplayDuration = dataLoader.GetNormalizedDuration(DATA_TTL);

            // Simulate and dump maelstrom data to CSV
            maelstrom.SimulateAndDumpDailyMaelstrom(_data);

            // Initialize DisplayObject pool
            displayObjectPool.Initialize(screenSize);

            Debug.Log($"Initialized ghostNet with {_data.Length} data points");
            Debug.Log($"One day in normalized data space: {_normalizedDisplayDuration:F6}");
            Debug.Log($"DisplayObject pool initialized with {displayObjectPool.GetPoolSize()} objects");
        }


        private void ProcessDataAndManageObjects()
        {
            // Use modulo to create looping behavior
            var normalizedCurrentTime = _currentTime % loopDuration / loopDuration;

            displayObjectPool.RecycleOldObjects(normalizedCurrentTime, _normalizedDisplayDuration);

            ProcessDayProgression(normalizedCurrentTime);

            // Publish current ghostNet maelstrom to network
            var localMaelstrom = maelstrom.GetCurrentMaelstrom();
            displayObjectPool.UpdateActiveObjects(localMaelstrom);
        }


        private void ProcessDayProgression(float normalizedCurrentTime)
        {
            // Calculate which day we should be processing
            var targetDay = GetDayFromNormalizedTime(normalizedCurrentTime);

            // Check if we've looped back to the beginning (when normalized time resets to 0)
            var hasLooped = normalizedCurrentTime < 0.01f && _currentTime > loopDuration;

            // If we've moved to a new day or looped back, load that day's data
            if (targetDay != _currentDay || hasLooped)
            {
                // Clear all active objects when looping to prevent accumulation
                if (hasLooped)
                {
                    Debug.Log(
                        $"LOOP DETECTED: Clearing {displayObjectPool.GetActiveObjectCount()} active objects and resetting data index");
                    displayObjectPool.ClearAllActiveObjects();

                    // Clear particle system when looping
                    if (particles != null)
                    {
                        particles.Stop();
                        particles.Clear();
                    }

                    _dataIndex = 0;
                }

                LoadDayData(targetDay);
                _currentDay = targetDay;
                _currentDayDataIndex = 0;
                _dayProgress = 0f;

                // Clear particle system when changing days
                if (particles != null) particles.Stop();
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
            var totalSpan = dataLoader.DataBounds.Max.date - dataLoader.DataBounds.Min.date;
            var targetDate = dataLoader.DataBounds.Min.date.AddTicks((long)(totalSpan.Ticks * normalizedTime));
            return targetDate.Date;
        }

        private float GetDayProgress(float normalizedTime)
        {
            if (!dataLoader.IsDataLoaded || _data.Length == 0) return 0f;

            // Calculate progress within the current day
            var totalSpan = dataLoader.DataBounds.Max.date - dataLoader.DataBounds.Min.date;
            var targetDate = dataLoader.DataBounds.Min.date.AddTicks((long)(totalSpan.Ticks * normalizedTime));

            // Get the start and end of the current day
            var dayStart = targetDate.Date;
            var dayEnd = dayStart.AddDays(1);

            // Calculate progress within the day (0 to 1)
            if (targetDate < dayStart) return 0f;
            if (targetDate >= dayEnd) return 1f;

            return (float)((targetDate - dayStart).TotalHours / 24.0);
        }

        private void LoadDayData(DateTime targetDay)
        {
            _currentDayData.Clear();

            // Find all data points for the target day, starting from dataIndex for efficiency
            for (var i = _dataIndex; i < _data.Length; i++)
                if (_data[i].date.Date == targetDay)
                {
                    maelstrom.RegisterData(_data[i]);
                    _currentDayData.Add(_data[i]);
                }
                else if (_data[i].date.Date > targetDay)
                {
                    // Since data is sorted chronologically, we can break early
                    break;
                }

            // If we didn't find data starting from dataIndex, search from beginning
            if (_currentDayData.Count == 0)
                for (var i = 0; i < _dataIndex; i++)
                    if (_data[i].date.Date == targetDay)
                    {
                        maelstrom.RegisterData(_data[i]);
                        _currentDayData.Add(_data[i]);
                    }

            // Sort by time of day for proper progression
            _currentDayData.Sort((a, b) => a.date.TimeOfDay.CompareTo(b.date.TimeOfDay));

            //  Debug.Log($"Loaded {_currentDayData.Count} data points for day {targetDay:yyyy-MM-dd}");
        }

        private void SpawnDataPointsForCurrentDay(float normalizedCurrentTime)
        {
            if (_currentDayData.Count == 0) return;

            // Calculate how many data points should be spawned based on day progress
            var totalPointsForDay = _currentDayData.Count;
            var targetSpawnedCount = Mathf.RoundToInt(_dayProgress * totalPointsForDay);

            // Get current maelstrom for radius calculation
            var currentMaelstrom = maelstrom.GetCurrentMaelstrom();

            // Spawn data points up to the target count with frame rate limiting
            while (_currentDayDataIndex < targetSpawnedCount &&
                   _currentDayDataIndex < _currentDayData.Count &&
                   _objectsSpawnedThisFrame < maxObjectsPerFrame &&
                   _objectsSpawnedThisSecond < maxObjectsPerSecond)
            {
                var dataPoint = _currentDayData[_currentDayDataIndex];

                // Handle ##OTHERS## datapoints with particle system
                if (dataPoint.screen_name == "##OTHERS##")
                {
                    GhostNetParticleManager.ConfigureParticleSystem(particles, dataPoint.nb_accounts_others,
                        currentMaelstrom);
                    particles.Play();
                }
                else
                {
                    // Regular datapoints use display objects
                    displayObjectPool.ActivateDataPoint(dataPoint, normalizedCurrentTime, currentMaelstrom);
                }

                _currentDisplayedDate = dataPoint.date;
                _currentDayDataIndex++;
                _objectsSpawnedThisFrame++;
                _objectsSpawnedThisSecond++;

                // Update global data index to track overall progress
                _dataIndex = Mathf.Max(_dataIndex, GetDataIndexForDate(dataPoint.date));
            }

            // Log warnings if we hit performance limits
            if (_objectsSpawnedThisFrame >= maxObjectsPerFrame)
                Debug.LogWarning($"Performance: Hit frame limit ({maxObjectsPerFrame} objects/frame). " +
                                 $"Remaining data points: {targetSpawnedCount - _currentDayDataIndex}");

            if (_objectsSpawnedThisSecond >= maxObjectsPerSecond)
                Debug.LogWarning($"Performance: Hit second limit ({maxObjectsPerSecond} objects/second). " +
                                 $"Remaining data points: {targetSpawnedCount - _currentDayDataIndex}");
        }

        private int GetDataIndexForDate(DateTime date)
        {
            // Find the index of the data point with the given date
            for (var i = 0; i < _data.Length; i++)
                if (_data[i].date == date)
                    return i;

            return 0; // Default to 0 if not found
        }

        private void LogDebugInfo()
        {
            var normalizedCurrentTime = _currentTime % loopDuration / loopDuration;
            Debug.Log($"Time: {_currentTime:F1}s, Normalized: {normalizedCurrentTime:F6}, " +
                      $"Active Objects: {displayObjectPool.GetActiveObjectCount()}, " +
                      $"Current Day: {_currentDay:yyyy-MM-dd}, Day Progress: {_dayProgress:F2}, " +
                      $"Data Index: {_dataIndex}/{_data.Length}");

            // Log recycling stats
            Debug.Log($"Recycling Stats - Active: {displayObjectPool.GetActiveObjectCount()}, " +
                      $"Inactive Queue: {displayObjectPool.GetInactiveObjectCount()}, " +
                      $"Pool Size: {displayObjectPool.GetPoolSize()}");

            // Log performance metrics
            Debug.Log($"Performance - Objects/Frame: {_objectsSpawnedThisFrame}/{maxObjectsPerFrame}, " +
                      $"Objects/Second: {_objectsSpawnedThisSecond}/{maxObjectsPerSecond}");

            // Log current day data progression
            if (_currentDayData.Count > 0)
            {
                Debug.Log($"  Current Day Data: {_currentDayDataIndex}/{_currentDayData.Count} spawned, " +
                          $"Target: {Mathf.RoundToInt(_dayProgress * _currentDayData.Count)}");

                if (_currentDayDataIndex < _currentDayData.Count)
                {
                    var nextDataPoint = _currentDayData[_currentDayDataIndex];
                    Debug.Log($"  Next data point: {nextDataPoint.date:yyyy-MM-dd HH:mm:ss}, " +
                              $"Tweets: {nextDataPoint.nb_tweets}, Followers: {nextDataPoint.followers_count}");
                }
            }

            // Log current date being displayed
            if (displayObjectPool.GetActiveObjectCount() > 0)
                Debug.Log($"  CURRENT DATE DISPLAYED: {_currentDisplayedDate:yyyy-MM-dd HH:mm:ss}");
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

        public int GetCurrentDataIndex()
        {
            return _dataIndex;
        }
    }
}