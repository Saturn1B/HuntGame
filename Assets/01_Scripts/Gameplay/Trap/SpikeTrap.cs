using UnityEngine;
using DG.Tweening;

namespace HuntingGame.Gameplay
{
	public class SpikeTrap : MonoBehaviour
	{
		[SerializeField] private Transform spikePart;
		[SerializeField] private float triggerSpeed;
		[SerializeField] private Vector3 triggeredPosition;
		[SerializeField] private Ease animationEasing;
		private Vector3 hidePosition;

		private TriggerDetection triggerDetection;

		private void Awake()
		{
			triggerDetection = GetComponent<TriggerDetection>();
			hidePosition = spikePart.transform.localPosition;
		}

		private void OnEnable()
		{
			if(triggerDetection != null)
			{
				triggerDetection._onTriggerEnter += SpikeOut;
				triggerDetection._onTriggerExit += SpikeIn;
			}
		}

		private void OnDisable()
		{
			if (triggerDetection != null)
			{
				triggerDetection._onTriggerEnter -= SpikeOut;
				triggerDetection._onTriggerExit -= SpikeIn;
			}
		}

		private void SpikeIn() => ToggleSpike(false);
		private void SpikeOut() => ToggleSpike(true);

		private void ToggleSpike(bool value)
		{
			Vector3 targetPosition = value ? triggeredPosition : hidePosition;

			spikePart.transform.DOLocalMove(targetPosition, triggerSpeed).SetEase(animationEasing);
		}
	}
}
