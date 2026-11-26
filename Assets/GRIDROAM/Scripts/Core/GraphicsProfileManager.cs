using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OurWorld.Core
{
    public enum GraphicsTier
    {
        Low = 0,
        Medium = 1
    }

    /// <summary>
    /// Manages shared Android & iOS Low and Medium URP graphics profiles at runtime.
    /// Low profile trims shadow distance, disables post-processing, lowers render scale,
    /// and notifies world systems (like traffic density) to keep a stable 30 FPS on both Android and iPhone.
    /// </summary>
    public class GraphicsProfileManager : MonoBehaviour
    {
        public static GraphicsProfileManager Instance { get; private set; }

        [Header("URP Pipeline Assets")]
        [SerializeField] private UniversalRenderPipelineAsset lowPipelineAsset;
        [SerializeField] private UniversalRenderPipelineAsset mediumPipelineAsset;

        [Header("Runtime Overrides")]
        [SerializeField] private int mobileTargetFps = 30;
        [SerializeField] private int editorTargetFps = 60;
        [SerializeField] private float lowRenderScale = 0.75f;
        [SerializeField] private float lowShadowDistance = 26f;
        [SerializeField] private float mediumRenderScale = 0.96f;
        [SerializeField] private float mediumShadowDistance = 58f;

        public GraphicsTier CurrentTier { get; private set; } = GraphicsTier.Medium;
        public event Action<GraphicsTier> OnTierChanged;

        public void ConfigureAssets(UniversalRenderPipelineAsset lowAsset, UniversalRenderPipelineAsset mediumAsset)
        {
            lowPipelineAsset = lowAsset;
            mediumPipelineAsset = mediumAsset;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            bool isMobileDevice = Application.platform == RuntimePlatform.Android ||
                                  Application.platform == RuntimePlatform.IPhonePlayer;
            Application.targetFrameRate = isMobileDevice ? mobileTargetFps : editorTargetFps;
            if (isMobileDevice)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            int savedTier = SaveManager.Instance != null ? SaveManager.Instance.Data.graphicsTierIndex : (int)GraphicsTier.Medium;
            ApplyTier((GraphicsTier)Mathf.Clamp(savedTier, 0, 1), persist: false);
        }

        public void CycleTier()
        {
            GraphicsTier next = CurrentTier == GraphicsTier.Low ? GraphicsTier.Medium : GraphicsTier.Low;
            ApplyTier(next, persist: true);
        }

        public void ApplyTier(GraphicsTier tier, bool persist = true)
        {
            CurrentTier = tier;
            bool isLow = tier == GraphicsTier.Low;

            UniversalRenderPipelineAsset targetAsset = isLow ? lowPipelineAsset : mediumPipelineAsset;
            if (targetAsset != null)
            {
                QualitySettings.renderPipeline = targetAsset;
                GraphicsSettings.defaultRenderPipeline = targetAsset;
                targetAsset.renderScale = isLow ? lowRenderScale : mediumRenderScale;
                targetAsset.shadowDistance = isLow ? lowShadowDistance : mediumShadowDistance;
            }
            else if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset activeUrp)
            {
                activeUrp.renderScale = isLow ? lowRenderScale : mediumRenderScale;
                activeUrp.shadowDistance = isLow ? lowShadowDistance : mediumShadowDistance;
            }

            QualitySettings.lodBias = isLow ? 0.75f : 1.0f;
            QualitySettings.maximumLODLevel = isLow ? 1 : 0;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                if (mainCam.farClipPlane < 960f)
                {
                    mainCam.farClipPlane = 960f;
                }
                UniversalAdditionalCameraData camData = mainCam.GetUniversalAdditionalCameraData();
                if (camData != null)
                {
                    camData.renderPostProcessing = !isLow;
                }
            }

            if (persist && SaveManager.Instance != null)
            {
                SaveManager.Instance.SetGraphicsTier((int)tier);
            }

            OnTierChanged?.Invoke(CurrentTier);
            Debug.Log($"[GraphicsProfileManager] Applied {CurrentTier} profile (TargetFPS={Application.targetFrameRate}).");
        }
    }
}
