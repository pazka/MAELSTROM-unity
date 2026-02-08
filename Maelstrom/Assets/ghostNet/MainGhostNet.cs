using System;
using UnityEditor;
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

        [SerializeField] private PureDataConnector pureDataConnector;

        private readonly TimeSpan DATA_TTL = TimeSpan.FromDays(3);
        private readonly GNMaelstromManager maelstrom = new();

        private GhostNetDataPoint[] _data;
        private bool _initialized;
        private int _loopDuration;
        private float _normalizedDataTTl;
        private bool _simulationMode;

        private GhostNetTimeController _timeController;

        private void Start()
        {
            Application.runInBackground = true;
            if (SceneManager.GetActiveScene().name != "GhostNetsScene")
            {
                gameObject.SetActive(false);
                return;
            }

            if (dataLoader == null)
                throw new Exception("GhostNetDataLoader not found! Please assign a GhostNetDataLoader component.");

            _loopDuration = Config.Get("loopDuration", 1200);
            _simulationMode = Config.Get("simulation", false);

            if (_simulationMode)
            {
                AppLogger.Log("[GHOSTNET_MAIN] Simulation mode enabled - waiting for data...");
                return;
            }

            NetworkManager.Instance.Initialize(5002, new[] { 5000, 5001, 5003 });
            CommonMaelstrom.InitializeWithPureData(CommonMaelstrom.RoleId.GhostNet, pureDataConnector);

            if (dataLoader.IsDataLoaded)
                InitializeData();
            else
                AppLogger.Log("Waiting for ghostNet data to load...");
        }

        private void Update()
        {
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

            NetworkManager.Instance?.ProcessCallbacks();

            _timeController.AdvanceTime(Time.deltaTime);

            ProcessDataAndManageObjects();
            maelstrom.Update();

            NetworkManager.Instance?.SendNetwork(DataTag.CurrentDataDate,
                new TextData(CommonMaelstrom.RoleId.GhostNet,
                    $" Data({_timeController.DataIndex}/{_data.Length})=>{_timeController.DayProgress:F2}/{_timeController.CurrentNormalizedTime:F2}({_timeController.CurrentDay:yyyy-MM-dd})")
            );
        }

        private void OnDestroy()
        {
            displayObjectPool.ClearPool();
            AppLogger.Log($"[GHOSTNET_MAIN] Cleanup completed - Pool size: {displayObjectPool.GetPoolSize()}");
        }

        private void RunSimulationMode()
        {
            _simulationMode = false;
            AppLogger.Log("[GHOSTNET_MAIN] Data loaded - starting simulation...");

            var simulator = new GhostNetSimulator();
            simulator.RunSimulation(
                dataLoader.Data,
                dataLoader.DataBounds,
                _loopDuration,
                2,
                30);

            AppLogger.Log("[GHOSTNET_MAIN] Simulation complete - quitting application...");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void InitializeData()
        {
            maelstrom.RegisterDataBounds(dataLoader.Data);
            _data = dataLoader.Data;
            _normalizedDataTTl = dataLoader.GetNormalizedDuration(DATA_TTL);

            var dataRangeByDate = GhostNetTimeController.BuildDataRangeByDateIndex(_data);
            var startPosition = Config.Get("startPosition", 0f);

            _timeController = new GhostNetTimeController(
                _loopDuration,
                dataLoader.DataBounds.Min.date,
                dataLoader.DataBounds.Max.date,
                dataRangeByDate,
                startPosition);

            _timeController.OnNewDay += OnNewDay;

            var centerOffset = Config.Get("centerPositionOffset", new[] { 0f, 0f });
            AppLogger.Log($"[GHOSTNET_MAIN] CenterOffset: {centerOffset}");
            displayObjectPool.Initialize(screenSize, new Vector2(centerOffset[0], centerOffset[1]));
            particlePool.Initialize(screenSize, new Vector2(centerOffset[0], centerOffset[1]));

            AppLogger.Log($"Initialized ghostNet with {_data.Length} data points");
            AppLogger.Log($"One day in normalized data space: {_normalizedDataTTl:F6}");
            AppLogger.Log($"DisplayObject pool initialized with {displayObjectPool.GetPoolSize()} objects");

            _initialized = true;
        }

        private void OnNewDay(DateTime newDay)
        {
            AppLogger.Log($"New day : {newDay:yyyy-MM-dd}");
            particlePool?.StopAll();
        }

        private void ProcessDataAndManageObjects()
        {
            displayObjectPool.RecycleOldObjects(_timeController.CurrentNormalizedTime, _normalizedDataTTl);

            var dataPointsToSpawn = _timeController.ProcessFrame();
            SpawnDataPoints(dataPointsToSpawn);

            displayObjectPool.UpdateActiveObjects();
        }

        private void SpawnDataPoints(int count)
        {
            var currentMaelstrom = maelstrom.GetCurrentMaelstrom();

            for (var i = 0; i < count; i++)
            {
                var dataIdx = _timeController.GetDataIndexForSpawn(i);
                if (dataIdx >= _data.Length) continue;

                var dataPoint = _data[dataIdx];

                if (dataPoint.screen_name == "##OTHERS##")
                {
                    particlePool.SpawnDataPoints(dataPoint.nb_accounts_others, currentMaelstrom);
                }
                else
                {
                    maelstrom.RegisterData(dataPoint);
                    displayObjectPool.ActivateDataPoint(dataPoint, _timeController.CurrentNormalizedTime,
                        currentMaelstrom);
                }
            }

            _timeController.MarkDataPointsSpawned(count);
        }
    }
}