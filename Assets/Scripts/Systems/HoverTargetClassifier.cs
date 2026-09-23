using ProjectName.Core;
using UnityEngine;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 상호작용 입력 코어 — 마우스 아래 대상 분류기 (순수 스태틱 유틸리티).
    /// 화면 좌표 → 카메라 레이 → RaycastAll → 태그/컴포넌트로 대상 종류 판정.
    /// 이름 매칭 없음 — 비인터랙티브 환경 오브젝트(풀 장식 등)까지 표식으로 오분류되는 것 방지(테스트 40).
    /// 가장 가까운(레이 순서) 첫 비-지형 의미 대상 우선(전경 대상 우선).
    /// 몬스터(AnimalAI) vs 적 병사(GuardPlaceholder 미영입) vs NPC 구분 — 커서 컨텍스트용(UTKCursorOverlay).
    /// RTSCommandSystem/PlayerCombat/TopDownCameraController 본체를 수정하지 않고,
    /// 이들이 이미 게시한 태그·컴포넌트 규칙만 읽는다.
    /// </summary>
    public static class HoverTargetClassifier
    {
        public enum TargetKind
        {
            None,        // 레이캐스트 무적중 / 카메라 없음
            Enemy,       // (폴백) 태그 Enemy|Monster|Guard|DraculaLord|Boss|Lord|DraculaGuard — AnimalAI/GuardPlaceholder 없는 태그 적(보스 등)
            Farm,        // FarmPlot 컴포넌트 보유 시
            Gather,      // HerbPickup 컴포넌트(부모 체인 포함) 보유 시
            Ally,        // 태그 Player|RecruitedSoldier 또는 GuardPlaceholder(IsRecruited)
            Terrain,     // 나머지 지형 (첫 지형 히트)
            Mine,        // ResourceNode(Wood/Stone/IronOre) 자원 노드 — 광질 대상
            Monster,     // AnimalAI(부모 체인 포함) 보유 몬스터 — 기본 공격(과녁) / Ctrl 정보(돋보기)
            EnemyGuard,  // 적 병사 — GuardPlaceholder(미영입) — 기본 공격(과녁) / Ctrl 대화(말풍선)
            NPC,         // 대화가능 NPC(NpcQuestGiver/NPCAmbientDialogue, 병사·몬스터 아닌 캐릭터) — 대화(말풍선)
            Door,        // LockedDoor(부모 체인 포함) — 열쇠
            Cook,        // 요리 스테이션(ProjectName.UI.CookingStation 계열, 리플렉션 판정) — 불
            Shop         // 상점(ProjectName.UI.ShopPlaceholder, 리플렉션 판정) — 돈
        }

        private const float MaxDistance = 200f;

        // ===== ProjectName.UI 타입 리플렉션 캐시 =====
        // Systems→UI 직접 참조 금지(asmdef 단방향: UI→Systems만 허용). VillageBuilder와 동일한
        // Type.GetType(어셈블리 한정) 선례. UI 어셈블리 미로드 환경에서는 null → 해당 종류 미판정(지형 폴백).
        private static readonly System.Type CookStationType =
            System.Type.GetType("ProjectName.UI.CookingStation, ProjectName.UI");
        private static readonly System.Type CookBenchType =
            System.Type.GetType("ProjectName.UI.CookingBench, ProjectName.UI");
        private static readonly System.Type ShopType =
            System.Type.GetType("ProjectName.UI.ShopPlaceholder, ProjectName.UI");

        /// <summary>
        /// 가장 가까운(레이 순서) 의미 있는 대상 종류를 반환.
        /// 자연스러운 앞 대상 우선 — 전경 대상(몬스터/적병사/문/요리/상점/광질/채집/밭) 우선, 나머지는 Terrain.
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

        /// <summary>단일 객체 분류 — 컴포넌트(부모 체인) → 태그 순. 이름 매칭 없음(테스트 40).</summary>
        private static TargetKind ClassifyOne(GameObject go)
        {
            // 0) Mine — 자원 노드(Wood/Stone/IronOre) 최우선 판정 (적/밭 태그 충돌보다 먼저)
            if (go.GetComponentInParent<ResourceNode>() != null) return TargetKind.Mine;

            // 1) Monster — AnimalAI 보유. 부모 루트에 컴포넌트, 자식 콜라이더 히트 커버(GetComponentInParent 필수).
            //    적 태그(Monster/Enemy 등)라도 AnimalAI가 있으면 몬스터로 우선 분류 —
            //    몬스터 vs 적병사 커서 구분이 사용자 요구의 핵심.
            if (go.GetComponentInParent<AnimalAI>() != null) return TargetKind.Monster;

            // 2) Door — LockedDoor(부모 체인 포함 — 문 프리팹 하위 콜라이더 히트 커버)
            if (go.GetComponentInParent<LockedDoor>() != null) return TargetKind.Door;

            // 3) Cook / Shop — ProjectName.UI 컴포넌트는 리플렉션으로만 판정(asmdef 순환 회피).
            //    GetComponentInParent(Type) 비제네릭 오버로드로 부모 체인 검색.
            if (GetComponentInParentUi(go, CookStationType) || GetComponentInParentUi(go, CookBenchType))
                return TargetKind.Cook;
            if (GetComponentInParentUi(go, ShopType)) return TargetKind.Shop;

            // 4) 아군 병사 태그 (기존 우선순위 유지 — GuardPlaceholder 판정보다 먼저)
            string tag = go.tag;
            if (tag == "Player" || tag == "RecruitedSoldier") return TargetKind.Ally;

            // 5) 병사 — GuardPlaceholder(부모 체인 포함). 영입 여부로 아군/적병사 분기.
            //    적 태그(Guard 등)를 단 적병사도 여기서 EnemyGuard로 잡힌다(태그 Enemy 폴백보다 먼저).
            var guard = go.GetComponentInParent<GuardPlaceholder>();
            if (guard != null) return guard.IsRecruited ? TargetKind.Ally : TargetKind.EnemyGuard;

            // 6) NPC — 대화가능 마커(병사/몬스터 아닌 캐릭터). 마을 주민(NpcQuestGiver) 등.
            if (go.GetComponentInParent<NpcQuestGiver>() != null
                || go.GetComponentInParent<NPCAmbientDialogue>() != null)
                return TargetKind.NPC;

            // 7) (폴백) 적 태그 — AnimalAI/GuardPlaceholder 없는 태그 적(보스/드라큘라 등).
            //    기존 Enemy 경로 유지 — ContextCommandRouter의 공격 폴백 스위치와 호환.
            if (IsEnemyTag(tag)) return TargetKind.Enemy;

            // 8) Farm — FarmPlot 컴포넌트 보유 시만. 이름 매칭 제거 — 비인터랙티브 환경
            //    오브젝트까지 밭으로 오분류되는 것 방지(테스트 40).
            if (go.GetComponent<FarmPlot>() != null) return TargetKind.Farm;

            // 9) Gather — HerbPickup 컴포넌트 보유 시만(부모 체인 포함 — 약초 프리팹 하위
            //    콜라이더 히트도 커버). 이름 매칭 제거 — 풀 장식(Grass 등)이 커서로
            //    오분류되는 것 방지(테스트 40). Farm이 먼저 판정되므로 Ready 밭(FarmPlot에
            //    AddComponent된 HerbPickup)은 Farm으로 유지된다.
            if (go.GetComponentInParent<HerbPickup>() != null) return TargetKind.Gather;

            // 10) 지형
            return TargetKind.Terrain;
        }

        /// <summary>ProjectName.UI 타입을 부모 체인에서 리플렉션 검색 — 타입 미로드 시 false.</summary>
        private static bool GetComponentInParentUi(GameObject go, System.Type uiType)
        {
            return uiType != null && go.GetComponentInParent(uiType) != null;
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
