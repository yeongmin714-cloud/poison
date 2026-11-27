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
            Wood,        // ResourceNode(NodeType==Wood) — 벌목 대상 (도끼 커서)
            Monster,     // AnimalAI(부모 체인 포함) 보유 몬스터 — 기본 공격(과녁) / Ctrl 정보(돋보기)
            EnemyGuard,  // 적 병사 — GuardPlaceholder(미영입) — 기본 공격(과녁) / Ctrl 대화(말풍선)
            NPC,         // 대화가능 NPC(NpcQuestGiver/NPCAmbientDialogue, 병사·몬스터 아닌 캐릭터) — 대화(말풍선)
            Door,        // LockedDoor(부모 체인 포함) — 열쇠
            Cook,        // 요리 스테이션(ProjectName.UI.CookingStation 계열, 리플렉션 판정) — 불
            Shop,        // 상점 건물(ProjectName.UI.ShopPlaceholder, 리플렉션 판정) — 돈
            ShopNPC,     // 상점 NPC
            Lord,        // 영주
            LordDaughter, // [126차] 영주 딸(LordDaughterNPC) — 컴포넌트 기반(태그 없음), Enemy 폴백보다 먼저
            ArenaManager, // 아레나 베팅 관리인(로비 — 121차 ArenaBetManager) — 대화하기/베팅하기(피그마 261:3)
            TrainingHall, // [125차] 훈련소 시설(FacilityStationInteractable training_hall) — E키 훈련소 UI
            Fishing,      // [144차 D2-FISH] 낚시 스팟(물가 근접 — FishingSystem 근접 판정) — 낚시하기(피그마 163:4)
            Bed           // [144차 D2-SLEEP] 침대(Bed 컴포넌트) — 잠자기(피그마 163:45)
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
        private static readonly System.Type TerritoryNpcType =
            System.Type.GetType("ProjectName.UI.TerritoryNPCBehaviour, ProjectName.UI");

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
            // 0) Mine / Wood — 자원 노드 최우선 판정 (적/밭 태그 충돌보다 먼저).
            //    NodeType==Wood → Wood(벌목·도끼 커서), 그 외 → Mine(광질·곡괭이 커서).
            var rn = go.GetComponentInParent<ResourceNode>();
            if (rn != null)
                return rn.NodeType == ResourceNode.ResourceType.Wood ? TargetKind.Wood : TargetKind.Mine;

            // 1) Monster — AnimalAI 보유. 부모 루트에 컴포넌트, 자식 콜라이더 히트 커버(GetComponentInParent 필수).
            //    적 태그(Monster/Enemy 등)라도 AnimalAI가 있으면 몬스터로 우선 분류 —
            //    몬스터 vs 적병사 커서 구분이 사용자 요구의 핵심.
            if (go.GetComponentInParent<AnimalAI>() != null) return TargetKind.Monster;

            // 2) Door — LockedDoor(부모 체인 포함 — 문 프리팹 하위 콜라이더 히트 커버)
            if (go.GetComponentInParent<LockedDoor>() != null) return TargetKind.Door;
            // [144차 D2-SLEEP] Bed — 침대(부모 체인 포함). Door와 상호 배타(가구/문).
            if (go.GetComponentInParent<Bed>() != null) return TargetKind.Bed;

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

            // 6) Lord — Lord 계열 태그/컴포넌트는 Enemy 태그 폴백보다 먼저 판정.
            if (tag == "Lord" || tag == "DraculaLord"
                || go.GetComponentInParent<DraculaLord>() != null
                || go.GetComponentInParent<LordFeedTarget>() != null
                || go.GetComponentInParent<InvadedLordEncounter>() != null)
                return TargetKind.Lord;

            // 6.2) [126차] 영주 딸 — LordDaughterNPC(태그 없음)를 Enemy 태그 폴백보다 먼저 판정.
            if (go.GetComponentInParent<LordDaughterNPC>() != null) return TargetKind.LordDaughter;

            // 6.5) ArenaManager — 아레나 베팅 관리인(같은 어셈블리 컴포넌트 — 직접 판정, 121차).
            if (go.GetComponentInParent<ArenaBetManager>() != null) return TargetKind.ArenaManager;

            // 7) NPC — 상점 여부를 먼저 확인하고, 비상점은 기존 대화 마커 경로를 유지.
            //    TerritoryNPCBehaviour는 UI 어셈블리 타입이므로 리플렉션으로 부모 체인을 검색.
            bool hasTerritoryNpc = GetComponentInParentUi(go, TerritoryNpcType);
            if (hasTerritoryNpc && IsShopTerritoryNpc(go)) return TargetKind.ShopNPC;
            if (go.GetComponentInParent<NpcQuestGiver>() != null
                || go.GetComponentInParent<NPCAmbientDialogue>() != null
                || hasTerritoryNpc)
                return TargetKind.NPC;

            // 8) (폴백) 적 태그 — AnimalAI/GuardPlaceholder 없는 태그 적(보스/드라큘라 등).
            //    기존 Enemy 경로 유지 — ContextCommandRouter의 공격 폴백 스위치와 호환.
            if (IsEnemyTag(tag)) return TargetKind.Enemy;

            // 7.5) [125차] 훈련소 — FacilityStationInteractable(training_hall) 직접 판정(같은 어셈블리).
            //      기존 시설 4종은 분류 없음(기존 계약 유지) — 훈련소만 신규 kind(호버 커서 피드백 계약).
            var trainingFacility = go.GetComponentInParent<FacilityStationInteractable>();
            if (trainingFacility != null && trainingFacility.EffectId == FacilityPropBuilder.TrainingHallEffectId)
                return TargetKind.TrainingHall;

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

        /// <summary>
        /// [Phase D] 영주/영주딸/NPC/상점NPC/아레나 관리인의 실제 대상 컴포넌트를 반환(리플렉션 포함).
        /// InteractionPanelUTK(ProjectName.UI)가 이 컴포넌트에서 NPCInstance(NPCData 프로퍼티)를 꺼낸다.
        /// 해당 종류가 아니면 kind=None, target=null을 반환.
        /// </summary>
        public static object ResolveInteractionTarget(GameObject go, out TargetKind kind)
        {
            kind = ClassifyOne(go);   // 같은 클래스 내 private 호출 — 단일 객체 분류 재사용
            if (kind == TargetKind.Lord)
            {
                var invadedLord = go.GetComponentInParent<InvadedLordEncounter>();
                if (invadedLord != null) return invadedLord;
                var lordFeed = go.GetComponentInParent<LordFeedTarget>();
                if (lordFeed != null) return lordFeed;
                var draculaLord = go.GetComponentInParent<DraculaLord>();
                if (draculaLord != null) return draculaLord;
                return go;   // 태그(Lord)만으로 분류된 경우 폴백 — 게임오브젝트 반환
            }
            // [126차] 영주 딸 — LordDaughterNPC 컴포넌트를 반환(패널 인터랙션 진입 타겟 계약).
            if (kind == TargetKind.LordDaughter)
            {
                var daughter = go.GetComponentInParent<LordDaughterNPC>();
                if (daughter != null) return daughter;
            }
            // [125차 후속8] 아레나 관리인 — ArenaBetManager 컴포넌트를 반환(같은 어셈블리 직접 판정).
            //   ClassifyOne은 ArenaManager 분류를 이미 제공하나(6.5단계) 본 리졸버가 소비하지 않아
            //   좌클릭 라우팅(TryRoutePlainNpcInteraction)이 대상 획득에 실패했다 → 소비 경로 회복.
            //   InteractionPanelUTK 아레나 패널(대화하기/베팅하기, 피그마 261:3)은 target을
            //   ArenaBetManager로 캐스트 — 컴포넌트 그대로 전달한다.
            if (kind == TargetKind.ArenaManager)
            {
                var arenaManager = go.GetComponentInParent<ArenaBetManager>();
                if (arenaManager != null) return arenaManager;
            }
            if (kind == TargetKind.NPC || kind == TargetKind.ShopNPC)
                return go.GetComponentInParent(TerritoryNpcType);

            // [144차 D2-SLEEP] Bed — Bed 컴포넌트 반환(패널 액션 타겟 계약).
            if (kind == TargetKind.Bed)
            {
                var bed = go.GetComponentInParent<Bed>();
                if (bed != null) return bed;
            }
            // [144차 D2-LOCK] Door — LockedDoor 컴포넌트 반환(구: 리졸버 미소비 → 패널 개방 경로 부재).
            //   영주실 문(house_lord_room) 포함 전 잠긴 문 공통 — 163:64/163:83 라벨은 LocationId 분기.
            if (kind == TargetKind.Door)
            {
                var door = go.GetComponentInParent<LockedDoor>();
                if (door != null) return door;
            }

            kind = TargetKind.None;
            return null;
        }

        /// <summary>TerritoryNPCBehaviour와 해당 NPCData에서 상점 플래그를 리플렉션으로 확인.</summary>
        // [116차 R2] public 노출 — PlayerMovement F키 경로가 좌클릭 경로와 동일 분류를 재사용(단일소스).
        public static bool IsShopTerritoryNpc(GameObject go)
        {
            if (TerritoryNpcType == null) return false;
            Component component = go.GetComponentInParent(TerritoryNpcType);
            if (component == null) return false;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic;
            System.Type componentType = component.GetType();
            var isShopProperty = componentType.GetProperty("IsShopNPC", flags);
            if (isShopProperty != null && isShopProperty.PropertyType == typeof(bool)
                && (bool)isShopProperty.GetValue(component, null))
                return true;

            var npcDataProperty = componentType.GetProperty("NPCData", flags);
            if (npcDataProperty == null) return false;
            object npcData = npcDataProperty.GetValue(component, null);
            if (npcData == null) return false;

            System.Type npcDataType = npcData.GetType();
            var shopFlagProperty = npcDataType.GetProperty("isShopNPC", flags)
                ?? npcDataType.GetProperty("IsShopNPC", flags);
            if (shopFlagProperty != null && shopFlagProperty.PropertyType == typeof(bool))
                return (bool)shopFlagProperty.GetValue(npcData, null);

            var shopFlagField = npcDataType.GetField("isShopNPC", flags)
                ?? npcDataType.GetField("IsShopNPC", flags);
            return shopFlagField != null && shopFlagField.FieldType == typeof(bool)
                && (bool)shopFlagField.GetValue(npcData);
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
