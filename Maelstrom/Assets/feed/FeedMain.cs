using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Main script for the Feed visualization in Unity
    /// </summary>
    public class FeedMain : MonoBehaviour
    {
        [Header("Display Settings")] [SerializeField]
        private Vector2 screenSize = new(1920, 1080);

        [Header("Object Pool Settings")] [SerializeField]
        private FeedDisplayObjectPool displayObjectPool;

        [Header("Data Settings")] [SerializeField]
        private FeedDataLoader dataLoader;

        [SerializeField] private GameObject positiveCaustics;


        [SerializeField] private PureDataConnector pureDataConnector;

        private readonly FeedMaelstromManager maelstrom = new();
        private FeedDataPoint[] _data;
        private bool _initialized;
        private float _lastDebugTime;
        private int _loopDuration;
        private float _normalizedDisplayDuration;
        private bool _simulationMode;

        private FeedTimeController _timeController;

        private void Start()
        {
            Application.runInBackground = true;
            if (SceneManager.GetActiveScene().name != "FeedScene")
            {
                gameObject.SetActive(false);
                return;
            }

            if (Display.displays.Length > 1)
                Display.displays[1].Activate();

            if (dataLoader == null)
                throw new Exception("DataLoader not found! Please assign a DataLoader component.");

            if (displayObjectPool == null)
                throw new Exception(
                    "FeedDisplayObjectPool not found! Please assign a FeedDisplayObjectPool component.");

            _loopDuration = Config.Get("loopDuration", 1200);
            _simulationMode = Config.Get("simulation", false);

            if (_simulationMode)
            {
                AppLogger.Log("[FEED_MAIN] Simulation mode enabled - waiting for data...");
                return;
            }

            NetworkManager.Instance.Initialize(5003, new[] { 5000, 5001, 5002 });
            CommonMaelstrom.InitializeWithPureData(CommonMaelstrom.RoleId.Feed, pureDataConnector);

            if (dataLoader.IsDataLoaded)
                InitializeData();
            else
                AppLogger.Log("Waiting for data to load...");
        }

        private void Update()
        {
            NetworkManager.Instance?.ProcessCallbacks();
            if (!dataLoader.IsDataLoaded) return;

            if (_simulationMode)
            {
                RunSimulationMode();
                return;
            }

            if (!_initialized)
            {
                InitializeData();
                return;
            }


            _timeController.AdvanceTime(Time.deltaTime);

            ProcessDataAndManageObjects();
            maelstrom.Update();

            NetworkManager.Instance?.SendNetwork(DataTag.CurrentDataDate,
                new TextData(CommonMaelstrom.RoleId.Feed,
                    $" Data Index: {_timeController.CurrentDataIndex}/{_data.Length} => {_timeController.CurrentNormalizedTime:F2}({_timeController.CurrentDisplayedDate:yyyy-MM-dd})"));
        }

        private void OnDisable()
        {
            try
            {
                if (displayObjectPool != null) displayObjectPool.ClearAllActiveObjects();
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[FEED_MAIN] Error during OnDisable: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (displayObjectPool != null) displayObjectPool.ClearPool();
                AppLogger.Log($"[FEED_MAIN] Cleanup completed - Pool size: {displayObjectPool?.GetPoolSize() ?? 0}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[FEED_MAIN] Error during cleanup: {ex.Message}");
            }
        }

        private void RunSimulationMode()
        {
            _simulationMode = false;
            AppLogger.Log("[FEED_MAIN] Data loaded - starting simulation...");

            var simulator = new FeedSimulator();
            simulator.RunSimulation(
                dataLoader.Data,
                dataLoader.DataBounds,
                _loopDuration);

            AppLogger.Log("[FEED_MAIN] Simulation complete - quitting application...");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void InitializeData()
        {
            _data = dataLoader.Data;
            _normalizedDisplayDuration = dataLoader.GetNormalizedDuration(TimeSpan.FromDays(7));

            maelstrom.RegisterDataBounds(_data);

            var startPosition = Config.Get("startPosition", 0f);
            _timeController = new FeedTimeController(_loopDuration, startPosition);

            displayObjectPool.Initialize(screenSize);

            AppLogger.Log($"Initialized with {_data.Length} data points");
            AppLogger.Log($"One week in normalized data space: {_normalizedDisplayDuration:F6}");
            AppLogger.Log($"DisplayObject pool initialized with {displayObjectPool.GetPoolSize()} objects");

            _initialized = true;
        }

        private void ProcessDataAndManageObjects()
        {
            displayObjectPool.RecycleOldObjects(_timeController.CurrentNormalizedTime, _normalizedDisplayDuration);

            var dataPointsToProcess = _timeController.GetNbDataPointsToProcess(_data,
                displayObjectPool.MaxActiveObjects - displayObjectPool.GetActiveObjectCount());

            var maelstromValue = maelstrom.GetCurrentMaelstrom();

            for (var i = 0; i < dataPointsToProcess; i++)
            {
                var dataIdx = _timeController.GetDataIndexForProcess(i);
                if (dataIdx >= _data.Length) continue;

                var dataPoint = _data[dataIdx];
                maelstrom.RegisterData(dataPoint);
                displayObjectPool.ActivateDataPoint(dataPoint, _timeController.CurrentNormalizedTime, maelstromValue);
                _timeController.MarkDataProcessed(_data[dataIdx]);
            }


            positiveCaustics.GetComponent<Renderer>().material.SetFloat("_Maelstrom", maelstromValue);
            displayObjectPool.UpdateActiveObjects(maelstromValue);
        }
    }
}