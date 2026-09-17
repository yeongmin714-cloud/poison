using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 상호작용 입력 코어 — 마우스 아래 대상 분류기 (순수 스태틱 유틸리티).
    /// 화면 좌표 → 카메라 레이 → RaycastAll → 태그/컴포넌트/이름으로 대상 종류 판정.
    /// Enemy / Farm / Gather / Ally / Terrain 중 가장 의미 있는(가장 앞선 비-지형) 종류를 반환.
    /// RTSCommandSystem/PlayerCombat/TopDownCameraController 본체를 수정하지 않고,
    /// 이들이 이미 게시한 태그·컴포넌트 규칙만 읽는다.
    /// </summary>
    public static class HoverTargetClassifier
    {
        public enum TargetKind
        {
            None,       // 레이캐스트 무적중 / 카메라 없음
            Enemy,      // 태그 Enemy|Monster|Guard|DraculaLord|Boss|Lord|DraculaGuard
            Farm,       // FarmPlot 컴포넌트 또는 이름 farm/경지
            Gather,     // HerbPickup 컴포넌트 또는 이름 gather/grass/herb/풀/약초
            Ally,       // 태그 Player|RecruitedSoldier 또는 GuardPlaceholder(IsRecruited)
            Terrain     // 나머지 지형 (첫 지형 히트)
        }

        private const float MaxDistance = 200f;

        /// <summary>
        /// 가장 가까운(레이 순서) 의미 있는 대상 종류를 반환.
        /// 자연스러운 앞 대상 우선 — Enemy &gt; Ally &gt; Farm &gt; Gather, 나머지는 Terrain.
        /// 예외 시 안전하게 None.
        /// </summary>
        public static TargetKind ClassifyAt(Vector2 screenPos)
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null) return TargetKind.None;

                Ray ray = cam.ScreenPointToRay(screenPos);
                RaycastHit[] hits = Physics.RaycastAll(ray, MaxDistance, ~0, QueryTriggerInteraction.Collide);
                if (hits == null || hits.Length == 0) return TargetKind.None;

                bool sawGround = false;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == null) continue;
                    GameObject go = hit.collider.gameObject;
                    if (go == null) continue;

                    TargetKind kind = ClassifyOne(go);
                    if (kind == TargetKind.Terrain)
                    {
                        sawGround = true;   // 첫 지형 히트 기억
                        continue;
                    }
                    // 첫 비-지형 의미 대상이면 즉시 반환 (전경 대상 우선)
                    return kind;
                }

                return sawGround ? TargetKind.Terrain : TargetKind.None;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HoverTargetClassifier] 분류 실패: {e.Message}");
                return TargetKind.None;
            }
        }

        /// <summary>단일 객체 분류 — 태그 → 컴포넌트 → 이름 순.</summary>
        private static TargetKind ClassifyOne(GameObject go)
        {
            // 1) 태그 기반 (Enemy 우선 — 적이 무조건 우선)
            string tag = go.tag;
            if (IsEnemyTag(tag)) return TargetKind.Enemy;

            // 2) Ally (아군 병사)
            if (tag == "Player" || tag == "RecruitedSoldier") return TargetKind.Ally;
            var guard = go.GetComponent<GuardPlaceholder>();
            if (guard != null && guard.IsRecruited) return TargetKind.Ally;

            // 3) Farm — 경작지(밀�) 프리셋 컴포넌트 또는 이름 매칭
            if (go.GetComponent<FarmPlot>() != null) return TargetKind.Farm;
            if (ContainsAny(go.name, "farm", "field", "경지", "밭")) return TargetKind.Farm;

            // 4) Gather — 약초(HerbPickup) 또는 이름 매칭
            if (go.GetComponent<HerbPickup>() != null) return TargetKind.Gather;
            if (ContainsAny(go.name, "gather", "grass", "herb", "풀", "약초", "나물")) return TargetKind.Gather;

            // 5) 지형
            return TargetKind.Terrain;
        }

        private static bool IsEnemyTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            return tag == "Enemy" || tag == "Monster" || tag == "Guard"
                || tag == "DraculaLord" || tag == "Boss" || tag == "Lord"
                || tag == "DraculaGuard";
        }

        private static bool ContainsAny(string s, params string[] keys)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string lower = s.ToLowerInvariant();
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] != null && lower.IndexOf(keys[i].ToLowerInvariant(), System.StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }
}
