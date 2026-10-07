using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Everything that defines one playable character: feel, weapons, skill and default animations.</summary>
    [CreateAssetMenu(menuName = "Subject 143/Nightfarer/Character Profile", fileName = "Profile_")]
    public class CharacterProfile : ScriptableObject
    {
        public string displayName = "Character";
        public NightfarerConfig config;
        public WeaponData[] weapons;
        public AbilityData skill;
        public AbilityData ultimate;
        public AnimationSet animationSet;
    }
}
