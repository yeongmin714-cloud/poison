using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [105차] 플레이어 장비 착용 수리 — 바디 측정 오염 차단 + 1.8m 스케일 폴백 상수 동행 검증.
    /// 스펙: docs/105_EQUIPMENT_FIT_BODY_MEASURE_POLLUTION_FIX_PLAN.md §F5.
    /// ①IsPartPlausible 순수 판정(오염 bounds → false / 정상 bounds → true — internal 경유 직접판정,
    ///   InternalsVisibleTo(ProjectName.Tests.EditMode) — NPCDialogueAdapter 어셈블리 선언 선례)
    /// ②소스 계약(무기 루트 제외 합침 + 파괴 예정 1프레임 유지 / NaturalBodyHeight 1.16 /
    ///   _targetSize·worldAnchor·반경·헬멧 캡·경고 임계 ×BodyScale 산정식)
    /// ③WeaponEquipManager.CurrentVisual 노출 계약.
    /// ④[무번호 27] P1 바디 측정 오염 차단 — placeholder 프리미티브 판정(실측 행렬) +
    ///   비활성GO/placeholder 제외 정책 + 정점 샘플 오염 특정 로그(measure-pollute) 소스 계약,
    ///   P3 measure-diag 보강(제외 렌더러 목록 + 오염 원인 출력) 소스 계약.
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class ArmorVisualFitGuardTests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");
        private static string ReadWeapon() => System.IO.File.ReadAllText("Assets/Scripts/Systems/WeaponEquipManager.cs");

        /// <summary>정상 전신 bounds(H=1.8m, min.y=0 지면, max.y=H) — IsPartPlausible 순수 판정 재료.</summary>
        private static Bounds Body(float h = 1.8f)
            => new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(0.6f, h, 0.4f));

        // ===== ① IsPartPlausible 순수 판정 =====

        [Test]
        public void IsPartPlausible_Helmet_PollutedBounds_Rejected()
        {
            // [105차] 실측 오염 part=(1.45, 1.46, 1.21) — 두개골(≈0.62m) + 창 돌출(들어올린 창의 Head 밴드 오염)
            var part = new Bounds(new Vector3(0f, 1.0f, 0f), new Vector3(1.45f, 1.46f, 1.21f));
            Assert.That(ArmorVisualAttachSystem.IsPartPlausible(EquipmentManager.EquipmentSlot.Helmet, part, Body()),
                Is.False, "[105차] 창 오염 bounds(part.size.y=1.46 > 0.55×H=0.99)는 게이트에서 거부되어야 한다.");
        }

        [Test]
        public void IsPartPlausible_Helmet_NormalBounds_Accepted()
        {
            // 정상 두개골 part=(0.5, 0.62, 0.5) — 중심 = 상단 43% 지점
            Bounds full = Body();
            var part = new Bounds(new Vector3(0f, full.max.y - 0.43f * full.size.y, 0f),
                new Vector3(0.5f, 0.62f, 0.5f));
            Assert.That(ArmorVisualAttachSystem.IsPartPlausible(EquipmentManager.EquipmentSlot.Helmet, part, full),
                Is.True, "[105차] 정상 두개골 bounds(size.y ≤ 0.55×H, 중심 ≥ 하단+0.45×H)는 통과해야 한다.");
        }

        [Test]
        public void IsPartPlausible_Armor_Overwide_Rejected()
        {
            // [105차] 실측 오염 torso part x=1.65m(실제 어깨폭 대비 ~3배) → 갑옷 ×3.00 클램프 과대의 원인
            var part = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1.65f, 0.64f, 0.97f));
            Assert.That(ArmorVisualAttachSystem.IsPartPlausible(EquipmentManager.EquipmentSlot.Armor, part, Body()),
                Is.False, "[105차] 창대가 가로지른 torso part(폭 1.65m > 0.75×H=1.35)는 거부되어야 한다.");
        }

        [Test]
        public void IsPartPlausible_Shoes_LowBand_Accepted()
        {
            // 정상 부츠 — 하단 밴드(size.y ≤ 0.25×H, center.y ≤ 하단+0.20×H)
            var part = new Bounds(new Vector3(0f, 0.12f, 0f), new Vector3(0.19f, 0.25f, 0.36f));
            Assert.That(ArmorVisualAttachSystem.IsPartPlausible(EquipmentManager.EquipmentSlot.Shoes, part, Body()),
                Is.True, "[105차] 지면 인접 정상 부츠 bounds(하단 밴드)는 통과해야 한다.");
        }

        // ===== ② 소스 계약 — ArmorVisualAttachSystem =====

        [Test]
        public void Source_MeasureExcludes_WeaponVisualRoot_And_PendingDestroyed()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("WeaponEquipManager.CurrentVisual"),
                "[105차 F1] 측정 excludeRoots에 무기 비주얼 루트 합침이 존재해야 한다.");
            Assert.That(src, Does.Contain("if (WeaponEquipManager.CurrentVisual != null)"),
                "[105차 F1] 무기 루트 null 가드 후 제외 합침(매 부착 재조회 — 멱등).");
            Assert.That(src, Does.Contain("_pendingExcluded"),
                "[105차 F1] 파괴 예정 비주얼 1프레임 유지(_pendingExcluded) 정책이 존재해야 한다.");
            Assert.That(src, Does.Contain("_pendingExcluded.Clear();"),
                "[105차 F1] 측정 1회 소비 후 해제 — 무한 누적 방지.");
        }

        [Test]
        public void Source_NaturalBodyHeight116_And_BodyScale_Formula()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("const float NaturalBodyHeight = 1.16f;"),
                "[105차 F3] 자연 키 원값 1.16m 상수(Heat.fbx 실측).");
            Assert.That(src, Does.Contain("TerritoryBuilder.SoldierTargetHeight / NaturalBodyHeight"),
                "[105차 F3] BodyScale = SoldierTargetHeight(1.8)/1.16 산정식 — 하드코딩 1.56 금지.");
            Assert.That(src, Does.Contain("0.28f * BodyScale"),
                "[105차 F3] footRadius 원값(0.28, 자연키 기준)×BodyScale 산정.");
            Assert.That(src, Does.Contain("0.25f * BodyScale"),
                "[105차 F3] handRadius 원값(0.25, 자연키 기준)×BodyScale 산정.");
            Assert.That(src, Does.Contain("0.6f * BodyScale"),
                "[105차 F3] 헬멧 치수 캡 원값(0.6)×BodyScale 산정.");
            Assert.That(src, Does.Contain("0.35f * BodyScale"),
                "[105차 F3] 본↔중심 경고 임계 원값(0.35)×BodyScale 산정.");
        }

        [Test]
        public void Source_TargetSize_And_WorldAnchor_Uses_BodyScale()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("0.34f * BodyScale"), "[105차 F3] _targetSize Helmet 원값×BodyScale.");
            Assert.That(src, Does.Contain("0.62f * BodyScale"), "[105차 F3] _targetSize Armor.");
            Assert.That(src, Does.Contain("0.36f * BodyScale"), "[105차 F3] _targetSize Shoes.");
            Assert.That(src, Does.Contain("0.24f * BodyScale"), "[105차 F3] _targetSize Gloves.");
            Assert.That(src, Does.Contain("0.85f * BodyScale"), "[105차 F3] _targetSize Back.");
            Assert.That(src, Does.Contain("0.30f * BodyScale"), "[105차 F3] _targetSize Mask.");
            Assert.That(src, Does.Contain("0.45f * BodyScale"), "[105차 F3] _targetSize Bag.");
            Assert.That(src, Does.Contain("Vector3.up * (0.26f * BodyScale)"),
                "[105차 F3] worldAnchor Helmet up 0.26 원값×BodyScale 형태.");
        }

        [Test]
        public void Source_Gate_FallbackLog_And_MeasureDiag_Present()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("부위 측정 게이트 실패"),
                "[105차 F2] 게이트 실패 로그 1회(WarnOnce) 존재.");
            Assert.That(src, Does.Contain("IsPartPlausible(slot, fitPart.worldBounds, mFullBody.worldBounds)"),
                "[105차 F2] FitVisualToBodyPart 호출부에서 부위 게이트 적용.");
            Assert.That(src, Does.Contain("measure-diag:"),
                "[105차 F4] 바디 측정 진단 1회성 키(measure-diag:{scene}) 존재.");
        }

        // ===== ③ 소스 계약 — WeaponEquipManager =====

        [Test]
        public void WeaponEquipManager_Exposes_CurrentVisual()
        {
            string src = ReadWeapon();
            Assert.That(src, Does.Contain("public static GameObject CurrentVisual => _current;"),
                "[105차 F1] 무기 비주얼 루트 읽기 전용 노출 계약.");
        }

        // ===== ④ [무번호 27] P1 — 바디 측정 bounds 오염 차단 =====

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _spawnedObjects = new List<Object>();

        [TearDown]
        public void TearDown_SpawnedFixtures()
        {
            // EditMode 테스트 잔해 정리 — 씬 오염 방지(결정론: 생성 순서 무관 전부 파괴)
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var obj in _spawnedObjects)
                if (obj != null) Object.DestroyImmediate(obj);
            _spawnedObjects.Clear();
        }

        [Test]
        public void IsUnityPrimitivePlaceholder_PrimitiveRenderers_Detected()
        {
            // [무번호 27-P1] CreatePrimitive가 부여한 기본 도형 메시 = placeholder 서명 —
            //   침하 감시용 placeholder 프리미티브(테스트 씬 부착) 측정 제외 근거.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _spawned.Add(cube); _spawned.Add(cyl); _spawned.Add(sphere);

            Assert.That(ArmorVisualAttachSystem.IsUnityPrimitivePlaceholder(cube.GetComponent<MeshRenderer>()), Is.True,
                "[무번호 27-P1] Unity 기본 도형(Cube) 렌더러는 placeholder 프리미티브로 판정되어야 한다.");
            Assert.That(ArmorVisualAttachSystem.IsUnityPrimitivePlaceholder(cyl.GetComponent<MeshRenderer>()), Is.True,
                "[무번호 27-P1] Unity 기본 도형(Cylinder) 렌더러는 placeholder 프리미티브로 판정되어야 한다.");
            Assert.That(ArmorVisualAttachSystem.IsUnityPrimitivePlaceholder(sphere.GetComponent<MeshRenderer>()), Is.True,
                "[무번호 27-P1] Unity 기본 도형(Sphere) 렌더러는 placeholder 프리미티브로 판정되어야 한다.");
        }

        [Test]
        public void IsUnityPrimitivePlaceholder_AvatarSkinnedAndNamedMeshes_Kept()
        {
            // [무번호 27-P1] 아바타 스킨 메시/자체 이름 메시(GLB·FBX)는 placeholder가 아니다 —
            //   오검출 시 몸통 측정이 공중분해되므로 명시적 부정 계약.
            var skinGo = new GameObject("P1_AvatarSkin");
            var smr = skinGo.AddComponent<SkinnedMeshRenderer>();
            var skinMesh = new Mesh { name = "PlayerBody" };
            smr.sharedMesh = skinMesh;
            var meshGo = new GameObject("P1_NamedMesh");
            var mf = meshGo.AddComponent<MeshFilter>();
            var namedMesh = new Mesh { name = "wood_armor_01" };
            mf.sharedMesh = namedMesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            _spawned.Add(skinGo); _spawned.Add(meshGo);
            _spawnedObjects.Add(skinMesh); _spawnedObjects.Add(namedMesh);

            Assert.That(ArmorVisualAttachSystem.IsUnityPrimitivePlaceholder(smr), Is.False,
                "[무번호 27-P1] SkinnedMeshRenderer(아바타 본체)는 placeholder 판정 제외 대상이다.");
            Assert.That(ArmorVisualAttachSystem.IsUnityPrimitivePlaceholder(mr), Is.False,
                "[무번호 27-P1] 자체 이름 메시(GLB/FBX)는 placeholder 판정 제외 대상이다.");
        }

        [Test]
        public void IsUnityPrimitivePlaceholder_NullAndMissingMesh_SafeFalse()
        {
            // [무번호 27-P1] null/MeshFilter 부재 — 측정 루트 중단 없는 안전 폴백(false).
            Assert.That(ArmorVisualAttachSystem.IsUnityPrimitivePlaceholder(null), Is.False,
                "[무번호 27-P1] null 렌더러는 false — NRE로 측정 중단 금지.");
            var empty = new GameObject("P1_NoMesh");
            var mr = empty.AddComponent<MeshRenderer>();
            _spawned.Add(empty);
            Assert.That(ArmorVisualAttachSystem.IsUnityPrimitivePlaceholder(mr), Is.False,
                "[무번호 27-P1] sharedMesh 부재 렌더러는 false — 안전 폴백.");
        }

        [Test]
        public void Source_P1_InactiveGO_And_Placeholder_Exclusion_Policy_Present()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("!r.gameObject.activeInHierarchy"),
                "[무번호 27-P1] 비활성 GameObject 렌더러(아바타 교체 잔재/비실행 대상) 제외 정책이 존재해야 한다 — 하드 계약.");
            Assert.That(src, Does.Contain("IsUnityPrimitivePlaceholder(r)"),
                "[무번호 27-P1] placeholder 프리미티브 판정이 측정 루프에서 호출되어야 한다(침하 감시 placeholder — 제거 아닌 측정 제외).");
            Assert.That(src, Does.Contain("테스트 씬 placeholder 프리미티브"),
                "[무번호 27-P1] fullBody=4.34m 오염 원인 분류 문자열(placeholder 프리미티브)이 제외 정책에 명시되어야 한다.");
        }

        [Test]
        public void Source_P1_VertexSample_Pollution_Attribution_Log_Present()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("measure-pollute:"),
                "[무번호 27-P1] fullBody 과대 오염 실측 특정 로그 키(measure-pollute:{scene})가 존재해야 한다.");
            Assert.That(src, Does.Contain("렌더러별 정점 샘플"),
                "[무번호 27-P1] 오염원 특정용 렌더러별 정점 샘플 로그(이름+y범위)가 존재해야 한다.");
        }

        [Test]
        public void Source_P3_MeasureDiag_Enriched_ExcludedNames_And_PollutionCause()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("measure-diag:"),
                "[무번호 27-P3] 기존 measure-diag:{scene} 키 계약 유지(105차 F4 회귀 금지).");
            Assert.That(src, Does.Contain("제외 목록=["),
                "[무번호 27-P3] measure-diag에 제외 렌더러 이름 목록이 출력되어야 한다.");
            Assert.That(src, Does.Contain("오염 원인="),
                "[무번호 27-P3] measure-diag에 오염 원인 분류(placeholder 등)가 출력되어야 한다.");
        }
    }
}
