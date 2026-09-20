// Phase O8 Batch B (C-O8-04): 건설 윈도우 — UTKWindowBase 파생(신규 기능, 원본 IMGUI 없음).
// ConstructionManager 데이터 API를 직접 소비(복제 금지). Theme.uss 클래스 재사용(utk-btn/utk-title-label),
// 섀도우는 UTKWindowBase가 자동 부착.
#pragma warning disable 114   // static Toggle()/Instance가 베이스 멤버를 숨김 — 의도됨(MercenaryHireUTK 선례)
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory
using ProjectName.Core.Data;       // BlueprintData
using ProjectName.Systems;         // ConstructionManager

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Phase O8 Batch B (C-O8-04) — 건설 윈도우 (UTK).
    ///
    /// [콘텐츠]
    ///  ① 설계도 목록 8행(BlueprintData.GetAll) — 이름/골드/시공 시간 표시 + "배치" 버튼.
    ///  ② 배치: 플레이어 위치(Camera.main 있으면 position + forward×5, 없으면 Vector3.zero)에서
    ///     ConstructionManager.TryPlace. TryPlace의 실패 사유(골드 부족/영지 경계 밖·겹침/경사)는
    ///     반환 메시지를 그대로 상태 라벨에 표시한다.
    ///     영지 id는 TerritoryDatabase 기본 "East_01" 고정 — 현재 영지 추적은 후속 과제(아래 OnPlaceBlueprint 주석).
    ///  ③ 구조물 목록(Structures) — id/설계도/완료 여부(시공 중 %) + "해체" 버튼(Demolish, 환불 50%).
    ///
    /// [갱신] StructureCompleted 정적 이벤트 구독(Subscribe/Unsubscribe 쌍 — StatusWindowUTK 패턴)
    ///        + 250ms 폴링(시공 진행률 반영, StatusWindowUTK Updater 패턴).
    /// [진입점] static Open() / Ensure() / Toggle(). UTKWindowManager 등록은 Show/Hide 경유 자동.
    /// [단축키] U — H는 GuardSelectionManager 사용 중(충돌 회피). U는 GameStatsUTK와 공유(후속 재배치 과제).
    /// </summary>
    public class ConstructionWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static ConstructionWindowUTK _instance;
        public static ConstructionWindowUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new ConstructionWindowUTK();
            var go = new GameObject("ConstructionWindowUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().window = _instance;
            Debug.Log("[ConstructionUTK] 건설 창 초기화 완료 — U키로 토글");
        }

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new ConstructionWindowUTK();
        }

        /// <summary>건설 창 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 480f;
        private const float WinH = 640f;
        private const float RefreshInterval = 0.25f;   // StatusWindowUTK 폴링 주기 동일
        private const float WinLeft = 520f;
        private const float WinTop = 96f;

        // ===== 레퍼런스 =====
        private readonly VisualElement _blueprintList;
        private readonly VisualElement _structureList;
        private Label _summaryLabel;
        private Label _statusLabel;
        private float _refreshTimer;
        private bool _structureSubscribed;   // 정적 이벤트 구독 플래그(중복 방지 — StatusWindowUTK 패턴)

        private ConstructionWindowUTK() : base("🏗️ 건설", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("🏗️ 영지 건설");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _content.Add(_summaryLabel);

            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 12f;
            _statusLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_statusLabel);

            _content.Add(MakeSectionHeader("📐 설계도"));

            _blueprintList = new VisualElement { name = "BlueprintList" };
            _blueprintList.style.flexDirection = FlexDirection.Column;
            _content.Add(_blueprintList);

            _content.Add(MakeSectionHeader("🏠 건설된 구조물"));

            _structureList = new VisualElement { name = "StructureList" };
            _structureList.style.flexDirection = FlexDirection.Column;
            _structureList.style.flexGrow = 1f;
            _content.Add(_structureList);

            ApplyUIToolkitFont(this);

            // 기본 숨김 + 위치 (MercenaryHireUTK 관례)
            style.display = DisplayStyle.None;
            style.left = WinLeft;
            style.top = WinTop;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = WinLeft;
            style.top = WinTop;
            SubscribeStructureCompleted();
            Refresh();
            Debug.Log("[ConstructionUTK] 건설 창 열림 (키: U)");
        }

        public override void Hide()
        {
            base.Hide();
            Debug.Log("[ConstructionUTK] 건설 창 닫힘");
        }

        // =================== 구독 관리 (Subscribe/Unsubscribe 쌍) ===================

        private void SubscribeStructureCompleted()
        {
            if (_structureSubscribed) return;
            ConstructionManager.StructureCompleted += OnStructureCompleted;
            _structureSubscribed = true;
        }

        private void UnsubscribeStructureCompleted()
        {
            if (!_structureSubscribed) return;
            ConstructionManager.StructureCompleted -= OnStructureCompleted;
            _structureSubscribed = false;
        }

        private void OnStructureCompleted(ConstructionManager.StructureEntry entry)
        {
            if (IsOpen) Refresh();
            SetStatus($"✅ 건설 완료: {DescribeBlueprint(entry.blueprintId)}");
        }

        // =================== 새로고침 (public — 이벤트/폴링/버튼 공용 경로) ===================

        public void Refresh()
        {
            RefreshSummary();
            RefreshBlueprints();
            RefreshStructures();
        }

        private void RefreshSummary()
        {
            int gold = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetItemCount("gold")
                : 0;
            int total = 0;
            int done = 0;
            var mgr = ConstructionManager.Instance;
            if (mgr != null)
            {
                total = mgr.Structures.Count;
                foreach (var s in mgr.Structures)
                {
                    if (s.isComplete) done++;
                }
            }
            _summaryLabel.text = $"🏗️ 영지 건설 — 💰 {gold}G · 🏠 {done}/{total} 완료";
        }

        private void RefreshBlueprints()
        {
            _blueprintList.Clear();

            foreach (var def in BlueprintData.GetAll())
            {
                var row = new VisualElement();
                row.name = "BlueprintRow_" + def.id;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 3f;
                row.style.paddingBottom = 3f;
                row.style.borderBottomWidth = 1f;
                row.style.borderBottomColor = new StyleColor(UTKColor.IronLine);

                var info = new VisualElement();
                info.style.flexGrow = 1f;
                info.Add(MakeLabel(def.displayName, UTKColor.TextPrimary, 14f));
                info.Add(MakeLabel(
                    $"💰 {def.goldCost}G · ⏱️ {def.buildTimeSeconds}초 · 반경 {def.footprintRadius:F1}m",
                    UTKColor.TextSecondary, 12f));
                row.Add(info);

                row.Add(UTKButton.Create("배치", () => OnPlaceBlueprint(def.id), UTKButton.Variant.Primary));
                _blueprintList.Add(row);
            }
        }

        private void RefreshStructures()
        {
            _structureList.Clear();

            var mgr = ConstructionManager.Instance;
            if (mgr == null)
            {
                _structureList.Add(MakeLabel("(ConstructionManager 없음)", UTKColor.TextSecondary, 12f));
                return;
            }

            if (mgr.Structures.Count == 0)
            {
                _structureList.Add(MakeLabel("건설된 구조물이 없습니다. 설계도를 배치하세요.",
                    UTKColor.TextSecondary, 12f));
                return;
            }

            foreach (var s in mgr.Structures)
            {
                string state = s.isComplete ? "✅ 완료" : $"🔨 시공 중 {Mathf.RoundToInt(s.progress * 100f)}%";

                var row = new VisualElement();
                row.name = "StructureRow_" + s.structureId;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 3f;
                row.style.paddingBottom = 3f;
                row.style.borderBottomWidth = 1f;
                row.style.borderBottomColor = new StyleColor(UTKColor.IronLine);

                var info = new VisualElement();
                info.style.flexGrow = 1f;
                info.Add(MakeLabel($"{DescribeBlueprint(s.blueprintId)}  {state}", UTKColor.TextPrimary, 13f));
                info.Add(MakeLabel($"#{s.structureId} @ {s.territoryId}", UTKColor.TextSecondary, 11f));
                row.Add(info);

                string id = s.structureId;   // 클로저 캡처용 지역 복사
                row.Add(UTKButton.Create("해체", () => OnDemolish(id), UTKButton.Variant.Danger));
                _structureList.Add(row);
            }
        }

        // =================== 배치 / 해체 ===================

        /// <summary>
        /// 배치 버튼 — [P6] 현재 위치 → 가장 가까운 영지 자동 판정(ResolveTerritoryAt).
        /// 위치: Camera.main 있으면 transform.position + forward×5, 없으면 Vector3.zero.
        /// TryPlace 반환 메시지(골드 부족/영지 경계 밖·겹침 등)는 그대로 상태 라벨에 표시.
        /// </summary>
        private void OnPlaceBlueprint(string blueprintId)
        {
            var mgr = ConstructionManager.Instance;
            if (mgr == null)
            {
                SetStatus("⚠️ ConstructionManager가 없습니다.");
                return;
            }

            Vector3 pos = GetPlacePosition();

            // [P6] 현재 위치 → 영지 자동 판정 (어디에도 해당 없으면 배치 거부)
            var db = ProjectName.Core.Data.TerritoryDatabase.Instance;
            var resolved = db?.ResolveTerritoryAt(pos);
            if (resolved == null)
            {
                SetStatus("영지 밖 — 영지 근처(60m)에서 배치하세요");
                Debug.LogWarning("[ConstructionUTK] 배치 실패: 영지 밖 위치");
                return;
            }
            string territoryId = resolved.Value.ToString();

            string result = mgr.TryPlace(territoryId, blueprintId, pos);
            SetStatus(result);
            Debug.Log($"[ConstructionUTK] 배치: {blueprintId} @ {territoryId} {pos} → {result}");
            Refresh();
        }

        private static Vector3 GetPlacePosition()
        {
            var cam = Camera.main;
            if (cam != null)
                return cam.transform.position + cam.transform.forward * 5f;
            return Vector3.zero;   // 카메라 없음(부팅 직전 등) 폴백
        }

        /// <summary>해체 버튼 — Demolish(환불 50%) 후 목록 갱신. 반환 메시지 그대로 표시.</summary>
        private void OnDemolish(string structureId)
        {
            var mgr = ConstructionManager.Instance;
            if (mgr == null) return;

            string result = mgr.Demolish(structureId);
            SetStatus(result);
            Debug.Log($"[ConstructionUTK] 해체: {structureId} → {result}");
            Refresh();
        }

        // =================== 키 토글 / 폴링용 Updater — StatusWindowUTK 패턴 ===================

        private class Updater : MonoBehaviour
        {
            public ConstructionWindowUTK window;

            private void Update()
            {
                // 부트스트랩 완료 전이어도 최초 부착을 멱등으로 시도
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && window != null)
                {
                    // [O8 C-O8-04] U키 — H는 GuardSelectionManager 사용 중. U는 GameStatsUTK와 공유(후속 재배치).
                    if (kb.uKey.wasPressedThisFrame) ConstructionWindowUTK.Toggle();
                    if (kb.escapeKey.wasPressedThisFrame && window.IsOpen) window.Close();
                }

                if (window == null || !window.IsOpen) return;
                window._refreshTimer += Time.unscaledDeltaTime;
                if (window._refreshTimer < RefreshInterval) return;
                window._refreshTimer = 0f;
                window.Refresh();
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.UnsubscribeStructureCompleted();
                    window.RemoveFromHierarchy();
                }
            }
        }

        // =================== 내부 헬퍼 ===================

        private static string DescribeBlueprint(string blueprintId)
        {
            return BlueprintData.TryGet(blueprintId, out var def)
                ? def.displayName
                : (string.IsNullOrEmpty(blueprintId) ? "(알 수 없음)" : blueprintId);
        }

        private static Label MakeSectionHeader(string text)
        {
            var l = new Label(text);
            l.AddToClassList("utk-title-label");
            l.style.fontSize = 14f;
            l.style.marginTop = 8f;
            return l;
        }

        private static Label MakeLabel(string text, Color color, float size)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        private void SetStatus(string text)
        {
            _statusLabel.text = text ?? "";
        }
    }
}
