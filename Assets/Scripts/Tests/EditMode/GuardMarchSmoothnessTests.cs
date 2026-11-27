using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [59차] 병사 행진 걸음 끊김 수리 검증 — 3가지 기계적 뿌리의 계약 테스트.
    ///   1) 도착 감속: StepToward가 마지막 0.6m에서 선형 감속 — 오버슈트 없이 도착, 감속 성립, 도착 후 정지 유지.
    ///   2) 즉시 정지 API: HumanoidClipDriver.NotifyStop() — Soldier 스무딩/홀드 상태를 0으로 스냅.
    ///   3) Speed 이중서술 게이트: HumanoidClipDriver.HasActiveDriverOn() — RC가 Speed를 기록하지 않을 조건.
    ///   4) RigAnimationController.Update가 드라이버 보유 오브젝트에서 예외 없이 동작(회귀 가드).
    /// 참고: EditMode Time.deltaTime은 0(저장소 실측 — GuardHoldCooldownTickTests 주석)이므로
    ///   이동 시뮬은 private StepToward(Vector3, Vector3, float, float)를 리플렉션으로 delta = 1/60s를 주어 구동한다.
    ///   도착 판정(ClearCommand)은 delta와 무관한 XZ 반경 분기이므로 공개 ExecuteMovement()로 검증한다.
    /// </summary>
    public class GuardMarchSmoothnessTests
    {
        private const float Delta = 1f / 60f;    // 시뮬 프레임 간격(실제 게임 프레임 가정)
        private const float ClearRadius = 0.25f; // MOVE_CLEAR_RADIUS mirror (production const is private)

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name == "DamageNumber") Object.DestroyImmediate(t.gameObject);
            }
        }

        // ----- helpers -----

        private GameObject Spawn(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        private static Vector3 XZ(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static void InvokeStepToward(GuardPlaceholder guard, Vector3 current, Vector3 target, float distXZ, float delta)
        {
            MethodInfo m = typeof(GuardPlaceholder).GetMethod("StepToward",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(m, Is.Not.Null, "GuardPlaceholder.StepToward(Vector3,Vector3,float,float) must exist.");
            m.Invoke(guard, new object[] { current, target, distXZ, delta });
        }

        [Test]
        public void MarchToCommand_DeceleratesNearTarget_NoOvershoot()
        {
            var guard = Spawn("행진테스트병사", Vector3.zero);
            var placeholder = guard.AddComponent<GuardPlaceholder>();
            placeholder.SetGuardInfo("행진테스트병사", 5, NationType.East);   // _moveSpeed 기본 3.0

            var target = new Vector3(0f, 0f, 3f);   // 3m 이동 — 인자 순서: SetCommandTarget(Vector3 t, bool a)
            placeholder.SetCommandTarget(target, false);   // 이동 명령(공격 아님). rb 없음 → transform 직접 이동.

            // 90회 루프 = 90 × 1/60s = 1.5s 시뮬. 매 회차 XZ 위치 기록.
            var steps = new List<float>();
            for (int i = 0; i < 90; i++)
            {
                Vector3 current = guard.transform.position;
                float distXZ = XZ(target - current).magnitude;
                if (distXZ <= ClearRadius) break;   // 도착 — ExecuteMovement 도착 분기와 동일 판정
                InvokeStepToward(placeholder, current, target, distXZ, Delta);
                float moved = XZ(guard.transform.position - current).magnitude;
                if (moved > 0f) steps.Add(moved);   // 유효 스텝(step > 0)만 기록
            }

            Assert.That(steps.Count, Is.GreaterThanOrEqualTo(10),
                "유효 스텝 부족 — 시뮬이 이동하지 못했다(EditMode 시뮬 구성 오류).");

            // ① 총 이동 ≥ 2.2m — 3m 행진이 1.5s 내에 실제로 성립(도착: dist ≤ 0.25 → ClearCommand 후 정지)
            float travel = 0f;
            foreach (float s in steps) travel += s;
            Assert.That(travel, Is.GreaterThanOrEqualTo(2.2f),
                $"3m 행진이 1.5s(90스텝) 안에 도착해야 한다. travel={travel:0.00}m");

            // ② 종단 시 |pos - target| ≤ 0.35m — 오버슈트 없음
            float finalDist = XZ(target - guard.transform.position).magnitude;
            Assert.That(finalDist, Is.LessThanOrEqualTo(0.35f),
                $"종단 시 목표 ±0.35m 이내여야 한다(오버슈트 금지). finalDist={finalDist:0.00}m");

            // ③ 감속 성립 — 마지막 유효 10스텝 평균 속도 < 첫 10스텝 평균 속도 × 0.7
            float firstAvg = 0f;
            for (int i = 0; i < 10; i++) firstAvg += steps[i];
            firstAvg = firstAvg / 10f / Delta;

            float lastAvg = 0f;
            for (int i = steps.Count - 10; i < steps.Count; i++) lastAvg += steps[i];
            lastAvg = lastAvg / 10f / Delta;

            Assert.That(lastAvg, Is.LessThan(firstAvg * 0.7f),
                $"도착 구간에서 감속해야 한다. first10Avg={firstAvg:0.00} last10Avg={lastAvg:0.00}");

            // ④ 도착 → ClearCommand → 추가 ExecuteMovement 5회 위치 불변(정지 유지)
            //    delta=0(EditMode)이어도 도착 판정은 XZ 반경 분기이므로 성립.
            guard.transform.position = new Vector3(target.x, guard.transform.position.y, target.z - 0.1f);
            placeholder.ExecuteMovement();   // distXZ=0.1 ≤ 0.25 → 도착 분기

            FieldInfo hasCmd = typeof(GuardPlaceholder).GetField("_hasCommand",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(hasCmd, Is.Not.Null, "GuardPlaceholder._hasCommand field must exist.");
            Assert.That((bool)hasCmd.GetValue(placeholder), Is.False, "도착 후 명령이 해제되어야 한다(ClearCommand).");

            Vector3 atArrival = guard.transform.position;
            for (int i = 0; i < 5; i++) placeholder.ExecuteMovement();
            float drift = XZ(guard.transform.position - atArrival).magnitude;
            Assert.That(drift, Is.EqualTo(0f).Within(1e-5f),
                $"도착 후 정지가 유지되어야 한다(추가 ExecuteMovement 5회). drift={drift:0.00}m");
        }

        [Test]
        public void NotifyStop_ZeroesSoldierSmoothing()
        {
            var go = Spawn("NotifyStop_Guard", Vector3.zero);
            var driver = go.AddComponent<HumanoidClipDriver>();
            driver.mode = HumanoidClipDriver.DriveMode.Soldier;   // public 필드 — 리플렉션 불필요
            // Start 미실행(EditMode) → _anim null — NotifyStop은 예외 없이 필드 리셋만 수행해야 한다.

            Assert.DoesNotThrow(() => driver.NotifyStop(),
                "NotifyStop은 _anim null에서도 예외 없이 동작해야 한다(null 가드 계약).");

            FieldInfo smoothed = typeof(HumanoidClipDriver).GetField("_soldierSmoothedSpeed",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo stall = typeof(HumanoidClipDriver).GetField("_soldierStallTime",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo lastTarget = typeof(HumanoidClipDriver).GetField("_soldierLastTarget",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(smoothed, Is.Not.Null, "_soldierSmoothedSpeed field must exist.");
            Assert.That(stall, Is.Not.Null, "_soldierStallTime field must exist.");
            Assert.That(lastTarget, Is.Not.Null, "_soldierLastTarget field must exist.");

            Assert.That((float)smoothed.GetValue(driver), Is.EqualTo(0f),
                "스무딩 속도는 0으로 스냅되어야 한다(제자리걸음 잔상 제거).");
            Assert.That((float)stall.GetValue(driver), Is.EqualTo(0f), "정체 홀드 타이머는 0으로 리셋.");
            Assert.That((float)lastTarget.GetValue(driver), Is.EqualTo(0f), "홀드 타깃 속도는 0으로 리셋.");
        }

        [Test]
        public void HasActiveDriverOn_GatesDoubleWriter()
        {
            var go = Spawn("Gate_Guard", Vector3.zero);
            Assert.That(HumanoidClipDriver.HasActiveDriverOn(go), Is.False,
                "드라이버가 없으면 게이트는 false여야 한다.");

            var driver = go.AddComponent<HumanoidClipDriver>();
            driver.mode = HumanoidClipDriver.DriveMode.Soldier;
            Assert.That(HumanoidClipDriver.HasActiveDriverOn(go), Is.True,
                "활성 Soldier 드라이버 존재 → 게이트 true(RC가 Speed 기록 생략).");

            driver.enabled = false;
            Assert.That(HumanoidClipDriver.HasActiveDriverOn(go), Is.False,
                "비활성 컴포넌트는 게이트 false여야 한다.");

            driver.enabled = true;
            driver.mode = HumanoidClipDriver.DriveMode.Player;
            Assert.That(HumanoidClipDriver.HasActiveDriverOn(go), Is.True,
                "활성 Player 드라이버 존재 → 게이트 true.");
        }

        [Test]
        public void RigAnimationController_SkipsSpeedWhenDriverPresent()
        {
            // 한계: EditMode에는 Animator/runtime controller가 없어 RC.Update의 내부 조기반환 경로만 검증 가능.
            //   본 테스트는 "드라이버 보유 오브젝트에서 Update가 예외 없이 동작"하는 회귀 가드 수준이다.
            //   실제 Speed 이중서술 차단 조건은 HasActiveDriverOn_GatesDoubleWriter가 단위 검증한다.
            var go = Spawn("RCGate_Guard", Vector3.zero);
            go.AddComponent<RigAnimationController>();
            var driver = go.AddComponent<HumanoidClipDriver>();
            driver.mode = HumanoidClipDriver.DriveMode.Soldier;

            MethodInfo update = typeof(RigAnimationController).GetMethod("Update",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(update, Is.Not.Null, "RigAnimationController.Update must exist.");
            var rc = go.GetComponent<RigAnimationController>();

            Assert.DoesNotThrow(() => update.Invoke(rc, null),
                "드라이버 보유 오브젝트에서 RC.Update는 예외 없이 통과해야 한다(게이트 회귀 가드).");
        }
    }
}
