using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [207차 P1] 카메라 단일화 계약 — 조준(활)의 단일 진실원을 실렌더(팔로우) 카메라로 고정.
    /// 배경(P0 프로브 실측): 원점 매몰 별개 "Main Camera"가 Camera.main을 탈취해 조준이 원점 고정
    /// ([BowAim] 88건 — BowAimAlignmentTests가 소비한 실측 로그).
    /// 핀 대상은 디스크 실측 시그니처 기준(이름 다르면 구현 쪽에 맞춤 원칙):
    /// PlayerMovement.SyncFollowBindingsToRendered / IsPlausibleAimCamera / AimCameraPlausibleDistance(60m),
    /// PlayerCombat.ResolveBowAimCamera 양분기 가드 + _bowAimCameraGuardWarned 1회 경고.
    /// 소스 계약(HorseSpawnSceneGate195Tests 관행 — src 문자열 잠금 + 순수 함수 직접 호출):
    ///  ① SyncFollowBindingsToRendered 수렴/멱등/null 가드(직접 호출 — 유령 카메라 → 실렌더 카메라)
    ///  ② ApplyFollowCamera 내 배선 소스핀(호출 정확히 1회 + _camera 갱신 + 불변식 유지)
    ///  ③ IsPlausibleAimCamera 가드(원거리 200m false / 근거리 true / 파괴·비활성·비활성화·null false)
    ///  ④ ResolveBowAimCamera 양분기(팔로우 1순위 + Camera.main 폴백) 가드 소스핀 + 1회 경고
    ///  ⑤ [BowAim] 발사 로그 camPos 소스핀(재발 시 즉시 판별용)
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 Unity 배치를 실행하지 않음).
    /// </summary>
    public class HorseArrowCamera207Tests
    {
        private GameObject _camGoA;
        private GameObject _camGoB;
        private GameObject _playerGo;
        private Camera _savedFollowCamera;

        [SetUp]
        public void SetUp()
        {
            // [75차 도메인 위생] 정적 FollowCamera 보존 — TearDown에서 복원(다른 EditMode 케이스 오염 금지)
            _savedFollowCamera = PlayerMovement.FollowCamera;
        }

        [TearDown]
        public void TearDown()
        {
            if (_camGoA != null) Object.DestroyImmediate(_camGoA);
            if (_camGoB != null) Object.DestroyImmediate(_camGoB);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            typeof(PlayerMovement)
                .GetProperty("FollowCamera", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, _savedFollowCamera);
        }

        private static string Src(string relativePath)
        {
            return System.IO.File.ReadAllText(relativePath);
        }

        [Test]
        public void A1_SyncFollowBindingsToRendered_Converges_Idempotent_NullGuard()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            _camGoA = new GameObject("207_GhostCamera");
            _camGoB = new GameObject("207_RenderedCamera");
            Camera ghost = _camGoA.AddComponent<Camera>();
            Camera rendered = _camGoB.AddComponent<Camera>();
            ghost.transform.position = Vector3.zero;
            rendered.transform.position = new Vector3(0f, 1.4f, 30f);

            bool changed = PlayerMovement.SyncFollowBindingsToRendered(rendered, ghost, out Camera synced);
            Assert.That(changed, Is.True,
                "[207차 P1] 유령 바인딩(ghost) 상태에서 실렌더 카메라로 수렴해야 한다(변경 발생=true).");
            Assert.That(synced, Is.SameAs(rendered),
                "[207차 P1] out syncedCamera는 실렌더 카메라 인스턴스여야 한다.");
            Assert.That(PlayerMovement.FollowCamera, Is.SameAs(rendered),
                "[207차 P1] 정적 FollowCamera(조준 단일 진실원)도 실렌더 카메라로 갱신되어야 한다.");

            bool changedAgain = PlayerMovement.SyncFollowBindingsToRendered(rendered, rendered, out Camera syncedAgain);
            Assert.That(changedAgain, Is.False,
                "[207차 P1] 동일 바인딩 재호출은 false(멱등 — 매 프레임 재할당 금지 계약).");
            Assert.That(syncedAgain, Is.SameAs(rendered),
                "[207차 P1] 멱등 재호출 시에도 out 값은 실렌더 카메라 유지.");

            bool changedNull = PlayerMovement.SyncFollowBindingsToRendered(null, rendered, out Camera syncedNull);
            Assert.That(changedNull, Is.False,
                "[207차 P1] renderedCamera==null — 변경 없음(false), 기존 바인딩 유지.");
            Assert.That(syncedNull, Is.SameAs(rendered),
                "[207차 P1] null 입력 시 out은 기존 바인딩(currentBound) 유지.");
            Assert.That(PlayerMovement.FollowCamera, Is.SameAs(rendered),
                "[207차 P1] null 입력이 정적 FollowCamera를 파손(null화)하지 않아야 한다.");
        }

        [Test]
        public void A2_ApplyFollowCamera_Wires_SyncFollowBindings_Once()
        {
            string src = Src("Assets/Scripts/Systems/PlayerMovement.cs");
            const string callLine =
                "SyncFollowBindingsToRendered(_cameraTransform.GetComponent<Camera>(), _camera, out Camera syncedCamera)";
            int count = 0;
            int idx = 0;
            while ((idx = src.IndexOf(callLine, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += 1;
            }
            Assert.That(count, Is.EqualTo(1),
                "[207차 P1] ApplyFollowCamera 내 배선 호출은 정확히 1회 — 중복 배선/누락 금지.");
            Assert.That(src, Does.Contain("_camera = syncedCamera;"),
                "[207차 P1] 배선 결과를 _camera 필드에 반영해야 한다(호출부 갱신 계약).");
            Assert.That(src, Does.Contain("public const float AimCameraPlausibleDistance = 60f;"),
                "[207차 P1] 조준 카메라 거리 상수 정의 핀(60m — 디스크 실측 시그니처, 값 변경은 이 테스트 갱신으로 승인 기록).");
            Assert.That(src, Does.Contain("if (_camera != null && !ReferenceEquals(FollowCamera, _camera))"),
                "[207차 P1] FollowCamera=_camera 불변식 매 프레임 유지 배선(자가 치유 바인딩 3단).");
        }

        [Test]
        public void A3_IsPlausibleAimCamera_Guard_Matrix()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.That(PlayerMovement.AimCameraPlausibleDistance, Is.EqualTo(60f),
                "[207차 P1] 조준 카메라 허용 거리 = 60m(오빗 상한 30m의 2배 — 계약 상수).");

            _playerGo = new GameObject("207_Player");
            Transform player = _playerGo.transform;

            _camGoA = new GameObject("207_NearCamera");
            Camera near = _camGoA.AddComponent<Camera>();
            near.transform.position = new Vector3(0f, 1.4f, 5f);
            Assert.That(PlayerMovement.IsPlausibleAimCamera(near, player), Is.True,
                "[207차 P1] 근거리(5m) 활성 카메라는 유효 — true.");

            _camGoB = new GameObject("207_FarCamera");
            Camera far = _camGoB.AddComponent<Camera>();
            far.transform.position = new Vector3(200f, 0f, 0f);
            Assert.That(PlayerMovement.IsPlausibleAimCamera(far, player), Is.False,
                "[207차 P1] 플레이어에서 200m 원거리 카메라는 상한(60m) 초과 — 잔존/원점 매몰로 판정해 false.");

            Assert.That(PlayerMovement.IsPlausibleAimCamera(null, player), Is.False,
                "[207차 P1] 파괴/부재 카메라(fake-null 포함)는 false — NRE 없이 기각.");
            Assert.That(PlayerMovement.IsPlausibleAimCamera(near, null), Is.False,
                "[207차 P1] player Transform 부재도 false — 판정부 전제(플레이어 위치 필요).");

            near.enabled = false;
            Assert.That(PlayerMovement.IsPlausibleAimCamera(near, player), Is.False,
                "[207차 P1] 컴포넌트 비활성화(enabled=false) 후보는 false — 가드 조건 ②.");
            near.enabled = true;

            _camGoA.SetActive(false);
            Assert.That(PlayerMovement.IsPlausibleAimCamera(near, player), Is.False,
                "[207차 P1] 계층 비활성화(activeInHierarchy=false) 후보는 false — 가드 조건 ③.");
            _camGoA.SetActive(true);

            Assert.That(PlayerMovement.IsPlausibleAimCamera(near, player), Is.True,
                "[207차 P1] 가드 조건 해제 후 재판정 — true(상태 전이 오염 없음 확인).");
        }

        [Test]
        public void A4_ResolveBowAimCamera_BothBranches_Guarded()
        {
            string src = Src("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("PlayerMovement.IsPlausibleAimCamera(followCamera, transform)"),
                "[207차 P1] 1순위 팔로우(실렌더) 카메라 분기에 가드 배선.");
            Assert.That(src, Does.Contain("!PlayerMovement.IsPlausibleAimCamera(currentMainCamera, transform)"),
                "[207차 P1] Camera.main 폴백 분기에 가드 배선(불합격 시 null — 기존 발사 취소 경로 소비).");
            Assert.That(src, Does.Contain("_bowAimCameraGuardWarned"),
                "[207차 P1] 두 후보 모두 불합격 시 경고 1회(스팸 방지) 배선.");
            Assert.That(src, Does.Contain("발사를 취소합니다(이후 재발 미출력)"),
                "[207차 P1] 폴백 메시지 핀 — null 반환(발사 취소) 경로가 경고와 결합됨을 고정.");
        }

        [Test]
        public void A5_BowAimFireLog_Includes_CamPos()
        {
            string src = Src("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("camPos={aimCamera.transform.position}"),
                "[207차 P1] [BowAim] 발사 로그에 camPos(카메라 원점 좌표) 포함 — 재발 시 즉시 판별용.");
        }
    }

    /// <summary>
    /// [207차 P2] 말 클립 루트 회전 보정 계약 — HorseRootPoseLock(캡처→LateUpdate 복원) + SpawnHorse 배선.
    /// 배경(P0 프로브 실측): 애니 적용 포즈A — 루트 본 localEuler=(270,0,0) = 옆으로 누움. 애니 무 포즈B —
    /// Root localEuler=(0,0,0) = 정립. 근원 = 클립이 루트 본 회전을 구동 → 바인드 자세 캡처 후 LateUpdate 복원.
    /// </summary>
    public class HorseRootPoseLock207Tests
    {
        private static BindingFlags All
        {
            get { return BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance; }
        }

        [Test]
        public void H1_SpawnHorse_Wires_RootPoseLock()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/HorseMountSystem.cs");
            int fn = src.IndexOf("void SpawnHorse(BreedSpec", System.StringComparison.Ordinal);
            Assert.That(fn, Is.GreaterThanOrEqualTo(0), "[207차 P2] SpawnHorse 정의가 존재해야 한다.");
            int end = src.IndexOf("static void NormalizeHorseScale", fn, System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(fn), "[207차 P2] SpawnHorse 본문 경계(NormalizeHorseScale) 탐색 가능.");
            string body = src.Substring(fn, end - fn);

            Assert.That(body, Does.Contain("GetComponentsInChildren<Transform>(true)"),
                "[207차 P2] 루트 본 탐색 — 비활성 포함 전체 자식 Transform 대상.");
            Assert.That(body, Does.Contain("name == \"Root\""),
                "[207차 P2] GLB 아마추어 루트 본 이름(\"Root\") 탐색 핀.");
            Assert.That(body, Does.Contain("AddComponent<HorseRootPoseLock>()"),
                "[207차 P2] HorseRootPoseLock 부착 배선.");
            Assert.That(body, Does.Contain("lockComp.RootBone = root;"),
                "[207차 P2] 루트 본 주입 배선.");
            Assert.That(body, Does.Contain("lockComp.CaptureBindPose();"),
                "[207차 P2] 바인드 자세 캡처 호출(컨트롤러 할당 직후 같은 프레임 — 순서 계약).");
            int capturePos = body.IndexOf("lockComp.CaptureBindPose();", System.StringComparison.Ordinal);
            int addPos = body.IndexOf("_horses.Add(new HorseEntity", System.StringComparison.Ordinal);
            Assert.That(capturePos, Is.LessThan(addPos),
                "[207차 P2] 캡처는 엔티티 등록(_horses.Add) 이전 — Animator 첫 평가 전 캡처 순서 계약.");
        }

        [Test]
        public void H2_RootPoseLock_Capture_Then_LateUpdate_Restores()
        {
            System.Type lockType = typeof(HorseMountSystem).GetNestedType(
                "HorseRootPoseLock", BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(lockType, Is.Not.Null, "[207차 P2] HorseRootPoseLock private 중첩 타입 존재.");
            Assert.That(lockType.IsSubclassOf(typeof(MonoBehaviour)), Is.True,
                "[207차 P2] HorseRootPoseLock은 MonoBehaviour — LateUpdate 복원 계약 전제.");

            GameObject bindGo = new GameObject("207_RootBone");
            GameObject lockGo = new GameObject("207_PoseLock");
            try
            {
                bindGo.transform.localRotation = Quaternion.identity;
                Component lockComp = lockGo.AddComponent(lockType);
                Assert.That(lockComp, Is.Not.Null, "[207차 P2] private 중첩 MonoBehaviour 인스턴스화 가능.");

                lockType.GetField("RootBone", All).SetValue(lockComp, bindGo.transform);
                lockType.GetMethod("CaptureBindPose", All).Invoke(lockComp, null);
                Quaternion bind = bindGo.transform.localRotation;

                bindGo.transform.localRotation = Quaternion.Euler(270f, 0f, 0f);
                lockType.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(lockComp, null);

                Assert.That(Quaternion.Angle(bindGo.transform.localRotation, bind), Is.LessThan(0.01f),
                    "[207차 P2] LateUpdate 복원 — 클립이 변형한(270° 눕힘) localRotation이 캡처 바인드 자세로 복원.");
            }
            finally
            {
                Object.DestroyImmediate(lockGo);
                Object.DestroyImmediate(bindGo);
            }
        }

        [Test]
        public void H3_RootPoseLock_Uncaptured_LateUpdate_IsNoOp()
        {
            System.Type lockType = typeof(HorseMountSystem).GetNestedType(
                "HorseRootPoseLock", BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(lockType, Is.Not.Null, "[207차 P2] HorseRootPoseLock private 중첩 타입 존재.");

            GameObject bindGo = new GameObject("207_RootBone_Guard");
            GameObject lockGo = new GameObject("207_PoseLock_Guard");
            try
            {
                Quaternion deformed = Quaternion.Euler(10f, 20f, 30f);
                bindGo.transform.localRotation = deformed;

                Component lockComp = lockGo.AddComponent(lockType);
                lockType.GetField("RootBone", All).SetValue(lockComp, bindGo.transform);
                // CaptureBindPose 미호출 — LateUpdate는 캡처 전 포즈 불변 가드
                lockType.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(lockComp, null);

                Assert.That(Quaternion.Angle(bindGo.transform.localRotation, deformed), Is.LessThan(0.01f),
                    "[207차 P2] 미캡처 상태 LateUpdate는 포즈를 건드리지 않는다(가드 — 캡처 전 오염 금지).");
            }
            finally
            {
                Object.DestroyImmediate(lockGo);
                Object.DestroyImmediate(bindGo);
            }
        }
    }

    /// <summary>
    /// [207차 P4-2] 폭탄 호 플루언트 자원 해제 계약 — BombThrower.OnDestroy(누출 금지).
    /// TelegraphArcIndicator.OnDestroy 선례와 동일한 런타임 자원 해제 패턴 고정.
    /// </summary>
    public class BombThrowerOnDestroy207Tests
    {
        [Test]
        public void B1_OnDestroy_Releases_DashMaterial_And_Texture()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/BombThrower.cs");
            Assert.That(src, Does.Contain("private void OnDestroy()"),
                "[207차 P4-2] OnDestroy 정의 존재 — 런타임 자원 해제 계약.");

            int count = 0;
            int idx = 0;
            while ((idx = src.IndexOf("private void OnDestroy()", idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += 1;
            }
            Assert.That(count, Is.EqualTo(1),
                "[207차 P4-2] OnDestroy 정의는 정확히 1회 — 중복 정의 금지.");

            Assert.That(src, Does.Contain("if (_dashMat != null) Destroy(_dashMat);"),
                "[207차 P4-2] 대시 머티리얼 인스턴스(_dashMat) 해제 — 누출 금지.");
            Assert.That(src, Does.Contain("if (_dashTex != null) Destroy(_dashTex);"),
                "[207차 P4-2] 코드 생성 대시 텍스처(_dashTex) 해제 — 누출 금지.");
        }
    }
}
