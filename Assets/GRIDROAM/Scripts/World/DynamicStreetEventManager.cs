using OurWorld.Core;
using OurWorld.Jobs;
using OurWorld.Missions;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Spawns unscripted, optional neighborhood micro-events while exploring Hamidiye Mahallesi
    /// (dropped grocery bag, hungry stray cat needing food, lost courier parcel) so the open world
    /// always feels alive with small moments of kindness and extra rewards.
    /// </summary>
    public sealed class DynamicStreetEventManager : MonoBehaviour
    {
        private static readonly Vector3[] SafeSidewalkSpots =
        {
            new Vector3(-52.0f, 0.08f,   4.9f), // Selman No:52
            new Vector3( 36.0f, 0.08f,   4.9f), // Selman No:66
            new Vector3( 76.0f, 0.08f,  -4.9f), // Selman No:81S
            new Vector3( 82.0f, 0.08f, -26.5f), // Hz. Osman Camii
            new Vector3( 16.0f, 1.40f, -33.5f), // Swimming Pool Deck
            new Vector3( -6.0f, 0.08f, 131.5f), // Fatih Saat Kulesi
            new Vector3( 34.0f, 0.08f, 131.5f), // Fatih East
            new Vector3( 12.0f, 0.08f,  89.6f), // Atatürk Cd.
            new Vector3( -8.0f, 0.08f,  51.4f), // Mürşit Cd.
            new Vector3(-153.2f,0.08f,  -8.0f), // Eyyubi Cd.
            new Vector3(-90.2f, 0.08f,  42.0f), // Seyyid Kutup Cd.
            new Vector3( -8.0f, 0.08f, -71.5f), // Petrol Yolu Cd.
            new Vector3(-22.0f, 0.08f,-119.6f), // Aziziye School
            new Vector3(  8.0f, 0.08f,-151.4f), // Zübeyde Hanım Cd.
            new Vector3(-18.0f, 0.08f, 180.8f), // Havadis Sokağı · Taş Fırın
            new Vector3( 36.0f, 0.08f, 191.2f), // Havadis Sokağı · Doğu
            new Vector3(-14.0f, 0.08f,-180.8f), // Kadife Sokağı · Kıraathane
            new Vector3( 34.0f, 0.08f,-191.2f), // Kadife Sokağı · Doğu
            new Vector3(-116.0f,0.08f, -58.0f), // Hamidiye Atlas AVM · Ön Meydan
            new Vector3(-164.0f,0.08f,   6.0f), // Sultanbeyli Göleti İskelesi
            new Vector3( 150.0f,0.08f,   4.2f)  // Aydos Yeşil Vadi · Çiftlik
        };

        private float _cooldown = 55f;
        private GameObject _activeEventGo;
        private Transform _activeRingTransform;
        private Vector3 _activeEventPos;
        private float _activeTimeLeft;
        private int _activeKind; // 0 = grocery, 1 = cat food, 2 = courier parcel
        private int _activeReward;
        private Material _ringMat;
        private Material _propMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlayerModeController>() == null) return;
            if (FindAnyObjectByType<DynamicStreetEventManager>() != null) return;
            GameObject go = new GameObject("Hamidiye_DynamicStreetEvents");
            go.AddComponent<DynamicStreetEventManager>();
        }

        private void Awake()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _ringMat = new Material(sh);
            if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", new Color(0.22f, 0.90f, 0.85f));
            if (_ringMat.HasProperty("_EmissionColor"))
            {
                _ringMat.EnableKeyword("_EMISSION");
                _ringMat.SetColor("_EmissionColor", new Color(0.18f, 0.82f, 0.78f) * 0.9f);
            }
            _propMat = new Material(sh);
            if (_propMat.HasProperty("_BaseColor")) _propMat.SetColor("_BaseColor", new Color(0.92f, 0.56f, 0.20f));
        }

        private void OnDestroy()
        {
            if (_ringMat != null) Destroy(_ringMat);
            if (_propMat != null) Destroy(_propMat);
        }

        private void Update()
        {
            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.IsModalLocked) return;

            if (_activeEventGo != null)
            {
                _activeTimeLeft -= Time.deltaTime;
                float pulse = 1.0f + Mathf.Sin(Time.time * 4.5f) * 0.14f;
                if (_activeRingTransform != null) _activeRingTransform.localScale = new Vector3(1.55f * pulse, 0.02f, 1.55f * pulse);

                Vector3 diff = mode.ActiveWorldPosition - _activeEventPos;
                diff.y = 0f;
                if (diff.sqrMagnitude <= (mode.IsDriving ? 12.25f : 5.76f))
                {
                    CompleteEvent();
                    return;
                }

                if (_activeTimeLeft <= 0f)
                {
                    ClearActiveEvent();
                    _cooldown = Random.Range(75f, 115f);
                }
                return;
            }

            // Don't interrupt active story dialogues or active side jobs
            if (MissionManager.Instance != null && MissionManager.Instance.IsDialoguePlaying) return;
            if (JobManager.Instance != null && JobManager.Instance.HasActiveJob) return;

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;

            TrySpawnNearbyEvent(mode.ActiveWorldPosition);
        }

        private void TrySpawnNearbyEvent(Vector3 playerPos)
        {
            Vector3 bestSpot = Vector3.zero;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < SafeSidewalkSpots.Length; i++)
            {
                float d = Vector3.Distance(playerPos, SafeSidewalkSpots[i]);
                if (d >= 18f && d <= 68f && d < bestDist)
                {
                    bestDist = d;
                    bestSpot = SafeSidewalkSpots[i];
                    found = true;
                }
            }

            if (!found)
            {
                _cooldown = 25f;
                return;
            }

            _activeKind = Random.Range(0, 3);
            _activeReward = _activeKind == 2 ? 85 : (_activeKind == 0 ? 65 : 55);
            _activeEventPos = bestSpot;
            _activeTimeLeft = 65f;

            _activeEventGo = new GameObject("DynamicStreetEvent_Active");
            _activeEventGo.transform.SetParent(transform, false);
            _activeEventGo.transform.position = bestSpot;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "EventRing";
            ring.transform.SetParent(_activeEventGo.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            ring.transform.localScale = new Vector3(1.55f, 0.02f, 1.55f);
            _activeRingTransform = ring.transform;
            Destroy(ring.GetComponent<Collider>());
            Renderer rr = ring.GetComponent<Renderer>();
            if (rr != null) { rr.sharedMaterial = _ringMat; rr.shadowCastingMode = ShadowCastingMode.Off; }

            GameObject prop = GameObject.CreatePrimitive(_activeKind == 1 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            prop.name = "EventProp";
            prop.transform.SetParent(_activeEventGo.transform, false);
            prop.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            prop.transform.localScale = _activeKind == 1 ? new Vector3(0.36f, 0.12f, 0.36f) : new Vector3(0.42f, 0.34f, 0.32f);
            Destroy(prop.GetComponent<Collider>());
            Renderer pr = prop.GetComponent<Renderer>();
            if (pr != null) { pr.sharedMaterial = _propMat; pr.shadowCastingMode = ShadowCastingMode.Off; }

            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            string announcement = _activeKind switch
            {
                0 => isEn ? "Neighborhood Event: A neighbor dropped a grocery bag nearby! Help out for +65 ₺." : "Mahalle Olayı: Yakında bir komşunun pazar poşeti düştü! Yardım et (+65 ₺).",
                1 => isEn ? "Neighborhood Event: A stray cat needs food nearby! Visit the glowing bowl (+55 ₺)." : "Mahalle Olayı: Yakında bir sokak kedisi mama bekliyor! Parlayan kaba uğra (+55 ₺).",
                _ => isEn ? "Neighborhood Event: A courier dropped a parcel nearby! Recover it (+85 ₺)." : "Mahalle Olayı: Yakında bir kurye paketi düştü! Teslim al (+85 ₺)."
            };
            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(announcement);
            }
            GameFeelAudio.PlayUiClick();
        }

        private void CompleteEvent()
        {
            Vector3 burstPos = _activeEventPos;
            int reward = _activeReward;
            int kind = _activeKind;
            ClearActiveEvent();
            _cooldown = Random.Range(95f, 140f);

            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                SaveManager.Instance.Data.streetEventsHelpedCount++;
                SaveManager.Instance.AddMoney(reward);
            }

            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            string doneMsg = kind switch
            {
                0 => isEn ? $"Returned the grocery bag! Neighbor thanks you (+{reward} ₺)" : $"Pazar poşetini komşuya ulaştırdın! (+{reward} ₺)",
                1 => isEn ? $"Fed the street cat! Purring happily (+{reward} ₺)" : $"Sokak kedisinin karnı doydu! (+{reward} ₺)",
                _ => isEn ? $"Returned the express parcel! (+{reward} ₺)" : $"Düşen kargo paketini sahibine ulaştırdın! (+{reward} ₺)"
            };

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(doneMsg);
            }
            GameFeelAudio.PlayCoinChime();
            GameFeelAudio.SpawnBurst(burstPos + Vector3.up * 0.5f, new Color(0.28f, 0.92f, 0.85f), count: 12, spread: 0.65f);
            NeighborhoodReputation.AddReputation(25);
        }

        private void ClearActiveEvent()
        {
            if (_activeEventGo != null)
            {
                Destroy(_activeEventGo);
                _activeEventGo = null;
            }
        }
    }
}
