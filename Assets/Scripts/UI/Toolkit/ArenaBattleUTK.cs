using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round C — 아레나 전투 HUD (UTK).
    /// 원본: Assets/Scripts/UI/ArenaBattleUI.cs (388줄, IMGUI) — 본 파일은 데이터 경로만
    /// UTKWindowBase 파생으로 이식한 별도 파일. 원본은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측]
    ///  - 전투원: ArenaSystem.OnArenaMatchStart 이벤트 (fighter, opponent) — Subscribe
    ///  - 진행 로그: ArenaSystem.Instance.GetCurrentBattleLog()
    ///  - 전투원: ArenaCombatantData { name, level, maxHP, currentHP, attack, defense, isPlayer }
    ///  - 라운드: ArenaBattleLog.rounds → ArenaRoundLog
    ///    { roundNumber, fighterHPBefore, opponentHPBefore, fighterDamageDealt, opponentDamageDealt,
    ///      isFighterDead, isOpponentDead }
    ///  - 결과: ArenaBattleLog { isVictory, rewardGold, bonusMultiplier, legendaryReward, totalRounds }
    ///
    /// 판넬: ⚔️ 타이틀 + 라운드 카운터, 상대(빨강)/아군(초록) HP 바 + 공/방, 최근 라운드 로그,
    ///       종료 시 승/패 + 보상 팝업 (5초 자동 닫힘). 
    /// 폴링 300ms(schedule.Execute().Every, Pause 정지) — ESC 닫기 (Updater).
    /// [ArenaBattleUTK] 로그.
    /// </summary>
    public class ArenaBattleUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static ArenaBattleUTK _instance;
        public static ArenaBattleUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성 + Updater 컴포넌트 부착 + 이벤트 구독.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new ArenaBattleUTK();
            var go = new GameObject("ArenaBattleUTK_UPD");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().window = _instance;
            if (ArenaSystem.Instance != null)
                ArenaSystem.Instance.OnArenaMatchStart += _instance.OnArenaMatchStart;
            Debug.Log("[ArenaBattleUTK] 인스턴스 생성 (OnArenaMatchStart 구독)");
        }

        /// <summary>팩토리 호출 겸용 (표시 없음, 수동 표시용 아님).</summary>
        public static void Open()
        {
            Ensure();
        }

        /// <summary>토글 — 일반적으로 전투 HUD로 쓰이므로 이벤트 중심. (수동 토글 위해 추가)</summary>
        public static void Toggle()
        {
            Ensure();
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            _instance.Show();
        }

        // ===== 설정 =====
        private const float WinW = 560f;
        private const float WinH = 430f;
        private const long RefreshMs = 300L;

        // ===== 상태 =====
        private ArenaCombatantData _fighter;
        private ArenaCombatantData _opponent;
        private ArenaBattleLog _currentLog;
        private bool _resultShown;
        private float _resultTimer;
        private VisualElement _list;
        private Label _roundLabel;
        private VisualElement _oppHPFill;
        private VisualElement _fgtHPFill;
        private Label _oppHPText;
        private Label _fgtHPText;
        private VisualElement _resultPopup;
        private Label _resultMsg;
        private Label _rewardMsg;
        private Label _closeHint;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private ArenaBattleUTK() : base("⚔️ 아레나 전투", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _list = new VisualElement { name = "BattleContent" };
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 400f;
            style.top = 100f;
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            _resultShown = false;
            _resultTimer = 0f;
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[ArenaBattleUTK] 전투 HUD 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[ArenaBattleUTK] 전투 HUD 닫힘");
        }

        /// <summary>ArenaSystem.OnArenaMatchStart 이벤트 수신 — 전투 시작.</summary>
        public void OnArenaMatchStart(ArenaCombatantData fighter, ArenaCombatantData opponent)
        {
            _fighter = fighter;
            _opponent = opponent;
            _currentLog = ArenaSystem.Instance != null ? ArenaSystem.Instance.GetCurrentBattleLog() : null;
            _resultShown = false;
            _resultTimer = 0f;
            if (!IsOpen) Show(); else RefreshDisplay();
            Debug.Log($"[ArenaBattleUTK] ⚔️ 전투 시작: {fighter.name} vs {opponent.name}");
        }

        // ===== 폴링 루프 =====

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                _currentLog = ArenaSystem.Instance != null ? ArenaSystem.Instance.GetCurrentBattleLog() : null;
                UpdateBattleState();
                RefreshDisplay();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        // ===== 전투 상태 갱신 (원본 실측: 반복 표시 기준) =====

        private void UpdateBattleState()
        {
            if (_currentLog == null || _currentLog.rounds == null || _currentLog.rounds.Count == 0)
                return;

            var lastRound = _currentLog.rounds[_currentLog.rounds.Count - 1];
            bool battleOver = lastRound.isOpponentDead || lastRound.isFighterDead
                              || _currentLog.totalRounds >= 5;

            if (!battleOver) return;

            if (!_resultShown)
            {
                _resultShown = true;
                _resultTimer = 0f;
            }
            _resultTimer += Time.deltaTime;

            // 5초 후 자동 닫힘
            if (_resultTimer > 5f)
                Hide();
        }

        // ===== 렌더링 =====

        private void RefreshDisplay()
        {
            _list.Clear();

            // 라운드 카운터
            int roundNum = _currentLog != null && _currentLog.rounds != null ? _currentLog.rounds.Count : 0;
            string roundStr = roundNum > 0 ? $"— {roundNum}라운드 진행 중 —" : "— 전투 준비 —";
            _roundLabel = new Label(roundStr);
            _roundLabel.style.fontSize = 15f;
            _roundLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _roundLabel.style.color = new StyleColor(UTKColor.AccentMagic);
            _roundLabel.style.alignSelf = Align.Center;
            _roundLabel.style.marginBottom = 4f;
            _list.Add(_roundLabel);

            if (_fighter.name == null) // 아직 전투 데이터 없음
            {
                _list.Add(Lbl("(대기 중 — 전투가 시작되면 표시됩니다)", UTKColor.TextSecondary));
                return;
            }

            // 상대 (빨강)
            _list.Add(Lbl($"⚔️ {_opponent.name} Lv.{_opponent.level}"));
            float oppRatio = _opponent.maxHP > 0 ? Mathf.Clamp01(_opponent.currentHP / _opponent.maxHP) : 0f;
            var oppBar = BuildBar(oppRatio, UTKColor.HealthRed, new Color(0.3f, 0.1f, 0.1f, 1f));
            _oppHPText = barText(oppBar);
            _list.Add(oppBar);
            _oppHPText.text = $"{Mathf.Max(0, _opponent.currentHP):F0}/{_opponent.maxHP:F0}";
            _list.Add(Lbl($"⚡ 공격력: {_opponent.attack:F1}  🛡️ 방어력: {_opponent.defense:F1}", UTKColor.TextSecondary));
            _list.Add(new VisualElement { style = { height = 6f } });

            // 아군 (초록)
            string fighterIcon = _fighter.isPlayer ? "👤" : "🪖";
            _list.Add(Lbl($"{fighterIcon} {_fighter.name} Lv.{_fighter.level}"));
            float fgtRatio = _fighter.maxHP > 0 ? Mathf.Clamp01(_fighter.currentHP / _fighter.maxHP) : 0f;
            var fgtBar = BuildBar(fgtRatio, UTKColor.GuildGreen, new Color(0.1f, 0.3f, 0.1f, 1f));
            _fgtHPText = barText(fgtBar);
            _list.Add(fgtBar);
            _fgtHPText.text = $"{Mathf.Max(0, _fighter.currentHP):F0}/{_fighter.maxHP:F0}";
            _list.Add(Lbl($"⚡ 공격력: {_fighter.attack:F1}  🛡️ 방어력: {_fighter.defense:F1}", UTKColor.TextSecondary));
            _list.Add(new VisualElement { style = { height = 6f } });

            // 최근 라운드 로그
            if (_currentLog != null && _currentLog.rounds != null && _currentLog.rounds.Count > 0)
            {
                var lr = _currentLog.rounds[_currentLog.rounds.Count - 1];
                string lastLog = $"{lr.roundNumber}라운드:\n💥 {_fighter.name} → {_opponent.name}: {lr.fighterDamageDealt:F1} 데미지!\n💥 {_opponent.name} → {_fighter.name}: {lr.opponentDamageDealt:F1} 데미지!";
                var ll = Lbl(lastLog, UTKColor.TextPrimary, 12f);
                ll.style.whiteSpace = WhiteSpace.Normal;
                _list.Add(ll);
            }

            // 결과 팝업
            if (_resultShown && _currentLog != null)
                DrawResultPopup();
        }

        // ---------- 결과 팝업 ----------

        private void DrawResultPopup()
        {
            _list.Add(new VisualElement { style = { height = 6f } });

            var panel = new VisualElement { name = "BattleResultPopup" };
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
            panel.style.borderTopWidth = 2f;
            panel.style.borderBottomWidth = 2f;
            panel.style.borderLeftWidth = 2f;
            panel.style.borderRightWidth = 2f;
            panel.style.borderTopColor = UTKColor.BorderGold;
            panel.style.borderBottomColor = UTKColor.BorderGold;
            panel.style.borderLeftColor = UTKColor.BorderGold;
            panel.style.borderRightColor = UTKColor.BorderGold;
            panel.style.borderTopLeftRadius = 6f;
            panel.style.borderTopRightRadius = 6f;
            panel.style.borderBottomLeftRadius = 6f;
            panel.style.borderBottomRightRadius = 6f;
            panel.style.paddingLeft = 12f;
            panel.style.paddingRight = 12f;
            panel.style.paddingTop = 10f;
            panel.style.paddingBottom = 10f;

            string resultMsg = _currentLog.isVictory ? "🏆 승리!" : "💀 패배...";
            _resultMsg = new Label(resultMsg);
            _resultMsg.style.fontSize = 26f;
            _resultMsg.style.unityFontStyleAndWeight = FontStyle.Bold;
            _resultMsg.style.color = new StyleColor(_currentLog.isVictory ? UTKColor.HoverGold : UTKColor.HealthRed);
            _resultMsg.style.alignSelf = Align.Center;
            panel.Add(_resultMsg);

            string reward = "";
            if (_currentLog.isVictory)
            {
                reward = $"💰 +{_currentLog.rewardGold}G 보상!";
                if (_currentLog.bonusMultiplier > 1f)
                    reward += $"\n✨ 연승 보너스 x{_currentLog.bonusMultiplier:F1}";
                if (_currentLog.legendaryReward)
                    reward += "\n🏆 전설 보상 획득!";
            }
            if (!string.IsNullOrEmpty(reward))
            {
                _rewardMsg = new Label(reward);
                _rewardMsg.style.fontSize = 15f;
                _rewardMsg.style.color = new StyleColor(UTKColor.TextPrimary);
                _rewardMsg.style.whiteSpace = WhiteSpace.Normal;
                _rewardMsg.style.alignSelf = Align.Center;
                panel.Add(_rewardMsg);
            }

            float remain = Mathf.Max(0f, 5f - _resultTimer);
            _closeHint = new Label($"자동으로 닫힙니다... ({remain:F1}초)");
            _closeHint.style.fontSize = 11f;
            _closeHint.style.color = new StyleColor(UTKColor.TextSecondary);
            _closeHint.style.alignSelf = Align.Center;
            _closeHint.style.marginTop = 4f;
            panel.Add(_closeHint);

            _list.Add(panel);
            _resultPopup = panel;
        }

        // ---------- 바 / 라벨 헬퍼 ----------

        private static Label barText(VisualElement bar)
        {
            var text = new Label("");
            text.style.alignSelf = Align.FlexStart;
            text.style.paddingLeft = 4f;
            text.style.fontSize = 11f;
            text.style.color = new StyleColor(UTKColor.TextPrimary);
            bar.Add(text);
            return text;
        }

        private static VisualElement BuildBar(float ratio, Color fill, Color bg)
        {
            var bar = new VisualElement { name = "HPBar" };
            bar.style.height = 18f;
            bar.style.marginTop = 2f;
            bar.style.marginBottom = 2f;
            bar.style.backgroundColor = bg;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.position = Position.Relative;

            var fillEl = new VisualElement { name = "Fill" };
            fillEl.style.height = new Length(100f, LengthUnit.Percent);
            fillEl.style.width = new Length(Mathf.Clamp01(ratio) * 100f, LengthUnit.Percent);
            fillEl.style.backgroundColor = new StyleColor(fill);
            bar.Add(fillEl);

            return bar;
        }

        private static Label Lbl(string text, Color? color = null, float size = 13f)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color ?? UTKColor.TextPrimary);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        // ===== ESC 닫기용 Updater =====

        private class Updater : MonoBehaviour
        {
            public ArenaBattleUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && window != null && kb.escapeKey.wasPressedThisFrame && window.IsOpen)
                    window.Close();
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    if (ArenaSystem.Instance != null)
                        ArenaSystem.Instance.OnArenaMatchStart -= window.OnArenaMatchStart;
                    window.StopRefreshLoop();
                    window.RemoveFromHierarchy();
                }
            }
        }
    }
}