using ProjectName.Core;
using UnityEngine;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 상호작용 입력 코어 — 마우스 아래 대상 분류기 (순수 스태틱 유틸리티).
    /// 화면 좌표 → 카메라 레이 → RaycastAll → 태그/컴포넌트로 대상 종류 판정.
    /// 이름 매칭 없음 — 비인터랙티브 환경 오브젝트(풀 장식 등)까지 표식으로 오분류되는 것 방지(테스트 40).
    /// Mine / Enemy / Farm / Gather / Ally / Terrain 중 가장 의미 있는(가장 앞선 비-지형) 종류를 반환.
    /// RTSCommandSystem/PlayerCombat/TopDownCameraController 본체를 수정하지 않고,
    /// 이들이 이미 게시한 태그·컴포넌트 규칙만 읽는다.
    /// </summary>
    public static class HoverTargetClassifier
    {
        public enum TargetKind
        {
            None,       // 레이캐스트 무적중 / 카메라 없음
            Enemy,      // 태그 Enemy|Monster|Guard|DraculaLord|Boss|Lord|DraculaGuard
            Farm,       // FarmPlot 컴포넌트 보유 시
            Gather,     // HerbPickup 컴포넌트(부모 체인 포함) 보유 시
            Ally,       // 태그 Player|RecruitedSoldier 또는 GuardPlaceholder(IsRecruited)
            Terrain,    // 나머지 지형 (첫 지형 히트)
            Mine        // ResourceNode(Wood/Stone/IronOre) 자원 노드 — 광질 대상
        }

        private const float MaxDistance = 200f;

        /// <summary>
        /// 가장 가까운(레이 순서) 의미 있는 대상 종류를 반환.
        /// 자연스러운 앞 대상 우선 — Mine &gt; Enemy &gt; Ally &gt; Farm &gt; Gather, 나머지는 Terrain.
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

        /// <summary>단일 객체 분류 — 태그 → 컴포넌트 순. 이름 매칭 없음(테스트 40).</summary>
        private static TargetKind ClassifyOne(GameObject go)
        {
            // 0) Mine — 자원 노드(Wood/Stone/IronOre) 최우선 판정 (적/밭 태그 충돌보다 먼저)
            if (go.GetComponentInParent<ResourceNode>() != null) return TargetKind.Mine;

            // 1) 태그 기반 (Enemy 우선 — 적이 무조건 우선)
            string tag = go.tag;
            if (IsEnemyTag(tag)) return TargetKind.Enemy;

            // 2) Ally (아군 병사)
            if (tag == "Player" || tag == "RecruitedSoldier") return TargetKind.Ally;
            var guard = go.GetComponent<GuardPlaceholder>();
            if (guard != null && guard.IsRecruited) return TargetKind.Ally;

            // 3) Farm — FarmPlot 컴포넌트 보유 시만. 이름 매칭 제거 — 비인터랙티브 환경
            //    오브젝트까지 밭으로 오분류되는 것 방지(테스트 40).
            if (go.GetComponent<FarmPlot>() != null) return TargetKind.Farm;

            // 4) Gather — HerbPickup 컴포넌트 보유 시만(부모 체인 포함 — 약초 프리팹 하위
            //    콜라이더 히트도 커버). 이름 매칭 제거 — 풀 장식(Grass 등)이 삽 커서로
            //    오분류되는 것 방지(테스트 40). Farm이 먼저 판정되므로 Ready 밭(FarmPlot에
            //    AddComponent된 HerbPickup)은 Farm으로 유지된다.
            if (go.GetComponentInParent<HerbPickup>() != null) return TargetKind.Gather;

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
    }
}
