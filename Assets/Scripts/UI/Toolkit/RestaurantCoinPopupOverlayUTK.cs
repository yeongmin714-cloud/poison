using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [100차] 음식점 결제 코인 팝업 — 손님 NPC 머리 위 코인 아이콘 + 결제 금액 표시(2s 부상·페이드).
    ///
    ///  · 구독: RestaurantVisitorSystem.PurchaseVisualized(정적 이벤트 — Systems→UI 단방향 규약 준수).
    ///  · 풀: NameplateOverlayUTK 풀/슬롯 패턴 준용(증분 풀, 상한 24, 만료 → 풀 반환, 리셋 후 재사용).
    ///  · 렌더: 기존 UTK 렌더 경로만 사용(벡터메시 신규 경로 금지 계약) — 코인=배경색 원형 VisualElement,
    ///    금액=Label "+N G".
    ///  · 투영: Camera.WorldToViewportPoint → 패널 좌표(NameplateOverlayUTK.PlaceSlot 동일 방식),
    ///    헤드 오프셋 2.2m(주민 1.8m 키 계약 머리 위), 화면 밖/카메라 뒤 숨김.
    ///  · 부트: RuntimeInitializeOnLoadMethod(AfterSceneLoad) — 런타임 AddComponent 전용(DDOL).
    ///  · ⚠ 연출 전용 — 구매 이벤트는 정산 상태와 무관(RestaurantVisitorSystem 혼합 계약).
    /// </summary>
    public sealed class RestaurantCoinPopupOverlayUTK : MonoBehaviour
    {
        private static RestaurantCoinPopupOverlayUTK _instance;

        private const int PoolInitial = 8;
        private const int PoolMax = 24;
        private const int PoolGrowStep = 4;
        private const float LifeTotalSeconds = 2.0f;   // 팝업 수명(초)
        private const float RisePixels = 40f;          // 수명 동안 상승량(px)
        private const float FadeLastSeconds = 0.6f;    // 말단 페이드 구간(초)
        private const float HeadOffsetM = 2.2f;        // 머리 위 오프셋(m) — 주민 1.8m 키 계약
        private const float ScreenMargin = 40f;        // 화면 밖 스킵 마진(px)
        private const float CamRefreshInterval = 0.5f; // Camera.main 재조회 주기

        private static readonly Color CoinGold = new Color(0xE3 / 255f, 0xB3 / 255f, 0x41 / 255f, 1f);   // #E3B341 골드 앰버(배치 링 동일)
        private static readonly Color CoinRim = new Color(0xB8 / 255f, 0x86 / 255f, 0x0B / 255f, 1f);    // #B8860B 테두리
        private static readonly Color AmountColor = new Color(0xF0 / 255f, 0xF6 / 255f, 0xFC / 255f, 1f); // #F0F6FC 본문
        private static readonly Color BackdropColor = new Color(0x0F / 255f, 0x14 / 255f, 0x1B / 255f, 0.85f); // #0F141B 어두운 표면

        private VisualElement _overlayRoot;
        private float _rootW;
        private float _rootH;
        private Camera _cam;
        private float _camNextRefresh;

        private readonly List<Popup> _pool = new List<Popup>(PoolInitial);
        private readonly List<Popup> _active = new List<Popup>(PoolInitial);

        private sealed class Popup
        {
            public bool used;
            public Vector3 worldPos;
            public float lifeLeft;
            public float lifeTotal;
            public VisualElement root;
            public Label amountLabel;
            public VisualElement coin;
        }

        // ═══════════════ 생명주기 ═══════════════

        private void OnEnable()
        {
            _instance = this;
            RestaurantVisitorSystem.PurchaseVisualized += OnPurchase;
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
            RestaurantVisitorSystem.PurchaseVisualized -= OnPurchase;
            ReleaseAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isPlaying || _instance != null) return;
            var go = new GameObject("RestaurantCoinPopupOverlay");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<RestaurantCoinPopupOverlayUTK>();
        }

        private void Update()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;                       // UIRoot 미준비 — 조용히 스킵

            if (_overlayRoot == null || _overlayRoot.panel == null)
                AttachToRoot(root);
            if (_overlayRoot == null) return;

            RefreshCamera();
            if (_cam == null) return;

            // [U8 규약 계승] 창 열리면 오버레이 전체 숨김(게이트 유지)
            if (ProjectName.Core.UITransitionState.AnyWindowOpen)
            {
                if (_overlayRoot.style.display != DisplayStyle.None)
                    _overlayRoot.style.display = DisplayStyle.None;
                return;
            }
            if (_overlayRoot.style.display != DisplayStyle.Flex)
                _overlayRoot.style.display = DisplayStyle.Flex;

            _rootW = _overlayRoot.worldBound.width;
            _rootH = _overlayRoot.worldBound.height;
            if (_rootW <= 0f || _rootH <= 0f) return;

            TickPopups();
        }

        // ═══════════════ 루트 부착 ═══════════════

        private void AttachToRoot(VisualElement root)
        {
            _overlayRoot = new VisualElement();
            _overlayRoot.name = "RestaurantCoinPopupOverlay";
            _overlayRoot.style.position = Position.Absolute;
            _overlayRoot.style.left = 0f; _overlayRoot.style.right = 0f;
            _overlayRoot.style.top = 0f;  _overlayRoot.style.bottom = 0f;
            _overlayRoot.pickingMode = PickingMode.Ignore;   // 클릭 통과 필수(이름표 계약 동일)
            // index 1 — 이름표(0) 위, 창들 아래(창과 겹침 방지).
            root.Insert(1, _overlayRoot);
        }

        private void RefreshCamera()
        {
            if (_cam != null && Time.unscaledTime < _camNextRefresh) return;
            _cam = Camera.main;
            _camNextRefresh = Time.unscaledTime + CamRefreshInterval;
        }

        // ═══════════════ 구매 이벤트 → 팝업 ═══════════════

        private void OnPurchase(NationType nation, int villageIndex, Vector3 worldPos, int amount)
        {
            if (amount <= 0) return;   // 금액 미확보 마을 — 연출 스킵(0원 연출 방지)
            Popup p = AcquirePopup();
            if (p == null) return;     // 풀 상한 — 조용히 스킵
            p.worldPos = worldPos + Vector3.up * HeadOffsetM;
            p.lifeTotal = LifeTotalSeconds;
            p.lifeLeft = LifeTotalSeconds;
            p.amountLabel.text = "+" + amount + " G";
        }

        private Popup AcquirePopup()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                Popup free = _pool[i];
                if (!free.used)
                {
                    _pool.RemoveAt(i);
                    EnsureElements(free);
                    _active.Add(free);
                    return free;
                }
            }
            if (_active.Count >= PoolMax) return null;   // 상한 — 스킵
            int grow = Mathf.Min(PoolGrowStep, PoolMax - _active.Count - _pool.Count);
            for (int i = 0; i < Mathf.Max(1, grow); i++)
                _pool.Add(new Popup());
            Popup got = _pool[0];
            _pool.RemoveAt(0);
            EnsureElements(got);
            _active.Add(got);
            return got;
        }

        private void EnsureElements(Popup p)
        {
            if (p.root == null)
            {
                p.root = new VisualElement();
                p.root.style.position = Position.Absolute;
                p.root.pickingMode = PickingMode.Ignore;
                p.root.style.flexDirection = FlexDirection.Row;
                p.root.style.alignItems = Align.Center;
                p.root.style.backgroundColor = BackdropColor;
                p.root.style.borderTopLeftRadius = 10f;     // [100차] borderRadius 존재 안 함(이 버전) — 4코너 개별 지정
                p.root.style.borderTopRightRadius = 10f;
                p.root.style.borderBottomLeftRadius = 10f;
                p.root.style.borderBottomRightRadius = 10f;
                p.root.style.borderTopWidth = 1f;
                p.root.style.borderBottomWidth = 1f;
                p.root.style.borderLeftWidth = 1f;
                p.root.style.borderRightWidth = 1f;
                p.root.style.borderTopColor = CoinRim;
                p.root.style.borderBottomColor = CoinRim;
                p.root.style.borderLeftColor = CoinRim;
                p.root.style.borderRightColor = CoinRim;
                p.root.style.paddingTop = 3f; p.root.style.paddingBottom = 3f;
                p.root.style.paddingLeft = 6f; p.root.style.paddingRight = 8f;
                p.root.style.width = 74f;
                p.root.style.height = 22f;

                p.coin = new VisualElement();
                p.coin.style.width = 14f;
                p.coin.style.height = 14f;
                p.coin.style.borderTopLeftRadius = 7f;      // [100차] 4코너 개별 지정(원형)
                p.coin.style.borderTopRightRadius = 7f;
                p.coin.style.borderBottomLeftRadius = 7f;
                p.coin.style.borderBottomRightRadius = 7f;
                p.coin.style.backgroundColor = CoinGold;
                p.coin.style.borderTopWidth = 1f;
                p.coin.style.borderBottomWidth = 1f;
                p.coin.style.borderLeftWidth = 1f;
                p.coin.style.borderRightWidth = 1f;
                p.coin.style.borderTopColor = CoinRim;
                p.coin.style.borderBottomColor = CoinRim;
                p.coin.style.borderLeftColor = CoinRim;
                p.coin.style.borderRightColor = CoinRim;
                p.coin.style.marginRight = 4f;
                p.root.Add(p.coin);

                p.amountLabel = new Label();
                p.amountLabel.style.fontSize = 13f;
                p.amountLabel.style.color = AmountColor;
                p.amountLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                p.amountLabel.style.flexGrow = 1f;
                p.root.Add(p.amountLabel);
            }
            if (p.root.panel == null)
                _overlayRoot.Add(p.root);
            p.used = true;
            p.root.style.display = DisplayStyle.Flex;
            p.root.style.opacity = 1f;
        }

        // ═══════════════ 갱신/만료 ═══════════════

        private void TickPopups()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Popup p = _active[i];
                p.lifeLeft -= Time.deltaTime;
                if (p.lifeLeft <= 0f)
                {
                    Release(i);
                    continue;
                }

                float elapsed = p.lifeTotal - p.lifeLeft;
                Vector3 anchor = p.worldPos;
                Vector3 vp = _cam.WorldToViewportPoint(anchor);
                if (vp.z <= 0f) { p.root.style.display = DisplayStyle.None; continue; }   // 카메라 뒤

                float x = vp.x * _rootW;
                float y = (1f - vp.y) * _rootH - (elapsed / p.lifeTotal) * RisePixels;   // 부상

                if (x < -ScreenMargin || x > _rootW + ScreenMargin ||
                    y < -ScreenMargin || y > _rootH + ScreenMargin)
                {
                    p.root.style.display = DisplayStyle.None;
                    continue;
                }

                p.root.style.display = DisplayStyle.Flex;
                p.root.style.left = x - 37f;    // 폭 74 — 중앙 정렬
                p.root.style.top = y - 22f;     // 높이 22 — 앵커 하단 기준
                p.root.style.opacity = p.lifeLeft < FadeLastSeconds ? p.lifeLeft / FadeLastSeconds : 1f;
            }
        }

        private void Release(int index)
        {
            Popup p = _active[index];
            p.used = false;
            if (p.root != null) p.root.style.display = DisplayStyle.None;
            _active.RemoveAt(index);
            _pool.Add(p);
        }

        private void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Release(i);
        }
    }
}
