using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class PlayerCombatParryInputTests
    {
        private GameObject _player;
        private PlayerCombat _combat;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("ParryInputRegressionPlayer");
            _combat = _player.AddComponent<PlayerCombat>();
            UITransitionState.PointerOverUI = false;
        }

        [TearDown]
        public void TearDown()
        {
            UITransitionState.PointerOverUI = false;
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [Test]
        public void DedicatedParryRequest_OpensOneWindowWithoutQueueingAnAttack()
        {
            // This reflection test exercises the parry-only request/state transition, not physical Keyboard input.
            float startedAt = Time.unscaledTime;

            Assert.That(RequestParry(), Is.True);

            Assert.That(GetField<bool>("_parryActive"), Is.True);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(startedAt + 0.45f).Within(0.001f));
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero);
            Assert.That(GetField<bool>("_queuedBowAction"), Is.False);

            float firstExpiry = GetField<float>("_parryActiveUntil");
            Assert.That(RequestParry(), Is.False, "A repeated parry event must not restart the active window.");
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(firstExpiry));
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero);
            Assert.That(GetField<bool>("_queuedBowAction"), Is.False);
        }

        [Test]
        public void PointerOverUI_BlocksDedicatedParryRequest()
        {
            UITransitionState.PointerOverUI = true;

            Assert.That(RequestParry(), Is.False);
            Assert.That(GetField<bool>("_parryActive"), Is.False);
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero);
            Assert.That(GetField<bool>("_queuedBowAction"), Is.False);
        }

        [Test]
        public void ParryWindowExpiry_EndsDedicatedWindow()
        {
            Assert.That(RequestParry(), Is.True);

            var endWindow = typeof(PlayerCombat).GetMethod("EndParryWindow", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(endWindow, Is.Not.Null);
            endWindow.Invoke(_combat, null);

            Assert.That(GetField<bool>("_parryActive"), Is.False);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(-999f));
        }

        [Test]
        public void TryParry_ConsumesDedicatedWindowOnce()
        {
            Assert.That(RequestParry(), Is.True);

            Assert.That(_combat.TryParry(), Is.True);
            Assert.That(GetField<bool>("_parryActive"), Is.False);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(-999f));
            Assert.That(_combat.TryParry(), Is.False);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(-999f));
        }

        // ===== [95차] 패링 액션감 강화 — F1 퍼펙트 판정 / F2 히스톱 / 반격 보상 / 정리 계약 =====

        [Test] // [95차 F1] EvaluateParryGrade 순수 함수 — 경과 경계(0/0.14/0.16/0.44)에서 PERFECT/GOOD 분류
        public void EvaluateParryGrade_ClassifiesWindowElapsedBoundaries()
        {
            Assert.That(PlayerCombat.EvaluateParryGrade(0f), Is.EqualTo(PlayerCombat.ParryGrade.Perfect),
                "경과 0 — 창 시작 즉시 판정은 PERFECT");
            Assert.That(PlayerCombat.EvaluateParryGrade(0.14f), Is.EqualTo(PlayerCombat.ParryGrade.Perfect),
                "경과 0.14 — PERFECT 경계(0.15) 이내");
            Assert.That(PlayerCombat.EvaluateParryGrade(0.15f), Is.EqualTo(PlayerCombat.ParryGrade.Perfect),
                "경과 0.15 — 경계값 포함 규약(≤ 0.15 → PERFECT)");
            Assert.That(PlayerCombat.EvaluateParryGrade(0.16f), Is.EqualTo(PlayerCombat.ParryGrade.Good),
                "경과 0.16 — 경계 초과는 GOOD");
            Assert.That(PlayerCombat.EvaluateParryGrade(0.44f), Is.EqualTo(PlayerCombat.ParryGrade.Good),
                "경과 0.44 — 창 끝단은 GOOD");
        }

        [Test] // [95차 F1] PERFECT 반격 보상 — 큐 해방(첫 1타 소비) + 배율 1.5 상수 존재
        public void PerfectCounterAttack_ReleasesQueuedAttackWithMultiplier()
        {
            Assert.That(PlayerCombat.ParryCounterMultiplier, Is.EqualTo(1.5f).Within(0.0001f),
                "반격 배율 상수 ParryCounterMultiplier=1.5 존재");

            SetField("_queuedWeaponAttacks", 2);
            float first = _combat.ConsumeQueuedCounterAttack();
            Assert.That(first, Is.EqualTo(1.5f).Within(0.0001f), "첫 1타 해방 — 반격 배율 반환");
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.EqualTo(1), "큐 해방 — 첫 1타 소비");

            float second = _combat.ConsumeQueuedCounterAttack();
            Assert.That(second, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero, "두 번째 1타 해방 후 큐 소진");

            Assert.That(_combat.ConsumeQueuedCounterAttack(), Is.EqualTo(1f),
                "큐가 비면 반격 보상 없음(배율 1)");
        }

        [Test] // [95차 F1] PERFECT 판정 경계 상수 존재 — 순수 함수와 동일 값 공유
        public void PerfectParryWindowConstant_Exists()
        {
            Assert.That(PlayerCombat.ParryPerfectWindow, Is.EqualTo(0.15f).Within(0.0001f),
                "PERFECT 경계 상수 ParryPerfectWindow=0.15 존재");
        }

        [Test] // [95차 F2] 히스톱 파라미터 존재 — 0.05(스케일)/50ms(hold)
        public void ParryHitStopParameters_Exist()
        {
            Assert.That(PlayerCombat.ParryHitStopScale, Is.EqualTo(0.05f).Within(0.0001f),
                "히스톱 로컬 타임스케일 0.05 존재");
            Assert.That(PlayerCombat.ParryHitStopHoldSeconds, Is.EqualTo(0.05f).Within(0.0001f),
                "히스톱 hold 50ms 존재");
        }

        [Test] // [95차 F1/F3] 기존 패링 계약 무손상 — OnDisable 정리(입력큐/반격 배율/펀치 상태) 확장 계약
        public void OnDisable_ClearsParryStateQueuesAndCounterMultiplier()
        {
            Assert.That(RequestParry(), Is.True);
            SetField("_queuedWeaponAttacks", 2);
            SetField("_pendingCounterAttackMultiplier", 1.5f);

            var onDisable = typeof(PlayerCombat).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(onDisable, Is.Not.Null);
            onDisable.Invoke(_combat, null);

            Assert.That(GetField<bool>("_parryActive"), Is.False, "기존 계약 — 창 상태 정리");
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero, "기존 계약 — 입력큐 정리");
            Assert.That(GetField<float>("_pendingCounterAttackMultiplier"), Is.EqualTo(1f),
                "[95차 F1] 반격 배율 리셋 — 정리 계약에 편입");
        }

        // ===== [95차 후속] 믹사모 패링 클립 연결 — 리소스 경로/오버라이드 연결/폴백 가드/연결 로그 소스 어서션 =====

        private static string PlayerCombatSource()
        {
            var path = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Scripts" + System.IO.Path.DirectorySeparatorChar.ToString() + "Systems" +
                System.IO.Path.DirectorySeparatorChar.ToString() + "PlayerCombat.cs");
            return System.IO.File.ReadAllText(path);
        }

        private static string ParryClipConst(string fieldName)
        {
            var field = typeof(PlayerCombat).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, $"Expected PlayerCombat.{fieldName}.");
            return (string)field.GetValue(null);
        }

        [Test] // [95차 후속 ①] 리소스 경로 계약 — Resources/Animations/Combat_Mixamo 폴더 이동+FBX 2종 실측 존재
        public void ParryClipResourceFolder_ContractAndFilesExist()
        {
            Assert.That(ParryClipConst("ParryClipResourceFolder"), Is.EqualTo("Animations/Combat_Mixamo"),
                "[95차 후속] 믹사모 패링 FBX 리소스 폴더 경로(런타임 Resources 로드 계약)");
            Assert.That(ParryClipConst("ParryBlockFileKey"), Is.EqualTo("Sword And Shield Block"),
                "[95차 후속] 방어 FBX 파일명 — 파일 단위 LoadAll 판정 소스");
            Assert.That(ParryClipConst("ParryCounterFileKey"), Is.EqualTo("Sword And Shield Slash"),
                "[95차 후속] 반격 FBX 파일명 — 파일 단위 LoadAll 판정 소스");

            var folder = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources", "Animations", "Combat_Mixamo");
            Assert.That(System.IO.Directory.Exists(folder), Is.True,
                "[95차 후속] Assets/Resources/Animations/Combat_Mixamo 폴더 존재(FBX 2종 이동 실측)");
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(folder, "Sword And Shield Block.fbx")), Is.True,
                "[95차 후속] 방어 FBX(Sword And Shield Block.fbx) 존재");
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(folder, "Sword And Shield Slash.fbx")), Is.True,
                "[95차 후속] 반격 FBX(Sword And Shield Slash.fbx) 존재");
        }

        [Test] // [95차 후속 ②] 오버라이드 연결 — 런타임 API 오버라이드 컨트롤러 생성+Parry 슬롯 스왑+부착
        public void ParryClipWiring_UsesRuntimeOverrideControllerAndAttaches()
        {
            var src = PlayerCombatSource();
            Assert.That(src, Does.Contain("new AnimatorOverrideController(baseCtrl)"),
                "[95차 후속] Player_AC 베이스 오버라이드 컨트롤러 런타임 생성(에셋 무수정)");
            Assert.That(src, Does.Contain("overrideCtrl.ApplyOverrides(pairs)"),
                "[95차 후속] Parry 슬롯 오버라이드 적용");
            Assert.That(src, Does.Contain("runtimeAnimatorController = _parryOverride"),
                "[95차 후속] plain Player_AC 감지 시 오버라이드 컨트롤러 부착");
            Assert.That(ParryClipConst("PlayerAcParrySlotClipName"),
                Is.EqualTo("Armature|Sword_Parry_Backward_1_withSkin"),
                "[95차 후속 실측] Parry 상태 슬롯 원본 클립명(컨트롤러 guid 역조회 — 내부 테이크명)");
        }

        [Test] // [95차 후속 ③] 폴백 가드 — 키워드 매칭 실패 시 파일 단위 이차 판정, 클립 부재 시 기존 동작 유지
        public void ParryClipFallback_GuardsMissingClips()
        {
            var src = PlayerCombatSource();
            Assert.That(src, Does.Contain("FirstParryClipNameContaining(clips, \"block\")"),
                "[95차 후속] 폴더 레벨 클립명 키워드 매칭(방어)");
            Assert.That(src, Does.Contain("FirstParryClipNameContaining(clips, \"slash\")"),
                "[95차 후속] 폴더 레벨 클립명 키워드 매칭(반격)");
            Assert.That(src, Does.Contain("ParryClipResourceFolder + \"/\" + ParryBlockFileKey"),
                "[95차 후속] 믹사모 테이크명(mixamo.com) 대응 — 방어 FBX 파일 단위 LoadAll 이차 판정");
            Assert.That(src, Does.Contain("ParryClipResourceFolder + \"/\" + ParryCounterFileKey"),
                "[95차 후속] 믹사모 테이크명(mixamo.com) 대응 — 반격 FBX 파일 단위 LoadAll 이차 판정");
            Assert.That(src, Does.Contain("_parryBlockClip == null && _parryCounterClip == null"),
                "[95차 후속] 클립 부재 시 오버라이드 미사용 가드(기존 Parry 클립 유지)");
            Assert.That(src, Does.Contain("current.name != PlayerAcControllerName"),
                "[95차 후속] plain Player_AC만 감지 — 부착 경로 없는 씬은 와이어 생략(폴백)");
        }

        [Test] // [95차 후속 ④] 연결 로그 1회 — Play 판별 마커(정적 캐시 세션당 1회)
        public void ParryClipWiring_LogsConnectedClipNamesOnce()
        {
            var src = PlayerCombatSource();
            Assert.That(src, Does.Contain("패링 클립 연결 — block="),
                "[95차 후속] 연결 로그 포맷(클립 매칭 실측명 판별용)");
            Assert.That(src, Does.Contain("counter="),
                "[95차 후속] 연결 로그에 반격 클립명 포함");
        }

        private bool RequestParry()
        {
            var method = typeof(PlayerCombat).GetMethod("TryStartParry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Dedicated parry input should have a separate parry-only handler.");
            return (bool)method.Invoke(_combat, null);
        }

        private T GetField<T>(string name)
        {
            var field = typeof(PlayerCombat).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Expected PlayerCombat.{name}.");
            return (T)field.GetValue(_combat);
        }

        private void SetField(string name, object value)
        {
            var field = typeof(PlayerCombat).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Expected PlayerCombat.{name}.");
            field.SetValue(_combat, value);
        }
    }
}
