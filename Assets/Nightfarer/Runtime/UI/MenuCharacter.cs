using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Title-screen character: holds the weapon and turns slowly on the stage.</summary>
    public class MenuCharacter : MonoBehaviour
    {
        public WeaponData weapon;
        public float turnSpeed = 6f;
        Animator anim;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            WeaponMount.PrepareGrip(anim, HumanBodyBones.RightHand);
        }

        void Start()
        {
            if (weapon != null) WeaponMount.Attach(anim, weapon, out _);
        }

        void Update() => transform.Rotate(0f, Mathf.Sin(Time.time * 0.3f) * turnSpeed * Time.deltaTime, 0f);
    }
}
