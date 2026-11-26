using System;
using System.Collections.Generic;
using UnityEngine;

namespace OurWorld.Missions
{
    public enum MissionStepType
    {
        ReachLocation,
        TalkToNpc,
        EnterVehicle,
        ExitVehicle,
        DeliverToZone,
        InspectTarget,
        TailVehicle,
        PlaySportMatch,
        SwimAtPool,
        RideTwoWheeler,
        DefeatHostiles,
        EvadePolice,
        StealthReach
    }

    [Serializable]
    public class MissionStepData
    {
        public MissionStepType stepType = MissionStepType.ReachLocation;

        [Tooltip("Localization key for the HUD objective text.")]
        public string objectiveKey;

        [Tooltip("WorldAnchor ID in the scene representing the target NPC, vehicle, or zone.")]
        public string targetAnchorId;

        [Tooltip("Override radius for ReachLocation / DeliverToZone. <= 0 uses WorldAnchor default.")]
        public float triggerRadius = 0f;

        [Tooltip("Optional dialogue played when interacting or reaching this step.")]
        public DialogueData dialogueAsset;

        [Tooltip("If > 0, starts a countdown timer for this step. Reaching 0 fails the mission.")]
        public float timeLimitSeconds = 0f;

        [Tooltip("If true, completing this step unlocks the player's sedan keys.")]
        public bool unlocksVehicleKeys = false;

        [Tooltip("Optional requirement: player must be inside vehicle to trigger zone completion.")]
        public bool requiresVehicle = false;

        [Tooltip("Optional requirement: player must be on foot to trigger zone completion.")]
        public bool requiresOnFoot = false;

        [Header("Tail Step Settings (Ready for Mission 4: Night Tail)")]
        public float tailMinDistance = 12f;
        public float tailMaxDistance = 65f;
        public float tailGraceSeconds = 6f;

        [Header("Branching Delivery Settings (Mission 5: End of Shift)")]
        [Tooltip("Optional alternative WorldAnchor ID (e.g. 'npc_elena_neighborhood') that also completes this step.")]
        public string secondaryTargetAnchorId;
        [Tooltip("Dialogue played if the player interacts with secondaryTargetAnchorId.")]
        public DialogueData secondaryDialogueAsset;
        public string primaryChoiceId;
        public string secondaryChoiceId;

        [Header("Atmosphere Override")]
        [Tooltip("If >= 0, transitions the world clock to this hour (0-24) when this step starts.")]
        public float setTimeOfDayHours = -1f;

        [Header("GTA Action Step Settings (Chapter 7-8)")]
        [Tooltip("For EvadePolice steps: Wanted Star level (1-5) triggered when the step starts.")]
        public int wantedStarsToTrigger = 0;

        [Tooltip("For DefeatHostiles steps: number of gang hostiles to knock out.")]
        public int hostileCount = 0;
    }

    /// <summary>
    /// Extensible ScriptableObject describing a single GRIDROAM mission.
    /// Adding new missions or neighborhoods only requires creating new MissionData assets
    /// and placing matching WorldAnchors in the scene—no mission C# rewrite required.
    /// </summary>
    [CreateAssetMenu(menuName = "OurWorld/Mission Data", fileName = "Mission_New")]
    public class MissionData : ScriptableObject
    {
        [Header("Identity & Localization Keys")]
        public string missionId = "m01_get_the_keys";
        public string titleKey = "m01.title";
        public string summaryKey = "m01.summary";
        public int rewardMoney = 100;

        [Header("Chaining & Free Roam")]
        [Tooltip("Next mission ID to queue upon completion. Leave empty for Free Roam.")]
        public string nextMissionId;

        [Tooltip("If true, automatically starts the next mission after a short completion banner.")]
        public bool autoStartNextMission = true;

        [Header("Retry Respawn Anchors")]
        public string retryPlayerAnchorId = "spawn_garage_foot";
        public string retryVehicleAnchorId = "spawn_garage_sedan";
        public bool retryStartInVehicle = false;

        [Header("Ordered Steps")]
        public List<MissionStepData> steps = new List<MissionStepData>();
    }
}
