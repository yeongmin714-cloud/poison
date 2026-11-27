// Harness: replicates the exact rabbit fixture from
// Tests/EditMode/RabbitCrocodileAnimationMappingTests.cs (RabbitTopology_MapsFrontAndHindPairsSeparately)
// and reports the BuildMap runtime-role result + head-distance analysis.
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace RabbitCheck
{
    public static class Harness
    {
        static Transform Bone(GameObject parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        public static int Main()
        {
            var rig = new GameObject("RabbitRig");
            var animator = rig.AddComponent<Animator>();
            var root = Bone(rig, "bone_0", new Vector3(0f, 0.24f, -0.12f));
            var spine = Bone(root.gameObject, "bone_1", new Vector3(0f, 0.01f, 0.06f));
            var b2 = Bone(spine.gameObject, "bone_2", new Vector3(0f, 0.09f, 0f));
            var b3 = Bone(b2.gameObject, "bone_3", new Vector3(0f, 0.12f, 0.08f));
            var b4 = Bone(b3.gameObject, "bone_4", new Vector3(0f, 0.07f, 0f));
            var b9 = Bone(b4.gameObject, "bone_9", new Vector3(0f, 0.12f, 0f));
            var earL = Bone(b9.gameObject, "bone_10", new Vector3(0.09f, 0.20f, 0f));
            var earR = Bone(b9.gameObject, "bone_11", new Vector3(-0.09f, 0.20f, 0f));
            var lFront = Bone(b3.gameObject, "bone_12", new Vector3(0.086f, 0.019f, -0.019f));
            Bone(lFront.gameObject, "bone_13", new Vector3(0f, 0.078f, 0f));
            Bone(lFront.gameObject, "bone_14", new Vector3(0f, 0.129f, 0f));
            Bone(lFront.gameObject, "bone_15", new Vector3(0f, 0.104f, 0f));
            var rFront = Bone(b3.gameObject, "bone_16", new Vector3(-0.086f, 0.019f, -0.019f));
            Bone(rFront.gameObject, "bone_17", new Vector3(0f, 0.078f, 0f));
            Bone(rFront.gameObject, "bone_18", new Vector3(0f, 0.129f, 0f));
            Bone(rFront.gameObject, "bone_19", new Vector3(0f, 0.104f, 0f));
            var lHind = Bone(root.gameObject, "bone_21", new Vector3(0.129f, 0f, 0f));
            Bone(lHind.gameObject, "bone_22", new Vector3(0f, 0.119f, 0f));
            Bone(lHind.gameObject, "bone_23", new Vector3(0f, 0.094f, 0f));
            Bone(lHind.gameObject, "bone_24", new Vector3(0f, 0.085f, 0f));
            var rHind = Bone(root.gameObject, "bone_25", new Vector3(-0.129f, 0f, 0f));
            Bone(rHind.gameObject, "bone_26", new Vector3(0f, 0.119f, 0f));
            Bone(rHind.gameObject, "bone_27", new Vector3(0f, 0.094f, 0f));
            Bone(rHind.gameObject, "bone_28", new Vector3(0f, 0.085f, 0f));

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);

            Console.WriteLine("=== Role mapping ===");
            foreach (var role in new[] { BoneRole.Root, BoneRole.Spine0, BoneRole.Neck, BoneRole.Head,
                                         BoneRole.L_Hip, BoneRole.R_Hip, BoneRole.L_HindHip, BoneRole.R_HindHip })
                Console.WriteLine($"{role,-10} = {(map[role] != null ? map[role].name : "<null>")}");

            Console.WriteLine("--- Pair-mid hip-root distance to head chain tip (ear tips avg) ---");
            var earTipAvg = (earL.position + earR.position) * 0.5f;
            Console.WriteLine($"earTipAvg world = {earTipAvg}");
            var frontMid = (lFront.position + rFront.position) * 0.5f;
            var hindMid = (lHind.position + rHind.position) * 0.5f;
            Console.WriteLine($"front pair [{lFront.name},{rFront.name}] mid-dist = {Vector3.Distance(frontMid, earTipAvg):F4}");
            Console.WriteLine($"hind  pair [{lHind.name},{rHind.name}] mid-dist = {Vector3.Distance(hindMid, earTipAvg):F4}");
            return 0;
        }
    }
}