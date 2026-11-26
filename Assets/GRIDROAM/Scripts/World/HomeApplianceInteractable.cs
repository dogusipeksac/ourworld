using OurWorld.Core;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public enum HomeApplianceKind
    {
        Television,
        TeaKettle,
        Refrigerator,
        Wardrobe,
        ComputerDesk,
        WashingMachine,
        BathroomSink
    }

    /// <summary>
    /// Makes household furniture & appliances inside every Selman Sokağı home genuinely usable:
    /// toggle TV channels & screen glow, brew & drink Turkish tea (+energy), grab cold ayran
    /// from the fridge (+energy), change outfit/jacket at the wardrobe, earn freelance pocket
    /// money on the study computer, run the washing machine, or wash up at the bathroom sink.
    /// </summary>
    public class HomeApplianceInteractable : MonoBehaviour
    {
        [SerializeField] private HomeApplianceKind applianceKind = HomeApplianceKind.Television;
        [SerializeField] private string displayLabel = "Eşya";

        private bool _isTurnedOn;
        private int _channelIndex;
        private float _lastActionTime = -99f;
        private Light _glowLight;

        private static readonly Color[] TvChannelColors =
        {
            new Color(0.32f, 0.64f, 1.0f),
            new Color(0.24f, 0.88f, 0.44f),
            new Color(1.0f, 0.62f, 0.28f),
            new Color(0.86f, 0.42f, 0.96f)
        };

        private static readonly string[] TvChannelNames =
        {
            "Sultanbeyli Yerel Haber",
            "Akşam Süper Lig Maçı",
            "İstanbul Belgeseli",
            "Yeşilçam Klasikleri"
        };

        private static readonly string[] TvChannelNamesEn =
        {
            "Sultanbeyli Local News",
            "Evening Super Lig Match",
            "Istanbul Documentary",
            "Yesilcam Classics"
        };

        public HomeApplianceKind Kind => applianceKind;
        public string DisplayLabel => displayLabel;

        public void Configure(HomeApplianceKind kind, string label)
        {
            applianceKind = kind;
            displayLabel = label;
        }

        public void Use(PlayerModeController mode)
        {
            if (mode == null || PlayerActionAnimator.IsBusyOn(mode)) return;
            bool en = LocalizationService.IsEnglish;

            switch (applianceKind)
            {
                case HomeApplianceKind.Television:
                {
                    PlayerActionAnimator.Play(mode, transform, PlayerActionKind.UseTvRemote, onClimax: () =>
                    {
                        string[] channels = en ? TvChannelNamesEn : TvChannelNames;
                        if (!_isTurnedOn)
                        {
                            _isTurnedOn = true;
                            _channelIndex = 0;
                            EnsureGlowLight(TvChannelColors[_channelIndex], 4.5f, 1.8f);
                            mode.RaiseStatusMessage(en
                                ? $"TV turned on: {channels[_channelIndex]} (Press again to switch channel)"
                                : $"TV açıldı: {channels[_channelIndex]} (Tekrar bas: Kanal değiştir)");
                        }
                        else
                        {
                            _channelIndex++;
                            if (_channelIndex >= channels.Length)
                            {
                                _isTurnedOn = false;
                                SetGlowEnabled(false);
                                mode.RaiseStatusMessage(en ? "TV turned off." : "TV kapatıldı.");
                            }
                            else
                            {
                                EnsureGlowLight(TvChannelColors[_channelIndex], 4.5f, 1.8f);
                                mode.RaiseStatusMessage(en
                                    ? $"Switched channel: {channels[_channelIndex]}"
                                    : $"Kanal değiştirildi: {channels[_channelIndex]}");
                            }
                        }
                    }, startStatus: en ? "Pressing TV remote..." : "TV kumandasına basılıyor...");
                    break;
                }

                case HomeApplianceKind.TeaKettle:
                {
                    if (Time.time - _lastActionTime < 8f)
                    {
                        mode.RaiseStatusMessage(en ? "The tea is still piping hot, wait a moment for another glass." : "Çay hâlâ sıcak, birazdan bir bardak daha içebilirsin.");
                        return;
                    }
                    _lastActionTime = Time.time;
                    GameFeelAudio.PlayWaterSplash();
                    GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.0f, new Color(0.95f, 0.56f, 0.24f), count: 8, spread: 0.35f);
                    if (mode.FootController != null && mode.CurrentMode == ControlMode.OnFoot)
                    {
                        mode.RaiseStatusMessage(en ? "Pouring freshly brewed Turkish tea..." : "Çaydanlıktan ince belli bardağa demli çay dolduruluyor...");
                        PlayerConsumeAnimator.PlayPurchase(
                            mode.FootController,
                            "tea_kettle",
                            ConsumeAnimKind.DrinkHotCup,
                            en ? "Brewed Tea" : "Demli Çay",
                            22f,
                            explicitTarget: transform);
                    }
                    else
                    {
                        if (PlayerNeedsController.Instance != null)
                        {
                            PlayerNeedsController.Instance.RestoreEnergy(22f);
                        }
                        mode.RaiseStatusMessage(en ? "Drank a fresh glass of Turkish tea! (+22 Energy)" : "Çaydanlıktan taze ince belli demli çay içtin! (+22 Enerji)");
                    }
                    break;
                }

                case HomeApplianceKind.Refrigerator:
                {
                    if (Time.time - _lastActionTime < 10f)
                    {
                        mode.RaiseStatusMessage(en ? "You just had a snack from the fridge and feel full." : "Buzdolabından az önce atıştırdın, tok hissediyorsun.");
                        return;
                    }
                    _lastActionTime = Time.time;
                    GameFeelAudio.PlayDoorLatch();
                    GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.1f, new Color(0.76f, 0.94f, 1.0f), count: 8, spread: 0.35f);
                    if (mode.FootController != null && mode.CurrentMode == ControlMode.OnFoot)
                    {
                        mode.RaiseStatusMessage(en ? "Grabbing cold ayran from the fridge..." : "Buzdolabından soğuk ayran alınıyor...");
                        PlayerConsumeAnimator.PlayPurchase(
                            mode.FootController,
                            "fridge",
                            ConsumeAnimKind.DrinkWater,
                            en ? "Cold Ayran" : "Soğuk Ayran",
                            28f,
                            explicitTarget: transform);
                    }
                    else
                    {
                        if (PlayerNeedsController.Instance != null)
                        {
                            PlayerNeedsController.Instance.RestoreEnergy(28f);
                        }
                        mode.RaiseStatusMessage(en ? "Had cold ayran, white cheese & olives from the fridge! (+28 Energy)" : "Buzdolabından soğuk ayran, beyaz peynir ve zeytin aldın! (+28 Enerji)");
                    }
                    break;
                }

                case HomeApplianceKind.Wardrobe:
                {
                    if (SaveManager.Instance != null && mode.FootController != null)
                    {
                        PlayerActionAnimator.Play(mode, transform, PlayerActionKind.ChangeOutfit, onClimax: () =>
                        {
                            if (SaveManager.Instance == null || mode.FootController == null) return;
                            int nextColor = (SaveManager.Instance.Data.playerJacketColorIndex + 1) % 4;
                            SaveManager.Instance.Data.playerJacketColorIndex = nextColor;
                            SaveManager.Instance.Save();
                            mode.FootController.ApplyJacketColor(nextColor);
                            GameFeelAudio.SpawnBurst(mode.ActiveWorldPosition + Vector3.up * 1.15f, new Color(0.38f, 0.85f, 0.80f), count: 12, spread: 0.45f);
                            string[] outfitNames = en
                                ? new[] { "Classic Navy Jacket", "Burgundy Leather Jacket", "Olive Parka", "Charcoal Coat" }
                                : new[] { "Klasik Lacivert Ceket", "Bordo Deri Ceket", "Zeytin Parka", "Antrasit Mont" };
                            mode.RaiseStatusMessage(en
                                ? $"Changed outfit from wardrobe: {outfitNames[nextColor]}"
                                : $"Gardıroptan kıyafet değiştirdin: {outfitNames[nextColor]}");
                        }, startStatus: en ? "Opening wardrobe, trying on a new jacket..." : "Gardırop açılıyor, yeni ceket giyiliyor...");
                    }
                    break;
                }

                case HomeApplianceKind.ComputerDesk:
                {
                    string startMsg = _isTurnedOn
                        ? (en ? "Shutting down computer..." : "Bilgisayar kapatılıyor...")
                        : (en ? "Sitting at the desk and typing..." : "Bilgisayar başına geçip klavyede çalışılıyor...");
                    PlayerActionAnimator.Play(mode, transform, PlayerActionKind.TypeComputer, onClimax: () =>
                    {
                        _isTurnedOn = !_isTurnedOn;
                        if (_isTurnedOn)
                        {
                            EnsureGlowLight(new Color(0.55f, 0.80f, 1.0f), 3.6f, 1.5f);
                            if (Time.time - _lastActionTime >= 25f && SaveManager.Instance != null)
                            {
                                _lastActionTime = Time.time;
                                SaveManager.Instance.AddMoney(20);
                                GameFeelAudio.PlayCoinChime();
                                GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.0f, new Color(1.0f, 0.84f, 0.24f), count: 10, spread: 0.4f);
                                mode.RaiseStatusMessage(en
                                    ? "Completed a local freelance listing on your PC! (+20 ₺)"
                                    : "Bilgisayardan küçük bir mahalle ilanı işi tamamladın! (+20 ₺)");
                            }
                            else
                            {
                                GameFeelAudio.PlayUiClick();
                                mode.RaiseStatusMessage(en
                                    ? "PC turned on: browsing Selman Street local listings."
                                    : "Bilgisayar açıldı: Selman Sokağı mahalle ilanlarına bakıyorsun.");
                            }
                        }
                        else
                        {
                            SetGlowEnabled(false);
                            mode.RaiseStatusMessage(en ? "Computer shut down." : "Bilgisayar kapatıldı.");
                        }
                    }, startStatus: startMsg);
                    break;
                }

                case HomeApplianceKind.WashingMachine:
                {
                    string startMsg = _isTurnedOn
                        ? (en ? "Stopping washing machine..." : "Çamaşır makinesi durduruluyor...")
                        : (en ? "Loading laundry and selecting wash cycle..." : "Çamaşır makinesi doldurulup program seçiliyor...");
                    PlayerActionAnimator.Play(mode, transform, PlayerActionKind.LoadWashingMachine, onClimax: () =>
                    {
                        _isTurnedOn = !_isTurnedOn;
                        if (_isTurnedOn)
                        {
                            EnsureGlowLight(new Color(0.45f, 0.88f, 1.0f), 2.8f, 1.1f);
                            mode.RaiseStatusMessage(en
                                ? "Washing machine started on 40°C cotton cycle!"
                                : "Çamaşır makinesi 40°C pamuklu programında çalışmaya başladı!");
                        }
                        else
                        {
                            SetGlowEnabled(false);
                            mode.RaiseStatusMessage(en
                                ? "Washing machine stopped — laundry smells fresh!"
                                : "Çamaşır makinesi durduruldu, çamaşırlar mis gibi kokuyor.");
                        }
                    }, startStatus: startMsg);
                    break;
                }

                case HomeApplianceKind.BathroomSink:
                {
                    if (Time.time - _lastActionTime < 5f)
                    {
                        mode.RaiseStatusMessage(en ? "You just washed up — squeaky clean!" : "Elini yüzünü az önce yıkadın, tertemizsin!");
                        return;
                    }
                    _lastActionTime = Time.time;
                    PlayerActionAnimator.Play(mode, transform, PlayerActionKind.WashFace, onClimax: () =>
                    {
                        if (PlayerNeedsController.Instance != null)
                        {
                            PlayerNeedsController.Instance.RestoreEnergy(15f);
                        }
                        mode.RaiseStatusMessage(en
                            ? "Splashed cold water on your face, feeling refreshed! (+15 Energy)"
                            : "Lavaboda soğuk suyla elini yüzünü yıkadın, kendine geldin! (+15 Enerji)");
                    }, startStatus: en ? "Washing hands and face with cold water..." : "Lavaboda soğuk suyla el ve yüz yıkanıyor...");
                    break;
                }
            }
        }

        private void EnsureGlowLight(Color col, float range, float intensity)
        {
            if (_glowLight == null)
            {
                GameObject lgo = new GameObject("Appliance_ActiveGlow");
                lgo.transform.SetParent(transform, false);
                lgo.transform.localPosition = new Vector3(0f, 1.05f, 0.35f);
                _glowLight = lgo.AddComponent<Light>();
                _glowLight.type = LightType.Point;
                _glowLight.shadows = LightShadows.None;
            }
            _glowLight.color = col;
            _glowLight.range = range;
            _glowLight.intensity = intensity;
            _glowLight.enabled = true;
        }

        private void SetGlowEnabled(bool active)
        {
            if (_glowLight != null)
            {
                _glowLight.enabled = active;
            }
        }
    }
}
