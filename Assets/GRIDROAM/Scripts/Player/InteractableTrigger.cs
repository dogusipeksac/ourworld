using OurWorld.Core;
using UnityEngine;

namespace OurWorld.Player
{
    public enum InteractableKind
    {
        Npc,
        PlayerVehicle,
        InspectObject,
        PickupPackage,
        Door,
        Seat,
        PaintableHouse,
        TownVehicle,
        JobBoard,
        ShopCounter,
        Bed,
        FuelPump,
        VendingMachine,
        TownCitizen,
        HomeAppliance,
        MosquePrayer,
        StreetCat,
        Football,
        CarryableProp,
        AtmTerminal,
        Bicycle,
        Tennis,
        Motorcycle,
        ShopItem,
        SportsArena,
        FarmPlot,
        FarmAnimal,
        FarmMarket,
        FarmBoard,
        GarageRepairBay,
        GarageModBoard,
        FishingPier,
        CarWashStation,
        HomeDecorBoard,
        HomeMangal
    }

    /// <summary>
    /// Marks any world object the player can interact with
    /// (NPC, Vehicle, Clue, Package, Door, Seat, PaintableHouse, TownVehicle,
    /// JobBoard, ShopCounter, ShopItem, Bed, FuelPump, VendingMachine).
    /// </summary>
    public class InteractableTrigger : MonoBehaviour
    {
        [SerializeField] private InteractableKind kind = InteractableKind.Npc;
        [SerializeField] private string interactableId = "npc_mechanic";
        [SerializeField] private string promptLocalizationKey = "ui.btn.talk";
        [SerializeField] private float interactionRadius = 3.2f;
        [SerializeField] private string customPromptText = string.Empty;
        private string _customPromptEn = string.Empty;

        public InteractableKind Kind => kind;
        public string InteractableId => interactableId;
        public string PromptLocalizationKey => promptLocalizationKey;
        public float InteractionRadius => interactionRadius;

        public void Configure(InteractableKind newKind, string id, string promptKey, float radius)
        {
            kind = newKind;
            interactableId = id;
            promptLocalizationKey = promptKey;
            interactionRadius = radius;
        }

        public void SetCustomPrompt(string text)
        {
            customPromptText = text ?? string.Empty;
            _customPromptEn = string.Empty;
        }

        public void SetCustomPrompt(string textTr, string textEn)
        {
            customPromptText = textTr ?? string.Empty;
            _customPromptEn = textEn ?? string.Empty;
        }

        public string GetLocalizedPrompt()
        {
            bool isEn = LocalizationService.Instance != null && LocalizationService.Instance.CurrentLanguage == "en";
            if (kind == InteractableKind.ShopCounter && PlayerShoppingCart.HasActiveItems)
            {
                string prefix = isEn ? "Pay at Checkout" : "Kasada Öde";
                return $"{prefix} · {PlayerShoppingCart.ActiveSummary}";
            }
            if (!string.IsNullOrEmpty(customPromptText))
            {
                return (isEn && !string.IsNullOrEmpty(_customPromptEn)) ? _customPromptEn : customPromptText;
            }
            return LocalizationService.Tr(promptLocalizationKey, isEn ? "Interact" : "Etkileş");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.31f, 0.70f, 0.66f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
#endif
    }
}
