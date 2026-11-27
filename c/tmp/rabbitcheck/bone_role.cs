using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Bones
{
    /// <summary>
    /// Standardized bone roles for procedural animation.
    /// Used by both ProceduralBoneUtility and ProceduralBoneMap.
    /// </summary>
    public enum BoneRole
    {
        // Root
        Root,           // Hips / Pelvis / Root
        Hip,            // Hips (Root or child)

        // Spine chain (root → head)
        Spine0,         // Lower spine
        Spine1,         // Mid spine
        Spine2,         // Upper spine / Chest
        Spine3,         // Neck base
        Neck,           // Neck
        Head,           // Head

        // Left Arm
        L_Clavicle,     // Left clavicle / collar
        L_Shoulder,     // Left shoulder / upper arm
        L_Elbow,        // Left elbow / lower arm
        L_Wrist,        // Left wrist / hand
        L_Hand,         // Left hand (end effector)
        L_Fingers,      // Optional: finger root

        // Right Arm
        R_Clavicle,
        R_Shoulder,
        R_Elbow,
        R_Wrist,
        R_Hand,
        R_Fingers,

        // Left Leg
        L_Hip,          // Left hip / thigh root
        L_Knee,         // Left knee / shin
        L_Ankle,        // Left ankle / foot
        L_Foot,         // Left foot (end effector)
        L_Toes,         // Optional: toes

        // Right Leg
        R_Hip,
        R_Knee,
        R_Ankle,
        R_Foot,
        R_Toes,

        // Quadruped hind legs (2026-09-22 익명 리그 토폴로지 매핑 — 앞/뒷다리 4개 체인 완전 구동용)
        L_HindHip,
        L_HindKnee,
        L_HindAnkle,
        L_HindFoot,
        R_HindHip,
        R_HindKnee,
        R_HindAnkle,
        R_HindFoot,
    }

    /// <summary>
    /// 익명 리그(bone_N) 토폴로지 매핑의 계열 힌트 — 4족은 앞/뒤 4다리, 2족은 2다리+2팔로 배치한다.
    /// </summary>
    public enum BoneFamilyHint
    {
        None,
        Biped,
        Quadruped,
        Special,
    }
}