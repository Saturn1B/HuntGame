using System;
using UnityEngine;

namespace HuntingGame
{
    public class Health : MonoBehaviour
    {
        [SerializeField, Range(1, 20)] private int healthPoint;
         private int currentHealthPoint;

        public Action<int> _onChangeHealthValue;
        public Action _onDeath;

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
            if (currentHealthPoint <= 0) return;

            if (Mathf.Abs(value) >= currentHealthPoint)
                currentHealthPoint = 0;
            else
                healthPoint += value;

            _onChangeHealthValue?.Invoke(currentHealthPoint);

            if (currentHealthPoint <= 0)
                _onDeath?.Invoke();
		}
    }
}
