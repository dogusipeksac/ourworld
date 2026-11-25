using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace OurWorld.World
{
    /// <summary>
    /// Lightweight, IL2CPP/iOS-safe runtime skeletal animation player built on Unity PlayableGraph
    /// + AnimationMixerPlayable. Loads all embedded AnimationClips from a Resources/OurWorldLife FBX
    /// and smoothly crossfades between Idle, Walk, Eating, Gallop, Happy, Swim, and Dabble clips
    /// without requiring pre-authored AnimatorController assets.
    /// </summary>
    public sealed class SkeletalClipPlayer : MonoBehaviour
    {
        private const string ResourceRoot = "OurWorldLife/";
        private static readonly Dictionary<string, AnimationClip[]> ClipCache = new Dictionary<string, AnimationClip[]>(16);

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly List<AnimationClip> _clips = new List<AnimationClip>(8);
        private readonly List<string> _shortNames = new List<string>(8);
        private readonly List<AnimationClipPlayable> _playables = new List<AnimationClipPlayable>(8);
        private readonly List<float> _weights = new List<float>(8);

        private int _activeIndex = -1;
        private float _fadeSpeed = 4f;
        private bool _isFading;
        private bool _graphValid;

        public bool HasClips => _graphValid && _clips.Count > 0;
        public string ActiveClipName => (_activeIndex >= 0 && _activeIndex < _clips.Count) ? _clips[_activeIndex].name : string.Empty;

        public bool Initialize(string resourceModelName, string defaultClip = "Idle", float startTimeOffset = 0f)
        {
            DisposeGraph();
            if (string.IsNullOrEmpty(resourceModelName)) return false;

            if (!ClipCache.TryGetValue(resourceModelName, out AnimationClip[] rawClips) || rawClips == null)
            {
                AnimationClip[] loaded = Resources.LoadAll<AnimationClip>(ResourceRoot + resourceModelName);
                List<AnimationClip> valid = new List<AnimationClip>(loaded.Length);
                for (int i = 0; i < loaded.Length; i++)
                {
                    AnimationClip c = loaded[i];
                    if (c == null) continue;
                    if (c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
                    valid.Add(c);
                }
                rawClips = valid.ToArray();
                ClipCache[resourceModelName] = rawClips;
            }

            if (rawClips.Length == 0) return false;

            Animator anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (anim == null) anim = gameObject.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            _clips.Clear();
            _shortNames.Clear();
            _playables.Clear();
            _weights.Clear();

            _graph = PlayableGraph.Create($"OW_Skel_{resourceModelName}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Animation", anim);
            _mixer = AnimationMixerPlayable.Create(_graph, rawClips.Length);
            output.SetSourcePlayable(_mixer);

            for (int i = 0; i < rawClips.Length; i++)
            {
                AnimationClip clip = rawClips[i];
                clip.wrapMode = WrapMode.Loop;
                AnimationClipPlayable cp = AnimationClipPlayable.Create(_graph, clip);
                cp.SetApplyFootIK(false);
                cp.SetApplyPlayableIK(false);
                if (startTimeOffset > 0f && clip.length > 0.05f)
                {
                    cp.SetTime(startTimeOffset % clip.length);
                }
                _graph.Connect(cp, 0, _mixer, i);
                _mixer.SetInputWeight(i, 0f);
                _clips.Add(clip);
                string cn = clip.name;
                int bar = cn.LastIndexOf('|');
                _shortNames.Add(bar >= 0 ? cn.Substring(bar + 1) : cn);
                _playables.Add(cp);
                _weights.Add(0f);
            }

            _graphValid = true;
            _graph.Play();

            int startIdx = FindClipIndex(defaultClip);
            if (startIdx < 0) startIdx = 0;
            _activeIndex = startIdx;
            _weights[startIdx] = 1f;
            _mixer.SetInputWeight(startIdx, 1f);
            _isFading = false;
            return true;
        }

        public bool PlayClip(string preferredName, float crossfadeSeconds = 0.25f, float speed = 1f)
        {
            if (!HasClips) return false;
            int idx = FindClipIndex(preferredName);
            if (idx < 0) return false;

            if (Math.Abs(_playables[idx].GetSpeed() - speed) > 0.001)
            {
                _playables[idx].SetSpeed(speed);
            }
            if (_activeIndex == idx) return true;

            _activeIndex = idx;
            _fadeSpeed = crossfadeSeconds > 0.01f ? (1f / crossfadeSeconds) : 100f;
            if (crossfadeSeconds <= 0.01f)
            {
                for (int i = 0; i < _weights.Count; i++)
                {
                    _weights[i] = i == idx ? 1f : 0f;
                    _mixer.SetInputWeight(i, _weights[i]);
                }
                _isFading = false;
            }
            else
            {
                _isFading = true;
            }
            return true;
        }

        public void SetActiveSpeed(float speed)
        {
            if (!HasClips || _activeIndex < 0 || _activeIndex >= _playables.Count) return;
            if (Math.Abs(_playables[_activeIndex].GetSpeed() - speed) > 0.001)
            {
                _playables[_activeIndex].SetSpeed(speed);
            }
        }

        public bool SetClipSpeed(string preferredName, float speed)
        {
            if (!HasClips) return false;
            int idx = FindClipIndex(preferredName);
            if (idx < 0) return false;
            if (Math.Abs(_playables[idx].GetSpeed() - speed) > 0.001)
            {
                _playables[idx].SetSpeed(speed);
            }
            return true;
        }

        public Transform FindBone(params string[] keywords)
        {
            if (keywords == null || keywords.Length == 0) return null;
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int k = 0; k < keywords.Length; k++)
            {
                string kw = keywords[k];
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && string.Equals(all[i].name, kw, StringComparison.OrdinalIgnoreCase))
                        return all[i];
                }
            }
            for (int k = 0; k < keywords.Length; k++)
            {
                string kw = keywords[k];
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        return all[i];
                }
            }
            return null;
        }

        private int FindClipIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            // Fast path: check currently active clip first (avoids scanning when already playing)
            if (_activeIndex >= 0 && _activeIndex < _clips.Count)
            {
                if (string.Equals(_clips[_activeIndex].name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_shortNames[_activeIndex], name, StringComparison.OrdinalIgnoreCase))
                {
                    return _activeIndex;
                }
            }
            // 1. Exact case-insensitive match
            for (int i = 0; i < _clips.Count; i++)
            {
                if (string.Equals(_clips[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            // 2. Cached suffix after '|' match (e.g. "AnimalArmature|Walk") — zero per-frame allocations
            for (int i = 0; i < _shortNames.Count; i++)
            {
                if (string.Equals(_shortNames[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            // 3. Contains match
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i].name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
            return -1;
        }

        private void Update()
        {
            if (!_graphValid || _activeIndex < 0) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_isFading)
            {
                float step = _fadeSpeed * dt;
                float total = 0f;
                bool anyMoving = false;
                for (int i = 0; i < _weights.Count; i++)
                {
                    float target = (i == _activeIndex) ? 1f : 0f;
                    float cur = _weights[i];
                    if (Mathf.Abs(cur - target) > 0.0001f)
                    {
                        cur = Mathf.MoveTowards(cur, target, step);
                        _weights[i] = cur;
                        if (Mathf.Abs(cur - target) > 0.0001f)
                        {
                            anyMoving = true;
                        }
                    }
                    total += Mathf.SmoothStep(0f, 1f, cur);
                }

                if (total > 0.0001f)
                {
                    float invTotal = 1f / total;
                    for (int i = 0; i < _weights.Count; i++)
                    {
                        float eased = Mathf.SmoothStep(0f, 1f, _weights[i]);
                        _mixer.SetInputWeight(i, eased * invTotal);
                    }
                }
                _isFading = anyMoving;
            }

            // Manually keep active & fading non-looping imported clips looping smoothly as a fail-safe
            for (int i = 0; i < _clips.Count; i++)
            {
                if (i != _activeIndex && _weights[i] <= 0.001f) continue;
                AnimationClip clip = _clips[i];
                if (clip != null && clip.length > 0.05f)
                {
                    double cur = _playables[i].GetTime();
                    if (cur >= clip.length)
                    {
                        _playables[i].SetTime(cur % clip.length);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            DisposeGraph();
        }

        private void DisposeGraph()
        {
            if (_graphValid && _graph.IsValid())
            {
                _graph.Destroy();
            }
            _graphValid = false;
            _isFading = false;
            _activeIndex = -1;
        }
    }
}
