using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.World
{
    /// <summary>
    /// Installs realistic swimming & aquatic sports across Our World at runtime:
    /// 1. Olympic 3-Lane 50m Swimming Race (LiveSwimRace) at Hamidiye Açık Yüzme Havuzu (16, 0, -40)
    ///    with floating lane ropes, 2 animated NPC rival swimmers, Dive-Start Reaction bar,
    ///    Rhythmic Stroke Boost bar, Freestyle/Breaststroke toggle, and Wall Flip Turns!
    /// 2. Acrobatic Springboard Diving (AcrobaticDivingBoard) using Blender 5.3 OW_DivingBoard.fbx
    ///    with spring charge, mid-air somersault/twist combos & live Jury Scoring (0.0 - 10.0)!
    /// 3. Swimmable WaterZone at Sultanbeyli Göleti (-198, 0, 0) + collectible glowing underwater pearls!
    /// </summary>
    public sealed class SwimWorldInstaller : MonoBehaviour
    {
        public static SwimWorldInstaller Instance { get; private set; }
        public static LiveSwimRace ActiveSwimRace { get; private set; }
        public static AcrobaticDivingBoard ActiveDivingBoard { get; private set; }

        private static readonly Vector3 PoolCenter = new Vector3(16.0f, 0.0f, -40.0f);
        private static readonly Vector3 LakeCenter = new Vector3(-198.0f, 0.0f, 0.0f);

        private readonly List<GameObject> _pearls = new List<GameObject>(8);
        private readonly List<Material> _ownedMats = new List<Material>(8);
        private Material _pearlMat;
        private bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<PlayerModeController>() == null) return;

            GameObject go = new GameObject("OurWorld_AquaticSportsAndLakeSwim");
            go.AddComponent<SwimWorldInstaller>();
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

        private IEnumerator Start()
        {
            yield return null;
            BuildPoolRaceAndDivingBoard();
            BuildLakeSwimmableZoneAndPearls();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandlePearlInteracted;
                PlayerModeController.Instance.RefreshInteractablesCache();
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandlePearlInteracted;
            }
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Gently bob & shimmer uncollected underwater pearls
            float t = Time.time;
            for (int i = 0; i < _pearls.Count; i++)
            {
                GameObject p = _pearls[i];
                if (p == null || !p.activeSelf) continue;
                p.transform.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);
                Vector3 pos = p.transform.localPosition;
                pos.y += Mathf.Sin(t * 2.6f + i * 1.4f) * 0.0012f;
                p.transform.localPosition = pos;
            }
        }

        private void BuildPoolRaceAndDivingBoard()
        {
            GameObject raceRoot = new GameObject("Hamidiye_SwimRaceArena");
            raceRoot.transform.SetParent(transform, false);
            raceRoot.transform.position = PoolCenter;

            // Floating Olympic Red-White-Blue Lane Ropes dividing the 3 lanes (z = -1.35 and z = +1.35)
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Material buoyRed = new Material(lit) { color = new Color(0.92f, 0.20f, 0.18f) };
            Material buoyWhite = new Material(lit) { color = new Color(0.96f, 0.97f, 0.99f) };
            Material buoyBlue = new Material(lit) { color = new Color(0.14f, 0.48f, 0.90f) };
            _ownedMats.Add(buoyRed);
            _ownedMats.Add(buoyWhite);
            _ownedMats.Add(buoyBlue);

            float[] ropeZs = { -1.35f, 1.35f };
            for (int r = 0; r < ropeZs.Length; r++)
            {
                GameObject rope = new GameObject($"OlympicLaneRope_{r}");
                rope.transform.SetParent(raceRoot.transform, false);
                for (int b = 0; b < 26; b++)
                {
                    float bx = Mathf.Lerp(-6.5f, 6.5f, b / 25f);
                    GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    buoy.name = $"Buoy_{b}";
                    Destroy(buoy.GetComponent<Collider>());
                    buoy.transform.SetParent(rope.transform, false);
                    buoy.transform.localPosition = new Vector3(bx, 1.18f, ropeZs[r]);
                    buoy.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    buoy.transform.localScale = new Vector3(0.13f, 0.18f, 0.13f);
                    MeshRenderer mr = buoy.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        mr.sharedMaterial = (b < 3 || b > 22) ? buoyRed : (b % 2 == 0 ? buoyWhite : buoyBlue);
                    }
                }
            }

            LiveSwimRace race = raceRoot.AddComponent<LiveSwimRace>();
            ActiveSwimRace = race;

            // Starting trigger stand on West/North pool entrance deck
            GameObject raceTrigGo = new GameObject("SwimRace_StartTrigger");
            raceTrigGo.transform.SetParent(raceRoot.transform, false);
            raceTrigGo.transform.localPosition = new Vector3(-7.6f, 1.45f, 3.6f);
            InteractableTrigger raceTrig = raceTrigGo.AddComponent<InteractableTrigger>();
            raceTrig.Configure(InteractableKind.SportsArena, "sport_swim_race", "ui.btn.interact", 3.4f);
            raceTrig.SetCustomPrompt(
                "50m Yüzme Yarışı Başlat (3 Kulvar · +95 ₺)",
                "Start 50m Swim Race (3 Lanes · +95 ₺)");

            // Blender 5.3.0 Olympic Springboard Diving Stand on South-West deck edge facing +X into pool!
            GameObject diveRoot = new GameObject("Hamidiye_DivingBoardArena");
            diveRoot.transform.SetParent(transform, false);
            diveRoot.transform.position = PoolCenter + new Vector3(-7.6f, 1.38f, 0.0f);
            diveRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            OurWorldModelLibrary.InstantiateModel("OW_DivingBoard", diveRoot.transform, Vector3.zero, Quaternion.identity, Vector3.one);

            AcrobaticDivingBoard diving = diveRoot.AddComponent<AcrobaticDivingBoard>();
            diving.ConfigurePool(PoolCenter, waterY: 1.18f);
            ActiveDivingBoard = diving;

            GameObject diveTrigGo = new GameObject("DivingBoard_Trigger");
            diveTrigGo.transform.SetParent(diveRoot.transform, false);
            diveTrigGo.transform.localPosition = new Vector3(0f, 0.6f, -1.1f);
            InteractableTrigger diveTrig = diveTrigGo.AddComponent<InteractableTrigger>();
            diveTrig.Configure(InteractableKind.SportsArena, "sport_diving_board", "ui.btn.interact", 2.9f);
            diveTrig.SetCustomPrompt(
                "Tramplenden Akrobatik Dalış Yap! (Jüri Puanlı)",
                "Acrobatic Springboard Dive! (Judged Score)");
        }

        private void BuildLakeSwimmableZoneAndPearls()
        {
            // Make Sultanbeyli Lake genuinely swimmable!
            GameObject lakeZoneGo = new GameObject("SultanbeyliLake_SwimmableWaterZone");
            lakeZoneGo.transform.SetParent(transform, false);
            lakeZoneGo.transform.position = LakeCenter;
            WaterZone lakeWz = lakeZoneGo.AddComponent<WaterZone>();
            lakeWz.Configure(new Vector3(0f, -0.8f, 0f), new Vector3(48f, 2.4f, 36f), surfaceY: 0.14f, floorY: -1.70f);

            // Spawn 6 shimmering collectible Underwater Pearls (4 in Sultanbeyli Lake, 2 on Hamidiye Pool floor!)
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _pearlMat = new Material(unlit) { color = new Color(0.70f, 0.98f, 1.0f, 1f) };
            _ownedMats.Add(_pearlMat);

            Vector3[] spots =
            {
                PoolCenter + new Vector3(3.2f, 0.28f, -2.1f),
                PoolCenter + new Vector3(-2.6f, 0.28f, 2.2f),
                LakeCenter + new Vector3(11.5f, -0.85f, 4.5f),
                LakeCenter + new Vector3(8.0f, -0.85f, -5.5f),
                LakeCenter + new Vector3(-4.5f, -0.85f, 7.2f),
                LakeCenter + new Vector3(-8.0f, -0.85f, -6.0f)
            };

            for (int i = 0; i < spots.Length; i++)
            {
                GameObject pearl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pearl.name = $"SunkenPearl_{i + 1}";
                Destroy(pearl.GetComponent<Collider>());
                pearl.transform.SetParent(transform, false);
                pearl.transform.position = spots[i];
                pearl.transform.localScale = Vector3.one * 0.34f;
                MeshRenderer mr = pearl.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.sharedMaterial = _pearlMat;
                }

                InteractableTrigger trig = pearl.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.InspectObject, $"sunken_pearl_{i}", "ui.btn.interact", 2.6f);
                trig.SetCustomPrompt("Parlayan Sualtı İncisini Topla (+35 ₺)", "Collect Glowing Underwater Pearl (+35 ₺)");
                _pearls.Add(pearl);
            }
        }

        private void HandlePearlInteracted(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;
            if (!trig.InteractableId.StartsWith("sunken_pearl_", StringComparison.Ordinal)) return;

            trig.gameObject.SetActive(false);
            SaveManager.Instance?.AddMoney(35);
            GameFeelAudio.PlayCoinChime();
            GameFeelAudio.PlayWaterSplash();
            GameFeelAudio.SpawnBurst(trig.transform.position + Vector3.up * 0.25f, new Color(0.55f, 0.96f, 1.0f), count: 14, spread: 0.55f);
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            NeighborhoodReputation.AddReputation(12, isEn ? "Underwater Pearl Found! (+35 ₺)" : "Sualtı İncisi Bulundu! (+35 ₺)");
            PlayerModeController.Instance?.RaiseStatusMessage(isEn
                ? "Collected a glowing underwater pearl! (+35 ₺ · +12 Rep)"
                : "Parlayan sualtı incisi toplandı! (+35 ₺ · +12 İtibar)");
        }
    }

    // =========================================================================
    // 1. OLYMPIC 3-LANE 50m SWIMMING RACE (IPlayableSportArena)
    // =========================================================================
    public sealed class LiveSwimRace : MonoBehaviour, IPlayableSportArena
    {
        private enum RacePhase { ReadyBlock, DivingIn, Swimming, FlipWindow, Finished }

        private const float LaneMinX = -6.2f;
        private const float LaneMaxX = 6.2f;
        private const float WaterBodyY = 0.42f;
        private const float DeckY = 1.44f;
        private const int TotalLaps = 4; // 4 x 12.5m = 50m Olympic sprint!

        private PlayerFootController _player;
        private ThirdPersonCameraRig _cameraRig;
        private Vector3 _beginPos;
        private Quaternion _beginRot;

        private ProceduralHumanoid _rivalTop;
        private ProceduralHumanoid _rivalBot;
        private float _rivalTopX, _rivalBotX;
        private float _rivalTopYaw = 90f, _rivalBotYaw = 90f;
        private int _rivalTopLap, _rivalBotLap;
        private int _rivalTopDir, _rivalBotDir;
        private bool _rivalTopDone, _rivalBotDone;

        private RacePhase _phase;
        private int _leagueTier;
        private int _playerLap;
        private int _playerDir; // +1 going East, -1 going West
        private float _playerVisualYaw = 90f;
        private float _playerX;
        private float _playerSpeed;
        private float _stamina = 100f;
        private bool _breaststroke;
        private float _raceTimer;
        private float _rhythmCursor;
        private float _rhythmDir = 1f;
        private float _diveTimer;
        private Vector3 _diveStartPos;
        private Vector3 _diveEndPos;
        private int _strokeCombo;
        private bool _flipBonusClaimed;
        private int _playerFinishRank;
        private float _finishWaitTimer;
        private string _calloutText;
        private Color _calloutColor = new Color(0.28f, 0.94f, 0.84f);
        private float _calloutTimer;
        private float _splashTimer;
        private float _hudTickTimer;

        public bool IsPlayerActive => _player != null;
        public int HudRevision { get; private set; }
        public Vector3 ReturnPosition => _beginPos;
        public Quaternion ReturnRotation => _beginRot;
        public bool HasSecondaryAction => _player != null && (_phase == RacePhase.Swimming || _phase == RacePhase.FlipWindow);
        public bool AcceptsPrimaryAction => _player != null;

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        private static string L(string tr, string en) => IsEn ? en : tr;

        private string GetLeagueLabel() => _leagueTier switch
        {
            0 => L("Acemi Lig", "Rookie League"),
            1 => L("Mahalle Ligi", "District League"),
            _ => L("Şampiyonlar Ligi", "Champions League")
        };

        private void SetCallout(string text, Color color, float duration)
        {
            _calloutText = text;
            _calloutColor = color;
            _calloutTimer = duration;
            HudRevision++;
        }

        public void BeginPlayerMatch(PlayerFootController player)
        {
            _player = player;
            _beginPos = player.transform.position;
            _beginRot = player.transform.rotation;
            _leagueTier = SaveManager.Instance != null ? Mathf.Clamp(SaveManager.Instance.Data.swimWins / 2, 0, 2) : 0;

            EnsureRivals(player.Humanoid);
            player.SetSeatedState(true);

            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (_cameraRig != null)
            {
                // Side-elevated Olympic broadcast camera looking smoothly across all 3 swimming lanes!
                _cameraRig.SetSportCameraOverride(true, distance: 10.6f, pitch: 30f, fov: 62f, lockYaw: 0f);
            }

            ResetRace();
            GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
            SetCallout(L($"{GetLeagueLabel()} · 50m Yüzme Yarışı! Hazırlan!", $"{GetLeagueLabel()} · 50m Swim Race! Get Ready!"), new Color(0.28f, 0.94f, 0.84f), 1.6f);
        }

        private void EnsureRivals(ProceduralHumanoid template)
        {
            if (template == null) return;
            if (_rivalTop == null)
            {
                _rivalTop = Instantiate(template, transform);
                _rivalTop.name = "SwimRival_Lane1";
                TintRival(_rivalTop, new Color(0.92f, 0.30f, 0.22f));
            }
            if (_rivalBot == null)
            {
                _rivalBot = Instantiate(template, transform);
                _rivalBot.name = "SwimRival_Lane3";
                TintRival(_rivalBot, new Color(0.96f, 0.72f, 0.14f));
            }
            _rivalTop.gameObject.SetActive(true);
            _rivalBot.gameObject.SetActive(true);
        }

        private static void TintRival(ProceduralHumanoid hum, Color col)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null || hum == null) return;
            Material m = new Material(lit) { color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            Renderer[] rends = hum.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                string n = rends[i].name.ToLowerInvariant();
                if (n.Contains("chest") || n.Contains("torso") || n.Contains("shirt") || n.Contains("arm"))
                {
                    rends[i].sharedMaterial = m;
                }
            }
        }

        private void ResetRace()
        {
            _phase = RacePhase.ReadyBlock;
            _playerLap = 0;
            _playerDir = 1;
            _playerVisualYaw = 90f;
            _playerX = LaneMinX - 0.85f;
            _playerSpeed = 0f;
            _stamina = 100f;
            _breaststroke = false;
            _raceTimer = 0f;
            _rhythmCursor = 0.08f;
            _rhythmDir = 1f;
            _strokeCombo = 0;
            _flipBonusClaimed = false;
            _playerFinishRank = 0;
            _finishWaitTimer = 0f;

            _rivalTopX = LaneMinX + 0.2f;
            _rivalBotX = LaneMinX + 0.2f;
            _rivalTopYaw = 90f;
            _rivalBotYaw = 90f;
            _rivalTopLap = 0;
            _rivalBotLap = 0;
            _rivalTopDir = 1;
            _rivalBotDir = 1;
            _rivalTopDone = false;
            _rivalBotDone = false;

            _player.TeleportTo(transform.TransformPoint(new Vector3(_playerX, DeckY, 0f)), Quaternion.Euler(0f, 90f, 0f));
            _player.Humanoid?.SetSwimming(false, 0f, false);
            _player.Humanoid?.SetLocomotion(0f, true, 0f);
            // Olympic starting-block crouched track-start stance
            Vector3 blockEdge = transform.TransformPoint(new Vector3(_playerX + 0.26f, DeckY + 0.08f, 0f));
            _player.Humanoid?.SetDualHandAction(true,
                blockEdge - _player.transform.right * 0.19f,
                blockEdge + _player.transform.right * 0.19f,
                0.75f, 0.75f, spineLeanDeg: 44f, headPitchDeg: 18f, hipsDrop: 0.16f);

            if (_rivalTop != null)
            {
                _rivalTop.transform.localPosition = new Vector3(_rivalTopX, WaterBodyY, 2.55f);
                _rivalTop.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                _rivalTop.SetSwimming(true, 0f, false);
            }
            if (_rivalBot != null)
            {
                _rivalBot.transform.localPosition = new Vector3(_rivalBotX, WaterBodyY, -2.55f);
                _rivalBot.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                _rivalBot.SetSwimming(true, 0f, false);
            }
            HudRevision++;
        }

        public void EndPlayerMatch()
        {
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(false);
                _cameraRig = null;
            }
            if (_player != null)
            {
                _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f);
                _player.Humanoid?.SetSwimming(false, 0f, false);
                _player.Humanoid?.SetLocomotion(0f, true, 0f);
                _player.SetSeatedState(false);
            }
            _player = null;
            if (_rivalTop != null) _rivalTop.gameObject.SetActive(false);
            if (_rivalBot != null) _rivalBot.gameObject.SetActive(false);
        }

        public string ControlsHintKey() => "sports.swim.controls";

        public string PrimaryButtonLabel()
        {
            return _phase switch
            {
                RacePhase.ReadyBlock => L("DALIŞ ÇIKIŞI!", "DIVE START!"),
                RacePhase.FlipWindow => L("TAKLALI DÖN!", "FLIP TURN!"),
                RacePhase.Finished => L("Tekrar Yarış", "Race Again"),
                _ => L("KULAÇ!", "STROKE!")
            };
        }

        public string SecondaryButtonLabel() => _breaststroke ? L("Serbest Stil", "Freestyle") : L("Kurbağalama", "Breaststroke");

        private float GetPlayerDistanceMeters()
        {
            float lapDist = _playerDir > 0
                ? Mathf.InverseLerp(LaneMinX, LaneMaxX, _playerX)
                : Mathf.InverseLerp(LaneMaxX, LaneMinX, _playerX);
            return Mathf.Clamp((_playerLap + Mathf.Clamp01(lapDist)) * 12.5f, 0f, 50f);
        }

        public string HudText()
        {
            float meters = GetPlayerDistanceMeters();
            string style = _breaststroke ? L("Kurbağalama", "Breaststroke") : L("Serbest", "Freestyle");
            string unit = L("sn", "s");
            string status = _phase switch
            {
                RacePhase.ReadyBlock => L("Çıkış barı ALTIN bölgedeyken VUR -> Patlayıcı Dalış Çıkışı!", "Tap HIT in the GOLD zone -> Explosive Dive Start!"),
                RacePhase.DivingIn => L("SUYA DALDIK! Ritim kulaçlarına hazırlan!", "SPLASH IN! Get ready for rhythm strokes!"),
                RacePhase.FlipWindow => L("DUVARA GELDİN! ŞİMDİ VUR -> Dönüş Taklası Hızlanması!", "AT THE WALL! TAP NOW -> Flip Turn Boost!"),
                RacePhase.Finished => _playerFinishRank == 1
                    ? L($"1. OLDUN! Süre: {_raceTimer:F2} sn — Tekrar için Vur!", $"1ST PLACE! Time: {_raceTimer:F2}s — Tap Hit to Race Again!")
                    : L($"{_playerFinishRank}. Sırada Bitirdin ({_raceTimer:F2} sn) — Rövanş için Vur!", $"Finished #{_playerFinishRank} ({_raceTimer:F2}s) — Tap Hit for Rematch!"),
                _ => L($"Stil: {style} · Ritim altın bölgedeyken KULAÇ vur! (Seri x{_strokeCombo})", $"Style: {style} · Tap STROKE in the gold zone! (Combo x{_strokeCombo})")
            };

            return $"{L("50m Yüzme Yarışı", "50m Swim Race")} [{GetLeagueLabel()}]  ·  {meters:F1}m / 50m  ·  {_raceTimer:F2} {unit}\n{status}";
        }

        public SportHudMeterState GetHudMeterState()
        {
            var state = new SportHudMeterState
            {
                ShowStaminaBar = _player != null && _phase != RacePhase.Finished,
                StaminaFill01 = Mathf.Clamp01(_stamina / 100f),
                CalloutBadge = _calloutTimer > 0f ? _calloutText : null,
                CalloutColor = _calloutColor
            };

            if (_phase == RacePhase.ReadyBlock)
            {
                bool sweet = _rhythmCursor >= 0.62f && _rhythmCursor <= 0.88f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _rhythmCursor;
                state.PrimaryColor = sweet ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.90f, 0.84f);
                state.PrimaryLabel = sweet ? L("ALTIN ÇIKIŞ! ŞİMDİ VUR!", "GOLD START! TAP NOW!") : L("ÇIKIŞ TEPKİSİ", "START REACTION");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.62f;
                state.SweetSpotMax01 = 0.88f;
            }
            else if (_phase == RacePhase.FlipWindow)
            {
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = 0.82f;
                state.PrimaryColor = new Color(1.0f, 0.84f, 0.20f);
                state.PrimaryLabel = L("DÖNÜŞ TAKLASI! ŞİMDİ VUR!", "FLIP TURN! TAP NOW!");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.65f;
                state.SweetSpotMax01 = 0.95f;
            }
            else if (_phase == RacePhase.Swimming)
            {
                bool sweet = _rhythmCursor >= 0.66f && _rhythmCursor <= 0.92f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _rhythmCursor;
                state.PrimaryColor = sweet ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.88f, 0.96f);
                state.PrimaryLabel = sweet ? L("ALTIN KULAÇ!", "GOLD STROKE!") : L("KULAÇ RİTMİ", "STROKE RHYTHM");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.66f;
                state.SweetSpotMax01 = 0.92f;
            }

            return state;
        }

        public void TriggerPrimaryAction()
        {
            if (_player == null) return;

            if (_phase == RacePhase.Finished)
            {
                if (_finishWaitTimer <= 0f)
                {
                    ResetRace();
                    GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                }
                return;
            }

            if (_phase == RacePhase.ReadyBlock)
            {
                bool golden = _rhythmCursor >= 0.62f && _rhythmCursor <= 0.88f;
                _phase = RacePhase.DivingIn;
                _diveTimer = 0f;
                _diveStartPos = new Vector3(_playerX, DeckY, 0f);
                _diveEndPos = new Vector3(golden ? -3.1f : -4.4f, WaterBodyY, 0f);
                _playerSpeed = golden ? 4.95f : 3.25f;
                _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f);
                _player.Humanoid?.PlaySportAnim(SportAnimKind.SwimDiveStart, 0.62f);
                GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
                if (golden)
                {
                    GameFeelAudio.TriggerHitstop(0.032f);
                    _cameraRig?.AddShake(0.12f, 0.16f);
                    SetCallout(L("ALTIN DALIŞ ÇIKIŞI! (+Hız Patlaması)", "GOLDEN DIVE START! (+Speed Burst)"), new Color(1.0f, 0.86f, 0.22f), 1.4f);
                }
                else
                {
                    SetCallout(L("TEMİZ ÇIKIŞ!", "CLEAN START!"), new Color(0.32f, 0.92f, 0.86f), 1.1f);
                }
                HudRevision++;
                return;
            }

            if (_phase == RacePhase.FlipWindow && !_flipBonusClaimed)
            {
                _flipBonusClaimed = true;
                _playerSpeed = 5.45f;
                _stamina = Mathf.Min(100f, _stamina + 14f);
                _player.Humanoid?.PlaySportAnim(SportAnimKind.SwimFlipTurn, 0.56f);
                GameFeelAudio.PlayWaterSplash();
                GameFeelAudio.SpawnBurst(_player.transform.position + Vector3.up * 0.5f, new Color(0.74f, 0.98f, 1.0f), count: 14, spread: 0.65f);
                _cameraRig?.AddShake(0.11f, 0.14f);
                SetCallout(L("MÜKEMMEL DÖNÜŞ TAKLASI! (+Turbo İtme)", "PERFECT FLIP TURN! (+Turbo Push)"), new Color(1.0f, 0.86f, 0.22f), 1.35f);
                ExecuteLapTurn();
                return;
            }

            if (_phase == RacePhase.Swimming)
            {
                bool golden = _rhythmCursor >= 0.66f && _rhythmCursor <= 0.92f;
                float stamCost = _breaststroke ? 4.5f : 7.5f;
                if (golden && _stamina >= 6f)
                {
                    _strokeCombo++;
                    _stamina = Mathf.Max(0f, _stamina - stamCost);
                    float comboBonus = Mathf.Min(0.85f, _strokeCombo * 0.16f);
                    _playerSpeed = (_breaststroke ? 4.15f : 4.85f) + comboBonus;
                    GameFeelAudio.PlayWaterSplash();
                    GameFeelAudio.SpawnBurst(_player.transform.position + Vector3.up * 0.65f, new Color(0.72f, 0.96f, 1.0f), count: 8, spread: 0.45f);
                    if (_strokeCombo >= 2)
                    {
                        SetCallout(L($"ALTIN KULAÇ SERİSİ x{_strokeCombo}!", $"GOLD STROKE COMBO x{_strokeCombo}!"), new Color(1.0f, 0.86f, 0.24f), 0.95f);
                    }
                    _rhythmCursor = 0.15f;
                    _rhythmDir = 1f;
                }
                else
                {
                    _strokeCombo = 0;
                    _stamina = Mathf.Max(0f, _stamina - stamCost * 1.4f);
                    _playerSpeed = Mathf.Max(_playerSpeed, 2.75f);
                }
                HudRevision++;
            }
        }

        public void TriggerPrimaryRelease(float holdSeconds) { }

        public void TriggerSecondaryAction()
        {
            if (_player == null || (_phase != RacePhase.Swimming && _phase != RacePhase.FlipWindow)) return;
            _breaststroke = !_breaststroke;
            SetCallout(_breaststroke
                ? L("KURBAĞALAMA (Enerji Koruma)", "BREASTSTROKE (Save Stamina)")
                : L("SERBEST STİL (Maksimum Hız)", "FREESTYLE (Max Speed)"), new Color(0.32f, 0.92f, 0.96f), 1.1f);
            HudRevision++;
        }

        public void TriggerSecondaryRelease(float holdSeconds) { }

        private void ExecuteLapTurn()
        {
            _playerLap++;
            if (_playerLap >= TotalLaps)
            {
                FinishRace();
                return;
            }
            _playerDir = -_playerDir;
            _phase = RacePhase.Swimming;
            _flipBonusClaimed = false;
            HudRevision++;
        }

        private void FinishRace()
        {
            _phase = RacePhase.Finished;
            _finishWaitTimer = 0.9f;
            int finishedBefore = (_rivalTopDone ? 1 : 0) + (_rivalBotDone ? 1 : 0);
            _playerFinishRank = finishedBefore + 1;
            bool won = _playerFinishRank == 1;

            _player.Humanoid?.SetSwimming(true, 0.2f, false);
            GameFeelAudio.PlayRefereeWhistle(doubleBlast: true);

            if (won)
            {
                GameFeelAudio.PlayCrowdCheer(bigCheer: true);
                GameFeelAudio.PlayMilestoneFanfare();
                int reward = 95 + _leagueTier * 30;
                SaveManager.Instance?.AddMoney(reward);
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.Data.swimWins++;
                    if (SaveManager.Instance.Data.bestSwim50mSeconds <= 0.1f || _raceTimer < SaveManager.Instance.Data.bestSwim50mSeconds)
                    {
                        SaveManager.Instance.Data.bestSwim50mSeconds = _raceTimer;
                    }
                    SaveManager.Instance.Save();
                }
                NeighborhoodReputation.AddReputation(35, L($"50m Yüzme Yarışı Şampiyonluğu! ({_raceTimer:F2} sn · +{reward} ₺)", $"50m Swim Race Victory! ({_raceTimer:F2}s · +{reward} ₺)"));
                SetCallout(L($"ALTIN MADALYA! ({_raceTimer:F2} sn · +{reward} ₺)", $"GOLD MEDAL! ({_raceTimer:F2}s · +{reward} ₺)"), new Color(1.0f, 0.86f, 0.22f), 2.8f);
            }
            else
            {
                SetCallout(L($"{_playerFinishRank}. SIRA ({_raceTimer:F2} sn)", $"RANK #{_playerFinishRank} ({_raceTimer:F2}s)"), new Color(0.42f, 0.82f, 1.0f), 2.4f);
            }

            SportMatchEvents.NotifyMatchFinished("sport_swim_race", won);
            HudRevision++;
        }

        private void Update()
        {
            if (_player == null) return;
            if (PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (_calloutTimer > 0f) { _calloutTimer -= dt; if (_calloutTimer <= 0f) HudRevision++; }
            if (_finishWaitTimer > 0f) _finishWaitTimer -= dt;

            if (_phase == RacePhase.ReadyBlock)
            {
                _rhythmCursor += _rhythmDir * dt * 1.15f;
                if (_rhythmCursor >= 1f) { _rhythmCursor = 1f; _rhythmDir = -1f; }
                else if (_rhythmCursor <= 0.05f) { _rhythmCursor = 0.05f; _rhythmDir = 1f; }
                return;
            }

            if (_phase == RacePhase.Finished) return;

            _raceTimer += dt;
            _hudTickTimer += dt;
            if (_hudTickTimer >= 0.12f)
            {
                _hudTickTimer = 0f;
                HudRevision++;
            }
            UpdateRivals(dt);

            if (_phase == RacePhase.DivingIn)
            {
                _diveTimer += dt;
                float u = Mathf.Clamp01(_diveTimer / 0.56f);
                Vector3 pos = Vector3.Lerp(_diveStartPos, _diveEndPos, u);
                pos.y += Mathf.Sin(u * Mathf.PI) * 1.05f;
                _playerX = pos.x;
                _player.TeleportTo(transform.TransformPoint(pos), Quaternion.Euler(0f, 90f, 0f));
                if (u >= 1f)
                {
                    _phase = RacePhase.Swimming;
                    GameFeelAudio.PlayWaterSplash();
                    GameFeelAudio.SpawnBurst(transform.TransformPoint(_diveEndPos + Vector3.up * 0.65f), new Color(0.80f, 0.98f, 1.0f), count: 18, spread: 0.72f);
                    HudRevision++;
                }
                return;
            }

            // Swimming & Wall Flip Turn phase
            float oscSpeed = _breaststroke ? 1.15f : 1.48f;
            _rhythmCursor += _rhythmDir * dt * oscSpeed;
            if (_rhythmCursor >= 1f) { _rhythmCursor = 1f; _rhythmDir = -1f; }
            else if (_rhythmCursor <= 0.04f) { _rhythmCursor = 0.04f; _rhythmDir = 1f; }

            // Passive stamina recovery (faster in Breaststroke!)
            _stamina = Mathf.Min(100f, _stamina + (_breaststroke ? 15f : 7.5f) * dt);

            float cruise = _breaststroke ? 2.65f : 2.95f;
            if (_stamina < 12f) cruise *= 0.75f;
            _playerSpeed = Mathf.MoveTowards(_playerSpeed, cruise, 2.2f * dt);

            _playerX += _playerDir * _playerSpeed * dt;
            float targetYaw = _playerDir > 0 ? 90f : -90f;
            _playerVisualYaw = Mathf.MoveTowardsAngle(_playerVisualYaw, targetYaw, 520f * dt);
            _player.TeleportTo(transform.TransformPoint(new Vector3(_playerX, WaterBodyY, 0f)), Quaternion.Euler(0f, _playerVisualYaw, 0f));
            _player.Humanoid?.SetSwimming(true, _playerSpeed, _breaststroke);

            _splashTimer -= dt;
            if (_splashTimer <= 0f && _playerSpeed > 2.4f)
            {
                _splashTimer = 0.24f;
                GameFeelAudio.SpawnBurst(_player.transform.position + Vector3.up * 0.65f, new Color(0.76f, 0.96f, 1.0f), count: 4, spread: 0.30f);
            }

            // Detect approaching East / West pool wall -> Flip Turn Window!
            float distToWall = _playerDir > 0 ? (LaneMaxX - _playerX) : (_playerX - LaneMinX);
            if (_playerLap < TotalLaps - 1 && distToWall <= 1.65f && _phase != RacePhase.FlipWindow)
            {
                _phase = RacePhase.FlipWindow;
                _flipBonusClaimed = false;
                HudRevision++;
            }

            if (distToWall <= 0.08f)
            {
                _playerX = _playerDir > 0 ? LaneMaxX - 0.1f : LaneMinX + 0.1f;
                if (_playerLap >= TotalLaps - 1)
                {
                    FinishRace();
                }
                else
                {
                    // Auto-turn if player missed the golden flip-turn window
                    _playerSpeed = 2.4f;
                    _player.Humanoid?.PlaySportAnim(SportAnimKind.SwimFlipTurn, 0.44f);
                    ExecuteLapTurn();
                }
            }
        }

        private void UpdateRivals(float dt)
        {
            float baseSpd1 = 3.15f + _leagueTier * 0.42f;
            float baseSpd2 = 3.38f + _leagueTier * 0.46f;
            StepRival(_rivalTop, ref _rivalTopX, ref _rivalTopYaw, ref _rivalTopLap, ref _rivalTopDir, ref _rivalTopDone, 2.55f, baseSpd1, dt);
            StepRival(_rivalBot, ref _rivalBotX, ref _rivalBotYaw, ref _rivalBotLap, ref _rivalBotDir, ref _rivalBotDone, -2.55f, baseSpd2, dt);
        }

        private void StepRival(ProceduralHumanoid hum, ref float rx, ref float rYaw, ref int lap, ref int dir, ref bool done, float laneZ, float speed, float dt)
        {
            if (hum == null || done) return;
            float wave = 1f + Mathf.Sin(Time.time * 2.2f + laneZ) * 0.08f;
            rx += dir * speed * wave * dt;
            if ((dir > 0 && rx >= LaneMaxX) || (dir < 0 && rx <= LaneMinX))
            {
                rx = dir > 0 ? LaneMaxX : LaneMinX;
                lap++;
                if (lap >= TotalLaps)
                {
                    done = true;
                    hum.SetSwimming(true, 0.2f, false);
                    return;
                }
                dir = -dir;
                hum.PlaySportAnim(SportAnimKind.SwimFlipTurn, 0.48f);
            }
            float targetYaw = dir > 0 ? 90f : -90f;
            rYaw = Mathf.MoveTowardsAngle(rYaw, targetYaw, 500f * dt);
            hum.transform.localPosition = new Vector3(rx, WaterBodyY, laneZ);
            hum.transform.localRotation = Quaternion.Euler(0f, rYaw, 0f);
            hum.SetSwimming(true, speed, false);
        }
    }

    // =========================================================================
    // 2. ACROBATIC SPRINGBOARD DIVING (OW_DivingBoard + JURY SCORING!)
    // =========================================================================
    public sealed class AcrobaticDivingBoard : MonoBehaviour, IPlayableSportArena
    {
        private enum DiveStage { ChargeSpring, Airborne, SplashScored }

        private Vector3 _poolCenter;
        private float _waterY = 1.18f;
        private PlayerFootController _player;
        private ThirdPersonCameraRig _cameraRig;
        private Vector3 _beginPos;
        private Quaternion _beginRot;

        private DiveStage _stage;
        private float _springCharge;
        private float _springDir = 1f;
        private float _airElapsed;
        private float _airDuration = 1.55f;
        private float _peakHeight = 4.4f;
        private int _flipsDone;
        private int _twistsDone;
        private float _currentTwistDeg;
        private float _juryScore;
        private float _scoreWaitTimer;
        private Vector3 _launchWorldPos;
        private Vector3 _entryWorldPos;
        private string _calloutText;
        private Color _calloutColor = new Color(1.0f, 0.86f, 0.24f);
        private float _calloutTimer;

        public bool IsPlayerActive => _player != null;
        public int HudRevision { get; private set; }
        public Vector3 ReturnPosition => _beginPos;
        public Quaternion ReturnRotation => _beginRot;
        public bool HasSecondaryAction => _player != null && _stage == DiveStage.Airborne;
        public bool AcceptsPrimaryAction => _player != null;

        public void ConfigurePool(Vector3 poolCenter, float waterY)
        {
            _poolCenter = poolCenter;
            _waterY = waterY;
        }

        public void BeginPlayerMatch(PlayerFootController player)
        {
            _player = player;
            _beginPos = player.transform.position;
            _beginRot = player.transform.rotation;
            player.SetSeatedState(true);

            _cameraRig = FindAnyObjectByType<ThirdPersonCameraRig>();
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(true, distance: 8.8f, pitch: 18f, fov: 62f, lockYaw: 18f);
            }

            ResetDive();
            GameFeelAudio.PlayRefereeWhistle(doubleBlast: false);
        }

        private void ResetDive()
        {
            _stage = DiveStage.ChargeSpring;
            _springCharge = 0.12f;
            _springDir = 1f;
            _airElapsed = 0f;
            _flipsDone = 0;
            _twistsDone = 0;
            _currentTwistDeg = 0f;
            _juryScore = 0f;
            _calloutText = L("TRAMPLEN DALIŞI · Altın bölgede zıpla!", "SPRINGBOARD DIVE · Jump in the gold zone!");
            _calloutColor = new Color(0.28f, 0.94f, 0.86f);
            _calloutTimer = 1.8f;

            _launchWorldPos = transform.TransformPoint(new Vector3(0f, 1.24f, 2.15f));
            _entryWorldPos = new Vector3(_poolCenter.x - 1.8f, _waterY - 0.55f, _poolCenter.z);
            _player.TeleportTo(_launchWorldPos, transform.rotation);
            _player.Humanoid?.SetSwimming(false, 0f, false);
            _player.Humanoid?.SetLocomotion(0f, true, 0f);
            HudRevision++;
        }

        public void EndPlayerMatch()
        {
            if (_cameraRig != null)
            {
                _cameraRig.SetSportCameraOverride(false);
                _cameraRig = null;
            }
            if (_player != null)
            {
                _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f);
                _player.Humanoid?.SetSwimming(false, 0f, false);
                _player.SetSeatedState(false);
            }
            _player = null;
        }

        private static bool IsEn =>
            LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";

        private static string L(string tr, string en) => IsEn ? en : tr;

        public string ControlsHintKey() => "sports.swim.controls";

        public string PrimaryButtonLabel()
        {
            return _stage switch
            {
                DiveStage.ChargeSpring => L("SIÇRA!", "JUMP!"),
                DiveStage.Airborne => L("TAKLA!", "FLIP!"),
                _ => L("Tekrar Atla", "Dive Again")
            };
        }

        public string SecondaryButtonLabel() => L("BURGU!", "TWIST!");

        public string HudText()
        {
            string status = _stage switch
            {
                DiveStage.ChargeSpring => L("Yay barı ALTIN bölgedeyken SIÇRA! (Yüksek atlayış = Çok takla)", "Tap JUMP in the GOLD spring zone! (Higher jump = More flips)"),
                DiveStage.Airborne => L($"HAVADASIN! Takla: {_flipsDone} · Burgu: {_twistsDone} — Suya girmeden kombinasyon yap!", $"AIRBORNE! Flips: {_flipsDone} · Twists: {_twistsDone} — Combo before splash!"),
                _ => L($"JÜRİ PUANI: {_juryScore:F1} / 10.0  ·  Tekrar atlamak için Vur!", $"JUDGES SCORE: {_juryScore:F1} / 10.0  ·  Tap Hit to Dive Again!")
            };
            return $"{L("Tramplen Akrobatik Dalış", "Acrobatic Springboard Diving")}  ·  {_flipsDone} {L("Takla", "Flips")} · {_twistsDone} {L("Burgu", "Twists")}\n{status}";
        }

        public SportHudMeterState GetHudMeterState()
        {
            var state = new SportHudMeterState
            {
                ShowStaminaBar = false,
                CalloutBadge = _calloutTimer > 0f ? _calloutText : null,
                CalloutColor = _calloutColor
            };

            if (_stage == DiveStage.ChargeSpring)
            {
                bool sweet = _springCharge >= 0.64f && _springCharge <= 0.90f;
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = _springCharge;
                state.PrimaryColor = sweet ? new Color(1.0f, 0.84f, 0.20f) : new Color(0.24f, 0.90f, 0.86f);
                state.PrimaryLabel = sweet ? L("ALTIN SIÇRAMA!", "GOLD LAUNCH!") : L("TRAMPLEN YAY GÜCÜ", "SPRINGBOARD POWER");
                state.ShowSweetSpot = true;
                state.SweetSpotMin01 = 0.64f;
                state.SweetSpotMax01 = 0.90f;
            }
            else if (_stage == DiveStage.Airborne)
            {
                float u = Mathf.Clamp01(_airElapsed / Mathf.Max(0.2f, _airDuration));
                state.ShowPrimaryBar = true;
                state.PrimaryFill01 = 1f - u;
                state.PrimaryColor = new Color(0.32f, 0.86f, 1.0f);
                state.PrimaryLabel = L("HAVADA KOMBİNASYON SÜRESİ", "AIR COMBO WINDOW");
                state.ShowSweetSpot = false;
            }

            return state;
        }

        public void TriggerPrimaryAction()
        {
            if (_player == null) return;

            if (_stage == DiveStage.SplashScored)
            {
                if (_scoreWaitTimer <= 0f) ResetDive();
                return;
            }

            if (_stage == DiveStage.ChargeSpring)
            {
                bool golden = _springCharge >= 0.64f && _springCharge <= 0.90f;
                _stage = DiveStage.Airborne;
                _airElapsed = 0f;
                _peakHeight = golden ? 4.85f : Mathf.Lerp(2.4f, 3.8f, _springCharge);
                _airDuration = golden ? 1.75f : 1.35f;
                _player.Humanoid?.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f);
                _player.Humanoid?.PlaySportAnim(SportAnimKind.SwimDiveStart, 0.55f);
                GameFeelAudio.PlaySlideWhoosh();
                _calloutText = golden
                    ? L("ALTIN SIÇRAMA! Havada Takla & Burgu bas!", "GOLD LAUNCH! Tap Flip & Twist mid-air!")
                    : L("ATLADIN! Havada Takla bas!", "LAUNCHED! Tap Flip mid-air!");
                _calloutColor = golden ? new Color(1.0f, 0.86f, 0.22f) : new Color(0.32f, 0.92f, 0.86f);
                _calloutTimer = 1.3f;
                HudRevision++;
                return;
            }

            if (_stage == DiveStage.Airborne && _flipsDone < 4)
            {
                _flipsDone++;
                _player.Humanoid?.PlaySportAnim(SportAnimKind.SwimFlipTurn, 0.42f);
                GameFeelAudio.PlaySlideWhoosh();
                _calloutText = L($"{_flipsDone}x TAKLA!", $"{_flipsDone}x FLIP!");
                _calloutColor = new Color(1.0f, 0.86f, 0.24f);
                _calloutTimer = 0.9f;
                HudRevision++;
            }
        }

        public void TriggerPrimaryRelease(float holdSeconds) { }

        public void TriggerSecondaryAction()
        {
            if (_player == null || _stage != DiveStage.Airborne || _twistsDone >= 3) return;
            _twistsDone++;
            _player.Humanoid?.PlaySportAnim(SportAnimKind.SwimDiveStart, 0.36f);
            GameFeelAudio.PlayUiClick();
            _calloutText = L($"{_flipsDone}x TAKLA + {_twistsDone}x BURGU!", $"{_flipsDone}x FLIP + {_twistsDone}x TWIST!");
            _calloutColor = new Color(0.36f, 0.96f, 0.86f);
            _calloutTimer = 0.9f;
            HudRevision++;
        }

        public void TriggerSecondaryRelease(float holdSeconds) { }

        private void Update()
        {
            if (_player == null) return;
            if (PlayerModeController.Instance != null && PlayerModeController.Instance.IsModalLocked) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (_calloutTimer > 0f) { _calloutTimer -= dt; if (_calloutTimer <= 0f) HudRevision++; }
            if (_scoreWaitTimer > 0f) _scoreWaitTimer -= dt;

            if (_stage == DiveStage.ChargeSpring)
            {
                _springCharge += _springDir * dt * 1.18f;
                if (_springCharge >= 1f) { _springCharge = 1f; _springDir = -1f; }
                else if (_springCharge <= 0.08f) { _springCharge = 0.08f; _springDir = 1f; }
                // Subtle rhythmic knee-dip bounce while charging springboard launch
                _player.Humanoid?.SetDualHandAction(true, Vector3.zero, Vector3.zero, 0f, 0f,
                    spineLeanDeg: Mathf.Lerp(6f, 22f, _springCharge),
                    headPitchDeg: 6f,
                    hipsDrop: Mathf.Lerp(0.02f, 0.14f, _springCharge));
                return;
            }

            if (_stage == DiveStage.Airborne)
            {
                _airElapsed += dt;
                float u = Mathf.Clamp01(_airElapsed / _airDuration);
                Vector3 pos = Vector3.Lerp(_launchWorldPos, _entryWorldPos, u);
                pos.y += 4f * _peakHeight * u * (1f - u);
                float targetTwistDeg = _twistsDone * 360f;
                float twistSpeed = Mathf.Max(560f, (targetTwistDeg - _currentTwistDeg) * 7.5f);
                _currentTwistDeg = Mathf.MoveTowards(_currentTwistDeg, targetTwistDeg, twistSpeed * dt);
                _player.TeleportTo(pos, transform.rotation * Quaternion.Euler(0f, _currentTwistDeg, 0f));

                if (u >= 1f)
                {
                    CompleteDiveSplash();
                }
            }
        }

        private void CompleteDiveSplash()
        {
            _stage = DiveStage.SplashScored;
            _scoreWaitTimer = 0.85f;
            _player.Humanoid?.SetSwimming(true, 0.2f, false);

            float rawScore = 5.4f + _flipsDone * 1.15f + _twistsDone * 0.85f + (_peakHeight > 4.2f ? 0.8f : 0.2f);
            _juryScore = Mathf.Clamp(Mathf.Round(rawScore * 10f) / 10f, 5.0f, 10.0f);

            GameFeelAudio.PlayWaterSplash();
            GameFeelAudio.SpawnBurst(_entryWorldPos + Vector3.up * 0.65f, new Color(0.84f, 0.98f, 1.0f), count: 24, spread: 0.95f);
            _cameraRig?.AddShake(0.16f, 0.22f);

            int cash = Mathf.RoundToInt(_juryScore * 7f);
            SaveManager.Instance?.AddMoney(cash);
            if (_juryScore >= 8.5f)
            {
                GameFeelAudio.PlayCrowdCheer(bigCheer: true);
                NeighborhoodReputation.AddReputation(18, L($"Müthiş Tramplen Dalışı ({_juryScore:F1}/10 · +{cash} ₺)", $"Awesome Springboard Dive ({_juryScore:F1}/10 · +{cash} ₺)"));
            }
            else
            {
                GameFeelAudio.PlayCoinChime();
            }

            _calloutText = L($"JÜRİ PUANI: {_juryScore:F1} / 10.0  (+{cash} ₺)", $"JUDGES SCORE: {_juryScore:F1} / 10.0  (+{cash} ₺)");
            _calloutColor = _juryScore >= 8.5f ? new Color(1.0f, 0.86f, 0.22f) : new Color(0.32f, 0.94f, 0.86f);
            _calloutTimer = 3.2f;
            SportMatchEvents.NotifyMatchFinished("sport_diving_board", _juryScore >= 7.5f);
            HudRevision++;
        }
    }
}
