using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// ui 예시 2 스펙 좌하단 스테이터스 게이지(P24 재구성).
    /// 좌측 HP바(HPBarUTK — 베이크 프레임+fill+팁글로우+수치) + 우측 스태미너 원형 게이지
    /// (UTKCircularGauge — 그린 fill/다크 트랙/세그먼트 눈금 12, 중앙 GaugeStaminaIcon).
    /// 기존 이중 원형 링 + 구(舊) 하트/번개 아이콘 구성(P22-5)은 폐기 — 구 아이콘 리소스 참조는 제거됨.
    /// 데이터: PlayerHealth.CurrentHP/MaxHP + PlayerMovement.Stamina/MaxStamina(250ms 폴링).
    /// [P23-3] 모든 요소 pickingMode=Ignore — 오버레이 클릭 흡수 방지.
    /// </summary>
    public class StatusGaugesUTK : VisualElement
    {
        private static StatusGaugesUTK _instance;

        private HPBarUTK _hp;
        private UTKCircularGauge _stamina;
        private IVisualElementScheduledItem _poll;

        private const float StaminaSize = 92f;
        private const float Gap = 14f;               // HP바-스태미너 간격
        private const float IconSize = 64f;

        public static StatusGaugesUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new StatusGaugesUTK();
            root.Add(_instance);
            Debug.Log("[StatusGaugesUTK][P24] 스태터스 게이지(HP바+스태미너 링) 부착");
            return _instance;
        }

        private PlayerMovement _pmCache;

        private StatusGaugesUTK()
        {
            name = "StatusGauges";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 18f;
            style.bottom = 14f;
            style.width = HPBarUTK.BarWidth + Gap + StaminaSize;   // 198 + 14 + 92 = 304
            style.height = 96f;

            // HP바 — 좌측 배치, 하단 정렬
            _hp = new HPBarUTK();
            _hp.style.position = Position.Absolute;
            _hp.style.left = 0f;
            _hp.style.bottom = 0f;
            Add(_hp);
            _hp.Set(1f, 0f, 0f);   // 첫 폴링 전 기본 상태(수치 라벨은 max=0으로 숨김)

            // 스태미너 원형 게이지 — HP바 우측 14px 간격, 하단 정렬
            _stamina = new UTKCircularGauge();
            _stamina.style.width = StaminaSize;
            _stamina.style.height = StaminaSize;
            _stamina.style.position = Position.Absolute;
            _stamina.style.left = HPBarUTK.BarWidth + Gap;   // 212
            _stamina.style.bottom = 0f;
            _stamina.FillColor = new Color(0.25f, 0.8f, 0.4f, 1f);
            _stamina.ShowSegmentTicks(true);
            Add(_stamina);

            // 중앙 스태미너 아이콘 — 게이지 정중앙 64×64(스케일 투 핏)
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = IconSize;
            icon.style.height = IconSize;
            icon.style.position = Position.Absolute;
            icon.style.left = HPBarUTK.BarWidth + Gap + (StaminaSize - IconSize) * 0.5f;   // 226
            icon.style.bottom = (StaminaSize - IconSize) * 0.5f;                            // 14
            icon.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/GaugeStaminaIcon"));
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            Add(icon);

            UTKWindowBase.ApplyUIToolkitFont(this);

            // 250ms 폴링 — 값 변화만 갱신(Set 더티 체크 / CommitIfDirty)
            _poll = schedule.Execute(Poll).Every(250);
        }

        private void Poll()
        {
            var ph = PlayerHealth.Instance;
            if (ph != null && ph.MaxHP > 0f)
                _hp.Set(Mathf.Clamp01(ph.CurrentHP / ph.MaxHP), ph.CurrentHP, ph.MaxHP);
            else
                _hp.Set(1f, 0f, 0f);

            // [P22-5] 플레이어 캐시 — 씬 전환(P19 월드 언로드/재로드) 대비 재탐색
            if (_pmCache == null)
                _pmCache = Object.FindAnyObjectByType<PlayerMovement>();
            var pm = _pmCache;
            float stFrac = (pm != null && pm.MaxStamina > 0f) ? Mathf.Clamp01(pm.Stamina / pm.MaxStamina) : 1f;
            _stamina.Fraction = stFrac;

            _stamina.CommitIfDirty();
        }
    }
}
