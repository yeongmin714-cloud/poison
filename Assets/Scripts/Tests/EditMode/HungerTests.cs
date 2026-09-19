using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O10-01: 허기 시스템 테스트 — 초기값/클램프/재생 게이트 경계/이속 배율 경계/소모율/
    /// PlayerPrefs 저장 라운드트립/정적 이벤트 발화/스테일 세이프.
    /// PlayerPrefs 실사용 — 테스트 오염 방지 위해 키를 사전/사후 정리(TitleTests 패턴).
    /// 에디터 EditMode: Awake 미실행 — 싱글턴 프로퍼티 대신 컴포넌트 참조 직접 호출(O7 확립 패턴).
    /// </summary>
    public class HungerTests
    {
        private const string SaveKey = "hunger_v1";

        private HungerSystem _hs;
        private GameObject _hsGo;

        // HungerChanged 수신 기록
        private bool _eventFired;
        private float _eventValue;

        [SetUp]
        public void SetUp()
        {
            CleanupKeys();
            _hsGo = new GameObject("HungerSystemTest");
            _hs = _hsGo.AddComponent<HungerSystem>();
            _eventFired = false;
            _eventValue = -1f;
        }

        [TearDown]
        public void TearDown()
        {
            HungerSystem.HungerChanged -= OnHungerChanged; // 정적 이벤트 오염 방지
            if (_hsGo != null) Object.DestroyImmediate(_hsGo);
            _hsGo = null;
            _hs = null;
            CleanupKeys();
        }

        private static void CleanupKeys()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        private void OnHungerChanged(float v)
        {
            _eventFired = true;
            _eventValue = v;
        }

        // ===================== 초기값 / 클램프 =====================

        [Test]
        public void Initial_Hunger_Is100()
        {
            Assert.AreEqual(100f, _hs.Hunger, 0.0001f, "기본 허기 = 100");
        }

        [Test]
        public void ForceSet_OverMax_ClampsTo100()
        {
            _hs.ForceSet(150f);
            Assert.AreEqual(100f, _hs.Hunger, 0.0001f, "150 → 100 클램프");
        }

        [Test]
        public void ForceSet_Negative_ClampsTo0()
        {
            _hs.ForceSet(-10f);
            Assert.AreEqual(0f, _hs.Hunger, 0.0001f, "-10 → 0 클램프");
        }

        [Test]
        public void Eat_Increases_AndClampsAtMax()
        {
            _hs.ForceSet(50f);
            _hs.Eat(30f);
            Assert.AreEqual(80f, _hs.Hunger, 0.0001f, "50 + 30 = 80");

            _hs.Eat(500f);
            Assert.AreEqual(100f, _hs.Hunger, 0.0001f, "초과 섭취 → 100 클램프");
        }

        // ===================== BlocksNaturalRegen 경계 =====================

        [Test]
        public void BlocksNaturalRegen_At29_IsTrue()
        {
            _hs.ForceSet(29f);
            Assert.IsTrue(_hs.BlocksNaturalRegen(), "29 (< 30) → 재생 정지");
        }

        [Test]
        public void BlocksNaturalRegen_At30_IsFalse()
        {
            _hs.ForceSet(30f);
            Assert.IsFalse(_hs.BlocksNaturalRegen(), "30 (임계값 포함) → 재생 허용");
        }

        [Test]
        public void BlocksNaturalRegen_At100_IsFalse()
        {
            _hs.ForceSet(100f);
            Assert.IsFalse(_hs.BlocksNaturalRegen(), "만복 → 재생 허용");
        }

        // ===================== GetSpeedMultiplier 경계 =====================

        [Test]
        public void GetSpeedMultiplier_At19_Is07()
        {
            _hs.ForceSet(19f);
            Assert.AreEqual(0.7f, _hs.GetSpeedMultiplier(), 0.0001f, "19 (< 20) → ×0.7");
        }

        [Test]
        public void GetSpeedMultiplier_At49_Is085()
        {
            _hs.ForceSet(49f);
            Assert.AreEqual(0.85f, _hs.GetSpeedMultiplier(), 0.0001f, "49 (< 50) → ×0.85");
        }

        [Test]
        public void GetSpeedMultiplier_At50_Is10()
        {
            _hs.ForceSet(50f);
            Assert.AreEqual(1f, _hs.GetSpeedMultiplier(), 0.0001f, "50 → ×1.0");
        }

        // ===================== 소모율 =====================

        [Test]
        public void GetConsumptionPerGameHour_Is4()
        {
            Assert.AreEqual(4f, _hs.GetConsumptionPerGameHour(), 0.0001f,
                "인게임 1시간당 -4 (하루 -96, 약 1일 1식)");
        }

        // ===================== 저장 라운드트립 (TitleTests 패턴) =====================

        [Test]
        public void SaveLoad_RoundTrip_PlayerPrefs()
        {
            _hs.ForceSet(37f);
            _hs.Save();
            Assert.AreEqual(37f, PlayerPrefs.GetFloat(SaveKey, -1f), 0.0001f,
                "Save → PlayerPrefs 직접 검증");

            PlayerPrefs.SetFloat(SaveKey, 55f);
            PlayerPrefs.Save();
            _hs.ForceSet(0f);
            _hs.Load();
            Assert.AreEqual(55f, _hs.Hunger, 0.0001f, "Load → PlayerPrefs 복원");
        }

        // ===================== 정적 이벤트 =====================

        [Test]
        public void HungerChanged_Fires_OnEat()
        {
            _hs.ForceSet(50f); // 사전 상태(구독 전이라 수신 없음)
            HungerSystem.HungerChanged += OnHungerChanged;

            _hs.Eat(10f);
            Assert.IsTrue(_eventFired, "Eat 시 HungerChanged 발화");
            Assert.AreEqual(60f, _eventValue, 0.0001f, "이벤트 값 = 변경 후 허기");
        }

        // ===================== 스테일 세이프 =====================

        [Test]
        public void StaleSafe_Update_WithoutTimeManager_NoNRE_NoDecrement()
        {
            // 전제: 에디터 EditMode — TimeManager 씬 인스턴스 없음
            Assert.IsNull(Object.FindAnyObjectByType<TimeManager>(),
                "전제: TimeManager 부재 환경");

            var update = typeof(HungerSystem).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(update, "Update 메서드 존재");

            Assert.DoesNotThrow(() => update.Invoke(_hs, null), "TimeManager 없어도 NRE 없음");
            Assert.AreEqual(100f, _hs.Hunger, 0.0001f, "TimeManager 부재 — 감소 스킵");
        }
    }
}
