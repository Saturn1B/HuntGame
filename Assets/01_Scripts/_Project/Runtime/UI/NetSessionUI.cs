using UnityEngine;
using TMPro;
using DungeonSteakhouse.Net.Session;

namespace DungeonSteakhouse.Net.UI
{
    public sealed class NetSessionUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private GameObject countdownPanel;

        private void Update()
        {
            var session = NetSessionManager.Instance;
            if (session == null || session.ReturnCountdownRemaining <= 0f)
            {
                countdownPanel?.SetActive(false);
                return;
            }

            countdownPanel?.SetActive(true);
            if (countdownText != null)
                countdownText.text = $"Returning in {Mathf.CeilToInt(session.ReturnCountdownRemaining)}...";
        }
    }
}
