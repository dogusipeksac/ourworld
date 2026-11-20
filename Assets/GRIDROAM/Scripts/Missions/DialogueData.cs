using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gridroam.Missions
{
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Localization key for the speaker name, e.g. 'npc.mechanic.name'.")]
        public string speakerKey;

        [Tooltip("Localization key for the spoken line, e.g. 'm01.dlg.1'.")]
        public string textKey;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string choiceId;
        public string labelKey;
        public DialogueData followUpDialogue;
    }

    /// <summary>
    /// Data-only dialogue asset referencing localization keys so English and future Turkish
    /// (or other) translations live outside Unity scenes and scripts.
    /// </summary>
    [CreateAssetMenu(menuName = "GRIDROAM/Dialogue Data", fileName = "DLG_New")]
    public class DialogueData : ScriptableObject
    {
        public List<DialogueLine> lines = new List<DialogueLine>();
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }
}
