using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Base class for special abilities (character skills). The data asset creates a runtime instance each
    /// time the ability is used; the character runs it inside its Ability state. New abilities only need a
    /// new AbilityData/AbilityInstance pair, no changes to the character.
    /// </summary>
    public abstract class AbilityData : ScriptableObject
    {
        public string displayName = "Ability";
        public float cooldown = 6f;
        public float staminaCost = 0f;
        public string animationSlot = "Skill";

        public abstract AbilityInstance CreateInstance(NightfarerCharacter owner);
    }

    public abstract class AbilityInstance
    {
        protected readonly NightfarerCharacter Owner;
        public float Elapsed { get; protected set; }
        public bool IsFinished { get; protected set; }
        public virtual string Phase => IsFinished ? "Done" : "Active";
        /// <summary>Set with IsFinished to leave the ability with a momentum jump (jump cancel).</summary>
        public bool LaunchJump { get; protected set; }

        protected AbilityInstance(NightfarerCharacter owner) { Owner = owner; }

        public abstract void Begin();

        public virtual void Tick(float dt) { Elapsed += dt; }

        /// <summary>Called when the ability finishes or is interrupted.</summary>
        public virtual void End() { }
    }
}
