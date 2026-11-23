using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Tints only the exterior wall renderers of a residential house via MaterialPropertyBlock
    /// (preserving underlying brick/plaster/facade PBR textures) with a 1.2 s animated blend,
    /// persisting across sessions through SaveManager and notifying JobManager for painting jobs.
    /// </summary>
    public class PaintableHouse : MonoBehaviour
    {
        public static event Action<PaintableHouse, Color> OnAnyHousePainted;

        [SerializeField] private string houseId = "house_01";
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private List<Renderer> exteriorWallRenderers = new List<Renderer>();
        [SerializeField] private Vector3 frontDoorLocalOffset = new Vector3(4.5f, 0.1f, 0f);
        [SerializeField] private float blendDurationSeconds = 1.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _mpb;
        private Color _defaultColor = Color.white;
        private Color _currentColor = Color.white;
        private bool _hasCustomColor;
        private Coroutine _blendRoutine;

        public string HouseId => houseId;
        public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName : houseId;
        public Color CurrentColor => _currentColor;
        public Vector3 FrontDoorWorldPosition => transform.TransformPoint(frontDoorLocalOffset);

        public void Configure(string id, List<Renderer> exteriorRenderers, string label = null, float halfDepthX = 3.8f)
        {
            houseId = id;
            exteriorWallRenderers = exteriorRenderers != null ? exteriorRenderers : new List<Renderer>();
            if (!string.IsNullOrEmpty(label))
            {
                displayName = label;
            }
            frontDoorLocalOffset = new Vector3(halfDepthX + 0.85f, 0.12f, 0f);
        }

        public void ConfigureWithLocalFront(string id, List<Renderer> exteriorRenderers, string label, Vector3 explicitFrontDoorLocal)
        {
            houseId = id;
            exteriorWallRenderers = exteriorRenderers != null ? exteriorRenderers : new List<Renderer>();
            if (!string.IsNullOrEmpty(label))
            {
                displayName = label;
            }
            frontDoorLocalOffset = explicitFrontDoorLocal;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            CacheDefaultColor();
        }

        private void Start()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveUpdated += HandleSaveUpdated;
                if (SaveManager.Instance.TryGetHouseColor(houseId, out Color saved))
                {
                    _currentColor = saved;
                    _hasCustomColor = true;
                    ApplyColorImmediate(_currentColor);
                }
            }
        }

        private void OnDestroy()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveUpdated -= HandleSaveUpdated;
            }
        }

        private void HandleSaveUpdated(SaveData data)
        {
            if (data == null || data.housePaint == null) return;
            if (_hasCustomColor && SaveManager.Instance != null && !SaveManager.Instance.TryGetHouseColor(houseId, out _))
            {
                _hasCustomColor = false;
                _currentColor = _defaultColor;
                ClearPropertyBlocks();
            }
        }

        private void CacheDefaultColor()
        {
            if (exteriorWallRenderers == null) return;
            for (int i = 0; i < exteriorWallRenderers.Count; i++)
            {
                Renderer r = exteriorWallRenderers[i];
                if (r != null && r.sharedMaterial != null)
                {
                    Material m = r.sharedMaterial;
                    if (m.HasProperty(BaseColorId))
                    {
                        _defaultColor = m.GetColor(BaseColorId);
                    }
                    else if (m.HasProperty(LegacyColorId))
                    {
                        _defaultColor = m.GetColor(LegacyColorId);
                    }
                    _currentColor = _defaultColor;
                    return;
                }
            }
        }

        public void ApplyColor(Color targetColor)
        {
            targetColor.a = 1f;
            _hasCustomColor = true;

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.housesPaintedCount++;
                SaveManager.Instance.SetHouseColor(houseId, targetColor);
            }

            OnAnyHousePainted?.Invoke(this, targetColor);

            if (!isActiveAndEnabled)
            {
                _currentColor = targetColor;
                ApplyColorImmediate(targetColor);
                return;
            }

            if (_blendRoutine != null)
            {
                StopCoroutine(_blendRoutine);
            }

            _blendRoutine = StartCoroutine(BlendColorRoutine(_currentColor, targetColor));
        }

        private IEnumerator BlendColorRoutine(Color fromColor, Color toColor)
        {
            float duration = Mathf.Max(0.05f, blendDurationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _currentColor = Color.Lerp(fromColor, toColor, t);
                ApplyColorImmediate(_currentColor);
                yield return null;
            }

            _currentColor = toColor;
            ApplyColorImmediate(toColor);
            _blendRoutine = null;
        }

        private void ApplyColorImmediate(Color c)
        {
            if (exteriorWallRenderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < exteriorWallRenderers.Count; i++)
            {
                Renderer r = exteriorWallRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void ClearPropertyBlocks()
        {
            if (exteriorWallRenderers == null) return;
            for (int i = 0; i < exteriorWallRenderers.Count; i++)
            {
                Renderer r = exteriorWallRenderers[i];
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
