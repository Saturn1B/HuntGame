using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonSteakhouse.Net.UI
{
    public sealed class EscapeMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private GameObject friendsListPanelRoot;
        [SerializeField] private Button inviteFriendButton;
        [SerializeField] private Button closeFriendsListButton;

        private void Awake()
        {
            inviteFriendButton.onClick.AddListener(() => friendsListPanelRoot.SetActive(true));
            closeFriendsListButton.onClick.AddListener(() => friendsListPanelRoot.SetActive(false));
            menuRoot.SetActive(false);
            friendsListPanelRoot.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ToggleMenu();
        }

        private void ToggleMenu()
        {
            bool willOpen = !menuRoot.activeSelf;
            menuRoot.SetActive(willOpen);
            if (!willOpen) friendsListPanelRoot.SetActive(false);
        }
    }
}
