using System;
using System.Linq;
using Maelstrom.Unity;
using TMPro;
using UnityEngine;

public class DebugMaelstrom : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private TextMeshProUGUI textMesh;
    private IMaelstromUdpService _udpService;

    void Start()
    {
        _udpService = new MaelstromUdpService();
            _udpService.SetLocalRole(0);
            _udpService.Start();
    }

    // Update is called once per frame
    void Update()
    {
        var maelstroms = _udpService.GetAllMaelstroms();

        var text = "";

        foreach(var maelstrom in maelstroms)
        {
            text += $"{maelstrom.Key} : {maelstrom.Value}\n";
        }
        textMesh.text = text;
        
    }
}
