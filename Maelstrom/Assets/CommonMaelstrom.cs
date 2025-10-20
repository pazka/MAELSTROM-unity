
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maelstrom.Unity
{
    public static class CommonMaelstrom
    {
        private static float HIGH_MAELSTROM_THRESHOLD = 0.99f;
        private static float MEDIUM_MAELSTROM_THRESHOLD = 0.94f;

        private static float currentMaelstrom = 0f;
        private static float targetMaelstrom = 0f;

        public static float UpdateMaelstrom(float currentRatio)
        {
            var rnd = new System.Random();
            var netRnd = rnd.NextDouble();

            if ((currentMaelstrom - targetMaelstrom) < 0.002f)
            {
                if (currentRatio > 0.3 && netRnd >= HIGH_MAELSTROM_THRESHOLD)
                {
                    targetMaelstrom = 1;
                    Debug.Log($"BIG Mal({netRnd}) : {targetMaelstrom}/{currentMaelstrom}");
                }
                else if (currentRatio > 0.3 && netRnd >= MEDIUM_MAELSTROM_THRESHOLD)
                {
                    targetMaelstrom = 0.7f;
                    Debug.Log($"MID Mal({netRnd}) : {targetMaelstrom}/{currentMaelstrom}");
                }
                else
                {
                    targetMaelstrom = Mathf.Lerp(currentMaelstrom, currentRatio, 0.1f);
                    Debug.Log($"Maelstrom Tgt : {targetMaelstrom}/{currentMaelstrom}");
                }
            }

            currentMaelstrom = Mathf.Lerp(currentMaelstrom, targetMaelstrom, 0.1f);

            return currentMaelstrom;
        }
    }
}