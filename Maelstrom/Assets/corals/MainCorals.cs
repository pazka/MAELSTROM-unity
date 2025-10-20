
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace Maelstrom.Unity
{
    public class MainCorals : MonoBehaviour
    {

        [Header("Data Settings")]
        [SerializeField] private CoralsDataLoader dataLoader;
        [Header("Data Settings")]
        [SerializeField] private Config config;

        // GameObjects representing three types of corals
        [SerializeField] private GameObject positive;
        [SerializeField] private GameObject negative;
        [SerializeField] private GameObject neutral;

        // Timing
        private float _currentTime = 0.0f;
        private CoralDataPoint[] _data;
        private CoralsMaelstromManager _maelstromManager;
        private float loopDuration;
        private int _currentDataIndex = 0;
        private bool _isLooping = false;

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "CoralsScene")
            {
                gameObject.SetActive(false);
                return;
            }

            // Initialize UDP service for corals role
            CommonMaelstrom.InitializeUdpService(1); // 1 = corals

            positive.SetActive(true);
            negative.SetActive(true);
            neutral.SetActive(true);
            loopDuration = config.Get("loopDuration", 600);
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
            _maelstromManager = new CoralsMaelstromManager();
            _maelstromManager.RegisterDataBounds(_data);
            Debug.Log($"Initialized corals with {_data.Length} data points");
        }

        private void ProcessDataAndUpdateCorals()
        {
            // Check if we need to loop
            if (_currentTime >= loopDuration)
            {
                _currentTime = 0.0f;
                _currentDataIndex = 0;
                _isLooping = true;

                // Reset maelstrom manager for new loop
                _maelstromManager = new CoralsMaelstromManager();
                _maelstromManager.RegisterDataBounds(_data);

                Debug.Log("Corals data looped - resetting maelstrom manager");
            }

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
                _maelstromManager.RegisterData(_data[0]);
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

            // Register current data point with maelstrom manager
            _maelstromManager.RegisterData(beforeData);

            UpdateCoralsAlpha(alphaPos, alphaNeu, alphaNeg);
        }

        private void UpdateCoralsAlpha(float alphaPos, float alphaNeu, float alphaNeg)
        {
            float localMaelstromValue = _maelstromManager.GetCurrentMaelstrom();

            positive.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaPos);
            positive.GetComponent<Renderer>().material.SetFloat("_Maelstrom", localMaelstromValue);

            neutral.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaNeu);
            neutral.GetComponent<Renderer>().material.SetFloat("_Maelstrom", localMaelstromValue);

            negative.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaNeg);
            negative.GetComponent<Renderer>().material.SetFloat("_Maelstrom", localMaelstromValue);
        }


        public float GetCurrentTime()
        {
            return _currentTime;
        }

        public bool IsLooping()
        {
            return _isLooping;
        }

        public int GetCurrentDataIndex()
        {
            return _currentDataIndex;
        }

        private void OnDestroy()
        {
            CommonMaelstrom.Cleanup();
        }
    }
}
