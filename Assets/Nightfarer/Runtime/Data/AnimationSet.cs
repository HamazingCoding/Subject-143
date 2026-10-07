using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Maps logical slots (see <see cref="AnimationSlots"/>) to humanoid clips. Empty slots fall back to
    /// the character's fallback set. To use your own animations, duplicate a set and drag clips in.
    /// </summary>
    [CreateAssetMenu(menuName = "Subject 143/Nightfarer/Animation Set", fileName = "AnimSet_")]
    public class AnimationSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string slot;
            public AnimationClip clip;
        }

        public string displayName = "Animation Set";
        [TextArea] public string notes;
        public List<Entry> entries = new List<Entry>();

        public AnimationClip Get(string slot)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].slot == slot) return entries[i].clip;
            return null;
        }

        public void Set(string slot, AnimationClip clip)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].slot != slot) continue;
                entries[i].clip = clip;
                return;
            }
            entries.Add(new Entry { slot = slot, clip = clip });
        }
    }
}
