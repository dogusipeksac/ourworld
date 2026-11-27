using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Keeps every uGUI label on the bundled type family (Hanken Grotesk / Sora) — including
    /// scene-baked HUD text, runtime-built panels and late-spawned overlays that still ask for
    /// the built-in Arial/LegacyRuntime font. Runs a cheap periodic sweep (reference compare
    /// per label) instead of touching every panel's builder.
    /// </summary>
    public sealed class OurWorldFontUpgrader : MonoBehaviour
    {
        private const float SweepInterval = 1.0f;
        private static OurWorldFontUpgrader _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("OurWorld_FontUpgrader");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<OurWorldFontUpgrader>();
        }

        /// <summary>Immediately upgrades all labels below a root (call right after building a panel).</summary>
        public static void UpgradeHierarchy(Transform root)
        {
            if (root == null) return;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) OurWorldUiKit.UpgradeFont(texts[i]);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private IEnumerator Start()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(SweepInterval);
            // First sweep right after scene start so the HUD never shows Arial for long
            yield return null;
            while (true)
            {
                Sweep();
                yield return wait;
            }
        }

        private static void Sweep()
        {
            Text[] all = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++) OurWorldUiKit.UpgradeFont(all[i]);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
