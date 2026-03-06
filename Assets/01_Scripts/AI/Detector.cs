using UnityEngine;
using System;
using System.Collections;

namespace HuntingGame.AI
{
    public class Detector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] protected LayerMask targetMask;
        [SerializeField] protected LayerMask obstacleMask;
        [SerializeField] protected float detectionInterval = .1f;

        public Action<Transform> _onPlayerSpotted;
        public Action<Transform> _onPlayerLost;

        protected Transform currentTarget;

        protected virtual void OnEnable()
        {
            StartCoroutine(DetectionLoop());
        }

        protected virtual void OnDisable()
        {
            StopAllCoroutines();
            currentTarget = null;
        }

        protected virtual IEnumerator DetectionLoop()
        {
            while (true)
            {
                Detect();
                yield return new WaitForSeconds(detectionInterval);
            }
        }

        protected virtual void Detect()
		{

		}

    }
}
