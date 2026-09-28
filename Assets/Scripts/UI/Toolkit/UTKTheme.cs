using UnityEngine;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 공용 Fluent/GitHub-dark 디자인 토큰 팔레트 (2026-09-28 전면 통일).
    ///
    /// 기준: 기존 18종 Figma 창이 각자 인라인으로 정의하던 GitHubDark 팔레트를
    /// 중앙화한 것. 기존 창의 인라인 GitHubDark는 그대로 두고(회귀 0),
    /// 미통일(하드코딩) 창들이 이 토큰을 참조해 분위기를 전면 통일한다.
    ///
    /// 그래서 2026-09-28 전환 작업 전까지 존재하던 모든 UTK 창의 기준 팔레트는
    /// 이 클래스가 단일 소스가 된다.
    /// </summary>
    public static class UTKTheme
    {
        // ===== GitHub-dark / Fluent dark 토큰 (F-UI 2026-09-23 확정) =====

        /// <summary>최배경 (화면 밖/딤드). #0B0E14</summary>
        public static readonly Color BgBase = Hex(0x0B0E14);
        /// <summary>창 본체 패널. #161B22</summary>
        public static readonly Color Panel = Hex(0x161B22);
        /// <summary>보조 패널(타이틀바/카드/행). #21262D</summary>
        public static readonly Color PanelSub = Hex(0x21262D);
        /// <summary>강조(액센트). #58A6FF</summary>
        public static readonly Color Accent = Hex(0x58A6FF);
        /// <summary>희귀/활성/골드. #E3B341</summary>
        public static readonly Color Gold = Hex(0xE3B341);
        /// <summary>기본 텍스트. #F0F6FC</summary>
        public static readonly Color TextMain = Hex(0xF0F6FC);
        /// <summary>보조 텍스트. #8B949E</summary>
        public static readonly Color TextSub = Hex(0x8B949E);
        /// <summary>테두리/구분선. #2E343D</summary>
        public static readonly Color Stroke = Hex(0x2E343D);
        /// <summary>danger 레드(GitHub-dark). #F85149</summary>
        public static readonly Color Danger = Hex(0xF85149);
        /// <summary>success 그린(회복/체력). #3FB950</summary>
        public static readonly Color Success = Hex(0x3FB950);
        /// <summary>attention 옐로(경고). #D29922</summary>
        public static readonly Color Warn = Hex(0xD29922);

        /// <summary>danger hover. #DA3633</summary>
        public static readonly Color DangerHover = Hex(0xDA3633);
        /// <summary>accent hover. #79C0FF</summary>
        public static readonly Color AccentHover = Hex(0x79C0FF);
        /// <summary>success hover. #57AB5A</summary>
        public static readonly Color SuccessHover = Hex(0x57AB5A);

        /// <summary>모서리 반경 — 메인 패널.</summary>
        public const int RadiusMain = 8;
        /// <summary>모서리 반경 — 서브 패널/카드.</summary>
        public const int RadiusSub = 6;
        /// <summary>모서리 반경 — 배지/칩.</summary>
        public const int RadiusBadge = 4;

        private static Color Hex(uint rgb)
        {
            return new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }
    }
}