using System;
using UnityEngine;

namespace HuntingGame
{
    public class Health : MonoBehaviour
    {
        [SerializeField, Range(1, 20)] private int healthPoint;
         private int currentHealthPoint;

        public event Action<int> _onChangeHealthValue;
        public event Action _onDeath;

		private void Start()
		{
            currentHealthPoint = healthPoint;
		}

		public int GetCurrentHealth()
		{
            return currentHealthPoint;
		}

        public void ChangeHealth(int value)
		{
            if (value < 0 && transform.GetComponentInChildren<CameraShakeManager>() != null)
                transform.GetComponentInChildren<CameraShakeManager>().Shake(.5f, .5f);

            if (currentHealthPoint <= 0) return;

            if (Mathf.Abs(value) >= currentHealthPoint)
                currentHealthPoint = 0;
            else
                currentHealthPoint += value;

            _onChangeHealthValue?.Invoke(currentHealthPoint);

            if (currentHealthPoint <= 0)
                _onDeath?.Invoke();
		}
    }
}
