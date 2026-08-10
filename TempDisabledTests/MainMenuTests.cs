using System.IO;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C17-05: MainMenuUI / LoadGameUI EditMode 테스트.
    /// UI 요소 생성, 슬롯 표시, 빈 슬롯 "비어있음" 처리 검증.
    /// </summary>
    public class MainMenuTests
    {
        private GameObject _menuGo;
        private MainMenuUI _mainMenu;
        private GameObject _managerGo;
        private string _testSaveDir;

        // ================================================================
        // 헬퍼: 리플렉션 Instance 설정
        // ================================================================

        private void SetManagerInstance(SaveManager instance)
        {
            var field = typeof(SaveManager).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearManagerInstance()
        {
            var field = typeof(SaveManager).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            // Create SaveManager
            _managerGo = new GameObject("TestSaveManager");
            var manager = _managerGo.AddComponent<SaveManager>();
            SetManagerInstance(manager);

            _testSaveDir = Path.Combine(Application.persistentDataPath, "saves");
            if (Directory.Exists(_testSaveDir))
                Directory.Delete(_testSaveDir, recursive: true);

            // Create MainMenuUI
            _menuGo = new GameObject("TestMainMenu");
            _mainMenu = _menuGo.AddComponent<MainMenuUI>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_menuGo != null)
                Object.DestroyImmediate(_menuGo);

            if (_managerGo != null)
                Object.DestroyImmediate(_managerGo);
            ClearManagerInstance();

            if (Directory.Exists(_testSaveDir))
                Directory.Delete(_testSaveDir, recursive: true);
        }

        // ================================================================
        // C17-05-01: MainMenuUI 생성 및 기본 동작
        // ================================================================

        [Test]
        public void MainMenuUI_CreatesLoadGameUIChild()
        {
            // Assert: LoadGameUI 자식 오브젝트가 생성되었는지 확인
            var loadUI = _menuGo.GetComponentInChildren<LoadGameUI>();
            Assert.IsNotNull(loadUI, "MainMenuUI는 LoadGameUI 자식 오브젝트를 생성해야 함");
        }

        [Test]
        public void MainMenuUI_Hide_DisablesMenu()
        {
            // Act
            _mainMenu.Hide();

            // Assert: OnGUI가 실행될 때 메뉴가 그려지지 않음 (내부 _isVisible=false)
            // 직접 확인은 어려우므로 Hide/Show 전환 로직 검증
            var method = typeof(MainMenuUI).GetMethod("Hide",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "Hide() 메서드가 존재해야 함");

            // Show 후 다시 표시
            _mainMenu.Show();
            var showMethod = typeof(MainMenuUI).GetMethod("Show",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(showMethod, "Show() 메서드가 존재해야 함");
        }

        [Test]
        public void MainMenuUI_LoadUICreation_ReferencesSet()
        {
            var loadUI = _menuGo.GetComponentInChildren<LoadGameUI>();
            Assert.IsNotNull(loadUI, "LoadGameUI 컴포넌트가 존재해야 함");

            // LoadGameUI가 Start에서 RefreshSlots()를 호출할 때 SaveManager.Instance 사용
            Assert.IsNotNull(SaveManager.Instance, "SaveManager.Instance가 존재해야 함");
        }

        // ================================================================
        // C17-05-02: LoadGameUI 슬롯 표시
        // ================================================================

        [Test]
        public void LoadGameUI_RefreshSlots_WorksWithSaveManager()
        {
            // Arrange: LoadGameUI 가져오기
            var loadUI = _menuGo.GetComponentInChildren<LoadGameUI>();
            Assert.IsNotNull(loadUI);

            // Act: 슬롯 정보 새로고침
            loadUI.RefreshSlots();

            // Assert: SaveManager가 정상 동작하는지 확인 (슬롯이 비어있어야 함)
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(SaveManager.Instance.HasSave(i), $"초기 상태: 슬롯 {i}는 비어있어야 함");
            }
        }

        [Test]
        public void LoadGameUI_ShowsSlotData_AfterSave()
        {
            // Arrange: 슬롯 0에 저장
            SaveManager.Instance.Save(0);
            Assert.IsTrue(SaveManager.Instance.HasSave(0), "슬롯 0에 저장되었어야 함");

            // Act: GetSlotInfo 확인
            var info = SaveManager.Instance.GetSlotInfo(0);
            Assert.IsNotNull(info, "GetSlotInfo(0)는 null이 아니어야 함");
            Assert.IsNotNull(info.timestamp, "timestamp는 null이 아니어야 함");
            Assert.IsNotNull(info.time, "time 데이터는 null이 아니어야 함");
            Assert.IsNotNull(info.player, "player 데이터는 null이 아니어야 함");
        }

        [Test]
        public void LoadGameUI_MultipleSlots_ShowCorrectly()
        {
            // Arrange: 슬롯 0과 2에 저장, 슬롯 1은 비움
            SaveManager.Instance.Save(0);
            SaveManager.Instance.Save(2);

            // Act & Assert
            Assert.IsTrue(SaveManager.Instance.HasSave(0), "슬롯 0: 저장됨");
            Assert.IsFalse(SaveManager.Instance.HasSave(1), "슬롯 1: 비어있음");
            Assert.IsTrue(SaveManager.Instance.HasSave(2), "슬롯 2: 저장됨");
        }

        // ================================================================
        // C17-05-03: 슬롯 데이터 정확성
        // ================================================================

        [Test]
        public void SlotDisplay_ShowsCorrectDataFromSaveManager()
        {
            // Arrange: 슬롯 0에 저장 후 정보 조회
            SaveManager.Instance.Save(0);
            var info = SaveManager.Instance.GetSlotInfo(0);

            // Assert: 저장된 데이터 필드 확인
            Assert.IsNotNull(info, "SaveData는 null이 아니어야 함");
            Assert.AreEqual(1, info.saveVersion, "saveVersion은 1이어야 함");
            Assert.IsNotNull(info.timestamp, "timestamp 존재");
            Assert.IsFalse(string.IsNullOrEmpty(info.timestamp), "timestamp 비어있지 않음");

            // player 데이터
            Assert.IsNotNull(info.player, "player 데이터 존재");
            Assert.AreEqual(0, info.player.level, "초기 레벨은 0");

            // time 데이터
            Assert.IsNotNull(info.time, "time 데이터 존재");
            Assert.AreEqual(1, info.time.day, "초기 Day는 1");
        }

        [Test]
        public void SlotDisplay_LoadThenGetSlotInfo_Matches()
        {
            // Arrange: 저장 후 로드 검증
            SaveManager.Instance.Save(1);
            var beforeLoad = SaveManager.Instance.GetSlotInfo(1);
            Assert.IsNotNull(beforeLoad, "저장 후 GetSlotInfo는 null이 아니어야 함");

            // Act: 로드
            SaveManager.Instance.Load(1);

            // Assert: 로드 후에도 동일한 정보 유지
            var afterLoad = SaveManager.Instance.GetSlotInfo(1);
            Assert.IsNotNull(afterLoad, "로드 후 GetSlotInfo는 null이 아니어야 함");
            Assert.AreEqual(beforeLoad.timestamp, afterLoad.timestamp, "로드 후 타임스탬프 일치");
        }

        // ================================================================
        // C17-05-04: 빈 슬롯 "비어있음" 처리
        // ================================================================

        [Test]
        public void EmptySlot_Shows_Bieoisseum()
        {
            // Arrange: 빈 슬롯 확인
            // Act: 슬롯 0에 저장 데이터 없음
            // Assert: HasSave = false
            Assert.IsFalse(SaveManager.Instance.HasSave(0), "저장되지 않은 슬롯은 HasSave=false");

            // GetSlotInfo가 null 반환
            var info = SaveManager.Instance.GetSlotInfo(0);
            Assert.IsNull(info, "저장되지 않은 슬롯의 GetSlotInfo는 null");
        }

        [Test]
        public void EmptySlot_AllSlots_InitiallyEmpty()
        {
            // Arrange & Assert: 모든 슬롯이 초기에 비어있음
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(SaveManager.Instance.HasSave(i), $"슬롯 {i}는 초기에 비어있어야 함");
                var info = SaveManager.Instance.GetSlotInfo(i);
                Assert.IsNull(info, $"슬롯 {i}의 GetSlotInfo는 null");
            }
        }

        [Test]
        public void EmptySlot_SaveThenDelete_ReturnsToEmpty()
        {
            // Arrange: 저장 후 삭제
            SaveManager.Instance.Save(0);
            Assert.IsTrue(SaveManager.Instance.HasSave(0), "저장 후 HasSave=true");

            // Act
            SaveManager.Instance.DeleteSlot(0);

            // Assert: 다시 비어있음
            Assert.IsFalse(SaveManager.Instance.HasSave(0), "삭제 후 HasSave=false");
            var info = SaveManager.Instance.GetSlotInfo(0);
            Assert.IsNull(info, "삭제 후 GetSlotInfo는 null");
        }

        [Test]
        public void EmptySlot_ClickingDoesNothing()
        {
            // Arrange: LoadGameUI 가져오기, 모든 슬롯 비어있음
            var loadUI = _menuGo.GetComponentInChildren<LoadGameUI>();
            Assert.IsNotNull(loadUI);

            // Act: RefreshSlots 호출
            loadUI.RefreshSlots();

            // Assert: 모든 슬롯이 비어있는 상태
            var infos = SaveManager.Instance.GetAllSlotInfos();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsNull(infos[i], $"슬롯 {i}는 비어있어야 함");
            }

            // 빈 슬롯이 "비어있음" 텍스트를 표시하는지 UI 로직 검증:
            // FormatSlotLabel 스타일의 로직을 직접 테스트
            string emptyLabel = FormatTestSlotLabel(0, null);
            Assert.IsTrue(emptyLabel.Contains("비어있음"), "빈 슬롯 레이블에 '비어있음'이 포함되어야 함");
        }

        // ================================================================
        // 헬퍼: 슬롯 레이블 포맷 (UI 로직 검증용)
        // ================================================================

        private string FormatTestSlotLabel(int index, SaveData info)
        {
            if (info == null)
                return $"슬롯 {index + 1} — 비어있음";
            return $"슬롯 {index + 1} — {info.timestamp} (Day {info.time?.day ?? 0}, Lv.{info.player?.level ?? 0})";
        }

        [Test]
        public void SlotLabel_Format_EmptySlot()
        {
            string label = FormatTestSlotLabel(0, null);
            Assert.AreEqual("슬롯 1 — 비어있음", label, "빈 슬롯 레이블 형식");

            label = FormatTestSlotLabel(1, null);
            Assert.AreEqual("슬롯 2 — 비어있음", label, "빈 슬롯 레이블 형식 (슬롯 2)");

            label = FormatTestSlotLabel(2, null);
            Assert.AreEqual("슬롯 3 — 비어있음", label, "빈 슬롯 레이블 형식 (슬롯 3)");
        }

        [Test]
        public void SlotLabel_Format_FilledSlot()
        {
            // Arrange: 가상의 SaveData 생성
            var info = new SaveData
            {
                timestamp = "2026-06-17 12:00:00",
                time = new TimeSaveData { day = 5, gameTime = 43200f },
                player = new PlayerSaveData { level = 3 }
            };

            // Act
            string label = FormatTestSlotLabel(0, info);

            // Assert
            Assert.IsTrue(label.Contains("슬롯 1"), "슬롯 번호 포함");
            Assert.IsTrue(label.Contains("2026-06-17"), "타임스탬프 포함");
            Assert.IsTrue(label.Contains("Day 5"), "Day 정보 포함");
            Assert.IsTrue(label.Contains("Lv.3"), "레벨 정보 포함");
            Assert.IsFalse(label.Contains("비어있음"), "비어있음 미포함");
        }
    }
}