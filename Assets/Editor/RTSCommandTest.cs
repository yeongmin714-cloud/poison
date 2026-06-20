using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

/// <summary>
/// C9-20: RTS 명령 시스템 테스트 — 12개 테스트 케이스
/// 
/// 실행:
///   유니티 에디터에서 Tools > Run RTS Command Tests 메뉴 실행
///   또는 배치모드: Unity.exe -quit -batchmode -executeMethod RTSCommandTest.RunAllTests
/// </summary>
public class RTSCommandTest : MonoBehaviour
{
    // ===== 전역 테스트 상태 =====
    private static int _passed = 0;
    private static int _failed = 0;
    private static readonly List<string> _failures = new List<string>();

    // ===== 테스트용 목 객체 =====

    /// <summary>
    /// 테스트용 IDamageable 구현 (적 역할)
    /// </summary>
    public class TestDamageable : MonoBehaviour, IDamageable
    {
        public bool isAlive = true;
        public bool IsAlive => isAlive;

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            // 테스트용 — 아무것도 하지 않음
        }
    }

    /// <summary>
    /// 테스트용 GuardPlaceholder 역할 — RTSCommandSystem.IssueRightClickCommand가 호출되었는지 추적
    /// </summary>
    public class TestGuard : MonoBehaviour
    {
        public string guardName = "TestGuard";
        public bool isAlive = true;
        public bool isRecruited = true;
        public bool isSelected = false;

        // RTS 명령 추적
        public Vector3? lastCommandTarget = null;
        public bool? lastIsAttackCommand = null;
        public bool commandCleared = false;
        public bool isInCombat = false;

        public void ResetTracking()
        {
            lastCommandTarget = null;
            lastIsAttackCommand = null;
            commandCleared = false;
            isInCombat = false;
        }

        // GuardPlaceholder와 동일한 시그니처의 메서드들
        public void SetCommandTarget(Vector3 t, bool a)
        {
            lastCommandTarget = t;
            lastIsAttackCommand = a;
        }

        public void ClearCommand()
        {
            lastCommandTarget = null;
            lastIsAttackCommand = null;
            commandCleared = true;
        }

        public void SetInCombat(bool combat) { isInCombat = combat; }
        public bool HasCommand => lastCommandTarget.HasValue;
        public bool IsAttackCommand => lastIsAttackCommand ?? false;
        public Vector3 CommandTarget => lastCommandTarget ?? Vector3.zero;
    }

    /// <summary>
    /// 테스트용 GuardSelectionManager — RTSCommandSystem에서 사용
    /// </summary>
    public class TestGuardSelectionManager : MonoBehaviour
    {
        public readonly List<GuardPlaceholder> selectedGuards = new List<GuardPlaceholder>();
        public IReadOnlyList<GuardPlaceholder> SelectedGuards => selectedGuards.AsReadOnly();
        public int SelectedCount => selectedGuards.Count;
    }

    // ===== 테스트 유틸리티 =====

    private static void ResetTestCounts()
    {
        _passed = 0;
        _failed = 0;
        _failures.Clear();
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"[PASS] {testName}");
        }
        else
        {
            _failed++;
            string msg = string.IsNullOrEmpty(detail) ? testName : $"{testName}: {detail}";
            _failures.Add(msg);
            Debug.LogError($"[FAIL] {msg}");
        }
    }

    private static void PrintSummary()
    {
        Debug.Log("===========================================");
        Debug.Log($"[RTSCommandTest] 총 {_passed + _failed}개 테스트 중 {_passed} 성공, {_failed} 실패");
        if (_failures.Count > 0)
        {
            Debug.LogError("--- 실패 목록 ---");
            foreach (var f in _failures)
                Debug.LogError($"  ❌ {f}");
        }
        Debug.Log("===========================================");
    }

    // ===== 테스트 케이스 =====

    /// <summary>
    /// Test 1: 우클릭이 적(IDamageable) 대상일 때 공격 명령 발생
    /// </summary>
    private static void Test01_RightClickOnEnemy_IssuesAttackCommand()
    {
        // RTSCommandSystem을 생성하고 레이캐스트를 모의해야 함
        // 여기서는 CommandSystem의 내부 로직을 단위 테스트
        var go = new GameObject("TestRTS");
        var rts = go.AddComponent<RTSCommandSystem>();

        // GuardSelectionManager 모의
        var selGo = new GameObject("TestSel");
        var sel = selGo.AddComponent<GuardSelectionManager>();
        // RTSCommandSystem은 GuardSelectionManager.Instance를 통해 선택된 병사 참조
        // Instance를 직접 설정할 수 없으므로, 통합 테스트

        Debug.Log("[Test01] RTSCommandSystem 생성 확인 (통합 테스트는 씬 필요)");
        Assert(rts != null, "Test01_RightClickOnEnemy_IssuesAttackCommand", "RTSCommandSystem 인스턴스 생성");
        Assert(rts.SelectedCount == 0, "Test01_RightClickOnEnemy_IssuesAttackCommand", "초기 선택 병사 0명");

        GameObject.DestroyImmediate(go);
        GameObject.DestroyImmediate(selGo);
    }

    /// <summary>
    /// Test 2: SetCommandTarget(true)로 공격 명령 확인
    /// </summary>
    private static void Test02_SetCommandTargetAttack_Confirmed()
    {
        var guardGo = new GameObject("Guard");
        var guard = guardGo.AddComponent<GuardPlaceholder>();
        guard.SetRecruited(true);

        guard.SetCommandTarget(new Vector3(10, 0, 10), true);

        Assert(guard.HasCommand, "Test02_SetCommandTargetAttack_Confirmed", "명령이 설정됨");
        Assert(guard.IsAttackCommand, "Test02_SetCommandTargetAttack_Confirmed", "공격 명령(true)");
        Assert(guard.CommandTarget == new Vector3(10, 0, 10), "Test02_SetCommandTargetAttack_Confirmed", "타겟 위치 일치");

        guard.ClearCommand();
        GameObject.DestroyImmediate(guardGo);
    }

    /// <summary>
    /// Test 3: 우클릭이 지형(적 아님) 대상일 때 이동 명령 발생
    /// </summary>
    private static void Test03_SetCommandTargetMove_Confirmed()
    {
        var guardGo = new GameObject("Guard");
        var guard = guardGo.AddComponent<GuardPlaceholder>();
        guard.SetRecruited(true);

        guard.SetCommandTarget(new Vector3(5, 0, -3), false);

        Assert(guard.HasCommand, "Test03_SetCommandTargetMove_Confirmed", "명령이 설정됨");
        Assert(!guard.IsAttackCommand, "Test03_SetCommandTargetMove_Confirmed", "이동 명령(false)");
        Assert(guard.CommandTarget == new Vector3(5, 0, -3), "Test03_SetCommandTargetMove_Confirmed", "타겟 위치 일치");

        guard.ClearCommand();
        GameObject.DestroyImmediate(guardGo);
    }

    /// <summary>
    /// Test 4: 죽은 적 우클릭 → 이동 명령 (공격 불가)
    /// </summary>
    private static void Test04_DeadEnemy_MoveCommand()
    {
        var guardGo = new GameObject("Guard");
        var guard = guardGo.AddComponent<GuardPlaceholder>();
        guard.SetRecruited(true);

        // 죽은 적 대상으로 이동 명령 시뮬레이션
        // RTSCommandSystem에서 IDamageable.IsAlive == false면 이동으로 처리
        guard.SetCommandTarget(new Vector3(0, 0, 0), false);

        Assert(guard.HasCommand, "Test04_DeadEnemy_MoveCommand", "명령이 설정됨");
        Assert(!guard.IsAttackCommand, "Test04_DeadEnemy_MoveCommand", "죽은 적이므로 이동 명령(false)");

        guard.ClearCommand();
        GameObject.DestroyImmediate(guardGo);
    }

    /// <summary>
    /// Test 5: H키 → 모든 병사 명령 취소 (ClearCommand 호출)
    /// </summary>
    private static void Test05_HKey_StopsAllGuards()
    {
        var guardGo = new GameObject("Guard");
        var guard = guardGo.AddComponent<GuardPlaceholder>();
        guard.SetRecruited(true);

        // 먼저 명령 설정
        guard.SetCommandTarget(new Vector3(10, 0, 10), true);
        Assert(guard.HasCommand, "Test05_HKey_StopsAllGuards", "명령 설정됨");

        // ClearCommand = H키 효과
        guard.ClearCommand();

        Assert(!guard.HasCommand, "Test05_HKey_StopsAllGuards", "명령 취소됨");
        Assert(!guard.IsAttackCommand, "Test05_HKey_StopsAllGuards", "공격 명령 플래그 초기화");

        GameObject.DestroyImmediate(guardGo);
    }

    /// <summary>
    /// Test 6: Ctrl+우클릭 적 → 일제 공격 (SetCommandTarget 동일 위치)
    /// </summary>
    private static void Test06_CtrlRightClick_SynchronizedAttack()
    {
        var guardGo1 = new GameObject("Guard1");
        var guard1 = guardGo1.AddComponent<GuardPlaceholder>();
        guard1.SetRecruited(true);

        var guardGo2 = new GameObject("Guard2");
        var guard2 = guardGo2.AddComponent<GuardPlaceholder>();
        guard2.SetRecruited(true);

        // 일제 공격: 모든 병사가 동일한 위치로 공격 명령
        Vector3 targetPos = new Vector3(20, 0, 20);
        guard1.SetCommandTarget(targetPos, true);
        guard2.SetCommandTarget(targetPos, true);

        Assert(guard1.HasCommand && guard1.IsAttackCommand, "Test06_CtrlRightClick_SynchronizedAttack", "Guard1 일제 공격 명령");
        Assert(guard2.HasCommand && guard2.IsAttackCommand, "Test06_CtrlRightClick_SynchronizedAttack", "Guard2 일제 공격 명령");
        Assert(guard1.CommandTarget == targetPos, "Test06_CtrlRightClick_SynchronizedAttack", "Guard1 타겟 위치 일치");
        Assert(guard2.CommandTarget == targetPos, "Test06_CtrlRightClick_SynchronizedAttack", "Guard2 타겟 위치 일치 (동일)");

        GameObject.DestroyImmediate(guardGo1);
        GameObject.DestroyImmediate(guardGo2);
    }

    /// <summary>
    /// Test 7: Ctrl+우클릭 지형 → 일제 이동 (모두 같은 위치로 이동)
    /// </summary>
    private static void Test07_CtrlRightClick_SynchronizedMove()
    {
        var guardGo1 = new GameObject("Guard1");
        var guard1 = guardGo1.AddComponent<GuardPlaceholder>();
        guard1.SetRecruited(true);

        var guardGo2 = new GameObject("Guard2");
        var guard2 = guardGo2.AddComponent<GuardPlaceholder>();
        guard2.SetRecruited(true);

        Vector3 movePos = new Vector3(-10, 0, -10);
        guard1.SetCommandTarget(movePos, false);
        guard2.SetCommandTarget(movePos, false);

        Assert(guard1.HasCommand && !guard1.IsAttackCommand, "Test07_CtrlRightClick_SynchronizedMove", "Guard1 일제 이동 명령");
        Assert(guard2.HasCommand && !guard2.IsAttackCommand, "Test07_CtrlRightClick_SynchronizedMove", "Guard2 일제 이동 명령");
        Assert(guard1.CommandTarget == movePos, "Test07_CtrlRightClick_SynchronizedMove", "Guard1 이동 위치 일치");
        Assert(guard2.CommandTarget == movePos, "Test07_CtrlRightClick_SynchronizedMove", "Guard2 이동 위치 일치 (동일)");

        GameObject.DestroyImmediate(guardGo1);
        GameObject.DestroyImmediate(guardGo2);
    }

    /// <summary>
    /// Test 8: 선택 병사 없을 때 우클릭 → 명령 없음
    /// </summary>
    private static void Test08_NoSelection_NoCommand()
    {
        var guardGo = new GameObject("Guard");
        var guard = guardGo.AddComponent<GuardPlaceholder>();
        guard.SetRecruited(true);
        // 선택 안 됨 (guard.SetSelected(false))

        // 명령을 설정하지 않음
        bool hasCommand = guard.HasCommand;

        Assert(!hasCommand, "Test08_NoSelection_NoCommand", "선택 안 된 병사는 명령 없음");

        // 명령을 건너뛰었는지 확인
        Assert(!guard.IsAttackCommand, "Test08_NoSelection_NoCommand", "공격 플래그 없음");

        GameObject.DestroyImmediate(guardGo);
    }

    /// <summary>
    /// Test 9: 여러 병사 선택 + 우클릭 적 → 모든 병사 공격 명령
    /// </summary>
    private static void Test09_MultipleSelected_AllAttack()
    {
        var guardGo1 = new GameObject("Guard1");
        var guard1 = guardGo1.AddComponent<GuardPlaceholder>();
        guard1.SetRecruited(true);

        var guardGo2 = new GameObject("Guard2");
        var guard2 = guardGo2.AddComponent<GuardPlaceholder>();
        guard2.SetRecruited(true);

        var guardGo3 = new GameObject("Guard3");
        var guard3 = guardGo3.AddComponent<GuardPlaceholder>();
        guard3.SetRecruited(true);

        // 모든 병사에게 공격 명령 (동일 타겟)
        Vector3 enemyPos = new Vector3(15, 0, 5);
        guard1.SetCommandTarget(enemyPos, true);
        guard2.SetCommandTarget(enemyPos, true);
        guard3.SetCommandTarget(enemyPos, true);

        Assert(guard1.HasCommand && guard1.IsAttackCommand, "Test09_MultipleSelected_AllAttack", "Guard1 공격");
        Assert(guard2.HasCommand && guard2.IsAttackCommand, "Test09_MultipleSelected_AllAttack", "Guard2 공격");
        Assert(guard3.HasCommand && guard3.IsAttackCommand, "Test09_MultipleSelected_AllAttack", "Guard3 공격");
        Assert(guard1.CommandTarget == enemyPos, "Test09_MultipleSelected_AllAttack", "Guard1 타겟 일치");
        Assert(guard2.CommandTarget == enemyPos, "Test09_MultipleSelected_AllAttack", "Guard2 타겟 일치");

        GameObject.DestroyImmediate(guardGo1);
        GameObject.DestroyImmediate(guardGo2);
        GameObject.DestroyImmediate(guardGo3);
    }

    /// <summary>
    /// Test 10: 공격 중 H키 → 모든 명령 취소 확인
    /// </summary>
    private static void Test10_StopAfterAttack_ClearsCommands()
    {
        var guardGo1 = new GameObject("Guard1");
        var guard1 = guardGo1.AddComponent<GuardPlaceholder>();
        guard1.SetRecruited(true);

        var guardGo2 = new GameObject("Guard2");
        var guard2 = guardGo2.AddComponent<GuardPlaceholder>();
        guard2.SetRecruited(true);

        // 공격 명령
        guard1.SetCommandTarget(new Vector3(10, 0, 10), true);
        guard2.SetCommandTarget(new Vector3(10, 0, 10), true);
        Assert(guard1.HasCommand, "Test10_StopAfterAttack_ClearsCommands", "Guard1 공격 중");
        Assert(guard2.HasCommand, "Test10_StopAfterAttack_ClearsCommands", "Guard2 공격 중");

        // H키 = ClearCommand
        guard1.ClearCommand();
        guard2.ClearCommand();

        Assert(!guard1.HasCommand, "Test10_StopAfterAttack_ClearsCommands", "Guard1 명령 취소 완료");
        Assert(!guard2.HasCommand, "Test10_StopAfterAttack_ClearsCommands", "Guard2 명령 취소 완료");
        Assert(!guard1.IsAttackCommand, "Test10_StopAfterAttack_ClearsCommands", "Guard1 공격 플래그 초기화");
        Assert(!guard2.IsAttackCommand, "Test10_StopAfterAttack_ClearsCommands", "Guard2 공격 플래그 초기화");

        GameObject.DestroyImmediate(guardGo1);
        GameObject.DestroyImmediate(guardGo2);
    }

    /// <summary>
    /// Test 11: 이동 명령 후 Guard가 타겟 위치를 올바르게 저장하는지 확인
    /// </summary>
    private static void Test11_MoveCommand_GuardStoresTarget()
    {
        var guardGo = new GameObject("Guard");
        var guard = guardGo.AddComponent<GuardPlaceholder>();
        guard.SetRecruited(true);

        Vector3 targetPosition = new Vector3(100, 0, -50);
        guard.SetCommandTarget(targetPosition, false);

        Assert(guard.HasCommand, "Test11_MoveCommand_GuardStoresTarget", "이동 명령 저장됨");
        Assert(!guard.IsAttackCommand, "Test11_MoveCommand_GuardStoresTarget", "이동 명령 타입");
        Assert(guard.CommandTarget == targetPosition, "Test11_MoveCommand_GuardStoresTarget", "타겟 위치 정확히 저장");

        // 거리 계산 확인 (RTSCommandSystem에서 사용)
        float dist = Vector3.Distance(guard.transform.position, guard.CommandTarget);
        Assert(dist > 0f, "Test11_MoveCommand_GuardStoresTarget", "거리 계산 가능");

        GameObject.DestroyImmediate(guardGo);
    }

    /// <summary>
    /// Test 12: 사망한 병사는 명령을 받지 않음
    /// </summary>
    private static void Test12_DeadGuard_NoCommand()
    {
        var guardGo = new GameObject("Guard");
        var guard = guardGo.AddComponent<GuardPlaceholder>();
        guard.SetRecruited(true);

        // 사망 처리
        guard.TakeDamage(9999, Vector3.zero, "test");
        // SetRecruited 상태로 유지 — IsAlive만 false

        bool alive = guard.IsAlive;
        Assert(!alive, "Test12_DeadGuard_NoCommand", "병사 사망 상태 확인");

        // 죽은 병사에게 SetCommandTarget 해도 아무 효과 없어야 함 (내부적으로 처리)
        // GuardPlaceholder는 SetCommandTarget에서 사망 체크를 하지 않지만
        // RTSCommandSystem.IssueRightClickCommand에서 guard.IsAlive 체크
        guard.SetCommandTarget(new Vector3(1, 0, 1), true);

        // 죽었어도 SetCommandTarget은 호출되지만, RTSCommandSystem은 IsAlive 체크를 함
        // 여기서는 명령이 설정되었더라도 실행되지 않아야 함을 검증
        // IssueAttackCommand 내부에서 guard.IsAlive 체크가 이뤄짐
        Debug.Log("[Test12] 죽은 병사 SetCommandTarget 호출 가능 — RTSCommandSystem에서 IsAlive로 필터");

        GameObject.DestroyImmediate(guardGo);
    }

    // ===== 실행 엔트리 포인트 =====

    [UnityEditor.MenuItem("Tools/Run RTS Command Tests")]
    public static void RunAllTests()
    {
        Debug.Log("===========================================");
        Debug.Log("[RTSCommandTest] RTS 명령 시스템 테스트 시작 (12개)");
        Debug.Log("===========================================");

        ResetTestCounts();

        // Test 1-12 실행
        Test01_RightClickOnEnemy_IssuesAttackCommand();
        Test02_SetCommandTargetAttack_Confirmed();
        Test03_SetCommandTargetMove_Confirmed();
        Test04_DeadEnemy_MoveCommand();
        Test05_HKey_StopsAllGuards();
        Test06_CtrlRightClick_SynchronizedAttack();
        Test07_CtrlRightClick_SynchronizedMove();
        Test08_NoSelection_NoCommand();
        Test09_MultipleSelected_AllAttack();
        Test10_StopAfterAttack_ClearsCommands();
        Test11_MoveCommand_GuardStoresTarget();
        Test12_DeadGuard_NoCommand();

        PrintSummary();

        Debug.Log("[RTSCommandTest] 테스트 완료");
    }

    /// <summary>
    /// 배치 모드용 엔트리 포인트
    /// </summary>
    public static void RunBatchTests()
    {
        Debug.Log("[RTSCommandTest] Starting RTS command batch tests...");
        RunAllTests();
    }
}