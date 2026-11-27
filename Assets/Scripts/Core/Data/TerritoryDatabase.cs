using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Utils; // [T1-2] TerritoryScatterMath(밴드 산포) — 프로젝트 산포 수학 API
using static ProjectName.Core.Utils.TerritoryScatterMath; // [T1-2 CS0246 수리] 중첩 struct ScatterInput 스코프 진입 — namespace using은 중첩 타입을 스코프에 가져오지 못함
#pragma warning disable 0414

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 영지 데이터베이스 — 모든 영지(82개)의 정의와 상태를 관리합니다.
    /// 
    /// 사용법:
    ///   TerritoryDatabase db = TerritoryDatabase.Instance;
    ///   TerritoryDefinition def = db.GetDefinition(NationType.East, 1);
    ///   TerritoryState state = db.GetState(NationType.East, 1);
    /// </summary>
    public class TerritoryDatabase
    {
        private static readonly Lazy<TerritoryDatabase> _instance =
            new Lazy<TerritoryDatabase>(() => new TerritoryDatabase(), isThreadSafe: true);

        public static TerritoryDatabase Instance => _instance.Value;

        private readonly Dictionary<string, TerritoryDefinition> _definitions = new Dictionary<string, TerritoryDefinition>();
        private readonly Dictionary<string, TerritoryState> _states = new Dictionary<string, TerritoryState>();

        // ===== 상수 =====
        private static readonly string[] _diseasePool = { "심장병", "당뇨", "간질환", "폐질환", "신장병" };

        /// <summary>
        /// 프로세스 재시작 간에도 동일한 결과를 보장하는 결정론적 문자열 해시
        /// (string.GetHashCode()는 .NET Core 5+에서 프로세스마다 값이 바뀌므로 사용 불가)
        /// </summary>
        private static int GetDeterministicHash(string input)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        // ===== 영주 이름 배열 (Core 내장 — NamePools 의존 없음) =====
        private static readonly string[] _lordEastNames = {
            "세드릭", "알드릭", "레오나드", "마커스", "펜드릭",
            "드레이크", "알릭", "에드릭", "하롤드", "랜슬롯",
            "고드윈", "테오도어", "에드먼드", "로드릭", "가윈",
            "오스왈드", "레지날드", "프레드릭", "밀러드", "오스릭"
        };
        private static readonly string[] _lordWestNames = {
            "가레트", "던컨", "루시우스", "발터", "고든",
            "레이너드", "마그누스", "알딘", "콘래드", "필립",
            "시어도어", "윌리엄", "아서", "라파엘", "길버트",
            "호레이스", "제라드", "랜돌프", "실레스", "티모시"
        };
        private static readonly string[] _lordSouthNames = {
            "발카르", "렉산더", "크라토스", "유스티안", "티베리우스",
            "막시무스", "마리우스", "센트리우스", "아티우스", "게르마니쿠스",
            "세베루스", "아우렐리우스", "에어리쿠스", "타키투스", "베스파시아누스",
            "트라야누스", "옥타비우스", "플라비우스", "메텔루스", "코르넬리우스"
        };
        private static readonly string[] _lordNorthNames = {
            "언윈", "울릭", "프리스트", "브라엄", "라그나르",
            "스벤", "비요른", "에이리크", "군나르", "이바르",
            "라우리츠", "크누트", "하랄드", "올라프", "시구르드",
            "엘리프", "헤르만", "볼프강", "디트리히", "알브레히트"
        };

        private TerritoryDatabase()
        {
            InitializeDefinitions();
            InitializeStates();
        }

        // ===== 정의 조회 =====

        public TerritoryDefinition GetDefinition(NationType nation, int index)
        {
            string key = new TerritoryId(nation, index).ToString();
            if (_definitions.TryGetValue(key, out var def))
                return def;
            Debug.LogWarning($"[TerritoryDatabase] 정의 없음: {key}");
            return new TerritoryDefinition();
        }

        public TerritoryDefinition GetDefinition(TerritoryId id)
        {
            string key = id.ToString();
            if (_definitions.TryGetValue(key, out var def))
                return def;
            Debug.LogWarning($"[TerritoryDatabase] 정의 없음: {key}");
            return new TerritoryDefinition();
        }

        public TerritoryDefinition GetDefinition(string key)
        {
            if (_definitions.TryGetValue(key, out var def))
                return def;
            Debug.LogWarning($"[TerritoryDatabase] 정의 없음: {key}");
            return new TerritoryDefinition();
        }

        public IEnumerable<TerritoryDefinition> GetAllDefinitions()
        {
            return _definitions.Values;
        }

        public IEnumerable<TerritoryDefinition> GetDefinitionsByNation(NationType nation)
        {
            foreach (var def in _definitions.Values)
            {
                if (def.nation == nation)
                    yield return def;
            }
        }

        // ===== 상태 조회/변경 =====

        public TerritoryState GetState(NationType nation, int index)
        {
            string key = new TerritoryId(nation, index).ToString();
            if (_states.TryGetValue(key, out var state))
                return state;
            Debug.LogWarning($"[TerritoryDatabase] 상태 없음: {key}");
            return new TerritoryState(new TerritoryId(nation, index));
        }

        public TerritoryState GetState(TerritoryId id)
        {
            string key = id.ToString();
            if (_states.TryGetValue(key, out var state))
                return state;
            Debug.LogWarning($"[TerritoryDatabase] 상태 없음: {key}");
            return new TerritoryState(id);
        }

        /// <summary>
        /// 문자열 키로 영지 상태를 조회합니다. (키 형식: "NationType_Index")
        /// </summary>
        /// <param name="key">영지 키 (예: "East_01")</param>
        public TerritoryState GetState(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            if (_states.TryGetValue(key, out var state))
                return state;

            // 키를 TerritoryId로 파싱하여 폴백
            string[] parts = key.Split('_');
            if (parts.Length == 2)
            {
                if (System.Enum.TryParse<NationType>(parts[0], out var nation) &&
                    int.TryParse(parts[1], out var index))
                {
                    return new TerritoryState(new TerritoryId(nation, index));
                }
            }

            Debug.LogWarning($"[TerritoryDatabase] 상태 없음: {key}");
            return null;
        }

        /// <summary>
        /// [P30-D] 등록된 모든 영지 상태 열거 (읽기 전용 뷰 — 참조 공유, TerritoryDrugSystem 전역 오염 진행용).
        /// foreach 규약 준수 — 순회 중 컬렉션 수정 금지(뷰는 사본 아님).
        /// </summary>
        public IEnumerable<TerritoryState> GetAllStates()
        {
            return _states.Values;
        }

        /// <summary>[O6 C-O6-02] 소유 변경 발화 — PlayerOwned로 "변경된" 경우에만. 구독자 없으면 무해.</summary>
        public static event System.Action<TerritoryId, TerritoryOwnership> OwnershipChanged;

        /// <summary>
        /// [F3] 소유 상실 발화 — PlayerOwned에서 다른 소유로 "변경된" 경우에만
        /// (영주 배신, AI 재탈환 등). 기존 OwnershipChanged 계약(PlayerOwned 전환만) 불변 유지,
        /// 신규 이벤트라 구독자 없으면 무해. 세이브 복원 중(OwnershipRestoreMode)엔 억제.
        /// </summary>
        public static event System.Action<TerritoryId, TerritoryOwnership> OwnershipLost;

        /// <summary>[P1] 세이브 로드 중 복원 모드 — 복원은 "새 점령"이 아니므로 이벤트 억제(칭호 카운터 오염 방지).</summary>
        public static bool OwnershipRestoreMode { get; set; }

        public void SetOwnership(NationType nation, int index, TerritoryOwnership ownership)
        {
            var state = GetState(nation, index);
            bool changed = state.ownership != ownership;
            bool wasPlayer = state.ownership == TerritoryOwnership.PlayerOwned; // [F3] 변경 전 소유 기록
            state.ownership = ownership;
            if (changed && ownership == TerritoryOwnership.PlayerOwned && !OwnershipRestoreMode)
                OwnershipChanged?.Invoke(state.id, ownership);
            if (changed && wasPlayer && ownership != TerritoryOwnership.PlayerOwned && !OwnershipRestoreMode)
                OwnershipLost?.Invoke(state.id, ownership);
        }

        public void SetOwnership(TerritoryId id, TerritoryOwnership ownership)
        {
            var state = GetState(id);
            bool changed = state.ownership != ownership;
            bool wasPlayer = state.ownership == TerritoryOwnership.PlayerOwned; // [F3] 변경 전 소유 기록
            state.ownership = ownership;
            if (changed && ownership == TerritoryOwnership.PlayerOwned && !OwnershipRestoreMode)
                OwnershipChanged?.Invoke(id, ownership);
            if (changed && wasPlayer && ownership != TerritoryOwnership.PlayerOwned && !OwnershipRestoreMode)
                OwnershipLost?.Invoke(id, ownership);
        }

        /// <summary>
        /// [123차] 영지 소유 영주 설정 — 소유권(ownership)과 별개로 "현재 소유 영주 이름"만 갱신.
        /// 영주↔영주 점령 경로(AIWarSystem.CompleteWar / TerritoryWarManager.HandleVictory),
        /// 배신 복귀(LordBetrayalSystem.ExecuteBetrayal), 뉴게임+ 리셋(NewGamePlusSystem)에서 호출.
        /// - 멱등: 동일 값 재설정은 무시(불필요한 변경 없음).
        /// - 가드: null/빈 territoryId·lordName 무시, 미등록 territoryId 무시.
        /// - 소유 영주만 기록 — ownership 자체는 건드리지 않음(이벤트 발화 없음).
        /// </summary>
        public void SetOwnerLord(string territoryId, string lordName)
        {
            if (string.IsNullOrEmpty(territoryId) || string.IsNullOrEmpty(lordName))
            {
                Debug.LogWarning($"[TerritoryDatabase] SetOwnerLord 무효 인자 무시 — territoryId: '{territoryId}', lordName: '{lordName}'");
                return;
            }
            if (!_states.TryGetValue(territoryId, out var state))
            {
                Debug.LogWarning($"[TerritoryDatabase] SetOwnerLord 미등록 영지 무시 — {territoryId}");
                return;
            }
            if (state.ownerLordName == lordName) return; // 멱등
            state.ownerLordName = lordName;
        }

        // ===== 초기화 =====

        private void InitializeDefinitions()
        {
            GenerateAllDefinitions();
        }

        /// <summary>
        /// 82개 전 영지 정의 생성 (Seed 기반 결정론적 생성)
        /// [T1-2] 좌표 산포 교체: 기존 링 고정 반경×지터(원 배치) → 밴드 산포(TerritoryScatterMath.TryScatter).
        /// 산포 순서 Ring4→Ring1(안쪽부터 확정)으로 밴드 단조 보장, 전역 최소 간격 120m,
        /// 스폰(728,−529) 회피 100m, Empire/Dracula는 고정 앵커로 taken 사전 시드.
        /// 난이도/이름/영주/병사수/설명 배정 로직은 기존 그대로 유지(좌표 산출만 교체).
        /// </summary>
        private void GenerateAllDefinitions()
        {
            // ===== [T1-2] 산포 상수 =====
            const float ScatterMinSpacing = 120f; // 전 영지 간 최소 간격(m)
            const int ScatterMaxTries = 200;      // 산포 재시도 상한(초과 시 폴백 좌표 반환)
            const float SpawnAvoidRadius = 100f;  // 플레이어 스폰 회피 반경(m)
            Vector2 spawnAvoidPoint = new Vector2(728f, -529f); // 플레이어 스폰(XZ 좌표 규약: Vector2(x, z))

            // 드라큘라 앵커 좌표 — 하단 드라큘라 블록과 동일한 상수(1350m @ 150°)를 그대로 재현.
            // (블록 무수정 원칙에 따라 블록은 그대로 두고, taken 사전 시드용으로만 동일 계산을 수행)
            const float DraculaAnchorDistance = 1350f;
            const float DraculaAnchorAngleDeg = 150f;
            float draculaAnchorRad = DraculaAnchorAngleDeg * Mathf.Deg2Rad;
            Vector2 draculaAnchorXZ = new Vector2(Mathf.Cos(draculaAnchorRad) * DraculaAnchorDistance,
                                                  Mathf.Sin(draculaAnchorRad) * DraculaAnchorDistance);

            // taken 리스트: 확정 좌표 누적 — 고정 앵커(황제국 중심 (0,0), 드라큘라 성)부터 사전 시드
            var taken = new List<Vector2> { Vector2.zero, draculaAnchorXZ };

            // 산포 순서: Ring4(안쪽·최난) → Ring3 → Ring2 → Ring1(바깥·최약)
            // 안쪽 밴드를 먼저 확정해 링 간 밴드 단조(Ring4max ≤ Ring3min ≤ …)를 보장한다.
            // 링 내 순서: nation × t=1..5 (인덱스 1~20 배정은 기존 규약 유지)
            for (int ring = 3; ring >= 0; ring--)
            {
                TerritoryDifficulty difficulty = (TerritoryDifficulty)ring;
                (float bandMin, float bandMax) = GetRingBand(difficulty);

                foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
                {
                    int baseGuardCount = GetGuardCount(nation, difficulty);
                    float nationBaseAngle = GetNationBaseAngle(nation);

                    for (int t = 0; t < 5; t++)
                    {
                        int index = ring * 5 + t + 1; // 1~20
                        string name = GenerateTerritoryName(nation, index);
                        LordInfo lord = GenerateLordInfo(nation, difficulty, index);
                        string desc = GenerateDescription(nation, difficulty, index);

                        // [T1-2] 밴드 산포: 거리 ∈ [bandMin, bandMax] 균등, 각도 ∈ 기저각 ±(45−6)° 균등,
                        // 기확정 좌표(taken)와 최소 간격 이상, 스폰 회피 원 밖 배치.
                        // 결정론 시드("{nation}_{ring}_{t}_scatter") → 재시작·재생성 시에도 항상 동일 좌표.
                        string scatterKey = $"{nation}_{ring}_{t}_scatter";
                        var scatterRng = new System.Random(GetDeterministicHash(scatterKey));
                        var scatterInput = new ScatterInput
                        {
                            key = scatterKey,
                            bandMin = bandMin,
                            bandMax = bandMax,
                            wedgeBaseDeg = nationBaseAngle,
                            wedgeMarginDeg = 6f
                        };
                        bool scattered = TerritoryScatterMath.TryScatter(
                            scatterInput, scatterRng, taken, ScatterMinSpacing,
                            spawnAvoidPoint, SpawnAvoidRadius, ScatterMaxTries,
                            out Vector2 scatteredXZ);
                        if (!scattered)
                        {
                            // 폴백 좌표(밴드 중간거리 + 쐐기 기저각)가 사용된 경우 — 생성은 계속되지만
                            // 간격 위반 가능성이 있으므로 전역 재검증(하단)에서 최종 확인된다.
                            Debug.LogWarning($"[TerritoryDatabase] 산포 실패 — 폴백 좌표 사용: {scatterKey}");
                        }
                        taken.Add(scatteredXZ);

                        // Vector2(x, z) 좌표 규약 → 세계 XZ (Y=0 고정)
                        Vector3 worldPos = new Vector3(scatteredXZ.x, 0f, scatteredXZ.y);

                        AddDefinition(nation, index, name, nation, difficulty, baseGuardCount, lord, desc, worldPos);
                    }
                }
            }

            // 황제국 (인덱스 1, Ring = Empire, 병사 50명) - 중심 (0, 0, 0)
            {
                var rng = new System.Random(GetDeterministicHash("Empire_1"));
                string[] empireNames = {
                    "황제국의 심장 아우리아", "황제국 빛의 대성당", "황제국 황금 돔",
                    "황제국 대리석 전당", "황제국 보석 정원", "황제국 태양 광장",
                    "황제국 학자의 탑", "황제국 황실 정원", "황제국 수정 궁전",
                    "황제국 은빛 분수대"
                };
                string name = empireNames[rng.Next(empireNames.Length)];

                var lord = new LordInfo
                {
                    lordName = "아우구스투스 황제",
                    // [2026-10-21] 강한 영지일수록 재료 구하기 힘든 음식(T3 고급) 지정 —
                    // 최강 난이도인 황제국은 T3 집합(꿀 케이크/양고기 스테이크/해산물 파이)에서만 선택.
                    preferredFood = new[] { "꿀 케이크", "양고기 스테이크", "해산물 파이" }[rng.Next(3)],
                    chronicDisease = "위 질환, 통풍, 고혈압",
                    loyalty = 95 + rng.Next(6), // 95~100
                    personality = (LordPersonality)rng.Next(7)
                };

                string description = "모든 영지의 중심, 황제가 다스리는 최후의 요새. 50명의 정예 친위대가 수호한다.";

                AddDefinition(NationType.Empire, 1, name, NationType.Empire,
                    TerritoryDifficulty.Empire, 50, lord, description, Vector3.zero);
            }

            // 드라큘라 영지 (Night Dracula, 인덱스 1, Ring4 난이도, 병사 10명)
            // 1350m North-North-East direction (angle ~60° from North = 150° from East)
            {
                var draculaLord = new LordInfo
                {
                    lordName = "드라큘라 백작",
                    preferredFood = "붉은 포도주",
                    chronicDisease = "",
                    loyalty = 0,
                    personality = LordPersonality.Cruel
                };

                string draculaDescription = "밤에만 출현하는 저주받은 성. 드라큘라 백작과 그의 스켈레톤 군대가 지키고 있다.";

                float draculaAngleDeg = 150f; // 150° from East (positive X) = 60° from North
                float draculaAngleRad = draculaAngleDeg * Mathf.Deg2Rad;
                Vector3 draculaPos = new Vector3(Mathf.Cos(draculaAngleRad) * 1350f, 0f, Mathf.Sin(draculaAngleRad) * 1350f);

                AddDefinition(NationType.Dracula, 1, "드라큘라의 성", NationType.Dracula,
                    TerritoryDifficulty.Ring4, 10, draculaLord, draculaDescription, draculaPos, isNightOnly: true);
            }

            // ===== [T1-2] 산포 완료 후 전역 재검증 =====
            // 82영지 전 쌍 간격 ≥ 120m 및 산포 영지 밴드 소속 확인. 플레이 타임 1회만 도는 초기화라 비용 무시 가능.
            {
                var allDefs = new List<TerritoryDefinition>(_definitions.Values);
                int spacingViolations = 0;
                int bandViolations = 0;
                var violationDetails = new System.Text.StringBuilder();

                for (int i = 0; i < allDefs.Count; i++)
                {
                    for (int j = i + 1; j < allDefs.Count; j++)
                    {
                        float dx = allDefs[i].worldPosition.x - allDefs[j].worldPosition.x;
                        float dz = allDefs[i].worldPosition.z - allDefs[j].worldPosition.z;
                        float pairDist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (pairDist < ScatterMinSpacing)
                        {
                            spacingViolations++;
                            if (violationDetails.Length < 2000) // 로그 과다 방지 — 앞부분 위반만 상세 기록
                                violationDetails.AppendLine($"  간격 {pairDist:F1}m: {allDefs[i].id}({allDefs[i].worldPosition.x:F1},{allDefs[i].worldPosition.z:F1}) ↔ {allDefs[j].id}({allDefs[j].worldPosition.x:F1},{allDefs[j].worldPosition.z:F1})");
                        }
                    }

                    // 밴드 이탈 검사 — 고정 앵커(황제국/드라큘라)는 산포 대상이 아니므로 제외
                    if (allDefs[i].nation != NationType.Empire && allDefs[i].nation != NationType.Dracula)
                    {
                        (float bandMin, float bandMax) = GetRingBand(allDefs[i].difficulty);
                        float dx = allDefs[i].worldPosition.x;
                        float dz = allDefs[i].worldPosition.z;
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist < bandMin - 0.5f || dist > bandMax + 0.5f)
                        {
                            bandViolations++;
                            if (violationDetails.Length < 2000)
                                violationDetails.AppendLine($"  밴드 이탈: {allDefs[i].id} 거리 {dist:F1}m ∉ [{bandMin:F0},{bandMax:F0}]");
                        }
                    }
                }

                if (spacingViolations > 0 || bandViolations > 0)
                    Debug.LogError($"[TerritoryDatabase] 산포 전역 재검증 실패 — 최소 간격({ScatterMinSpacing}m) 위반 {spacingViolations}건, 밴드 이탈 {bandViolations}건\n{violationDetails}");
                else
                    Debug.Log($"[TerritoryDatabase] 산포 전역 재검증 통과 — 영지 {allDefs.Count}개 전 쌍 간격 ≥ {ScatterMinSpacing}m, 밴드 이탈 없음");
            }
        }

        /// <summary>
        /// 링별 거리 반환 (미터 단위)
        /// </summary>
        private static float GetRingDistance(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => 1450f,
                TerritoryDifficulty.Ring2 => 1000f,
                TerritoryDifficulty.Ring3 => 550f,
                TerritoryDifficulty.Ring4 => 150f,
                _ => 0f
            };
        }

        /// <summary>
        /// [T1-2] 링별 배치 밴드(최소/최대 거리, 미터) — 원 배치(링 고정 반경×지터)를 밴드 산포로 교체하며 신설.
        /// Ring4=(50,380) 최난(안쪽) / Ring3=(380,750) / Ring2=(750,1150) / Ring1=(1150,1550) 최약(바깥).
        /// 밴드 경계는 맞닿아 있어 산포 순서(Ring4→Ring1)가 밴드 단조를 보장한다.
        /// Ring1 최대 1550m < 세계 extent 1600m — 지도 밖 방지.
        /// [2026-10-15 실측 — 밴드 확장 근거] 기존 Ring4=(50,300)은 78°쐐기(±39°) 내 120m 간격 5점 배치가
        /// 리젝션 포화에 도달해 20/20 시드에서 폴백 발생(시뮬 평균 4.25건/시드, 전부 Ring4).
        /// 밴드 확장으로 리젝션 여유를 확보했다. 단조 원칙(중앙 가까울수록 어려움)·쐐기·간격 규약 유지.
        /// 지도 extent 1600m 내(Ring1 최대 1550m).
        /// 황제국은 밴드 없음(고정 (0,0)) — Empire 호출 시 (0,0) 폴백.
        /// </summary>
        private static (float min, float max) GetRingBand(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring4 => (50f, 380f),
                TerritoryDifficulty.Ring3 => (380f, 750f),
                TerritoryDifficulty.Ring2 => (750f, 1150f),
                TerritoryDifficulty.Ring1 => (1150f, 1550f),
                _ => (0f, 0f)
            };
        }

        /// <summary>
        /// 국가별 기본 각도 반환 (도 단위, East=0°, North=90°, West=180°, South=270°)
        /// </summary>
        private static float GetNationBaseAngle(NationType nation)
        {
            return nation switch
            {
                NationType.East => 0f,
                NationType.North => 90f,
                NationType.West => 180f,
                NationType.South => 270f,
                _ => 0f
            };
        }

        /// <summary>
        /// 국가 & 난이도에 따른 병사 수 반환 (ROADMAP 3.1 표)
        /// </summary>
        private static int GetGuardCount(NationType nation, TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => nation switch
                {
                    NationType.East => 3,
                    NationType.West => 4,
                    NationType.South => 4,
                    NationType.North => 5,
                    _ => 3
                },
                TerritoryDifficulty.Ring2 => nation switch
                {
                    NationType.East => 5,
                    NationType.West => 7,
                    NationType.South => 8,
                    NationType.North => 10,
                    _ => 5
                },
                TerritoryDifficulty.Ring3 => nation switch
                {
                    NationType.East => 8,
                    NationType.West => 12,
                    NationType.South => 15,
                    NationType.North => 18,
                    _ => 8
                },
                TerritoryDifficulty.Ring4 => nation switch
                {
                    NationType.East => 12,
                    NationType.West => 18,
                    NationType.South => 25,
                    NationType.North => 35,
                    _ => 12
                },
                TerritoryDifficulty.Empire => 50,
                _ => 3
            };
        }

        /// <summary>
        /// 영지 이름 생성 (Seed 기반 결정론적)
        /// </summary>
        private static string GenerateTerritoryName(NationType nation, int index)
        {
            string prefix = nation switch
            {
                NationType.East => "동쪽",
                NationType.West => "서부",
                NationType.South => "남부",
                NationType.North => "북부",
                _ => "영지"
            };

            // 지역명 풀 (Ring별 5개씩, 총 20개)
            string[] locationNames = nation switch
            {
                NationType.East => new[] {
                    "평화로운 초원", "맑은 시내", "푸른 언덕", "꽃피는 들판", "별빛 숲",
                    "산들바람 계곡", "무지개 언덕", "노래하는 개울", "햇살 가득한 곳", "푸른 호수",
                    "숲속 공터", "은하수 언덕", "이슬 맺힌 숲", "해바라기 밭", "평온한 마을",
                    "비단결 강", "아침 안개 마을", "신비로운 숲", "황금 들판", "영원한 봄의 정원"
                },
                NationType.West => new[] {
                    "황무지", "바위 계곡", "늪지대", "모래 언덕", "고원",
                    "척박한 땅", "메마른 강", "가시 덤불", "돌무더기", "먼지 바람",
                    "깊은 협곡", "황량한 언덕", "마른 나무 숲", "바위투성이", "갈라진 땅",
                    "선인장 평원", "뾰족 바위", "석양 언덕", "그늘 계곡", "침묵의 사막"
                },
                NationType.South => new[] {
                    "불꽃 평야", "화산 기슭", "용암 계곡", "붉은 모래", "재투성이",
                    "뜨거운 온천", "증기 분수", "용암 분출구", "용의 아귀", "불타는 숲",
                    "검은 재", "화염 동굴", "마그마 호수", "유황 냄새", "불기둥",
                    "시뻘건 돌", "화산재 언덕", "굽이치는 용암", "불의 정원", "작열하는 땅"
                },
                NationType.North => new[] {
                    "설원", "얼음 계곡", "눈보라 고개", "빙하 호수", "툰드라",
                    "얼음 동굴", "눈 덮인 숲", "빙벽", "얼음 왕관", "눈사태 언덕",
                    "싸락눈 평원", "흰 눈길", "얼음 조각", "칼바람 언덕", "설송 숲",
                    "얼음 꽃", "북극성 언덕", "얼음 탑", "눈사람 마을", "빙하의 숨결"
                },
                _ => new[] { "초원", "언덕", "계곡", "평야", "숲" }
            };

            // index는 1~20, 배열은 0~19
            string location = locationNames[(index - 1) % locationNames.Length];
            return $"{prefix} {location}";
        }

        /// <summary>
        /// 영주 정보 생성 (Seed 기반 결정론적)
        /// </summary>
        private static LordInfo GenerateLordInfo(NationType nation, TerritoryDifficulty difficulty, int index)
        {
            int seed = GetDeterministicHash($"{nation}_{index}");
            var rng = new System.Random(seed);

            // 영주 이름 (Core 내장 배열 — NamePools 의존 없음)
            string[] namePool = nation switch
            {
                NationType.East => _lordEastNames,
                NationType.West => _lordWestNames,
                NationType.South => _lordSouthNames,
                NationType.North => _lordNorthNames,
                _ => _lordEastNames
            };
            string lordName = namePool[(index - 1) % namePool.Length];

            // 선호 음식 (링 가중치 결정론) — T1 쉬움/T3 어려움, 링 강할수록 T3 고급 음식 가중.
            // 링별 10슬롯 배열(합계 반드시 10)로 가중치 구현. RNG 소모 1회(rng.Next(10)) 유지 —
            // 이후 지병/충성심/성격 추첨 순서·횟수 불변(바이트 동일 보장). 슬롯 순서는 고정 계약.
            string[] foods = difficulty switch
            {
                TerritoryDifficulty.Ring1 => new[] {
                    "구운 고기", "구운 고기",             // T1 70 (2+2+2+1=7)
                    "야채 샐러드", "야채 샐러드",
                    "빵과 치즈", "빵과 치즈",
                    "버섯 수프",
                    "생선 스튜", "로스트 치킨", "포도주" // T2 30 (3)
                },
                TerritoryDifficulty.Ring2 => new[] {
                    "구운 고기", "야채 샐러드", "빵과 치즈", // T1 30 (3)
                    "생선 스튜", "생선 스튜",              // T2 50 (2+2+1=5)
                    "로스트 치킨", "로스트 치킨",
                    "포도주",
                    "꿀 케이크", "해산물 파이"            // T3 20 (2)
                },
                TerritoryDifficulty.Ring3 => new[] {
                    "구운 고기",                        // T1 10 (1)
                    "생선 스튜", "로스트 치킨",           // T2 40 (1+1+2=4)
                    "포도주", "포도주",
                    "꿀 케이크", "꿀 케이크",             // T3 50 (2+2+1=5)
                    "양고기 스테이크", "양고기 스테이크",
                    "해산물 파이"
                },
                TerritoryDifficulty.Ring4 => new[] {
                    "꿀 케이크", "해산물 파이",              // T2 잔여 슬롯 폐기 → T3 100% (최강 링은 어려운 음식만)
                    "꿀 케이크", "꿀 케이크", "꿀 케이크",     // T3 80+20 = 100 (3+3+2 기존 + 교체 2 = 4/3/3)
                    "양고기 스테이크", "양고기 스테이크", "양고기 스테이크",
                    "해산물 파이", "해산물 파이"
                },
                _ => new[] { "구운 고기", "구운 고기", "야채 샐러드", "야채 샐러드", "빵과 치즈", "빵과 치즈", "버섯 수프", "생선 스튜", "로스트 치킨", "포도주" }
            };
            string preferredFood = foods[rng.Next(10)];

            // 지병 (Ring별)
            string chronicDisease = difficulty switch
            {
                TerritoryDifficulty.Ring1 => "",
                TerritoryDifficulty.Ring2 => rng.Next(4) == 0 ? new[] { "통풍", "관절염", "두통" }[rng.Next(3)] : "",
                TerritoryDifficulty.Ring3 => new[] { "편두통", "천식", "소화불량", "불면증" }[rng.Next(4)],
                TerritoryDifficulty.Ring4 => rng.Next(2) == 0
                    ? _diseasePool[rng.Next(_diseasePool.Length)]
                    : $"{_diseasePool[rng.Next(_diseasePool.Length)]}, {_diseasePool[rng.Next(_diseasePool.Length)]}",
                _ => ""
            };

            // 충성심 (Ring별)
            int loyalty = difficulty switch
            {
                TerritoryDifficulty.Ring1 => 70 + rng.Next(11),  // 70~80
                TerritoryDifficulty.Ring2 => 75 + rng.Next(11),  // 75~85
                TerritoryDifficulty.Ring3 => 80 + rng.Next(11),  // 80~90
                TerritoryDifficulty.Ring4 => 85 + rng.Next(11),  // 85~95
                _ => 70
            };

            // 성격 (랜덤)
            LordPersonality personality = (LordPersonality)rng.Next(7);

            return new LordInfo
            {
                lordName = $"{lordName} 경",
                preferredFood = preferredFood,
                chronicDisease = chronicDisease,
                loyalty = loyalty,
                personality = personality
            };
        }

        /// <summary>
        /// 영지 설명 생성
        /// </summary>
        private static string GenerateDescription(NationType nation, TerritoryDifficulty difficulty, int index)
        {
            string nationName = nation switch
            {
                NationType.East => "동쪽",
                NationType.West => "서부",
                NationType.South => "남부",
                NationType.North => "북부",
                _ => "미지의"
            };

            string ringDesc = difficulty switch
            {
                TerritoryDifficulty.Ring1 => "국경 근처의 조용한",
                TerritoryDifficulty.Ring2 => "내륙의 평화로운",
                TerritoryDifficulty.Ring3 => "중앙부의 격전지",
                TerritoryDifficulty.Ring4 => "황제국 인근의 최전선",
                _ => "모든 영지의 중심"
            };

            return $"{nationName} {ringDesc} 영지. {(int)difficulty + 1}단계 난이도.";
        }

        private void InitializeStates()
        {
            foreach (var kvp in _definitions)
            {
                var state = new TerritoryState(kvp.Value.id);
                // [123차] 소유 영주 결정론 매핑 — 초기 소유 영주 = def.lord.lordName.
                // 씬 로드 시점과 무관하게 DB 생성(InitializeStates) 경로에서 확정되므로
                // 같은 정의가 주어지면 항상 같은 ownerLordName이 보장된다.
                state.ownerLordName = kvp.Value.lord.lordName;
                _states[kvp.Key] = state;
            }
        }

        private void AddDefinition(NationType nation, int index, string name, NationType nationType,
            TerritoryDifficulty difficulty, int guardCount, LordInfo lord, string description, Vector3 worldPosition, bool isNightOnly = false)
        {
            var id = new TerritoryId(nation, index);
            var def = new TerritoryDefinition
            {
                id = id,
                territoryName = name,
                nation = nationType,
                difficulty = difficulty,
                guardCount = guardCount,
                lord = lord,
                description = description,
                isNightOnly = isNightOnly,
                worldPosition = worldPosition
            };
            _definitions[id.ToString()] = def;
        }

        /// <summary>
        /// [P6] 위치 → 영지 판정 — 가장 가까운 영지 중심(반경 radius 내). 어디에도 해당 없으면 null.
        /// 걷기 이동 시 CurrentTerritoryId가 갱신되지 않는 허점을 보완(건설 배치 등 위치 기반 판정용).
        /// </summary>
        public TerritoryId? ResolveTerritoryAt(Vector3 pos, float radius = 60f)
        {
            TerritoryId? best = null;
            float bestDist = float.MaxValue;
            foreach (var def in GetAllDefinitions())
            {
                float dist = Vector3.Distance(def.worldPosition, pos);
                if (dist <= radius && dist < bestDist)
                {
                    bestDist = dist;
                    best = def.id;
                }
            }
            return best;
        }

        /// <summary>
        /// 국가와 인덱스로 월드 위치 조회
        /// </summary>
        public Vector3 GetWorldPosition(NationType nation, int index)
        {
            var def = GetDefinition(nation, index);
            return def.worldPosition;
        }
    }
}