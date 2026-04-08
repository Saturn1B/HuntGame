using System;
using UnityEngine;

namespace HuntingGame.Gameplay
{
    public class TriggerDetection : MonoBehaviour
    {
		private int playerInDetection = 0;

		public event Action _onTriggerEnter;
		public event Action _onTriggerExit;

		private void OnTriggerEnter(Collider other)
		{
			if (other.CompareTag("Player"))
				playerInDetection++;

			if (playerInDetection > 0)
				_onTriggerEnter?.Invoke();
		}

		private void OnTriggerExit(Collider other)
		{
			if (other.CompareTag("Player"))
				playerInDetection--;

			if (playerInDetection <= 0)
			{
				playerInDetection = 0;
				_onTriggerExit?.Invoke();
			}
		}
	}
}
