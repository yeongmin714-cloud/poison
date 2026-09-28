// [P-ANIM7 Phase4 수리] 4족 토폴로지 매핑 날개(어깨) 배치 EditMode 검증.
// 뿌리: FindLimbChains가 FillArmRoles를 호출하지 않아 L_Shoulder/R_Shoulder가 항상 미매핑 →
// ApplyWingFlap이 griffin/manticore 익명 리그(bone_N)에서 발동하지 않았다(QAPROGRESS P-ANIM7 ⚠️).
// 본 테스트는 익명 리그(bone_N) 합성 4족 리그를 에디터에서 구성해 다음을 검증한다.
//  ① 날개 체인이 좌우 미러 페어로 검출되어 어깨 역할(L/R_Shoulder)로 배치된다.
//  ② 날개 뼈는 척추 매핑 후보에서 제외된다(최장 체인이 날개로 새지 않는다 — Spine0/Head 무오염).
//  ③ 다리(앞/뒤 4개) 배치는 기존 규약대로 유지된다.
//  ④ 이름 사전 매핑("arm_l"류)으로 어깨가 이미 확보된 리그는 토폴로지 추정으로 덮어쓰지 않는다.
//     (BuildMap 이름 매핑 사코드 — prefill 후 !ContainsKey 항상 false — 수리가 전제)
//  ⑤ 날개 없는 4족(꼬리만)은 어깨 오탐 없이 미매핑을 유지한다.
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Tests.EditMode
{
    public class ProceduralBoneUtilityWingTests
    {
        private GameObject _rig;

        [TearDown]
        public void TearDown()
        {
            if (_rig != null)
                Object.DestroyImmediate(_rig);
        }

        // ──────────────────────────────────────────────
        // 익명 리그(bone_N) 합성 4족 리그 — 골반→척추→가슴 분기
        // (전/후 다리 4개 + 날개 페어 + 꼬리, 전부 월드 위치 부여 — 부모 회전/스케일 없음)
        // ──────────────────────────────────────────────
        private GameObject BuildQuadrupedRig(bool withWings, bool includeNameShoulder)
        {
            var root = new GameObject("Rig");
            root.AddComponent<Animator>();

            var b0 = AddBone(root, "bone_0", new Vector3(0f, 1.0f, 0.2f));    // 골반(트리 루트)
            var b1 = AddBone(b0.gameObject, "bone_1", new Vector3(0f, 1.1f, 0.1f));   // 척추
            var b2 = AddBone(b1.gameObject, "bone_2", new Vector3(0f, 1.15f, -0.1f)); // 가슴

            var b3 = AddBone(b2.gameObject, "bone_3", new Vector3(0f, 1.35f, -0.25f)); // 목
            AddBone(b3.gameObject, "bone_4", new Vector3(0f, 1.55f, -0.35f));          // 머리(잎)

            if (withWings)
            {
                var b5 = AddBone(b2.gameObject, "bone_5", new Vector3(0.45f, 1.25f, -0.1f));   // 날개L 뿌리
                AddBone(b5.gameObject, "bone_6", new Vector3(0.95f, 1.45f, -0.15f));           // 날개L 끝(잎)
                var b7 = AddBone(b2.gameObject, "bone_7", new Vector3(-0.45f, 1.25f, -0.1f));  // 날개R 뿌리
                AddBone(b7.gameObject, "bone_8", new Vector3(-0.95f, 1.45f, -0.15f));          // 날개R 끝(잎)
            }

            var b9 = AddBone(b2.gameObject, "bone_9", new Vector3(0.3f, 1.05f, -0.2f));   // 앞다리L
            AddBone(b9.gameObject, "bone_10", new Vector3(0.3f, 0.15f, -0.2f));           // 발L(잎)
            var b11 = AddBone(b2.gameObject, "bone_11", new Vector3(-0.3f, 1.05f, -0.2f)); // 앞다리R
            AddBone(b11.gameObject, "bone_12", new Vector3(-0.3f, 0.15f, -0.2f));         // 발R(잎)

            var b13 = AddBone(b0.gameObject, "bone_13", new Vector3(0.32f, 0.95f, 0.4f));  // 뒷다리L
            AddBone(b13.gameObject, "bone_14", new Vector3(0.32f, 0.1f, 0.4f));           // 발L(잎)
            var b15 = AddBone(b0.gameObject, "bone_15", new Vector3(-0.32f, 0.95f, 0.4f)); // 뒷다리R
            AddBone(b15.gameObject, "bone_16", new Vector3(-0.32f, 0.1f, 0.4f));          // 발R(잎)

            var b17 = AddBone(b0.gameObject, "bone_17", new Vector3(0f, 1.05f, 0.5f));    // 꼬리 뿌리(중앙)
            AddBone(b17.gameObject, "bone_18", new Vector3(0f, 0.9f, 0.75f));             // 꼬리 끝(잎)

            if (includeNameShoulder)
                AddBone(b2.gameObject, "arm_l", new Vector3(0.2f, 1.2f, -0.1f)); // 이름 매핑용 어깨(단독 잎 — 체인 후보 아님)

            return root;
        }

        private static Transform AddBone(GameObject parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos; // 부모 회전/스케일 1:1 → local == world 판정
            return go.transform;
        }

        // ──────────────────────────────────────────────
        // ①③② 날개 페어 → 어깨 배치 + 다리/척추 무오염
        // ──────────────────────────────────────────────
        [Test]
        public void BuildMap_QuadrupedWings_ShoulderRolesAssigned_AndSpineUnpolluted()
        {
            _rig = BuildQuadrupedRig(withWings: true, includeNameShoulder: false);
            var animator = _rig.GetComponent<Animator>();

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);

            // ① 날개 체인 뿌리가 어깨 역할로 배치(bone_5/bone_7 — 좌우 어느 쪽이 L인지는 리그 좌표계 의존이라 집합 비교)
            Assert.That(
                new[] { map[BoneRole.L_Shoulder] != null ? map[BoneRole.L_Shoulder].name : null,
                        map[BoneRole.R_Shoulder] != null ? map[BoneRole.R_Shoulder].name : null },
                Is.EquivalentTo(new[] { "bone_5", "bone_7" }),
                "날개 체인 뿌리가 L/R_Shoulder로 배치되어야 한다");

            // ③ 다리 배치 유지 — 앞다리=bone_9/bone_11, 뒷다리=bone_13/bone_15
            Assert.That(
                new[] { map[BoneRole.L_Hip]?.name, map[BoneRole.R_Hip]?.name },
                Is.EquivalentTo(new[] { "bone_9", "bone_11" }), "앞다리 배치 유지");
            Assert.That(
                new[] { map[BoneRole.L_HindHip]?.name, map[BoneRole.R_HindHip]?.name },
                Is.EquivalentTo(new[] { "bone_13", "bone_15" }), "뒷다리 배치 유지");

            // ② 날개 뼈 제외 후 척추 매핑 — Spine0/Head가 날개/다리 뼈로 오염되지 않는다
            Assert.AreEqual("bone_1", map[BoneRole.Spine0]?.name, "Spine0은 척추 뼈여야 한다(날개/다리 오염 금지)");
            Assert.AreEqual("bone_4", map[BoneRole.Head]?.name, "Head는 머리 뼈여야 한다(날개 오염 금지)");
            Assert.AreNotEqual("bone_9", map[BoneRole.Neck]?.name, "Neck에 다리 뼈 배치 금지");
        }

        // ──────────────────────────────────────────────
        // ④ 이름 매핑 어깨 보존 — 토폴로지 추정 덮어쓰기 없음
        // ──────────────────────────────────────────────
        [Test]
        public void BuildMap_NameMappedShoulder_NotOverwrittenByTopology()
        {
            _rig = BuildQuadrupedRig(withWings: true, includeNameShoulder: true);
            var animator = _rig.GetComponent<Animator>();

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);

            Assert.AreEqual("arm_l", map[BoneRole.L_Shoulder]?.name,
                "이름 사전 매핑 어깨는 토폴로지 추정으로 덮어쓰지 않아야 한다");
            Assert.IsNull(map[BoneRole.R_Shoulder],
                "이름 매핑이 한쪽만 확보한 상태에서 날개 페어를 절반만 배치하지 않아야 한다(보수 스킵)");
        }

        // ──────────────────────────────────────────────
        // ⑤ 날개 없는 4족 — 꼬리/머리 장식이 어깨로 오탐되지 않는다
        // ──────────────────────────────────────────────
        [Test]
        public void BuildMap_QuadrupedWithoutWings_ShouldersRemainNull()
        {
            _rig = BuildQuadrupedRig(withWings: false, includeNameShoulder: false);
            var animator = _rig.GetComponent<Animator>();

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);

            Assert.IsNull(map[BoneRole.L_Shoulder], "날개 없는 리그에서 어깨 오탐 금지(꼬리=중앙 부착)");
            Assert.IsNull(map[BoneRole.R_Shoulder], "날개 없는 리그에서 어깨 오탐 금지");
            Assert.AreEqual("bone_1", map[BoneRole.Spine0]?.name, "날개 부재 시 척추 매핑 회귀 없음");
            Assert.AreEqual("bone_4", map[BoneRole.Head]?.name, "날개 부재 시 Head 매핑 회귀 없음");
        }

        // [회귀] Griffin의 legs pair 검증이 실패해도 독립적인 상반부 wing pair는 보존한다.
        [Test]
        public void BuildMap_QuadrupedWithRejectedLegPair_StillMapsBothWings()
        {
            _rig = new GameObject("GriffinWingPairWithRejectedLegs");
            var animator = _rig.AddComponent<Animator>();
            var root = AddBone(_rig, "bone_0", new Vector3(0f, 0.2f, 0f));
            var spine = AddBone(root.gameObject, "bone_1", new Vector3(0f, 0.3f, 0f));
            var chest = AddBone(spine.gameObject, "bone_2", new Vector3(0f, 0.4f, 0f));
            var head = AddBone(chest.gameObject, "bone_3", new Vector3(0f, 0.5f, 0f));
            AddBone(head.gameObject, "bone_4", new Vector3(0f, 0.55f, 0.02f));

            // 다리 후보 루트가 0.04m 떨어져 있어 current distinctRoots의 금지 구간(0.02~0.08)에 든다.
            var legL = AddBone(chest.gameObject, "bone_14", new Vector3(-0.02f, 0.35f, 0.02f));
            AddBone(legL.gameObject, "bone_15", new Vector3(-0.06f, 0.20f, 0.02f));
            var legR = AddBone(chest.gameObject, "bone_18", new Vector3(0.02f, 0.35f, 0.02f));
            AddBone(legR.gameObject, "bone_19", new Vector3(0.06f, 0.20f, 0.02f));

            var wingL = AddBone(chest.gameObject, "bone_10", new Vector3(-0.08f, 0.42f, 0.02f));
            AddBone(wingL.gameObject, "bone_11", new Vector3(-0.80f, 0.46f, 0.02f));
            var wingR = AddBone(chest.gameObject, "bone_6", new Vector3(0.08f, 0.42f, 0.02f));
            AddBone(wingR.gameObject, "bone_7", new Vector3(0.80f, 0.46f, 0.02f));

            var map = ProceduralBoneUtility.BuildMap(animator, BoneFamilyHint.Quadruped);

            Assert.That(map[BoneRole.L_Shoulder]?.name, Is.EqualTo("bone_10"));
            Assert.That(map[BoneRole.R_Shoulder]?.name, Is.EqualTo("bone_6"));
            Assert.That(map[BoneRole.L_Hip], Is.Null);
            Assert.That(map[BoneRole.R_Hip], Is.Null);
            Assert.That(map[BoneRole.Head], Is.Not.SameAs(map[BoneRole.L_Shoulder]),
                "날개 본을 Head로 겸용하면 HeadLook과 flap이 충돌한다");
            Assert.That(map[BoneRole.Head], Is.Not.SameAs(map[BoneRole.R_Shoulder]),
                "어느 쪽 날개도 Head 역할로 동시에 구동하지 않는다");
        }

        [Test]
        public void SynchronizedFlapAxis_MirroredWingsMoveInSameVerticalDirection()
        {
            var leftDirection = new Vector3(-0.31f, -0.12f, 0.03f).normalized;
            var rightDirection = new Vector3(0.31f, -0.12f, 0.03f).normalized;
            var up = Vector3.up;
            var leftAxis = QuadrupedProceduralAnimation.GetSynchronizedFlapAxis(leftDirection, up);
            var rightAxis = QuadrupedProceduralAnimation.GetSynchronizedFlapAxis(rightDirection, up);

            var leftMotion = Vector3.Cross(leftAxis, leftDirection);
            var rightMotion = Vector3.Cross(rightAxis, rightDirection);
            Assert.That(Vector3.Dot(leftMotion.normalized, up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(rightMotion.normalized, up), Is.GreaterThan(0.999f));
        }
    }
}