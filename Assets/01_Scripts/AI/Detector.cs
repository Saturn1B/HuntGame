using UnityEngine;
using System;
using System.Collections;

namespace HuntingGame.AI
{
    public abstract class Detector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] protected LayerMask targetMask;
        [SerializeField] protected LayerMask obstacleMask;
        [SerializeField] protected float detectionInterval = .1f;

        public event Action<Transform> _onPlayerSpotted;
        public event Action<Transform> _onPlayerLost;

        protected void InvokePlayerSpotted(Transform target) => _onPlayerSpotted?.Invoke(target);
        protected void InvokePlayerLost(Transform target) => _onPlayerLost?.Invoke(target);

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

        protected abstract void Detect();

    }
}
