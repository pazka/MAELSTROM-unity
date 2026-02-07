using System;
using System.Collections.Generic;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Manages time progression and day tracking for GhostNet.
    ///     Extracted to be reusable by both visualization and simulation.
    /// </summary>
    public class GhostNetTimeController
    {
        public event Action<DateTime> OnNewDay;
        public event Action OnLoopComplete;

        private readonly int _loopDuration;
        private readonly DateTime _minDate;
        private readonly DateTime _maxDate;
        private readonly int _dateRangeDays;
        private readonly Dictionary<DateTime, (int startIndex, int count)> _dataRangeByDate;

        private float _currentTime;
        private float _currentNormalizedTime;
        private DateTime _currentDay;
        private bool _hasLooped;
        private int _loopCount;

        private int _dataIndex;
        private int _dataIndexStartForCurrentDate;
        private int _targetDataToSpawnForThisDate;
        private int _nbDataSpawnedForThisDate;
        private float _dayProgress;
        private float _dayProgressAtLastSpawn;

        public float CurrentTime => _currentTime;
        public float CurrentNormalizedTime => _currentNormalizedTime;
        public DateTime CurrentDay => _currentDay;
        public bool HasLooped => _hasLooped;
        public int LoopCount => _loopCount;
        public float DayProgress => _dayProgress;
        public int DataIndex => _dataIndex;
        public int NbDataSpawnedForThisDate => _nbDataSpawnedForThisDate;

        public GhostNetTimeController(
            int loopDuration,
            DateTime minDate,
            DateTime maxDate,
            Dictionary<DateTime, (int startIndex, int count)> dataRangeByDate,
            float startPosition = 0f)
        {
            _loopDuration = loopDuration;
            _minDate = minDate.Date;
            _maxDate = maxDate.Date;
            _dateRangeDays = (_maxDate - _minDate).Days;
            _dataRangeByDate = dataRangeByDate;

            _currentTime = startPosition * loopDuration;
            _currentNormalizedTime = startPosition;
            _currentDay = GetDayFromNormalizedTime(startPosition);
            _loopCount = 0;

            InitializeDayData(_currentDay);
        }

        /// <summary>
        ///     Advances time by deltaTime seconds. Returns true if a loop completed.
        /// </summary>
        public bool AdvanceTime(float deltaTime)
        {
            _hasLooped = false;

            _currentTime += deltaTime;
            _currentNormalizedTime = _currentTime / _loopDuration;

            if (_currentNormalizedTime > 1f)
            {
                _currentTime = 0;
                _currentNormalizedTime = 0;
                _dataIndex = 0;
                _hasLooped = true;
                _loopCount++;
                OnLoopComplete?.Invoke();
            }

            return _hasLooped;
        }

        /// <summary>
        ///     Processes day progression and returns the number of data points to spawn this frame.
        /// </summary>
        public int ProcessFrame()
        {
            var targetDay = GetDayFromNormalizedTime(_currentNormalizedTime);

            if (targetDay.Date != _currentDay.Date)
            {
                StartNewDay(targetDay);
            }

            _dayProgress = GetDayInternalProgress();
            return CalculateDataPointsToSpawn(_dayProgress);
        }

        /// <summary>
        ///     Called after spawning data points to update internal counters.
        /// </summary>
        public void MarkDataPointsSpawned(int count)
        {
            _nbDataSpawnedForThisDate += count;
            _dataIndex = _dataIndexStartForCurrentDate + _nbDataSpawnedForThisDate;

            _dayProgressAtLastSpawn = _targetDataToSpawnForThisDate > 0
                ? _nbDataSpawnedForThisDate / (float)_targetDataToSpawnForThisDate
                : _dayProgress;
        }

        /// <summary>
        ///     Gets the data point at the specified spawn index for the current day.
        /// </summary>
        public int GetDataIndexForSpawn(int spawnIndex)
        {
            return _dataIndexStartForCurrentDate + _nbDataSpawnedForThisDate + spawnIndex;
        }

        public DateTime GetDayFromNormalizedTime(float normalizedTime)
        {
            return _minDate + TimeSpan.FromDays(normalizedTime * _dateRangeDays);
        }

        private float GetDayInternalProgress()
        {
            var normalizedOneDayDuration = 1f / _dateRangeDays;
            var normalizedTimeStartForDayStart = (_currentDay.Date - _minDate).TotalDays / _dateRangeDays;
            var currentDayProgress = (_currentNormalizedTime - normalizedTimeStartForDayStart) / normalizedOneDayDuration;

            return (float)currentDayProgress;
        }

        private void StartNewDay(DateTime targetDay)
        {
            AppLogger.Log($"[TimeController] New day: {targetDay:yyyy-MM-dd}");

            if (!_hasLooped)
            {
                if (!_dataRangeByDate.TryGetValue(targetDay.Date, out var range))
                {
                    AppLogger.Log($"[TimeController] No data range for {targetDay:yyyy-MM-dd}");
                    _targetDataToSpawnForThisDate = 0;
                }
                else
                {
                    _dataIndexStartForCurrentDate = range.startIndex;
                    _targetDataToSpawnForThisDate = range.count;
                }
            }
            else
            {
                InitializeDayData(targetDay);
            }

            _currentDay = targetDay;
            _nbDataSpawnedForThisDate = 0;
            _dayProgressAtLastSpawn = 0f;

            OnNewDay?.Invoke(targetDay);
        }

        private void InitializeDayData(DateTime day)
        {
            if (_dataRangeByDate.TryGetValue(day.Date, out var range))
            {
                _dataIndexStartForCurrentDate = range.startIndex;
                _targetDataToSpawnForThisDate = range.count;
            }
            else
            {
                _dataIndexStartForCurrentDate = 0;
                _targetDataToSpawnForThisDate = 0;
            }

            _nbDataSpawnedForThisDate = 0;
            _dayProgressAtLastSpawn = 0f;
        }

        private int CalculateDataPointsToSpawn(float currentDayProgress)
        {
            if (_targetDataToSpawnForThisDate == 0) 
                return 0;

            var deltaProgress = Math.Max(0f, currentDayProgress - _dayProgressAtLastSpawn);
            var toSpawn = (int)Math.Round(deltaProgress * _targetDataToSpawnForThisDate);
            toSpawn = Math.Min(toSpawn, _targetDataToSpawnForThisDate - _nbDataSpawnedForThisDate);

            return toSpawn;
        }

        /// <summary>
        ///     Builds a date range index from data points.
        /// </summary>
        public static Dictionary<DateTime, (int startIndex, int count)> BuildDataRangeByDateIndex(GhostNetDataPoint[] data)
        {
            var dataRangeByDate = new Dictionary<DateTime, (int startIndex, int count)>();
            if (data.Length == 0) return dataRangeByDate;

            var currentDay = data[0].date.Date;
            var startIndex = 0;

            for (var i = 1; i < data.Length; i++)
            {
                var day = data[i].date.Date;
                if (day == currentDay) continue;

                dataRangeByDate[currentDay] = (startIndex, i - startIndex);
                currentDay = day;
                startIndex = i;
            }

            dataRangeByDate[currentDay] = (startIndex, data.Length - startIndex);
            AppLogger.Log($"[TimeController] Built day index with {dataRangeByDate.Count} days");

            return dataRangeByDate;
        }
    }
}
