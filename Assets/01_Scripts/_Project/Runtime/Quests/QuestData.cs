using UnityEngine;

namespace HuntingGame.Quests
{
    public enum QuestObjectiveType
    {
        KillEnemy,
        CollectItem,
        Explore
    }

    [CreateAssetMenu(menuName = "HuntingGame/Quest")]
    public class QuestData : ScriptableObject
    {
        public string title;
        public string description;
        public Sprite icon;
        public int goldReward;
        public QuestObjectiveType objectiveType;
        public int objectiveTargetCount;
        // Used for future tracking: enemy name or item id
        public string objectiveTargetId;
    }
}
