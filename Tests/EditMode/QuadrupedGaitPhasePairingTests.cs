using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-09-23] 대각 보행 계약 검증 — QuadrupedProceduralAnimation의 렌더 위상 4필드
    /// (ApplyRotationGait가 SwingLegChain에 직접 소비) 기본 오프셋은 대각 쌍
    /// LF+RH / RF+LH(반 사이클 차이)로 짝지어져야 한다(스킬 규약, Locomotion Trot 튜플과 동일).
    /// EditMode에서는 Awake/UpdateLegPhases가 구동되지 않으므로 필드 초기값이 곧 계약 대상이다.
    /// </summary>
    public class QuadrupedGaitPhasePairingTests
    {
        private GameObject _rig;

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
        }

        /// <summary>원형(0~1) 위상 차이 — [-0.5, 0.5) 부호차. 0=동일 위상, ±0.5=반 사이클 차이.</summary>
        private static float CircDiff(float a, float b) => Mathf.Repeat(a - b + 0.5f, 1f) - 0.5f;

        [Test]
        public void LocomotionSync_GallopOverride_UsesRotaryPhases_AndCanSwitchBackToWalk()
        {
            _rig = new GameObject("QuadrupedOverridePhaseRig");
            var anim = _rig.AddComponent<QuadrupedProceduralAnimation>();
            var locomotion = anim.LocomotionModule; // Awake가 소유/생성한 실제 동기화 모듈 — 중복 AddComponent 금지
            Assert.That(locomotion, Is.Not.Null);
            locomotion.SetGaitOverride(QuadrupedProceduralLocomotion.Gait.Gallop);

            AssertPhase(anim.LF_Phase, 0f, "Gallop LF rotary offset");
            AssertPhase(anim.RF_Phase, 0.25f, "Gallop RF rotary offset");
            AssertPhase(anim.LH_Phase, 0.125f, "Gallop LH rotary offset");
            AssertPhase(anim.RH_Phase, 0.375f, "Gallop RH rotary offset");

            locomotion.SetGaitOverride(QuadrupedProceduralLocomotion.Gait.Walk);
            AssertPhase(anim.LF_Phase, 0f, "Walk LF offset");
            AssertPhase(anim.RF_Phase, 0.5f, "Walk RF offset");
            AssertPhase(anim.LH_Phase, 0.75f, "Walk LH offset");
            AssertPhase(anim.RH_Phase, 0.25f, "Walk RH offset");
        }

        private static void AssertPhase(float actual, float expected, string message)
        {
            Assert.That(Mathf.Abs(CircDiff(actual, expected)), Is.LessThan(0.0001f), message);
        }

        [Test]
        public void DefaultLegPhases_PairDiagonally_LFwithRH_RFwithLH()
        {
            _rig = new GameObject("QuadrupedPhasePairRig");
            var anim = _rig.AddComponent<QuadrupedProceduralAnimation>();

            float lf = Mathf.Repeat(anim.LF_Phase, 1f);
            float rf = Mathf.Repeat(anim.RF_Phase, 1f);
            float lh = Mathf.Repeat(anim.LH_Phase, 1f);
            float rh = Mathf.Repeat(anim.RH_Phase, 1f);

            // 대각 쌍 동일 위상: LF≡RH, RF≡LH
            Assert.That(Mathf.Abs(CircDiff(lf, rh)), Is.LessThan(0.0001f), "LF와 RH는 동일 위상(대각 쌍 A)");
            Assert.That(Mathf.Abs(CircDiff(rf, lh)), Is.LessThan(0.0001f), "RF와 LH는 동일 위상(대각 쌍 B)");

            // 두 대각 쌍은 반 사이클(0.5) 차이
            Assert.That(Mathf.Abs(Mathf.Abs(CircDiff(lf, rf)) - 0.5f), Is.LessThan(0.0001f),
                "대각 쌍 A(=LF/RH)와 B(=RF/LH)는 반 사이클 차이");
        }

        // ──────────────────────────────────────────────
        // [2026-09-25] 날개 동기 플랩 / croc 바인드 스윙 축 — 순수 축 헬퍼 계약.
        // 컴포넌트 내부(LateUpdate 구동 private 경로)는 EditMode에서 실행할 수 없어, 실제
        // 프로덕션 경로(CacheWingAxis / SwingLegChain)가 호출하는 public static 순수 헬퍼를
        // 직접 검증한다. 헬퍼는 상태 없이 입력(bind 방향, 상방/전방/측방 기준)만으로 축을 산출하므로
        // "캐시/복원된 축이 이후 포즈에 의존하지 않는다" 계약의 순수 계산부를 고정한다.
        // ──────────────────────────────────────────────

        /// <summary>좌우 거울 벡터 — 4족 리그 좌우 대칭 가정(X만 부호 반전).</summary>
        private static Vector3 MirrorX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        [Test]
        public void WingFlapAxis_MirroredWingSpans_CommonPhaseMovesBothTipsVerticallyTogether()
        {
            // bind 날개 스팬(피벗→윙팁): 왼쪽 = 옆+약간 아래+약간 앞. 오른쪽은 X 거울.
            Vector3 spanL = new Vector3(-0.9f, -0.25f, 0.15f);
            Vector3 spanR = MirrorX(spanL);

            Vector3 axisL = QuadrupedProceduralAnimation.ComputeWingFlapAxisWorld(spanL.normalized, Vector3.up);
            Vector3 axisR = QuadrupedProceduralAnimation.ComputeWingFlapAxisWorld(spanR.normalized, Vector3.up);

            Assert.That(axisL.magnitude, Is.EqualTo(1f).Within(1e-4f), "좌 날개 축은 정규화돼야 한다");
            Assert.That(axisR.magnitude, Is.EqualTo(1f).Within(1e-4f), "우 날개 축은 정규화돼야 한다");

            // 거울 날개의 축은 '거울(부호 반대)' 관계여야 한다. 기존 구현처럼 양쪽에 같은 부호를
            // 강제하면 공통 sin 위상에서 두 팁이 반대 방향(번갈아)으로 움직였다.
            Assert.That(Vector3.Dot(axisL, axisR), Is.LessThan(0.9f),
                "좌우 플랩 축은 동일 부호가 아니라 거울 관계여야 한다");

            // 공통 위상(같은 양의 각도) — 두 윙팁이 같은 월드 수직 방향으로 함께 움직여야 한다(동기 플랩).
            const float flapDeg = 25f;
            Vector3 tipL = Quaternion.AngleAxis(flapDeg, axisL) * spanL;
            Vector3 tipR = Quaternion.AngleAxis(flapDeg, axisR) * spanR;
            Assert.That(tipL.y, Is.GreaterThan(spanL.y), "양의 위상에서 좌 팁은 위로");
            Assert.That(tipR.y, Is.GreaterThan(spanR.y), "양의 위상에서 우 팁도 같이 위로(교차 금지)");
            Assert.That(tipL.y - spanL.y, Is.EqualTo(tipR.y - spanR.y).Within(1e-3f),
                "거울 스팬은 공통 위상에서 동일한 수직 변위를 가져야 한다");

            // 반대 위상에서도 두 팁이 함께 아래로 — 사인 위상 전 구간 동기.
            Vector3 tipL2 = Quaternion.AngleAxis(-flapDeg, axisL) * spanL;
            Vector3 tipR2 = Quaternion.AngleAxis(-flapDeg, axisR) * spanR;
            Assert.That(tipL2.y, Is.LessThan(spanL.y), "음의 위상에서 좌 팁은 아래로");
            Assert.That(tipR2.y, Is.LessThan(spanR.y), "음의 위상에서 우 팁도 같이 아래로");

            // 플랩은 '세로' 운동 — 수직 변위가 수평 변위보다 커야 한다.
            float horizL = new Vector3(tipL.x - spanL.x, 0f, tipL.z - spanL.z).magnitude;
            Assert.That(tipL.y - spanL.y, Is.GreaterThan(horizL), "팁 변위는 수직 우세");
        }

        [Test]
        public void WingFlapAxis_SpanParallelToUp_IsDegenerate_ReturnsZeroForCallerFallback()
        {
            var axis = QuadrupedProceduralAnimation.ComputeWingFlapAxisWorld(Vector3.up, Vector3.up);
            Assert.That(axis.sqrMagnitude, Is.EqualTo(0f),
                "스팬이 상방과 평행하면 크로스 퇴화 — 0 반환(프로덕션은 루트 forward 폴백 사용)");
        }

        [Test]
        public void WingFlapAxis_HelperIsPure_IgnoresLaterScenePoseChanges()
        {
            // 캐시/복원된 축이 이후 포즈 변화에 끌려가지 않음을 헬퍼 순수성으로 고정:
            // 같은 bind 스팬 입력이면 씬 포즈가 변해도 동일한 축(인스턴스 코드는 이 값을 캐시해 재사용).
            _rig = new GameObject("WingAxisPurityRig");
            Vector3 spanL = new Vector3(-0.9f, -0.25f, 0.15f).normalized;
            Vector3 first = QuadrupedProceduralAnimation.ComputeWingFlapAxisWorld(spanL, Vector3.up);

            _rig.transform.rotation = Quaternion.Euler(37f, 61f, 13f); // 이후 포즈 변화 시뮬레이션
            _rig.transform.position = new Vector3(3f, -2f, 5f);
            Vector3 second = QuadrupedProceduralAnimation.ComputeWingFlapAxisWorld(spanL, Vector3.up);

            Assert.That((second - first).sqrMagnitude, Is.LessThan(1e-10f),
                "축 산출은 입력에만 의존 — 이후 포즈/씬 상태에 무관해야 한다");
        }

        [Test]
        public void BindSwingAxis_BindCapture_ValidPerChain_LateralSign_ForeAftTipSwing()
        {
            // croc 스윙 축: bind 다리 방향에서 1회 산출. 좌/우 체인 각각 유효한 축(체인별 축 유지).
            Vector3 legR = new Vector3(0.3f, -1f, 0.12f).normalized;  // 우측 다리: 아래+밖+앞
            Vector3 legL = new Vector3(-0.3f, -1f, 0.12f).normalized; // 좌측 거울

            Vector3 axisR = QuadrupedProceduralAnimation.ComputeBindSwingAxisWorld(legR, Vector3.forward, Vector3.right);
            Vector3 axisL = QuadrupedProceduralAnimation.ComputeBindSwingAxisWorld(legL, Vector3.forward, Vector3.right);

            Assert.That(axisR.magnitude, Is.EqualTo(1f).Within(1e-4f), "우 축 정규화");
            Assert.That(axisL.magnitude, Is.EqualTo(1f).Within(1e-4f), "좌 축 정규화");
            Assert.That(Mathf.Abs(Vector3.Dot(axisR, legR)), Is.LessThan(1e-4f), "축은 다리 방향과 수직");
            Assert.That(Mathf.Abs(Vector3.Dot(axisR, Vector3.forward)), Is.LessThan(1e-4f), "축은 전방과 수직");
            // 진양성 관례 유지 — 좌우 모두 양의 lateral(transform.right) 쪽(기존 동작 계승).
            Assert.That(Vector3.Dot(axisR, Vector3.right), Is.GreaterThan(0f), "우 축 부호 관례");
            Assert.That(Vector3.Dot(axisL, Vector3.right), Is.GreaterThan(0f), "좌 축 부호 관례");
            // 체인별 축 — 좌우 다리 방향이 다르면 축도 다르다(단일 공유 축 아님).
            Assert.That(Mathf.Abs(Vector3.Dot(axisL, axisR)), Is.LessThan(0.999f), "좌/우 체인 축은 독립");

            // 팁 스윙은 전후 방향(수직 성분 미미) — 힙 회전이 팁을 상하로 흔들거나 비틀지 않는다.
            Vector3 tipSwingR = Vector3.Cross(axisR, legR);
            Assert.That(Mathf.Abs(tipSwingR.y), Is.LessThan(0.1f), "팁 이동 수직 성분 미미");
            Assert.That(Mathf.Abs(tipSwingR.z), Is.GreaterThan(Mathf.Abs(tipSwingR.y) * 5f),
                "팁 이동은 전후 우세(스윙 평면 보정)");
        }

        [Test]
        public void BindSwingAxis_PerFrameRecomputeDrifts_ButCachedBindAxisKeepsSwingPlaneStable()
        {
            // croc 전완/hand 비틀림 회귀 고정: 힙 회전 → 다리 방향 변화 → 축 재계산 feedback으로
            // 축이 자세를 따라 흘렀다(구버전). bind에서 포착한 축을 고정하면 스윙 평면이 보존된다.
            Vector3 bindLeg = new Vector3(0.3f, -1f, 0.12f).normalized;
            Vector3 axisBind = QuadrupedProceduralAnimation.ComputeBindSwingAxisWorld(bindLeg, Vector3.forward, Vector3.right);

            // 구버전 동작 재현: 매 단계 현재 포즈에서 축을 재계산한 뒤 그 축으로 스윙을 적용.
            Vector3 pose = bindLeg;
            float maxDrift = 0f;
            float[] steps = { 30f, -30f, 30f, -30f };
            foreach (float step in steps)
            {
                Vector3 recomputed = QuadrupedProceduralAnimation.ComputeBindSwingAxisWorld(pose, Vector3.forward, Vector3.right);
                maxDrift = Mathf.Max(maxDrift, (recomputed - axisBind).magnitude);
                pose = Quaternion.AngleAxis(step, recomputed) * bindLeg; // 프레임별로 base에서 재구성
            }
            Assert.That(maxDrift, Is.GreaterThan(1e-3f),
                "스윙된 자세에서 재계산한 축은 bind 축과 달라진다(프레임별 재계산 = 드리프트 원인 → 캐시 필요)");

            // 캐시된 bind 축으로 스윙하면 축 방향 성분이 보존된다 — distal 본이 고정 평면 밖으로 비틀리지 않음.
            Vector3 swungFixed = Quaternion.AngleAxis(30f, axisBind) * bindLeg;
            Assert.That(Vector3.Dot(swungFixed, axisBind),
                Is.EqualTo(Vector3.Dot(bindLeg, axisBind)).Within(1e-4f),
                "고정(bind 캐시) 축 회전은 축 성분을 보존 — 전완/hand 비틀림 소멸 계약");
        }
    }
}
