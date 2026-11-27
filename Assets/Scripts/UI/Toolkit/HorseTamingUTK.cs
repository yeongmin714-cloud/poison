using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // HorseMountSystem

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [무번호 85-P4] 말 길들이기 미니게임 — 낚시게임(FishingUTK)과 동일한 UI 형식의 신규 UTK
    /// (UTKWindowBase 파생 · 자가부트 싱글턴 · 게이지 바 미니게임 · 성공/실패 이원 판정).
    /// 규약: 3라운드 중 2회 성공 = 길들임 / 2회 실패 = 재도전. 마커는 결정론 스윕(Random 금지 — VFX 계약 승계).
    /// 어셈블리 순환 방지: 시스템(HorseMountSystem)의 정적 이벤트를 구독해 열리고, 결과는 콜백 반환.
    /// </summary>
    public class HorseTamingUTK : UTKWindowBase
    {
        static HorseTamingUTK _instance;
        static Updater _updater;

        const int TickMs = 16;
        const int RoundsNeeded = 2;      // 3라운드 중 2회 성공
        const int RoundsTotal = 3;
        const float MarkerSpeed = 1.35f; // 초당 왕복 주기 배수(결정론 스윕)

        Label _statusLabel;
        Label _resultLabel;
        VisualElement _marker;
        VisualElement _zone;

        System.Action<bool> _onComplete;
        string _breed = "말";
        int _round, _successes;
        float _markerT, _markerDir = 1f;
        float _zoneCenter = 0.5f, _zoneHalf = 0.14f;
        bool _resolved;

        public HorseTamingUTK() : base("말 길들이기", new Vector2(380f, 260f))
        {
            var info = AddContentPanel("taming");
            info.style.flexDirection = FlexDirection.Column;
            info.style.paddingTop = 8f;

            _statusLabel = new Label("야생 말에게 안장을 채웁니다.");
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            info.Add(_statusLabel);

            // 게이지 트랙 — 목표 구간(초록) + 마커(흰)
            var track = new VisualElement();
            track.style.height = 34f;
            track.style.marginTop = 14f;
            track.style.marginBottom = 8f;
            track.style.backgroundColor = new Color(0.06f, 0.08f, 0.10f, 0.9f);
            info.Add(track);

            _zone = new VisualElement();
            _zone.style.position = Position.Absolute;
            _zone.style.height = 30f;
            _zone.style.backgroundColor = new Color(0.25f, 0.62f, 0.35f, 0.55f);
            track.Add(_zone);

            _marker = new VisualElement();
            _marker.style.position = Position.Absolute;
            _marker.style.width = 6f;
            _marker.style.height = 30f;
            _marker.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);
            track.Add(_marker);

            _resultLabel = new Label("스페이스바 — 마커를 초록 구간에 정지하세요.");
            _resultLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _resultLabel.style.marginTop = 6f;
            info.Add(_resultLabel);
        }

        /// <summary>FishingUTK 선례 — 전 씬 자가부트 + 시스템 이벤트 구독(어셈블리 역방향 알림).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            Ensure();
            HorseMountSystem.OnTamingRequested += Begin;
        }

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new HorseTamingUTK();
            var go = new GameObject("HorseTamingUTK_UPD");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
            _updater.window = _instance;
            Debug.Log("[HorseTameUTK] 인스턴스 생성");
        }

        class Updater : MonoBehaviour { public HorseTamingUTK window; void Update() { window?.GameTick(); } }

        public static void Begin(string breed, System.Action<bool> onComplete)
        {
            Ensure();
            _instance._breed = breed;
            _instance._onComplete = onComplete;
            _instance._round = 0;
            _instance._successes = 0;
            _instance._resolved = false;
            _instance.ResetRound();
            _instance._statusLabel.text = "야생 " + breed + "에게 안장을 채웁니다.";
            _instance._resultLabel.text = "스페이스바 — 마커를 초록 구간에 정지하세요.";
            _instance.Show();
        }

        void ResetRound()
        {
            _round++;
            _markerT = 0f;
            _markerDir = 1f;
            // 결정론 구간 변주 — 라운드마다 중심/폭 고정 패턴(Random 금지)
            _zoneCenter = _round % 2 == 0 ? 0.35f : 0.62f;
            _zoneHalf = 0.16f - 0.02f * _round;   // 라운드마다 좁아짐
            _zone.style.left = new Length((_zoneCenter - _zoneHalf) * 100f, LengthUnit.Percent);
            _zone.style.width = new Length(_zoneHalf * 2f * 100f, LengthUnit.Percent);
        }

        public void GameTick()
        {
            if (!IsOpen || _resolved) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Finish(false, "도중 포기"); return; }

            // 결정론 스윕 마커
            _markerT += _markerDir * MarkerSpeed * Time.deltaTime;
            if (_markerT > 1f) { _markerT = 1f; _markerDir = -1f; }
            if (_markerT < 0f) { _markerT = 0f; _markerDir = 1f; }
            _marker.style.left = new Length(_markerT * 100f, LengthUnit.Percent);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                bool hit = Mathf.Abs(_markerT - _zoneCenter) <= _zoneHalf;
                if (hit) _successes++;
                _resultLabel.text = hit ? $"적중! ({_successes}/{RoundsNeeded})" : "빗나감!";
                Debug.Log("[HorseTameUTK] 라운드 " + _round + " — " + (hit ? "적중" : "미적중"));
                if (_successes >= RoundsNeeded) { Finish(true, "말이 길들여졌다!"); return; }
                if (_round >= RoundsTotal) { Finish(false, "말이 흥분을 가라앉히지 못했다."); return; }
                ResetRound();
            }
        }

        void Finish(bool success, string message)
        {
            if (_resolved) return;
            _resolved = true;
            _resultLabel.text = (success ? "✅ " : "❌ ") + message;
            Debug.Log("[HorseTameUTK] 결과: " + (success ? "성공" : "실패") + " — " + message);
            var cb = _onComplete;
            _onComplete = null;
            Hide();
            cb?.Invoke(success);   // 시스템 콜백(길들임/재도전) — 창 숨김 후 직접 반환
        }

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null) root.Add(this);
        }
    }
}
