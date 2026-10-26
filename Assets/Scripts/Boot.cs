using UnityEngine;

namespace WobbleWaiter
{
    // The scene is empty on purpose: the whole game is created from code when it starts.
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindAnyObjectByType<Game>() != null) return;
            new GameObject("Wobble Waiter").AddComponent<Game>();
        }
    }
}
