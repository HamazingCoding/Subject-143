using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>A weapon: its model, how it sits in the hand, its blade hit volume and its moveset.</summary>
    [CreateAssetMenu(menuName = "Subject 143/Nightfarer/Weapon", fileName = "Weapon_")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "Weapon";

        [Header("Model")]
        public GameObject modelPrefab;
        public HumanBodyBones attachBone = HumanBodyBones.RightHand;
        [Tooltip("Derive the grip from the rig's finger/thumb bones; offsets below then fine-tune it.")]
        public bool autoGrip = true;
        public Vector3 gripPositionOffset;
        public Vector3 gripRotationOffset;

        [Header("Handling (procedural weapon layer)")]
        [Tooltip("Off hand joins the grip (guard and attacks).")]
        public bool twoHanded = true;
        [Tooltip("Where the off hand holds the handle, in weapon local space (metres).")]
        public Vector3 secondGrip = new Vector3(0f, 0.15f, 0f);
        [Tooltip("Standing / locked-on guard pose.")]
        public SwingKey guardPose = new SwingKey(0f, new Vector3(0.12f, 1.0f, 0.32f), new Vector3(0.15f, 0.75f, 0.65f));
        [Tooltip("Pose while moving freely.")]
        public SwingKey carryPose = new SwingKey(0f, new Vector3(0.32f, 0.92f, 0.05f), new Vector3(0.15f, -0.35f, -0.92f));

        [Header("Claws (natural weapons: no model, hit volumes follow the hands)")]
        public bool claws;
        [Tooltip("The hand that carries the main claw.")]
        public HumanBodyBones primaryHand = HumanBodyBones.LeftHand;
        [Tooltip("Claw reach beyond the wrist, along the fingers (metres).")]
        public float primaryClawLength = 0.26f;
        public float primaryClawRadius = 0.11f;
        public float offClawLength = 0.12f;
        public float offClawRadius = 0.08f;
        [Tooltip("Off-hand ready pose (claws).")]
        public SwingKey offGuardPose = new SwingKey(0f, new Vector3(0.22f, 0.95f, 0.25f), new Vector3(0.2f, 0.3f, 0.93f));

        [Header("Blade hit volume (weapon local space)")]
        public Vector3 bladeStart = new Vector3(0f, 0.15f, 0f);
        public Vector3 bladeEnd = new Vector3(0f, 1.3f, 0f);
        public float bladeRadius = 0.14f;

        [Header("Moveset")]
        [Tooltip("Generated movesets are rebuilt when their version changes.")]
        public int movesetVersion;
        public List<AttackData> lightChain = new List<AttackData>();
        public List<AttackData> heavyChain = new List<AttackData>();
        public AttackData sprintAttack;
        public AttackData jumpAttack;
        public AttackData rollAttack;
        public AttackData backstepAttack;
    }
}
