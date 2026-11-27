# [116차] 계획 — 월드맵 통합 레이아웃 / F키 상점NPC 패널 / 음식점 가시성·재건설 / NPC 1.8m+애니

> 상태: **계획 수립 — 구현 전 사용자 승인 대기.** 구현·QA는 승인 후 서브 분리 위임.
> 조사 근거: Screenshots/월드맵 ui.PNG(10:38) vision 실측 + Editor.log + 코드 라인 실측.

---

## 0. 실측 요약 — 4건 결함 근원

### R1. 월드맵 위/아래 분리 + 마커 배경 이탈 (스크린샷 판정)
- **스크린샷 실측**: 상단 절반=빈 어두운 공간+나침반 고립, 하단에만 콘텐츠 쏠림. 양피지 배경은 중앙 좁은 띠(좌우 검은 레터박스). 마커+지명 라벨이 배경 밖 검은 공간에 흩어져 가독성 0.
- **코드 근원(실측)**:
  - `_mapCanvas.style.backgroundSize = Contain`(WorldMapWindowUTK L189) — 양피지는 **절차 생성 정사각 텍스처**(BuildParchment, L638~)라 넓은 캔버스에서 좌우 레터박스 → "중앙 좁은 띠".
  - 마커는 `_mapCanvas` 자식(L455)이지만 left/top이 **캔버스 전체 폭 %**(L473~474: `u*100%`, `(1-v)*100%`) → 레터박스 밖 좌표에도 배치 → "배경 밖 흩어짐".
  - 창 세로 레이아웃: legend(L157)+hudRow(L165)+canvas(L199, flexGrow) — 스크린샷상 캔버스가 남은 공간을 못 채움(상단 빈 공간) → ApplyCanvasBounds(L280~291: FigmaCanvasLayout.Apply(0,0,1920,1080)+compass sx/sy)와 실제 루트 크기 정합 검증 필요.
- **사용자 요구**: 위/아래 구분 없이 **하나의 지도에 마커가 지도 위에** 표시.

### R2. 상점 NPC F키 패널에 세금제안 없음 (코드 근원 확정)
- **PlayerMovement L658~659**: TerritoryNPCBehaviour 근접 F키 → **무조건 `TargetKind.NPC` 발행** → InteractionPanel NPC 4버튼(상태보기/대화하기/선물주기/퀘스트) — 상점/세금제안 없음. = 사용자 증상 그대로.
- 좌클릭 경로(HoverTargetClassifier.ClassifyAt L58 → IsShopTerritoryNpc L193)는 ShopNPC 분류 정상 — **F키 경로만 분류 누락**.
- InteractionPanelUTK ShopNPC 4버튼(상태보기/대화하기/상점/세금제안 — 115차 완료)은 이미 준비돼 있음 — 분류만 고치면 연결됨.

### R3. "음식점이 지어지지 않는다" — 로그 실측으로 판정 변경
- **Editor.log 실측**: `[Test15VillageDrug] ✅ 음식점 건설 완료(실제 건설 경로): West #12 pos=(-435,-82)` + `[RestaurantPropBuilder] 🏠 음식점 실물 생성: pos=(-435.4,-82.0)` — **건설·실물 생성 모두 성공**.
- 잔여 문제 후보: ①실물 가시성(마을 중심→성 방향 12m 위치 — 건물군과 분리/접지 불일치 가능) ②사용자 U키 재건설 시도가 **"이미 음식점이 있는 마을" 가드**(ConstructionHologram — 로그 다수)에 반복 거부 → "못 짓는다" 체감. 플래그는 있으나 실물이 안 보이면 교착.
- 수리: 진단 로그 + 접지 정합 + 가드 개선(아래 Phase).

### R4. NPC 크기/애니메이션 — 신규 상점 NPC가 캘리브레이션 미적용
- `SpawnVillageNpc`(Test_13/14/15 공용, TestTerritoryCombatSetup L813)은 캡슐→GLB 교체만 하고 **1.8m 캘리브레이션·Animator 부착이 없음** — 마을 상인이 플레이어와 크기 다름+애니 없음(정지 포즈).
- 마을 주민은 VillageNpcSpawner가 87차 캘리브레이션(1.8m 고정계약)+믹사모 Player_AC 적용 완료 — 갭은 SpawnVillageNpc 경로만.

---

## R1. 월드맵 통합 레이아웃 (UI)

### Phase B1 — 레이아웃 수리 (WorldMapWindowUTK, 부모 직접 권장 — 대형 UI 재구성은 위임 사망 클래스, 74차 교훈)
1. **캔버스 정사각 고정**: `_mapCanvas`를 지도 텍스처와 동일 비율(1:1)로 고정 — `aspect-ratio:1`(지원 시) 또는 GeometryChangedEvent에서 `height=width` 강제 + 수평 중앙 배치. 배경 `backgroundSize=ScaleAndCrop|Stretch`로 레터박스 원천 제거.
2. **마커 좌표계 정합**: 마커 left/top % 는 캔버스(=텍스처 영역) 기준이므로 1에서 자동 정합. WorldU/WorldV(WORLD_SPAN=3200) 산식 무수정.
3. **마커 스프레드 옵션 [사용자 결정 대기]**: 현재 u,v ≈ 0.34~0.66(영지 반경 ~500m vs span 3200)이라 지도 중앙 1/3에만 분포. "전체 지도에 고르게"를 원하면 span 축소(예: 1200) 또는 u,v 리매핑 함수 추가 — 지도 정확성(실측 비율) vs 시각 분포 트레이드오프. 기본안: span 유지+캔버스 정합(위치 정확도 우선).
4. **세로 레이아웃**: legend/hudRow 고정 높이 + canvas가 잔여 높이 전부 차지(flexGrow/flexBasis 동작 검증 — worldBound 실측 로그), 나침반은 canvas 우상단 내부 배치로 이동 고려(현재 창 루트 절대좌표 — 스케일 정합 취약).
5. 계약 보존: GitHub-dark 팔레트/마커 클릭(OnTerritoryClicked)/M키 폴링/114차 배선 무수정. WorldMapSceneWiringTests 보호.

### B 테스트
- `WorldMapLayoutTests` 신규: backgroundSize 계약(Contain 폐기 어서션), 캔버스 정사각 강제 로직 소스 어서션, 마커 % 산식 유지 어서션.

## R2. F키 ShopNPC 분류 수리 (Systems 증분)

1. `HoverTargetClassifier.IsShopTerritoryNpc` → `public static` 노출(내부 로직 무수정 — 리플렉션 isShopNPC 판정).
2. `PlayerMovement` F키 TerritoryNPCBehaviour 분기(L637~659): NPC 발행 전 `HoverTargetClassifier.IsShopTerritoryNpc(nearestNpc.gameObject)` 판정 → `TargetKind.ShopNPC` 발행 분기 추가. Systems 내 참조라 asmdef 문제 없음(동일 어셈블리 실측).
3. 상호작용 불가 피드백 계약 유지 — 분기 변화만, 기존 NPC/주민 경로 무손상.
4. 테스트: `TaxProposalWiringTests` 확장 — PlayerMovement 소스에 `IsShopTerritoryNpc`+`TargetKind.ShopNPC` 배선 어서션 2건.

## R3. 음식점 가시성·재건설 정합 (Systems)

1. **진단 로그**: RestaurantPropBuilder.BuildProp 완료 시 `실물 world pos/y + GetHeightAt(y) + 마을 중심 거리 + 마을 반경 내 여부` 1회성 로그 — 다음 Play에서 매몰/원거리 판정 데이터.
2. **접지 정합**: BuildProp의 y 산식과 Test_15 지형 패치(SurfaceY=GetHeightAt+1) 대조 — 불일치 시 접지 수리.
3. **가드 개선**: "이미 음식점이 있는 마을" 거부 시 토스트 문구에 실물 위치 안내(`음식점이 이미 마을에 있습니다 — 남쪽 ○○m`) + **플래그 true·실물 부재 상태 감지 시** 재건설 허용 플로우(실물 존재 실측 — RestaurantPropBuilder._props 캐시 조회).
4. Play 재확인: 마을 중심 12m 지점 음식점 실물 육안 확인.

## R4. NPC 1.8m 캘리브레이션 + 애니 (Systems 증분)

1. `SpawnVillageNpc` 확장 — **GLB 교체 후** 순서로:
   - 1.8m 캘리브레이션: `TerritoryBuilder.MeasureStandingHeight(npc,false)` → 균등 스케일 ×(1.8/실측) → bounds.min.y 재접지(VillageNpcSpawner L206~222 검증 블록 이식 — 캡슐 폴백 경로도 동일 적용, 2m 캡슐→1.8 스케일).
   - 애니: `GetComponentInChildren<Animator>` + 아바타 무효 시 BuildHumanAvatar([91차 F6] 선례) + `Player_AC` 로드 → 믹사모 Idle 오버라이드(ResolveMixamoOverride 선례) + `cullingMode=AlwaysAnimate`([92차 후속 F-B] 계약). ShopNPC는 배회 없음 → Idle 고정(대화 Talk 트리거는 NPCAmbientDialogue 미부착 — 후속).
2. 적용 범위: Test_13/14/15 `SpawnVillageNpc` NPC 전부(공용 헬퍼라 자동). 마을 주민 경로(VillageNpcSpawner) 무수정.
3. 테스트: 소스 어서션(SpawnVillageNpc 내 MeasureStandingHeight+Player_AC 배선) 2건.

---

## 실행 분할 (승인 후)

| Phase | 범위 | 담당 |
|---|---|---|
| P1 | R2(F키 분류)+R4(NPC 캘리브레이션/애니) — Systems 증분+테스트 | 구현 서브 (사전소화 지시서) |
| P2 | R1 월드맵 레이아웃 수리+테스트 | 부모 직접 (대형 UI 재구성) |
| P3 | R3 진단 로그+가드 개선 | 구현 서브 |
| QA | 컴파일(batchmode)+EditMode 전수 — baseline 11건 1:1 판정 | 부모 |

- 순서: P1 → P2‖P3 → QA → Play.
- ⚠서브 지시서 공통: "Unity 실행 절대 금지 / write_file 경로에 따옴표 금지(115차 오착지 선례) / meta는 생성하지 않고 Unity 재생성 위임".

## Play 게이트 (승인 후)
1. **월드맵**: M키 → 지도가 양피지로 가득(레터박스 0), 마커 전부 지도 위 배치, 상하 분리 소멸, 나침반 정위치. [스프레드 옵션 선택 시] 마커가 지도 전체에 분산.
2. **F키**: Test_15 마을 상인 근접 F키 → "상태보기/대화하기/상점/**세금제안**" 4버튼 패널 → 세금제안 클릭 → 권장세금 창.
3. **음식점**: 마을 중심→성 방향 12m 지점 음식점 실물 육안 확인 + U키 재건설 시도 시 안내 토스트 확인.
4. **NPC**: 마을 상인이 플레이어와 동일 키(눈높이) + idle 애니 재생(포즈 동결/T-pose 없음).

## 리스크
1. 월드맵 캔버스 정사각 고정이 FigmaCanvasLayout 등배수 계약(창박스=figma×k)과 충돌 가능 — 창이 전체화면 캔버스 Rect(0,0,1920,1080) 적용이라 구현 시 worldBound 실측 선행.
2. `aspect-ratio` 미지원 버전 대비 GeometryChangedEvent 폴백 필요(UTK API 실측 — IStyle에 aspectRatio 없으면 폴백).
3. SpawnVillageNpc 캘리브레이션이 Test_13/14 기존 Play 검증 경로에 회귀 영향 — 캡슐 폴백 가드 동봉.
4. R3 플래그·실물 동기화 개선은 세이브 복원 경로(RebuildFromSave)와 상호 간섭 주의 — 멱등 가드 유지.

## 미실측 (구현 시 확인)
- `FigmaCanvasLayout.Apply`가 이 창에 실제 적용하는 rect/k 값(상단 빈 공간의 정확한 원인).
- BuildProp y 산식과 패치 SurfaceY의 실차.
- UTK `IStyle.aspectRatio` 지원 여부(6000.4.10f1).
