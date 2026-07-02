using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// ⚔️ 아레나 메뉴 UI — IMGUI 싱글톤.
    /// E키로 오픈, 직접 싸우기/병사 출전 모드 선택, 전투 결과 표시.
    /// </summary>
    public class ArenaMenuUI : MonoBehaviour
    {
        public static ArenaMenuUI Instance { get; private set; }

        [Header("UI 설정")]
        [SerializeField] private float _panelWidth = 600f;
        [SerializeField] private float _panelHeight = 500f;

        // 상태
        private bool _isOpen = false;
        private ArenaMenuTab _currentTab = ArenaMenuTab.Main;

        // 병사 목록 스크롤
        private Vector2 _guardScrollPos;
        private int _selectedGuardIndex = -1;

        // 전투 로그 표시
        private ArenaBattleLog _lastLog;
        private Vector2 _logScrollPos;
        private float _logTimer = 0f;

        // GUIStyle 캐싱
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleButton;
        private GUIStyle _styleMsg;
        private GUIStyle _styleWin;
        private GUIStyle _styleLose;
        private GUIStyle _styleLogText;
        private bool _stylesInitialized;
        private Texture2D _texWhite;

        // 캐시된 텍스트
        private string _cachedStreakText;
        private string _cachedFeeText;
        private string _cachedLevelText;
        private int _cachedStreak = -1;
        private int _cachedFee = -1;
        private int _cachedLevel = -1;

        private enum ArenaMenuTab
        {
            Main,
            GuardSelect,
            BattleLog
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_texWhite != null)
            {
                Destroy(_texWhite);
                _texWhite = null;
            }
        }

        // ===== 공개 API =====

        public void Show()
        {
            _isOpen = true;
            _currentTab = ArenaMenuTab.Main;
            _selectedGuardIndex = -1;
            _guardScrollPos = Vector2.zero;
            _logScrollPos = Vector2.zero;

            // 최근 전투 로그 확인
            _lastLog = ArenaSystem.Instance?.GetCurrentBattleLog();
            if (_lastLog != null)
            {
                _currentTab = ArenaMenuTab.BattleLog;
                _logTimer = 0f;
            }

            // 캐시 초기화
            _cachedStreak = -1;
            _cachedFee = -1;
            _cachedLevel = -1;

            Debug.Log("[ArenaMenuUI] 🎪 아레나 메뉴 열림");
        }

        public void Hide()
        {
            _isOpen = false;
            Debug.Log("[ArenaMenuUI] 🎪 아레나 메뉴 닫힘");
        }

        public bool IsOpen => _isOpen;

        private void Update()
        {
            if (!_isOpen) return;

            // ESC로 닫기
            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_currentTab == ArenaMenuTab.Main)
                {
                    Hide();
                }
                else
                {
                    _currentTab = ArenaMenuTab.Main;
                }
            }

            // 전투 로그 타이머
            if (_currentTab == ArenaMenuTab.BattleLog && _lastLog != null)
            {
                _logTimer += Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            // ArenaSystem이 없으면 표시 불가
            if (ArenaSystem.Instance == null) return;

            EnsureStyles();
            EnsureWhiteTexture();

            // 패널 중앙 배치
            float x = (Screen.width - _panelWidth) / 2f;
            float y = (Screen.height - _panelHeight) / 2f;

            // 배경 박스
            GUI.Box(new Rect(x, y, _panelWidth, _panelHeight), "");

            // 탭별 렌더링
            switch (_currentTab)
            {
                case ArenaMenuTab.Main:
                    DrawMainTab(x, y);
                    break;
                case ArenaMenuTab.GuardSelect:
                    DrawGuardSelectTab(x, y);
                    break;
                case ArenaMenuTab.BattleLog:
                    DrawBattleLogTab(x, y);
                    break;
            }
        }

        // ================================================================
        // 메인 탭
        // ================================================================

        private void DrawMainTab(float panelX, float panelY)
        {
            float cy = panelY + 10f;
            float contentW = _panelWidth - 20f;

            // 타이틀
            GUI.Label(new Rect(panelX + 10, cy, contentW, 30), "⚔️ 아레나", _styleTitle);
            cy += 35f;

            // 현재 연승 기록
            var arena = ArenaSystem.Instance;
            if (arena == null) return;

            int streak = arena.CurrentWinStreak;
            int best = arena.BestWinStreak;
            int total = arena.TotalWins;

            if (streak != _cachedStreak)
            {
                _cachedStreak = streak;
                _cachedStreakText = $"🔥 현재 연승: {streak}연승  |  최고: {best}연승  |  총 승리: {total}회";
            }

            GUI.Label(new Rect(panelX + 10, cy, contentW, 22), _cachedStreakText, _styleValue);
            cy += 28f;

            // 참가비
            int fee = arena.GetCurrentEntryFee();
            if (fee != _cachedFee)
            {
                _cachedFee = fee;
                _cachedFeeText = $"💰 참가비: {fee}G";
            }
            GUI.Label(new Rect(panelX + 10, cy, contentW, 22), _cachedFeeText, _styleLabel);
            cy += 24f;

            // 연승 보너스 정보
            float bonus = arena.GetWinStreakMultiplier();
            string bonusInfo = bonus > 1f
                ? $"✨ 현재 연승 보너스: x{bonus:F1}"
                : "⚡ 2연승 이상 시 보너스 적용!";
            GUI.Label(new Rect(panelX + 10, cy, contentW, 22), bonusInfo, _styleLabel);
            cy += 24f;

            // 참가 조건
            int playerLevel = PlayerStats.Instance?.Level ?? 1;
            if (playerLevel != _cachedLevel)
            {
                _cachedLevel = playerLevel;
                _cachedLevelText = $"👤 플레이어 Lv.{playerLevel}";
            }
            GUI.Label(new Rect(panelX + 10, cy, contentW, 22), _cachedLevelText, _styleLabel);
            cy += 30f;

            // 참가 가능 여부
            string checkMsg = arena.CanParticipate();
            if (checkMsg != null)
            {
                GUI.Label(new Rect(panelX + 10, cy, contentW, 22), $"⚠️ {checkMsg}", _styleMsg);
                cy += 28f;
            }

            // 버튼 영역
            float btnY = panelY + _panelHeight - 100f;
            float btnW = (_panelWidth - 40f) / 2f;

            bool canParticipate = checkMsg == null && !arena.IsInBattle;

            // 모드 1: 직접 싸우기
            GUI.enabled = canParticipate;
            if (GUI.Button(new Rect(panelX + 10, btnY, btnW, 40), "⚔️ 직접 싸우기", _styleButton))
            {
                if (arena != null && checkMsg == null)
                {
                    StartCoroutine(arena.StartPlayerFight());
                    _currentTab = ArenaMenuTab.BattleLog;
                }
            }

            // 모드 2: 병사/용병 출전
            if (GUI.Button(new Rect(panelX + 20 + btnW, btnY, btnW, 40), "🪖 병사/용병 출전", _styleButton))
            {
                if (canParticipate)
                {
                    _currentTab = ArenaMenuTab.GuardSelect;
                    _selectedGuardIndex = -1;
                    _guardScrollPos = Vector2.zero;
                }
            }
            GUI.enabled = true;

            // 닫기 버튼
            float closeBtnY = panelY + _panelHeight - 45f;
            if (GUI.Button(new Rect(panelX + _panelWidth / 2f - 60f, closeBtnY, 120f, 30f), "🔙 닫기", _styleButton))
            {
                Hide();
            }
        }

        // ================================================================
        // 병사 선택 탭
        // ================================================================

        private void DrawGuardSelectTab(float panelX, float panelY)
        {
            float cy = panelY + 10f;
            float contentW = _panelWidth - 20f;

            // 타이틀
            GUI.Label(new Rect(panelX + 10, cy, contentW, 30), "🪖 병사/용병 선택", _styleTitle);
            cy += 35f;

            // 병사 목록 가져오기
            List<GuardPlaceholder> guards = GetAvailableGuards();
            MercenaryInstance[] mercs = GetAvailableMercenaries();

            float listY = cy;
            float listH = _panelHeight - 180f;

            // 스크롤뷰
            GUI.BeginGroup(new Rect(panelX + 10, listY, contentW, listH));

            float itemH = 50f;
            int totalItems = guards.Count + mercs.Length;
            float viewH = totalItems * itemH + 10f;

            _guardScrollPos = GUI.BeginScrollView(
                new Rect(0, 0, contentW, listH),
                _guardScrollPos,
                new Rect(0, 0, contentW - 20f, viewH)
            );

            float iy = 5f;
            int index = 0;

            // 병사 목록
            if (guards.Count == 0 && mercs.Length == 0)
            {
                GUI.Label(new Rect(0, iy, contentW - 20f, 24f), "출전 가능한 병사나 용병이 없습니다.", _styleMsg);
                iy += 30f;
            }
            else
            {
                // 병사 표시
                for (int i = 0; i < guards.Count; i++)
                {
                    var guard = guards[i];
                    if (guard == null) continue;

                    bool isSelected = (_selectedGuardIndex == index);
                    string label = $"🪖 {guard.GuardName} Lv.{guard.Level}  ❤️{guard.HP:F0}/{guard.MaxHP:F0}";

                    Rect itemRect = new Rect(0, iy, contentW - 20f, itemH - 2f);
                    GUI.Box(itemRect, "", isSelected ? _styleButton : _styleLabel);

                    if (GUI.Button(itemRect, label, _styleLabel))
                    {
                        _selectedGuardIndex = index;
                    }

                    iy += itemH;
                    index++;
                }

                // 용병 표시
                for (int i = 0; i < mercs.Length; i++)
                {
                    var merc = mercs[i];
                    if (!merc.isAlive) continue;

                    bool isSelected = (_selectedGuardIndex == index);
                    string label = $"⭐ {merc.data.mercenaryName} {merc.data.GradeStars}  ❤️{merc.currentHP:F0}/{merc.data.maxHP:F0}";

                    Rect itemRect = new Rect(0, iy, contentW - 20f, itemH - 2f);
                    GUI.Box(itemRect, "", isSelected ? _styleButton : _styleLabel);

                    if (GUI.Button(itemRect, label, _styleLabel))
                    {
                        _selectedGuardIndex = index;
                    }

                    iy += itemH;
                    index++;
                }
            }

            GUI.EndScrollView();
            GUI.EndGroup();

            // 하단 버튼
            float btnY = panelY + _panelHeight - 80f;
            float btnW = 150f;

            // 출전 버튼 (선택된 경우)
            bool canFight = _selectedGuardIndex >= 0 && _selectedGuardIndex < totalItems;
            GUI.enabled = canFight;
            if (GUI.Button(new Rect(panelX + 10, btnY, btnW, 35f), "⚔️ 출전!", _styleButton))
            {
                StartFightWithSelected(guards, mercs);
            }
            GUI.enabled = true;

            // 뒤로가기
            if (GUI.Button(new Rect(panelX + _panelWidth - btnW - 10, btnY, btnW, 35f), "🔙 뒤로", _styleButton))
            {
                _currentTab = ArenaMenuTab.Main;
            }
        }

        private List<GuardPlaceholder> GetAvailableGuards()
        {
            var result = new List<GuardPlaceholder>();
            if (GuardManager.Instance == null) return result;

            var guards = GuardManager.Instance.GetAllPlayerGuards();
            foreach (var g in guards)
            {
                if (g != null && g.IsAlive)
                    result.Add(g);
            }
            return result;
        }

        private MercenaryInstance[] GetAvailableMercenaries()
        {
            if (MercenaryManager.Instance == null) return new MercenaryInstance[0];
            return MercenaryManager.Instance.GetHiredMercenaries();
        }

        private void StartFightWithSelected(List<GuardPlaceholder> guards, MercenaryInstance[] mercs)
        {
            var arena = ArenaSystem.Instance;
            if (arena == null) return;

            // 병사인지 용병인지 판별
            int guardCount = guards.Count;
            if (_selectedGuardIndex < guardCount)
            {
                // 병사 출전
                var guard = guards[_selectedGuardIndex];
                if (guard != null)
                {
                    StartCoroutine(arena.StartGuardFight(guard));
                }
            }
            else
            {
                // 용병 출전
                int mercIndex = _selectedGuardIndex - guardCount;
                if (mercIndex >= 0 && mercIndex < mercs.Length)
                {
                    StartCoroutine(arena.StartMercenaryFight(mercs[mercIndex]));
                }
            }

            _currentTab = ArenaMenuTab.BattleLog;
        }

        // ================================================================
        // 전투 로그 탭
        // ================================================================

        private void DrawBattleLogTab(float panelX, float panelY)
        {
            float cy = panelY + 10f;
            float contentW = _panelWidth - 20f;

            if (_lastLog == null)
            {
                GUI.Label(new Rect(panelX + 10, cy, contentW, 30), "전투 기록이 없습니다.", _styleMsg);
                return;
            }

            // 타이틀
            string title = _lastLog.isVictory ? "⚔️ 전투 결과 — 승리!" : "⚔️ 전투 결과 — 패배...";
            GUI.Label(new Rect(panelX + 10, cy, contentW, 30), title, _lastLog.isVictory ? _styleWin : _styleLose);
            cy += 35f;

            // 요약 정보
            GUI.Label(new Rect(panelX + 10, cy, contentW, 22),
                $"🥊 {_lastLog.fighterName} vs {_lastLog.opponentName}", _styleLabel);
            cy += 24f;

            GUI.Label(new Rect(panelX + 10, cy, contentW, 22),
                $"📊 총 {_lastLog.totalRounds}라운드 진행", _styleLabel);
            cy += 24f;

            if (_lastLog.isVictory)
            {
                GUI.Label(new Rect(panelX + 10, cy, contentW, 22),
                    $"💰 보상: {_lastLog.rewardGold}G", _styleValue);
                cy += 22f;

                if (_lastLog.bonusMultiplier > 1f)
                {
                    GUI.Label(new Rect(panelX + 10, cy, contentW, 22),
                        $"✨ 연승 보너스 x{_lastLog.bonusMultiplier:F1} 적용!", _styleMsg);
                    cy += 22f;
                }

                if (_lastLog.legendaryReward)
                {
                    GUI.Label(new Rect(panelX + 10, cy, contentW, 26),
                        "🏆 전설 보상: 아레나 챔피언 토큰 획득!", _styleWin);
                    cy += 28f;
                }
            }
            cy += 10f;

            // 라운드별 로그 (스크롤)
            float logY = cy;
            float logH = _panelHeight - cy - 80f;

            GUI.BeginGroup(new Rect(panelX + 10, logY, contentW, logH));

            float logViewH = _lastLog.rounds.Count * 60f + 10f;
            _logScrollPos = GUI.BeginScrollView(
                new Rect(0, 0, contentW, logH),
                _logScrollPos,
                new Rect(0, 0, contentW - 20f, logViewH)
            );

            float liy = 5f;
            for (int i = 0; i < _lastLog.rounds.Count; i++)
            {
                var round = _lastLog.rounds[i];
                string logText = $"--- {round.roundNumber}라운드 ---\n";
                logText += $"💥 {_lastLog.fighterName}: {round.fighterDamageDealt:F1} 데미지! (HP: {round.fighterHPBefore:F1} → {Mathf.Max(0, round.fighterHPBefore - round.opponentDamageDealt):F1})\n";
                logText += $"💥 {_lastLog.opponentName}: {round.opponentDamageDealt:F1} 데미지! (HP: {round.opponentHPBefore:F1} → {Mathf.Max(0, round.opponentHPBefore - round.fighterDamageDealt):F1})";

                if (round.isOpponentDead)
                    logText += "\n💀 상대 쓰러짐!";
                if (round.isFighterDead)
                    logText += "\n💀 아군 쓰러짐...";

                GUI.Label(new Rect(0, liy, contentW - 20f, 55f), logText, _styleLogText);
                liy += 60f;
            }

            GUI.EndScrollView();
            GUI.EndGroup();

            // 닫기 버튼
            float closeBtnY = panelY + _panelHeight - 45f;
            if (GUI.Button(new Rect(panelX + _panelWidth / 2f - 60f, closeBtnY, 120f, 30f), "🔙 닫기", _styleButton))
            {
                _currentTab = ArenaMenuTab.Main;
                _lastLog = null;
            }
        }

        // ================================================================
        // 스타일/텍스처
        // ================================================================

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow },
                alignment = TextAnchor.MiddleLeft
            };

            _styleButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow }
            };

            _styleMsg = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                normal = { textColor = Color.cyan }
            };

            _styleWin = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.green },
                alignment = TextAnchor.MiddleCenter
            };

            _styleLose = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.red },
                alignment = TextAnchor.MiddleCenter
            };

            _styleLogText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.85f) },
                wordWrap = true
            };
        }

        private void EnsureWhiteTexture()
        {
            if (_texWhite == null)
            {
                _texWhite = new Texture2D(1, 1);
                _texWhite.SetPixel(0, 0, Color.white);
                _texWhite.Apply();
            }
        }
    }
}