using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// O3 C-O3-03: 컨텐츠 의존성 게이팅 — 레벨대별 컨텐츠 개방 체계.
    ///
    /// 목표 페이스 (docs/PHASE_O3_LEVEL_CURVE.md §4) 기반 정적 게이트 표:
    /// 플레이어 Lv1~10 링1 / 10~20 링2 / 20~30 링3 / 30~40 링4 / 40~50 황제국.
    ///
    /// - 미등록 컨텐츠 id는 항상 개방 (게이팅 없음 정책 — 오픈월드 진입 장벽 최소화).
    /// - Core 전용(static) — Systems 참조 불가 규약 준수 (PlayerStats(Core)만 조회).
    /// - 잠금 안내는 가장 얕은 진입점(고용/열기/확장 시도)에서 1회 차단 — 연쇄 최소화.
    /// foreach 규약 준수.
    /// </summary>
    public static class ContentGate
    {
        /// <summary>컨텐츠 게이트 정보 (단일 항목).</summary>
        public struct ContentGateInfo
        {
            public string contentId;
            public string displayName;
            public int minLevel;
            public string description;
        }

        // 게이트 표 — minLevel 오름차순 유지 (동일 레벨은 등록 순서). UI/문서는 GetAllGates() 사용.
        private static readonly ContentGateInfo[] _gates = new ContentGateInfo[]
        {
            new ContentGateInfo { contentId = "tavern_mercenary",      displayName = "용병 고용",       minLevel = 5,  description = "선술집/용병 — 초반 보호" },
            new ContentGateInfo { contentId = "bomb_craft",            displayName = "폭탄 제작",       minLevel = 8,  description = "Phase 4 확장 — 중반 위험물" },
            new ContentGateInfo { contentId = "gem_chest",             displayName = "동굴 보석상자",   minLevel = 10, description = "Phase 29 — 링2 파밍" },
            new ContentGateInfo { contentId = "warehouse_expansion_2", displayName = "창고 2차 확장",   minLevel = 12, description = "C-O2-03 연계 — 성장 투자" },
            new ContentGateInfo { contentId = "warehouse_expansion_3", displayName = "창고 3차 확장",   minLevel = 20, description = "창고 최종 확장 — 후반 재고 관리" },
            new ContentGateInfo { contentId = "dracula_territory",     displayName = "드라큘라 영지",   minLevel = 20, description = "Phase 28 — 고위험" },
        };

        // ── 개방 판정 ─────────────────────────────────────────────────────

        /// <summary>게이트 개방 여부 — 미등록 id는 true (게이팅 없음 정책).</summary>
        public static bool IsUnlocked(string contentId, int playerLevel)
        {
            var gate = GetGateInfo(contentId);
            if (!gate.HasValue)
                return true;
            return playerLevel >= gate.Value.minLevel;
        }

        /// <summary>게이트 정보 조회 — 미등록이면 null. Nullable struct (CS0173 주의: HasValue/Value로 접근).</summary>
        public static ContentGateInfo? GetGateInfo(string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
                return null;

            foreach (var gate in _gates)
            {
                if (gate.contentId == contentId)
                    return gate;
            }
            return null;
        }

        // ── 잠금 안내 ─────────────────────────────────────────────────────

        /// <summary>
        /// 잠금 안내 메시지 — 현재 플레이어 레벨이 미달일 때만 생성.
        /// "용병 고용은 레벨 5부터 가능합니다. (현재 레벨 3)" 형식.
        /// 개방 상태거나 등록 없으면 "" 반환.
        /// </summary>
        public static string GetLockedMessage(string contentId)
        {
            int level = PlayerStats.Instance != null ? PlayerStats.Instance.Level : 1;
            return GetLockedMessage(contentId, level);
        }

        /// <summary>잠금 안내 메시지 (레벨 명시 오버로드 — 테스트/UI 배치 미생성 환경용).</summary>
        public static string GetLockedMessage(string contentId, int currentLevel)
        {
            var gate = GetGateInfo(contentId);
            if (!gate.HasValue)
                return "";
            if (currentLevel >= gate.Value.minLevel)
                return "";

            string name = gate.Value.displayName;
            return $"{name}{GetSubjectParticle(name)} 레벨 {gate.Value.minLevel}부터 가능합니다. (현재 레벨 {currentLevel})";
        }

        // ── 전체 목록 ─────────────────────────────────────────────────────

        /// <summary>전체 게이트 목록 (minLevel 오름차순) — 문서/UI용 방어 복사본.</summary>
        public static ContentGateInfo[] GetAllGates()
        {
            var list = new List<ContentGateInfo>(_gates.Length);
            foreach (var gate in _gates)
                list.Add(gate);
            return list.ToArray();
        }

        // ── Internal ──────────────────────────────────────────────────────

        /// <summary>한글 주격 조사 (은/는) — 마지막 글자 받침(종성) 유무로 판정.</summary>
        private static string GetSubjectParticle(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "은";

            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3)
                return "은"; // 한글 음절 범위 외 — 은 폴백

            int jongseong = (last - 0xAC00) % 28;
            return jongseong == 0 ? "는" : "은";
        }
    }
}
