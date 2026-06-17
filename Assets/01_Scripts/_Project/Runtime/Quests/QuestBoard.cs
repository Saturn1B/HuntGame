using UnityEngine;

namespace HuntingGame.Quests
{
    [DisallowMultipleComponent]
    public class QuestBoard : MonoBehaviour
    {
        [SerializeField] private QuestData[] quests = new QuestData[3];
        [SerializeField] private QuestCardDisplay[] cardDisplays = new QuestCardDisplay[3];
        [SerializeField] private bool verboseLogs;

        private void Start()
        {
            int count = Mathf.Min(quests.Length, cardDisplays.Length);
            for (int i = 0; i < count; i++)
            {
                if (quests[i] == null || cardDisplays[i] == null)
                {
                    if (verboseLogs)
                        Debug.Log($"[QuestBoard] Slot {i} skipped (quest or display is null).");
                    continue;
                }

                cardDisplays[i].Setup(quests[i]);

                if (verboseLogs)
                    Debug.Log($"[QuestBoard] Slot {i} initialized: {quests[i].title}");
            }
        }
    }
}
