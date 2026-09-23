using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P22-5 — 좌하단 원형 스테이터스 게이지(UTK).
    /// ui 예시 2 스타일: 체력(레드 링, 안쪽) + 스태미너(그린 링, 바깥쪽) 이중 원형 링,
    /// 중앙 베이크 아이콘(GaugeHeart/GaugeBolt). IMGUI 스태미나 바/하트 HUD 대체.
    /// 링은 UTKCircularGauge 벡터 호 — 시계방향 fill(값 감소 = 시계방향 소모).
    /// 데이터: PlayerHealth.CurrentHP/MaxHP + PlayerMovement.Stamina/MaxStamina(250ms 폴링).
    /// </summary>
    public class StatusGaugesUTK : VisualElement
    {
        private static StatusGaugesUTK _instance;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 상태 게이지 한정 인라인 오버라이드 — 링 fill 색만 GitHub-dark
        //  토큰(danger 레드/success 그린)으로 정렬. 데이터/폴링 로직 무수정.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color Danger  = Hex(0xF85149);   // 체력 링 — danger 레드
            public static readonly Color Success = Hex(0x3FB950);   // 스태미너 링 — success 그린

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        private UTKCircularGauge _hp;
        private UTKCircularGauge _stamina;
        private IVisualElementScheduledItem _poll;

        public static StatusGaugesUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new StatusGaugesUTK();
            root.Add(_instance);
            Debug.Log("[StatusGaugesUTK][P22-5] 원형 스테이터스 게이지 부착");
            return _instance;
        }

        private StatusGaugesUTK()
        {
            name = "StatusGauges";
            style.position = Position.Absolute;
            style.left = 18f;
            style.bottom = 14f;
            style.width = 132f;
            style.height = 132f;

            // 바깥 링: 스태미너(그린)
            _stamina = new UTKCircularGauge();
            _stamina.style.width = 132f;
            _stamina.style.height = 132f;
            _stamina.style.position = Position.Absolute;
            _stamina.style.left = 0f;
            _stamina.style.top = 0f;
            _stamina.FillColor = GitHubDark.Success;   // [GitHub-dark] success 그린 #3FB950
            Add(_stamina);

            // 안쪽 링: 체력(레드) — 스태미너 링 안에 겹침
            _hp = new UTKCircularGauge();
            _hp.style.width = 96f;
            _hp.style.height = 96f;
            _hp.style.position = Position.Absolute;
            _hp.style.left = 18f;
            _hp.style.top = 18f;
            _hp.FillColor = GitHubDark.Danger;   // [GitHub-dark] danger 레드 #F85149
            Add(_hp);

            // 중앙 아이콘: 하트(베이크 PNG) + 좌상단 소형 번개
            var heart = new VisualElement();
            heart.style.width = 34f;
            heart.style.height = 34f;
            heart.style.position = Position.Absolute;
            heart.style.left = 49f;
            heart.style.top = 49f;
            heart.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/GaugeHeart"));
            heart.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            Add(heart);

            var bolt = new VisualElement();
            bolt.style.width = 22f;
            bolt.style.height = 22f;
            bolt.style.position = Position.Absolute;
            bolt.style.left = 8f;
            bolt.style.top = 8f;
            bolt.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/GaugeBolt"));
            bolt.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            Add(bolt);

            UTKWindowBase.ApplyUIToolkitFont(this);

            // 250ms 폴링 — 값 변화만 다시 그림(CommitIfDirty)
            _poll = schedule.Execute(Poll).Every(250);
        }

        private PlayerMovement _pmCache;

        private void Poll()
        {
            var ph = PlayerHealth.Instance;
            float hpFrac = (ph != null && ph.MaxHP > 0f) ? Mathf.Clamp01(ph.CurrentHP / ph.MaxHP) : 1f;
            _hp.Fraction = hpFrac;

            // [P22-5] 플레이어 캐시 — 씬 전환(P19 월드 언로드/재로드) 대비 재탐색
            if (_pmCache == null)
                _pmCache = Object.FindAnyObjectByType<PlayerMovement>();
            var pm = _pmCache;
            float stFrac = (pm != null && pm.MaxStamina > 0f) ? Mathf.Clamp01(pm.Stamina / pm.MaxStamina) : 1f;
            _stamina.Fraction = stFrac;

            _hp.CommitIfDirty();
            _stamina.CommitIfDirty();
        }
    }
}
