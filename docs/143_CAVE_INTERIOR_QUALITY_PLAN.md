# 143차 — 동굴 내부 씬 고품질화 + 지하 5층 계획

작성: 2026-10-30 · 상태: **승인 대기 (실행 전)** · 선행: 141차 텍스처 계약(무조건 고품질), 139차 P4 17꼭짓점 다각형 동굴

사용자 요청 확장(10-30 2차):
- ① 지하로 내려가는 길(하강 갱도) ② 1층 입구에 실제 문 ③ 동굴 전용 몬스터(GLB는 나중에 추가) ④ **지하 5층까지** ⑤ 보물상자 드롭 1~2개.

---

## Stage A — 1층(입구층) 고품질화

### 0. 조사로 확정한 현재 상태 (실측)

- `CaveInteriorBuilder.cs`(534줄): 17꼭짓점 다각형(반경 8.6~11.2m) 바닥 메시 + 변별 백플레인 벽(두께 0.3, 높이 3.2) + rock GLB 벽링 + 7 횟불(TorchFlicker) + 화로 + 보석상자 4 + 채굴노드 3. 천장 없음(탑다운 계약).
- 표면은 **전부 단색 머티리얼**(바닥 0.25/벽 0.22/스트립 톤 2종) — 텍스처·노멀 0.
- `Test_21_CaveInterior` 씬명이 "Interior"를 포함 → **AERO 포그(0.12)·IndoorPostProcess(블룸/비네트)는 이미 적용 중**.
- 성내부 H1~H5(10-10 확립) 품질 공식이 동굴에는 **미이식** — 벽톱면 캡/블롭섀도우/존틴트/마모 전부 부재.
- 재사용 선례: `IndoorMaterialFactory.CreateBlobShadowMat()`(H2), `CreateFloorWearMat()`(H5), `CreateEmberUnlit(HDR)`, `NaturalResourceSpawner.LoadRockVisuals()`, `IndoorFurnitureCatalog.CreateCrate`(전리품 상자 GLB 2종 풀).

### 조사에서 발견한 기존 결함 2건 (P0/P4에서 봉합)

1. **[결함-A] Test_21 플레이어 스폰 좌표 낡음** — `(0,1.5,-3.5)`는 137차 사각 룸 시절 좌표. 139차 다각형 전환으로 입구가 남측(+z, v1→v2 변 z≈9)으로 이동했는데 갱신 안 됨 → 플레이어가 **북쪽 심부(숨은 상자 옆)에 스폰**됨.
2. **[결함-B] 화로 라이트 range가 플리커에 클램프됨** — `TorchFlicker.Update`가 range를 절대값 5~7로 Lerp → 화로 대형광(range 9)은 매 프레임 7 이하로 강제 다운. (TorchFlicker 사용처는 동굴+MedievalShellBuilder뿐 — 옵트인 수리 안전.)

### 1. Stage A 설계 원칙

- **탑다운 계약**: 천장 금지 유지. 신규 요소는 벽 톱 캡/바닥 데칼/접지 쿼드뿐.
- **횟불 ≤8 계약**: 신규 Light **0건**. 존틴트는 기존 7기 색만 변경.
- **결정론 계약**: 신규 배치 전부 하드박제. 기존 rng 소비 순서 원칙 유지(P11 드롭 추첨은 예외 — 시드 결정론이므로 재빌드 동일 보장).
- **텍스처 계약(141차)**: 기존 에셋 파일 무변경. 신규 절차 텍스처는 신규 생성(바닥 2048², 벽 1024², 노멀 동반).
- **콜라이더 단일소스**: 바닥 메시+변 벽만. 캡/스탬프/블롭/불꽃/장식석은 콜라이더 없음.

### P0 — 사전봉합: Test_21 스폰 좌표 정합 [결함-A]
- **파일**: `CaveInteriorTestSetup.cs`, `CaveInteriorBuilder.cs`(스폰 헬퍼 1개)
- 빌더에 `public static Vector3 GetEntranceSpawnPosition(int tier)` 신설 — 입구 변 중점×tierScale에서 내부 1.5m 안쪽 하드박제. `EnsurePlayer` 교체 + 낡은 주석/OnGUI 안내문("횟불 4개"→7개 등) 갱신.
- **검증**: 컴파일 CS 0 + focused `CaveInteriorSceneTests` 7종.

### P1 — 표면 고품질: 절차 암반 텍스처 (바닥/벽/톤스트립)
- **파일**: 신규 `CaveMaterialFactory.cs`(+meta), `CaveInteriorBuilder.cs`
- 절차 암반 컬러+노멀(해시 노이즈 결정론, 정적 캐시). 바닥 2048², 벽 1024², 노멀 실패 시 컬러 폴백.
- 바닥 메시 타일링 + 변 벽 16개 변 길이별 `mainTextureScale` + 톤 스트립 5종 텍스처화(광장 밝은 암반/통로 어두운 흙 유지).
- **검증**: 컴파일 + focused(바인딩/존재) + Topology 6/6 무변경.

### P2 — 경계 리딩: 벽 톱면 캡 + 입구 지보목 (H1 이식)
- **파일**: `CaveInteriorBuilder.cs`
- `CaveWallCap_E{nn}` 16(폭 0.55, y=height+0.05, 입구 변 제외) + 입구 지보목(포스트 2 + 린틀, wood_frame 재사용, 콜라이더 없음).
- **검증**: 컴파일 + focused(캡 16/이름 규약).

### P3 — 접지·마모: 블롭 8 + 마모/재 스탬프 12 + 입구 빛웅덩이 (H2/H5 이식)
- **파일**: `CaveInteriorBuilder.cs`(팩토리 재사용)
- `CaveBlob_*` 8(상자4·노드3·화로1), `CaveWearStamp_00~11`(알파 ≤0.22), `CaveEntranceLightPool` 1(EmberUnlit 저강도).
- **검증**: 컴파일 + focused(카운트/이름 규약).

### P4 — 화로·횟불 고도화: 돌링 + 불꽃 시각 + 존틴트 + TorchFlicker 수리 [결함-B]
- **파일**: `CaveInteriorBuilder.cs`, `TorchFlicker.cs`
- 화로 주변 rock GLB 8(45° 하드박제), 횟불 7기 불꽃 쿼드(EmberUnlit HDR — Light 추가 0), 존틴트(입구=중립웜/화로 3기=딥오렌지/서갱도=카바이드 쿨/동저장고=앰버/북심부=청백 저광), `TorchFlicker._useBaseRelative` 옵트인(기본 false 무변경 — 화로 range 9 실동작 수리)+`ConfigureFlicker`, 중앙광 1.2→0.6.
- **검증**: 컴파일 + focused + Play 화로 광역 실측.

### P5 — 소품 GLB: 보석상자 치환 + 채굴 잔석
- **파일**: `CaveInteriorBuilder.cs`(필요 시 `IndoorFurnitureCatalog.cs` 래퍼 소량)
- 보석상자 4: 루트(이름/좌표/BoxCollider/GemChest) 불변, 큐브 시각 off + 전리품 상자 GLB 자식. 채굴 노드 3: 큐브 불변 + 잔석 rock GLB 2개씩.
- **검증**: 컴파일 + focused Topology + Play E키 개방.

### P6 — Stage A 검증: 동굴 고품질 계약 테스트
- **파일**: 신규 `CaveInteriorQualityTests.cs`(+meta)
- P0~P5 요소 이름규약/카운트/부재계약(천장·신규 콜라이더 0)/스폰 헬퍼/TorchFlicker 기본값 false 단정.
- **검증**: focused(신규+기존 동굴 6종). 전체 회귀는 P12에서 1회.

---

## Stage B — 지하 다층 동굴 시스템 (신규 요청 반영)

### Stage B 설계 원칙

- **탑다운 계약 = 수직 적층 금지** → 지하 5층은 **층 전환 방식**: 층마다 별도 동굴 Room, 하강/상승 갱도 포탈(E키)로 전환 시 기존 Room 파괴→다음층 Room 빌드→플레이어 텔레포트. 한 화면엔 항상 1개 층만 (성내부 층 없음 계약과 동일 논리).
- **시그니처 계약 유지**: `BuildCaveInterior(territoryId, tier)` 그대로(IndoorSceneTransition/테스트 호출부 무영향) — `floorIndex`는 새 오버로드 `BuildCaveInterior(territoryId, tier, floorIndex)`로 추가(기본 0=1층=현행 레이아웃).
- **진입/퇴장 계약 유지**: 지상 `CaveEntrance`→`IndoorSceneTransition` "cave" 케이스(현행)로 1층 진입. 1층 귀환 포탈은 기존 실내 퇴장 경로(CaveEntrance 저장위치 복원) 재사용.
- **층 구성(총 5층, 하드박제 5세트 레이아웃 — 결정론)**:
  - **1층 입구층** — 현행 17각 다각형(Stage A 고품질 전체 적용) + 입구 문(P8) + 하강 갱도.
  - **2층 채굴 갱도층** — 채굴 노드 확대(Stone/IronOre 증가 배치, ResourceType 기존 범위) + 갱도형 좌우 통로 레이아웃.
  - **3층 지하수·수정층** — 지하수 웅덩이 데칼(발광 청록 수정 결정 = EmberUnlit 절차 결정) + 좁은 늪형 레이아웃.
  - **4층 동굴 보물고** — 보물상자 밀집 레이아웃(상자 수 증가) + 돌문·금고 분위기(쿨 틴트).
  - **5층 심연 단말** — 엘리트 동굴 몬스터 + 대형 보물 + 상승 포탈만 있는 막다른 단말.
- **깊이 무드**: 층별 앰비언트 강도 단계 축소(1층 0.10 → 5층 ~0.04) + 층별 횟불 색온도 변화 + `IndoorLighting` centralLight 층별 조정. AERO 포그/포스트는 기존 게이트 그대로.
- **층 안내**: 층 전환 시 기존 UTK 토스트 서비스로 "지하 N층 진입" 표시(신규 UI 최소화).

### P7 — 다층 인프라: CaveLevelDirector + 하강/상승 갱도 포탈
- **파일**: 신규 `Assets/Scripts/Systems/CaveLevelDirector.cs`(+meta), `CaveInteriorBuilder.cs`(오버로드+포탈 배치), `CaveInteriorTestSetup.cs`(층 지정 상수 — 기본 1층)
- **내용**:
  - `CaveLevelDirector`(정적 상태 관리): 현재 층, 층 전환(현재 Room 파괴→새 Room 빌드→플레이어 텔레포트→토스트), 1층 귀환은 기존 퇴장 경로 호출.
  - `CaveLevelPortal`(E키 포탈): 하강 갱도(각 층 끝단 — 지보목 프레임+암흑 터널 박스 시각+E 프롬프트 "지하 N+1층으로") / 상승 갱도(입구 쪽 "지하 N−1층으로"). 프롬프트는 CaveEntrance/GemChest 선례(근거리 E).
  - 갱도 시각은 탑다운에서 읽히게: 변 벽에 붙은 터널 개구부 + 내부 어둠 박스 + 바닥 하강 계단 데칼. 콜라이더 충돌은 기존 변 벽 유지 — 포탈은 E키 상호작용으로만 통과(문 닫힘 방지 포탈 근처 개방 처리).
- **계약**: 횟불 ≤8은 **층별** 상한(각 Room 독립 — 층당 ≤8 유지), 천장 부재 계약 전층 유지, 결정론(포탈 좌표 하드박제).
- **검증**: 컴파일 + focused(포탈 존재/디렉터 전환 상태 단정) + Play 1→2층 왕복.

### P8 — 1층 입구 문 (E키 개폐)
- **파일**: 신규 `Assets/Scripts/Systems/CaveEntranceDoor.cs`(+meta), `CaveInteriorBuilder.cs`(1층에만 배치)
- **내용**:
  - 입구 변(v1→v2) 중앙에 문틀(지보목 연결 통일 — wood_frame 재사용) + 목재 더블 패널 2짝.
  - E키 근접 개폐(토글): 열림=패널 x축 슬라이드(벽 안쪽 수납 — 탑다운에서 문짝이 통로를 막지 않음), 닫힘=패널 원위치+통로 막힘(패널 콜라이더는 열림 시 비활성 — **바닥/변 벽 외 신규 충돌은 이 패널 1개뿐**).
  - 기본 상태 **열림**(진입 흐름 방해 금지 — CaveEntrance 진입 직후 문 열림 상태로 시작). 결정론(문 좌표/크기 하드박제).
- **계약**: 상호작용 근거리 E 규약(다른 E 상호작용과 근접 우선순위 충돌 없게 문 프롬프트는 2.0m로 상자 2.5m보다 좁게).
- **검증**: 컴파일 + focused(문 존재/기본 열림/콜라이더 토글) + Play 개폐.

### P9 — 2~5층 레이아웃 (하드박제 5세트)
- **파일**: `CaveInteriorBuilder.cs`(FloorLayout 구조체 + 5세트 상수), `CaveLevelDirector.cs`
- **내용**:
  - 층별 고정 다각형 꼭짓점+팬 삼각분할(139차 선례 동일 방식 — 층별 좌표만 다름) + 층별 구역(위 테마 표) + 층별 횟불 배치(층당 ≤8).
  - 층별 무드 상수(앰비언트/중앙광/횟불 색온도) 하드박제.
  - 4층 보물고는 보물상자 6~8(드롭 1~2 적용), 5층은 엘리트+대형 보물 1(고정).
- **검증**: 컴파일 + focused(층 1~5 빌드 성공/결정론 재빌드 동일) + Play 각층 순회.

### P10 — 동굴 전용 몬스터 스포너 (GLB 후 추가 대응)
- **파일**: 신규 `Assets/Scripts/Systems/CaveMonsterSpawner.cs`(+meta), `MonsterDatabase`(동굴 몬스터 정의 추가), `CaveInteriorBuilder.cs`(층별 스폰 앵커 하드박제)
- **내용**:
  - 기존 몬스터 파이프라인 재사용: `AnimalAI`(SetMonsterId/SetLevel) + `MonsterHeadUI.Setup` + `ModelAnimatorAssigner`(GLB 자동 분기) — MonsterSpawner(야외 링 시스템)와 독립, 동굴 전용 소형 스포너.
  - `MonsterDatabase`에 동굴 몬스터 정의 추가: 1~2층 약체(예: cave_bat류) / 3~4층 중간(예: cave_lurker류) / 5층 엘리트+보스 자리 1. 레벨 = 층 가중.
  - GLB 레지스트리: `Assets/새로운 glb/동굴몬스터/` 폴더(사용자 후 추가 — 파일명 규약은 구현 시 확정). **GLB 부재 시 해당 층 스폰 스킵 + 로그 1회**(현행 동굴과 동일 상태 — 시각 에셋 투입 즉시 활성).
  - 배치 결정론: 층별 스폰 앵커 하드박제 + 시드(territoryId+층) 요터.
  - QA용 디버그 플래그(기본 OFF): GLB 부재 시 절차 폴백 몬스터 1종 파이프라인 검증용(AnimalAI/MonsterHeadUI 부착) — 승인 시에만 활성.
- **계약**: 야외 MonsterSpawner 링 시스템 무영향(동굴은 실내 — 야외 스폰과 별도), 실내 이동/전투 게이트(`UITransitionState.IndoorActive`) 기존 계약 내 동작.
- **검증**: 컴파일 + focused(스포너 구성/스킵 로그) + Play(GLB 투입 후 실기동 — 별도 게이트).

### P11 — 보물상자 드롭 1~2개
- **파일**: `GemChest.cs`(GemCount 공개 속성 추가 — `GemType` 선례), `CaveInteriorBuilder.cs`
- **내용**:
  - `GemChest.GemCount { get; set; }` 추가(기본 1 — 타 사용처 무영향). `OpenInternal`은 기존 `_gemCount` 사용 경로 그대로(속성이 필드 반영).
  - 동굴 빌더에서 상자 생성 시 시드 결정론 추첨: **1~2개** — 층 가중(1층 60%:40%, 5층 20%:80% — 하드박제 확률표, 시드 rng 단일 소비). 층 심화일수록 2개 확률 상향.
  - 드롭 2개 시 LootBasket에 ×2(기존 `AddItem(item, count)` 경로 — 별도 드랍 물체 2개 아님, 바구니에 2개).
- **계약**: Topology 테스트(상자 4/좌표) 불변 — 수량은 컴포넌트 속성이라 좌표 계약 무충돌. GemType 시드 소비 순서 뒤에 추첨 추가(재빌드 동일성 유지).
- **검증**: 컴파일 + focused(1~2 범위/시드 재현 동일) + Play 개방 확인.

### P12 — 전수 검증 + 기록
- **파일**: `CaveInteriorQualityTests.cs`(Stage B 계약 추가), QAPROGRESS
- **검증**: focused 전체(동굴 신규+기존 6종) → **전체 EditMode baseline 1:1**(현행 1737/1726/11, `qa137_baseline_fails.txt`) → QAPROGRESS 143차 기록.

---

## 3. 셀프 감사 (중복·키충돌·계약)

- **키 입력**: 신규 바인딩 0건 — 문/포탈 모두 기존 E키 근접 상호작용 규약(문 프롬프트 2.0m < 상자 2.5m로 우선순위 충돌 방지).
- **이름 충돌**: `CaveLevelDirector/CaveLevelPortal/CaveEntranceDoor/CaveMonsterSpawner/CaveWallCap_/CaveBlob_/CaveWearStamp_/CaveEntranceLightPool/CaveHearthRockRing` — 기존 소스에 부재 확인.
- **수량 계약**: 층당 횟불 ≤8 / 1층 상자 4 / 노드 3 / 좌표 박제 불변 — GLB는 자식 시각만. 4층 상자 증가는 신규 층 레이아웃이라 1층 계약과 무충돌.
- **TorchFlicker 폭발력**: 사용처 동굴+MedievalShellBuilder뿐, 기본 false 불변.
- **실내 진입 계약**: `BuildCaveInterior(territoryId, tier)` 시그니처 불변(기존 호출부 2곳 무영향), floorIndex 오버로드만 추가.
- **탑다운 계약**: 수직 적층 없음(층 전환 방식), 신규 콜라이더는 입구 문 패널 1개(닫힘 시만).
- **존매트 금지 계약**: 신규 데칼은 H5 마모 계열(성내부 승인 방식), 존매트 아님.
- **URP 라이트 예산**: 층당 13(중앙+앰비언트+횟불≤8+상자광) — 신규 Light 0건.

## 4. Play 게이트 (사용자 승인 항목)

- Stage A: 탑다운 캡처 — 벽 리/암반 표면/화로 불꽃+광역/입구 빛웅덩이·지보목/존틴트/상자 GLB/블롭·마모.
- Stage B: ①입구 문 개폐 ②1→2→…→5층 하강/상승 순회 ③층별 무드 변화 ④4층 상자 다수+드롭 1~2 확인 ⑤(GLB 투입 후) 동굴 몬스터 실기동.
- 튜닝선(1줄씩): 중앙광 0.6, AERO 포그 0.12 유지 판정, 마모 알파, 층별 앰비언트.
- 커밋은 별도 승인(누적 미커밋 유지).

## 5. 위임 구조

- 구현/QA 서브에이전트 분리 위임(docs/AGENT_DELEGATION_POLICY.md), flock 락 파일 사용.
- 서브에이전트 타임아웃 3회 이상 반복 시 부모 직접 실행(64차 선승인 폴백).

## 6. 실행 순서 요약

P0 → P1~P5 → P6(Stage A 검증) → P7~P11 → P12(전수). 각 Phase마다 컴파일 CS 0 + focused 게이트. Play는 Stage A 완료 시점과 Stage B 완료 시점 2회 게이트.
