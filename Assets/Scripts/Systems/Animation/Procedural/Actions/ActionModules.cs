using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Actions
{
    /// <summary>
    /// Attack phase controller: charge -> swing -> recovery with procedural upper-body IK.
    /// </summary>
    [BurstCompile]
    public struct AttackPhaseJob : IJob
    {
        public enum Phase { Charge, Swing, Recovery, None }

        [ReadOnly] public Phase CurrentPhase;
        [ReadOnly] public float PhaseTimer;
        [ReadOnly] public float ChargeDuration;
        [ReadOnly] public float SwingDuration;
        [ReadOnly] public float RecoveryDuration;
        [ReadOnly] public float3 TargetPosition;
        [ReadOnly] public float3 RightHandPos;
        [ReadOnly] public float3 RightElbowPos;
        [ReadOnly] public float3 RightShoulderPos;
        [ReadOnly] public float3 Spine1Pos;
        [ReadOnly] public quaternion Spine1Rot;

        public NativeArray<float3> OutRightHandTarget;
        public NativeArray<float3> OutRightHandHint;
        public NativeArray<quaternion> OutSpine1Rotation;
        public NativeArray<float> OutPhaseProgress;
        public NativeArray<bool> OutPhaseComplete;

        public void Execute()
        {
            float3 handTarget = RightHandPos;
            float3 handHint = RightElbowPos;
            quaternion spineRot = Spine1Rot;
            float progress = 0f;
            bool complete = false;

            if (CurrentPhase == Phase.Charge)
            {
                progress = math.saturate(PhaseTimer / ChargeDuration);
                float smooth = math.smoothstep(0f, 1f, progress);

                // Pull back: shoulder back, arm up
                handTarget = RightShoulderPos + math.mul(Spine1Rot, new float3(-0.3f, 0.2f, -0.2f));
                handHint = RightElbowPos + new float3(-0.2f, 0.1f, -0.1f);

                // Spine winds up
                spineRot = math.mul(Spine1Rot, quaternion.AxisAngle(math.up(), math.radians(20f * smooth)));
            }
            else if (CurrentPhase == Phase.Swing)
            {
                progress = math.saturate(PhaseTimer / SwingDuration);
                // Fast acceleration, slow deceleration
                float swingCurve = 1f - math.pow(1f - progress, 3f);

                // Swing toward target
                handTarget = math.lerp(RightHandPos, TargetPosition, swingCurve * 1.5f);
                handHint = RightElbowPos + math.normalize(TargetPosition - RightHandPos) * 0.5f;

                // Spine follows through
                float spineFollow = math.sin(progress * math.PI) * 30f;
                spineRot = math.mul(Spine1Rot, quaternion.AxisAngle(math.up(), math.radians(-spineFollow)));
            }
            else if (CurrentPhase == Phase.Recovery)
            {
                progress = math.saturate(PhaseTimer / RecoveryDuration);
                float smooth = math.smoothstep(0f, 1f, progress);

                // Return to neutral
                handTarget = math.lerp(TargetPosition, RightHandPos, smooth);
                handHint = math.lerp(RightElbowPos + math.normalize(TargetPosition - RightHandPos) * 0.5f, RightElbowPos, smooth);
                spineRot = math.slerp(Spine1Rot, quaternion.identity, smooth);

                if (progress >= 1f) complete = true;
            }

            OutRightHandTarget[0] = handTarget;
            OutRightHandHint[0] = handHint;
            OutSpine1Rotation[0] = spineRot;
            OutPhaseProgress[0] = progress;
            OutPhaseComplete[0] = complete;
        }
    }

    /// <summary>
    /// Weapon IK: positions weapon tip to hit target.
    /// </summary>
    [BurstCompile]
    public struct WeaponIKJob : IJob
    {
        [ReadOnly] public float3 WeaponTipPos;
        [ReadOnly] public float3 WeaponTipRot; // euler
        [ReadOnly] public float3 TargetPos;
        [ReadOnly] public float3 RightHandPos;
        [ReadOnly] public float3 RightElbowPos;
        [ReadOnly] public float3 RightShoulderPos;
        [ReadOnly] public float WeaponLength;
        [ReadOnly] public float MaxReach;

        public NativeArray<float3> OutRightHandTarget;
        public NativeArray<float3> OutRightHandHint;
        public NativeArray<float3> OutWeaponTipTarget;
        public NativeArray<bool> OutTargetReachable;

        public void Execute()
        {
            float3 toTarget = TargetPos - WeaponTipPos;
            float dist = math.length(toTarget);
            bool reachable = dist <= MaxReach;

            float3 handTarget = RightHandPos;
            float3 handHint = RightElbowPos;

            if (reachable && dist > 0.01f)
            {
                // Move hand so weapon tip reaches target
                float3 dir = math.normalize(toTarget);
                handTarget = RightHandPos + dir * math.min(dist, WeaponLength);
                handHint = RightElbowPos + dir * 0.3f;
            }

            OutRightHandTarget[0] = handTarget;
            OutRightHandHint[0] = handHint;
            OutWeaponTipTarget[0] = TargetPos;
            OutTargetReachable[0] = reachable;
        }
    }

    /// <summary>
    /// Body torque: pelvic/spine rotation for power generation in attacks/throws.
    /// </summary>
    [BurstCompile]
    public struct BodyTorqueJob : IJob
    {
        [ReadOnly] public float Progress; // 0-1 through action
        [ReadOnly] public float MaxTorqueAngle; // degrees
        [ReadOnly] public float3 TorqueAxis; // local axis
        [ReadOnly] public quaternion PelvisRotation;
        [ReadOnly] public quaternion Spine0Rotation;
        [ReadOnly] public quaternion Spine1Rotation;
        [ReadOnly] public quaternion Spine2Rotation;

        public NativeArray<quaternion> OutPelvisRotation;
        public NativeArray<quaternion> OutSpine0Rotation;
        public NativeArray<quaternion> OutSpine1Rotation;
        public NativeArray<quaternion> OutSpine2Rotation;

        public void Execute()
        {
            // Torque curve: wind up (0-0.3), release (0.3-0.7), follow-through (0.7-1)
            float torqueAmount = 0f;
            if (Progress < 0.3f)
            {
                torqueAmount = math.smoothstep(0f, 1f, Progress / 0.3f);
            }
            else if (Progress < 0.7f)
            {
                torqueAmount = 1f - math.smoothstep(0f, 1f, (Progress - 0.3f) / 0.4f);
            }
            // else 0

            torqueAmount *= math.radians(MaxTorqueAngle);
            quaternion torqueRot = quaternion.AxisAngle(math.normalize(TorqueAxis), torqueAmount);

            OutPelvisRotation[0] = math.mul(PelvisRotation, torqueRot);
            OutSpine0Rotation[0] = math.mul(Spine0Rotation, quaternion.Slerp(quaternion.identity, torqueRot, 0.3f));
            OutSpine1Rotation[0] = math.mul(Spine1Rotation, quaternion.Slerp(quaternion.identity, torqueRot, 0.6f));
            OutSpine2Rotation[0] = math.mul(Spine2Rotation, quaternion.Slerp(quaternion.identity, torqueRot, 0.3f));
        }
    }

    /// <summary>
    /// Hit reaction: procedural knockback/hitstun pose.
    /// </summary>
    [BurstCompile]
    public struct HitReactionJob : IJob
    {
        public enum ReactionType { Light, Heavy, Launch, Stagger }

        [ReadOnly] public ReactionType Type;
        [ReadOnly] public float TimeSinceHit;
        [ReadOnly] public float Duration;
        [ReadOnly] public float3 HitDirection; // normalized, from attacker
        [ReadOnly] public float Force;
        [ReadOnly] public quaternion RootRotation;
        [ReadOnly] public float3 Spine0Pos, Spine1Pos, Spine2Pos;
        [ReadOnly] public quaternion Spine0Rot, Spine1Rot, Spine2Rot;
        [ReadOnly] public float3 HeadPos;

        public NativeArray<quaternion> OutRootRotation;
        public NativeArray<quaternion> OutSpine0Rotation;
        public NativeArray<quaternion> OutSpine1Rotation;
        public NativeArray<quaternion> OutSpine2Rotation;
        public NativeArray<float3> OutHeadOffset;
        public NativeArray<float> OutReactionIntensity;
        public NativeArray<bool> OutIsActive;

        public void Execute()
        {
            float progress = math.saturate(TimeSinceHit / Duration);
            bool active = progress < 1f;

            float3 headOffset = float3(0);
            quaternion rootRot = RootRotation;
            quaternion s0 = Spine0Rot, s1 = Spine1Rot, s2 = Spine2Rot;
            float intensity = 0f;

            if (active)
            {
                // Ease out
                float curve = 1f - math.pow(progress, 2f);

                switch (Type)
                {
                    case ReactionType.Light:
                        // Small head snap, slight spine bend
                        intensity = Force * 0.5f * curve;
                        headOffset = -HitDirection * 0.1f * intensity;
                        s1 = math.mul(s1, quaternion.AxisAngle(math.right(), math.radians(5f * intensity)));
                        break;

                    case ReactionType.Heavy:
                        // Full body bend back
                        intensity = Force * curve;
                        headOffset = -HitDirection * 0.2f * intensity;
                        rootRot = math.mul(rootRot, quaternion.AxisAngle(math.right(), math.radians(10f * intensity)));
                        s0 = math.mul(s0, quaternion.AxisAngle(math.right(), math.radians(15f * intensity)));
                        s1 = math.mul(s1, quaternion.AxisAngle(math.right(), math.radians(20f * intensity)));
                        s2 = math.mul(s2, quaternion.AxisAngle(math.right(), math.radians(10f * intensity)));
                        break;

                    case ReactionType.Launch:
                        // Upward/backward
                        intensity = Force * curve;
                        headOffset = new float3(-HitDirection.x * 0.15f, 0.1f, -HitDirection.z * 0.15f) * intensity;
                        rootRot = math.mul(rootRot, quaternion.AxisAngle(math.right(), math.radians(-20f * intensity)));
                        s0 = math.mul(s0, quaternion.AxisAngle(math.right(), math.radians(-15f * intensity)));
                        break;

                    case ReactionType.Stagger:
                        // Side lean, off-balance
                        intensity = Force * curve;
                        float3 sideDir = math.normalize(math.cross(HitDirection, math.up()));
                        headOffset = sideDir * 0.15f * intensity;
                        rootRot = math.mul(rootRot, quaternion.AxisAngle(math.forward(), math.radians(15f * intensity)));
                        s1 = math.mul(s1, quaternion.AxisAngle(math.forward(), math.radians(10f * intensity)));
                        break;
                }
            }
            else
            {
                intensity = 0f;
            }

            OutRootRotation[0] = rootRot;
            OutSpine0Rotation[0] = s0;
            OutSpine1Rotation[0] = s1;
            OutSpine2Rotation[0] = s2;
            OutHeadOffset[0] = headOffset;
            OutReactionIntensity[0] = intensity;
            OutIsActive[0] = active;
        }
    }

    /// <summary>
    /// Reach IK: full-body IK to reach a target with hand(s).
    /// </summary>
    [BurstCompile]
    public struct ReachIKJob : IJob
    {
        [ReadOnly] public float3 TargetPos;
        [ReadOnly] public float3 LeftHandPos, RightHandPos;
        [ReadOnly] public float3 LeftElbowPos, RightElbowPos;
        [ReadOnly] public float3 LeftShoulderPos, RightShoulderPos;
        [ReadOnly] public float3 Spine1Pos;
        [ReadOnly] public quaternion LeftShoulderRot, RightShoulderRot;
        [ReadOnly] public bool UseLeftHand, UseRightHand;
        [ReadOnly] public float MaxReach;
        [ReadOnly] public float SpineInfluence; // 0-1

        public NativeArray<float3> OutLeftHandTarget;
        public NativeArray<float3> OutRightHandTarget;
        public NativeArray<float3> OutLeftHandHint;
        public NativeArray<float3> OutRightHandHint;
        public NativeArray<quaternion> OutSpine1Rotation;
        public NativeArray<bool> OutLeftReachable;
        public NativeArray<bool> OutRightReachable;

        public void Execute()
        {
            float3 lTarget = LeftHandPos;
            float3 rTarget = RightHandPos;
            float3 lHint = LeftElbowPos;
            float3 rHint = RightElbowPos;
            quaternion spine1Rot = quaternion.identity;
            bool lReach = false, rReach = false;

            if (UseLeftHand)
            {
                float3 toTarget = TargetPos - LeftShoulderPos;
                float dist = math.length(toTarget);
                lReach = dist <= MaxReach;
                if (lReach && dist > 0.01f)
                {
                    lTarget = math.lerp(LeftHandPos, TargetPos, math.min(1f, MaxReach / dist));
                    lHint = LeftElbowPos + math.normalize(toTarget) * 0.3f;

                    // Spine rotates toward target
                    float spineAngle = math.asin(math.min(1f, dist / MaxReach)) * SpineInfluence * 0.5f;
                    spine1Rot = quaternion.AxisAngle(math.up(), spineAngle);
                }
            }

            if (UseRightHand)
            {
                float3 toTarget = TargetPos - RightShoulderPos;
                float dist = math.length(toTarget);
                rReach = dist <= MaxReach;
                if (rReach && dist > 0.01f)
                {
                    rTarget = math.lerp(RightHandPos, TargetPos, math.min(1f, MaxReach / dist));
                    rHint = RightElbowPos + math.normalize(toTarget) * 0.3f;

                    float spineAngle = math.asin(math.min(1f, dist / MaxReach)) * SpineInfluence * 0.5f;
                    spine1Rot = math.mul(spine1Rot, quaternion.AxisAngle(math.up(), -spineAngle));
                }
            }

            OutLeftHandTarget[0] = lTarget;
            OutRightHandTarget[0] = rTarget;
            OutLeftHandHint[0] = lHint;
            OutRightHandHint[0] = rHint;
            OutSpine1Rotation[0] = spine1Rot;
            OutLeftReachable[0] = lReach;
            OutRightReachable[0] = rReach;
        }
    }

    /// <summary>
    /// Gather motion: bend down -> grab -> stand up.
    /// </summary>
    [BurstCompile]
    public struct GatherMotionJob : IJob
    {
        public enum Phase { Approach, Bend, Grab, Rise, None }

        [ReadOnly] public Phase CurrentPhase;
        [ReadOnly] public float PhaseTimer;
        [ReadOnly] public float ApproachDur, BendDur, GrabDur, RiseDur;
        [ReadOnly] public float3 TargetPos;
        [ReadOnly] public float3 LeftHandPos, RightHandPos;
        [ReadOnly] public float3 LeftElbowPos, RightElbowPos;
        [ReadOnly] public float3 Spine0Pos, Spine1Pos, Spine2Pos;
        [ReadOnly] public quaternion Spine0Rot, Spine1Rot, Spine2Rot;
        [ReadOnly] public float MaxBendAngle; // degrees

        public NativeArray<float3> OutLeftHandTarget;
        public NativeArray<float3> OutRightHandTarget;
        public NativeArray<quaternion> OutSpine0Rotation;
        public NativeArray<quaternion> OutSpine1Rotation;
        public NativeArray<quaternion> OutSpine2Rotation;
        public NativeArray<float> OutPhaseProgress;
        public NativeArray<bool> OutPhaseComplete;

        public void Execute()
        {
            float3 lTarget = LeftHandPos;
            float3 rTarget = RightHandPos;
            quaternion s0 = Spine0Rot, s1 = Spine1Rot, s2 = Spine2Rot;
            float progress = 0f;
            bool complete = false;

            if (CurrentPhase == Phase.Approach)
            {
                progress = math.saturate(PhaseTimer / ApproachDur);
                // Hands move toward target
                lTarget = math.lerp(LeftHandPos, TargetPos, progress);
                rTarget = math.lerp(RightHandPos, TargetPos, progress);
            }
            else if (CurrentPhase == Phase.Bend)
            {
                progress = math.saturate(PhaseTimer / BendDur);
                float smooth = math.smoothstep(0f, 1f, progress);
                float bend = MaxBendAngle * smooth;

                s0 = math.mul(s0, quaternion.AxisAngle(math.right(), math.radians(bend * 0.2f)));
                s1 = math.mul(s1, quaternion.AxisAngle(math.right(), math.radians(bend * 0.5f)));
                s2 = math.mul(s2, quaternion.AxisAngle(math.right(), math.radians(bend * 0.3f)));

                lTarget = math.lerp(LeftHandPos, TargetPos, 0.7f + progress * 0.3f);
                rTarget = math.lerp(RightHandPos, TargetPos, 0.7f + progress * 0.3f);
            }
            else if (CurrentPhase == Phase.Grab)
            {
                progress = math.saturate(PhaseTimer / GrabDur);
                // Hold at target
                lTarget = TargetPos;
                rTarget = TargetPos;
            }
            else if (CurrentPhase == Phase.Rise)
            {
                progress = math.saturate(PhaseTimer / RiseDur);
                float smooth = 1f - math.smoothstep(0f, 1f, progress);
                float bend = MaxBendAngle * smooth;

                s0 = math.mul(s0, quaternion.AxisAngle(math.right(), math.radians(bend * 0.2f)));
                s1 = math.mul(s1, quaternion.AxisAngle(math.right(), math.radians(bend * 0.5f)));
                s2 = math.mul(s2, quaternion.AxisAngle(math.right(), math.radians(bend * 0.3f)));

                if (progress >= 1f) complete = true;
            }

            OutLeftHandTarget[0] = lTarget;
            OutRightHandTarget[0] = rTarget;
            OutSpine0Rotation[0] = s0;
            OutSpine1Rotation[0] = s1;
            OutSpine2Rotation[0] = s2;
            OutPhaseProgress[0] = progress;
            OutPhaseComplete[0] = complete;
        }
    }

    /// <summary>
    /// Roll trajectory: parabolic arc for dodge roll.
    /// </summary>
    [BurstCompile]
    public struct RollTrajectoryJob : IJob
    {
        [ReadOnly] public float3 StartPos;
        [ReadOnly] public float3 Direction; // normalized
        [ReadOnly] public float Distance;
        [ReadOnly] public float Height; // arc height
        [ReadOnly] public float Time;
        [ReadOnly] public float Duration;

        public NativeArray<float3> OutPosition;
        public NativeArray<float3> OutVelocity;
        public NativeArray<float> OutProgress;
        public NativeArray<bool> OutIsComplete;

        public void Execute()
        {
            float progress = math.saturate(Time / Duration);
            float smoothProgress = math.smoothstep(0f, 1f, progress);

            // Parabolic arc
            float3 pos = StartPos + Direction * Distance * smoothProgress;
            pos.y += math.sin(smoothProgress * math.PI) * Height;

            float3 vel = Direction * Distance / Duration;
            vel.y += math.cos(smoothProgress * math.PI) * math.PI * Height / Duration;

            OutPosition[0] = pos;
            OutVelocity[0] = vel;
            OutProgress[0] = smoothProgress;
            OutIsComplete[0] = progress >= 1f;
        }
    }

    /// <summary>
    /// Inverted pendulum: body rotation during roll.
    /// </summary>
    [BurstCompile]
    public struct InvertedPendulumJob : IJob
    {
        [ReadOnly] public float3 RollAxis; // forward = forward roll, right = side roll
        [ReadOnly] public float Progress; // 0-1
        [ReadOnly] public float MaxAngle; // degrees
        [ReadOnly] public quaternion BodyRotation;

        public NativeArray<quaternion> OutBodyRotation;
        public NativeArray<float> OutRollAngle;

        public void Execute()
        {
            // Full 360 rotation over roll duration
            float angle = Progress * 360f;
            quaternion rollRot = quaternion.AxisAngle(math.normalize(RollAxis), math.radians(angle));

            OutBodyRotation[0] = math.mul(BodyRotation, rollRot);
            OutRollAngle[0] = angle;
        }
    }
}