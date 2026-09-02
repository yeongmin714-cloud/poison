# 영지 구조 재설계 계획 (Territory Restructure: Castle + Interior + Garrison)

작성: 2026-09-02 11:15 | 대상: /mnt/c/Unity/code (Unity 6000.4.10f1, URP)
기준: 스크린샷 34.PNG (Territory East 06) 분석 + 사용자 요구사항

---

## 1. 현재 상태 (조사 확정)

### 사용자 지적 6가지 → 조사 결과
| # | 지적 | 조사 결과 |
|:-:|------|----------|
| 1 | 병사 수 너무 많음 | GetGuardCount: Ring별 3~35명(East Ring4=12, North Ring4=35) **전원 영지 밖 원형 배치** (스크린샷: T-pose 30~40명) |
| 2 | 병사 크기 이상 | soldier GLB 로딩 중 + scale (1.5, 2, 1.5) → **세로 2배 확대** + 애니 미연결(T-pose) |
| 3 | 상점/크래프트는 진입 시 등장 원함 | 상점/크래프트하우스/교회/주택 4채 + 광장이 **전부 외부 노출** |
| 4 | 문지기 2~4명만, 전쟁 시 내부→외부 | 현재 주둔 개념 없음 — 전부 밖에 상시 스폰 |
| 5 | hut이 아니라 castle.glb + 방위별 색 | **castle GLB 5종 존재** (castle/red/blue/green/purple) + RuntimeModelLoader 키 매핑 완료 — 안 쓰고 hut만 사용 중 |
| 6 | 영지 크기 너무 작음 | 광장 6m + 건물 3m — 성 규모 아님 |

### 색상 규칙 (NationFlagVisualData.GetFlagColorName — 기존 지정 그대로)
- **East=파랑 → blue_castle / West=초록 → green_castle / South=빨강 → red_castle / North=보라 → purple_castle / Empire=castle(무색)**
- 모든 GLB 키가 RuntimeModelLoader에 이미 등록됨 (109~112행)

### 이미 존재하지만 미연결인 시스템 (재활용)
- **CastleInteriorBuilder.BuildCastleInterior(nationStyle)** (270줄) — 성 내부(방 20x6x15 + 왕좌 + 기둥 2열, 국가별 스타일) 생성
- **IndoorTransitionSetup.CreateBuildingTrigger(position, buildingType, interactRange)** — BuildingTrigger(E키 상호작용) 생성 유틸
- **BuildingTrigger** — E키 상호작용 컴포넌트
- **TerritoryWarManager** (1065줄, 씬 존재) — 영지 소유권/전쟁 관리 — 주둔군 스폰 훅 확인 후 연결

### 유지 계약
- 지표면 y = 1 + TerrainGenerator.GetHeightAt (S1에서 적용된 TrySpawnModelOrPlaceholder 보정 유지)
- 82영지/호수 제외존/스폰 평탄화 등 기존 배치 로직 유지
- soldier GLB는 리깅 모델 — 애니메이션 연결(T-pose 해결)은 별도 이슈로 분리

---

## 2. Phase 계획 (R1~R4)

### Phase R1 — 성(Castle) 중심 영지 재구축 [최우선]
- [ ] R1-1 TerritoryBuilder.BuildBuildingsAt 재작성:
  - 기존 외부 건물(광장/상점/크래프트/교회/NPC주택) 생성 **제거**
  - 영지 중심에 **국가별 castle GLB 1개** (색상 매핑 표 참고) + 국기(FlagManager 연동 유지)
  - castle 스케일: 영지 반경 25~40m 규모 (현재 6m 광장 대비 5배+) — _castleScale 파라미터화, GLB 원본 크기 확인 후 결정
  - 성문 위치 계산: castle 피벗/바운즈 기반 문 앞 지점 산출 (BuildingTrigger 부착 위치)
- [ ] R1-2 IsTerritoryAlreadyBuilt 호환: 기존 Territory_* 오브젝트는 hut 기반 → **재빌드 판정 변경** (자식에 "Castle" 프리팹 없으면 재생성) or 이름 마커 변경 (Territory_{nation}_{index}_v2)
- [ ] R1-3 로드 실패 폴백: castle GLB 실패 시 대형 Cube placeholder (색상은 국가색 유지)
- 검증: Play → 하이어라키 프레임 → 파란 성(East) 육안, 크기감 확인

### Phase R2 — 진입 시 내부 등장 (기존 시스템 연결)
- [ ] R2-1 성문 앞 BuildingTrigger 배치: IndoorTransitionSetup.CreateBuildingTrigger(성문위치, "Castle", interactRange=3~4m)
- [ ] R2-2 진입(E키) → CastleInteriorBuilder.BuildCastleInterior(nationStyle) 호출 — 성 내부 씬(방/왕좌) 생성 + **상점/크래프트하우스를 내부로 이동 배치** (기존 상점 NPC/상호작용은 BuildingPlaceholder 그대로, 위치만 내부)
- [ ] R2-3 퇴장 처리: 나가기 트리거/문 상호작용 → 내부 언로드 (기존 IndoorTransition 흐름 준용)
- [ ] R2-4 Empire 중앙 성: castle(무색) + 대리석 내부 스타일
- 검증: Play → 성문 E키 → 내부에서 상점/크래프트 상호작용 → 나가기

### Phase R3 — 병사 재배치 (문지기 + 주둔군)
- [ ] R3-1 외부 문지기: def.guardCount와 무관하게 **2~4명** (Ring 난이도 따라 2→4) 성문 양옆 배치, scale (1,1,1)로 교정
- [ ] R3-2 주둔군: 나머지 병사는 **스폰하지 않고 데이터만 유지** (TerritoryDefinition.guardCount) — 전쟁 시 TerritoryWarManager가 내부→외부 스폰하는 훅 추가 (전쟁 시작 이벤트 위치 확인, SpawnGarrison(def) API)
- [ ] R3-3 병사 스케일 교정: CreateGuard scale (1.5,2,1.5) → (1,1,1) (soldier GLB 원본 크기 — 사람 키 ~1.8m 확인)
- [ ] R3-4 (옵션, 별도 이슈 권장) T-pose → AnimationMotionController 연결
- 검증: Play → 문지기 2~4명만 외부, 크기 정상

### Phase R4 — 종합 검증/문서
- [ ] Play 전체 체크: 성 크기/색(방위별) + 문지기 수/크기 + E키 진입 내부 + 상호작용
- [ ] QAPROGRESS.md + 메모리 + 커밋+푸시

---

## 3. 실행 방식
- 각 Phase: code agent(delegate_task) 구현 → 배치컴파일 → Play 눈검증 → 커밋
- R1+R3-3(스케일)은 함께 구현 가능 (같은 파일). R2는 트리거 흐름이라 별도. R3-2(전쟁 훅)는 TerritoryWarManager 확인 필요해 별도.

## 4. 리스크 / 미확정
- castle GLB의 실제 피벗/원본 크기 미확인 — R1에서 로드 로그 + 스케일 계수로 맞춤
- CastleInteriorBuilder nationStyle 파라미터 형식("East"? 한글?) 확인 필요 — R2 진입 시
- BuildingTrigger의 기존 소비자(상점 등)와의 상호작용 충돌 가능 — R2에서 회귀 확인
- 전쟁 시스템(TerritoryWarManager 1065줄)과 주둔군 스폰 연동은 최소 훅만 — 전쟁 게임플레이 상세는 별도 Phase
- 병사 T-pose는 애니메이션 이슈로 분리 (R3-4 옵션)
