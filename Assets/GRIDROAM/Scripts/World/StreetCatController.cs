using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Beloved roaming Istanbul neighborhood street cat (Sarman, Tekir, Pamuk, Kömür, Badem, Huzur):
    /// gently strolls near its home sidewalk or mosque courtyard, watches nearby pedestrians & player,
    /// and lets the player pet it (E) for purring meow audio, golden sparkles, and +12 Energy!
    /// </summary>
    public class StreetCatController : MonoBehaviour
    {
        [SerializeField] private string catName = "Sarman";
        [SerializeField] private Color furColor = new Color(0.86f, 0.56f, 0.28f, 1f);
        [SerializeField] private float wanderRadius = 3.2f;
        [SerializeField] private float walkSpeed = 1.05f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly string[] PetMessages =
        {
            "{0} mırıldanarak bacağına sürtündü! (+12 Enerji)",
            "{0} başını eline yasladı, keyifle mırlıyor! (+12 Enerji)",
            "{0} kuyruğunu dikti ve sana eşlik ediyor! (+12 Enerji)",
            "{0} karnını açtı, Sultanbeyli'nin en uysal kedisi! (+12 Enerji)"
        };

        private Vector3 _homePos;
        private Vector3 _targetPos;
        private float _stateTimer;
        private bool _isWalking;
        private float _hopTimer = -1f;
        private float _lastEnergyPetTime = -99f;
        private float _animPhase;

        public void Configure(string displayName, Color coatColor, float radius = 3.2f)
        {
            catName = displayName;
            furColor = coatColor;
            wanderRadius = Mathf.Max(1.0f, radius);
            ApplyFurColor();
        }

        private void Start()
        {
            _homePos = transform.position;
            _targetPos = _homePos;
            _stateTimer = UnityEngine.Random.Range(1.5f, 4.5f);
            _animPhase = UnityEngine.Random.Range(0f, 10f);
            ApplyFurColor();
        }

        private void ApplyFurColor()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                string n = r.gameObject.name;
                bool isFurPart = n.Contains("Body") || n.Contains("Head") || n.Contains("Ear") ||
                                 n.Contains("Leg") || (n.Contains("Tail") && !n.Contains("TailTip"));
                if (!isFurPart) continue;

                Material[] mats = r.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && mats[m].HasProperty(BaseColorId))
                    {
                        mats[m].SetColor(BaseColorId, furColor);
                    }
                }
                r.materials = mats;
            }
        }

        public void Pet(PlayerModeController mode)
        {
            if (mode == null) return;

            _isWalking = false;
            _stateTimer = 4.5f;
            _hopTimer = 0f;

            // Face player warmly
            Vector3 toPlayer = mode.ActiveWorldPosition - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.04f)
            {
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            }

            GameFeelAudio.PlayCatMeow();
            GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.42f, new Color(1.0f, 0.78f, 0.32f), count: 10, spread: 0.42f);

            if (Time.time - _lastEnergyPetTime >= 10f)
            {
                _lastEnergyPetTime = Time.time;
                if (PlayerNeedsController.Instance != null)
                {
                    PlayerNeedsController.Instance.RestoreEnergy(12f);
                }
                string msg = string.Format(PetMessages[UnityEngine.Random.Range(0, PetMessages.Length)], catName);
                mode.RaiseStatusMessage(msg);
            }
            else
            {
                mode.RaiseStatusMessage($"{catName} seni görünce keyifle mırlıyor!");
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _animPhase += dt;
            _stateTimer -= dt;

            if (_hopTimer >= 0f)
            {
                _hopTimer += dt;
                const float hopDur = 0.36f;
                if (_hopTimer >= hopDur)
                {
                    _hopTimer = -1f;
                    Vector3 p = transform.position;
                    p.y = _homePos.y;
                    transform.position = p;
                }
                else
                {
                    float env = Mathf.Sin((_hopTimer / hopDur) * Mathf.PI);
                    Vector3 p = transform.position;
                    p.y = _homePos.y + env * 0.22f;
                    transform.position = p;
                }
                return;
            }

            if (_isWalking)
            {
                Vector3 toTarget = _targetPos - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                if (dist < 0.18f || _stateTimer <= 0f)
                {
                    _isWalking = false;
                    _stateTimer = UnityEngine.Random.Range(2.5f, 6.0f);
                }
                else
                {
                    Vector3 dir = toTarget / dist;
                    Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-6f * dt));
                    Vector3 nextPos = transform.position + dir * (walkSpeed * dt);
                    nextPos.y = _homePos.y + Mathf.Abs(Mathf.Sin(_animPhase * 9.5f)) * 0.025f;
                    transform.position = nextPos;
                }
            }
            else
            {
                // Gentle idle breathing & looking toward nearby player
                Vector3 p = transform.position;
                p.y = _homePos.y;
                transform.position = p;

                if (PlayerModeController.Instance != null && !PlayerModeController.Instance.IsDriving)
                {
                    Vector3 toPlayer = PlayerModeController.Instance.ActiveWorldPosition - transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude < 12f && toPlayer.sqrMagnitude > 0.25f)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 1f - Mathf.Exp(-3.5f * dt));
                    }
                }

                if (_stateTimer <= 0f)
                {
                    Vector2 rnd = UnityEngine.Random.insideUnitCircle * wanderRadius;
                    _targetPos = _homePos + new Vector3(rnd.x, 0f, rnd.y);
                    _isWalking = true;
                    _stateTimer = UnityEngine.Random.Range(2.2f, 4.5f);
                }
            }
        }
    }
}
