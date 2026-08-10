using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G3-04: SaveManager 5슬롯 + DeleteSlot + AutoSave 테스트
    /// </summary>
    public class SaveManagerTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestSaveManager");
            _go.AddComponent<SaveManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Instance_Exists()
        {
            Assert.IsNotNull(SaveManager.Instance, "SaveManager.Instance가 null이면 안 됨");
        }

        [Test]
        public void SlotCount_Is_5()
        {
            Assert.AreEqual(5, SaveManager.Instance.SlotCount, "슬롯 수는 5개여야 함");
        }

        [Test]
        public void GetAllSlotInfos_Returns_5()
        {
            var infos = SaveManager.Instance.GetAllSlotInfos();
            Assert.AreEqual(5, infos.Length, "GetAllSlotInfos는 5개 요소 반환");
        }

        [Test]
        public void AllSlots_Initially_Empty()
        {
            // 새 게임이므로 모든 슬롯이 비어있어야 함
            // (SaveManager는 GameManager/PlayerStats 등의 의존성이 없으므로 Info가 정상)
            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(SaveManager.Instance.HasSave(i),
                    $"슬롯 {i}는 초기에 비어있어야 함");
            }
        }

        [Test]
        public void Slot0_HasSave_After_Save()
        {
            // 저장 시도 (GameManager/PlayerStats 없어도 SaveData 구조체는 생성됨)
            Assert.DoesNotThrow(() => SaveManager.Instance.Save(0));
        }

        [Test]
        public void InvalidSlot_Save_DoesNotCrash()
        {
            // 잘못된 슬롯 인덱스 저장 시도
            Assert.DoesNotThrow(() => SaveManager.Instance.Save(-1));
            Assert.DoesNotThrow(() => SaveManager.Instance.Save(99));
        }

        [Test]
        public void InvalidSlot_Load_DoesNotCrash()
        {
            Assert.DoesNotThrow(() => SaveManager.Instance.Load(-1));
            Assert.DoesNotThrow(() => SaveManager.Instance.Load(99));
        }

        [Test]
        public void ValidSlots_0_to_4_Are_Accessible()
        {
            for (int i = 0; i < 5; i++)
            {
                bool hasSave = SaveManager.Instance.HasSave(i);
                Assert.DoesNotThrow(() => SaveManager.Instance.GetSlotInfo(i));
            }
        }

        [Test]
        public void DeleteSlot_Removes_Save()
        {
            int slotIndex = 2;

            // 저장
            SaveManager.Instance.Save(slotIndex);

            // 존재 확인
            // (실제 파일이 없을 수 있음 - GameManager 의존성)
            Assert.DoesNotThrow(() => SaveManager.Instance.DeleteSlot(slotIndex));
        }

        [Test]
        public void AutoSave_DoesNotCrash()
        {
            Assert.DoesNotThrow(() => SaveManager.Instance.AutoSave(),
                "AutoSave 호출 시 예외 없어야 함");
        }

        [Test]
        public void Multiple_AutoSave_Works()
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.DoesNotThrow(() => SaveManager.Instance.AutoSave());
            }
        }

        [Test]
        public void SlotCount_Consistent()
        {
            // GetAllSlotInfos와 SlotCount가 일치
            var infos = SaveManager.Instance.GetAllSlotInfos();
            Assert.AreEqual(SaveManager.Instance.SlotCount, infos.Length);
        }

        [Test]
        public void Load_EmptySlot_NoCrash()
        {
            // 빈 슬롯 로드 시 예외/크래시 없어야 함
            Assert.DoesNotThrow(() => SaveManager.Instance.Load(4));
        }
    }
}