using UnityEngine;

namespace HuntGame.UI
{
    /// <summary>
    /// Marks a root GameObject as DontDestroyOnLoad. Used on the single EventSystem that should
    /// survive across the Bootstrap -> Elevator -> Taverne/Dungeon additive scene stack, instead
    /// of every scene shipping its own EventSystem (which causes duplicate-EventSystem warnings
    /// and unpredictable UI input routing once more than one is loaded at a time).
    /// </summary>
    public sealed class PersistAcrossScenes : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
