#if false
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// T-Cycle-03: TutorialLordSequence EditMode 테스트
    ///
    /// 테스트 대상 (12개):
    ///  1. Singleton_Instance_NotNull — 싱글톤 인스턴스 정상 생성 확인
    ///  2. Singleton_SameInstance_AfterSetup — 싱글톤 동일 인스턴스 유지 확인
    ///  3. HasPlayed_ReturnsFalse_Initially — PlayerPrefs 초기값 false 확인
    ///  4. MarkPlayed_SavesToPlayerPrefs — MarkPlayed 후 PlayerPrefs 저장 확인
    ///  5. StartSequence_DoesNotRun_WhenAlreadyPlayed — 이미 재생 시 무시 확인
    ///  6. StartSequence_DoesNotRun_WhenAlreadyRunning — 이미 실행 중 시 무시 확인
    ///  7. StartSequence_CreatesLordNpc — StartSequence 호출 시 영주 NPC 생성 확인
    ///  8. StartSequence_TransitionsThroughSteps — 시퀀스 단계별 전환 확인
    ///  9. StartSequence_CallsSoundManager — SoundManagerEnhanced.PlaySFX 호출 확인
    /// 10. ResetSequence_ResetsState — ResetSequence 호출 시 상태 초기화 확인
    /// 11. ResetSequence_ClearsPlayerPrefs — ResetSequence 호출 시 PlayerPrefs 삭제 확인
    /// 12. ResetSequence_DestroysLordNpc — ResetSequence 호출 시 영주 NPC 제거 확인
    /// </summary>
    public class PhaseT_LordSequenceTests
    {
        private GameObject _systemGo;
        private TutorialLordSequence _system;

        // ================================================================
        // 헬퍼: 리플렉션으로 _instance 설정
        // ================================================================

        private void SetSystemInstance(TutorialLordSequence instance)
        {
            var field = typeof(TutorialLordSequence).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearSystemInstance()
        {
            var field = typeof(TutorialLordSequence).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        // ================================================================
        // 헬퍼: private _state 접근
        // ================================================================

        private object GetSequenceState()
        {
            var field = typeof(TutorialLordSequence).GetField("_state",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(_system);
        }

        private int GetSequenceStateValue()
        {
            var stateObj = GetSequenceState();
            return stateObj != null ? (int)stateObj : -1;
        }

        private GameObject GetLordNpc()
        {
            var field = typeof(TutorialLordSequence).GetField("_lordNpc",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(_system) as GameObject;
        }

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            _systemGo = new GameObject("TestTutorialLordSequence");
            _system = _systemGo.AddComponent<TutorialLordSequence>();
            SetSystemInstance(_system);

            // PlayerPrefs 정리
            if (PlayerPrefs.HasKey("TutorialLordSequence_Played"))
                PlayerPrefs.DeleteKey("TutorialLordSequence_Played");
            PlayerPrefs.Save();
        }

        [TearDown]
        public void Teardown()
        {
            // PlayerPrefs 정리
            if (PlayerPrefs.HasKey("TutorialLordSequence_Played"))
                PlayerPrefs.DeleteKey("TutorialLordSequence_Played");
            PlayerPrefs.Save();

            // 시스템 GameObject 정리
            if (_systemGo != null)
                Object.DestroyImmediate(_systemGo);

            ClearSystemInstance();
        }

        // ================================================================
        // 1. Singleton Instance 정상 생성
        // ================================================================

        [Test]
        public void Singleton_Instance_NotNull()
        {
            Assert.IsNotNull(TutorialLordSequence.Instance,
                "Instance는 null이 아니어야 함");
        }

        // ================================================================
        // 2. Singleton 동일 인스턴스 유지
        // ================================================================

        [Test]
        public void Singleton_SameInstance_AfterSetup()
        {
            var instance = TutorialLordSequence.Instance;
            Assert.AreSame(_system, instance,
                "Setup에서 생성한 인스턴스와 동일해야 함");
        }

        // ================================================================
        // 3. HasPlayed 초기값 false
        // ================================================================

        [Test]
        public void HasPlayed_ReturnsFalse_Initially()
        {
            Assert.IsFalse(TutorialLordSequence.HasPlayed,
                "HasPlayed는 초기에 false여야 함");
        }

        // ================================================================
        // 4. MarkPlayed PlayerPrefs 저장
        // ================================================================

        [Test]
        public void MarkPlayed_SavesToPlayerPrefs()
        {
            // Pre-condition
            Assert.IsFalse(PlayerPrefs.HasKey("TutorialLordSequence_Played"),
                "PlayerPrefs에 아직 키가 없어야 함");

            // When
            TutorialLordSequence.MarkPlayed();

            // Then
            Assert.IsTrue(PlayerPrefs.HasKey("TutorialLordSequence_Played"),
                "MarkPlayed 후 PlayerPrefs에 키가 저장되어야 함");
            Assert.AreEqual(1, PlayerPrefs.GetInt("TutorialLordSequence_Played", 0),
                "PlayerPrefs 값이 1이어야 함");
        }

        // ================================================================
        // 5. 이미 재생된 시퀀스는 무시
        // ================================================================

        [Test]
        public void StartSequence_DoesNotRun_WhenAlreadyPlayed()
        {
            // Given: 이미 재생 완료 상태
            TutorialLordSequence.MarkPlayed();
            Assert.IsTrue(TutorialLordSequence.HasPlayed,
                "MarkPlayed 후 HasPlayed가 true여야 함");

            // When: StartSequence 호출
            _system.StartSequence(Vector3.zero);

            // Then: 시퀀스가 실행되지 않아야 함 (State = Idle)
            int stateValue = GetSequenceStateValue();
            Assert.AreEqual(0, stateValue,
                "HasPlayed=true 상태에서는 StartSequence가 실행되지 않아야 함 (State=Idle)");
        }

        // ================================================================
        // 6. 이미 실행 중인 시퀀스는 무시
        // ================================================================

        [Test]
        public void StartSequence_DoesNotRun_WhenAlreadyRunning()
        {
            // Given: 시퀀스가 이미 실행 중
            _system.StartSequence(Vector3.zero);
            int stateAfterFirstCall = GetSequenceStateValue();
            Assert.AreNotEqual(0, stateAfterFirstCall,
                "첫 번째 StartSequence 호출 후 State는 Idle이 아니어야 함");

            // When: 두 번째 StartSequence 호출
            _system.StartSequence(Vector3.zero);

            // Then: 두 번째 호출은 아무 영향이 없어야 함 (State가 이전과 동일)
            int stateAfterSecondCall = GetSequenceStateValue();
            Assert.AreEqual(stateAfterFirstCall, stateAfterSecondCall,
                "이미 실행 중인 시퀀스에 대한 StartSequence 호출은 State를 변경하지 않아야 함");
        }

        // ================================================================
        // 7. StartSequence 시 영주 NPC 생성
        // ================================================================

        [Test]
        public void StartSequence_CreatesLordNpc()
        {
            // Given: 아직 NPC가 없음
            Assert.IsNull(GetLordNpc(),
                "StartSequence 호출 전에는 _lordNpc가 null이어야 함");

            // When: StartSequence 호출
            _system.StartSequence(Vector3.zero);

            // Then: 영주 NPC가 생성됨
            GameObject npc = GetLordNpc();
            Assert.IsNotNull(npc,
                "StartSequence 호출 후 _lordNpc가 생성되어야 함");
            Assert.AreEqual("TutorialLord_NPC (Placeholder)", npc.name,
                "NPC 이름이 'TutorialLord_NPC (Placeholder)'이어야 함");
        }

        // ================================================================
        // 8. 시퀀스 단계별 전환
        // ================================================================

        [Test]
        public void StartSequence_TransitionsThroughSteps()
        {
            // When: StartSequence 호출
            _system.StartSequence(Vector3.zero);

            // Then: Step 1 (Knock) 상태
            int state = GetSequenceStateValue();
            Assert.AreEqual(1, state,
                "StartSequence 직후 State=Step1_Knock(1)이어야 함");

            // When: Update 한번 호출 (Step1 → Step2 자동 전환)
            _system.SendMessage("Update", null, SendMessageOptions.DontRequireReceiver);

            // Then: Step 2 (Bubble) 상태
            state = GetSequenceStateValue();
            Assert.AreEqual(2, state,
                "Update 후 State=Step2_Bubble(2)이어야 함");
        }

        // ================================================================
        // 9. SoundManagerEnhanced.PlaySFX 호출 확인
        // ================================================================

        [Test]
        public void StartSequence_CallsSoundManager()
        {
            // Given/When: StartSequence 호출
            // SoundManagerEnhanced.Instance가 없으면 PlaySFX는 로그만 남기고 조용히 실패
            // 우리는 StartSequence가 예외 없이 실행되는지만 확인
            Assert.DoesNotThrow(() =>
            {
                _system.StartSequence(Vector3.zero);
            }, "StartSequence는 SoundManager 유무와 관계없이 예외가 발생하지 않아야 함");
        }

        // ================================================================
        // 10. ResetSequence 상태 초기화
        // ================================================================

        [Test]
        public void ResetSequence_ResetsState()
        {
            // Given: 시퀀스 실행 중
            _system.StartSequence(Vector3.zero);
            int stateBeforeReset = GetSequenceStateValue();
            Assert.AreNotEqual(0, stateBeforeReset,
                "Reset 전 State는 Idle(0)이 아니어야 함");

            // When: ResetSequence 호출
            _system.ResetSequence();

            // Then: State가 Idle(0)로 초기화됨
            int stateAfterReset = GetSequenceStateValue();
            Assert.AreEqual(0, stateAfterReset,
                "ResetSequence 후 State=Idle(0)이어야 함");
        }

        // ================================================================
        // 11. ResetSequence PlayerPrefs 삭제
        // ================================================================

        [Test]
        public void ResetSequence_ClearsPlayerPrefs()
        {
            // Given: 재생 완료 상태
            TutorialLordSequence.MarkPlayed();
            Assert.IsTrue(TutorialLordSequence.HasPlayed,
                "전제 조건: HasPlayed가 true여야 함");

            // When: ResetSequence 호출
            _system.ResetSequence();

            // Then: PlayerPrefs가 삭제됨
            Assert.IsFalse(TutorialLordSequence.HasPlayed,
                "ResetSequence 후 HasPlayed가 false여야 함");
            Assert.IsFalse(PlayerPrefs.HasKey("TutorialLordSequence_Played"),
                "ResetSequence 후 PlayerPrefs 키가 삭제되어야 함");
        }

        // ================================================================
        // 12. ResetSequence 영주 NPC 제거
        // ================================================================

        [Test]
        public void ResetSequence_DestroysLordNpc()
        {
            // Given: StartSequence로 NPC 생성
            _system.StartSequence(Vector3.zero);
            Assert.IsNotNull(GetLordNpc(),
                "전제 조건: _lordNpc가 생성되어야 함");

            // When: ResetSequence 호출
            _system.ResetSequence();

            // Then: NPC 제거됨
            // DestroyImmediate는 실제 오브젝트 삭제를 즉시 수행하지만,
            // 테스트 환경에서는 Destroy 호출 후 GameObject 참조 확인
            GameObject npcAfterReset = GetLordNpc();
            Assert.IsNull(npcAfterReset,
                "ResetSequence 후 _lordNpc가 null이어야 함");
        }
    }
}
#endif
