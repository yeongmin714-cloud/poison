using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;      // PlayerInventory
using ProjectName.Systems;   // LockpickingSystem, LockedDoor

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round B — 자물쇠 따기 미니게임 (UTKWindowBase 파생).
    /// 원본: Assets/Scripts/UI/LockpickingUI.cs (524줄, IMGUI) — 본 파일은 그것의 표시부를
    /// UTK로 재현하고 진행/판정은 원본 LockpickingSystem 정적 API를 그대로 호출한다.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [실측 API — 원본 LockpickingUI 경로 동일]
    ///   진행: LockpickingSystem.StartSession(difficulty, pickGrade, locationId)
    ///         LockpickingSystem.UpdateSession(deltaTime)
    ///         LockpickingSystem.AdjustCurrentPin(±0.05f) / TrySetCurrentPin()
    ///         LockpickingSystem.OnSessionEnded (+= / -=, Action<LockpickingSession,bool>)
    ///         LockpickingSystem.AbortSession() / GlobalConsecutiveFails / ResetConsecutiveFails()
    ///   상태: LockpickingSystem.CurrentSession.difficulty/pins[]/timeRemaining/currentPinIndex...
    ///   내구도: LockpickingSystem.GetMaxDurability(PickGrade)
    ///   열기: LockedDoor.OnLockpickRequested  (원본은 이 이벤트를 구독해 열림)
    ///
    /// 폴링: schedule.Execute().Every(50ms) → Pause 정지. 키 입력은 DontDestroyOnLoad
    /// Updater(MonoBehaviour)를 통해 처리 (폴링으로는 홀드키 감지 부정확). [LockUTK] 로그.
    /// </summary>
    public class LockpickingUTK : UTKWindowBase
    {
        private static LockpickingUTK _instance;
        private static Updater _updater;

        private const float WinW = 520f;
        private const float WinH = 470f;
        private const long TickMs = 50L;

        // 상태 (원본 LockpickingUI의 _currentPickGrade/_currentDurability 대응)
        private LockpickingSystem.PickGrade _pickGrade = LockpickingSystem.PickGrade.Basic;
        private int _durability;
        private int _maxDurability;
        private string _locationId = "";

        // 렌더 레퍼런스
        private Label _durLabel;
        private Label _timeLabel;
        private Label _infoLabel;
        private VisualElement _pinsRow;
        private readonly PinColumn[] _pinColumns = new PinColumn[7];
        private IVisualElementScheduledItem _tickTask;

        /// <summary>마스터 키 보유 여부 (외부 주입, 원본과 동일).</summary>
        public static bool HasMasterKey;

        public static LockpickingUTK Instance => _instance;

        private LockpickingUTK() : base("🔒 자물쇠 따기", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 10f;
            _content.style.paddingTop = 6f;

            _infoLabel = MakeText("🔒 난이도 —", UTKColor.TextSecondary);
            _infoLabel.style.fontSize = 14f;
            _content.Add(_infoLabel);

            _durLabel = MakeText("❤️ 내구도: -/-", UTKColor.GuildGreen);
            _durLabel.style.fontSize = 14f;
            _durLabel.style.marginTop = 4f;
            _content.Add(_durLabel);

            _timeLabel = MakeText("⏱️ 남은 시간: -초", UTKColor.TextSecondary);
            _timeLabel.style.fontSize = 14f;
            _timeLabel.style.marginTop = 2f;
            _content.Add(_timeLabel);

            // 픽 도구 선택 행
            var gradeRow = new VisualElement();
            gradeRow.style.flexDirection = FlexDirection.Row;
            gradeRow.style.marginTop = 8f;
            _content.Add(gradeRow);
            var basic = UTKButton.Create("기본(내구5)", () => SelectGrade(LockpickingSystem.PickGrade.Basic), UTKButton.Variant.Secondary);
            var adv = UTKButton.Create("고급(내구10)", () => SelectGrade(LockpickingSystem.PickGrade.Advanced), UTKButton.Variant.Secondary);
            var master = UTKButton.Create("마스터(내구20)", () => SelectGrade(LockpickingSystem.PickGrade.Master), UTKButton.Variant.Secondary);
            basic.style.flexGrow = 1f; adv.style.flexGrow = 1f; master.style.flexGrow = 1f;
            basic.style.marginRight = 4f; adv.style.marginRight = 4f;
            gradeRow.Add(basic); gradeRow.Add(adv); gradeRow.Add(master);

            // 핀 표시 영역
            _pinsRow = new VisualElement();
            _pinsRow.style.flexDirection = FlexDirection.Row;
            _pinsRow.style.justifyContent = Justify.Center;
            _pinsRow.style.marginTop = 10f;
            _pinsRow.style.height = 190f;
            _content.Add(_pinsRow);
            for (int i = 0; i < 7; i++)
            {
                var col = new PinColumn();
                _pinColumns[i] = col;
                col.style.marginRight = (i < 6) ? 8f : 0f;
                _pinsRow.Add(col);
            }

            var ctrl = MakeText("W/S: 핀 조절 | Space: 고정 | ESC: 취소 | ✕: 닫기", UTKColor.TextSecondary);
            ctrl.style.fontSize = 13f;
            ctrl.style.marginTop = 10f;
            _content.Add(ctrl);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 60f;
            style.top = 60f;
        }

        // ===== 공개 API =====

        /// <summary>싱글톤 + 키 입력 Updater 보장 (멱등).</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new LockpickingUTK();
            var go = new GameObject("LockpickingUTK_UPD");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
            _updater.window = _instance;
            // LockedDoor E키 요청을 자물쇠 창이 구독 (원본 LockpickingUI.Awake 동일 경로)
            LockedDoor.OnLockpickRequested += _instance.OnLockpickRequested;
            Debug.Log("[LockUTK] 인스턴스 생성 — LockedDoor 요청 구독");
        }

        /// <summary>자물쇠 따기 창 오픈 (난이도 직접 지정). LockedDoor 요청/외부 호출 겸용.</summary>
        public static void Open(string locationId, LockpickingSystem.LockDifficulty difficulty)
        {
            Ensure();
            if (_instance == null) return;
            _instance.StartSession(locationId, difficulty);
        }

        /// <summary>스택 최상단 닫기 — 세션 중단 (원본 Close() 동일).</summary>
        public static void CloseUI() => _instance?.AbortSession();

        /// <summary>토글 — IsOpen 분기 필수.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.AbortSession(); return; }
            // 닫힘 상태 → 자동 재오픈 없음 (연결 위치 ID 필요하므로 Open() 사용 권장)
            Debug.Log("[LockUTK] Toggle: 닫힘 — Open(locationId, difficulty) 호출 필요");
        }

        // ===== 세션 흐름 (원본 API 실측) =====

        private void StartSession(string locationId, LockpickingSystem.LockDifficulty difficulty)
        {
            if (IsOpen) { Debug.LogWarning("[LockUTK] 이미 열려있음"); return; }

            _locationId = locationId;

            // 마스터 키 보유 → 미니게임 스킵 (원본 Open() 동일 경로)
            if (HasMasterKey)
            {
                Debug.Log("[LockUTK] 마스터 키 보유 → 미니게임 스킵");
                LockpickingSystem.ResetConsecutiveFails();
                LockpickingSystem.MasterKeyOpen(difficulty, locationId);
                if (PlayerInventory.Instance != null)
                    PlayerInventory.Instance.RemoveItem("lockpick_master", 1);
                HasMasterKey = false;
                OnSessionEndedHandler(null, true);
                return;
            }

            LockpickingSystem.OnSessionEnded += OnSessionEndedHandler;
            LockpickingSystem.StartSession(difficulty, _pickGrade, locationId);
            _durability = LockpickingSystem.GetMaxDurability(_pickGrade);
            _maxDurability = _durability;
            RefreshInfo(session: LockpickingSystem.CurrentSession);

            Show();
            Debug.Log($"[LockUTK] 자물쇠 따기 시작: 위치={locationId}, 난이도={difficulty}, 픽={_pickGrade}");
        }

        private void AbortSession()
        {
            if (!IsOpen) return;
            LockpickingSystem.OnSessionEnded -= OnSessionEndedHandler;
            LockpickingSystem.AbortSession();
            Hide();
            Debug.Log("[LockUTK] 자물쇠 세션 중단/닫기");
        }

        private void OnSessionEndedHandler(LockpickingSystem.LockpickingSession session, bool success)
        {
            LockpickingSystem.OnSessionEnded -= OnSessionEndedHandler;
            Debug.Log(success
                ? $"[LockUTK] 🔓 문 열림! 위치={_locationId}"
                : $"[LockUTK] ❌ 자물쇠 따기 실패! 위치={_locationId}");
            if (IsOpen) Hide();
        }

        private void SelectGrade(LockpickingSystem.PickGrade grade)
        {
            _pickGrade = grade;
            if (IsOpen)
            {
                _durability = LockpickingSystem.GetMaxDurability(grade);
                _maxDurability = _durability;
                _durability = _maxDurability;
            }
            Debug.Log($"[LockUTK] 픽 도구 변경: {grade}");
            RefreshInfo(LockpickingSystem.CurrentSession);
        }

        // ===== 틱 갱신 (schedule.Execute().Every → Pause) =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            StartTick();
            RefreshInfo(LockpickingSystem.CurrentSession);
        }

        public override void Hide()
        {
            StopTick();
            base.Hide();
        }

        private void StartTick()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshInfo(LockpickingSystem.CurrentSession);
            }).Every(TickMs);
        }

        private void StopTick()
        {
            if (_tickTask != null)
            {
                _tickTask.Pause();
                _tickTask = null;
            }
        }

        private void RefreshInfo(LockpickingSystem.LockpickingSession session)
        {
            _infoLabel.text = "🔒 " + GetDifficultyDisplayName(session?.difficulty ?? LockpickingSystem.LockDifficulty.Easy) +
                              $" | 위치: {_locationId}";
            _durLabel.text = $"❤️ 내구도: {_durability}/{_maxDurability}";
            _timeLabel.text = session != null ? $"⏱️ 남은 시간: {session.timeRemaining:F1}초" : "⏱️ 남은 시간: -초";

            bool hasSession = session != null && session.isActive;
            for (int i = 0; i < 7; i++)
            {
                var col = _pinColumns[i];
                bool visible = hasSession && i < session.pins.Length;
                col.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                var pin = session.pins[i];
                col.Apply(pin, i == session.currentPinIndex);
            }
        }

        // ===== 핀 열 컨트롤 =====

        private class PinColumn : VisualElement
        {
            private readonly VisualElement _bg;
            private readonly VisualElement _fill;
            private readonly VisualElement _target;
            private readonly VisualElement _sel;
            private readonly Label _num;

            public PinColumn()
            {
                style.width = 52f;
                style.height = 160f;
                style.position = Position.Relative;

                _bg = new VisualElement();
                _bg.style.position = Position.Absolute;
                _bg.style.left = 0; _bg.style.top = 0; _bg.style.right = 0; _bg.style.bottom = 0;
                _bg.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f, 0.9f));
                _bg.style.borderTopWidth = 1; _bg.style.borderBottomWidth = 1;
                _bg.style.borderLeftWidth = 1; _bg.style.borderRightWidth = 1;
                _bg.style.borderTopColor = _bg.style.borderBottomColor =
                    _bg.style.borderLeftColor = _bg.style.borderRightColor = new StyleColor(UTKColor.IronLine);
                Add(_bg);

                _sel = new VisualElement();    // 선택 하이라이트
                _sel.style.position = Position.Absolute;
                _sel.style.left = -2; _sel.style.top = -2; _sel.style.right = -2; _sel.style.bottom = -2;
                _sel.style.borderTopWidth = 2; _sel.style.borderBottomWidth = 2;
                _sel.style.borderLeftWidth = 2; _sel.style.borderRightWidth = 2;
                _sel.style.borderTopColor = _sel.style.borderBottomColor =
                    _sel.style.borderLeftColor = _sel.style.borderRightColor = new StyleColor(UTKColor.AccentMagic);
                _sel.style.display = DisplayStyle.None;
                Add(_sel);

                _target = new VisualElement(); // 목표 위치 표시선
                _target.style.position = Position.Absolute;
                _target.style.left = 5; _target.style.right = 5;
                _target.style.height = 4;
                _target.style.backgroundColor = new StyleColor(new Color(1f, 0.8f, 0.2f));
                Add(_target);

                _fill = new VisualElement();   // 현재 핀 위치
                _fill.style.position = Position.Absolute;
                _fill.style.left = 20; _fill.style.right = 20;
                _fill.style.bottom = 8;
                _fill.style.height = 10;
                _fill.style.backgroundColor = new StyleColor(new Color(0.2f, 0.4f, 0.8f, 0.9f));
                Add(_fill);

                _num = new Label("");
                _num.style.position = Position.Absolute;
                _num.style.left = 0; _num.style.right = 0; _num.style.bottom = -16;
                _num.style.unityTextAlign = TextAnchor.MiddleCenter;
                _num.style.fontSize = 11;
                _num.style.color = new StyleColor(UTKColor.TextSecondary);
                Add(_num);
            }

            public void Apply(LockpickingSystem.PinState pin, bool selected)
            {
                float trackHeight = 160f;
                _sel.style.display = (selected && !pin.isSet) ? DisplayStyle.Flex : DisplayStyle.None;

                if (pin.isSet)
                {
                    _fill.style.backgroundColor = new StyleColor(new Color(0f, 0.8f, 0f, 0.8f));
                    _target.style.backgroundColor = new StyleColor(UTKColor.GuildGreen);
                }
                else
                {
                    _fill.style.backgroundColor = new StyleColor(new Color(0.2f, 0.4f, 0.8f, 0.9f));
                    _target.style.backgroundColor = new StyleColor(new Color(1f, 0.8f, 0.2f));
                }

                float targetY = (1f - pin.targetHeight) * trackHeight;
                float curY = (1f - pin.currentHeight) * trackHeight;
                _target.style.top = Mathf.Clamp(targetY - 2f, 0f, trackHeight - 4f);
                _fill.style.bottom = Mathf.Clamp(curY - 5f, 0f, trackHeight - 6f);
                _num.text = (pin.index + 1).ToString();
            }
        }

        // ===== 키 입력 Updater (helfinger 인식 위해 Mono Update) =====

        private class Updater : MonoBehaviour
        {
            public LockpickingUTK window;

            private bool _upHeld;
            private bool _downHeld;
            private float _repeatTimer;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                if (window == null || !window.IsOpen) return;
                var session = LockpickingSystem.CurrentSession;
                if (session == null || !session.isActive) return;

                LockpickingSystem.UpdateSession(Time.deltaTime);

                // 상승
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                {
                    if (!_upHeld || _repeatTimer <= 0)
                    {
                        LockpickingSystem.AdjustCurrentPin(0.05f);
                        _upHeld = true;
                        _repeatTimer = 0.1f;
                    }
                    _repeatTimer -= Time.deltaTime;
                }
                else _upHeld = false;

                // 하강
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                {
                    if (!_downHeld || _repeatTimer <= 0)
                    {
                        LockpickingSystem.AdjustCurrentPin(-0.05f);
                        _downHeld = true;
                        _repeatTimer = 0.1f;
                    }
                    _repeatTimer -= Time.deltaTime;
                }
                else _downHeld = false;

                // 고정 시도
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
                {
                    bool result = LockpickingSystem.TrySetCurrentPin();
                    if (!result)
                    {
                        window._durability--;
                        if (window._durability <= 0)
                        {
                            Debug.Log("[LockUTK] 🔧 픽 내구도 소진! 세션 중단");
                            window.AbortSession();
                        }
                    }
                }

                // ESC 취소
                if (Input.GetKeyDown(KeyCode.Escape) && window.IsOpen)
                    window.AbortSession();
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopTick();
                    window.RemoveFromHierarchy();
                }
            }
        }

        // ===== 헬퍼 =====

        private static Label MakeText(string text, Color color)
        {
            var l = new Label(text);
            l.style.color = new StyleColor(color);
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            return l;
        }

        private static string GetDifficultyDisplayName(LockpickingSystem.LockDifficulty d)
        {
            switch (d)
            {
                case LockpickingSystem.LockDifficulty.Easy:     return "쉬움";
                case LockpickingSystem.LockDifficulty.Medium:   return "보통";
                case LockpickingSystem.LockDifficulty.Hard:     return "어려움";
                case LockpickingSystem.LockDifficulty.VeryHard: return "매우 어려움";
                case LockpickingSystem.LockDifficulty.Legendary:return "전설";
                default: return "???";
            }
        }

        private void OnLockpickRequested(string locationId, LockpickingSystem.LockDifficulty difficulty)
            => StartSession(locationId, difficulty);
    }
}