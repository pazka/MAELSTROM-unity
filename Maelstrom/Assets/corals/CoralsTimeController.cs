using System;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Manages time progression for Corals visualization and simulation.
    ///     Corals interpolates between data points based on normalized time.
    /// </summary>
    public class CoralsTimeController
    {
        public event Action OnLoopComplete;

        private readonly int _loopDuration;

        private float _currentTime;
        private float _currentNormalizedTime;
        private int _beforeIndex;
        private int _nextIndex;
        private int _lastProcessedIndex = -1;
        private int _loopCount;

        public float CurrentTime => _currentTime;
        public float CurrentNormalizedTime => _currentNormalizedTime;
        public int BeforeIndex => _beforeIndex;
        public int NextIndex => _nextIndex;
        public int LastProcessedIndex => _lastProcessedIndex;
        public int LoopCount => _loopCount;

        public CoralsTimeController(int loopDuration, float startPosition = 0f)
        {
            _loopDuration = loopDuration;
            _currentTime = startPosition * loopDuration;
            _currentNormalizedTime = startPosition;
            _loopCount = 0;
        }

        /// <summary>
        ///     Advances time by deltaTime seconds. Returns true if a loop completed.
        /// </summary>
        public bool AdvanceTime(float deltaTime)
        {
            _currentTime += deltaTime;
            _currentNormalizedTime = _currentTime / _loopDuration;

            if (_currentTime >= _loopDuration)
            {
                _currentTime = 0f;
                _currentNormalizedTime = 0f;
                _beforeIndex = -1;
                _nextIndex = -1;
                _lastProcessedIndex = -1;
                _loopCount++;
                OnLoopComplete?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Finds the interpolation indices (before and next) for the current time.
        /// </summary>
        public void FindInterpolationIndices(CoralDataPoint[] data)
        {
            _beforeIndex = -1;
            _nextIndex = -1;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].normalizedDate <= _currentNormalizedTime)
                {
                    _beforeIndex = i;
                }
                else
                {
                    _nextIndex = i;
                    break;
                }
            }

            if (_nextIndex == -1)
                _nextIndex = 0;
        }

        /// <summary>
        ///     Gets the interpolation factor between before and next data points.
        /// </summary>
        public float GetInterpolationT(CoralDataPoint[] data)
        {
            if (_beforeIndex == -1 || _nextIndex <= _beforeIndex)
                return 0f;

            var beforeData = data[_beforeIndex];
            var nextData = data[_nextIndex];
            var dateBefore = beforeData.normalizedDate;
            var dateNext = nextData.normalizedDate;

            if (Math.Abs(dateNext - dateBefore) < 0.0001f)
                return 0f;

            return Math.Clamp((_currentNormalizedTime - dateBefore) / (dateNext - dateBefore), 0f, 1f);
        }

        /// <summary>
        ///     Checks if we need to process a new data point.
        ///     Returns true if nextIndex changed since last check.
        /// </summary>
        public bool ShouldProcessNewDataPoint()
        {
            var shouldProcess = _nextIndex != _lastProcessedIndex;
            return shouldProcess;
        }

        /// <summary>
        ///     Marks the current data point as processed.
        /// </summary>
        public void MarkDataProcessed()
        {
            _lastProcessedIndex = _nextIndex;
        }

        /// <summary>
        ///     Gets the index of the data point to process.
        ///     Returns the appropriate index based on the current state.
        /// </summary>
        public int GetDataIndexToProcess(CoralDataPoint[] data)
        {
            if (_beforeIndex == -1)
                return 0;
            return _nextIndex;
        }

        /// <summary>
        ///     Resets the controller for a new simulation run.
        /// </summary>
        public void Reset()
        {
            _currentTime = 0f;
            _currentNormalizedTime = 0f;
            _beforeIndex = -1;
            _nextIndex = -1;
            _lastProcessedIndex = -1;
            _loopCount = 0;
        }

        /// <summary>
        ///     Interpolates coral alpha values between before and next data points.
        /// </summary>
        public (float pos, float neu, float neg) InterpolateAlphas(CoralDataPoint[] data)
        {
            if (_beforeIndex == -1)
            {
                return (data[0].dayNormPos, data[0].dayNormNeu, data[0].dayNormNeg);
            }

            var nextData = data[_nextIndex];

            if (_nextIndex > _beforeIndex)
            {
                var beforeData = data[_beforeIndex];
                var t = GetInterpolationT(data);
                return (
                    Lerp(beforeData.dayNormPos, nextData.dayNormPos, t),
                    Lerp(beforeData.dayNormNeu, nextData.dayNormNeu, t),
                    Lerp(beforeData.dayNormNeg, nextData.dayNormNeg, t)
                );
            }

            return (nextData.dayNormPos, nextData.dayNormNeu, nextData.dayNormNeg);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
