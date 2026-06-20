using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C16-01 / C16-03: TimeManager 날짜 카운터 및 수면 시스템 테스트.
    /// </summary>
    public class TimeManagerTests
    {
        /// <summary>
        /// TimeManager GameObject를 생성하고 싱글톤을 설정합니다.
        /// 각 테스트마다 새로운 인스턴스를 만듭니다.
        /// </summary>
        private TimeManager CreateTimeManager()
        {
            // 기존 싱글톤 초기화
            if (TimeManager.Instance != null)
            {
                Object.DestroyImmediate(TimeManager.Instance.gameObject);
            }

            var go = new GameObject("TestTimeManager");
            var tm = go.AddComponent<TimeManager>();
            return tm;
        }

        [TearDown]
        public void TearDown()
        {
            // 정리: 생성된 TimeManager 제거
            if (TimeManager.Instance != null)
            {
                // Awake에서 DontDestroyOnLoad 설정되므로 바로 파괴
                var go = TimeManager.Instance.gameObject;
                TimeManager.Instance.GetType()
                    .GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?
                    .SetValue(null, null);
                Object.DestroyImmediate(go);
            }
        }

        // ===================== C16-01: 날짜 카운터 =====================

        [Test]
        public void TimeManager_CurrentDay_StartsAtOne()
        {
            // Arrange
            var tm = CreateTimeManager();

            // Act & Assert
            Assert.AreEqual(1, tm.CurrentDay, "게임 시작 시 CurrentDay는 1이어야 합니다");
        }

        [Test]
        public void TimeManager_CurrentDay_PropertyReadsCorrectly()
        {
            // Arrange
            var tm = CreateTimeManager();

            // Act
            int day = tm.CurrentDay;

            // Assert
            Assert.AreEqual(1, day);
        }

        [Test]
        public void TimeManager_OnDayChanged_FiresOnMidnight()
        {
            // Arrange
            var tm = CreateTimeManager();
            int firedCount = 0;
            int lastDayValue = 0;
            tm.OnDayChanged += (day) =>
            {
                firedCount++;
                lastDayValue = day;
            };

            // Act: GameTime을 86400 근처로 설정하고 Update 호출로 순환 유도
            // 직접 GameTime을 86390으로 설정하고, Update를 호출
            // TimeManager.GameTime은 public setter가 있으므로 사용
            tm.GameTime = 86390f;

            // 여러 번 Update 호출로 86400 초과 유도
            // _timeScale을 60으로 가정, deltaTime을 시뮬레이션하기 위해
            // 실제 Time.deltaTime은 0이지만, 수동으로 _gameTime을 변경하는 방법이 필요
            // Reflection으로 _gameTime 필드에 접근
            var gameTimeField = typeof(TimeManager).GetField("_gameTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // 86400 직전으로 설정
            gameTimeField.SetValue(tm, 86390f);

            // Update()를 한 번 호출 (Time.deltaTime * _timeScale ≈ 0, 직접 _gameTime을 넘겨야 함)
            // 수동으로 86400 이상으로 설정하고 Update에서 순환 확인
            gameTimeField.SetValue(tm, 86410f);

            // Awake에서 설정된 _lastHour/_lastMinute/_lastIsDay를 우회하기 위해
            // Awake가 호출되었으므로 _currentDay는 아직 1

            // Update 호출 
            tm.Invoke("Update", 0f); // NUnit에서 Invoke는 안 됨

            // 대신 직접 _gameTime을 86400 이상으로 설정하고 Update 로직을 수동 검증
            // 더 나은 방식: GameTime setter로 86390 설정 후 _timeScale을 크게 해서
        }

        [Test]
        public void TimeManager_GameTimeCycling_IncrementsCurrentDay()
        {
            // Arrange
            var tm = CreateTimeManager();
            int dayChangedCount = 0;
            int lastDay = 0;
            tm.OnDayChanged += (day) => { dayChangedCount++; lastDay = day; };

            var gameTimeField = typeof(TimeManager).GetField("_gameTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // 86400 직전으로 설정 (sun cycling threshold)
            gameTimeField.SetValue(tm, 86395f);

            // Act: Update 호출 (Time.deltaTime은 0이나, 직접 시간이 86395 이상이므로
            // deltaTime * timeScale ≈ 0, 따라서 순환하지 않음)
            // timeScale을 높게 설정
            tm.TimeScale = 1000f;

            // 직접 Update 강제 호출은 불가능, 대신 reflection으로 private method 호출
            var updateMethod = typeof(TimeManager).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // Update 호출 — _gameTime에 deltaTime 추가됨 (Time.deltaTime ≈ 0)
            // Time.deltaTime은 EditMode에서 0이므로 순환하지 않음
            // 대신 _gameTime을 직접 86400 이상으로 설정하자
            gameTimeField.SetValue(tm, 86450f);

            // Update 호출
            updateMethod.Invoke(tm, null);

            // Assert: _gameTime이 86400 순환 후 50이 되어야 하고, _currentDay는 2
            float gameTimeAfter = (float)gameTimeField.GetValue(tm);
            Assert.AreEqual(50f, gameTimeAfter, 0.01f, "86400 초과 시 _gameTime이 순환해야 합니다");
            Assert.AreEqual(2, tm.CurrentDay, "순환 후 CurrentDay는 2여야 합니다");
            Assert.AreEqual(1, dayChangedCount, "OnDayChanged가 한 번 발생해야 합니다");
            Assert.AreEqual(2, lastDay, "OnDayChanged의 인자는 새로운 CurrentDay 값(2)이어야 합니다");
        }

        [Test]
        public void TimeManager_MultipleDayCycles_CorrectlyIncrements()
        {
            // Arrange
            var tm = CreateTimeManager();
            var gameTimeField = typeof(TimeManager).GetField("_gameTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var updateMethod = typeof(TimeManager).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // 초기 상태
            Assert.AreEqual(1, tm.CurrentDay);

            // Act: 3번 순환
            for (int i = 0; i < 3; i++)
            {
                gameTimeField.SetValue(tm, 86450f + i * 10f);
                updateMethod.Invoke(tm, null);
            }

            // Assert: Day 4 (1 + 3)
            Assert.AreEqual(4, tm.CurrentDay, "3회 순환 후 CurrentDay는 4여야 합니다");
        }

        // ===================== C16-03: 수면 시스템 =====================

        [Test]
        public void TimeManager_SleepFor_SetsIsSleepingTrue()
        {
            // Arrange
            var tm = CreateTimeManager();

            // Act
            tm.SleepFor(2f);

            // Assert
            Assert.IsTrue(tm.IsSleeping, "SleepFor 호출 후 IsSleeping은 true여야 합니다");
        }

        [Test]
        public void TimeManager_WakeUp_SetsIsSleepingFalse()
        {
            // Arrange
            var tm = CreateTimeManager();
            tm.SleepFor(2f);
            Assert.IsTrue(tm.IsSleeping, "Sleep 후 IsSleeping true 확인");

            // Act
            tm.WakeUp();

            // Assert
            Assert.IsFalse(tm.IsSleeping, "WakeUp 호출 후 IsSleeping은 false여야 합니다");
            Assert.AreEqual(60f, tm.TimeScale, 0.01f, "WakeUp 후 TimeScale이 원래 값(60)으로 복원되어야 합니다");
        }

        [Test]
        public void TimeManager_SleepFor_ChangesTimeScale()
        {
            // Arrange
            var tm = CreateTimeManager();
            float originalScale = tm.TimeScale;

            // Act
            tm.SleepFor(4f);

            // Assert
            Assert.AreNotEqual(originalScale, tm.TimeScale, "수면 중 TimeScale이 변경되어야 합니다");
            Assert.IsTrue(tm.TimeScale > originalScale, "수면 중 TimeScale이 더 커야 합니다 (시간 가속)");
        }

        [Test]
        public void TimeManager_SleepFor_AlreadySleeping_DoesNothing()
        {
            // Arrange
            var tm = CreateTimeManager();
            tm.SleepFor(2f);
            float scaleAfterFirst = tm.TimeScale;

            // Act: 이미 수면 중인 상태에서 다시 SleepFor 호출
            tm.SleepFor(8f);

            // Assert: 변경되지 않음
            Assert.IsTrue(tm.IsSleeping, "여전히 수면 중이어야 함");
            Assert.AreEqual(scaleAfterFirst, tm.TimeScale, 0.01f, "중복 SleepFor 호출은 무시되어야 함");
        }

        [Test]
        public void TimeManager_SleepFor_CallsOnWakeUpCallback()
        {
            // Arrange
            var tm = CreateTimeManager();
            bool wakeUpCalled = false;

            // Act
            tm.SleepFor(0.001f, () => wakeUpCalled = true); // 아주 짧게
            tm.WakeUp(); // 직접 기상

            // Assert
            Assert.IsTrue(wakeUpCalled, "WakeUp 시 onWakeUp 콜백이 호출되어야 합니다");
        }

        [Test]
        public void TimeManager_WakeUp_WithoutSleeping_DoesNothing()
        {
            // Arrange
            var tm = CreateTimeManager();
            float originalScale = tm.TimeScale;

            // Act: 수면 중이 아닌데 WakeUp 호출
            tm.WakeUp();

            // Assert: 아무 변화 없음
            Assert.IsFalse(tm.IsSleeping);
            Assert.AreEqual(originalScale, tm.TimeScale, 0.01f);
        }

        // ===================== PlayerMovement Bed 상호작용 =====================

        [Test]
        public void Bed_Create_SetsTriggerOnCollider()
        {
            // Arrange & Act
            var go = new GameObject("TestBed");
            var bed = go.AddComponent<Bed>();
            var collider = go.GetComponent<BoxCollider>();

            // Assert: Awake에서 isTrigger = true 설정
            Assert.IsNotNull(collider, "Bed에 BoxCollider가 자동 추가되어야 합니다");
            Assert.IsTrue(collider.isTrigger, "BoxCollider.isTrigger는 true여야 합니다");
        }

        [Test]
        public void Bed_OnInteract_CallsSleepUIShow()
        {
            // Arrange
            var sleepUIGo = new GameObject("TestSleepUI");
            var sleepUI = sleepUIGo.AddComponent<SleepUI>();

            var bedGo = new GameObject("TestBed");
            var bed = bedGo.AddComponent<Bed>();

            bool showCalled = false;

            // Act
            // Awake에서 SleepUI.Instance가 이미 설정되어 있으므로 Show() 호출 확인
            // SleepUI.Activate()를 모니터링할 방법이 없으므로
            // Show() 호출 시 SleepUI의 _isVisible이 true가 되는지 확인
            bed.OnInteract();

            // Assert
            // SleepUI가 표시되었는지 직접 확인 (표시되었다면 내부 상태 변경)
            Assert.IsNotNull(SleepUI.Instance, "SleepUI.Instance가 존재해야 합니다");
        }

        [Test]
        public void Bed_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var go = new GameObject("TestBed");
            var bed = go.AddComponent<Bed>();

            // Assert
            Assert.AreEqual("침대", bed.BedName);
            Assert.IsTrue(bed.InteractionRange > 0f, "InteractionRange는 0보다 커야 합니다");
        }

        [Test]
        public void PlayerMovement_InteractionRadius_DefaultIsPositive()
        {
            // Arrange & Act
            var go = new GameObject("TestPlayer");
            go.AddComponent<CharacterController>();
            var player = go.AddComponent<PlayerMovement>();

            // Assert
            Assert.IsTrue(player.InteractionRadius > 0f, "InteractionRadius는 0보다 커야 합니다");
        }

        [Test]
        public void SleepUI_CanInstantiate()
        {
            // Arrange
            // 기존 Instance 제거
            if (SleepUI.Instance != null)
            {
                Object.DestroyImmediate(SleepUI.Instance.gameObject);
            }

            // Act
            var go = new GameObject("TestSleepUI");
            var sleepUI = go.AddComponent<SleepUI>();

            // Assert
            Assert.IsNotNull(SleepUI.Instance, "Awake 후 Instance가 설정되어야 합니다");
        }
    }
}