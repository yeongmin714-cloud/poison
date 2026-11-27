# 152차 계획 — 침대 세이브 확인 + 훈련장 건설창 UI 추가 + 실내 배치 검증·건설 완결

작성: 2026-11-03 (152차). 승인 전까지 구현 없음.

## 0. 사용자 지시 (Telegram 2026-11-03)

1. 침대에서 세이브 시스템 구현 여부 확인
2. 실내 씬에서 훈련장 배치 가능 여부 확인
3. 건설 창에 훈련장 UI가 없음 — 동일 스타일/그리드/폰트로 훈련장 건설 항목 추가 + 실제로 지어질 수 있게
4. 계획부터

## 1. 사전 실측 결과 (확인 요청 2건에 대한 답)

### ① 침대 세이브 — **구현돼 있음** (신규 작업 불필요)
- `Systems/Bed.cs` — E키 상호작용(EInteractionPromptBridge "E - 잠자기/세이브") → SleepUI 표시
- `Systems/SleepUI.cs` L291-314 — **"💾 여기서 세이브" 버튼**: ①현재 침대 위치를 부활 스폰핀 기록 ②`SaveManager.AutoSave()` 실제 저장(빈 슬롯 우선, 없으면 가장 오래된 슬롯 덮어쓰기)
- `Systems/SaveManager.cs` — `Application.persistentDataPath/saves/slot_{n}.json`, 마을 음식점/마약 재고·고용 시장 카운트 등 상태 수출(v4)
- 수면 누움 강제: BedRestEnforcer(39차 — 실내 배치 경로 전용)

### ② 실내 훈련장 배치 — **규약상 이미 허용, 실측 과제만 잔존**
- `ConstructionManager.cs` L234 — 훈련소 전용 검증(영지당 1개) 주석 명시: **"실내 배치는 허용: 음식점 금지 규약과 무관"** (음식점만 실내 금지 — 90차)
- 실물/상호작용도 구현돼 있음: FacilityPropBuilder.BuildTrainingHallStation(125차) + TrainingHallWindowUTK + effectId training_hall 라우팅(ConstructionWindowUTK L255)
- **미확정**: 실내 씬에서 건설창을 여는 경로(현 개방점 = AttackFormationUTK "건물짓기" 버튼)와 TryPlace의 영지 판정(ValidatePlacement 영지 경계/ResolveTerritoryAt/TerrainFlatCheck 지면 레이캐스트)이 **실내 좌표계에서 성립하는지** — 이건 구현 전 실측으로 확정 필요

### ③ 건설창에 훈련장 없는 원인 — **확정: 목록 배열 누락**
- `ConstructionWindowUTK.FacilityIds`(L141) = **bp_cooking/forge/restaurant/barracks/alchemy 5종뿐 — bp_traininghall 누락**
- RefreshBlueprintList(L798): 시설 탭 = FacilityIds 루프만 / 기타 탭 = 시설 제외(기존 O8 8종만) → **bp_traininghall은 어느 탭에도 표출되지 않음** (BlueprintData에는 14종 중 존재, 실물 빌더도 완성 — UI 목록만 빠진 상태)

## 2. Phase 분해

### P0 — 건설창 훈련장 항목 추가 [핵심]
- **목표**: 건설창 좌 목록에 훈련소 카드 표출 — 기존 시설 카드와 동일 스타일/그리드/폰트(BuildFacilityItem 빌더 재사용이므로 배열에 넣는 것만으로 동일 외관 보장)
- **구현** (`UI/Toolkit/ConstructionWindowUTK.cs`):
  1. FacilityIds에 `"bp_traininghall"` 추가 (5종→6종, 기타 탭은 IsFacilityBlueprint 기반이라 자동 조정)
  2. FacilityRoleLabel에 훈련소 부제목 추가 (기존 문구 톤 준수 — 예: "훈련 · 병사 훈련과 전투 훈련", Figma 항목 부제목 11/400 #8B949E 체계)
  3. 탭 카운트 라벨("전체 N"/"기타 N") 자동 반영 확인
- **계약 갱신 목록(소멸 예정 — 같은 위임에서 처리)**: ConstructionWindowTests/ConstructionWindowFigmaTests에서 시설 5종/기타 8종/"전체 N" 카운트·FacilityIds 배열 어서션 전수 grep → 6종/기타 8종 유지로 갱신 (125차 카운트 계약 Count14 변형 주의 — 메서드명 속 수치 포함)
- **검증**: 신규 소스계약 테스트(FacilityIds에 traininghall 포함+RoleLabel 존재, _152 접미) + ConstructionWindow 패밀리 집중 GREEN

### P1 — 실내 배치 가능성 실측 → 필요 시 수리
- **목표**: 실내 씬에서 훈련장이 실제로 지어지는 경로 완결 확인
- **실측(구현 서브가 코드로 판정)**:
  1. 건설창 실내 개방 경로 — AttackFormationUTK "건물짓기"가 실내 씬 UI 체계에서 도달 가능한지 (UTKWindowManager 게이트/실내 HUD)
  2. TryPlace 판정 체인의 실내 좌표계 성립성 — region/영지 판정(ResolveTerritoryAt), ValidatePlacement(영지 경계), TerrainFlatCheck(지면 레이캐스트 — 실내 바닥에서 성립?) — 각 단계가 실내 씬에서 거부하는 지점 유무를 코드로 확정
- **수리 방향(실측 결과에 따라)**: 거부 지점이 있으면 **실내 씬 한정 분기**(IndoorSceneTransition 게이트 — 소유 영지 판정 승계/지면 검증 스킵) 추가. 음식점 실내 금지 규약은 불변.
- **검증**: 실내 배치 소스계약/판정 함수 테스트 (실내 게이트 순수 함수로 분리 — EditMode 직접 판정)

### P2 — 건설 완결 연출 확인
- 시공(ConstructionHologram→완료→FacilityPropBuilder 실물→E키→TrainingHallWindowUTK) 체인은 125차 구현 존재 — P0/P1 수리 후 **소스계약으로 체인 무결성 핀**(bp_traininghall이 ConstructionManager.TryPlace/FacilityPropBuilder/TrainingHallWindowUTK 3점 연결) + Play 육안 게이트

## 3. 순서·의존성

P0(독립 — 건설창 UI) / P1(독립 — 실내 판정) 병렬 가능(파일 소유권 분리: P0=ConstructionWindowUTK+건설창 테스트, P1=ConstructionManager+신규 판정 테스트). P2는 P0+P1 완료 후 마무리 계약테스트.

## 4. 검증 공통

- 컴파일 error CS 0 → EditMode 전수 baseline 1:1(151차 qa_151 1834/1824/10 대비 — Melee 해소로 baseline은 10건) → 집중(ConstructionWindow/TrainingHall 패밀리) GREEN
- Play(사용자 승인): ①건설창 좌 목록에 훈련소 카드(동일 스타일) ②재료/골드 소비 후 실제 시공 ③실내 씬에서 배치 성립 여부 ④완성 후 E키 → 훈련 UI 개방

## 5. 미확정(사용자 확인 대기)

- 훈련소 부제목 문구: "훈련 · 병사 훈련과 전투 훈련" 안으로 초안 — 원하는 문구 있으면 지정
- 실내 배치 위치 제약: 실내에서도 영지당 1개 제한을 그대로 적용(기존 규약 유지) — 실내/야외 합산 1개
