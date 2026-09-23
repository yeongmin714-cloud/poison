using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Tests.EditMode
{
    public class ManticoreCrocodileMappingTests
    {
        GameObject _rig;

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
        }

        static Transform Bone(GameObject parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        [Test]
        public void Crocodile_CoincidentLimbBasesWithMirroredDistalChains_MapAsPair()
        {
            _rig = new GameObject("CrocRig");
            var animator = _rig.AddComponent<Animator>();
            var root = Bone(_rig, "bone_0", new Vector3(0f, .3f, .1f));
            var body = Bone(root.gameObject, "bone_1", new Vector3(0f, .02f, 0f));
            var shoulder = Bone(body.gameObject, "bone_2", new Vector3(0f, .18f, .03f));
            var left = Bone(shoulder.gameObject, "bone_22", new Vector3(0f, -.22f, 0f));
            var l1 = Bone(left.gameObject, "bone_23", new Vector3(.16f, -.08f, .06f));
            var l2 = Bone(l1.gameObject, "bone_24", new Vector3(-.004f, -.117f, -.08f));
            Bone(l2.gameObject, "bone_25", new Vector3(.035f, -.063f, .074f));
            var right = Bone(shoulder.gameObject, "bone_26", new Vector3(0f, -.22f, 0f));
            var r1 = Bone(right.gameObject, "bone_27", new Vector3(-.16f, -.08f, .06f));
            var r2 = Bone(r1.gameObject, "bone_28", new Vector3(.004f, -.117f, -.08f));
            Bone(r2.gameObject, "bone_29", new Vector3(-.035f, -.063f, .074f));
            var tail = Bone(root, "bone_30", new Vector3(0f, -.1f, -.18f));
            var tail1 = Bone(tail.gameObject, "bone_31", new Vector3(0f, -.14f, -.12f));
            Bone(tail1.gameObject, "bone_32", new Vector3(0f, -.16f, -.1f));

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);
            Assert.That(map[BoneRole.L_Hip].name, Is.EqualTo("bone_22"));
            Assert.That(map[BoneRole.R_Hip].name, Is.EqualTo("bone_26"));
            Assert.That(map[BoneRole.L_Knee].name, Is.EqualTo("bone_23"));
            Assert.That(map[BoneRole.R_Knee].name, Is.EqualTo("bone_27"));
        }

        [Test]
        public void Manticore_FourDistinctMirroredLimbs_MapAndTailIsExcluded()
        {
            _rig = new GameObject("ManticoreRig");
            var animator = _rig.AddComponent<Animator>();
            var root = Bone(_rig, "bone_0", new Vector3(0f, .3f, 0f));
            var spine = Bone(root.gameObject, "bone_1", new Vector3(0f, .03f, .05f));
            var torso = Bone(spine.gameObject, "bone_2", new Vector3(0f, .03f, .07f));
            var chest = Bone(torso.gameObject, "bone_3", new Vector3(0f, .03f, .1f));
            var neck = Bone(chest.gameObject, "bone_4", new Vector3(0f, .04f, .03f));
            var head = Bone(neck.gameObject, "bone_5", new Vector3(0f, .04f, .03f));
            Bone(head.gameObject, "bone_12", new Vector3(0f, .04f, .03f));

            var fl = Bone(chest.gameObject, "bone_13", new Vector3(-.16f, -.05f, .02f));
            Bone(fl.gameObject, "bone_14", new Vector3(.078f, -.016f, -.012f));
            Bone(fl.GetChild(0).gameObject, "bone_15", new Vector3(-.008f, -.09f, -.024f));
            Bone(fl.GetChild(0).GetChild(0).gameObject, "bone_16", new Vector3(-.016f, -.101f, .016f));
            var fr = Bone(chest.gameObject, "bone_17", new Vector3(.16f, -.05f, .02f));
            Bone(fr.gameObject, "bone_18", new Vector3(-.078f, -.016f, -.012f));
            Bone(fr.GetChild(0).gameObject, "bone_19", new Vector3(.008f, -.09f, -.024f));
            Bone(fr.GetChild(0).GetChild(0).gameObject, "bone_20", new Vector3(.016f, -.101f, .016f));

            var hl = Bone(root, "bone_23", new Vector3(-.17f, -.08f, -.10f));
            Bone(hl.gameObject, "bone_24", new Vector3(.004f, -.113f, -.004f));
            Bone(hl.GetChild(0).gameObject, "bone_25", new Vector3(0f, -.078f, -.035f));
            Bone(hl.GetChild(0).GetChild(0).gameObject, "bone_26", new Vector3(.012f, -.054f, .008f));
            var hr = Bone(root, "bone_27", new Vector3(.17f, -.08f, -.10f));
            Bone(hr.gameObject, "bone_28", new Vector3(-.004f, -.113f, -.004f));
            Bone(hr.GetChild(0).gameObject, "bone_29", new Vector3(0f, -.078f, -.035f));
            Bone(hr.GetChild(0).GetChild(0).gameObject, "bone_30", new Vector3(-.012f, -.054f, .008f));

            var tail = Bone(root, "bone_31", new Vector3(0f, .023f, -.054f));
            var tail1 = Bone(tail.gameObject, "bone_32", new Vector3(.012f, -.081f, -.059f));
            var tail2 = Bone(tail1.gameObject, "bone_33", new Vector3(.031f, -.074f, -.058f));
            Bone(tail2.gameObject, "bone_34", new Vector3(.082f, -.032f, -.066f));

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);
            Assert.That(map[BoneRole.L_Hip].name, Is.EqualTo("bone_13"));
            Assert.That(map[BoneRole.R_Hip].name, Is.EqualTo("bone_17"));
            Assert.That(map[BoneRole.L_HindHip].name, Is.EqualTo("bone_23"));
            Assert.That(map[BoneRole.R_HindHip].name, Is.EqualTo("bone_27"));
            foreach (var role in new[] { BoneRole.L_Hip, BoneRole.R_Hip, BoneRole.L_HindHip, BoneRole.R_HindHip })
                Assert.That(map[role].name, Does.Not.StartWith("bone_31"));
        }
    }
}
