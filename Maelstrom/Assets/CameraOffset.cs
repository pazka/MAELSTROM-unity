using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    /// <summary>
    ///     Main script for the GhostNet visualization in Unity
    /// </summary>
    public class CameraOffset : MonoBehaviour
    {
        [SerializeField] bool secondary = false;

        void Start()
        {
            var offset = Config.Get(secondary ? "camera2Offset" : "camera1Offset", secondary ? new[] { 0f, 0f } : new[] { 0f, 1080f });
            var offsetTransform = new Vector3(offset[0], offset[1], gameObject.transform.position.z);
            gameObject.transform.SetLocalPositionAndRotation(offsetTransform, gameObject.transform.rotation);
        }
    }
}