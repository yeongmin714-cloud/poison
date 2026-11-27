# 130 — 동물의 숲식 획득 연출(Acquisition Cinema) 계획

> 상태: 구현 지시 승인(2026-10-29 사용자 확정 — "플레이어 액션만 연출, 병사는 표시 생략").
> Play·커밋은 별도 승인 게이트 유지. 실측 기반 — 소스 실측 2026-10-29.

## 0. 사용자 요구 (그대로)

1. **환호 확장**: 벌목·채광 마무리 시에도 환호 애니 (현재 채집만 배선)
2. **낚시 성공**에도 환호 애니
3. **기존 획득 UI 폐기**: 채집/채광/낚시/벌목/농경 후 뜨는 결과창(HarvestResultUTK)은 "그게 아니고" → 제거
4. **상반신 카메라**: 환호 애니 중 플레이어 상반신이 보이는 카메라
5. **손끝 GLB**: 무엇을 획득했는지 플레이어 손끝에 GLB가 바로 보이도록
6. **NPC 대화 패널**: "~을(를) 획득했다" 대사 표시
7. = 동물의 숲 아이템 획득 연출
8. **병사 자동작업(채집/채광/농경)은 연출 없음** — 인벤/저장고 반영만 (사용자 확정)

## 1. 현황 실측 (소스 근거)

| 액션 | 성공 지점 | 환호(Cheer) | 결과창 publish |
|---|---|---|---|
| 채집(약초/과일/작물/들꽃) | HerbPickup.Harvest | ✅ L320 직접 호출 | ✅ L340 |
| 벌목/채광 | ResourceNode.HarvestForPlayer (L152 — E키 채널 콜백, **플레이어 전용**) | ❌ | ✅ L163 (immediate:true) |
| 낚시 | FishingSystem 성공 2분기 (L376/L424) | ❌ | ✅ L379/L427 |
| 농경(밭 약초) | FarmingSystem.Harvest (L178~) | ❌ | ❌ (로그만) |
| 크래프트/요리 | CraftingUI/CookingUI | ✅ (129차) | 별도 창(CraftResultPopup) — 이번 범위 외 |

- **병사 경로 분리 이미 완결**: 병사 채굴=`ResourceNode.TryAutoMine`(LootBasket 지급, publish 없음), 병사 농경=GuardTaskSystem(저장고). publish 지점은 전부 플레이어 경로 → **byPlayer 플래그 불필요**.
- **결과창 소비 구조**: `HarvestResultBridge`(static publish) → `HarvestResultUTK`(UTKWindowBase, 16ms 폴링 소비). asmdef 역전 회피용 브리지 패턴 — 신규 연출도 동일 패턴으로 소비.
- **카메라**: `DialogueCameraDirector.Begin(Component, string, bool fullBody=true)` — 전신 1.8m 고정 계약(FullBodyFixedFrameH/FullBodyFov=40, 고정 베어링+LookAt 갱신+패널 닫힘 폴링 자동 End). 계약 테스트가 기존 시그니처 참조 → **기존 오버로드 무수정, 상반신 신규 오버로드 추가**.
- **대화 패널**: `NPCDialoguePanelUTK.OpenForStatic(Component target, string displayName, string npcLine, string subLine=null)` — 임의 텍스트 표시 가능.
- **GLB 자산 실측**:
  - 물고기 333종 `Assets/새로운 glb/crops-fish/fish-*.glb` (FishCatalog.GlbPath)
  - 작물 100종 `crop-*.glb` (CropCatalog.GlbPath)
  - 약초 `herb_{red,purple,gold,blue,green}.glb` (UserProvided)
  - 음식/장비 GLB 다수(UserProvided) / **목재·석재 단품 GLB 부재** → 임시 폴백 지정(아래 표), 사용자 에셋 교체 대비 매핑 테이블 분리
- **GLB 로딩 선례**: `NaturalResourceSpawner.LoadVisualPrefabs` — `#if UNITY_EDITOR` AssetDatabase GLB 로드 → 실패 시 Resources 프리팹 폴백. 동일 패턴 준용.

## 2. 설계 결정 (근거)

1. **신규 `AcquisitionCinema`(MonoBehaviour, Systems)** — HarvestResultBridge의 소비자를 HarvestResultUTK에서 교체.
   - 16ms 폴링 소비(LatestToken/IsConsumed/MarkConsumed 계약 그대로 — HarvestResultUTK 패턴 복사).
   - publish 소비 시: ①대화 패널 표시 ②손 GLB 부착 ③카메라 상반신 Begin. **환호 트리거는 기존 129차 직접 호출 구조 유지**(아래 P1 — 발화 이중 방지).
   - 종료: 환호 클립 길이(4.7s)+0.3s 마진 자동 종료(카메라 End+GLB 제거+패널 Hide). 이동 입력(WASD/Space) 감지 시 조기 종료+드라이버 TriggerActionEnd. 잠금(입력 차단) 없음 — 최소 침습.
   - 연출 중 연출 재발행(낚시 직후 채집 등) → 진행 중이면 종료 후 재시작(리셋).
2. **환호 배선 추가(129차 패턴 준용 — PlayerActionAnimContractTests 소스 계약과 같은 방식)**:
   - `ResourceNode.HarvestForPlayer` 성공 직후 TriggerCheer(드라이버 획득 패턴 — HerbPickup L297~ 선례 복사).
   - `FishingSystem` 성공 2분기 각각 TriggerCheer.
   - 기존 HerbPickup L320/WeaponForgeUTK/CookingUI TriggerCheer 무수정.
   - **[130차 후속 정정]** FarmingSystem.Harvest는 **병사 자동 농경 전용 경로**(GuardTaskSystem L997 유일 호출부)라
     publish/환호 배선 **미포함으로 확정**("병사 자동작업은 제외" — 사용자 재확인). 플레이어 밭 수확은 FarmPlot
     수확 채널 → HerbPickup 완료 콜백(환호+publish 기존 배선)으로 연출 커버 — 이중 배선 불요.
     병사 경로 4종(TryAutoMine/Harvest/TryAutoGather/HarvestDirectly) 전부 publish 부재 — 소스계약으로 고정.
3. **카메라 상반신 모드**: `DialogueCameraDirector.BeginUpperBody(Component target, string displayName)` 신설 — 내부적으로 기존 기계(고정 베어링/yaw 위임/패널 폴링 End) 재사용, frameH≈1.05m·center≈가슴(≈1.15m)만 분기. 기존 Begin 3종 오버로드와 fullBody 계약(1.8m/테스트) 무변경. CameraFollowSuppressed 게이트는 기존 대화카메라 경로가 이미 처리(추가 작업 없음).
4. **손끝 GLB `AcquisitionItemVisualResolver`(static, Systems)** — 매핑 테이블 결정론:
   - id 접두 `fish_glb_` → FishCatalog.GlbPath(인덱스)
   - crop 계열(_cropKey/id crop_*) → CropCatalog.GlbPath
   - 약초 herb_yakcho → HerbType 색 매핑(red→herb_red, purple→herb_purple, yellow→herb_gold, silver→herb_blue, green/기본→herb_green)
   - mat_wood → `UserProvided/stake.glb`(임시) / mat_stone → Resources `IdyllicPrefabs/Rocks` 첫 프리팹 축소(임시) — **사용자 GLB 준비 시 매핑 1줄 교체**
   - 음식/장비 → UserProvided GLB 파일명 매핑(존재분만 — 미상 id는 폴백)
   - 로드: AssetDatabase(에디터) → 실패 시 Resources 폴백 → 최종 실패 시 GLB 생략+패널만 표시(연출 중단 없음).
   - 부착: 플레이어 오른손 본(RightHand) 자식 인스턴스, 로컬 오프셋/스케일 상수(0.6 기본 — 손끝에 보이도록 튜닝), 환호 종료 시 파괴. armor-attach-grip 선례(본 탐색+월드 보정).
5. **대화 패널 텍스트**: kind별 동사 — 벌목 "벌목 성공! {itemName} x{count}을(를) 획득했다!" / 낚시 "{itemName}을(를) 낚았다!" / 채집·농경 "수확했다!" (HarvestResultBridge.LatestResultVerb·LatestItemName·LatestCount 그대로 소비 — 문자열 가공은 director 내부). displayName=빈 문자열(화자 이름 미표시 — AC 나레이션 톤).
6. **HarvestResultUTK 폐기**: Ensure() 호출부 제거+인스턴스 파괴 가드(연출 전환), **코드는 잔존**(Play 검증 후 삭제는 별도 승인 — 미동작 경로 잔존 선례). HarvestResultBridge 시그니처 무변경(소비자만 교체).
7. **계약 테스트 갱신(같은 사이클)**: PlayerActionAnimContractTests에 ResourceNode/FishingSystem TriggerCheer 소스 계약 추가. 신규 AcquisitionCinemaTests(GLB 매핑 결정론/패널 텍스트 가공/병사 경로 미연출 — TryAutoMine에 publish 없음의 소스 계약). DialogueCameraDirector 기존 테스트 무수정 확인.

## 3. Phase 구분

- **P1 환호 배선 확장(코드)**: ResourceNode+ FishingSystem TriggerCheer, FarmingSystem publish+TriggerCheer. 계약 테스트 갱신.
- **P2 카메라 상반신(코드)**: DialogueCameraDirector.BeginUpperBody 신설(기존 경로 0변경).
- **P3 손 GLB(코드)**: AcquisitionItemVisualResolver(매핑+로드+부착/제거).
- **P4 연출 지휘(코드)**: AcquisitionCinema(브리지 소비→패널/GLB/카메라 오케스트레이션+종료 로직).
- **P5 결과창 폐기(코드)**: HarvestResultUTK Ensure() 제거.
- **P6 QA(별도 서브에이전트)**: 컴파일 0 + EditMode 신규/관련 계약 전부 통과 + 증거 로그.

## 4. 완료 기준 / 검증

- 컴파일 error CS 0.
- EditMode: PlayerActionAnimContractTests(갱신본) + AcquisitionCinemaTests + 기존 관련(Npc2026/DialogueCameraDirector 계약) 통과.
- baseline 11건 1:1 유지(씬 구조 무변경 — 컨트롤러 에셋 수정 없음, 모두 코드 배선).
- Play 검증(카메라 프레이밍/손 GLB 위치/패널 타이밍 실눈)은 별도 승인 후.
