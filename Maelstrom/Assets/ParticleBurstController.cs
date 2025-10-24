using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Maelstrom.Unity
{
    public class ParticleController : MonoBehaviour {
    void OnMouseDown()
        {

			var emission = GetComponent<ParticleSystem>().emission;
			emission.rateOverTime = 500;
            emission.enabled = true;
			GetComponent<ParticleSystem>().Emit(10);
				GetComponent<ParticleSystem>().Play();

			Debug.Log("clicked");
        }

	void Start () {
			// You can use particleSystem instead of
			// gameObject.particleSystem.
			// They are the same, if I may say so
			var emission = GetComponent<ParticleSystem>().emission;
			emission.rateOverTime = 0;
	}
	
	void Update () {
	}
}
}