using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [73차 Phase B] 병사 상태 메시지 월드 말풍선 — StatusMessageChanged 구독 → 대상 머리 위(y+2.2m) TextMesh 4초 표시.
    ///
    /// 진단(73차): 뇌물/포섭/대화가 GuardPlaceholder._statusMessage 세팅만 하고 표시 경로가 없어
    /// 유저가 아무 변화를 못 봤다(결과 피드백 부재). NameplateDisplay/EInteractionPromptUTK 선례의
    /// 월드 텍스트 계열로 TextMesh만 표시(배경 프리미티브 없음 — 깔끔) + 카메라 빌보드(DamageFont 선례).
    ///
    /// 구독: 정적 부트(RuntimeInitializeOnLoadMethod — EInteractionPromptUTK 선례) + 1s 재스캔으로
    /// 씬 로드/동적 스폰 병사까지 커버. 이벤트 시그니처는 Action<string> 불변 — 발화 주체는 클로저 캡처로 식별.
    /// 연속 대사: 이벤트 재발화 시 타이머 리셋 + 텍스트 교체.
    /// </summary>
    public class GuardTalkBubble : MonoBehaviour
    {
        public const float DisplaySeconds = 4f;   // 말풍선 표시 시간(초)
        public const float HeadOffsetY = 2.2f;    // 대상 머리 위 오프셋(m)
        private const float ScanInterval = 1f;    // 미구독 병사 재스캔 간격(초)

        private static GuardTalkBubble _instance;
        private static readonly List<GuardPlaceholder> _subscribed = new List<GuardPlaceholder>(16);

        private GuardPlaceholder _guard;
        private TextMesh _text;
        private Transform _textT;
        private float _remaining;
        private float _scanTimer = ScanInterval;

        public static GuardTalkBubble Instance => _instance;

        /// <summary>전 씬 자동 배선 — 정적 구독 부트(EInteractionPromptUTK 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SubscribeAll();
            Ensure();
        }

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("GuardTalkBubble");
            go.AddComponent<GuardTalkBubble>();   // OnEnable에서 _instance 세팅
        }

        private void OnEnable() => _instance = this;
        private void OnDisable() { if (_instance == this) _instance = null; }

        /// <summary>미구독 병사 전수 구독 — 씬 로드/동적 스폰 커버(파괴된 참조 정리 겸임).</summary>
        private static void SubscribeAll()
        {
            var guards = Object.FindObjectsByType<GuardPlaceholder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var g in guards)
                Subscribe(g);
        }

        private static void Subscribe(GuardPlaceholder guard)
        {
            if (guard == null) return;

            // 파괴된 참조 정리(Unity fake-null).
            for (int i = _subscribed.Count - 1; i >= 0; i--)
            {
                if (_subscribed[i] == null) _subscribed.RemoveAt(i);
            }
            if (_subscribed.Contains(guard)) return;

            _subscribed.Add(guard);
            // 이벤트 시그니처(StatusMessageChanged: Action<string>) 불변 — 발화 주체는 클로저 캡처로 식별.
            GuardPlaceholder captured = guard;
            guard.StatusMessageChanged += message => Show(captured, message);
        }

        /// <summary>말풍선 표시 진입점 — StatusMessageChanged 구독 경유. 대상/메시지 유효 시에만.</summary>
        public static void Show(GuardPlaceholder guard, string message)
        {
            if (guard == null || string.IsNullOrEmpty(message)) return;
            if (_instance == null) Ensure();
            if (_instance != null) _instance.ShowInstance(guard, message);
        }

        /// <summary>표시 본체 — 리그 지연 생성 + 텍스트 교체 + 타이머 리셋(연속 대사 계약).</summary>
        public void ShowInstance(GuardPlaceholder guard, string message)
        {
            EnsureRig();
            _guard = guard;
            _text.text = message;
            _remaining = DisplaySeconds;   // 재발화 시 타이머 리셋 + 텍스트 교체
            PositionAt(guard);
            enabled = true;   // 빌보드/카운트다운 재개
        }

        // ── 월드 리그 — TextMesh만(쿼드 프리미티브 없음) ──
        private void EnsureRig()
        {
            if (_text != null) return;

            var labelGo = new GameObject("GuardTalkBubbleText");
            labelGo.transform.SetParent(transform, false);
            _textT = labelGo.transform;
            _text = labelGo.AddComponent<TextMesh>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 48;
            _text.characterSize = 0.28f / (_text.fontSize * 0.1f);   // world height = fontSize×charSize×0.1 ≈ 0.28m
            _text.anchor = TextAnchor.LowerCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = new Color32(0xF0, 0xF6, 0xFC, 0xFF);   // #F0F6FC
            var rend = labelGo.GetComponent<MeshRenderer>();
            if (rend != null && _text.font != null)
                rend.sharedMaterial = _text.font.material;
        }

        private void PositionAt(GuardPlaceholder guard)
        {
            if (_textT == null || guard == null) return;
            _textT.position = guard.transform.position + Vector3.up * HeadOffsetY;
        }

        private void Update()
        {
            // 미구독 병사 재스캔(동적 스폰/씬 전환 커버).
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = ScanInterval;
                SubscribeAll();
            }

            if (_remaining <= 0f) return;

            if (_guard == null)
            {
                _remaining = 0f;
                if (_text != null) _text.text = "";
                enabled = false;
                return;
            }

            _remaining -= Time.deltaTime;
            PositionAt(_guard);
            Camera cam = Camera.main;
            if (cam != null)
                _textT.rotation = cam.transform.rotation;   // 카메라 빌보드(DamageFont 선례)

            if (_remaining <= 0f)
            {
                _text.text = "";
                enabled = false;   // 표시 종료 — 빌보드/카운트다운 정지(재발화 시 재개)
            }
        }
    }
}
