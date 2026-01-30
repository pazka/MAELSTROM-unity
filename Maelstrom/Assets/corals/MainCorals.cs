using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    public class MainCorals : MonoBehaviour
    {
        [Header("Data Settings")] [SerializeField]
        private CoralsDataLoader dataLoader;

        // GameObjects representing three types of corals
        [SerializeField] private GameObject positive;
        [SerializeField] private GameObject negative;
        [SerializeField] private GameObject neutral;
        [SerializeField] private PureDataConnector pureDataConnector;
        private int _currentDataIndex;

        // Timing
        private float _currentTime;
        private CoralDataPoint[] _data;
        private bool _isLooping;
        [SerializeField] private CoralsMaelstromManager _maelstromManager;
        private float loopDuration;


        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "CoralsScene")
            {
                gameObject.SetActive(false);
                return;
            }

            positive.SetActive(true);
            negative.SetActive(true);
            neutral.SetActive(true);
            loopDuration = Config.Get("loopDuration", 2000);
            if (dataLoader == null)
                throw new Exception("CoralDataLoader not found! Please assign a CoralDataLoader component.");

            // Wait for data to load
            if (dataLoader.IsDataLoaded)
                InitializeData();
            else
                AppLogger.Log("Waiting for corals data to load...");

            NetworkManager.Instance.Initialize(5001, new[] { 5000, 5002, 5003 });
            CommonMaelstrom.InitializeWithPureData(CommonMaelstrom.RoleId.DeadComunities, pureDataConnector);
        }

        private void Update()
        {
            if (!dataLoader.IsDataLoaded) return;

            _currentTime += Time.deltaTime;
            ProcessDataAndUpdateCorals();
        }

        private void OnDestroy()
        {
            CommonMaelstrom.Cleanup();
        }

        private void InitializeData()
        {
            _data = dataLoader.Data;
            _maelstromManager = new CoralsMaelstromManager();
            _maelstromManager.RegisterDataBounds(_data);

            // Simulate and dump maelstrom data to CSV
            _maelstromManager.SimulateAndDumpDailyMaelstrom(_data);

            AppLogger.Log($"Initialized corals with {_data.Length} data points");
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

                AppLogger.Log("Corals data looped - resetting maelstrom manager");
            }

            var normalizedCurrentTime = _currentTime / loopDuration;

            // Find the two data points to interpolate between
            var beforeIndex = -1;
            var nextIndex = -1;

            for (var i = 0; i < _data.Length; i++)
                if (_data[i].normalizedDate <= normalizedCurrentTime)
                {
                    beforeIndex = i;
                }
                else
                {
                    nextIndex = i;
                    break;
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
                // After last data point, loop back to start
                nextIndex = 0;

            // Interpolate between the two data points
            var beforeData = _data[beforeIndex];
            var nextData = _data[nextIndex];

            float t;
            if (nextIndex == 0)
            {
                // Wrapping around from end to beginning
                var timeToEnd = 1.0f - beforeData.normalizedDate;
                var timeFromStart = nextData.normalizedDate;
                var totalTime = timeToEnd + timeFromStart;
                var currentTimeFromBefore = normalizedCurrentTime - beforeData.normalizedDate;

                if (currentTimeFromBefore <= timeToEnd)
                    t = currentTimeFromBefore / timeToEnd;
                else
                    t = (currentTimeFromBefore - timeToEnd) / timeFromStart;
            }
            else
            {
                // Normal interpolation between consecutive points
                var timeSpan = nextData.normalizedDate - beforeData.normalizedDate;
                var currentTimeFromBefore = normalizedCurrentTime - beforeData.normalizedDate;
                t = currentTimeFromBefore / timeSpan;
            }

            // Use smoothstep for smoother interpolation
            t = t * t * (3.0f - 2.0f * t);

            // Interpolate alpha values
            var alphaPos = Mathf.Lerp(beforeData.dayNormPos, nextData.dayNormPos, t);
            var alphaNeu = Mathf.Lerp(beforeData.dayNormNeu, nextData.dayNormNeu, t);
            var alphaNeg = Mathf.Lerp(beforeData.dayNormNeg, nextData.dayNormNeg, t);

            // Register current data point with maelstrom manager
            _maelstromManager.RegisterData(beforeData);

            UpdateCoralsAlpha(alphaPos, alphaNeu, alphaNeg);
        }

        private void UpdateCoralsAlpha(float alphaPos, float alphaNeu, float alphaNeg)
        {
            var localMaelstromValue = _maelstromManager.GetCurrentMaelstrom();

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
    }
}