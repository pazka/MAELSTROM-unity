using System;
using System.Collections.Generic;
using System.Linq;
using Maelstrom.Unity;
using TMPro;
using UnityEngine;

public class DebugMaelstrom : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMesh;
    private IMaelstromUdpService _udpService;

    private Dictionary<string, LineRenderer> lineRenderers = new Dictionary<string, LineRenderer>();
    private Dictionary<string, Queue<float>> valueHistory = new Dictionary<string, Queue<float>>();
    private Dictionary<string, int> lastLoggedCount = new Dictionary<string, int>();
    private const int HISTORY_SIZE = 1080;
    private const float LINE_HEIGHT = 1f;

    private Color[] lineColors = new Color[]
    {
        new Color(1f, 0.3f, 0.3f),  // corals - red
        new Color(0.3f, 0.3f, 1f),  // ghostNet - blue
        new Color(0.3f, 1f, 0.3f)   // feed - green
    };

    void Start()
    {
        _udpService = new MaelstromUdpService();
        _udpService.SetLocalRole(0);
        _udpService.Start();

        string[] keys = { "corals", "ghostNet", "feed" };
        for (int i = 0; i < keys.Length; i++)
        {
            CreateLineRenderer(keys[i], lineColors[i]);
            valueHistory[keys[i]] = new Queue<float>();
        }
    }

    void Update()
    {
        var maelstroms = _udpService.GetAllMaelstroms();

        var text = "";

        foreach (var maelstrom in maelstroms)
        {
            text += $"{maelstrom.Key} : {maelstrom.Value:F4}\n";
            
            if (valueHistory.ContainsKey(maelstrom.Key))
            {
                valueHistory[maelstrom.Key].Enqueue(maelstrom.Value);
                
                if (valueHistory[maelstrom.Key].Count > HISTORY_SIZE)
                {
                    valueHistory[maelstrom.Key].Dequeue();
                }
                
                int currentCount = valueHistory[maelstrom.Key].Count;
                if (!lastLoggedCount.ContainsKey(maelstrom.Key) || lastLoggedCount[maelstrom.Key] != currentCount)
                {
                    Debug.Log($"Updated {maelstrom.Key} history: {currentCount} values, current: {maelstrom.Value:F4}");
                    lastLoggedCount[maelstrom.Key] = currentCount;
                }
            }
        }
        
        textMesh.text = text;
        
        foreach (var kvp in valueHistory)
        {
            UpdateLine(kvp.Key);
        }
    }

    private void CreateLineRenderer(string key, Color color)
    {
        GameObject lineObj = new GameObject($"Line_{key}");
        lineObj.transform.SetParent(transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.positionCount = 0;
        lr.useWorldSpace = false;

        lineRenderers[key] = lr;
    }

    private void UpdateLine(string key)
    {
        if (!lineRenderers.ContainsKey(key) || !valueHistory.ContainsKey(key))
            return;

        LineRenderer lr = lineRenderers[key];
        Queue<float> values = valueHistory[key];

        if (values.Count == 0)
            return;

        float[] valuesArray = values.ToArray();
        lr.positionCount = valuesArray.Length;

        float span = Math.Max(1f, valuesArray.Length - 1);

        for (int i = 0; i < valuesArray.Length; i++)
        {
            float normalizedX = (float)i / span * 2f - 1f;
            float normalizedY = (valuesArray[i] * LINE_HEIGHT) - LINE_HEIGHT * 0.5f;
            lr.SetPosition(i, new Vector3(normalizedX, normalizedY, 0));
        }
    }
}
