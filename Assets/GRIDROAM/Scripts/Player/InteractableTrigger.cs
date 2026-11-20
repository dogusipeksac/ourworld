using Gridroam.Core;
using UnityEngine;

namespace Gridroam.Player
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
        VendingMachine
    }

    /// <summary>
    /// Marks any world object the player can interact with
    /// (NPC, Vehicle, Clue, Package, Door, Seat, PaintableHouse, TownVehicle,
    /// JobBoard, ShopCounter, Bed, FuelPump, VendingMachine).
    /// </summary>
    public class InteractableTrigger : MonoBehaviour
    {
        [SerializeField] private InteractableKind kind = InteractableKind.Npc;
        [SerializeField] private string interactableId = "npc_mechanic";
        [SerializeField] private string promptLocalizationKey = "ui.btn.talk";
        [SerializeField] private float interactionRadius = 3.2f;

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

        public string GetLocalizedPrompt()
        {
            return LocalizationService.Tr(promptLocalizationKey, "Etkileş");
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
