using System;
using System.Collections.Generic;
using Maelstrom.Unity;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class DebugMaelstrom : MonoBehaviour
{
    private const int LogsSize = 30;
    private const float UiMargin = 5f;
    private const float DebugPanelWidthFraction = 0.4f;
    private const float DebugPanelHeightFraction = 0.35f;
    private const float LogPanelHeightFraction = 0.35f;
    private const float FontSizeMin = 5f;
    private const float FontSizeMax = 12f;
    private const float FontSizePerHeight = 1f / 27f;

    [SerializeField] private TextMeshProUGUI debugMesh;
    [SerializeField] private TextMeshProUGUI logMesh;
    private readonly Dictionary<CommonMaelstrom.RoleId, float> _lastValues = new();

    private readonly Dictionary<CommonMaelstrom.RoleId, LineRenderer> _lineRenderers = new();
    private readonly List<string> _logs = new();
    private readonly Dictionary<string, Tuple<CommonMaelstrom.RoleId, string>> _texts = new();
    private int _currentGraphPosition;
    private int _lastScreenHeight;
    private int _lastScreenWidth;

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

        foreach (var role in CommonMaelstrom.RoleIds)
        {
            _lastValues[role] = -1f;
            CreateLineRenderer(role, _lineColors[role]);
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        ApplyCameraOrthoSize();
        ApplyDebugUILayout();

        NetworkManager.Instance.Initialize(5000);
        NetworkManager.Instance.ListenNetwork<FloatData>(DataTag.TargetMaelstromValue, UpdateTargetMaelstrom);
        NetworkManager.Instance.ListenNetwork<TextData>(DataTag.CurrentDataDate, UpdateCurrentDate);
        NetworkManager.Instance.ListenNetwork<TextData>(DataTag.Logs, UpdateLogs);
    }

    private void Update()
    {
        NetworkManager.Instance.ProcessCallbacks();

        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            HandleScreenResize();

        var allMaelstroms = CommonMaelstrom.GetAllMaelstroms();

        foreach (var sculptureMaelstrom in allMaelstroms)
            _texts[$"curr_{sculptureMaelstrom.Key}"] =
                new Tuple<CommonMaelstrom.RoleId, string>(sculptureMaelstrom.Key, $"{sculptureMaelstrom.Value:F3}\n");

        foreach (var roleId in CommonMaelstrom.RoleIds)
            _lastValues[roleId] = allMaelstroms.TryGetValue(roleId, out var v) ? v : _lastValues[roleId];

        var debugText = "";
        foreach (var kvp in _texts)
            debugText += $"<color=#{_lineColors[kvp.Value.Item1].ToHexString()}>{kvp.Key}: {kvp.Value.Item2}</color>\n";
        debugMesh.text = debugText;

        logMesh.text = string.Join("\n", _logs);

        foreach (var role in CommonMaelstrom.RoleIds) UpdateLine(role);

        _currentGraphPosition++;

        if (_currentGraphPosition >= Screen.width)
            _currentGraphPosition = 0;
    }

    private void HandleScreenResize()
    {
        foreach (var lr in _lineRenderers.Values)
        {
            lr.positionCount = Screen.width;
            for (var i = 0; i < Screen.width; i++)
                lr.SetPosition(i, Vector3.zero);
        }

        _currentGraphPosition = Mathf.Clamp(_currentGraphPosition, 0, Screen.width - 1);
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        ApplyCameraOrthoSize();
        ApplyDebugUILayout();
    }

    private void ApplyCameraOrthoSize()
    {
        if (Camera.main != null)
            Camera.main.orthographicSize = Screen.height / 2f;
    }

    private void ApplyDebugUILayout()
    {
        var fontSize = Mathf.Clamp(Screen.height * FontSizePerHeight, FontSizeMin, FontSizeMax);

        if (debugMesh != null)
        {
            var rt = debugMesh.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-UiMargin, -UiMargin);
            rt.sizeDelta = new Vector2(Screen.width * DebugPanelWidthFraction, Screen.height * DebugPanelHeightFraction);
            debugMesh.fontSize = fontSize;
        }

        if (logMesh != null)
        {
            var rt = logMesh.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(UiMargin, -UiMargin);
            rt.sizeDelta = new Vector2(Screen.width * DebugPanelWidthFraction, Screen.height * LogPanelHeightFraction);
            logMesh.fontSize = fontSize;
        }
    }

    private void UpdateTargetMaelstrom(FloatData data)
    {
        _texts[$"trgt_{CommonMaelstrom.RoleToKey(data.RoleId)}"] =
            new Tuple<CommonMaelstrom.RoleId, string>(data.RoleId, data.Value.ToString("F3"));
    }

    private void UpdateCurrentDate(TextData data)
    {
        _texts[$"date_{CommonMaelstrom.RoleToKey(data.RoleId)}"] =
            new Tuple<CommonMaelstrom.RoleId, string>(data.RoleId, data.Text);
    }

    private void UpdateLogs(TextData data)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var roleKey = CommonMaelstrom.RoleToKey(data.RoleId);
        var line = $"{timestamp} {roleKey} : {data.Text}";
        if (_lineColors.TryGetValue(data.RoleId, out var color))
            line = $"<color=#{color.ToHexString()}>{line}</color>";
        _logs.Add(line);

        if (_logs.Count > LogsSize)
            _logs.RemoveAt(0);
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
        lr.positionCount = Screen.width;
        lr.useWorldSpace = false;

        _lineRenderers[key] = lr;
    }

    private void UpdateLine(CommonMaelstrom.RoleId key)
    {
        var normalizedX = (_currentGraphPosition / (float)Screen.width - 0.5f) * Screen.width;
        var normalizedY = _lastValues[key] * Screen.height - Screen.height * 0.5f;
        _lineRenderers[key].SetPosition(_currentGraphPosition, new Vector3(normalizedX, normalizedY, 0));
    }
}