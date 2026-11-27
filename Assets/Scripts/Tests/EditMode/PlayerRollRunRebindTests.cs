using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [132차] 구르기/달리기 키 리바인드 — 소스계약 + 에셋계약 테스트.
    /// 진실 출처: docs/132_ROLL_RUN_REBIND_PLAN.md
    ///  · Q 해방(퀘스트 토글 전용) / Shift+방향 구르기(양엣지·최근 방향 우선) /
    ///    WASD 더블탭 달리기 래치(스태미나 소모 없음) / StartRoll 탑승 가드 / 애니 4방향 분기.
    ///  · 소스계약(계약 1~6): EditMode 규약 — 런타임 인스턴스화 없이 File.ReadAllText 문자열 단정
    ///    (PlayerActionAnimContractTests 129차 소스계약 선례 준용).
    ///  · 에셋계약(계약 7~9): Player_AC.controller를 AssetDatabase로 직접 로드해 검증.
    ///    ※ 주의: 컨트롤러 에셋은 아직 패치 전이다. 상태(RollBack/RollSideL/RollSideR)·트리거 파라미터·
    ///      전이는 별도 에디터 스크립트(RollDirectionsAnimSetup)가 QA 단계에서 주입하며,
    ///      계약 7~9는 그 실행 후에야 GREEN이 확정된다(에셋 패치 후 기준으로 작성됨 — 현재 RED 예상).
    /// </summary>
    public class PlayerRollRunRebindTests
    {
        private const string MovementPath = "Assets/Scripts/Systems/PlayerMovement.cs";
        private const string DriverPath = "Assets/Scripts/Systems/HumanoidClipDriver.cs";
        private const string ControllerPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
        private const string RollBackFbxPath = "Assets/Animations/MeshyUser/Roll_Back.fbx";
        private const string RollSideFbxPath = "Assets/Animations/MeshyUser/Roll_Side.fbx";

        // ───────────────────────── 소스계약 ─────────────────────────

        /// <summary>
        /// [132차] 계약 1 — QUnlock_SourceContract: PlayerMovement에 qKey 구르기 분기 부재.
        /// Q는 퀘스트 토글 전용으로 해방(KeyBindings.Q 무변경) — 소스 어디에도 qKey 입력 분기가 남으면 실패.
        /// </summary>
        [Test]
        public void QUnlock_SourceContract_132()
        {
            var src = System.IO.File.ReadAllText(MovementPath);
            Assert.That(src, Does.Not.Contain("qKey"),
                "[132차] Q 해방 계약 — qKey 구르기 분기 완전 소멸(주석 포함 패러프레이즈).");
        }

        /// <summary>
        /// [132차] 계약 2 — ShiftDirectionRoll_SourceContract: HandleRoll 영역(보조 메서드 포함)에
        /// Shift+방향 조합 배선 존재 — 양엣지(방향키 fresh && Shift 홀드 / Shift fresh && 방향키 홀드)
        /// 및 W/S/A/D 방향키 판정, GetDirectionVector(카메라 상대) 재사용.
        /// </summary>
        [Test]
        public void ShiftDirectionRoll_SourceContract_132()
        {
            var src = System.IO.File.ReadAllText(MovementPath);
            string body = Slice(src, "private void HandleRoll()", "private void StartRoll(");
            Assert.That(body, Is.Not.Empty, "[132차] HandleRoll 영역 슬라이스 실패 — 앵커 확인 필요.");

            // 양엣지 (a): 방향키 wasPressedThisFrame && leftShiftKey.isPressed
            Assert.That(body, Does.Contain("kb.leftShiftKey.isPressed"),
                "[132차] (a)엣지 — Shift 홀드 판정(leftShiftKey.isPressed) 존재.");
            // 양엣지 (b): leftShiftKey wasPressedThisFrame && 방향키 홀드
            Assert.That(body, Does.Contain("kb.leftShiftKey.wasPressedThisFrame"),
                "[132차] (b)엣지 — Shift 상승엣지 판정(leftShiftKey.wasPressedThisFrame) 존재.");
            // 방향키 4종 배선(화살표 대체키 매핑은 기존 WASD 이동과 동일 패턴)
            foreach (var key in new[] { "wKey.wasPressedThisFrame", "sKey.wasPressedThisFrame",
                                        "aKey.wasPressedThisFrame", "dKey.wasPressedThisFrame" })
                Assert.That(body, Does.Contain(key),
                    $"[132차] Shift+방향 구르기 — 방향키 '{key}' 배선 존재.");
            // 방향 벡터 — 기존 GetDirectionVector(카메라 상대) 재사용
            Assert.That(body, Does.Contain("GetDirectionVector"),
                "[132차] 방향 벡터 — GetDirectionVector 재사용 존재.");
        }

        /// <summary>
        /// [132차] 계약 3 — DoubleTapRunLatch_SourceContract: DetectDoubleTap/CheckDoubleTap에
        /// StartRoll 호출 부재(더블탭=구르기 폐지) + 더블탭 달리기 래치 필드 존재.
        /// </summary>
        [Test]
        public void DoubleTapRunLatch_SourceContract_132()
        {
            var src = System.IO.File.ReadAllText(MovementPath);
            string body = Slice(src, "private void DetectDoubleTap()", "private Vector3 GetDirectionVector");
            Assert.That(body, Is.Not.Empty, "[132차] DetectDoubleTap/CheckDoubleTap 영역 슬라이스 실패.");

            Assert.That(body, Does.Not.Contain("StartRoll"),
                "[132차] 더블탭 경로에 StartRoll 호출 부재 — 더블탭은 달리기 래치로 용도 변경.");
            Assert.That(body, Does.Contain("_doubleTapRunLatched"),
                "[132차] 더블탭 달리기 래치 필드(_doubleTapRunLatched) 존재.");

            // 래치 수명 주기 — 감지 시 설정 + 방향키를 떼면 해제(홀드 유지 계약)
            Assert.That(src, Does.Contain("_doubleTapRunLatched[idx] = true"),
                "[132차] 더블탭 감지 → 래치 설정 존재.");
            Assert.That(src, Does.Contain("IsDoubleTapRunActive"),
                "[132차] 래치 활성 여부 프로퍼티(IsDoubleTapRunActive) 존재.");
        }

        /// <summary>
        /// [132차] 계약 4 — StartRollMountGuard_SourceContract: StartRoll에 탑승 가드 존재
        /// (HandleJump 선례 — 탑승 중 Shift는 마운트 질주이므로 탑승자 구르기 오발 봉합).
        /// [206차] 갱신 — 게이트 통일(HorseMountSystem.IsMounted — 레거시 MountSystem 참조 제거).
        /// </summary>
        [Test]
        public void StartRollMountGuard_SourceContract_132()
        {
            var src = System.IO.File.ReadAllText(MovementPath);
            string body = Slice(src, "private void StartRoll(", "private void HandleJump()");
            Assert.That(body, Is.Not.Empty, "[132차] StartRoll 영역 슬라이스 실패.");

            Assert.That(body, Does.Contain("HorseMountSystem.IsMounted"),
                "[132차] StartRoll 탑승 가드 존재 — HandleJump와 동일 선례. [206차] 갱신 — 게이트 통일(HorseMountSystem.IsMounted).");
        }

        /// <summary>
        /// [132차] 계약 5 — RollKindApi_SourceContract: RollKind enum 4값 + LastRollKind 프로퍼티 존재
        /// (기본값 Forward — 기존 호출부 호환).
        /// </summary>
        [Test]
        public void RollKindApi_SourceContract_132()
        {
            var src = System.IO.File.ReadAllText(MovementPath);
            Assert.That(src, Does.Contain("public enum RollKind { Forward, Back, Left, Right }"),
                "[132차] RollKind enum(Forward/Back/Left/Right) 존재.");
            Assert.That(src, Does.Contain("public RollKind LastRollKind { get; private set; }"),
                "[132차] LastRollKind 프로퍼티(public get / private set) 존재.");
            Assert.That(src, Does.Contain("= RollKind.Forward;"),
                "[132차] LastRollKind 기본값 Forward 존재.");
        }

        /// <summary>
        /// [132차] 계약 6 — DriverRollBranch_SourceContract: HumanoidClipDriver가 LastRollKind로
        /// SetTrigger 4종(Roll/RollBack/RollSideL/RollSideR) 분기 + 신규 상태 3종이
        /// IsHigherPriorityHitLightState에 포함(Roll과 동일한 HitLight 면책 계약).
        /// </summary>
        [Test]
        public void DriverRollBranch_SourceContract_132()
        {
            var src = System.IO.File.ReadAllText(DriverPath);

            foreach (var trigger in new[] { "SetTrigger(\"Roll\")", "SetTrigger(\"RollBack\")",
                                            "SetTrigger(\"RollSideL\")", "SetTrigger(\"RollSideR\")" })
                Assert.That(src, Does.Contain(trigger),
                    $"[132차] Driver — LastRollKind 분기 '{trigger}' 존재.");

            string priorityBody = Slice(src, "private static bool IsHigherPriorityHitLightState",
                "private static bool IsHitLightState");
            Assert.That(priorityBody, Is.Not.Empty, "[132차] IsHigherPriorityHitLightState 슬라이스 실패.");
            foreach (var hash in new[] { "RollBackStateShortNameHash", "RollSideLStateShortNameHash",
                                         "RollSideRStateShortNameHash" })
                Assert.That(priorityBody, Does.Contain(hash),
                    $"[132차] IsHigherPriorityHitLightState — '{hash}' 포함(HitLight 면책 계약).");
        }

        // ───────────────────────── 에셋계약 (에셋 패치 후 GREEN) ─────────────────────────

        /// <summary>
        /// [132차] 계약 7 — RollStates_AssetContract: Player_AC에 상태 RollBack/RollSideL/RollSideR +
        /// 트리거 파라미터 3종 존재. ※ 에셋 패치(RollDirectionsAnimSetup) 후 GREEN.
        /// </summary>
        [Test]
        public void RollStates_AssetContract_132()
        {
            var controller = LoadPlayerController();

            foreach (var stateName in new[] { "RollBack", "RollSideL", "RollSideR" })
                Assert.That(FindState(controller, stateName), Is.Not.Null,
                    $"[132차] Player_AC — 상태 '{stateName}' 존재(에셋 패치 후 GREEN).");

            foreach (var trigger in new[] { "RollBack", "RollSideL", "RollSideR" })
            {
                bool hasTrigger = controller.parameters.Any(p =>
                    p.name == trigger && p.type == AnimatorControllerParameterType.Trigger);
                Assert.That(hasTrigger, Is.True,
                    $"[132차] Player_AC — 트리거 파라미터 '{trigger}' 존재(에셋 패치 후 GREEN).");
            }
        }

        /// <summary>
        /// [132차] 계약 8 — RollClips_AssetContract: RollBack 모션=Roll_Back.fbx 클립,
        /// RollSideL/R 모션=Roll_Side.fbx 클립(동일 FBX 공유), RollSideL.mirror=true(RollSideR=false).
        /// ※ 에셋 패치 후 GREEN.
        /// </summary>
        [Test]
        public void RollClips_AssetContract_132()
        {
            var controller = LoadPlayerController();
            var rollBackClips = LoadFbxClips(RollBackFbxPath);
            var rollSideClips = LoadFbxClips(RollSideFbxPath);
            Assert.That(rollBackClips, Is.Not.Empty, $"[132차] {RollBackFbxPath} 클립 로드 실패.");
            Assert.That(rollSideClips, Is.Not.Empty, $"[132차] {RollSideFbxPath} 클립 로드 실패.");

            var rollBack = FindState(controller, "RollBack");
            var rollSideL = FindState(controller, "RollSideL");
            var rollSideR = FindState(controller, "RollSideR");
            Assert.That(rollBack, Is.Not.Null, "[132차] RollBack 상태 부재(에셋 패치 후 GREEN).");
            Assert.That(rollSideL, Is.Not.Null, "[132차] RollSideL 상태 부재(에셋 패치 후 GREEN).");
            Assert.That(rollSideR, Is.Not.Null, "[132차] RollSideR 상태 부재(에셋 패치 후 GREEN).");

            Assert.That(rollBack.motion, Is.Not.Null, "[132차] RollBack 모션 미배선.");
            Assert.That(rollBackClips.Contains(rollBack.motion), Is.True,
                "[132차] RollBack 모션은 Roll_Back.fbx 클립이어야 한다.");

            Assert.That(rollSideL.motion, Is.Not.Null, "[132차] RollSideL 모션 미배선.");
            Assert.That(rollSideR.motion, Is.Not.Null, "[132차] RollSideR 모션 미배선.");
            Assert.That(rollSideClips.Contains(rollSideL.motion), Is.True,
                "[132차] RollSideL 모션은 Roll_Side.fbx 클립이어야 한다.");
            Assert.That(rollSideClips.Contains(rollSideR.motion), Is.True,
                "[132차] RollSideR 모션은 Roll_Side.fbx 클립이어야 한다.");

            Assert.That(rollSideL.mirror, Is.True,
                "[132차] RollSideL.mirror=true — 좌/우 측면 재주 반전 계약.");
            Assert.That(rollSideR.mirror, Is.False,
                "[132차] RollSideR.mirror=false — 좌/우 측면 재주 반전 계약.");
        }

        /// <summary>
        /// [132차] 계약 9 — RollTransitions_AssetContract: AnyState→신규상태 트리거(If) 전이 +
        /// 신규상태→Idle ExitTime 전이 존재(MixamoControllerBuilder AnyState/ExitTo 선례와 동일 형태).
        /// ※ 에셋 패치 후 GREEN.
        /// </summary>
        [Test]
        public void RollTransitions_AssetContract_132()
        {
            var controller = LoadPlayerController();
            var sm = controller.layers[0].stateMachine;

            foreach (var pair in new[] { ("RollBack", "RollBack"), ("RollSideL", "RollSideL"),
                                         ("RollSideR", "RollSideR") })
            {
                bool anyStateEntry = sm.anyStateTransitions.Any(t =>
                    t.destinationState != null && t.destinationState.name == pair.Item1
                    && t.conditions.Any(c => c.mode == AnimatorConditionMode.If
                                             && c.parameter == pair.Item2));
                Assert.That(anyStateEntry, Is.True,
                    $"[132차] AnyState→'{pair.Item1}' 트리거(If) 전이 존재(에셋 패치 후 GREEN).");
            }

            foreach (var stateName in new[] { "RollBack", "RollSideL", "RollSideR" })
            {
                var state = FindState(controller, stateName);
                Assert.That(state, Is.Not.Null, $"[132차] 상태 '{stateName}' 부재.");
                bool exitToIdle = state.transitions.Any(t =>
                    t.destinationState != null && t.destinationState.name == "Idle" && t.hasExitTime);
                Assert.That(exitToIdle, Is.True,
                    $"[132차] '{stateName}'→Idle ExitTime 전이 존재(에셋 패치 후 GREEN).");
            }
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        /// <summary>계약 2/3/4/6 공용: 시작 앵커부터 종료 앵커 직전까지의 소스 슬라이스.</summary>
        private static string Slice(string src, string beginAnchor, string endAnchor)
        {
            int begin = src.IndexOf(beginAnchor, System.StringComparison.Ordinal);
            if (begin < 0) return string.Empty;
            int end = src.IndexOf(endAnchor, begin + beginAnchor.Length, System.StringComparison.Ordinal);
            return end > begin ? src.Substring(begin, end - begin) : src.Substring(begin);
        }

        private static AnimatorController LoadPlayerController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, $"[132차] {ControllerPath} 로드 실패.");
            return controller;
        }

        /// <summary>FBX 내장 클립 전체 로드 — 상태 모션이 해당 FBX 소속인지 소속 판정용(테이크명 무관).</summary>
        private static AnimationClip[] LoadFbxClips(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .ToArray();
        }

        private static AnimatorState FindState(AnimatorController controller, string stateName)
        {
            var sm = controller.layers[0].stateMachine;
            return sm.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == stateName);
        }
    }
}
