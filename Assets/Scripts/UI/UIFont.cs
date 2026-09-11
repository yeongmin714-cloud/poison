using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// P7-1/P7-2: UI 폰트 통합 헬퍼 (static, GUIStyle static 캐시 관례 준수).
    ///
    /// 폰트 로드 우선순위 (한글 서포트):
    ///   1. Assets/Resources/Fonts/NotoSansKR-VF.ttf  — 한글+라틴 고품질 산세리프 (OFL)
    ///   2. Assets/Resources/Fonts/malgun.ttf         — 맑은 고딕 폴백
    ///   3. 빌트인 LegacyRuntime.ttf / Arial.ttf       — 기존 동작과 동일한 최종 폴백
    ///
    /// - 커스텀 에셋 임포트 실패/부재 시에도 빌트인으로 자동 폴백되므로 기존 동작 보장.
    /// - 폰트는 프로세스 수명 동안 1회 로드 후 static 캐시 (매 프레임 Resources.Load 금지 관례).
    /// - 이 클래스는 ProjectName.UI(UI asmdef) 전용. Systems asmdef는 UI 참조 불가이므로
    ///   Systems 계층(MonsterHeadUI 등)은 Resources.Load&lt;Font&gt;("Fonts/...")를 직접 사용.
    /// </summary>
    public static class UIFont
    {
        // ================================================================
        // P7-2: 타입스케일 5단계 (fontSize 정규화 상수)
        // ================================================================

        /// <summary>대형 제목/연출 텍스트 (56~64) — 정보패널 대제목, 사망 연출 등</summary>
        public const int Display = 60;

        /// <summary>창 제목/아이템명 (36~40) — 윈도우 타이틀, 목록형 아이템명, 대형 버튼</summary>
        public const int Title = 38;

        /// <summary>본문/슬롯라벨 (22~28) — 탭, 설명 본문, 필드 라벨, 개수</summary>
        public const int Body = 24;

        /// <summary>캡션/보조 (16~18) — 보조 설명, 작은 슬롯 라벨</summary>
        public const int Caption = 17;

        /// <summary>숫자/배지 (12~14) — 내구도, 카운트다운, HP 수치, Lv 표기</summary>
        public const int Badge = 13;

        // ================================================================
        // 폰트 로드 (static 캐시)
        // ================================================================

        private static Font _cachedFont;
        private static bool _loadAttempted;

        /// <summary>
        /// UI 전역 공용 폰트. 한글 서포트 커스텀 폰트(Noto Sans KR) 우선,
        /// 실패 시 기존과 동일한 빌트인 폴백. 결과는 static 캐시 (1회 로드).
        /// </summary>
        public static Font Load()
        {
            if (_cachedFont != null) return _cachedFont;
            if (_loadAttempted) return null;
            _loadAttempted = true;

            // 1순위: Noto Sans KR Variable (Assets/Resources/Fonts/) — 한글+라틴
            try
            {
                Font noto = Resources.Load<Font>("Fonts/NotoSansKR-VF");
                if (noto != null)
                {
                    _cachedFont = noto;
                    return _cachedFont;
                }
            }
            catch { /* 다음 후보 진행 */ }

            // 2순위: 맑은 고딕 폴백 (한글 확정 서포트)
            try
            {
                Font malgun = Resources.Load<Font>("Fonts/malgun");
                if (malgun != null)
                {
                    _cachedFont = malgun;
                    return _cachedFont;
                }
            }
            catch { /* 다음 후보 진행 */ }

            // 최종 폴백: 빌트인 (기존 GetBuiltinResource 동작과 동일)
            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 2022.2+
            }
            catch { /* 구버전 폴백 진행 */ }

            if (_cachedFont == null)
            {
                try
                {
                    _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); // 구버전 Unity
                }
                catch { /* null 반환 — 호출부 기존 폴백 정책 유지 */ }
            }
            return _cachedFont;
        }
    }
}
