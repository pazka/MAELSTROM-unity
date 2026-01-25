using System;
using System.Collections.Generic;
using Maelstrom.Unity;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class DebugMaelstrom : MonoBehaviour
{
    private const int HistorySize = 50_000;
    [SerializeField] private TextMeshProUGUI debugMesh;

    private readonly Dictionary<CommonMaelstrom.RoleId, LineRenderer> _lineRenderers = new();
    private readonly List<string> _logs = new();
    private readonly Dictionary<string, Tuple<CommonMaelstrom.RoleId, string>> _texts = new();
    private readonly Dictionary<CommonMaelstrom.RoleId, Queue<float>> _valueHistory = new();

    private Dictionary<CommonMaelstrom.RoleId, Color> _lineColors;

    private void Start()
    {
        Application.runInBackground = true;
        _lineColors = new Dictionary<CommonMaelstrom.RoleId, Color>
            {
                {
                    CommonMaelstrom.RoleId.DeadComunities, new Color(1f, 0.3f, 0.3f)
                },
                {
                    CommonMaelstrom.RoleId.GhostNet, new Color(0.3f, 0.3f, 1f)
                },
                {
                    CommonMaelstrom.RoleId.Feed, new Color(0.3f, 1f, 0.3f)
                },
                {
                    CommonMaelstrom.RoleId.Debug, new Color(1f, 1f, 1f)
                }
            }
            ;
        CommonMaelstrom.Initialize(CommonMaelstrom.RoleId.Debug);

        CreateLineRenderer(CommonMaelstrom.RoleId.DeadComunities,
            _lineColors[CommonMaelstrom.RoleId.DeadComunities]);
        CreateLineRenderer(CommonMaelstrom.RoleId.GhostNet,
            _lineColors[CommonMaelstrom.RoleId.GhostNet]);
        CreateLineRenderer(CommonMaelstrom.RoleId.Feed,
            _lineColors[CommonMaelstrom.RoleId.Feed]);
        CreateLineRenderer(CommonMaelstrom.RoleId.Debug,
            _lineColors[CommonMaelstrom.RoleId.Debug]);

        _valueHistory[CommonMaelstrom.RoleId.DeadComunities] = new Queue<float>();
        _valueHistory[CommonMaelstrom.RoleId.GhostNet] = new Queue<float>();
        _valueHistory[CommonMaelstrom.RoleId.Feed] = new Queue<float>();
        _valueHistory[CommonMaelstrom.RoleId.Debug] = new Queue<float>();

        NetworkManager.Instance.Initialize(5000);
        NetworkManager.Instance.ListenNetwork<FloatData>(DataTag.TargetMaelstromValue, UpdateTargetMaelstrom);
        NetworkManager.Instance.ListenNetwork<TextData>(DataTag.CurrentDataDate, UpdateCurrentDate);
        NetworkManager.Instance.ListenNetwork<TextData>(DataTag.Logs, UpdateLogs);
    }

    private void Update()
    {
        NetworkManager.Instance.ProcessCallbacks();
        var allMaelstroms = CommonMaelstrom.GetAllMaelstroms();

        foreach (var sculptureMaelstrom in allMaelstroms)
        {
            _texts[$"curr_{sculptureMaelstrom.Key}"] =
                new Tuple<CommonMaelstrom.RoleId, string>(sculptureMaelstrom.Key, $"{sculptureMaelstrom.Value:F4}\n");

            _valueHistory[sculptureMaelstrom.Key].Enqueue(sculptureMaelstrom.Value);

            if (_valueHistory[sculptureMaelstrom.Key].Count > HistorySize)
                _valueHistory[sculptureMaelstrom.Key].Dequeue();
        }

        var debugText = "";
        foreach (var kvp in _texts)
            debugText += $"<color=#{_lineColors[kvp.Value.Item1].ToHexString()}>{kvp.Key}: {kvp.Value.Item2}</color>\n";
        debugMesh.text = debugText;

        foreach (var kvp in _valueHistory) UpdateLine(kvp.Key);
    }

    private void UpdateTargetMaelstrom(FloatData data)
    {
        _texts[$"trgt_{CommonMaelstrom.RoleToKey(data.RoleId)}"] =
            new Tuple<CommonMaelstrom.RoleId, string>(data.RoleId, data.Value.ToString());
    }

    private void UpdateCurrentDate(TextData data)
    {
        _texts[$"date_{CommonMaelstrom.RoleToKey(data.RoleId)}"] =
            new Tuple<CommonMaelstrom.RoleId, string>(data.RoleId, data.Text);
    }

    private void UpdateLogs(TextData data)
    {
        _logs.Add($"{CommonMaelstrom.RoleToKey(data.RoleId)} : {data.Text}");
    }

    private void CreateLineRenderer(CommonMaelstrom.RoleId key, Color color)
    {
        var lineObj = new GameObject($"Line_{key}");
        lineObj.transform.SetParent(transform);

        var lr = lineObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 2f;
        lr.endWidth = 2f;
        lr.positionCount = 0;
        lr.useWorldSpace = false;

        _lineRenderers[key] = lr;
    }

    private void UpdateLine(CommonMaelstrom.RoleId key)
    {
        if (!_lineRenderers.ContainsKey(key) || !_valueHistory.ContainsKey(key))
            return;

        var lr = _lineRenderers[key];
        var values = _valueHistory[key];

        if (values.Count == 0)
            return;

        var valuesArray = values.ToArray();
        lr.positionCount = valuesArray.Length;

        var span = Math.Max(1f, valuesArray.Length - 1);

        for (var i = 0; i < valuesArray.Length; i++)
        {
            var normalizedX = (i / span - 0.5f) * 1920;
            var normalizedY = valuesArray[i] * 1080 - 1080 * 0.5f;
            lr.SetPosition(i, new Vector3(normalizedX, normalizedY, 0));
        }
    }
}