using System;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Manages time progression for Feed visualization and simulation.
    ///     Feed uses normalized time to iterate through data points sequentially.
    /// </summary>
    public class FeedTimeController
    {
        private readonly int _loopDuration;
        private float _delayedStartPosition;

        public FeedTimeController(int loopDuration, float startPosition = 0f)
        {
            _loopDuration = loopDuration;
            _delayedStartPosition = startPosition;
            CurrentTime = _delayedStartPosition * loopDuration;
            CurrentNormalizedTime = _delayedStartPosition;
            LoopCount = 0;
        }

        public float CurrentTime { get; private set; }

        public float CurrentNormalizedTime { get; private set; }

        public int CurrentDataIndex { get; private set; }

        public int LoopCount { get; private set; }

        public DateTime CurrentDisplayedDate { get; private set; } = DateTime.MinValue;

        public event Action OnLoopComplete;

        /// <summary>
        ///     Advances time by deltaTime seconds.
        /// </summary>
        public void AdvanceTime(float deltaTime)
        {
            CurrentTime += deltaTime;
            CurrentNormalizedTime = CurrentTime / _loopDuration;

            if (CurrentTime > _loopDuration || CurrentNormalizedTime > 1f)
            {
                CurrentTime = 0;
                CurrentNormalizedTime = 0;
                CurrentDataIndex = 0;
                LoopCount++;
                OnLoopComplete?.Invoke();
            }
        }

        /// <summary>
        ///     Gets the number of data points ready to be processed at the current time.
        /// </summary>
        public int GetNbDataPointsToProcess(FeedDataPoint[] data, int maxToProcess = 50000)
        {
            var count = 0;
            var tempIndex = CurrentDataIndex;

            while (tempIndex < data.Length && count < maxToProcess)
                if (data[tempIndex].normalizedDate <= CurrentNormalizedTime)
                {
                    if (_delayedStartPosition > 0 && data[tempIndex].normalizedDate <= _delayedStartPosition)
                        CurrentDataIndex = tempIndex; //handle first delayed loop
                    else
                        count++;

                    tempIndex++;
                }
                else
                {
                    break;
                }


            _delayedStartPosition = 0;

            return count;
        }

        /// <summary>
        ///     Gets the data index for a specific spawn within the current batch.
        /// </summary>
        public int GetDataIndexForProcess(int processIndex)
        {
            return CurrentDataIndex + processIndex;
        }

        /// <summary>
        ///     Marks data points as processed and updates internal state.
        ///     Returns true if the loop completed.
        /// </summary>
        public bool MarkDataProcessed(FeedDataPoint data)
        {
            CurrentDisplayedDate = data.date;

            CurrentDataIndex += 1;

            return false;
        }

        /// <summary>
        ///     Resets the controller for a new simulation run.
        /// </summary>
        public void Reset()
        {
            CurrentTime = 0f;
            CurrentNormalizedTime = 0f;
            CurrentDataIndex = 0;
            LoopCount = 0;
            CurrentDisplayedDate = DateTime.MinValue;
        }
    }
}