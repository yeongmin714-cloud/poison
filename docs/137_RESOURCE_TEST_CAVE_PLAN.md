# 137차 계획 — 자원 채널 테스트 씬 재건 + 동굴 실내 테스트 씬 + 동굴(지하 던전) 전체 설계

작성: 2026-10-30 (137차). 사용자 요청 3건: ①채광/채집/벌목/농경 테스트 창 재건 ②동굴 실내씬 테스트 창 신규 ③동굴 실내 전체 설계(깜깜한 지형 + 횟불 조명) — ③은 계획만(승인 대기), ①②는 구현 승인됨.
상태: 계획 + 위임 지시서. Play·커밋 별도 승인.

## §1 발견 사실 (실측)

1. **Test_15_Livelihood.unity는 존재**하고 배선도 산다 — `TestTerritoryCombatSetup.SetupLivelihoodTestScene()`(L399)이 낚시/벌목(나무 GLB+스웨이)/채광(돌·광맥)/채집(허브)/농경(밭) 전 채널을 배치(L406 확인 로그에 "벌목(E 채널·나무 흔들림·통나무)" 명시). 삭제/파손 이력 없음(git deleted 이력 7월분뿐). 사용자가 못 찾은 것일 가능성(씬명이 "Livelihood"라 식별 어려움).
2. 그럼에도 사용자 요구대로 **전용 신규 씬 Test_20_LivelihoodResources**를 만든다 — Test_15의 구조(3 GameObject + TestTerritoryCombatSetup guid 6e49b159612c4ba5a0d20cdd6263ae62)를 그대로 계승하는 복제 씬. 씬 파일명에 "Livelihood" 포함 → 기존 `scene.name.Contains("Livelihood")` 게이트(L35)가 자동 발동 = **프로덕션 코드 0수정**으로 검증된 4채널+마을/음식점 셋업 전부 재현.
3. **동굴은 이미 빌더가 있다**: `CaveInteriorBuilder.BuildCaveInterior(territoryId, tier)`(238줄) — 박스 룸+보석상자 1~3(랜덤)+앰비언트 (0.02,0.02,0.06)@0.10+벽면 횟불 4개(TorchFlicker Perlin 플리커). 단 ①천장 큐브 존재(탑다운 천장 금지 계약 위반 소지) ②박스 기하(암석감 없음) ③채굴 노드 없음.
4. **테스트 씬 직접구축 선례**: Test_17/19(LordInteractionTestSetup/LordPeaceVisitTestSetup) — 신규 부트스트래퍼 + 최소 씬 yaml + 소스계약 테스트. Test_19가 최신(씬 3 GameObject + m_Script guid 참조, meta 수기 발급 실측 통과).

## §2 구현 (위임 — 2분할 병렬, 파일 소유권 분리)

### T1 — Test_20_LivelihoodResources (자원 채널 재건)
- Assets/Scenes/TestScenes/Test_20_LivelihoodResources.unity = Test_15.unity yaml 동일 구조 복제(m_Script guid 6e49b159… 그대로 — 부트스트래퍼 공유).
- .meta는 수기 작성 금지(113차 교훈) — 씬 파일만 두고 배치컴파일 1회로 Unity 임포트가 meta 생성 → 생성 확인.
- 검증: 배치컴파일 exit 0 + EditMode 소스계약 1케이스(씬 파일 존재 + 씬 yaml의 m_Script guid가 TestTerritoryCombatSetup guid와 동일 어서션 — 128-1 §4 선례).
- **절대 금지**: Test_15_Livelihood.unity 및 TestTerritoryCombatSetup.cs 수정(공유 계약 보호).

### T2 — Test_21_CaveInterior (동굴 실내 테스트 씬)
- 신규 3종: `Assets/Scripts/Systems/CaveInteriorTestSetup.cs`(128-1 패턴 — Awake 최상단 씬 이름 게이트 `Test_21_CaveInterior`) + 씬 yaml + 소스계약 테스트 `CaveInteriorSceneTests.cs`.
- 셋업: `CaveInteriorBuilder.BuildCaveInterior("cave_test", 2)` → **천장 숨김**(탑다운 천장 금지 계약 — CaveCeiling renderer.enabled=false 또는 HideCeiling 선례 재사용, 원본 보존) → `NeutralizeSceneSunlights`+조명 보정(Test_17 ApplyTestLightingWhenReady 선례 — DNC 봉쇄+방향광 0) → 플레이어 스폰(입구) → 보석상자 상호작용 확인 로그.
- 시퀀스: .cs 작성 → 배치컴파일(Unity가 script .meta+guid 생성) → guid read → 씬 yaml 작성(m_Script guid 참조) → 씬 meta는 수기 발급 가능(128-1 실측 통과 형식: fileFormatVersion 2 + 소문자 guid 32hex) → 재배치컴파일로 씬 임포트 검증.
- 검증: 컴파일 0 + 소스계약(씬 게이트 문자열/빌더 호출/천장 숨김 호출) + EditMode 전수는 별도 QA 라운드.

## §3 동굴 실내 전체 설계 — "깜깜한 지형 + 횟불" (사용자 승인 대기)

> 설계 원칙: ⭐탑다운 계약 유지(천장 금지 — 어둠은 지형이 아니라 조명으로), 현실적 지형, 과밀 배제, 기능공간 독립, 국가색 없음(중립 지하).

### 3-1 공간 구조 (단일 Room, 비대칭 — 24×18m 기준, tier 확장)
| 구역 | 위치 | 내용 |
|---|---|---|
| 입구 사면 | 남측 (0,−z) | 경사 통로 + 횟불 1 + 갈치불빛 안내 |
| 중앙 동굴 광장 | 중심 | 대형 화로(횟불 3중 — 시각 앵커), 지형 요철 |
| 서쪽 채굴 갱도 | 좌측 | 광맥 노드 3~5(채광 채널 — MiningMission/HoverTargetClassifier 연동, 지명 광물), 낮은 천장감은 벽 상단 캡으로만 |
| 동쪽 저장고 | 우측 | 보석상자 2~3(기존 GemChest 계약) + 석상/항아리 소품 |
| 북쪽 심부 갱도 | 상층 | 막다른 갱도 1 + 숨은 상자 1 — 루프 없는 단말(위험 연출) |
| 순환 통로 | 외곽 링 | 구역 연결, 폭 3m 이상, 막다른 길 최소화 |

### 3-2 지형/머티리얼
- 벽: 박스 → **암석 GLB 재배치**(NaturalResourceSpawner.LoadRockVisuals 재사용 — 결정론 시드 territoryKey 기반, 과밀 배제), 벽 뒤 백플레인 컬러로 틈 메움.
- 바닥: 암석 텍스처(IndoorTexturePipeline 선례 — Resources/Indoor 불변 계약), 구역별 톤 차등(광장 밝은 암반/갱도 어두운 흙).
- 천장: **부재 유지**(탑다운 계약) — 벽 상단 암석 캡(벽톱면캡 H시리즈 선례)으로 실내감만.

### 3-3 조명 (핵심 — "횟불로 밝히는")
- 앰비언트: (0.02,0.02,0.06)@0.10 유지(현행) — 기본은 거의 암흑.
- 횟불 포인트라이트: 웜 오렌지 (1.0,0.6,0.2), 구역당 1~2개, 총 ≤8(성능 상한), **TorchFlicker 전부 유지**(Perlin 플리커).
- 그림자: 주요 2개(중앙 화로·입구)만 Soft, 나머지 None + 블롭섀도우 병행(탑다운 H시리즈 선례).
- 밝기 격차 연출: 횟불 반경 밖은 침침 — 카메라 근처만 선명한 "탐험" 독법. 라이트 프로브/거리 컬링은 후속 옵션.

### 3-4 상호작용/연동
- 보석상자(기존 GemChest 상등), 채굴 노드(E 채널 — HoverTargetClassifier 채굴 분류 재사용), 횟불 자체 픽업(손에 든 횟불=주변 조명 반경 확대)은 **후속 옵션**(본 설계 범위 밖, 별도 승인).

### 3-5 구현 단계(승인 후)
- P-A: CaveInteriorBuilder 개편(구역/암석 벽/횟불 재배치 — 결정론 시드, Random 0건 계약 유지 — 배회 실내 변형 규약 준수).
- P-B: 소스계약 테스트 신설(CaveInteriorTopologyTests — 구역 그래프/횟불 수 ≤8/플리커 컴포넌트/천장 부재/보석상자 배치 — CastleInteriorTopology 선례).
- P-C: Test_21_CaveInterior 재판정(개편 빌더 사용) → 컴파일 0 + EditMode baseline 1:1 → Play(사용자).

## §4 승인 게이트
- §2 T1/T2는 즉시 위임(사용자 요청 승인 범위). §3 동굴 전체 설계는 **본 계획 승인 후** P-A~C 진행. Play·커밋은 상시 별도 승인.
