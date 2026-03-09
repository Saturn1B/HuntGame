using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace DungeonSteakhouse.Net.Session
{
    public sealed class NetElevatorDescentController : NetworkBehaviour
    {
        [Header("Local Movement")]
        [SerializeField] private Vector3 localAxis = Vector3.down;

        private Coroutine _routine;
        private bool _isMoving;

        public bool IsMoving => _isMoving;

        public void ServerMoveBy(float distance, float duration)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[NetElevatorDescentController] ServerMoveBy called on a non-server instance.");
                return;
            }

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(MoveRoutine(distance, duration));
        }

        private IEnumerator MoveRoutine(float distance, float duration)
        {
            _isMoving = true;

            Vector3 start = transform.localPosition;
            Vector3 end = start + localAxis.normalized * distance;

            duration = Mathf.Max(0.01f, duration);

            float t = 0f;
            while (t < duration)
            {
                float a = t / duration;
                a = a * a * (3f - 2f * a); // SmoothStep easing

                transform.localPosition = Vector3.Lerp(start, end, a);

                t += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = end;

            _isMoving = false;
            _routine = null;
        }
    }
}