using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

namespace HuntingGame
{
    public class CameraShakeManager : MonoBehaviour
    {
        public static CameraShakeManager Instance { get; private set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(this);
			}
			else
			{
				Instance = this;
			}
		}

		[Header("Standard Shake Settings")]
		[SerializeField] private float defaultDuration = 0.2f;
		[SerializeField] private float defaultStrength = 0.1f;
		[SerializeField] private int defaultVibrato = 20;

		public void Shake(float strength = -1, float duration = -1)
		{
			float s = strength < 0 ? defaultStrength : strength;
			float d = duration < 0 ? defaultDuration : duration;

			transform.DOComplete();
			transform.DOShakePosition(d, s, defaultVibrato, 90, false, true);
		}

		public void HeavyShake()
		{
			transform.DOComplete();
			transform.DOShakePosition(0.5f, 0.3f, 30, 90);
			transform.DOShakeRotation(0.5f, 2f, 30);
		}

		public void TinyThud()
		{
			transform.DOComplete();
			transform.DOPunchPosition(new Vector3(0, -0.05f, 0), 0.15f, 10, 1);
		}
	}
}
