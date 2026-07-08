using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// ⚔️ 아레나 전투 UI — IMGUI 싱글톤.
    /// 전투 중 표시 (3초 라운드 간격), 체력바, 라운드 카운터, 승/패 메시지.
    /// </summary>
    public class ArenaBattleUI : MonoBehaviour
    {
        public static ArenaBattleUI Instance { get; private set; }

        [Header("UI 설정")]
        [SerializeField] private float _panelWidth = 500f;
        [SerializeField] private float _panelHeight = 400f;

        // 상태
        private bool _isOpen = false;
        private float _battleTimer = 0f;
        private bool _resultShown = false;
        private float _resultTimer = 0f;

        // 현재 전투 데이터 (ArenaSystem에서 주입)
        private ArenaCombatantData _fighter;
        private ArenaCombatantData _opponent;
        private ArenaBattleLog _currentLog;

        // GUIStyle 캐싱
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleHPText;
        private GUIStyle _styleRoundText;
        private GUIStyle _styleResultText;
        private GUIStyle _styleRewardText;
        private bool _stylesInitialized;
        private Texture2D _texWhite;

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

        /// <summary>ArenaSystem에서 전투 시작 시 호출</summary>
        public void ShowBattle(ArenaCombatantData fighter, ArenaCombatantData opponent)
        {
            _fighter = fighter;
            _opponent = opponent;
            _currentLog = ArenaSystem.Instance?.GetCurrentBattleLog();
            _battleTimer = 0f;
            _resultShown = false;
            _resultTimer = 0f;
            _isOpen = true;

            Debug.Log($"[ArenaBattleUI] ⚔️ 전투 시작: {fighter.name} vs {opponent.name}");
        }

        /// <summary>전투 업데이트 (코루틴에서 호출)</summary>
        public void UpdateBattle()
        {
            _currentLog = ArenaSystem.Instance?.GetCurrentBattleLog();
            _battleTimer = 0f;

            // 전투 종료 체크
            if (_currentLog != null && _currentLog.rounds != null && _currentLog.rounds.Count > 0)
            {
                var lastRound = _currentLog.rounds[_currentLog.rounds.Count - 1];
                if (lastRound.isOpponentDead || lastRound.isFighterDead)
                {
                    // 종료 조건 만족
                }
            }
        }

        public void Hide()
        {
            _isOpen = false;
            _currentLog = null;
        }

        public bool IsOpen => _isOpen;

        private void Update()
        {
            if (!_isOpen) return;
            if (ArenaSystem.Instance == null) return;

            _battleTimer += Time.deltaTime;

            // 로그 업데이트
            _currentLog = ArenaSystem.Instance.GetCurrentBattleLog();

            // 전투 종료 후 결과 표시
            if (_currentLog != null && _currentLog.rounds != null && _currentLog.rounds.Count > 0)
            {
                var lastRound = _currentLog.rounds[_currentLog.rounds.Count - 1];
                if (lastRound.isOpponentDead || lastRound.isFighterDead
                    || _currentLog.totalRounds >= 5)
                {
                    if (!_resultShown)
                    {
                        _resultShown = true;
                        _resultTimer = 0f;
                    }
                    _resultTimer += Time.deltaTime;

                    // 5초 후 자동 닫힘
                    if (_resultTimer > 5f)
                    {
                        Hide();
                    }
                }
            }

            // ESC로 닫기
            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            EnsureStyles();
            EnsureWhiteTexture();

            // 화면 중앙
            float x = (Screen.width - _panelWidth) / 2f;
            float y = (Screen.height - _panelHeight) / 2f;

            // 배경
            GUI.Box(new Rect(x, y, _panelWidth, _panelHeight), "");

            float cy = y + 10f;
            float contentW = _panelWidth - 20f;

            // 타이틀
            GUI.Label(new Rect(x + 10, cy, contentW, 28), "⚔️ 아레나 전투", _styleTitle);
            cy += 32f;

            // 라운드 카운터
            int roundNum = _currentLog?.rounds?.Count ?? 0;
            string roundStr = roundNum > 0 ? $"— {roundNum}라운드 진행 중 —" : "— 전투 준비 —";
            GUI.Label(new Rect(x + 10, cy, contentW, 24), roundStr, _styleRoundText);
            cy += 30f;

            // ============================================================
            // 상대 NPC 정보 + 체력바
            // ============================================================
            float barWidth = _panelWidth - 80f;
            float barHeight = 20f;
            float labelOffset = 70f;
            _ = labelOffset;

            // 상대 (좌측)
            GUI.Label(new Rect(x + 10, cy, contentW, 22), $"⚔️ {_opponent.name} Lv.{_opponent.level}", _styleLabel);
            cy += 22f;

            // 상대 체력바
            float opponentHPRatio = _opponent.maxHP > 0 ? Mathf.Clamp01(_opponent.currentHP / _opponent.maxHP) : 0f;
            DrawBar(x + 10, cy, barWidth, barHeight, opponentHPRatio, Color.red, new Color(0.3f, 0.1f, 0.1f));
            GUI.Label(new Rect(x + 10 + barWidth + 5, cy, 100, barHeight),
                $"{Mathf.Max(0, _opponent.currentHP):F0}/{_opponent.maxHP:F0}", _styleHPText);
            cy += barHeight + 6f;

            // 상대 공격/방어
            GUI.Label(new Rect(x + 10, cy, contentW, 18),
                $"⚡ 공격력: {_opponent.attack:F1}  🛡️ 방어력: {_opponent.defense:F1}", _styleValue);
            cy += 24f;

            // ============================================================
            // 구분선
            // ============================================================
            cy += 4f;
            DrawLine(x + 10, cy, _panelWidth - 20f, 2f, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            cy += 10f;

            // ============================================================
            // 플레이어/병사 정보 + 체력바
            // ============================================================
            string fighterIcon = _fighter.isPlayer ? "👤" : "🪖";
            GUI.Label(new Rect(x + 10, cy, contentW, 22), $"{fighterIcon} {_fighter.name} Lv.{_fighter.level}", _styleLabel);
            cy += 22f;

            // 플레이어/병사 체력바
            float fighterHPRatio = _fighter.maxHP > 0 ? Mathf.Clamp01(_fighter.currentHP / _fighter.maxHP) : 0f;
            DrawBar(x + 10, cy, barWidth, barHeight, fighterHPRatio, Color.green, new Color(0.1f, 0.3f, 0.1f));
            GUI.Label(new Rect(x + 10 + barWidth + 5, cy, 100, barHeight),
                $"{Mathf.Max(0, _fighter.currentHP):F0}/{_fighter.maxHP:F0}", _styleHPText);
            cy += barHeight + 6f;

            // 플레이어 공격/방어
            GUI.Label(new Rect(x + 10, cy, contentW, 18),
                $"⚡ 공격력: {_fighter.attack:F1}  🛡️ 방어력: {_fighter.defense:F1}", _styleValue);
            cy += 26f;

            // ============================================================
            // 최근 라운드 로그
            // ============================================================
            if (_currentLog?.rounds != null && _currentLog.rounds.Count > 0)
            {
                var lastRound = _currentLog.rounds[_currentLog.rounds.Count - 1];
                string lastLog = $"{lastRound.roundNumber}라운드:\n";
                lastLog += $"💥 {_fighter.name} → {_opponent.name}: {lastRound.fighterDamageDealt:F1} 데미지! ";
                lastLog += $"💥 {_opponent.name} → {_fighter.name}: {lastRound.opponentDamageDealt:F1} 데미지!";

                GUI.Label(new Rect(x + 10, cy, contentW, 36), lastLog, _styleLabel);
                cy += 40f;
            }

            // ============================================================
            // 승/패 메시지 + 보상 팝업
            // ============================================================
            if (_resultShown && _currentLog != null)
            {
                float popupX = x + _panelWidth / 2f - 150f;
                float popupY = y + _panelHeight / 2f - 60f;
                float popupW = 300f;
                float popupH = 130f;

                // 반투명 배경
                Color oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.Box(new Rect(popupX, popupY, popupW, popupH), "");
                GUI.color = oldColor;

                // 결과 메시지
                string resultMsg = _currentLog.isVictory ? "🏆 승리!" : "💀 패배...";
                string rewardMsg = "";

                if (_currentLog.isVictory)
                {
                    rewardMsg = $"💰 +{_currentLog.rewardGold}G 보상!";

                    if (_currentLog.bonusMultiplier > 1f)
                        rewardMsg += $"\n✨ 연승 보너스 x{_currentLog.bonusMultiplier:F1}";

                    if (_currentLog.legendaryReward)
                        rewardMsg += "\n🏆 전설 보상 획득!";
                }

                GUI.Label(new Rect(popupX, popupY + 15f, popupW, 30f),
                    resultMsg, _styleResultText);

                if (!string.IsNullOrEmpty(rewardMsg))
                {
                    GUI.Label(new Rect(popupX, popupY + 50f, popupW, 40f),
                        rewardMsg, _styleRewardText);
                }

                // 닫기 안내
                string closeHint = _resultTimer < 5f
                    ? $"자동으로 닫힙니다... ({(5f - _resultTimer):F1}초)"
                    : "닫는 중...";
                GUI.Label(new Rect(popupX, popupY + popupH - 24f, popupW, 20f),
                    closeHint, _styleLabel);
            }
        }

        // ================================================================
        // 드로우 헬퍼
        // ================================================================

        private void DrawBar(float x, float y, float width, float height, float ratio, Color fillColor, Color bgColor)
        {
            if (_texWhite == null) return;

            var prevColor = GUI.color;

            // 배경
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(x, y, width, height), _texWhite);

            // 채움
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(ratio), height), _texWhite);

            GUI.color = prevColor;
        }

        private void DrawLine(float x, float y, float width, float height, Color color)
        {
            if (_texWhite == null) return;

            var prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, width, height), _texWhite);
            GUI.color = prevColor;
        }

        // ================================================================
        // 스타일
        // ================================================================

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _styleHPText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };

            _styleRoundText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan },
                alignment = TextAnchor.MiddleCenter
            };

            _styleResultText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow },
                alignment = TextAnchor.MiddleCenter
            };

            _styleRewardText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.84f, 0f) }, // 골드색
                alignment = TextAnchor.MiddleCenter
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