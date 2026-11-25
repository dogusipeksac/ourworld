using OurWorld.CameraSystem;
using OurWorld.Core;
using OurWorld.Input;
using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    /// <summary>
    /// Lightweight event bus broadcasting completed player sport matches (Tennis, Football, Basketball, Volleyball)
    /// so story missions and achievements react cleanly without tight coupling.
    /// </summary>
    public static class SportMatchEvents
    {
        public static event System.Action<string, bool> OnMatchFinished;

        public static void NotifyMatchFinished(string sportId, bool playerWon)
        {
            OnMatchFinished?.Invoke(sportId, playerWon);
        }
    }

    /// <summary>
    /// Live real-time HUD meter payload rendered under the top scoreboard pill
    /// (shot/serve charge power, spike/dig timing sweet-spot, stamina bar, and dynamic highlight badge).
    /// </summary>
    public struct SportHudMeterState
    {
        public bool ShowPrimaryBar;
        public float PrimaryFill01;
        public Color PrimaryColor;
        public string PrimaryLabel;
        public bool ShowSweetSpot;
        public float SweetSpotMin01;
        public float SweetSpotMax01;
        public bool ShowStaminaBar;
        public float StaminaFill01;
        public string CalloutBadge;
        public Color CalloutColor;
    }

    /// <summary>
    /// Shared runtime contract for any playable neighborhood sports arena
    /// (3v3 Halı Saha Football, 2v2 Streetball Basketball, 2v2 Sand Volleyball).
    /// </summary>
    public interface IPlayableSportArena
    {
        bool IsPlayerActive { get; }
        int HudRevision { get; }
        bool AcceptsPrimaryAction { get; }
        bool HasSecondaryAction { get; }
        Vector3 ReturnPosition { get; }
        Quaternion ReturnRotation { get; }
        string HudText();
        string PrimaryButtonLabel();
        string SecondaryButtonLabel();
        string ControlsHintKey();
        void BeginPlayerMatch(PlayerFootController player);
        void EndPlayerMatch();
        void TriggerPrimaryAction();
        void TriggerSecondaryAction();
        void TriggerPrimaryRelease(float holdSeconds);
        void TriggerSecondaryRelease(float holdSeconds);
        SportHudMeterState GetHudMeterState();
    }


    internal static class SportLoc
    {
        public static bool IsEn => LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
        public static string L(string tr, string en) => IsEn ? en : tr;
    }
}
