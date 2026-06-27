using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C14-05: RevealReason 시스템 — 영주 처형 시 복수 이유 표시
    /// C14-07: 독살 공모자 보상 연결
    /// C14-08: 복수명부 완료 조건 체크
    /// </summary>
    public static class RevengeListIntegration
    {
        private static bool _initialized = false;
        private static NotificationController _controller;
        private static bool _rewardApplied = false; // 중복 보상 방지

        /// <summary>
        /// LordSurrenderSystem.OnLordExecuted 구독 및 UI 초기화
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            LordSurrenderSystem.OnLordExecuted += OnLordExecuted;
            PoisonTakeoverSystem.OnLordPoisoned += OnLordPoisoned;
            AssassinationCutscene.OnAssassinationExecuted += OnAssassinationExecuted;

            // 숨은 GameObject 생성 또는 기존 GameObject 재사용 (도메인 리로드 대응)
            _controller = FindOrCreateController();

            // C14-07: 모든 독살 공모자 보상 구독
            RevengeListManager.Instance.AllPoisonFound += OnAllPoisonFound;

            _initialized = true;
            Debug.Log("[RevengeListIntegration] 초기화 완료 — OnLordExecuted, OnLordPoisoned, OnAssassinationExecuted 구독됨");
        }

        /// <summary>기존 GameObject가 있으면 재사용, 없으면 새로 생성 (도메인 리로드 안전)</summary>
        private static NotificationController FindOrCreateController()
        {
            const string goName = "[RevengeListIntegration]";

            // 도메인 리로드 후에도 DontDestroyOnLoad GameObject가 남아있을 수 있음
            var existing = GameObject.Find(goName);
            if (existing != null)
            {
                var ctrl = existing.GetComponent<NotificationController>();
                if (ctrl != null)
                    return ctrl;
            }

            var go = new GameObject(goName);
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<NotificationController>();
        }

        /// <summary>
        /// 구독 해제 및 정리 (도메인 리로드 / 게임 종료 시)
        /// </summary>
        public static void Deinitialize()
        {
            if (!_initialized) return;

            LordSurrenderSystem.OnLordExecuted -= OnLordExecuted;
            PoisonTakeoverSystem.OnLordPoisoned -= OnLordPoisoned;
            AssassinationCutscene.OnAssassinationExecuted -= OnAssassinationExecuted;

            if (RevengeListManager.Instance != null)
                RevengeListManager.Instance.AllPoisonFound -= OnAllPoisonFound;

            _initialized = false;
            Debug.Log("[RevengeListIntegration] 구독 해제 완료");
        }

        /// <summary>
        /// 영주 처형 이벤트 핸들러
        /// </summary>
        private static void OnLordExecuted(TerritoryId territoryId, LordSurrenderSystem.LordData lord)
        {
            string tid = territoryId.ToString();

            // 이유 공개
            RevengeListManager.Instance.RevealReason(tid);

            // 엔트리 조회
            var entry = RevengeListManager.Instance.GetEntry(tid);

            // 알림 텍스트 구성
            string message;
            if (entry.isPoisonConspirator)
            {
                message = $"☠️ {entry.lordName}이(가) 왕의 독살에 가담했다!";
            }
            else if (!string.IsNullOrEmpty(entry.revengeReason))
            {
                message = $"🔍 {entry.lordName}: {entry.revengeReason}";
            }
            else
            {
                message = $"🔍 {entry.lordName}: 복수 이유";
            }

            _controller?.ShowRevealNotification(message, entry.isPoisonConspirator);

            // C14-08: 모든 복수 완료 체크
            CheckFullCompletion();
        }

        /// <summary>
        /// C14-05: 독살/암살 영주 이벤트 핸들러 — PoisonTakeoverSystem.OnLordPoisoned
        /// </summary>
        private static void OnLordPoisoned(TerritoryId territoryId)
        {
            string tid = territoryId.ToString();

            // 이유 공개
            RevengeListManager.Instance.RevealReason(tid);

            // 엔트리 조회
            var entry = RevengeListManager.Instance.GetEntry(tid);

            // 알림 텍스트 구성
            string message;
            if (entry.isPoisonConspirator)
            {
                message = $"☠️ {entry.lordName}이(가) 왕의 독살에 가담했다!";
            }
            else if (!string.IsNullOrEmpty(entry.revengeReason))
            {
                message = $"☠️ {entry.lordName}(독살): {entry.revengeReason}";
            }
            else
            {
                message = $"☠️ {entry.lordName}(이)가 독살되었다!";
            }

            _controller?.ShowRevealNotification(message, entry.isPoisonConspirator);

            // C14-08: 모든 복수 완료 체크
            CheckFullCompletion();
        }

        /// <summary>
        /// C14-05: 암살 컷씬 영주 사망 핸들러 — AssassinationCutscene.OnAssassinationExecuted
        /// 독살과 동일한 RevealReason 로직 적용
        /// </summary>
        private static void OnAssassinationExecuted(TerritoryId territoryId)
        {
            string tid = territoryId.ToString();

            // 이유 공개
            RevengeListManager.Instance.RevealReason(tid);

            // 엔트리 조회
            var entry = RevengeListManager.Instance.GetEntry(tid);

            // 알림 텍스트 구성
            string message;
            if (entry.isPoisonConspirator)
            {
                message = $"☠️ {entry.lordName}이(가) 왕의 독살에 가담했다!";
            }
            else if (!string.IsNullOrEmpty(entry.revengeReason))
            {
                message = $"🗡️ {entry.lordName}(암살): {entry.revengeReason}";
            }
            else
            {
                message = $"🗡️ {entry.lordName}(이)가 암살되었다!";
            }

            _controller?.ShowRevealNotification(message, entry.isPoisonConspirator);

            // C14-08: 모든 복수 완료 체크
            CheckFullCompletion();
        }

        /// <summary>
        /// C14-07: 모든 독살 공모자 발견 시 보상 적용
        /// </summary>
        private static void OnAllPoisonFound()
        {
            if (_rewardApplied) return;
            _rewardApplied = true;

            var stats = PlayerStats.Instance;
            if (stats != null)
            {
                stats.ApplyRevengeListReward();
            }

            _controller?.ShowRewardNotification("🎉 모든 독살 공모자를 발견했다! 능력치가 영구 상승했다!");
        }

        /// <summary>
        /// C14-08: 모든 81명 완료 체크
        /// </summary>
        private static void CheckFullCompletion()
        {
            if (RevengeListManager.Instance.IsFullyComplete())
            {
                _controller?.ShowCompletionNotification("🏆 모든 복수가 완료되었다! 왕이여, 편히 쉬소서...");
                GameClearFlag.SetClear(); // 게임 클리어 플래그 설정
            }
        }

        /// <summary>
        /// 알림 표시를 담당하는 내부 MonoBehaviour
        /// </summary>
        private class NotificationController : MonoBehaviour
        {
            private enum NotificationType { Reveal, Reward, Completion }

            private struct Notification
            {
                public string message;
                public bool isPoison;
                public float startTime;
                public NotificationType type;
            }

            private List<Notification> _notifications = new List<Notification>();

            private GUIStyle _styleReveal;
            private GUIStyle _stylePoison;
            private GUIStyle _styleReward;
            private GUIStyle _styleCompletion;
            private bool _stylesInit;

            public void ShowRevealNotification(string message, bool isPoison)
            {
                _notifications.Add(new Notification
                {
                    message = message,
                    isPoison = isPoison,
                    startTime = Time.time,
                    type = NotificationType.Reveal
                });
            }

            public void ShowRewardNotification(string message)
            {
                _notifications.Add(new Notification
                {
                    message = message,
                    startTime = Time.time,
                    type = NotificationType.Reward
                });
            }

            public void ShowCompletionNotification(string message)
            {
                _notifications.Add(new Notification
                {
                    message = message,
                    startTime = Time.time,
                    type = NotificationType.Completion
                });
            }

            private void OnGUI()
            {
                EnsureStyles();

                float y = 60f;
                for (int i = _notifications.Count - 1; i >= 0; i--)
                {
                    var notif = _notifications[i];
                    float elapsed = Time.time - notif.startTime;
                    float duration = (notif.type == NotificationType.Completion) ? 5f : 3f;

                    if (elapsed > duration)
                    {
                        _notifications.RemoveAt(i);
                        continue;
                    }

                    // 페이드 아웃 (3초/5초)
                    float alpha = Mathf.Clamp01(1.0f - (elapsed / duration));

                    GUIStyle style;
                    Color baseColor;
                    switch (notif.type)
                    {
                        case NotificationType.Reveal:
                            if (notif.isPoison)
                            {
                                style = _stylePoison;
                                baseColor = new Color(1f, 0.3f, 0.3f, 1f);
                            }
                            else
                            {
                                style = _styleReveal;
                                baseColor = new Color(1f, 0.1f, 0.1f, 1f);
                            }
                            break;
                        case NotificationType.Reward:
                            style = _styleReward;
                            baseColor = new Color(0.3f, 1f, 0.3f, 1f);
                            break;
                        case NotificationType.Completion:
                            style = _styleCompletion;
                            baseColor = new Color(1f, 0.8f, 0f, 1f);
                            break;
                        default:
                            style = _styleReveal;
                            baseColor = new Color(1f, 0.1f, 0.1f, 1f);
                            break;
                    }

                    float textWidth = style.CalcSize(new GUIContent(notif.message)).x;
                    float x = (Screen.width - textWidth) / 2f;

                    // GUI.contentColor로 알파 페이드 아웃 (style.textColor 불변 유지)
                    Color prevColor = GUI.contentColor;
                    GUI.contentColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                    GUI.Label(new Rect(x, y, textWidth + 20, 45), notif.message, style);
                    GUI.contentColor = prevColor;
                    y += 50f;
                }
            }

            private void EnsureStyles()
            {
                if (_stylesInit) return;

                _styleReveal = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                _stylePoison = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                _styleReward = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                _styleCompletion = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                _stylesInit = true;
            }

            private void OnDestroy()
            {
                _notifications.Clear();
            }
        }
    }

    /// <summary>
    /// C14-08: 게임 클리어 플래그
    /// </summary>
    public static class GameClearFlag
    {
        public static bool IsCleared { get; private set; } = false;

        public static void SetClear()
        {
            if (!IsCleared)
            {
                IsCleared = true;
                Debug.Log("[GameClearFlag] 🏆 게임 클리어! 모든 복수가 완료되었다.");
            }
        }

        public static void Reset()
        {
            IsCleared = false;
        }
    }
}