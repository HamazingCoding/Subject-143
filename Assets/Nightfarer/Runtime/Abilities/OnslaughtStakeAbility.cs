using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Ultimate Art inspired by Wylder's Onslaught Stake: a hyper-armoured, briefly invulnerable wind-up, a lunge,
    /// then a ground-shattering blast in front of the character. Charged by dealing damage (see the config's
    /// ultimateGaugeDamage).
    /// </summary>
    [CreateAssetMenu(menuName = "Subject 143/Nightfarer/Abilities/Onslaught Stake (Ultimate)", fileName = "Ultimate_OnslaughtStake")]
    public class OnslaughtStakeAbility : AbilityData
    {
        public float windup = 0.7f;
        public float recovery = 0.75f;
        public float lungeDistance = 1.4f;
        public float blastRadius = 4.5f;
        public float blastArc = 220f;
        public float damage = 420f;
        public float poiseDamage = 250f;
        public Color blastColor = new Color(1f, 0.55f, 0.2f, 0.55f);
        [Tooltip("Anime impact frames (black/white, inverted) and a freeze when the blast lands.")]
        public bool impactFrames = true;
        public float impactFrameTime = 0.28f;
        [Tooltip("The air cracks open at the blast.")]
        public bool spaceCrack = true;

        public override AbilityInstance CreateInstance(NightfarerCharacter owner) => new Instance(owner, this);

        class Instance : AbilityInstance, IWeaponPoseProvider
        {
            readonly OnslaughtStakeAbility data;
            readonly SwingPath path;
            bool blasted;
            float lastLunge;

            public Instance(NightfarerCharacter owner, OnslaughtStakeAbility data) : base(owner)
            {
                this.data = data;
                float w = data.windup / (data.windup + data.recovery);
                path = new SwingPath();
                path.keys.Add(new SwingKey(0f, new Vector3(0.12f, 1.0f, 0.32f), new Vector3(0.15f, 0.75f, 0.65f)));
                path.keys.Add(new SwingKey(w * 0.55f, new Vector3(0.05f, 1.8f, -0.1f), new Vector3(0.05f, 0.3f, -0.95f)));
                path.keys.Add(new SwingKey(w * 0.85f, new Vector3(0.03f, 1.75f, 0.2f), new Vector3(0.0f, 0.95f, 0.3f)));
                path.keys.Add(new SwingKey(w, new Vector3(0f, 0.55f, 0.6f), new Vector3(0f, -0.95f, 0.3f)));
                path.keys.Add(new SwingKey(Mathf.Min(0.95f, w + 0.25f), new Vector3(0f, 0.55f, 0.6f), new Vector3(0f, -0.95f, 0.3f)));
                path.keys.Add(new SwingKey(1f, new Vector3(0.12f, 1.0f, 0.32f), new Vector3(0.15f, 0.75f, 0.65f)));
            }

            public override string Phase => IsFinished ? "Done" : blasted ? "Recovery" : "Ultimate wind-up";

            public override void Begin()
            {
                Owner.Animator.PlayAction(data.animationSlot, data.windup + data.recovery, 0.06f);
                Owner.Motor.SetPlanarVelocity(Vector3.zero);
                Owner.HasHyperArmor = true;
                Owner.IsInvulnerable = true;
                Owner.Notify(data.displayName + "!");
            }

            public bool TryGetPose(out WeaponPose pose, out float weight)
            {
                float u = Elapsed / (data.windup + data.recovery);
                pose = path.Sample(u);
                weight = Mathf.Clamp01(Mathf.Min(u / 0.05f, (1f - u) / 0.1f));
                return true;
            }

            public override void Tick(float dt)
            {
                Elapsed += dt;
                if (Owner.IsLocked) Owner.Motor.FaceDirection(Owner.LockTargetDirection(), 720f, dt);
                else if (Owner.MoveInputWorld().sqrMagnitude > 0.02f && Elapsed < data.windup * 0.5f)
                    Owner.Motor.FaceDirection(Owner.MoveInputWorld(), 540f, dt);

                float lu = Mathf.Clamp01((Elapsed - data.windup * 0.55f) / (data.windup * 0.45f));
                float p = Mathf.SmoothStep(0f, 1f, lu) * data.lungeDistance;
                if (!(Owner.IsLocked && Owner.DistanceToLockTarget() < 1.6f)) Owner.Motor.AddDisplacement(Owner.transform.forward * (p - lastLunge));
                lastLunge = p;

                if (!blasted && Elapsed >= data.windup) Blast();
                if (Elapsed >= data.windup + data.recovery) IsFinished = true;
            }

            void Blast()
            {
                blasted = true;
                Owner.IsInvulnerable = false;
                Vector3 centre = Owner.transform.position + Owner.transform.forward * (data.blastRadius * 0.45f);
                var seen = new System.Collections.Generic.HashSet<IDamageable>();
                foreach (var col in Physics.OverlapSphere(centre + Vector3.up, data.blastRadius, ~0, QueryTriggerInteraction.Collide))
                {
                    if (col.transform.IsChildOf(Owner.transform)) continue;
                    var target = col.GetComponentInParent<IDamageable>();
                    if (target == null || !target.IsAlive || !seen.Add(target)) continue;
                    Vector3 flat = target.transform.position - Owner.transform.position;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.01f && Vector3.Angle(Owner.transform.forward, flat) > data.blastArc * 0.5f) continue;
                    var info = new DamageInfo
                    {
                        amount = data.damage, poiseDamage = data.poiseDamage, point = col.ClosestPoint(centre),
                        direction = flat.normalized, source = Owner.gameObject, attackName = data.displayName,
                    };
                    target.ReceiveDamage(info);
                }
                Owner.Animator.HitStop(data.impactFrames ? data.impactFrameTime * 0.85f : 0.12f);
                if (Owner.cameraRig != null) Owner.cameraRig.Shake(0.35f, 0.45f);
                BlastEffect.Spawn(centre, data.blastRadius, data.blastColor);
                if (data.impactFrames) ScreenFX.ImpactFrames(data.impactFrameTime);
                ScreenFX.Blur(0.09f, 0.4f, centre + Vector3.up * 0.6f);
                if (data.spaceCrack)
                {
                    SpaceCrack.Spawn(centre + Vector3.up * 0.7f, data.blastRadius * 0.55f, null, 0.9f, 12);
                    GroundImpact.Spawn(centre, 0.8f, false);
                }
            }

            public override void End()
            {
                Owner.HasHyperArmor = false;
                Owner.IsInvulnerable = false;
            }
        }
    }
}
