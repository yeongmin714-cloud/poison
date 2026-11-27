using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [104차 IMPL-B] NPC 대화 시선 전환(F2) + 루트↔가시 신체 괴리 수리(F3) 테스트.
    /// 계획서: docs/104_CONSTRUCTION_HOLOGRAM_AND_NPC_GAZE_PLAN.md (F2/F3 — IMPL-B).
    ///
    /// F2 근거(실측): 영상 테스트14 36~38s NPC 측면 응시 — Editor.log [103차 F-2] 정면각 "각=0.0° OK"
    /// (루트 forward는 정면)과 모순 → 가시 신체 facing ≠ 루트 yaw(골격 체인 내부 오프셋).
    /// 수리: ComputeFacingYaw 순수 헬퍼(정면각 ≤5° 로그 판정 기준과 동일 기하) + Begin 원본 yaw 기록 →
    /// yaw 전용 적용(AngleAxis — 피치/롤 보존, 멱등) → LateUpdate 매 틱 유지 → End 원본 yaw 복원
    /// + VillagerWanderer.DialogueYawControlled 게이트(대화 중 yaw 제어권 Director 위임 — 멱등).
    ///
    /// F3 근거(실측 — Editor.log [103차 P1] 12건 + [99차 F-D]): 스폰 스냅 Δ≈-0.3(정합) → 첫 Idle 평가 후
    /// 발 본이 루트 기준 +3.5~4.1m 부상(footY=5.62~6.27 vs hitY=2.12) → 배회 스냅이 루트를 지하 -3.5m
    /// 침강 = 루트↔가시 3.7m 괴리. 수리: 골격 루트 바인드 캡처(Init) + LateUpdate 괴리 초과분 상쇄 →
    /// SnapToGroundRay shift ≤ 0.5m 수렴(SnapToGroundRay/P1 로그 구조 무변경 — 재활용 계약).
    ///
    /// 참고: internal 헬퍼(ComputeFacingYaw/ApplyDialogueFacingYaw/ComputeBodyDivergenceExcess)는
    /// InternalsVisibleTo(ProjectName.Tests.EditMode) 경유 직접판정([103차 F-1] ComputeFullBodyCenter 선례).
    /// Begin/End 테스트는 MainCameraOverride/DialoguePanelOpenOverride 주입 + 종료 후 End() 멱등 정리
    /// (DialogueCamera103Tests 선례). Play 모드 실행 금지 — EditMode 안전 계약.
    /// </summary>
    public class DialogueGaze104Tests
    {
        private static string Read(string rel) => System.IO.File.ReadAllText(rel);

        // ── 공용 픽스처 — 103차 테스트 기하 선례 ──

        private static Camera CreateMainCamera(string name)
        {
            var camGo = new GameObject(name);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            camGo.transform.position = new Vector3(10f, 20f, -30f);
            camGo.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
            return cam;
        }

        private static GameObject CreatePlayer(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.tag = "Player";
            go.transform.position = pos;
            return go;
        }

        private static float HorizontalAngle(Transform t, Vector3 focusPos)
        {
            Vector3 toFocus = focusPos - t.position;
            toFocus.y = 0f;
            Vector3 fwd = t.forward;
            fwd.y = 0f;
            return Vector3.Angle(fwd.normalized, toFocus.normalized);
        }

        // ═══════════════ 1. [104차 F2] ComputeFacingYaw 순수 헬퍼 ═══════════════

        /// <summary>
        /// [104차 F2] ComputeFacingYaw — 산출 yaw로 조준 시 forward가 초점 수평각 ≤5°([103차 F-2]
        /// 정면각 로그 판정 기준과 연동 — 실측 로그 "각=0.0° OK"와 동일 판정식).
        /// </summary>
        [Test]
        public void ComputeFacingYaw_PointsAtFocus_Within5Deg_104F2()
        {
            Vector3 from = new Vector3(3f, 2.0f, -4f);   // 수직 성분 무관(y 투영 계약)
            Vector3[] focuses =
            {
                from + new Vector3(0f, 50f, 10f),    // 정면(+Z 쪽)
                from + new Vector3(10f, -3f, 0f),    // 우측(+X)
                from + new Vector3(-7f, 1f, -2f),    // 좌후방
                from + new Vector3(0.3f, 0f, 9f),    // 근소 오프셋
            };
            foreach (var focus in focuses)
            {
                float yaw = DialogueCameraDirector.ComputeFacingYaw(from, focus);
                Assert.That(float.IsNaN(yaw), Is.False, $"[104차 F2] 비퇴화 입력 — yaw 산출({focus}).");
                var probe = new GameObject("DG104_YawProbe");
                try
                {
                    probe.transform.position = from;
                    probe.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    Assert.That(HorizontalAngle(probe.transform, focus), Is.LessThanOrEqualTo(5f),
                        $"[104차 F2] 산출 yaw 조준 시 초점 수평각 ≤5°([103차 F-2] 판정식 연동 — focus {focus}).");
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }
            }
        }

        /// <summary>[104차 F2] 퇴화 입력(동일 위치/수평 영벡터) — NaN 반환(호출처 무기입 폴백 계약).</summary>
        [Test]
        public void ComputeFacingYaw_Degenerate_ReturnsNaN_104F2()
        {
            Vector3 p = new Vector3(1f, 2f, 3f);
            Assert.That(DialogueCameraDirector.ComputeFacingYaw(p, p), Is.NaN,
                "[104차 F2] 동일 위치 — NaN(무기입 폴백).");
            Assert.That(DialogueCameraDirector.ComputeFacingYaw(p, p + Vector3.up * 5f), Is.NaN,
                "[104차 F2] 수직 방향만 존재(수평 영벡터) — NaN.");
        }

        // ═══════════════ 2. [104차 F2] ApplyDialogueFacingYaw — yaw 전용/피치롤 보존/멱등 ═══════════════

        /// <summary>
        /// [104차 F2] yaw 전용 적용 — ①적용 후 forward가 초점 수평각 ≤5° ②기존 기울임 보존(회전축이
        /// world up — up.y 불변 = 새 피치 도입 없음) ③재적용 멱등(회전 불변).
        /// </summary>
        [Test]
        public void ApplyDialogueFacingYaw_YawOnly_PreservesTilt_Idempotent_104F2()
        {
            var go = new GameObject("DG104_Facing");
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.Euler(12f, 40f, 7f);   // 피치/롤 포함 초기 자세
                Vector3 focus = new Vector3(-4f, 0f, 9f);
                float upYBefore = go.transform.up.y;
                Quaternion before = go.transform.rotation;

                DialogueCameraDirector.ApplyDialogueFacingYaw(go.transform, focus);

                Assert.That(HorizontalAngle(go.transform, focus), Is.LessThanOrEqualTo(5f),
                    "[104차 F2] 적용 후 초점 수평각 ≤5°(시선 전환 계약).");
                Assert.That(go.transform.up.y, Is.EqualTo(upYBefore).Within(0.001f),
                    "[104차 F2] yaw 전용 — up.y 불변(회전축 world up — 새 피치/롤 도입 없음).");
                Assert.That(go.transform.rotation, Is.Not.EqualTo(before),
                    "[104차 F2] yaw 변화 실제 적용(40°→초점 방향).");

                Quaternion afterOnce = go.transform.rotation;
                DialogueCameraDirector.ApplyDialogueFacingYaw(go.transform, focus);
                Assert.That(go.transform.rotation, Is.EqualTo(afterOnce),
                    "[104차 F2] 재적용 멱등 — 유효 회전 없으면 무기입.");

                DialogueCameraDirector.ApplyDialogueFacingYaw(go.transform, go.transform.position);   // 퇴화
                Assert.That(go.transform.rotation, Is.EqualTo(afterOnce),
                    "[104차 F2] 퇴화 입력(NaN) — 무기입 폴백.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ═══════════════ 3. [104차 F2] Begin 기록 → 세션 유지 → End 원본 yaw 복원(통합) ═══════════════

        /// <summary>
        /// [104차 F2] fullBody 세션 — ①Begin(fullBody) 후 NPC가 플레이어 수평각 ≤5°로 전환(원본 210°에서)
        /// ②DialogueYawControlled 게이트 ON ③End 후 원본 yaw 복원(±0.5°) + 게이트 OFF. 원본 yaw 기록은
        /// 시선 회전(③ 블록) 이전 — 복원이 회전에 오염되지 않는 계약.
        /// </summary>
        [Test]
        public void FullBody_Begin_FacesPlayer_EndRestoresOriginalYaw_104F2()
        {
            var playerGo = CreatePlayer("DG104A_Player", new Vector3(0f, 0f, 5f));
            var villagerGo = new GameObject("DG104A_Villager");
            GameObject footGo = null;
            Camera cam = null;
            try
            {
                cam = CreateMainCamera("DG104A_Cam");
                villagerGo.transform.position = Vector3.zero;
                villagerGo.transform.rotation = Quaternion.Euler(0f, 210f, 0f);   // 원본 yaw 210°
                footGo = new GameObject("foot.L");   // Foot 앵커 center 경로([103차 F-1]) 필요 — 이름 필터 계약
                footGo.transform.SetParent(villagerGo.transform);
                footGo.transform.localPosition = new Vector3(0f, 0.14f, 0f);

                DialogueCameraDirector.MainCameraOverride = () => cam;
                DialogueCameraDirector.DialoguePanelOpenOverride = () => true;
                try
                {
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(DialogueCameraDirector.IsActive, Is.True, "Begin → 활성.");
                    Assert.That(HorizontalAngle(villagerGo.transform, playerGo.transform.position),
                        Is.LessThanOrEqualTo(5f), "[104차 F2] Begin 후 NPC 시선 = 플레이어 수평각 ≤5°.");
                    Assert.That(VillagerWanderer.DialogueYawControlled, Is.True,
                        "[104차 F2] Begin(fullBody) → yaw 제어권 위임 게이트 ON.");

                    villagerGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // 외부 간섭 시뮬레이션
                    DialogueCameraDirector.End();
                    Assert.That(VillagerWanderer.DialogueYawControlled, Is.False,
                        "[104차 F2] End → 게이트 OFF(모든 종료 경로 수렴 — 멱등).");
                    Assert.That(villagerGo.transform.eulerAngles.y, Is.EqualTo(210f).Within(0.5f),
                        "[104차 F2] End → 원본 yaw(210°) 복원 — 외부 간섭(90°) 무관 기록 기준 원복.");
                }
                finally
                {
                    DialogueCameraDirector.End();   // 멱등 — 잔여 정리
                    DialogueCameraDirector.MainCameraOverride = null;
                    DialogueCameraDirector.DialoguePanelOpenOverride = null;
                }
            }
            finally
            {
                if (footGo != null) Object.DestroyImmediate(footGo);
                Object.DestroyImmediate(villagerGo);
                Object.DestroyImmediate(playerGo);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        /// <summary>
        /// [104차 F2] 병사/영주 경로(fullBody=false) 0변경 — ①시선 회전 = 기존 LookRotation(③ 블록 분기 유지)
        /// ②게이트 미설정(세션 중에도 false — 시선 위임은 fullBody 전용) ③End 원복 유지.
        /// </summary>
        [Test]
        public void NonFullBody_SoldierPath_GateUntouched_RotationRestored_104F2()
        {
            var playerGo = CreatePlayer("DG104B_Player", new Vector3(0f, 0f, 5f));
            var guardGo = new GameObject("DG104B_Guard");
            Camera cam = null;
            try
            {
                cam = CreateMainCamera("DG104B_Cam");
                guardGo.transform.position = Vector3.zero;
                guardGo.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

                DialogueCameraDirector.MainCameraOverride = () => cam;
                DialogueCameraDirector.DialoguePanelOpenOverride = () => true;
                try
                {
                    DialogueCameraDirector.Begin(guardGo.transform, "병사", false);   // [105차 B] 3인자 명시 — fullBody=false 레거시 옵트인(2인자는 fullBody=true 통일)
                    Assert.That(VillagerWanderer.DialogueYawControlled, Is.False,
                        "[104차 F2] fullBody=false → 게이트 미설정(병사/영주 경로 0변경).");
                    Assert.That(HorizontalAngle(guardGo.transform, playerGo.transform.position),
                        Is.LessThanOrEqualTo(5f), "[103차 계약] 병사 경로 ③ 룩앳 회전 유지(기존 동작).");
                    DialogueCameraDirector.End();
                    Assert.That(guardGo.transform.eulerAngles.y, Is.EqualTo(135f).Within(0.5f),
                        "[92차 계약] End 원복(대화 자세 원복 — 기존 계약 무변경).");
                }
                finally
                {
                    DialogueCameraDirector.End();
                    DialogueCameraDirector.MainCameraOverride = null;
                    DialogueCameraDirector.DialoguePanelOpenOverride = null;
                }
            }
            finally
            {
                Object.DestroyImmediate(guardGo);
                Object.DestroyImmediate(playerGo);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        // ═══════════════ 4. [104차 F2/F3] 소스 어서션 — 기록/유지/복원/게이트/보상 구조 ═══════════════

        /// <summary>
        /// [104차 F2] Director 소스 어서션 — ①Begin 원본 yaw 기록(_gazeSavedYaw = _targetSavedRot 기준)
        /// ②③ 블록 fullBody 분기 = ApplyDialogueFacingYaw / else = 기존 LookRotation(병사/영주 무변경)
        /// ③LateUpdate 틱 재적용(ReframeCamera) ④End 원본 yaw 복원 + 상태 클리어 ⑤게이트 set/clear 쌍
        /// ⑥실행순서 계약(DefaultExecutionOrder(10) — Wanderer 접지 보상 이후).
        /// </summary>
        [Test]
        public void Director_Gaze_SourceAssertions_104F2()
        {
            var src = Read("Assets/Scripts/Systems/DialogueCameraDirector.cs");

            Assert.That(src, Does.Contain("_gazeSavedYaw = _targetSavedRot.eulerAngles.y;"),
                "[104차 F2] Begin(fullBody) 원본 yaw 기록 — _targetSavedRot 캡처 기준(③ 회전 이전).");
            Assert.That(src, Does.Contain("_gazeYawCaptured = true;"),
                "[104차 F2] 시선 세션 상태 ON — End 복원 쌍 판정 기준.");
            Assert.That(src, Does.Contain("VillagerWanderer.DialogueYawControlled = true;"),
                "[104차 F2] Begin(fullBody) yaw 제어권 위임 게이트 ON.");
            Assert.That(src, Does.Contain("ApplyDialogueFacingYaw(targetT, playerT.position);"),
                "[104차 F2] ③ 블록 fullBody 분기 = yaw 전용 적용(비fullBody는 기존 LookRotation 분기 — 하단 어서션).");
            Assert.That(src, Does.Contain("targetT.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);"),
                "[103차 계약] ③ 블록 비fullBody = 기존 LookRotation 유지(병사/영주 경로 0변경).");
            Assert.That(src, Does.Contain("ApplyDialogueFacingYaw(targetT, playerTick.position);"),
                "[104차 F2] LateUpdate 매 틱 시선 유지(ReframeCamera — [103차 F-4] 재적용 계약 승계).");
            Assert.That(src, Does.Contain("Mathf.DeltaAngle(restoredT.eulerAngles.y, _gazeSavedYaw)"),
                "[104차 F2] End 원본 yaw 복원(기록값 기준 yaw 전용 최소 보정).");
            Assert.That(src, Does.Contain("_gazeYawCaptured = false;"),
                "[104차 F2] End 시선 세션 상태 클리어(멱등).");
            Assert.That(src, Does.Contain("VillagerWanderer.DialogueYawControlled = false;"),
                "[104차 F2] End 게이트 해제(비세션 End 무영향 — CameraFollowSuppressed 동일 멱등 계약).");
            Assert.That(src, Does.Contain("internal static float ComputeFacingYaw(Vector3 fromPos, Vector3 toPos)"),
                "[104차 F2] 시선 yaw 순수 헬퍼 — EditMode 직접판정용(internal).");
            Assert.That(src, Does.Contain("[DefaultExecutionOrder(10)]"),
                "[104차 F3 연동] LateUpdate 실행순서 — Wanderer 접지 보상 이후(Foot 앵커가 보상 후 자세 조준).");
        }

        /// <summary>
        /// [104차 F2] Wanderer 소스 어서션 — ①게이트 필드(public static — Begin/End 쌍) ②배회 회전 분기 게이트
        /// ③[94차 F4] F키 정지 경로 무간섭(PauseForDialogue — 기존 경로 문자열 유지).
        /// </summary>
        [Test]
        public void Wanderer_YawGate_SourceAssertions_104F2()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");

            Assert.That(src, Does.Contain("public static bool DialogueYawControlled;"),
                "[104차 F2] yaw 제어권 위임 게이트 필드(정적 — 세션 단일소스).");
            Assert.That(src, Does.Contain("if (!DialogueYawControlled)"),
                "[104차 F2] 배회 회전 분기 게이트 — 게이트 중 무기입(Director 단일 제어).");
            Assert.That(src, Does.Contain("public void PauseForDialogue()"),
                "[94차 F4] F키 정지 경로 무변경(PauseForDialogue — 대화 정지 계약 유지).");
            Assert.That(src, Does.Contain("Quaternion.RotateTowards(transform.rotation, want, turnRate * dt);"),
                "[93차 F3] 선회 보간 산식 무변경(게이트는 호출 여부만 가드 — 계약 무손상).");
        }

        /// <summary>
        /// [104차 F3] 괴리 순수 헬퍼 — 실측치 연동 판정: ①[103차 P1] 실측(footY=6.06/rootY=2.019)에서
        /// 초과분 ≈3.55(상쇄 대상) ②정상 보행 들어올림(여유 이내)은 무보상 ③상쇄 후 스냅 shift ≤0.5m
        /// ([104차 F3] 목표 — [103차 P1] shift=-3.9 해소).
        /// </summary>
        [Test]
        public void BodyDivergence_Excess_PureMath_104F3()
        {
            // [103차 P1] 실측 연동 — Villager_1: hitY=2.12 footY=6.06 rootY=2.019(스폰 스냅 후 값)
            const float measuredFootY = 6.06f;
            const float measuredRootY = 2.019f;
            const float hitY = 2.12f;
            const float bindOffset = 0.14f;    // 스폰 스냅 직후 실측(발 본 − 루트 ≈ 0.14m — [99차 F-D] 대당)
            const float allowance = 0.35f;     // VillagerWanderer.BodyLiftAllowanceM 계약

            Assert.That(VillageNpcSpawner.ComputeFootRootOffset(measuredFootY, measuredRootY),
                Is.EqualTo(4.041f).Within(0.001f), "[104차 F3] 실측 발-루트 오프셋 4.041m(괴리 실측 재현).");
            float excess = VillageNpcSpawner.ComputeBodyDivergenceExcess(measuredFootY, measuredRootY, bindOffset, allowance);
            Assert.That(excess, Is.EqualTo(3.551f).Within(0.001f),
                "[104차 F3] 실측 초과분 3.551m — 상쇄 대상(골격루트 구동 과잉).");
            Assert.That(excess, Is.GreaterThan(1.0f), "[104차 F3] 진단 로그 게이트(1.0m) 초과 — [104차 F3] 로그 발화 대상.");

            // 정상 보행 들어올림(바인드+여유 이내) — 무보상(스트라이드 애니 존중)
            float strideLiftFootY = measuredRootY + bindOffset + 0.30f;
            Assert.That(VillageNpcSpawner.ComputeBodyDivergenceExcess(strideLiftFootY, measuredRootY, bindOffset, allowance),
                Is.LessThanOrEqualTo(0f), "[104차 F3] 여유 이내 들어올림 — 초과분 ≤0(무보상 — 보행 애니 무손상).");

            // 상쇄 후 스냅 shift — 발 본이 바인드+여유 상한에 수렴하면 shift ≤ 0.5m([104차 F3] 목표)
            float compensatedFootY = measuredRootY + bindOffset + allowance;
            float postShift = VillageNpcSpawner.ComputeFootRootOffset(hitY, compensatedFootY);
            Assert.That(Mathf.Abs(postShift), Is.LessThanOrEqualTo(0.5f),
                $"[104차 F3] 상쇄 후 스냅 shift {postShift:F3}m — ≤0.5m 목표([103차 P1] shift=-3.9 해소).");
        }

        /// <summary>
        /// [104차 F3] Wanderer 소스 어서션 — ①Init 바인드 캡처 호출 ②LateUpdate 바인드 리셋(위치+회전)
        /// ③초과분 상쇼(순수 헬퍼 경유) ④진단 마커([104차 F3] 골격 괴리 — 애니 근원 확정용) ⑤SnapToGroundRay/
        /// P1 로그 구조 무변경(재활용 계약) ⑥여유 상수 계약.
        /// </summary>
        [Test]
        public void Wanderer_Compensator_SourceAssertions_104F3()
        {
            var src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");
            var spawnerSrc = Read("Assets/Scripts/Systems/VillageNpcSpawner.cs");

            Assert.That(src, Does.Contain("CaptureSkeletonBindPose();"),
                "[104차 F3] Init 바인드 캡처 호출(애니메이터 첫 평가 이전 — 기준선 계약).");
            Assert.That(src, Does.Contain("private void LateUpdate()"),
                "[104차 F3] LateUpdate 보상 — 애니메이터 평가 후 최종 적용권.");
            Assert.That(src, Does.Contain("_skeletonRoot.localPosition = _bindSkeletonLocalPos;"),
                "[104차 F3] 골격 루트 바인드 위치 리셋(클립 비리타겟 커브 무효화 — 부상 상쇄).");
            Assert.That(src, Does.Contain("_skeletonRoot.localRotation = _bindSkeletonLocalRot;"),
                "[104차 F3] 골격 루트 바인드 회전 리셋(요각 오프셋 상쇄 — [104차 F2] 시선 불일치 근원).");
            Assert.That(src, Does.Contain("ComputeBodyDivergenceExcess("),
                "[104차 F3] 초과분 판정 — 순수 헬퍼 단일소스(VillageNpcSpawner).");
            Assert.That(src, Does.Contain("BodyLiftAllowanceM = 0.35f"),
                "[104차 F3] 보행 발 들어올림 여유 0.35m — 정상 보행 애니 무손상 계약.");
            Assert.That(src, Does.Contain("[104차 F3] 골격 괴리"),
                "[104차 F3] 진단 로그 마커 — 애니 근원 확정 데이터(스로틀 계약).");
            Assert.That(src, Does.Contain("VillageNpcSpawner.ComputeFootRootOffset(_footBone.position.y, transform.position.y)"),
                "[104차 F3] 바인드 오프셋/진단 실측 — 발 본−루트 단일 산식.");
            Assert.That(spawnerSrc, Does.Contain("[103차 P1] 접지근원"),
                "[103차 P1] 접지근원 로그 구조 무변경(재활용 계약 — SnapToGroundRay 로직 무변경).");
            Assert.That(spawnerSrc, Does.Contain("internal static float ComputeBodyDivergenceExcess(float footY, float rootY, float bindOffset, float liftAllowance)"),
                "[104차 F3] 괴리 초과분 순수 헬퍼 — EditMode 직접판정용(internal).");
        }

        // ═══════════════ 5. [104차 F3] 바인드 캡처 로직 테스트(실제 계층) ═══════════════

        /// <summary>
        /// [104차 F3] CaptureSkeletonBindPose 로직 — 실제 계층(루트→Root→…→foot.L)에서 ①골격 루트 = 루트 GO
        /// 직계 자식("Root") 탐지(이름 무의존 — 최상위 직계 조상) ②바인드 발 오프셋 = 발y − 루트y ③발 본 부재
        /// 모델 = 보상 스킵(무해). Init는 EditMode 호출 안전(Animator.Play try/catch — [103차] 계약).
        /// </summary>
        [Test]
        public void Wanderer_BindCapture_Hierarchy_Logic_104F3()
        {
            var rootGo = new GameObject("DG104C_Villager");
            GameObject skeletonGo = null, footGo = null;
            try
            {
                rootGo.transform.position = new Vector3(5f, 2.0f, -3f);
                skeletonGo = new GameObject("Root");                 // GLB 리그 골격 루트(직계 자식)
                skeletonGo.transform.SetParent(rootGo.transform, false);
                var spineGo = new GameObject("spine");               // 중간 본(히프 체인)
                spineGo.transform.SetParent(skeletonGo.transform, false);
                footGo = new GameObject("foot.L");                   // 발 본(FindFootBone 계약 — LeftFoot 우선)
                footGo.transform.SetParent(spineGo.transform, false);
                footGo.transform.localPosition = new Vector3(0.1f, 0.14f, 0.05f);   // 월드 y = 2.0 + 0.14 — 루트 위 0.14m(스폰 스냅 직후 실측 계약)

                var wanderer = rootGo.AddComponent<VillagerWanderer>();
                wanderer.Init(new Vector3(5f, 2.0f, -3f), 20f, 12345);   // EditMode 안전(Play try/catch)

                var skeletonRoot = ReadPrivate<Transform>(wanderer, "_skeletonRoot");
                Assert.That(skeletonRoot, Is.Not.Null, "[104차 F3] 골격 루트 탐지 성공.");
                Assert.That(skeletonRoot.name, Is.EqualTo("Root"),
                    "[104차 F3] 골격 루트 = 발 본 최상위 직계 조상(루트 GO 직계 자식 — 이름 무의존).");
                Assert.That(ReadPrivate<float>(wanderer, "_bindFootOffset"),
                    Is.EqualTo(0.14f).Within(0.001f), "[104차 F3] 바인드 발 오프셋 = 발y(2.14) − 루트y(2.0) = +0.14m(스폰 스냅 직후 실측 계약).");
                Assert.That(ReadPrivate<bool>(wanderer, "_bodyCompensatorReady"), Is.True,
                    "[104차 F3] 보상 가동 플래그(캡처 성공).");
            }
            finally
            {
                if (footGo != null) Object.DestroyImmediate(footGo);
                if (skeletonGo != null) Object.DestroyImmediate(skeletonGo);
                Object.DestroyImmediate(rootGo);
            }

            // 발 본 부재 — 보상 스킵(무해: _bodyCompensatorReady false)
            var bareGo = new GameObject("DG104C_NoFoot");
            try
            {
                var wanderer2 = bareGo.AddComponent<VillagerWanderer>();
                wanderer2.Init(Vector3.zero, 20f, 777);
                Assert.That(ReadPrivate<bool>(wanderer2, "_bodyCompensatorReady"), Is.False,
                    "[104차 F3] 발 본 부재 — 보상 스킵(무해 — 기존 경로 유지).");
            }
            finally
            {
                Object.DestroyImmediate(bareGo);
            }
        }

        private static T ReadPrivate<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"리플렉션 대상 필드 존재: {fieldName}");
            return (T)field.GetValue(instance);
        }
    }
}
