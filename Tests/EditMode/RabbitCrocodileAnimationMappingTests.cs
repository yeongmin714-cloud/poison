using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Tests.EditMode
{
    public class RabbitCrocodileAnimationMappingTests
    {
        private GameObject _rig;

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
        }

        private static Transform Bone(GameObject parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        [Test]
        public void RabbitTopology_HeadProximalPairMapsToFront_AndHindPairStaysDistinct()
        {
            _rig = new GameObject("RabbitRig");
            var animator = _rig.AddComponent<Animator>();
            var root = Bone(_rig, "bone_0", new Vector3(0f, 0.8f, 0f));
            var spine = Bone(root.gameObject, "bone_1", new Vector3(0f, 0.05f, 0f));
            var chest = Bone(spine.gameObject, "bone_2", new Vector3(0f, 0.1f, 0.1f));
            var neck = Bone(chest.gameObject, "bone_3", new Vector3(0f, 0.1f, 0.15f));
            var head = Bone(neck.gameObject, "bone_4", new Vector3(0f, 0.1f, 0.15f));
            Bone(head.gameObject, "bone_5", new Vector3(0f, 0.08f, 0.08f));

            var frontL = Bone(chest.gameObject, "bone_12", new Vector3(-0.18f, -0.05f, 0.04f));
            var frontL1 = Bone(frontL.gameObject, "bone_13", new Vector3(0f, -0.16f, 0f));
            var frontL2 = Bone(frontL1.gameObject, "bone_14", new Vector3(0f, -0.15f, 0f));
            Bone(frontL2.gameObject, "bone_15", new Vector3(0f, -0.12f, 0f));
            var frontR = Bone(chest.gameObject, "bone_16", new Vector3(0.18f, -0.05f, 0.04f));
            var frontR1 = Bone(frontR.gameObject, "bone_17", new Vector3(0f, -0.16f, 0f));
            var frontR2 = Bone(frontR1.gameObject, "bone_18", new Vector3(0f, -0.15f, 0f));
            Bone(frontR2.gameObject, "bone_19", new Vector3(0f, -0.12f, 0f));

            var hindL = Bone(root, "bone_21", new Vector3(-0.28f, -0.12f, -0.28f));
            var hindL1 = Bone(hindL.gameObject, "bone_22", new Vector3(0f, -0.15f, 0f));
            var hindL2 = Bone(hindL1.gameObject, "bone_23", new Vector3(0f, -0.13f, 0f));
            Bone(hindL2.gameObject, "bone_24", new Vector3(0f, -0.10f, 0f));
            var hindR = Bone(root, "bone_25", new Vector3(0.28f, -0.12f, -0.28f));
            var hindR1 = Bone(hindR.gameObject, "bone_26", new Vector3(0f, -0.15f, 0f));
            var hindR2 = Bone(hindR1.gameObject, "bone_27", new Vector3(0f, -0.13f, 0f));
            Bone(hindR2.gameObject, "bone_28", new Vector3(0f, -0.10f, 0f));

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);
            Assert.That(map[BoneRole.L_Hip].name, Is.EqualTo("bone_12"));
            Assert.That(map[BoneRole.R_Hip].name, Is.EqualTo("bone_16"));
            Assert.That(map[BoneRole.L_HindHip].name, Is.EqualTo("bone_21"));
            Assert.That(map[BoneRole.R_HindHip].name, Is.EqualTo("bone_25"));
        }

        [Test]
        public void CrocodileBranchedShoulder_CoincidentRoots_DivergentDescendantsFormLeftRightPair()
        {
            _rig = new GameObject("CrocBranchedShoulderRig");
            var animator = _rig.AddComponent<Animator>();
            var root = Bone(_rig, "bone_0", new Vector3(0f, 0.28f, 0.10f));
            var b1 = Bone(root.gameObject, "bone_1", new Vector3(0f, 0.016f, 0f));
            var body = Bone(b1.gameObject, "bone_2", new Vector3(0f, 0.185f, 0f));
            var head = Bone(body.gameObject, "bone_3", new Vector3(0f, 0.149f, 0.021f));
            var headTip = Bone(head.gameObject, "bone_4", new Vector3(0f, 0.12f, 0.08f));
            Bone(headTip.gameObject, "bone_5", new Vector3(0f, 0.08f, 0.04f));

            var left = Bone(body.gameObject, "bone_22", new Vector3(0f, -0.224f, 0.006f));
            var left1 = Bone(left.gameObject, "bone_23", new Vector3(0.156f, -0.078f, 0.063f));
            var left2 = Bone(left1.gameObject, "bone_24", new Vector3(-0.004f, -0.117f, -0.082f));
            Bone(left2.gameObject, "bone_25", new Vector3(0.035f, -0.063f, 0.074f));
            var right = Bone(body.gameObject, "bone_26", new Vector3(0f, -0.224f, 0.006f));
            var right1 = Bone(right.gameObject, "bone_27", new Vector3(-0.156f, -0.078f, 0.063f));
            var right2 = Bone(right1.gameObject, "bone_28", new Vector3(0.004f, -0.117f, -0.082f));
            Bone(right2.gameObject, "bone_29", new Vector3(-0.035f, -0.063f, 0.074f));

            var tail = Bone(root, "bone_30", new Vector3(0f, -0.094f, -0.172f));
            var tail1 = Bone(tail.gameObject, "bone_31", new Vector3(0f, 0.185f, 0f));
            Bone(tail1.gameObject, "bone_32", new Vector3(0f, 0.2f, 0f));

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);
            Assert.That(map[BoneRole.L_Hip], Is.Not.Null);
            Assert.That(map[BoneRole.R_Hip], Is.Not.Null);
            Assert.That(new[] { map[BoneRole.L_Hip].name, map[BoneRole.R_Hip].name }, Is.EquivalentTo(new[] { "bone_22", "bone_26" }));
            Assert.That(map[BoneRole.L_Hip], Is.Not.SameAs(map[BoneRole.R_Hip]));
            Assert.That(map[BoneRole.L_Knee].name, Is.EqualTo("bone_23"));
            Assert.That(map[BoneRole.R_Knee].name, Is.EqualTo("bone_27"));
        }

        [Test]
        public void ManticoreTopology_FourLimbsMapped_TailNeverMappedAsLegRole()
        {
            _rig = new GameObject("ManticoreRig");
            var animator = _rig.AddComponent<Animator>();
            var root = Bone(_rig, "bone_0", new Vector3(0f, 0.55f, 0f));
            var s1 = Bone(root.gameObject, "bone_1", new Vector3(0f, 0.12f, 0.05f));
            var s2 = Bone(s1.gameObject, "bone_2", new Vector3(0f, 0.14f, 0.08f));
            var chest = Bone(s2.gameObject, "bone_3", new Vector3(0f, 0.15f, 0.12f));
            var head = Bone(chest.gameObject, "bone_4", new Vector3(0f, 0.12f, 0.10f));
            var headTip = Bone(head.gameObject, "bone_5", new Vector3(0f, 0.10f, 0.12f));
            Bone(headTip.gameObject, "bone_12", new Vector3(0f, 0.10f, 0f));

            var fl = Bone(chest.gameObject, "bone_13", new Vector3(-0.16f, -0.06f, 0.02f));
            Bone(fl.gameObject, "bone_14", new Vector3(0f, -0.16f, 0f));
            Bone(fl.gameObject, "bone_15", new Vector3(0f, -0.31f, 0f));
            Bone(fl.gameObject, "bone_16", new Vector3(0f, -0.43f, 0f));
            var fr = Bone(chest.gameObject, "bone_17", new Vector3(0.16f, -0.06f, 0.02f));
            Bone(fr.gameObject, "bone_18", new Vector3(0f, -0.16f, 0f));
            Bone(fr.gameObject, "bone_19", new Vector3(0f, -0.31f, 0f));
            Bone(fr.gameObject, "bone_20", new Vector3(0f, -0.43f, 0f));

            var hl = Bone(root, "bone_23", new Vector3(-0.17f, -0.08f, -0.10f));
            Bone(hl.gameObject, "bone_24", new Vector3(0f, -0.17f, 0f));
            Bone(hl.gameObject, "bone_25", new Vector3(0f, -0.32f, 0f));
            Bone(hl.gameObject, "bone_26", new Vector3(0f, -0.43f, 0f));
            var hr = Bone(root, "bone_27", new Vector3(0.17f, -0.08f, -0.10f));
            Bone(hr.gameObject, "bone_28", new Vector3(0f, -0.17f, 0f));
            Bone(hr.gameObject, "bone_29", new Vector3(0f, -0.32f, 0f));
            Bone(hr.gameObject, "bone_30", new Vector3(0f, -0.43f, 0f));

            var tail = Bone(root, "bone_31", new Vector3(0.02f, -0.06f, -0.18f));
            var tail1 = Bone(tail.gameObject, "bone_32", new Vector3(0.05f, -0.18f, -0.06f));
            var tail2 = Bone(tail1.gameObject, "bone_33", new Vector3(0.08f, -0.35f, -0.10f));
            Bone(tail2.gameObject, "bone_34", new Vector3(0.10f, -0.50f, -0.12f));

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);
            Assert.That(map[BoneRole.L_Hip], Is.Not.Null);
            Assert.That(map[BoneRole.R_Hip], Is.Not.Null);
            Assert.That(map[BoneRole.L_HindHip], Is.Not.Null);
            Assert.That(map[BoneRole.R_HindHip], Is.Not.Null);
            Assert.That(map[BoneRole.L_Hip].name, Is.Not.EqualTo("bone_31"));
            Assert.That(map[BoneRole.R_Hip].name, Is.Not.EqualTo("bone_31"));
            Assert.That(map[BoneRole.L_HindHip].name, Is.Not.EqualTo("bone_31"));
            Assert.That(map[BoneRole.R_HindHip].name, Is.Not.EqualTo("bone_31"));
        }
    }
}
