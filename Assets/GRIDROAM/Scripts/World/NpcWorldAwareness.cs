using System;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Missions;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Neighborhood-wide NPC event bus & smart Story NPC presence system:
    /// - Propagates violence / car-horn / sport-cheer events so nearby pedestrians react intelligently as a crowd.
    /// - Automatically upgrades all Story NPCs with eye contact (head-look IK), natural body turning,
    ///   mission-target beckoning waves + golden speech bubbles, and contextual greetings.
    /// </summary>
    public sealed class NpcWorldAwareness : MonoBehaviour
    {
        public static event Action<Vector3, Vector3, TownPedestrian> OnViolenceOccurred;
        public static event Action<Vector3, Vector3> OnHornHeard;
        public static event Action<Vector3> OnSportHighlight;

        private float _storyAttachTimer = 0.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            if (FindAnyObjectByType<NpcWorldAwareness>() != null) return;
            GameObject go = new GameObject("OurWorld_NpcWorldAwareness");
            go.AddComponent<NpcWorldAwareness>();
        }

        public static void NotifyViolence(Vector3 incidentPos, Vector3 attackerPos, TownPedestrian victim)
        {
            OnViolenceOccurred?.Invoke(incidentPos, attackerPos, victim);
        }

        public static void NotifySportHighlight(Vector3 arenaPos)
        {
            OnSportHighlight?.Invoke(arenaPos);
        }

        private void OnEnable()
        {
            GameFeelAudio.OnCarHornPlayed += HandleCarHornPlayed;
            SportMatchEvents.OnMatchFinished += HandleSportMatchFinished;
        }

        private void OnDisable()
        {
            GameFeelAudio.OnCarHornPlayed -= HandleCarHornPlayed;
            SportMatchEvents.OnMatchFinished -= HandleSportMatchFinished;
        }

        private void Update()
        {
            _storyAttachTimer -= Time.deltaTime;
            if (_storyAttachTimer <= 0f)
            {
                _storyAttachTimer = 6.0f;
                AttachSmartPresenceToStoryNpcs();
            }
        }

        private static void HandleCarHornPlayed()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null) return;
            Vector3 pos = mode.ActiveWorldPosition;
            Vector3 fwd = mode.CurrentVehicle != null
                ? mode.CurrentVehicle.transform.forward
                : (mode.FootController != null ? mode.FootController.transform.forward : Vector3.forward);
            OnHornHeard?.Invoke(pos, fwd);
        }

        private static void HandleSportMatchFinished(string sportId, bool playerWon)
        {
            if (!playerWon || PlayerModeController.Instance == null) return;
            OnSportHighlight?.Invoke(PlayerModeController.Instance.ActiveWorldPosition);
        }

        private static void AttachSmartPresenceToStoryNpcs()
        {
            InteractableTrigger[] triggers = FindObjectsByType<InteractableTrigger>(FindObjectsInactive.Exclude);
            for (int i = 0; i < triggers.Length; i++)
            {
                InteractableTrigger trig = triggers[i];
                if (trig == null || trig.Kind != InteractableKind.Npc) continue;
                if (trig.GetComponent<TownPedestrian>() != null) continue;
                if (trig.GetComponent<SmartStoryNpcPresence>() != null) continue;
                trig.gameObject.AddComponent<SmartStoryNpcPresence>().Initialize(trig.InteractableId);
            }
        }
    }

    /// <summary>
    /// Gives static Story NPCs (Usta Kemal, Elif Abla, Ayşe Teyze, Muhtar Remzi, Avukat Tarık,
    /// Antrenör Kerem, Elektrik Ustası Hasan, Gazeteci Defne, etc.) lifelike eye-contact,
    /// natural turning toward the player, beckoning waves when they are the active mission target,
    /// and subtle contextual greetings!
    /// </summary>
    public sealed class SmartStoryNpcPresence : MonoBehaviour
    {
        private string _anchorId;
        private ProceduralHumanoid _humanoid;
        private Quaternion _homeRotation;
        private float _checkTimer;
        private float _greetCooldown;

        private static readonly Dictionary<string, (string tr, string en)> SpeakerLookup =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["npc_mechanic"] = ("Usta Kemal", "Master Kemal"),
                ["npc_harbor_receiver"] = ("Elif Abla", "Elif"),
                ["npc_elena_neighborhood"] = ("Elif Abla", "Elif"),
                ["npc_ayse_teyze"] = ("Ayşe Teyze", "Auntie Ayşe"),
                ["npc_efe"] = ("Küçük Efe", "Little Efe"),
                ["npc_pharmacist"] = ("Eczacı Selin", "Pharmacist Selin"),
                ["npc_muhtar"] = ("Muhtar Remzi", "Mukhtar Remzi"),
                ["npc_muhtar_plaza"] = ("Muhtar Remzi", "Mukhtar Remzi"),
                ["npc_avukat_tarik"] = ("Avukat Tarık", "Lawyer Tarık"),
                ["npc_coach_kerem"] = ("Antrenör Kerem", "Coach Kerem"),
                ["npc_electrician_hasan"] = ("Usta Hasan", "Master Hasan"),
                ["npc_journalist_defne"] = ("Gazeteci Defne", "Journalist Defne")
            };

        public void Initialize(string anchorId)
        {
            _anchorId = anchorId ?? string.Empty;
            _humanoid = GetComponentInChildren<ProceduralHumanoid>(true);
            _homeRotation = transform.rotation;
            _checkTimer = UnityEngine.Random.Range(0.2f, 0.8f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_greetCooldown > 0f) _greetCooldown -= dt;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked) return;

            Vector3 playerPos = mode.ActiveWorldPosition;
            Vector3 toPlayer = playerPos - transform.position;
            toPlayer.y = 0f;
            float distSq = toPlayer.sqrMagnitude;

            if (distSq <= 100f) // Within 10 m
            {
                if (_humanoid != null)
                {
                    _humanoid.SetLookAtPoint(true, playerPos + Vector3.up * 1.55f);
                }

                if (distSq <= 49f && toPlayer.sqrMagnitude > 0.04f) // Within 7 m turn smoothly toward player
                {
                    Quaternion lookRot = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 1f - Mathf.Exp(-5.5f * dt));
                }

                _checkTimer -= dt;
                if (_checkTimer <= 0f)
                {
                    _checkTimer = 0.65f;
                    EvaluateGreeting(Mathf.Sqrt(distSq));
                }
            }
            else
            {
                if (_humanoid != null) _humanoid.SetLookAtPoint(false, Vector3.zero);
                transform.rotation = Quaternion.Slerp(transform.rotation, _homeRotation, 1f - Mathf.Exp(-2.2f * dt));
            }
        }

        private void EvaluateGreeting(float dist)
        {
            if (_greetCooldown > 0f || dist > 9.5f) return;

            MissionManager mm = MissionManager.Instance;
            MissionStepData step = mm != null ? mm.CurrentStep : null;
            bool isMissionTarget = step != null &&
                (string.Equals(step.targetAnchorId, _anchorId, StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrEmpty(step.secondaryTargetAnchorId) &&
                  string.Equals(step.secondaryTargetAnchorId, _anchorId, StringComparison.OrdinalIgnoreCase)));

            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            string speakerName = GetSpeakerName(isEn);

            if (isMissionTarget && dist <= 9.2f)
            {
                _greetCooldown = 14f;
                if (_humanoid != null) _humanoid.PlayWave();
                string callout = isEn
                    ? "Deniz! Over here — let's talk!"
                    : "Deniz! Buraya gel, seni bekliyordum!";
                NpcSpeechBubbleManager.Say(transform, speakerName, callout, 3.8f, NpcBubbleMood.Mission);
            }
            else if (dist <= 5.8f)
            {
                _greetCooldown = 28f;
                if (_humanoid != null) _humanoid.PlayWave();
                float h = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentHour : 12f;
                string greet;
                if (isEn)
                {
                    greet = h < 11.5f ? "Good morning, Deniz!"
                          : h < 17.5f ? "Good to see you around, Deniz!"
                          : h < 20.5f ? "Lovely evening in Hamidiye, Deniz."
                          : "Out late tonight, Deniz? Stay safe!";
                }
                else
                {
                    greet = h < 11.5f ? "Günaydın Deniz, hayırlı sabahlar!"
                          : h < 17.5f ? "Selam Deniz, mahallede her şey yolunda mı?"
                          : h < 20.5f ? "İyi akşamlar Deniz, gün batımı harika!"
                          : "İyi geceler Deniz, geç saate kalmışsın!";
                }
                NpcSpeechBubbleManager.Say(transform, speakerName, greet, 3.2f, NpcBubbleMood.Friendly);
            }
        }

        private string GetSpeakerName(bool isEn)
        {
            if (SpeakerLookup.TryGetValue(_anchorId, out var pair))
            {
                return isEn ? pair.en : pair.tr;
            }
            return isEn ? "Neighbor" : "Komşu";
        }
    }
}
