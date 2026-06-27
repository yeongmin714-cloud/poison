using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.UI.Themes;
using System.Collections.Generic;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// C14-03: 복수명부 윈도우 — IMGUI 기반, 'K' 키로 열기
    /// 좌측: 81명 목록 (스크롤), 우측: 선택된 영주 상세 정보
    /// 상단: 발견/완료 통계
    /// </summary>
    public class RevengeListWindow : UIWindow
    {
        private UIDesignTheme _revengeTheme;

        // ======================================================================
        // 스타일
        // ======================================================================
        private GUIStyle _styleTitle;
        private GUIStyle _styleStatLabel;
        private GUIStyle _styleStatValue;
        private GUIStyle _styleListItem;
        private GUIStyle _styleListRevealed;
        private GUIStyle _styleListCompleted;
        private GUIStyle _styleDetailLabel;
        private GUIStyle _styleDetailValue;
        private GUIStyle _styleDetailReason;
        private GUIStyle _styleCloseButton;
        private GUIStyle _styleDetailHidden;   // 미발견 영주 ??? 스타일 (GC 방지)
        private GUIStyle _styleDetailStatus;   // 상태 텍스트 스타일 (GC 방지)

        // ======================================================================
        // 상태
        // ======================================================================
        private Vector2 _scrollPos = Vector2.zero;
        private int _selectedIndex = -1;
        private string _cachedStatsText = "";       // OnGUI GC 방지: 통계 문자열 캐시
        private int _lastRevealedCount = -1;
        private int _lastCompletedCount = -1;
        private int _lastTotalPoisonCount = -1;

        // ======================================================================
        // UIWindow 상속
        // ======================================================================

        protected override void OnShow()
        {
            Debug.Log("[RevengeListWindow] 열림");
            _selectedIndex = -1;

            if (_revengeTheme == null)
                _revengeTheme = Phase33_Themes.RevengeTheme();
            ApplyTheme(_revengeTheme);

            // RevengeListManager가 초기화되지 않았으면 초기화
            if (!RevengeListManager.Instance.IsInitialized)
            {
                RevengeListManager.Instance.Initialize();
            }
        }

        protected override void OnHide()
        {
            Debug.Log("[RevengeListWindow] 닫힘");
        }

        // ======================================================================
        // OnGUI — IMGUI 렌더링
        // ======================================================================

        private void OnGUI()
        {
            if (!IsOpen) return;

            EnsureStyles();

            var mgr = RevengeListManager.Instance;
            if (!mgr.IsInitialized) return;

            // GC 방지: Entries를 한 번만 캐싱 (AsReadOnly() 매 접근 시 ReadOnlyCollection 할당 방지)
            var allEntries = mgr.Entries;
            int total = allEntries.Count;
            if (total == 0) return;

            // C14-12: 창 크기 — 최소 500x400, 최대 900x700
            float panelW = Mathf.Clamp(Screen.width * 0.55f, 500f, 900f);
            float panelH = Mathf.Clamp(Screen.height * 0.65f, 400f, 700f);

            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            // 배경
            GUI.Box(new Rect(x, y, panelW, panelH), "");

            // 상단: 타이틀 + 통계
            DrawTopBar(x, y, panelW, mgr, total);

            // 좌측: 81명 목록 (스크롤)
            DrawLordList(x, y, panelW, panelH, allEntries, total);

            // 우측: 선택된 영주 상세 정보
            DrawDetailPanel(x, y, panelW, panelH, allEntries, total);

            // 닫기 버튼
            if (GUI.Button(new Rect(x + panelW - 70, y + panelH - 38, 135, 42), "닫기", _styleCloseButton))
            {
                Hide();
            }
        }

        // ======================================================================
        // 상단 통계 표시줄
        // ======================================================================

        private void DrawTopBar(float x, float y, float panelW, RevengeListManager mgr, int total)
        {
            int completed = mgr.GetCompletionCount();
            int revealedPoison = mgr.GetRevealedPoisonConspiratorCount();
            int totalPoison = mgr.GetPoisonConspirators().Count;

            // GC 방지: 값이 변경된 경우에만 문자열 재생성
            if (_lastRevealedCount != revealedPoison || _lastCompletedCount != completed || _lastTotalPoisonCount != totalPoison)
            {
                _cachedStatsText = $"발견: {revealedPoison}/{totalPoison} 독살 공모자  |  완료: {completed}/{total}";
                _lastRevealedCount = revealedPoison;
                _lastCompletedCount = completed;
                _lastTotalPoisonCount = totalPoison;
            }

            const float statsH = 54f;

            // C14-12: 제목 "🗡️ 복수명부"
            GUI.Label(new Rect(x + 12, y + 4, 300, statsH), "🗡️ 복수명부", _styleTitle);

            // C14-12: 통계: "발견: X/10 독살 공모자 | 완료: X/81"
            GUI.Label(new Rect(x + 200, y + 6, panelW - 220, statsH), _cachedStatsText, _styleStatLabel);
        }

        // ======================================================================
        // 좌측: 영주 목록 (스크롤)
        // ======================================================================

        private void DrawLordList(float x, float y, float panelW, float panelH, IReadOnlyList<RevengeListEntry> entries, int total)
        {
            const float statsH = 54f;
            const float listItemH = 33f;
            float listW = panelW * 0.44f;
            float listX = x + 10;
            float listY = y + statsH + 10;
            float listH = panelH - statsH - 70;

            float totalH = entries.Count * listItemH + 10;

            // C14-12: 스크롤뷰 — 높이 22px 라인
            _scrollPos = GUI.BeginScrollView(
                new Rect(listX, listY, listW, listH),
                _scrollPos,
                new Rect(0, 0, listW - 20, totalH));

            float cy = 4;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool isSelected = (i == _selectedIndex);

                // 항목 배경 (선택된 항목 하이라이트)
                if (isSelected)
                {
                    GUI.Box(new Rect(2, cy, listW - 24, listItemH), "");
                }

                // 항목 텍스트
                string label;
                GUIStyle style;

                if (!entry.isRevealed && !entry.isCompleted)
                {
                    // 미발견: "???" (회색)
                    label = "???";
                    style = _styleListItem;
                }
                else if (entry.isCompleted)
                {
                    // C14-12: 완료 — ✅ 마크, 취소선 없음
                    string poisonMark = entry.isPoisonConspirator ? " ☠️" : "";
                    label = $"✅ {entry.lordName}{poisonMark}";
                    style = _styleListCompleted;
                }
                else
                {
                    // 공개됨: 영주 이름 + 사유 (주황색)
                    // C14-12: 독살 공모자 ☠️ 아이콘
                    string poisonMark = entry.isPoisonConspirator ? " ☠️" : "";
                    label = $"{entry.lordName}{poisonMark}";
                    style = _styleListRevealed;
                }

                // 클릭 처리
                Rect itemRect = new Rect(6, cy, listW - 32, listItemH);
                if (GUI.Button(itemRect, label, style))
                {
                    _selectedIndex = i;
                }

                cy += listItemH;
            }

            GUI.EndScrollView();
        }

        // ======================================================================
        // 우측: 선택된 영주 상세 정보
        // ======================================================================

        private void DrawDetailPanel(float x, float y, float panelW, float panelH, IReadOnlyList<RevengeListEntry> entries, int total)
        {
            const float statsH = 54f;
            float listW = panelW * 0.44f;
            float detailX = x + listW + 20;
            float detailY = y + statsH + 10;
            float detailW = panelW - listW - 40;
            float detailH = panelH - statsH - 80;

            GUI.Box(new Rect(detailX, detailY, detailW, detailH), "");

            if (_selectedIndex < 0 || _selectedIndex >= entries.Count)
            {
                GUI.Label(new Rect(detailX + 10, detailY + 10, detailW - 20, 30),
                    "영주를 선택하세요.", _styleDetailLabel);
                return;
            }

            var entry = entries[_selectedIndex];

            // TerritoryDatabase에서 영지 정보 조회
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(entry.territoryId);

            float dy = detailY + 14;

            // 영주 이름 (독살 공모자 ☠️ 배지)
            string nameStr = entry.isRevealed || entry.isCompleted
                ? entry.lordName
                : "???";
            string poisonBadge = entry.isPoisonConspirator ? " ☠️" : "";
            GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 36), $"👤 {nameStr}{poisonBadge}", _styleTitle);
            dy += 30;

            // 국가
            string nationStr = GetNationDisplayName(def.nation);
            GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), "국가", _styleDetailLabel);
            GUI.Label(new Rect(detailX + 100, dy, detailW - 120, 30), nationStr, _styleDetailValue);
            dy += 24;

            // 난이도
            string diffStr = GetDifficultyDisplayName(def.difficulty);
            GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), "난이도", _styleDetailLabel);
            GUI.Label(new Rect(detailX + 100, dy, detailW - 120, 30), diffStr, _styleDetailValue);
            dy += 24;

            // 영지
            GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), "영지", _styleDetailLabel);
            string terrStr = !string.IsNullOrEmpty(def.territoryName)
                ? def.territoryName
                : entry.territoryId;
            GUI.Label(new Rect(detailX + 100, dy, detailW - 120, 30), terrStr, _styleDetailValue);
            dy += 30;

            // 구분선
            GUI.Box(new Rect(detailX + 10, dy, detailW - 20, 2), "");
            dy += 10;

            // 복수 이유 (공개된 경우에만)
            if (entry.isRevealed || entry.isCompleted)
            {
                GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), "복수 이유", _styleDetailLabel);
                dy += 22;

                string reasonText = entry.revengeReason;
                if (entry.isPoisonConspirator)
                {
                    reasonText = $"☠️ {reasonText}";
                }
                GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 66), reasonText, _styleDetailReason);
                dy += 50;
            }
            else
            {
                GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), "복수 이유", _styleDetailLabel);
                dy += 22;
                GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), "???", _styleDetailHidden);
                dy += 30;
            }

            // 상태
            GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), "상태", _styleDetailLabel);
            dy += 22;

            string statusText;
            Color statusColor;

            if (entry.isCompleted)
            {
                statusText = "✅ 복수 완료";
                statusColor = Color.gray;
            }
            else if (entry.isRevealed)
            {
                statusText = "🔍 공개됨 (영주 사망)";
                statusColor = new Color(1f, 0.6f, 0f); // 주황
            }
            else
            {
                statusText = "❓ 미발견";
                statusColor = Color.gray;
            }

            GUI.Label(new Rect(detailX + 14, dy, detailW - 28, 30), statusText, _styleDetailStatus);
            // Dynamic color update (매 프레임 new GUIStyle 방지)
            _styleDetailStatus.normal.textColor = statusColor;
        }

        // ======================================================================
        // 스타일 초기화
        // ======================================================================

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = Color.white }
            };

            _styleStatLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _styleStatValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.green }
            };

            _styleListItem = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.gray },
                hover = { textColor = Color.white },
                margin = new RectOffset(0, 0, 1, 1)
            };

            _styleListRevealed = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.6f, 0f) }, // 주황
                hover = { textColor = Color.white },
                margin = new RectOffset(0, 0, 1, 1)
            };

            _styleListCompleted = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }, // 회색
                hover = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                margin = new RectOffset(0, 0, 1, 1)
            };

            _styleDetailLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _styleDetailValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _styleDetailReason = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = true,
                normal = { textColor = new Color(1f, 0.4f, 0.2f) } // 붉은 주황
            };

            _styleCloseButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                normal = { textColor = Color.white }
            };

            _styleDetailHidden = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Italic,
                normal = { textColor = Color.gray }
            };

            _styleDetailStatus = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        // ======================================================================
        // 튜토리얼 연동 — reflection 없이 _selectedIndex 설정
        // ======================================================================

        /// <summary>TutorialRevengeListIntegration에서 호출 — reflection 우회</summary>
        public void SelectIndex(int index)
        {
            _selectedIndex = index;
        }

        // ======================================================================
        // 헬퍼
        // ======================================================================

        private static string GetNationDisplayName(NationType nation)
        {
            return nation switch
            {
                NationType.East => "동 (East)",
                NationType.West => "서 (West)",
                NationType.South => "남 (South)",
                NationType.North => "북 (North)",
                NationType.Empire => "황제국 (Empire)",
                _ => "미소속"
            };
        }

        private static string GetDifficultyDisplayName(TerritoryDifficulty diff)
        {
            return diff switch
            {
                TerritoryDifficulty.Ring1 => "🟢 쉬움 (Ring 1)",
                TerritoryDifficulty.Ring2 => "🟡 보통 (Ring 2)",
                TerritoryDifficulty.Ring3 => "🟠 어려움 (Ring 3)",
                TerritoryDifficulty.Ring4 => "🔴 매우 어려움 (Ring 4)",
                TerritoryDifficulty.Empire => "👑 최종 (Empire)",
                _ => "알 수 없음"
            };
        }
    }
}