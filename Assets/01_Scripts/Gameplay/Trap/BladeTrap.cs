using UnityEngine;
using DG.Tweening;

namespace HuntingGame.Gameplay
{
    public class BladeTrap : MonoBehaviour
    {
		[SerializeField] private Transform bladePart;
		[SerializeField] private float triggerSpeed;
		[SerializeField] private Vector3 triggeredEndRotation;
		[SerializeField] private Ease animationEasing;
		private Vector3 startRotation;

		private TriggerDetection triggerDetection;

		private void Awake()
		{
			triggerDetection = GetComponent<TriggerDetection>();
			startRotation = bladePart.transform.localEulerAngles;
		}

		private void OnEnable()
		{
			if (triggerDetection != null)
			{
				triggerDetection._onTriggerEnter += ToggleBlade;
			}
		}

		private void OnDisable()
		{
			if (triggerDetection != null)
			{
				triggerDetection._onTriggerEnter -= ToggleBlade;
			}
		}

		private void ToggleBlade()
		{
			bladePart.DOLocalRotate(triggeredEndRotation, triggerSpeed).SetEase(animationEasing).OnComplete(() =>
			{
				bladePart.DOLocalRotate(startRotation, triggerSpeed).SetEase(animationEasing);
			});
		}
	}
}
