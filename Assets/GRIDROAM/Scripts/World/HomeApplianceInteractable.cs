using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
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

        public HomeApplianceKind Kind => applianceKind;
        public string DisplayLabel => displayLabel;

        public void Configure(HomeApplianceKind kind, string label)
        {
            applianceKind = kind;
            displayLabel = label;
        }

        public void Use(PlayerModeController mode)
        {
            if (mode == null) return;

            switch (applianceKind)
            {
                case HomeApplianceKind.Television:
                {
                    GameFeelAudio.PlayUiClick();
                    if (!_isTurnedOn)
                    {
                        _isTurnedOn = true;
                        _channelIndex = 0;
                        EnsureGlowLight(TvChannelColors[_channelIndex], 4.5f, 1.8f);
                        mode.RaiseStatusMessage($"TV açıldı: {TvChannelNames[_channelIndex]} (Tekrar bas: Kanal değiştir)");
                    }
                    else
                    {
                        _channelIndex++;
                        if (_channelIndex >= TvChannelNames.Length)
                        {
                            _isTurnedOn = false;
                            SetGlowEnabled(false);
                            mode.RaiseStatusMessage("TV kapatıldı.");
                        }
                        else
                        {
                            EnsureGlowLight(TvChannelColors[_channelIndex], 4.5f, 1.8f);
                            mode.RaiseStatusMessage($"Kanal değiştirildi: {TvChannelNames[_channelIndex]}");
                        }
                    }
                    break;
                }

                case HomeApplianceKind.TeaKettle:
                {
                    if (Time.time - _lastActionTime < 8f)
                    {
                        mode.RaiseStatusMessage("Çay hâlâ sıcak, birazdan bir bardak daha içebilirsin.");
                        return;
                    }
                    _lastActionTime = Time.time;
                    GameFeelAudio.PlayWaterSplash();
                    GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.0f, new Color(0.95f, 0.56f, 0.24f), count: 8, spread: 0.35f);
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(22f);
                    }
                    mode.RaiseStatusMessage("Çaydanlıktan taze ince belli demli çay içtin! (+22 Enerji)");
                    break;
                }

                case HomeApplianceKind.Refrigerator:
                {
                    if (Time.time - _lastActionTime < 10f)
                    {
                        mode.RaiseStatusMessage("Buzdolabından az önce atıştırdın, tok hissediyorsun.");
                        return;
                    }
                    _lastActionTime = Time.time;
                    GameFeelAudio.PlayDoorLatch();
                    GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 1.1f, new Color(0.76f, 0.94f, 1.0f), count: 8, spread: 0.35f);
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(28f);
                    }
                    mode.RaiseStatusMessage("Buzdolabından soğuk ayran, beyaz peynir ve zeytin aldın! (+28 Enerji)");
                    break;
                }

                case HomeApplianceKind.Wardrobe:
                {
                    if (SaveManager.Instance != null && mode.FootController != null)
                    {
                        int nextColor = (SaveManager.Instance.Data.playerJacketColorIndex + 1) % 4;
                        SaveManager.Instance.Data.playerJacketColorIndex = nextColor;
                        SaveManager.Instance.Save();
                        mode.FootController.ApplyJacketColor(nextColor);
                        GameFeelAudio.PlayDoorLatch();
                        GameFeelAudio.SpawnBurst(mode.ActiveWorldPosition + Vector3.up * 1.15f, new Color(0.38f, 0.85f, 0.80f), count: 10, spread: 0.45f);
                        string[] outfitNames = { "Klasik Lacivert Ceket", "Bordo Deri Ceket", "Zeytin Parka", "Antrasit Mont" };
                        mode.RaiseStatusMessage($"Gardıroptan kıyafet değiştirdin: {outfitNames[nextColor]}");
                    }
                    break;
                }

                case HomeApplianceKind.ComputerDesk:
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
                            mode.RaiseStatusMessage("Bilgisayardan küçük bir mahalle ilanı işi tamamladın! (+20 ₺)");
                        }
                        else
                        {
                            GameFeelAudio.PlayUiClick();
                            mode.RaiseStatusMessage("Bilgisayar açıldı: Selman Sokağı mahalle ilanlarına bakıyorsun.");
                        }
                    }
                    else
                    {
                        SetGlowEnabled(false);
                        mode.RaiseStatusMessage("Bilgisayar kapatıldı.");
                    }
                    break;
                }

                case HomeApplianceKind.WashingMachine:
                {
                    _isTurnedOn = !_isTurnedOn;
                    if (_isTurnedOn)
                    {
                        EnsureGlowLight(new Color(0.45f, 0.88f, 1.0f), 2.8f, 1.1f);
                        mode.RaiseStatusMessage("Çamaşır makinesi 40°C pamuklu programında çalışmaya başladı!");
                    }
                    else
                    {
                        SetGlowEnabled(false);
                        mode.RaiseStatusMessage("Çamaşır makinesi durduruldu, çamaşırlar mis gibi kokuyor.");
                    }
                    break;
                }

                case HomeApplianceKind.BathroomSink:
                {
                    GameFeelAudio.PlayWaterSplash();
                    GameFeelAudio.SpawnBurst(transform.position + Vector3.up * 0.95f, new Color(0.45f, 0.86f, 1.0f), count: 8, spread: 0.35f);
                    if (PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(15f);
                    }
                    mode.RaiseStatusMessage("Lavaboda soğuk suyla elini yüzünü yıkadın, kendine geldin! (+15 Enerji)");
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
