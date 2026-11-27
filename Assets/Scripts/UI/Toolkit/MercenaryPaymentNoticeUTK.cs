using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Due-item DTO for the mercenary payment notice window. Supplied by the caller (parent/sibling);
    /// the window never fabricates entries, payments, or statuses.
    /// </summary>
    public sealed class DueMercenaryEntry
    {
        /// <summary>MercenaryData.id — the key passed to onApprove / onRelease.</summary>
        public readonly string Id;
        /// <summary>Display name (MercenaryData.mercenaryName).</summary>
        public readonly string DisplayName;
        /// <summary>MercenaryData.jobType (e.g. "Soldier", "Bard").</summary>
        public readonly string JobType;
        /// <summary>MercenaryData.GradeStars — [144차 P7-MRC #3] 별표 표시는 폐기(Figma 무존재, Lv 배지로 대체). 외부 호환용으로 값만 유지.</summary>
        public readonly string GradeStars;
        /// <summary>[144차 P7-MRC #2] MercenaryData.grade 실측 — 등급바 4색 체계용. null = 등급 데이터 부재(스킵+기록).</summary>
        public readonly MercenaryGrade? Grade;
        /// <summary>[144차 P7-MRC #3] 실제 레벨. 데이터 모델(MercenaryData/DueMercenaryContract)에 level 필드가 없어
        ///   현재 전행 -1(부재) — Lv 배지는 실레벨 데이터가 확보될 때만 렌더(위조 금지 계약).</summary>
        public readonly int Level;
        /// <summary>Runtime status text. Empty/null falls back to the neutral "고용 중".</summary>
        public readonly string Status;
        /// <summary>Amount due in gold for this entry (weekly contract cost).</summary>
        public readonly int Fee;

        public DueMercenaryEntry(string id, string displayName, string jobType, string gradeStars, string status, int fee,
            MercenaryGrade? grade = null, int level = -1)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            JobType = jobType ?? string.Empty;
            GradeStars = gradeStars ?? string.Empty;
            Status = status ?? string.Empty;
            Fee = fee < 0 ? 0 : fee;
            Grade = grade;
            Level = level;
        }
    }

    /// <summary>
    /// UI Toolkit — 고용비 알림 윈도우 (Figma 210:169, 624x984 design px).
    ///
    /// [자동 오픈] SoldierWageSystem.OnMercenaryContractPaymentsDue 이벤트 구독 (Bootstrap).
    ///   due 대상이 있을 때만 GetDueMercenaryContracts 스냅샷으로 열린다. 대상이 없으면 열지 않는다.
    /// [데이터] ShowForDueDay(day, entries) 로 주입된 실제 지불 대상만 표시한다.
    ///   - 샘플/가짜 레코드·고정 합계 없음. 합계 = 현재 필터로 보이는 행의 Fee 합.
    ///   - 하단 인원 표시 = MercenaryManager.HiredCount / AvailableMercenaryCount (실측 값).
    /// [동작] 승인 → SoldierWageSystem.TryApproveMercenaryContract, 방출 → SoldierWageSystem.ReleaseMercenaryContract.
    ///   승인 후 due 스냅샷을 다시 읽어 목록을 갱신하고, 대상이 없으면 창을 숨긴다.
    ///   골드 부족(InsufficientFunds)이면 대기 행을 유지하고 로그만 남긴다.
    /// [닫기] Hide/ESC 시 대기 목록(_pending)은 유지된다.
    /// [진입점] static Ensure() / Open() / Open(day, entries) — 멱등.
    /// [144차 P7-MRC] P0 4건 수리 — ①코너 데칼 4개(12×12 L자 1.8 #58A6FF) ②행 등급바(4.8×86.4,
    ///   SoldierManagementUTK TierColorForLevel 4색 체계) ③Lv 배지(fs14.4 Geist Mono 700 #58A6FF,
    ///   GradeStars 별표 폐기 — 실레벨 데이터 부재 시 스킵+기록) ④지불 요약 섹션 카드((24,844) 576×116.4
    ///   stroke #30363D@0.502 + 수용 인원 행 + 총 지불 금액 카드).
    /// </summary>
    public class MercenaryPaymentNoticeUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static MercenaryPaymentNoticeUTK _instance;
        public static MercenaryPaymentNoticeUTK Instance => _instance;

        /// <summary>팩토리 — 멱등. UIRoot 부착은 Show()에서 수행.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new MercenaryPaymentNoticeUTK();
        }

        /// <summary>현재 대기 목록을 열어 표시 (대기 목록 유지).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>due 이벤트에서 호출 — 지불 대상 목록으로 갱신 후 표시.</summary>
        public static void Open(int day, IReadOnlyList<DueMercenaryEntry> entries)
        {
            Ensure();
            _instance.ShowForDueDay(day, entries);
        }

        // ===== 자동 오픈 (SoldierWageSystem due 이벤트) =====
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SoldierWageSystem.OnMercenaryContractPaymentsDue -= HandleContractPaymentsDue;
            SoldierWageSystem.OnMercenaryContractPaymentsDue += HandleContractPaymentsDue;
        }

        private static void HandleContractPaymentsDue(int day)
        {
            var entries = BuildEntries(SoldierWageSystem.GetDueMercenaryContracts(day));
            if (entries.Count == 0) return;
            Open(day, entries);
        }

        /// <summary>SoldierWageSystem 스냅샷 → UI DTO 변환 (실제 값만 사용).</summary>
        private static List<DueMercenaryEntry> BuildEntries(SoldierWageSystem.DueMercenaryContract[] due)
        {
            var list = new List<DueMercenaryEntry>();
            if (due == null) return list;
            foreach (var d in due)
            {
                list.Add(new DueMercenaryEntry(
                    d.mercenaryId,
                    d.mercenaryName,
                    d.jobType,
                    GradeToStars(d.grade),
                    d.isAlive ? "고용 중" : "사망",
                    d.weeklyCost,
                    d.grade,   // [144차 P7-MRC #2] 등급바 실측 — MercenaryData.grade
                    -1));      // [144차 P7-MRC #3] 레벨 데이터 부재(모델에 level 필드 없음, 실측) — 스킵+기록
            }
            return list;
        }

        private static string GradeToStars(MercenaryGrade grade)
        {
            switch (grade)
            {
                case MercenaryGrade.Normal: return "★";
                case MercenaryGrade.High: return "★★";
                case MercenaryGrade.Elite: return "★★★";
                case MercenaryGrade.Legendary: return "★★★★";
                default: return "★";
            }
        }

        // ===== 설정 =====
        /// <summary>Figma 디자인 박스 (624x984). FigmaCanvasLayout이 1920x1080 캔버스에 등배수 스케일 적용.</summary>
        private const float DesignW = 624f;
        private const float DesignH = 984f;
        private static readonly Rect CanvasBounds = new Rect(648f, 48f, DesignW, DesignH);

        private const string FilterAll = "all";
        private const string FilterCombat = "Soldier";
        private const string FilterMercenary = "mercenary";
        private const string SubtitleText = "부대원 고용비 확인";

        // ===== [144차 P7-MRC] Figma 진실치 팔레트(210:169) — WeeklyTaxWindowUTK 로컬 팔레트 선례 준용 =====
        private static readonly Color AccentFigma = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f);         // #58A6FF
        private static readonly Color TextSubFigma = new Color(0x8B / 255f, 0x94 / 255f, 0x9E / 255f, 1f);       // #8B949E
        private static readonly Color TextMainFigma = new Color(0xF0 / 255f, 0xF6 / 255f, 0xFC / 255f, 1f);      // #F0F6FC
        private static readonly Color TierPurple = new Color32(0xBC, 0x8C, 0xFF, 0xFF);                           // #BC8CFF — 등급바 최상
        private static readonly Color TierDeepBlue = new Color32(0x1F, 0x6F, 0xEB, 0xFF);                         // #1F6FEB — 등급바 딥블루
        private static readonly Color ChromeStroke502 = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f); // #30363D@0.502
        private static readonly Color AccentFill078 = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.078f);   // #58A6FF@0.078
        private static readonly Color AccentStroke251 = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.251f); // #58A6FF@0.251
        private static readonly Color RootFill13181F = new Color(0x13 / 255f, 0x18 / 255f, 0x1F / 255f, 1f);     // #13181F α1.0 — 루트 패널 fill [144차 P7-MRC #2]
        private static readonly Color OnAccentText = new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f);       // #0B0E14 — 선택 탭 텍스트 [144차 P7-MRC #10]
        private static readonly Color SelectedRowFill = new Color(1f, 1f, 1f, 0.078f);                            // #FFFFFF@0.078 — 선택 행 fill [144차 P7-MRC #15]
        private static readonly Color RowFill002 = new Color(1f, 1f, 1f, 0.02f);                                 // #FFFFFF@0.02 — 일반 행 fill [144차 P7-MRC #16]
        private static readonly Color AvatarFill = new Color(0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 0.6f);       // #21262D@0.6 — 아바타 fill [144차 P7-MRC #18]
        private static readonly Color BadgeFill251 = new Color(0f, 0f, 0f, 0.251f);                              // #000000@0.251 — 상태 배지 fill [144차 P7-MRC #23]
        private static readonly Color StatusGreen = new Color(0x39 / 255f, 0xD3 / 255f, 0x53 / 255f, 1f);        // #39D353 — 상태 배지(고용 중) [144차 P7-MRC #23]
        private static readonly Color StatusOrange = new Color(0xFF / 255f, 0x9E / 255f, 0x2C / 255f, 1f);       // #FF9E2C — 상태 배지(사망) [144차 P7-MRC #23]
        private static Font _geistMono;                                                                            // GeistMono 폴백 캐시(선례)

        // ===== 콜백 (생성자에서 실제 계약 API에 바인딩) =====
        /// <summary>승인 버튼 → 승인 처리 후 due 스냅샷 재동기화.</summary>
        public Action<string> OnApprove { get; set; }
        /// <summary>방출 버튼 → 계약 해지 처리 후 due 스냅샷 재동기화.</summary>
        public Action<string> OnRelease { get; set; }

        // ===== 상태 =====
        private readonly List<DueMercenaryEntry> _pending = new List<DueMercenaryEntry>();
        private int _day;
        private string _filter = FilterAll;
        private VisualElement _canvasLayoutRoot;

        // ===== 레퍼런스 =====
        private readonly Label _subtitleLabel;
        private readonly VisualElement _tabBar;
        private readonly ScrollView _rows;
        private readonly Label _emptyLabel;
        private readonly Label _capacityLabel;    // [144차 P7-MRC #4] 수용 인원 우측 값 "N / M 명"
        private readonly Label _sumDescLabel;     // [144차 P7-MRC #4] "명부에 표시된 N명 고용비 합계"
        private readonly Label _totalLabel;
        private bool _gradeSkipLogged;            // [144차 P7-MRC #2] 등급 부재 스킵 기록(리프레시당 1회)
        private bool _levelSkipLogged;            // [144차 P7-MRC #3] 레벨 부재 스킵 기록(리프레시당 1회)
        private string _selectedId;               // [144차 P7-MRC #15] 선택 행 Id — 시각 전용(계약 로직 무관), null=미선택

        public int Day => _day;
        /// <summary>현재 대기 중인 지불 대상 (읽기 전용 스냅샷 아님 — 내부 목록 뷰).</summary>
        public IReadOnlyList<DueMercenaryEntry> PendingEntries => _pending;

        private MercenaryPaymentNoticeUTK()
            : base("고용비 알림", new Vector2(DesignW, DesignH), UTKWindowChrome.Frameless)
        {
            OnApprove = ApproveContract;
            OnRelease = ReleaseContract;

            // [144차 P7-MRC #2] 루트 패널 — UTKTheme.Panel(#161B22) 무분기 → Figma 진실치 fill #13181F α1.0,
            //   r 14.4, stroke #30363D@0.502 1.2 — 210:169.
            _content.style.backgroundColor = new StyleColor(RootFill13181F);
            SetBorder(_content, 1.2f, ChromeStroke502, 14.4f);
            // [144차 P7-MRC #3] Figma 루트 blur/그림자(210:169) — UIToolkit 배경 블러/드롭 섀도 미지원 →
            //   기록 전용(렌더 스킵, 위조 금지). 시각 효과 부재는 의도된 스킵임.
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;
            _content.style.overflow = Overflow.Hidden;

            // [144차 P7-MRC #1] 코너 데칼 4개 12×12 L자 1.8 #58A6FF — 210:170-181. UTKTheme 공용 어휘
            //   (WeeklyTaxWindowUTK:118 동일 12×12 선례, 코너 밖(outside) 모드 기본).
            UTKTheme.AddCornerDecals(_content, 12f, 1.8f, "#58A6FF");

            // 제목 (Figma 상단 섹션) — [144차 P7-MRC #5] fs20 → 21.6 Bold #F0F6FC, y 20 → 24 — 210:172-173.
            var title = MakeLabel("고용비 알림", 21.6f, TextMainFigma, true);
            SetRect(title, 24f, 24f, 540f, 30f);
            _content.Add(title);

            // [144차 P7-MRC #6] 보조제목 — 하단 블록 → 제목 우측 인라인 (141,32) BASELINE, fs13.2 500 — 210:176-177.
            //   UTK 폰트 웨이트 축소: 500 → Normal(400) 근사(위조 금지 — 축소 기록).
            _subtitleLabel = MakeLabel(SubtitleText, 13.2f, TextSubFigma, false);
            SetRect(_subtitleLabel, 141f, 32f, DesignW - 141f - 24f, 20f);
            _content.Add(_subtitleLabel);

            // 닫기 (프레임리스 — 타이틀바 없음). ESC와 동일하게 Close()=Hide() → 대기 목록 유지.
            var closeBtn = new Button(() => Close()) { text = "✕" };
            StyleButton(closeBtn, UTKTheme.PanelSub, UTKTheme.TextSub, UTKTheme.Stroke, 1f);
            SetRect(closeBtn, DesignW - 24f - 28f, 18f, 28f, 28f);
            _content.Add(closeBtn);

            // 탭 (전체 / 전투 / 용병) — [144차 P7-MRC #9] (24,92) → (24,96), h 36 → 36.2 — 210:187-188.
            _tabBar = new VisualElement { name = "PaymentTabs" };
            SetRect(_tabBar, 24f, 96f, DesignW - 48f, 36.2f);
            _tabBar.style.flexDirection = FlexDirection.Row;
            _content.Add(_tabBar);

            // 동적 행 목록 영역 — [144차 P7-MRC #13] y 144 → 151 (576×673, 행간 gap 9.6) — 210:193-194.
            _rows = new ScrollView(ScrollViewMode.Vertical) { name = "PaymentRows" };
            SetRect(_rows, 24f, 151f, DesignW - 48f, 673f);
            _rows.style.paddingRight = 4f;
            _content.Add(_rows);

            _emptyLabel = MakeLabel("지불 대상 없음", 14f, UTKTheme.TextSub, false);
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            SetRect(_emptyLabel, 24f, 300f, DesignW - 48f, 24f);
            _content.Add(_emptyLabel);

            // [144차 P7-MRC #4] 지불 금액 요약 섹션 (24,844) 576×116.4 — 210:333 FRAME stroke #30363D@0.502 w1.0.
            //   구 divider(게임 전용, Figma 부재 — 구 :204-207)+rosterLabel+totalLabel 단일 라벨 체계 폐기 →
            //   2행 카드 구조로 재배치(감사 #29/#30/#31/#32, y 기준선 852→844). 승인/방출/합계(표시 행 Fee 합) 무손상.
            var summary = new VisualElement { name = "PaymentSummary" };
            SetRect(summary, 24f, 844f, 576f, 116.4f);
            SetBorder(summary, 1f, ChromeStroke502, 0f);
            _content.Add(summary);

            // 행① 수용 인원 (rel 0,14) 576×20 SPACE_BETWEEN — 210:334-336.
            //   좌 "총원 / 최대 수용력" fs15.6 400 #8B949E / 우 "N / M 명" fs15.6 Geist Mono 700 #58A6FF — 실측 API 그대로.
            var capacityRow = new VisualElement { name = "CapacityRow" };
            SetRect(capacityRow, 0f, 14f, 576f, 20f);
            capacityRow.style.flexDirection = FlexDirection.Row;
            capacityRow.style.justifyContent = Justify.SpaceBetween;
            capacityRow.style.alignItems = Align.Center;
            summary.Add(capacityRow);
            var capacityItem = MakeLabel("총원 / 최대 수용력", 15.6f, TextSubFigma, false);
            capacityItem.pickingMode = PickingMode.Ignore;
            capacityRow.Add(capacityItem);
            _capacityLabel = MakeLabel("— / — 명", 15.6f, AccentFigma, true);
            _capacityLabel.style.unityFont = GetGeistMonoFont();   // Geist Mono 700 — 에셋 부재 시 UIFont 폴백(선례)
            capacityRow.Add(_capacityLabel);

            // 행② 총 지불 금액 카드 (rel 0,48) 576×67.6 — fill #58A6FF@0.078 / stroke #58A6FF@0.251 1.2 r9.6 — 210:337-341.
            //   좌 "총 지불 금액" fs16.8 700 #F0F6FC + "명부에 표시된 N명 고용비 합계" fs12 400 #8B949E(gap 4.8),
            //   우 합계 fs26.4 Geist Mono 700 #58A6FF.
            var totalCard = new VisualElement { name = "PaymentTotalCard" };
            SetRect(totalCard, 0f, 48f, 576f, 67.6f);
            totalCard.style.backgroundColor = AccentFill078;
            SetBorder(totalCard, 1.2f, AccentStroke251, 9.6f);
            totalCard.style.paddingLeft = 14.4f;
            totalCard.style.paddingRight = 14.4f;
            totalCard.style.justifyContent = Justify.SpaceBetween;
            totalCard.style.alignItems = Align.Center;
            summary.Add(totalCard);

            var totalInfo = new VisualElement { name = "PaymentTotalInfo" };
            totalInfo.style.flexShrink = 1f;
            totalInfo.style.flexDirection = FlexDirection.Column;
            totalCard.Add(totalInfo);
            var totalTitle = MakeLabel("총 지불 금액", 16.8f, TextMainFigma, true);
            totalTitle.pickingMode = PickingMode.Ignore;
            totalInfo.Add(totalTitle);
            _sumDescLabel = MakeLabel("—", 12f, TextSubFigma, false);
            _sumDescLabel.style.marginTop = 4.8f;   // 210:338 금액 설명 VERTICAL gap 4.8
            _sumDescLabel.pickingMode = PickingMode.Ignore;
            totalInfo.Add(_sumDescLabel);

            _totalLabel = MakeLabel("", 26.4f, AccentFigma, true);
            _totalLabel.style.unityFont = GetGeistMonoFont();      // Geist Mono 700 — 에셋 부재 시 UIFont 폴백(선례)
            totalCard.Add(_totalLabel);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        // =================== 생명주기 ===================

        /// <summary>due 이벤트 진입점 — 지불 대상 목록을 교체하고 표시.</summary>
        public void ShowForDueDay(int day, IReadOnlyList<DueMercenaryEntry> entries)
        {
            _day = day;
            _pending.Clear();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                    bool dup = false;
                    foreach (var p in _pending)
                        if (p.Id == e.Id) { dup = true; break; }
                    if (!dup) _pending.Add(e);
                }
            }
            Show();
        }

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);

            if (_canvasLayoutRoot != root)
            {
                if (_canvasLayoutRoot != null)
                    _canvasLayoutRoot.UnregisterCallback<GeometryChangedEvent>(OnCanvasRootGeometryChanged);
                _canvasLayoutRoot = root;
                if (_canvasLayoutRoot != null)
                    _canvasLayoutRoot.RegisterCallback<GeometryChangedEvent>(OnCanvasRootGeometryChanged);
            }
            ApplyLayout();
            Refresh();
        }

        // Hide()/Close()/ESC는 base 그대로 — 대기 목록(_pending)은 지우지 않는다.

        private void OnCanvasRootGeometryChanged(GeometryChangedEvent evt) => ApplyLayout();

        private void ApplyLayout()
        {
            if (_canvasLayoutRoot == null) return;
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, CanvasBounds, _canvasLayoutRoot);
        }

        // =================== 계약 API 바인딩 ===================

        /// <summary>
        /// 현재 게임일 — 창이 열려 있는 동안 날짜가 넘어갈 수 있으므로 표시 시점 스냅샷(_day)이 아닌
        /// TimeManager 실시간 값을 우선 사용한다. TimeManager 부재 시에만 마지막 due 일(_day)로 대체.
        /// </summary>
        private int CurrentGameDay()
        {
            var tm = TimeManager.Instance;
            return tm != null ? tm.CurrentDay : _day;
        }

        private void ApproveContract(string id)
        {
            var r = SoldierWageSystem.TryApproveMercenaryContract(id, CurrentGameDay(), out int charged, out int next);
            if (r == SoldierWageSystem.MercenaryContractResult.Success)
                Debug.Log($"[PaymentNoticeUTK] 고용비 승인: {id} {charged}G, 다음 청구일 {next}");
            else
                Debug.LogWarning($"[PaymentNoticeUTK] 고용비 승인 실패: {id} ({r}) — 대기 유지");
        }

        private void ReleaseContract(string id)
        {
            var r = SoldierWageSystem.ReleaseMercenaryContract(id);
            if (r != SoldierWageSystem.MercenaryContractResult.Success)
                Debug.LogWarning($"[PaymentNoticeUTK] 방출 결과: {id} ({r})");
        }

        /// <summary>due 스냅샷을 다시 읽어 대기 목록을 갱신. 대상이 없으면 창을 숨긴다.</summary>
        private void SyncWithDueSnapshot()
        {
            _day = CurrentGameDay();
            _pending.Clear();
            _pending.AddRange(BuildEntries(SoldierWageSystem.GetDueMercenaryContracts(_day)));
            if (_pending.Count == 0)
            {
                Hide();
                return;
            }
            Refresh();
        }

        // =================== 갱신 ===================

        private static string CategoryLabel(string jobType)
        {
            return string.Equals(jobType, FilterCombat, StringComparison.OrdinalIgnoreCase) ? "전투" : "용병";
        }

        private bool MatchesFilter(DueMercenaryEntry e)
        {
            if (_filter == FilterAll) return true;
            bool isCombat = string.Equals(e.JobType, FilterCombat, StringComparison.OrdinalIgnoreCase);
            return _filter == FilterCombat ? isCombat : !isCombat;
        }

        private void Refresh()
        {
            _subtitleLabel.text = SubtitleText;
            _gradeSkipLogged = false;   // [144차 P7-MRC #2/#3] 스킵 기록 플래그 — 리프레시당 1회 로그
            _levelSkipLogged = false;

            RebuildTabs();

            _rows.contentContainer.Clear();
            int visible = 0;
            long visibleTotal = 0;
            foreach (var e in _pending)
            {
                if (!MatchesFilter(e)) continue;
                visible++;
                visibleTotal += e.Fee;
                _rows.Add(BuildRow(e));
            }
            ApplyUIToolkitFont(_rows);

            _emptyLabel.style.display = _pending.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // [144차 P7-MRC #4] 수용 인원 우측 값 — 실측 API(MercenaryManager.HiredCount / AvailableMercenaryCount)
            //   그대로 — 210:336 "12 / 20 명" 체계.
            var mgr = MercenaryManager.Instance;
            _capacityLabel.text = mgr != null
                ? $"{mgr.HiredCount} / {mgr.AvailableMercenaryCount} 명"
                : "— / — 명";

            // [144차 P7-MRC #4] 합산 설명 + 합계 — 표시 행 Fee 합 산출 규약 유지(감사 #33 PASS),
            //   포맷만 Figma 진실치("6,990g" — 천단위 콤마 + 소문자 g)로 교체.
            _sumDescLabel.text = $"명부에 표시된 {visible}명 고용비 합계";
            _totalLabel.text = visibleTotal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "g";

            // [144차 P7-MRC #3] Lv 배지 스킵+기록 — 실제 레벨 데이터 부재(실측: MercenaryData/
            //   DueMercenaryContract에 level 필드 없음). 렌더 경로는 BuildRow 구현 — 데이터 확보 시 자동 표시.
            if (visible > 0 && !_levelSkipLogged)
            {
                Debug.LogWarning("[PaymentNoticeUTK] Lv 배지 스킵 — 실제 레벨 데이터 부재(MercenaryData level 필드 없음) — 210:201 Lv.NN");
                _levelSkipLogged = true;
            }
        }

        private void RebuildTabs()
        {
            _tabBar.Clear();
            AddTab(FilterAll, "전체", 0);
            AddTab(FilterCombat, "전투", 1);
            AddTab(FilterMercenary, "용병", 2);
        }

        private void AddTab(string filter, string text, int index)
        {
            bool selected = _filter == filter;
            var btn = new Button(() =>
            {
                _filter = filter;
                Refresh();
            })
            {
                text = text,
                name = "PaymentTab_" + filter,
            };
            btn.style.flexGrow = 1f;
            // [144차 P7-MRC #9] 탭 간격 gap 12 → 4.8 — 210:376-377.
            btn.style.marginRight = index < 2 ? 4.8f : 0f;
            // [144차 P7-MRC #12] 탭 문구 fs13 → 14.4 Bold — 210:378.
            btn.style.fontSize = 14.4f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            // [144차 P7-MRC #9] 탭 r6 → 7.2 (StyleButton 코너 반경 인자화 — 타 버튼 기존 6f 유지).
            if (selected)
            {
                // [144차 P7-MRC #10] 선택 탭 — PanelSub 배경+Accent 텍스트+border 2 폐기 →
                //   fill #58A6FF + 텍스트 #0B0E14 + stroke 없음 — 210:381.
                StyleButton(btn, AccentFigma, OnAccentText, AccentFigma, 0f, 7.2f);
            }
            else
            {
                StyleButton(btn, Color.clear, UTKTheme.TextSub, UTKTheme.Stroke, 1f, 7.2f);
            }
            _tabBar.Add(btn);
        }

        private VisualElement BuildRow(DueMercenaryEntry e)
        {
            // [144차 P7-MRC #14] Figma 행: 576 x 86.4, 행간 marginBottom 9.6
            //   (7행 = 7×86.4 + 6×9.6 = 662.4 ≤ 목록 673 — 210:389-399).
            bool selected = !string.IsNullOrEmpty(_selectedId) && e.Id == _selectedId;
            var row = new VisualElement { name = "PaymentRow_" + e.Id };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 86.4f;
            row.style.flexShrink = 0f;
            row.style.marginBottom = 9.6f;
            row.style.paddingLeft = 14.4f;
            row.style.paddingRight = 14.4f;
            // [144차 P7-MRC #15] 행 표면 — 전 행 Color.clear 무분기 → 선택 행만
            //   fill #FFFFFF@0.078 + stroke #58A6FF 1.8 — 210:398-399. 비선택 행 표면은 #16에서 처리(이번 패스는
            //   선택분만) — 기존 stroke 1f/#8B949E 계열은 비선택 분기에 그대로 유지(통합).
            //   선택 상태(_selectedId)는 행 탭으로 갱신 — 승인/방출/합계/탭 필터 로직과 무관(시각 전용).
            // [144차 P7-MRC #16] 비선택 행 표면 — Color.clear+Stroke(#2E343D)1f 폐기 →
            //   fill #FFFFFF@0.02 + stroke #30363D@0.502 1.2 — 210:398-399. 선택 행은 #15 그대로.
            row.style.backgroundColor = selected ? new StyleColor(SelectedRowFill) : new StyleColor(RowFill002);
            SetBorder(row, selected ? 1.8f : 1.2f, selected ? AccentFigma : ChromeStroke502, 9.6f);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_selectedId == e.Id) return;
                _selectedId = e.Id;
                Refresh();
            });

            // [144차 P7-MRC #2] 등급바 4.8×86.4 좌측 — 210:194 등, 등급색 4종(SoldierManagementUTK
            //   TierColorForLevel 4색 체계). 등급 데이터 부재 엔트리(외부 주입)는 바를 스킵하고 기록(위조 색 금지).
            if (e.Grade.HasValue)
            {
                var tierBar = new VisualElement { name = "TierBar_" + e.Id };
                SetRect(tierBar, 0f, 0f, 4.8f, 86.4f);
                tierBar.style.backgroundColor = new StyleColor(TierColorForGrade(e.Grade.Value));
                tierBar.pickingMode = PickingMode.Ignore;
                row.Add(tierBar);
            }
            else if (!_gradeSkipLogged)
            {
                Debug.LogWarning($"[PaymentNoticeUTK] 등급바 스킵 — 등급 데이터 부재: {e.Id}");
                _gradeSkipLogged = true;
            }

            // [144차 P7-MRC #18] 아바타 52.8 — PanelSub(α1.0)/r20/border Accent 1f 폐기 →
            //   fill #21262D@0.6 r26.4(원형) border #30363D@0.502 1.2 — 210:402-410.
            //   [144차 P7-MRC #19] 부대원 아이콘 — 실제 이미지 자산 부재(아바타 슬롯용 아이콘 에셋 미보유
            //   실측) → 이니셜 텍스트 대체 유지. 에셋 확보 시 이니셜 라벨을 Image로 스왑(스킵 기록 — 위조 금지).
            var avatar = new VisualElement { name = "Avatar" };
            avatar.style.width = 52.8f;
            avatar.style.height = 52.8f;
            avatar.style.flexShrink = 0f;
            // [144차 P7-MRC #14] 아바타-정보 열 gap 12 → 14.4 — 210:389-399.
            avatar.style.marginRight = 14.4f;
            avatar.style.backgroundColor = AvatarFill;
            avatar.style.justifyContent = Justify.Center;
            avatar.style.alignItems = Align.Center;
            SetBorder(avatar, 1.2f, ChromeStroke502, 26.4f);
            string initial = string.IsNullOrEmpty(e.DisplayName) ? "?" : e.DisplayName.Substring(0, 1);
            avatar.Add(MakeLabel(initial, 16f, UTKTheme.TextMain, true));
            row.Add(avatar);

            // 정보 열
            var info = new VisualElement();
            info.style.flexGrow = 1f;
            info.style.flexShrink = 1f;
            info.style.flexDirection = FlexDirection.Column;

            // [144차 P7-MRC #3] 이름 행 — 우측 Lv 배지(SPACE_BETWEEN, fs14.4 Geist Mono 700 #58A6FF) — 210:201.
            //   GradeStars 별표 폐기(Figma 무존재). 실제 레벨 부재 시 배지 생략(스킵+기록 — Refresh 참조).
            var headerRow = new VisualElement { name = "RowHeader_" + e.Id };
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            // [144차 P7-MRC #20] 이름 fs15 → 16.8 Bold #F0F6FC — 210:420.
            headerRow.Add(MakeLabel(string.IsNullOrEmpty(e.DisplayName) ? e.Id : e.DisplayName, 16.8f, TextMainFigma, true));
            if (e.Level >= 0)
            {
                var lvBadge = MakeLabel($"Lv.{e.Level}", 14.4f, AccentFigma, true);
                lvBadge.style.unityFont = GetGeistMonoFont();   // Geist Mono 700 — 에셋 부재 시 UIFont 폴백(선례)
                headerRow.Add(lvBadge);
            }
            info.Add(headerRow);

            // [144차 P7-MRC #22] 병종 — meta 별표 fs12 폐기(P0에서 Lv 배지로 대체됨) →
            //   병종 fs14.4 400 #8B949E(실측: CategoryLabel(e.JobType) — "전투"/"용병") — 210:422-424.
            string meta = CategoryLabel(e.JobType);
            info.Add(MakeLabel(meta, 14.4f, TextSubFigma, false));

            // [144차 P7-MRC #23] 상태 배지 — 텍스트 라벨만 폐기 → 배지 프레임 62.4×18.8
            //   fill #000000@0.251 r4.8 pad 7.2 + 상태색 3종(실측 상태 매핑: 고용 중 #39D353 /
            //   사망 #FF9E2C / 기타·기본 #8B949E) — 210:426-427.
            string status = string.IsNullOrEmpty(e.Status) ? "고용 중" : e.Status;
            var statusBadge = new VisualElement { name = "StatusBadge_" + e.Id };
            statusBadge.style.width = 62.4f;
            statusBadge.style.height = 18.8f;
            statusBadge.style.flexShrink = 0f;
            statusBadge.style.backgroundColor = BadgeFill251;
            statusBadge.style.paddingLeft = 7.2f;
            statusBadge.style.paddingRight = 7.2f;
            statusBadge.style.justifyContent = Justify.Center;
            statusBadge.style.alignItems = Align.Center;
            statusBadge.pickingMode = PickingMode.Ignore;
            SetBorder(statusBadge, 0f, Color.clear, 4.8f);
            statusBadge.Add(MakeLabel(status, 12f, StatusColorFor(status), false));
            info.Add(statusBadge);
            row.Add(info);

            // 지불액
            var fee = new VisualElement();
            fee.style.width = 96f;
            fee.style.alignItems = Align.FlexEnd;
            // [144차 P7-MRC #24] 고용비 — fs16 Gold #E3B341 폐기 → fs12 Geist Mono 700 #58A6FF,
            //   포맷 "1,200g"(천단위 콤마 + 소문자 g — 실측 Fee 데이터) — 210:431-434.
            var feeLabel = MakeLabel(e.Fee.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "g", 12f, AccentFigma, true);
            feeLabel.style.unityFont = GetGeistMonoFont();   // Geist Mono 700 — 에셋 부재 시 UIFont 폴백(선례)
            fee.Add(feeLabel);
            row.Add(fee);

            // 동작 (실제 계약 API 바인딩) — Figma: 129.6 x 36, 승인/방출 60x36 가로 배치 (간격 9.6)
            var actions = new VisualElement { name = "PaymentActions" };
            actions.style.width = 129.6f;
            actions.style.height = 36f;
            actions.style.minWidth = 129.6f;
            actions.style.flexShrink = 0f;
            actions.style.marginLeft = 10f;
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;

            var approve = new Button(() => Resolve(e.Id, OnApprove, "승인")) { text = "승인", name = "ApproveBtn" };
            StyleButton(approve, UTKTheme.Accent, UTKTheme.Panel, UTKTheme.Accent, 1f);
            approve.style.width = 60f;
            approve.style.height = 36f;
            approve.style.marginRight = 9.6f;
            // [144차 P7-MRC #28] 버튼 문구 fs13 → 14.4 Bold(승인/방출 공통) — 210:447/452/456/460.
            approve.style.fontSize = 14.4f;
            approve.style.unityFontStyleAndWeight = FontStyle.Bold;
            approve.SetEnabled(OnApprove != null);
            actions.Add(approve);

            var release = new Button(() => Resolve(e.Id, OnRelease, "방출")) { text = "방출", name = "ReleaseBtn" };
            // [144차 P7-MRC #27] 방출 버튼 — Danger #F85149 fill 폐기 → fill #FFFFFF@0.02 +
            //   stroke #30363D@0.502 + 텍스트 #F0F6FC r7.2(승인 버튼과 동일 중성 프레임 체계) — 210:457.
            StyleButton(release, RowFill002, TextMainFigma, ChromeStroke502, 1f, 7.2f);
            release.style.width = 60f;
            release.style.height = 36f;
            release.style.fontSize = 14.4f;
            release.style.unityFontStyleAndWeight = FontStyle.Bold;
            release.SetEnabled(OnRelease != null);
            actions.Add(release);

            row.Add(actions);
            return row;
        }

        private void Resolve(string id, Action<string> callback, string verb)
        {
            if (callback == null)
            {
                Debug.LogWarning($"[PaymentNoticeUTK] {verb} 콜백 미지정 — 무시: {id}");
                return;
            }
            callback(id);
            // 실제 상태는 due 스냅샷으로 확정 (승인 실패 시 행 유지, 성공/방출 시 행 제거)
            SyncWithDueSnapshot();
        }

        // =================== 헬퍼 ===================

        private static Label MakeLabel(string text, float fontSize, Color color, bool bold)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = fontSize;
            l.style.color = new StyleColor(color);
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.overflow = Overflow.Hidden;
            return l;
        }

        private static void SetRect(VisualElement ve, float x, float y, float w, float h)
        {
            ve.style.position = Position.Absolute;
            ve.style.left = x;
            ve.style.top = y;
            ve.style.width = w;
            ve.style.height = h;
        }

        /// <summary>[144차 P7-MRC #23] 상태 배지 색 — 실측 상태 매핑 3색: 고용 중 #39D353 / 사망 #FF9E2C / 기타·기본 #8B949E.</summary>
        private static Color StatusColorFor(string status)
        {
            if (string.IsNullOrEmpty(status)) return TextSubFigma;
            if (status.Contains("고용")) return StatusGreen;
            if (status.Contains("사망")) return StatusOrange;
            return TextSubFigma;
        }

        private static void SetBorder(VisualElement ve, float width, Color color, float radius)
        {
            ve.style.borderTopWidth = ve.style.borderBottomWidth = ve.style.borderLeftWidth = ve.style.borderRightWidth = width;
            ve.style.borderTopColor = ve.style.borderBottomColor = ve.style.borderLeftColor = ve.style.borderRightColor = new StyleColor(color);
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = radius;
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = radius;
        }

        private static void StyleButton(Button btn, Color bg, Color text, Color border, float borderWidth, float cornerRadius = 6f)
        {
            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = bg;
            btn.style.color = text;
            SetBorder(btn, borderWidth, border, cornerRadius);
        }

        /// <summary>[144차 P7-MRC #2] 등급바 색 — SoldierManagementUTK TierColorForLevel 4색 체계를 용병 등급에 매핑
        ///   (최상 Legendary 퍼플 #BC8CFF → Elite 딥블루 #1F6FEB → High 액센트 #58A6FF → 최하 Normal 그레이 #8B949E).</summary>
        private static Color TierColorForGrade(MercenaryGrade grade)
        {
            switch (grade)
            {
                case MercenaryGrade.Legendary: return TierPurple;
                case MercenaryGrade.Elite: return TierDeepBlue;
                case MercenaryGrade.High: return AccentFigma;
                default: return TextSubFigma;
            }
        }

        /// <summary>[144차 P7-MRC #3/#4] Geist Mono 로드 — WorldMap/WeeklyTaxWindowUTK 선례. 프로젝트에
        ///   Geist Mono 에셋 미보유 실측 → UIFont 폴백 렌더(위조 금지 계약 — 스왑 기록, 에셋 추가 시 자동 적용).</summary>
        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }
    }
}
