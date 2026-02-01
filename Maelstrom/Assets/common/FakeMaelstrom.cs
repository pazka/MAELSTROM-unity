using UnityEngine;
using Random = System.Random;

namespace Maelstrom.Unity
{
    public class FakeMaelstrom : MonoBehaviour
    {
        [SerializeField] private bool isActive;

        private void Start()
        {
            if (!isActive) return;
            CommonMaelstrom.Initialize(CommonMaelstrom.RoleId.Feed);
        }

        private void Update()
        {
            if (!isActive) return;

            var rnd = new Random();
            var target = rnd.NextDouble();
            CommonMaelstrom.UpdateMaelstrom((float)rnd.NextDouble(), (float)rnd.NextDouble());
            NetworkManager.Instance.SendNetwork(DataTag.CurrentDataDate,
                new TextData(CommonMaelstrom.RoleId.Feed, "Some text"));
        }
    }
}