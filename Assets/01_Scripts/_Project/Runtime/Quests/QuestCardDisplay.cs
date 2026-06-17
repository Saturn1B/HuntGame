using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuntingGame.Quests
{
    public class QuestCardDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject iconRoot;

        public void Setup(QuestData data)
        {
            titleText.text = data.title;
            descriptionText.text = data.description;
            rewardText.text = $"{data.goldReward} 🪙";

            bool hasIcon = data.icon != null;
            if (iconRoot != null)
                iconRoot.SetActive(hasIcon);

            if (hasIcon && iconImage != null)
                iconImage.sprite = data.icon;
        }
    }
}
