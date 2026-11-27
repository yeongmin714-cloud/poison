using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 영주 딸 NPC — [126차] "영주 딸 등장·꽃 선물 호감도·프로포즈 결혼" 시스템.
    ///
    /// - 등장: [10-24 사용자 정정] 생존 영주(비PlayerOwned — 타 소속 영주 살아있음)의 성 내부
    ///   영주실(LordBed 앵커 인접)에 결정론 확률 40% 등장. 병사 포섭 후 적성 영지 진입 경로.
    ///   영지ID 키 FNV-1a 해시 기반 — 저장 불요(재계산), 동일 영지는 항상 동일 결과(결정론 계약).
    /// - 외형: 침략 영주(InvadedLordEncounter 99~128행)와 동일 캡슐 프리미티브 파이프라인 —
    ///   살짝 작게 + 연분홍 계열. 신규 태그 생성 금지 — 호버 분류는 컴포넌트 기반(HoverTargetClassifier).
    /// - 상호작용: 들꽃(flower_wild) 선물 → 호감도 +10(0~100 클램프), 호감도 ≥80에서 프로포즈 수락 →
    ///   결혼 상태 저장 + 해당 영지 loyaltyToPlayer +30(1회, 멱등).
    /// - 상태 단일소스는 TerritoryState(daughterAffection/daughterMarried) — 딸 GameObject는
    ///   재진입 시 재스폰되므로 상태를 보관하지 않는다.
    /// </summary>
    public class LordDaughterNPC : MonoBehaviour
    {
        public enum GiftResult
        {
            Success,        // 선물 완료 — 꽃 1개 소비 + 호감도 +10
            NoInventory,    // 인벤토리 참조 부재 — 소비 없음
            NoFlower,       // 들꽃 보유 0 — 소비 없음("선물할 들꽃이 없다" 피드백 계약)
            AffectionMaxed, // 호감도 100 — 추가 선물 거부(꽃 소비 없음)
            NoTerritory     // 영지 상태 조회 실패 — 소비 없음(잘못된 키 등)
        }

        public enum ProposeResult
        {
            Accepted,        // 수락 — 결혼 상태 설정 + 충성도 +30(1회)
            AlreadyMarried,  // 이미 부부 — 멱등(재가 없음)
            NotReady         // 게이트 미충족(호감도 &lt; 80 또는 상태 부재)
        }

        // ===== [126차] 설계 상수 — 스펙 §0/§3 고정값 =====
        public const int GiftAffectionGain = 10;              // 들꽃 1개 선물 호감도 증가
        public const int ProposeAffectionThreshold = 80;      // 프로포즈 버튼 노출/수락 게이트
        public const int MarriageLoyaltyBonus = 30;           // 결혼 1회 충성도 보너스(멱등)
        public const int SpawnChancePercent = 40;             // 영주실 등장 결정론 확률(%)
        public const int DaughterNamePoolSize = 12;           // 딸 이름 풀 크기(결정론 선택)

        /// <summary>선물 아이템 ID — PlayerInventory.Flower_Wild.id의 단일 소스.</summary>
        public const string FlowerItemId = "flower_wild";

        // 딸 이름 풀 12개(한국풍 여성명) — 영지ID 결정론 선택(스펙 §0).
        private static readonly string[] DaughterNames =
        {
            "수아", "하윤", "지우", "서연", "민서", "소연",
            "다은", "유진", "하린", "나연", "보라", "세라",
        };

        [SerializeField] private string _territoryKey = "";
        private string _displayName = "딸";
        private NameplateDisplay _nameplate;

        public string TerritoryKey => _territoryKey;
        public string DisplayName => _displayName;
        public NameplateDisplay Nameplate => _nameplate;

        /// <summary>현재 호감도 — TerritoryState 단일 소스 조회(상태 부재 시 0).</summary>
        public int Affection => GetState()?.daughterAffection ?? 0;

        /// <summary>결혼 여부 — TerritoryState 단일 소스 조회(상태 부재 시 false).</summary>
        public bool IsMarried => GetState()?.daughterMarried ?? false;

        // ================================================================
        // 결정론 등장 판정 / 스폰
        // ================================================================

        /// <summary>
        /// 영지 키 → 딸 등장 여부. FNV-1a 32비트(UTF-16 코드 유닛) % 100 &lt; 40 —
        /// Random/시간/전역 상태 미사용(동일 키는 항상 동일 결과, 저장 불요). null/빈 키는 false.
        /// </summary>
        public static bool HasLordDaughter(string territoryKey)
        {
            if (string.IsNullOrEmpty(territoryKey)) return false;
            return Fnv1a32(territoryKey) % 100u < (uint)SpawnChancePercent;
        }

        /// <summary>딸 이름 — 풀 12개에서 영지ID 결정론 선택. :name 접미 재해시로 등장 롤과 무상관 분리.</summary>
        public static string GetDaughterName(string territoryKey)
        {
            if (string.IsNullOrEmpty(territoryKey)) return DaughterNames[0];
            int index = (int)(Fnv1a32(territoryKey + ":daughter-name") % (uint)DaughterNamePoolSize);
            return DaughterNames[index];
        }

        /// <summary>
        /// 딸 스폰 위치 산출(스폰 없는 순수 계약 — 테스트/호출부 검증용).
        /// 영주실 앵커(활성 레이아웃 LordBed) + 오프셋 (1.6, 0, 0.6), Bedroom 존 판정 실패 시
        /// 폴백 (−1.6, 0, −0.6), 그래도 실패 시 false(스폰 생략 — 호출부 로그 계약).
        /// </summary>
        public static bool TryComputeSpawnPosition(GameObject room, out Vector3 localPosition)
        {
            localPosition = Vector3.zero;
            if (room == null) return false;

            Vector2 anchor2D = InvadedLordEncounter.GetNorthernBedroomAnchor(room);
            Vector2 primary = anchor2D + new Vector2(1.6f, 0.6f);
            if (InvadedLordEncounter.IsNorthernBedroomPosition(room, primary))
            {
                localPosition = new Vector3(primary.x, 0f, primary.y);
                return true;
            }
            Vector2 fallback = anchor2D + new Vector2(-1.6f, -0.6f);
            if (InvadedLordEncounter.IsNorthernBedroomPosition(room, fallback))
            {
                localPosition = new Vector3(fallback.x, 0f, fallback.y);
                return true;
            }
            Debug.LogWarning($"[LordDaughterNPC] 영주실 앵커 양오프셋 모두 Bedroom 밖 — 딸 스폰 생략: {anchor2D}");
            return false;
        }

        /// <summary>
        /// 영주실에 딸을 스폰한다(멱등 — 같은 방에 동일 키 딸이 이미 있으면 기존 인스턴스 반환).
        /// 소유권/등장확률 게이트는 호출부 훅(InvadedLordEncounter.SpawnForInvadedTerritory 말미) 소관.
        /// </summary>
        public static LordDaughterNPC TrySpawnForTerritory(GameObject room, string territoryKey)
        {
            if (room == null || string.IsNullOrEmpty(territoryKey)) return null;

            // 멱등 가드 — 재진입/중복 훅에서 이중 스폰 방지.
            var existing = room.GetComponentInChildren<LordDaughterNPC>(true);
            if (existing != null && existing.TerritoryKey == territoryKey) return existing;

            TerritoryState state = TerritoryDatabase.Instance?.GetState(territoryKey);
            if (state == null)
            {
                Debug.LogWarning($"[LordDaughterNPC] 영지 상태 조회 실패 — 딸 스폰 생략: {territoryKey}");
                return null;
            }

            if (!TryComputeSpawnPosition(room, out Vector3 local))
                return null;   // 위치 계약 실패 — 경위는 TryComputeSpawnPosition이 로그.

            string daughterName = GetDaughterName(territoryKey);

            // 캡슐 NPC — InvadedLordEncounter 99~128행 패턴 그대로(살짝 작게 + 연분홍 계열).
            var go = new GameObject("LordDaughter_" + territoryKey);
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = local;
            // 태그 미설정(Untagged) — 신규 태그 생성 금지, 분류는 컴포넌트 기반(스펙 §2 계약).

            var daughter = go.AddComponent<LordDaughterNPC>();
            daughter.Initialize(territoryKey, daughterName);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "DaughterVisual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            visual.transform.localScale = new Vector3(0.66f, 0.85f, 0.66f);   // 영주(0.75/0.95)보다 살짝 작게
            var visualCollider = visual.GetComponent<Collider>();
            // [126차 수리] DestroyImmediate — Object.Destroy는 EditMode 테스트에서 에러 로그를
            // 남겨 테스트 실패로 이어진다(스폰 1회성이라 런타임 동작/비용 동일).
            if (visualCollider != null) Object.DestroyImmediate(visualCollider);
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                // [126차 수리] .material 자동 인스턴스화는 EditMode에서 에러 로그를 남긴다 —
                // 명시적 인스턴스 머티리얼로 동일 결과(연분홍)를 에디터 클린하게.
                var mat = new Material(renderer.sharedMaterial) { color = new Color(0.97f, 0.72f, 0.80f) };
                renderer.sharedMaterial = mat;
            }

            var collider = go.AddComponent<CapsuleCollider>();
            collider.height = 1.75f;   // 스펙 지정 — 영주(1.9)보다 살짝 작음
            collider.radius = 0.38f;
            collider.center = new Vector3(0f, 0.875f, 0f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var nameplate = go.AddComponent<NameplateDisplay>();
            // 결혼 상태는 저장 대상 — 재진입 재스폰 시에도 부부 표기가 유지된다.
            nameplate.DisplayName = (state.daughterMarried ? "♥ " : "🌸 ") + daughterName;

            Debug.Log($"[LordDaughterNPC] 영주실 딸 스폰 — {daughterName}, territory={territoryKey}, local={local}, married={state.daughterMarried}");
            return daughter;
        }

        /// <summary>호버 대상(GameObject/Component)에서 딸 컴포넌트 탐색 — 없으면 null(InteractionPanelUTK 배선용).</summary>
        public static LordDaughterNPC FindOnTarget(object target)
        {
            if (target == null) return null;
            if (target is LordDaughterNPC daughter) return daughter;
            if (target is Component component) return component.GetComponentInParent<LordDaughterNPC>();
            if (target is GameObject go) return go.GetComponentInParent<LordDaughterNPC>();
            return null;
        }

        // ================================================================
        // 상호작용 — 선물 / 프로포즈
        // ================================================================

        /// <summary>
        /// 들꽃 선물 — 꽃 1개 소비 + 호감도 +10(0~100 클램프). 실패 시 사유 반환(꽃 소비 없음).
        /// 상호작용불가=피드백 필수 계약: 호출부(InteractionPanelUTK)는 모든 결과에 피드백을 표시한다.
        /// </summary>
        public GiftResult GiftFlower(PlayerInventory inventory)
        {
            TerritoryState state = GetState();
            if (state == null) return GiftResult.NoTerritory;
            if (state.daughterAffection >= 100) return GiftResult.AffectionMaxed;   // 100에서 선물 거부 — 소비 없음
            if (inventory == null) return GiftResult.NoInventory;

            if (inventory.GetItemCount(FlowerItemId) <= 0 || !inventory.RemoveItem(FlowerItemId, 1))
                return GiftResult.NoFlower;   // 인벤에 꽃 없음 — 소비 없음

            state.daughterAffection = state.daughterAffection + GiftAffectionGain;   // 세터가 0~100 클램프
            Debug.Log($"[LordDaughterNPC] 🌸 들꽃 선물 — {DisplayName}({TerritoryKey}) 호감도 +{GiftAffectionGain} → {state.daughterAffection}/100");
            return GiftResult.Success;
        }

        /// <summary>
        /// 프로포즈 — 호감도 ≥80 게이트. 수락 시 daughterMarried=true + loyaltyToPlayer +30(1회, 멱등) +
        /// 네임플레이트 "♥ {이름}" 갱신. 이미 부부면 멱등(재가 없음).
        /// </summary>
        public ProposeResult TryPropose()
        {
            TerritoryState state = GetState();
            if (state == null) return ProposeResult.NotReady;
            if (state.daughterMarried) return ProposeResult.AlreadyMarried;   // 멱등 — 충성도 재가 없음
            if (state.daughterAffection < ProposeAffectionThreshold) return ProposeResult.NotReady;

            state.daughterMarried = true;
            state.loyaltyToPlayer = state.loyaltyToPlayer + MarriageLoyaltyBonus;   // 세터가 0~100 클램프
            if (_nameplate == null) _nameplate = GetComponent<NameplateDisplay>();
            if (_nameplate != null) _nameplate.DisplayName = "♥ " + _displayName;
            Debug.Log($"[LordDaughterNPC] 💍 프로포즈 수락 — {DisplayName}({TerritoryKey}) 결혼, loyaltyToPlayer +{MarriageLoyaltyBonus} → {state.loyaltyToPlayer}");
            return ProposeResult.Accepted;
        }

        /// <summary>대화하기 인사 — 현재 호감도 힌트 대사(호감도 구간 4단계 + 부부 전용).</summary>
        public string GetGreetingLine()
        {
            if (IsMarried) return $"{DisplayName}: \"언제나 곁에 있어 주세요. 오늘도 좋은 하루예요.\"";
            int affection = Affection;
            if (affection >= ProposeAffectionThreshold)
                return $"{DisplayName}: \"당신의 마음, 느끼고 있어요. 용기를 내 주세요.\"";
            if (affection >= 40)
                return $"{DisplayName}: \"꽃 향기가 참 좋네요. 이렇게 찾아와 주셔서 기뻐요.\"";
            return $"{DisplayName}: \"…아직 서로 알아가는 중이네요.\"";
        }

        /// <summary>[125차 후속9] 대화창 1행용 인사 본문 — GetGreetingLine의 "이름: " 접두사 제거
        /// (대화창 이름행 = DisplayName과 중복 방지). 접두사 불일치 시 원문 폴백(계약 안전).</summary>
        public string GetGreetingBody()
        {
            string line = GetGreetingLine();
            string prefix = _displayName + ": ";
            return line.StartsWith(prefix) ? line.Substring(prefix.Length) : line;
        }

        // ================================================================
        // 내부
        // ================================================================

        /// <summary>스포너/테스트 셋업용 초기화 — 키·이름·네임플레이트 참조 확정.</summary>
        public void Initialize(string territoryKey, string daughterName)
        {
            _territoryKey = territoryKey ?? string.Empty;
            _displayName = string.IsNullOrEmpty(daughterName) ? GetDaughterName(_territoryKey) : daughterName;
            _nameplate = GetComponent<NameplateDisplay>();
        }

        /// <summary>영지 상태 조회 — 키 파싱 없이 문자열 키 오버로드를 사용한다(단일 소스 TerritoryState).</summary>
        private TerritoryState GetState()
        {
            if (string.IsNullOrEmpty(_territoryKey)) return null;
            return TerritoryDatabase.Instance != null ? TerritoryDatabase.Instance.GetState(_territoryKey) : null;
        }

        /// <summary>FNV-1a 32비트 결정론 해시 — string.GetHashCode 미사용(런타임 비결정) 계약.</summary>
        private static uint Fnv1a32(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;             // FNV offset basis
                foreach (char c in text)
                {
                    hash ^= c;                       // UTF-16 코드 유닛 단위(InteriorLayoutDefinition 선례)
                    hash *= 16777619u;               // FNV prime
                }
                return hash;
            }
        }
    }
}
