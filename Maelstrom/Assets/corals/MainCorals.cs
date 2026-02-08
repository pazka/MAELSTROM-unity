using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    public class MainCorals : MonoBehaviour
    {
        [Header("Data Settings")] [SerializeField]
        private CoralsDataLoader dataLoader;

        [SerializeField] private GameObject positive;
        [SerializeField] private GameObject negative;
        [SerializeField] private GameObject neutral;
        [SerializeField] private PureDataConnector pureDataConnector;

        private CoralDataPoint[] _data;
        private CoralsMaelstromManager _maelstromManager;
        private CoralsTimeController _timeController;
        private int _loopDuration;
        private bool _simulationMode;
        private bool _initialized;

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

            _loopDuration = Config.Get("loopDuration", 2000);
            _simulationMode = Config.Get("simulation", false);

            if (dataLoader == null)
                throw new Exception("CoralDataLoader not found! Please assign a CoralDataLoader component.");

            if (_simulationMode)
            {
                AppLogger.Log("[CORALS_MAIN] Simulation mode enabled - waiting for data...");
                return;
            }

            NetworkManager.Instance.Initialize(5001, new[] { 5000, 5002, 5003 });
            CommonMaelstrom.InitializeWithPureData(CommonMaelstrom.RoleId.DeadComunities, pureDataConnector);

            if (dataLoader.IsDataLoaded)
                InitializeData();
            else
                AppLogger.Log("Waiting for corals data to load...");
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

            var looped = _timeController.AdvanceTime(Time.deltaTime);

            if (looped)
            {
                _maelstromManager.Reset();
                CommonMaelstrom.Reset();
                AppLogger.Log("Corals data looped - resetting maelstrom manager");
            }

            ProcessDataAndUpdateCorals();
        }

        private void RunSimulationMode()
        {
            _simulationMode = false;
            AppLogger.Log("[CORALS_MAIN] Data loaded - starting simulation...");

            var simulator = new CoralsSimulator();
            simulator.RunSimulation(
                dataLoader.Data,
                dataLoader.DataBounds,
                _loopDuration,
                targetLoops: 2,
                targetFps: 30);

            AppLogger.Log("[CORALS_MAIN] Simulation complete - quitting application...");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void InitializeData()
        {
            _data = dataLoader.Data;
            _maelstromManager = new CoralsMaelstromManager();
            _maelstromManager.RegisterDataBounds(_data);

            var startPosition = Config.Get("startPosition", 0f);
            _timeController = new CoralsTimeController(_loopDuration, startPosition);
            _timeController.OnLoopComplete += OnLoopComplete;

            AppLogger.Log($"Initialized corals with {_data.Length} data points");

            _initialized = true;
        }

        private void OnLoopComplete()
        {
            _maelstromManager.Reset();
            CommonMaelstrom.Reset();
            AppLogger.Log("Corals data looped - resetting maelstrom manager");
        }

        private void ProcessDataAndUpdateCorals()
        {
            _timeController.FindInterpolationIndices(_data);

            if (_timeController.ShouldProcessNewDataPoint())
            {
                var dataIdx = _timeController.GetDataIndexToProcess(_data);
                if (dataIdx < _data.Length)
                {
                    _maelstromManager.RegisterData(_data[dataIdx]);
                }
                _timeController.MarkDataProcessed();
            }
            else
            {
                _maelstromManager.Update();
            }

            var (alphaPos, alphaNeu, alphaNeg) = _timeController.InterpolateAlphas(_data);
            UpdateCoralsAlpha(alphaPos, alphaNeu, alphaNeg);
        }

        private void UpdateCoralsAlpha(float alphaPos, float alphaNeu, float alphaNeg)
        {
            var localMaelstromValue = _maelstromManager.GetCurrentMaelstrom();

            positive.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaPos * (1 - localMaelstromValue));
            positive.GetComponent<Renderer>().material.SetFloat("_Maelstrom", localMaelstromValue);

            neutral.GetComponent<Renderer>().material.SetFloat("_Opacity", alphaNeu * (1 - localMaelstromValue));
            neutral.GetComponent<Renderer>().material.SetFloat("_Maelstrom", localMaelstromValue);

            negative.GetComponent<Renderer>().material.SetFloat("_Opacity", (alphaNeg + localMaelstromValue) / 2);
            negative.GetComponent<Renderer>().material.SetFloat("_Maelstrom", localMaelstromValue);
        }
    }
}
