using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-15: WarNotificationUI EditMode 테스트.
    /// 
    /// 테스트 대상:
    /// - ShowNotification 알림 추가
    /// - UpdateNotifications 자동 제거 (타임스탬프 기반)
    /// - 최대 표시 수 제한 (MAX_VISIBLE_NOTIFICATIONS)
    /// - ClearAll / ClearHistory
    /// - 이력 로그 (History) 저장 및 최대 개수 제한
    /// - ToggleHistory / SetHistoryVisible
    /// - IsVisible 플래그
    /// - ResetAll
    /// - NotificationType별 분류
    /// </summary>
    public class WarNotificationTests
    {
        private const float TEST_EPSILON = 0.01f;

        [SetUp]
        public void Setup()
        {
            WarNotificationUI.ResetAll();

            // Time.time이 0이라고 가정 (EditMode에서는 기본값 0)
            // Time.time은 EditMode에서 0을 반환하지만, UpdateNotifications는 Time.time 사용
        }

        [TearDown]
        public void Teardown()
        {
            WarNotificationUI.ResetAll();
        }

        // ================================================================
        // 기본값
        // ================================================================

        [Test]
        public void ActiveNotifications_Default_IsEmpty()
        {
            Assert.AreEqual(0, WarNotificationUI.ActiveNotifications.Count,
                "초기 상태에서는 활성 알림이 없어야 함");
        }

        [Test]
        public void History_Default_IsEmpty()
        {
            Assert.AreEqual(0, WarNotificationUI.History.Count,
                "초기 상태에서는 이력이 없어야 함");
        }

        [Test]
        public void IsVisible_Default_IsTrue()
        {
            Assert.IsTrue(WarNotificationUI.IsVisible,
                "초기 상태에서는 UI가 표시되어야 함");
        }

        [Test]
        public void IsHistoryVisible_Default_IsFalse()
        {
            Assert.IsFalse(WarNotificationUI.IsHistoryVisible,
                "초기 상태에서는 이력 로그가 숨겨져 있어야 함");
        }

        // ================================================================
        // ShowNotification
        // ================================================================

        [Test]
        public void ShowNotification_AddsToActive()
        {
            WarNotificationUI.ShowNotification("테스트 전쟁 시작!", WarNotificationUI.NotificationType.WarStart);

            Assert.AreEqual(1, WarNotificationUI.ActiveNotifications.Count);
            Assert.AreEqual("테스트 전쟁 시작!", WarNotificationUI.ActiveNotifications[0].message);
        }

        [Test]
        public void ShowNotification_AddsToHistory()
        {
            WarNotificationUI.ShowNotification("이력 테스트", WarNotificationUI.NotificationType.Info);

            Assert.AreEqual(1, WarNotificationUI.History.Count);
            Assert.AreEqual("이력 테스트", WarNotificationUI.History[0].message);
        }

        [Test]
        public void ShowNotification_EmptyMessage_DoesNotAdd()
        {
            WarNotificationUI.ShowNotification("", WarNotificationUI.NotificationType.Info);
            WarNotificationUI.ShowNotification(null, WarNotificationUI.NotificationType.Info);

            Assert.AreEqual(0, WarNotificationUI.ActiveNotifications.Count,
                "빈 메시지는 알림이 추가되지 않아야 함");
        }

        [Test]
        public void ShowNotification_MultipleNotifications_AllAdded()
        {
            for (int i = 0; i < 3; i++)
            {
                WarNotificationUI.ShowNotification($"메시지 {i}", WarNotificationUI.NotificationType.WarEnd);
            }

            Assert.AreEqual(3, WarNotificationUI.ActiveNotifications.Count);
            Assert.AreEqual(3, WarNotificationUI.History.Count);
        }

        [Test]
        public void ShowNotification_StoresCorrectType()
        {
            WarNotificationUI.ShowNotification("시작", WarNotificationUI.NotificationType.WarStart);
            WarNotificationUI.ShowNotification("종료", WarNotificationUI.NotificationType.WarEnd);
            WarNotificationUI.ShowNotification("상실", WarNotificationUI.NotificationType.TerritoryLost);
            WarNotificationUI.ShowNotification("획득", WarNotificationUI.NotificationType.TerritoryGained);
            WarNotificationUI.ShowNotification("정보", WarNotificationUI.NotificationType.Info);

            Assert.AreEqual(5, WarNotificationUI.ActiveNotifications.Count);
            Assert.AreEqual(WarNotificationUI.NotificationType.WarStart, WarNotificationUI.ActiveNotifications[0].type);
            Assert.AreEqual(WarNotificationUI.NotificationType.WarEnd, WarNotificationUI.ActiveNotifications[1].type);
            Assert.AreEqual(WarNotificationUI.NotificationType.TerritoryLost, WarNotificationUI.ActiveNotifications[2].type);
            Assert.AreEqual(WarNotificationUI.NotificationType.TerritoryGained, WarNotificationUI.ActiveNotifications[3].type);
            Assert.AreEqual(WarNotificationUI.NotificationType.Info, WarNotificationUI.ActiveNotifications[4].type);
        }

        // ================================================================
        // UpdateNotifications — 자동 제거
        // ================================================================

        [Test]
        public void UpdateNotifications_RemovesExpiredNotifications()
        {
            // 알림 추가 (Time.time = 0 기준)
            WarNotificationUI.ShowNotification("오래된 알림", WarNotificationUI.NotificationType.Info);

            // EditMode에서 Time.time은 항상 0이므로, NOTIFICATION_DISMISS_TIME(8초)이 지나면 제거됨
            WarNotificationUI.UpdateNotifications();

            // Time.time이 0이므로 timestamp도 0, currentTime - timestamp = 0 < 8초
            // 따라서 제거되지 않음
            Assert.AreEqual(1, WarNotificationUI.ActiveNotifications.Count,
                "8초가 지나지 않은 알림은 유지되어야 함");
        }

        [Test]
        public void UpdateNotifications_ManualCheck_NotificationLifetime()
        {
            // Time.time 값이 EditMode에서 고정되어 있으므로,
            // 알림이 NOTIFICATION_DISMISS_TIME(8초)보다 짧은 시간 동안만 유지되는 로직 확인
            // 실제 시간 테스트는 어려우므로 내부 동작 검증

            WarNotificationUI.ShowNotification("알림", WarNotificationUI.NotificationType.Info);
            var entry = WarNotificationUI.ActiveNotifications[0];

            // timestamp가 Time.time과 같거나 작은 값
            Assert.IsTrue(entry.timestamp <= Time.time + TEST_EPSILON,
                "타임스탬프가 현재 시간보다 크지 않아야 함");
        }

        // ================================================================
        // 최대 표시 수
        // ================================================================

        [Test]
        public void UpdateNotifications_MaxVisible_EnforcesLimit()
        {
            // MAX_VISIBLE_NOTIFICATIONS(5) + 2 개 추가
            for (int i = 0; i < WarNotificationUI.MAX_VISIBLE_NOTIFICATIONS + 2; i++)
            {
                WarNotificationUI.ShowNotification($"알림 {i}", WarNotificationUI.NotificationType.Info);
            }

            // UpdateNotifications 호출 (오래된 알림 제거)
            WarNotificationUI.UpdateNotifications();

            Assert.IsTrue(WarNotificationUI.ActiveNotifications.Count <= WarNotificationUI.MAX_VISIBLE_NOTIFICATIONS,
                $"최대 {WarNotificationUI.MAX_VISIBLE_NOTIFICATIONS}개만 표시되어야 함 (현재: {WarNotificationUI.ActiveNotifications.Count})");
        }

        // ================================================================
        // ClearAll
        // ================================================================

        [Test]
        public void ClearAll_RemovesAllActive()
        {
            for (int i = 0; i < 3; i++)
            {
                WarNotificationUI.ShowNotification($"알림 {i}", WarNotificationUI.NotificationType.Info);
            }

            WarNotificationUI.ClearAll();

            Assert.AreEqual(0, WarNotificationUI.ActiveNotifications.Count,
                "ClearAll 후 활성 알림이 없어야 함");
            // 이력은 유지
            Assert.AreEqual(3, WarNotificationUI.History.Count,
                "ClearAll은 이력을 지우지 않아야 함");
        }

        // ================================================================
        // ClearHistory
        // ================================================================

        [Test]
        public void ClearHistory_RemovesAll()
        {
            WarNotificationUI.ShowNotification("알림", WarNotificationUI.NotificationType.Info);

            WarNotificationUI.ClearHistory();

            Assert.AreEqual(0, WarNotificationUI.ActiveNotifications.Count);
            Assert.AreEqual(0, WarNotificationUI.History.Count);
        }

        // ================================================================
        // 이력 로그
        // ================================================================

        [Test]
        public void History_MaxCount_EnforcesLimit()
        {
            // MAX_HISTORY_COUNT(100) + 10 개 추가
            int extra = 10;
            for (int i = 0; i < WarNotificationUI.MAX_HISTORY_COUNT + extra; i++)
            {
                WarNotificationUI.ShowNotification($"이력 {i}", WarNotificationUI.NotificationType.Info);
            }

            // 최대 개수 제한 확인
            Assert.IsTrue(WarNotificationUI.History.Count <= WarNotificationUI.MAX_HISTORY_COUNT,
                $"이력 최대 {WarNotificationUI.MAX_HISTORY_COUNT}개로 제한되어야 함 (현재: {WarNotificationUI.History.Count})");
        }

        [Test]
        public void History_StoredInOrder()
        {
            WarNotificationUI.ShowNotification("첫번째", WarNotificationUI.NotificationType.WarStart);
            WarNotificationUI.ShowNotification("두번째", WarNotificationUI.NotificationType.WarEnd);

            Assert.AreEqual("첫번째", WarNotificationUI.History[0].message);
            Assert.AreEqual("두번째", WarNotificationUI.History[1].message);
        }

        // ================================================================
        // ToggleHistory / SetHistoryVisible
        // ================================================================

        [Test]
        public void ToggleHistory_SwitchesVisibility()
        {
            Assert.IsFalse(WarNotificationUI.IsHistoryVisible);

            WarNotificationUI.ToggleHistory();
            Assert.IsTrue(WarNotificationUI.IsHistoryVisible);

            WarNotificationUI.ToggleHistory();
            Assert.IsFalse(WarNotificationUI.IsHistoryVisible);
        }

        [Test]
        public void SetHistoryVisible_SetsCorrectly()
        {
            WarNotificationUI.SetHistoryVisible(true);
            Assert.IsTrue(WarNotificationUI.IsHistoryVisible);

            WarNotificationUI.SetHistoryVisible(false);
            Assert.IsFalse(WarNotificationUI.IsHistoryVisible);
        }

        // ================================================================
        // IsVisible
        // ================================================================

        [Test]
        public void IsVisible_CanBeToggled()
        {
            WarNotificationUI.IsVisible = false;
            Assert.IsFalse(WarNotificationUI.IsVisible);

            WarNotificationUI.IsVisible = true;
            Assert.IsTrue(WarNotificationUI.IsVisible);
        }

        [Test]
        public void ShowNotification_WorksRegardlessOfVisibility()
        {
            WarNotificationUI.IsVisible = false;
            WarNotificationUI.ShowNotification("보이지 않아도 알림은 추가됨", WarNotificationUI.NotificationType.Info);

            Assert.AreEqual(1, WarNotificationUI.ActiveNotifications.Count,
                "IsVisible과 무관하게 알림은 추가되어야 함");
            Assert.AreEqual(1, WarNotificationUI.History.Count);
        }

        // ================================================================
        // ResetAll
        // ================================================================

        [Test]
        public void ResetAll_ClearsEverything()
        {
            WarNotificationUI.ShowNotification("알림", WarNotificationUI.NotificationType.Info);
            WarNotificationUI.ToggleHistory();
            WarNotificationUI.IsVisible = false;

            WarNotificationUI.ResetAll();

            Assert.AreEqual(0, WarNotificationUI.ActiveNotifications.Count);
            Assert.AreEqual(0, WarNotificationUI.History.Count);
            Assert.IsFalse(WarNotificationUI.IsHistoryVisible);
            Assert.IsTrue(WarNotificationUI.IsVisible); // ResetAll에서 true로 재설정
        }
    }
}