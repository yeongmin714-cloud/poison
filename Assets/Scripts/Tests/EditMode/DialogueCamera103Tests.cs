using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [103차 IMPL-B] 대화 카메라 P2 3요구 수리 테스트 — <b>작성만(실행 금지 — QA 서브에이전트 몫)</b>.
    /// 계획서: .hermes/plans/2026-10-19_123000-dialogue-camera-anim-fix-plan.md §3 IMPL-B F-6.
    /// 주입 지점 계약 활용: MainCameraOverride(테스트 카메라) / DialoguePanelOpenOverride(true — 세션 유지).
    /// 종료 후 DialogueCameraDirector.End() 반드시 호출(멱등) + 주입 원복(DialogueFeedbackTests 선례).
    /// 대상: F-1 Foot 앵커 center / F-2 플레이어 렌더러 숨김·복원 / F-4 정면각 / 병사 경로(fullBody=false) 무영향.
    /// 참고: ComputeFullBodyCenter는 internal — InternalsVisibleTo(ProjectName.Tests.EditMode) 경유 직접판정
    /// (NPCDialogueAdapter 어셈블리 선언 선례 — Systems 어셈블리 전역 적용).
    /// </summary>
    public class DialogueCamera103Tests
    {
        // ── 공용 픽스처 — MainCamera 태그 카메라 생성(92차 테스트 기하 선례) ──

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

        private static GameObject CreatePlayer(string name, Vector3 pos, bool withRenderer)
        {
            var go = new GameObject(name);
            go.tag = "Player";
            go.transform.position = pos;
            if (withRenderer) go.AddComponent<MeshRenderer>();
            return go;
        }

        // ── 1. [103차 F-1] Foot 앵커 — 루트 지하 침강(-1.68) + 발 본 월드 y=2.0 → 카메라가 발 기준 프레이밍 ──

        [Test]
        public void FullBody_FootAnchor_CameraHeight3_5_HorizontalDist2_92_103F1()
        {
            var playerGo = CreatePlayer("DC103A_Player", new Vector3(0f, 0f, -5f), withRenderer: false);
            var villagerGo = new GameObject("DC103A_Villager");
            GameObject footGo = null;
            Camera cam = null;
            try
            {
                cam = CreateMainCamera("DC103A_Cam");

                // 주민 루트 — 지하 침강 재현(실측: 루트 y≈-1.7, 발 본 월드 +2.0).
                villagerGo.transform.position = new Vector3(0f, -1.68f, 0f);
                footGo = new GameObject("foot.L");   // 이름 필터 계약 — foot/toe 포함(대소문자 무시)
                footGo.transform.SetParent(villagerGo.transform);
                footGo.transform.localPosition = new Vector3(0f, 3.68f, 0f);   // 월드 y = -1.68 + 3.68 = 2.0

                DialogueCameraDirector.MainCameraOverride = () => cam;
                DialogueCameraDirector.DialoguePanelOpenOverride = () => true;   // [97차 F2] 세션 유지 시뮬레이션
                try
                {
                    // 헬퍼 직접판정 — center = 발 본 y(2.0) + 0.9 = 2.9(수평은 루트 유지).
                    Vector3 center = DialogueCameraDirector.ComputeFullBodyCenter(villagerGo.transform);
                    Assert.That(center.x, Is.EqualTo(0f).Within(0.001f), "[103차 F-1] center 수평 x = 루트 유지.");
                    Assert.That(center.y, Is.EqualTo(2.9f).Within(0.001f),
                        "[103차 F-1] center.y = Foot 본 월드 y(2.0) + FullBodyCenterUpM(0.9) — 루트 지하 침강 무시.");
                    Assert.That(center.z, Is.EqualTo(0f).Within(0.001f), "[103차 F-1] center 수평 z = 루트 유지.");

                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(DialogueCameraDirector.IsActive, Is.True, "Begin → 활성.");

                    // 카메라 높이 = center.y + FullBodyCamHeightUpM(0.6) = 2.9 + 0.6 = 3.5 — [103차 F-3] 단일식
                    // (플레이어 y 가드 폐기 확인 — 플레이어 y=0이어도 3.5 유지).
                    Assert.That(cam.transform.position.y, Is.EqualTo(3.5f).Within(0.01f),
                        "[103차 F-1/F-3] 카메라 pos.y = Foot 앵커 center(2.9) + 0.6 = 3.5(ε 0.01).");

                    // 카메라↔center 수평거리 = dist 산식 (1.8×0.5)/tan(20°)×1.18 ≈ 2.92m(ε 0.05).
                    float dist = (1.8f * 0.5f) / Mathf.Tan(20f * Mathf.Deg2Rad) * 1.18f;
                    Vector3 camOffset = cam.transform.position - center;
                    camOffset.y = 0f;
                    Assert.That(camOffset.magnitude, Is.EqualTo(dist).Within(0.05f),
                        $"[92차 후속 F-A → 103차 F-1] 수평거리 = {dist:F2}m(ε 0.05) — dist 산식 무변경.");

                    // LookAt = center + up×0.15(머리 쪽 조준 — [100차 F-2] 계약 유지).
                    Vector3 lookTarget = center + Vector3.up * 0.15f;
                    Assert.That(Vector3.Dot(cam.transform.forward, (lookTarget - cam.transform.position).normalized),
                        Is.GreaterThan(0.999f), "[100차 F-2] LookAt 방향 = center+up×0.15 조준(dot > 0.999).");
                    Assert.That(cam.fieldOfView, Is.EqualTo(40f), "[92차 결함3] FOV 40 고정 유지.");

                    DialogueCameraDirector.End();
                    Assert.That(cam.transform.position, Is.EqualTo(new Vector3(10f, 20f, -30f)),
                        "[92차 결함3] End 원복 계약 무변경(보조 판정).");
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

        // ── 2. [103차 F-2] 플레이어 렌더러 숨김/복원 — Begin false → End true(재진입 1회 추가 판정) ──

        [Test]
        public void FullBody_PlayerRenderers_HiddenOnBegin_RestoredOnEnd_Reentrant_103F2()
        {
            var playerGo = CreatePlayer("DC103B_Player", new Vector3(0f, 0f, -5f), withRenderer: true);
            var villagerGo = new GameObject("DC103B_Villager");
            GameObject childGo = null;
            GameObject inactiveGo = null;
            Camera cam = null;
            try
            {
                cam = CreateMainCamera("DC103B_Cam");

                // 전수 스냅샷 대상 3종 — 루트(활성) + 자식(활성) + 자식(비활성 GO — includeInactive=true 커버).
                childGo = new GameObject("DC103B_PlayerChild");
                childGo.transform.SetParent(playerGo.transform);
                childGo.AddComponent<MeshRenderer>();
                inactiveGo = new GameObject("DC103B_PlayerInactive");
                inactiveGo.transform.SetParent(playerGo.transform);
                inactiveGo.AddComponent<MeshRenderer>();
                inactiveGo.SetActive(false);

                villagerGo.transform.position = new Vector3(0f, 0f, 0f);

                var rootR = playerGo.GetComponent<MeshRenderer>();
                var childR = childGo.GetComponent<MeshRenderer>();
                var inactiveR = inactiveGo.GetComponent<MeshRenderer>();
                Assert.That(rootR.enabled, Is.True, "사전 조건 — 루트 렌더러 활성.");
                Assert.That(childR.enabled, Is.True, "사전 조건 — 자식 렌더러 활성.");
                Assert.That(inactiveR.enabled, Is.True, "사전 조건 — 비활성 GO 렌더러 enabled=true.");

                DialogueCameraDirector.MainCameraOverride = () => cam;
                DialogueCameraDirector.DialoguePanelOpenOverride = () => true;
                try
                {
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(rootR.enabled, Is.False, "[103차 F-2] Begin(fullBody) → 루트 렌더러 숨김.");
                    Assert.That(childR.enabled, Is.False, "[103차 F-2] Begin(fullBody) → 자식 렌더러 숨김(GetComponentsInChildren 전수).");
                    Assert.That(inactiveR.enabled, Is.False, "[103차 F-2] Begin(fullBody) → 비활성 GO 렌더러도 숨김(includeInactive=true).");

                    DialogueCameraDirector.End();
                    Assert.That(rootR.enabled, Is.True, "[103차 F-2] End → 루트 렌더러 복원.");
                    Assert.That(childR.enabled, Is.True, "[103차 F-2] End → 자식 렌더러 복원.");
                    Assert.That(inactiveR.enabled, Is.True, "[103차 F-2] End → 비활성 GO 렌더러 복원.");

                    // 재진입 1회 추가 — Begin 내 if(_active) End() 경유 재숨김이 스냅샷 오염 없이 동작.
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(rootR.enabled, Is.False, "[103차 F-2] 재진입 Begin → 재숨김(스냅샷 재캡처).");
                    DialogueCameraDirector.End();
                    Assert.That(rootR.enabled, Is.True, "[103차 F-2] 재진입 End → 복원(멱등).");
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
                if (inactiveGo != null) Object.DestroyImmediate(inactiveGo);
                if (childGo != null) Object.DestroyImmediate(childGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(villagerGo);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        // ── 3. [103차 F-4] NPC 정면 보장 — Begin 후 NPC forward vs (플레이어→NPC) 수평각 ≤5° ──

        [Test]
        public void FullBody_NpcFacing_PlayerDirection_Within5Deg_103F4()
        {
            var playerGo = CreatePlayer("DC103C_Player", new Vector3(0f, 0f, 5f), withRenderer: false);
            var villagerGo = new GameObject("DC103C_Villager");
            Camera cam = null;
            try
            {
                cam = CreateMainCamera("DC103C_Cam");

                villagerGo.transform.position = Vector3.zero;
                villagerGo.transform.rotation = Quaternion.identity;   // forward +Z — 스펙 기하
                Assert.That(Vector3.Angle(villagerGo.transform.forward, Vector3.forward), Is.LessThan(0.01f),
                    "사전 조건 — NPC forward = +Z.");

                DialogueCameraDirector.MainCameraOverride = () => cam;
                DialogueCameraDirector.DialoguePanelOpenOverride = () => true;
                try
                {
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);

                    Vector3 toPlayer = playerGo.transform.position - villagerGo.transform.position;
                    toPlayer.y = 0f;
                    Vector3 fwd = villagerGo.transform.forward;
                    fwd.y = 0f;
                    float angle = Vector3.Angle(fwd.normalized, toPlayer.normalized);
                    Assert.That(angle, Is.LessThanOrEqualTo(5f),
                        "[103차 F-4] Begin 후 NPC forward가 플레이어 방향 수평각 ≤5°(③ 룩앳 회전 — [103차 F-2] 정면각 로그 판정 대상).");
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
                Object.DestroyImmediate(villagerGo);
                Object.DestroyImmediate(playerGo);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        // ── 4. 병사 경로(fullBody=false) 무영향 — 렌더러 스냅샷 미동작(enabled 변화 없음) ──

        [Test]
        public void NonFullBody_PlayerRenderers_Unchanged_103Scope()
        {
            var playerGo = CreatePlayer("DC103D_Player", new Vector3(0f, 0f, -5f), withRenderer: true);
            var guardGo = new GameObject("DC103D_Guard");
            Camera cam = null;
            try
            {
                cam = CreateMainCamera("DC103D_Cam");
                guardGo.transform.position = new Vector3(0f, 0f, 0f);

                var playerR = playerGo.GetComponent<MeshRenderer>();
                Assert.That(playerR.enabled, Is.True, "사전 조건 — 플레이어 렌더러 활성.");

                DialogueCameraDirector.MainCameraOverride = () => cam;
                DialogueCameraDirector.DialoguePanelOpenOverride = () => true;
                try
                {
                    DialogueCameraDirector.Begin(guardGo.transform, "병사", false);   // [105차 B] 3인자 명시 — fullBody=false 레거시 옵트인(2인자는 fullBody=true 통일)
                    Assert.That(playerR.enabled, Is.True,
                        "[103차 범위] fullBody=false → 플레이어 렌더러 스냅샷 미동작(Begin 중 enabled 유지).");
                    DialogueCameraDirector.End();
                    Assert.That(playerR.enabled, Is.True,
                        "[103차 범위] fullBody=false End 후에도 enabled 유지(병사/영주 경로 0변경).");
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
                Object.DestroyImmediate(guardGo);
                Object.DestroyImmediate(playerGo);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        // ── 5. [103차 F-1] ComputeFullBodyCenter 폴백 — 발 본 부재 시 기존 계약(root + up×0.9) ──

        [Test]
        public void ComputeFullBodyCenter_Fallback_RootPlus0_9_WithoutFoot_103F1()
        {
            var go = new GameObject("DC103E_NoFoot");
            try
            {
                go.transform.position = new Vector3(3f, -1.68f, 4f);
                Vector3 center = DialogueCameraDirector.ComputeFullBodyCenter(go.transform);
                Assert.That(center, Is.EqualTo(new Vector3(3f, -0.78f, 4f)).Within(0.001f),
                    "[103차 F-1] 발 본 부재 폴백 — target.position + up×0.9(기존 계약 — 수평 유지).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
