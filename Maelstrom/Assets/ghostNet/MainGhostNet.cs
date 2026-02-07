using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [SerializeField] private ParticleSystemPool particlePool;

        [Header("Data Settings")] [SerializeField]
        private GhostNetDataLoader dataLoader;


        [Header("Debug")] [SerializeField] private bool showDebugInfo = true;

        [SerializeField] private float debugUpdateInterval = 5.0f;

        // Frame rate limiting for spawning
        [Header("Performance Settings")] [SerializeField]
        private int maxObjectsPerFrame = 1000; // Limit objects spawned per frame

        [SerializeField] private int maxObjectsPerSecond = 1000; // Limit objects spawned per second

        [SerializeField] private PureDataConnector pureDataConnector;
        private readonly TimeSpan DATA_TTL = TimeSpan.FromDays(3);
        private readonly GNMaelstromManager maelstrom = new();

        // Day progression tracking for smooth spawning
        private DateTime _currentDay = DateTime.MinValue;


        // Timing
        private float _currentNormalizedTime;
        private float _currentTime;

        // Data management
        private GhostNetDataPoint[] _data;
        private int _dataIndex;
        private int _dataIndexStartForCurrentDate;
        private Dictionary<DateTime, (int startIndex, int count)> _dataRangeByDate;
        private float _dayProgress; // 0 to 1, progress through current day
        private float _dayProgressAtLastSpawn;
        private bool _hasLooped;
        private int _loopDuration;
        private int _nbDataSpawnedForThisDate;
        private float _normalizedDataTTl; // One day in normalized data space

        private int _targetDataToSpawnForThisDate;

        private void Start()
        {
            NetworkManager.Instance.Initialize(5002, new[] { 5000, 5001, 5003 });
            Application.runInBackground = true;
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
                AppLogger.Log("Waiting for ghostNet data to load...");

            _loopDuration = Config.Get("loopDuration", 1200);
            var startPosition = Config.Get("startPosition", 0f);

            CommonMaelstrom.InitializeWithPureData(CommonMaelstrom.RoleId.GhostNet, pureDataConnector);

            _currentTime = startPosition * _loopDuration;
            _currentNormalizedTime = startPosition;
            _currentDay = GetCurrentDayFromNormalizedTime(startPosition);

            if (dataLoader.IsDataLoaded && _dataRangeByDate != null &&
                _dataRangeByDate.TryGetValue(_currentDay.Date, out var range))
            {
                _dataIndexStartForCurrentDate = range.startIndex;
                _targetDataToSpawnForThisDate = range.count;
                _nbDataSpawnedForThisDate = 0;
                _dayProgressAtLastSpawn = 0f;
            }
        }

        private void Update()
        {
            _hasLooped = false;
            NetworkManager.Instance?.ProcessCallbacks();
            if (!dataLoader.IsDataLoaded) return;

            _currentTime += Time.deltaTime;
            _currentNormalizedTime = _currentTime / _loopDuration;
            if (_currentNormalizedTime > 1f)
            {
                _currentTime = 0;
                _currentNormalizedTime = 0;
                _dataIndex = 0;
                _hasLooped = true;
            }

            // Process data and manage display objects
            ProcessDataAndManageObjects(_currentNormalizedTime);
            maelstrom.Update();

            NetworkManager.Instance?.SendNetwork(DataTag.CurrentDataDate,
                new TextData(CommonMaelstrom.RoleId.GhostNet,
                    $" Data({_dataIndex}/{_data.Length})=>{_dayProgress:F2}/{_currentNormalizedTime:F2}({_currentDay:yyyy-MM-dd})")
            );
        }

        private void OnDestroy()
        {
            // Clean up all objects using the static pool
            displayObjectPool.ClearPool();


            AppLogger.Log($"[GHOSTNET_MAIN] Cleanup completed - Pool size: {displayObjectPool.GetPoolSize()}");
        }

        private void InitializeData()
        {
            maelstrom.RegisterDataBounds(dataLoader.Data);
            _data = dataLoader.Data;
            _normalizedDataTTl = dataLoader.GetNormalizedDuration(DATA_TTL);
            _dataRangeByDate = BuildDataRangeByDateIndex();

            // Simulate and dump maelstrom data to CSV
            // maelstrom.SimulateAndDumpDailyMaelstrom(_data);

            // Initialize DisplayObject pool
            var centerOffset = Config.Get("centerPositionOffset", new[]
            {
                0f, 0f
            });
            AppLogger.Log($"[GHOSTNET_MAIN] CenterOffset: {centerOffset}");
            displayObjectPool.Initialize(screenSize, new Vector2(centerOffset[0], centerOffset[1]));
            particlePool.Initialize(screenSize, new Vector2(centerOffset[0], centerOffset[1]));
            AppLogger.Log($"Initialized ghostNet with {_data.Length} data points");
            AppLogger.Log($"One day in normalized data space: {_normalizedDataTTl:F6}");
            AppLogger.Log($"DisplayObject pool initialized with {displayObjectPool.GetPoolSize()} objects");
        }


        private void ProcessDataAndManageObjects(float normalizedCurrentTime)
        {
            // Use modulo to create looping behavior

            displayObjectPool.RecycleOldObjects(normalizedCurrentTime, _normalizedDataTTl);

            ProcessDayProgression(normalizedCurrentTime);

            // Publish current ghostNet maelstrom to network
            var localMaelstrom = maelstrom.GetCurrentMaelstrom();
            displayObjectPool.UpdateActiveObjects(localMaelstrom);
        }


        private void ProcessDayProgression(float normalizedCurrentTime)
        {
            // Calculate which day we should be processing
            var targetDay = GetCurrentDayFromNormalizedTime(normalizedCurrentTime);

            // If we've moved to a new day or looped back, load that day's data
            if (targetDay.Date != _currentDay.Date) StartNewDay(targetDay);

            //know where in the current day we are so that will know how many data points to spawn
            _dayProgress = GetCurrentDayInternalProgress();
            // Spawn data points progressively throughout the day
            SpawnDataPointsForCurrentDayProgress(_dayProgress);
        }

        private void StartNewDay(DateTime targetDay)
        {
            AppLogger.Log($"New day : {targetDay:yyyy-MM-dd}");
            // Clear all active objects when looping to prevent accumulation
            if (!_hasLooped)
            {
                if (!_dataRangeByDate.TryGetValue(targetDay.Date, out var range))
                {
                    AppLogger.Log($"_dataRangeByDate doesn't have a range for {targetDay}");
                    return;
                }

                _dataIndexStartForCurrentDate = range.startIndex;
                _targetDataToSpawnForThisDate = range.count;
            }

            _currentDay = targetDay;
            _nbDataSpawnedForThisDate = 0;
            _dayProgressAtLastSpawn = 0f;

            if (particlePool != null)
                particlePool.StopAll();
        }

        // used to determine when have passed a new day precomputed,
        // so that we know for how long we have to display some data
        private Dictionary<DateTime, (int startIndex, int count)> BuildDataRangeByDateIndex()
        {
            var dataRangeByDate = new Dictionary<DateTime, (int startIndex, int count)>();
            if (_data.Length == 0) return dataRangeByDate;

            var currentDay = _data[0].date.Date;
            var startIndex = 0;

            for (var i = 1; i < _data.Length; i++)
            {
                var day = _data[i].date.Date;
                var isNewDay = day != currentDay;
                if (!isNewDay) continue;

                //new day
                dataRangeByDate[currentDay] = (startIndex, i - startIndex);
                currentDay = day;
                startIndex = i;
            }

            //handle last loop
            dataRangeByDate[currentDay] = (startIndex, _data.Length - startIndex);
            AppLogger.Log($"Built day index with {dataRangeByDate.Count} days");

            return dataRangeByDate;
        }

        private float GetCurrentDayInternalProgress()
        {
            var minDate = dataLoader.DataBounds.Min.date.Date;
            var maxDate = dataLoader.DataBounds.Max.date.Date;
            var dateRange = maxDate - minDate;

            var normalizedOneDayDuration = 1f / dateRange.Days;
            var normalizedTimeStartForDayStart = (_currentDay.Date - minDate).TotalDays / dateRange.Days;
            var currentDayProgress =
                (_currentNormalizedTime - normalizedTimeStartForDayStart) / normalizedOneDayDuration;


            return (float)currentDayProgress;
        }

        private DateTime GetCurrentDayFromNormalizedTime(float normalizedTime)
        {
            var minDate = dataLoader.DataBounds.Min.date.Date;
            var maxDate = dataLoader.DataBounds.Max.date.Date;
            var dateRange = maxDate - minDate;
            var currentDate = minDate + TimeSpan.FromDays(normalizedTime * dateRange.Days);
            return currentDate;
        }

        private void SpawnDataPointsForCurrentDayProgress(float currentDayProgress)
        {
            if (_targetDataToSpawnForThisDate == 0) return;

            var deltaProgress = Mathf.Max(0f, currentDayProgress - _dayProgressAtLastSpawn);
            var toSpawnThisTime = Mathf.RoundToInt(deltaProgress * _targetDataToSpawnForThisDate);
            toSpawnThisTime = Mathf.Min(toSpawnThisTime, _targetDataToSpawnForThisDate - _nbDataSpawnedForThisDate);

            var currentMaelstrom = maelstrom.GetCurrentMaelstrom();
            for (var i = 0; i < toSpawnThisTime; i++)
            {
                var dataPoint = _data[_dataIndexStartForCurrentDate + _nbDataSpawnedForThisDate];

                if (dataPoint.screen_name == "##OTHERS##")
                {
                    particlePool.SpawnDataPoints(dataPoint.nb_accounts_others, currentMaelstrom);
                }
                else
                {
                    maelstrom.RegisterData(dataPoint);
                    displayObjectPool.ActivateDataPoint(dataPoint, _currentNormalizedTime, currentMaelstrom);
                }

                _nbDataSpawnedForThisDate++;
                _dataIndex = _dataIndexStartForCurrentDate + _nbDataSpawnedForThisDate;
            }

            _dayProgressAtLastSpawn = _targetDataToSpawnForThisDate > 0
                ? _nbDataSpawnedForThisDate / (float)_targetDataToSpawnForThisDate
                : currentDayProgress;
        }
    }
}