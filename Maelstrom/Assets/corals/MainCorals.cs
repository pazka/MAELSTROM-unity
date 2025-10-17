
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace Maelstrom.Unity
{
    public class MainCorals : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private float loopDuration = 600.0f; // seconds

        [Header("Data Settings")]
        [SerializeField] private CoralsDataLoader dataLoader;

        // GameObjects representing three types of corals
        [SerializeField] private GameObject positive;
        [SerializeField] private GameObject negative;
        [SerializeField] private GameObject neutral;

        // Timing
        private float _currentTime = 0.0f;
        private CoralDataPoint[] _data;

        private void Start()
        {
            positive.SetActive(false);
            negative.SetActive(false);
            neutral.SetActive(false);

            if (SceneManager.GetActiveScene().name != "CoralsScene")
            {
                return;
            }

            if (dataLoader == null)
            {
                throw new System.Exception("CoralDataLoader not found! Please assign a CoralDataLoader component.");
            }

            // Wait for data to load
            if (dataLoader.IsDataLoaded)
            {
                InitializeData();
            }
            else
            {
                Debug.Log("Waiting for corals data to load...");
            }

            positive.SetActive(true);
            negative.SetActive(true);
            neutral.SetActive(true);
        }

        private void Update()
        {
            if (!dataLoader.IsDataLoaded) return;

            _currentTime += Time.deltaTime;
            ProcessDataAndUpdateCorals();
        }

        private void InitializeData()
        {
            _data = dataLoader.Data;
            Debug.Log($"Initialized corals with {_data.Length} data points");
        }

        private void ProcessDataAndUpdateCorals()
        {
            float normalizedCurrentTime = _currentTime / loopDuration;

            // Find the two data points to interpolate between
            int beforeIndex = -1;
            int nextIndex = -1;

            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i].normalizedDate <= normalizedCurrentTime)
                {
                    beforeIndex = i;
                }
                else
                {
                    nextIndex = i;
                    break;
                }
            }

            // Handle edge cases
            if (beforeIndex == -1)
            {
                // Before first data point, use first data point
                UpdateCoralsAlpha(_data[0].dayNormPos, _data[0].dayNormNeu, _data[0].dayNormNeg);
                return;
            }

            if (nextIndex == -1)
            {
                // After last data point, loop back to start
                nextIndex = 0;
            }

            // Interpolate between the two data points
            CoralDataPoint beforeData = _data[beforeIndex];
            CoralDataPoint nextData = _data[nextIndex];

            float t;
            if (nextIndex == 0)
            {
                // Wrapping around from end to beginning
                float timeToEnd = 1.0f - beforeData.normalizedDate;
                float timeFromStart = nextData.normalizedDate;
                float totalTime = timeToEnd + timeFromStart;
                float currentTimeFromBefore = normalizedCurrentTime - beforeData.normalizedDate;

                if (currentTimeFromBefore <= timeToEnd)
                {
                    t = currentTimeFromBefore / timeToEnd;
                }
                else
                {
                    t = (currentTimeFromBefore - timeToEnd) / timeFromStart;
                }
            }
            else
            {
                // Normal interpolation between consecutive points
                float timeSpan = nextData.normalizedDate - beforeData.normalizedDate;
                float currentTimeFromBefore = normalizedCurrentTime - beforeData.normalizedDate;
                t = currentTimeFromBefore / timeSpan;
            }

            // Use smoothstep for smoother interpolation
            t = t * t * (3.0f - 2.0f * t);

            // Interpolate alpha values
            float alphaPos = Mathf.Lerp(beforeData.dayNormPos, nextData.dayNormPos, t);
            float alphaNeu = Mathf.Lerp(beforeData.dayNormNeu, nextData.dayNormNeu, t);
            float alphaNeg = Mathf.Lerp(beforeData.dayNormNeg, nextData.dayNormNeg, t);

            UpdateCoralsAlpha(alphaPos, alphaNeu, alphaNeg);
        }

        private void UpdateCoralsAlpha(float alphaPos, float alphaNeu, float alphaNeg)
        {
            positive.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaPos);
            neutral.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaNeu);
            negative.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaNeg);
        }

        // Public methods for external control
        public void SetLoopDuration(float newLoopDuration)
        {
            loopDuration = newLoopDuration;
        }

        public float GetCurrentTime()
        {
            return _currentTime;
        }
    }
}
