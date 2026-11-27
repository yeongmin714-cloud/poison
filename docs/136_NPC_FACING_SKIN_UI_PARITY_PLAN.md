# 136차 계획 — NPC 대화 정면 재판정 프로브 + NPC 피부 재검증 + 인벤/설명/전리품 Figma 100% 정합

작성: 2026-10-30 (136차). 사용자 요청 3건: ①NPC 등 돌림 원인 파악 ②NPC 피부 깨짐 재확인 ③인벤/아이템설명/전리품 창 Figma 백퍼센트 정합(플루언트, 여백·배열) 수리.
상태: 계획+위임 지시서. NPC 항목은 **진단 전용(확정 전 수리 금지)**, Figma 항목은 구현 승인됨. Play·커밋은 별도 승인.

---

## §1 NPC 대화 정면 — 현재까지의 확정 사실 (실측 근거)

1. **타임라인**: 135차 FBX meta 클립 베이크 요각 패치 = 2026-10-30 02:44~02:49 착지(24캐릭터 idle/talk .meta). 사용자 최신 영상은 00:01~02:16 녹화(패치 이전). **오늘 아침 Play 세션 = Editor.log 세션 2026-10-30 08:17~08:28 KST(패치 이후, 재임포트 동반 세션)** — Test_15_Livelihood 로드, 1회 Play.
2. **오늘 아침 Play 로그(Editor.log) 실측** — `[134차 C2] 대화 forward 정합` dot: Villager_6 −0.70 / Villager_4 −0.87 / Villager_9 −0.91 / Villager_7 −0.43.
3. **⚠️ 로그 판정 부호 반전 발견(본 차수 신규 판정)**: DialogueCameraDirector.cs L400-416 — dot = Dot(targetT.forward, bearing), bearing = **player→NPC 방향**. NPC가 플레이어를 바라보면 forward ≈ −bearing → dot ≈ **−1이 정면**. 즉 로그 주석 "1=정면/−1=뒷모습"은 **부호가 반대**이고, 134차 §9의 "forward는 카메라 쪽을 향하는데" 해석도 반대로 읽은 것(양수 dot = 등 돌림이었다). **오늘 아침 음수 dot = 루트가 플레이어 쪽을 향하고 있었다**(Begin 시점, 시선 회전 적용 이전 측정).
4. 요약 — 루트 레벨 정면은 이미 성립, 135차 클립 베이크 패치로 hips도 루트에 정렬됨(배치 프로브 Idle ≤+4.3°/TalkA ±0.5°). 그럼에도 사용자 신고 "여전히 등" → **렌더(화면)와 트랜스폼 수치 사이의 미확인 간극**이 남아 있다. 후보: ①프로브가 보지 못한 시각-메시 정면 불일치 ②서보/yaw 재적용 경로의 세션 내 상실 ③사용자가 본 상황이 대화 세션이 아닌 배회/접근 상황 ④클립 패치가 실제 렌더에 반영 안 된 잔여 경로. **변형 수학 추정으로는 확정 불가 — 렌더 스크린샷 기반 재판정 필요**.
5. 결론: 본 차수는 **수리 없이** 프로브(스크린샷 포함)로 확정 → 결과 보고 후 수리 방향 사용자 승인.

## §2 NPC 피부 — 현재까지의 확정 사실

- 134차 A4: 머티리얼 바인딩 완전 정상(slots=1, URP/Lit, baseMap=mainTex=base_color, uvBounds 정상, badWeights=0, 오늘 아침 로그에도 `[134차 A] 텍스처 자가 부착 — Villager_10 tex='base_color' 적용=True` 재확인). 135차 P3-B로 대화 카메라 근평면 가드 복원(1.82m).
- 사용자 재신고 "피부 깨짐" → **에셋/바인딩 레벨은 계속 정상**이므로 렌더 단계 근거가 필요. 본 차수: 근접 스크린샷(배치 프로브 렌더) 재촬영 + 텍스처 UV 샘플 대조 → vision 판독으로 "깨짐"의 실체(근평면 클리핑 잔여 / UV 얼룩 / 스키닝 변형 / 단순 저해상도) 판별.

## §3 NPC 진단 프로브 스펙 (QA 위임 — 수리 금지)

- 신규 에디터 도구: `Assets/Editor/Tools/NpcFacing136Probe.cs` (기존 NpcFacingProbe.cs의 spawn/Capture 유틸 재사용 — Run L24 / Capture L122 선례).
- 시퀀스(캐릭터 3종 이상: girl1/boy1/man2 계열):
  1. NPC 스폰 + Player 서로게이트 배치(기존 프로브 선례 준용) → **비대화 상태** 스냅샷: root world yaw, hips world yaw, SMR bounds 중심 방향, [135차] 패치 클립 재생 여부 로그.
  2. `DialogueCameraDirector.Begin(Component, string)` 실호출(주민 경로) → 즉시 스냅샷: root yaw vs 플레이어 방향, hips yaw, `_gazeSavedYaw`, DialogueYawControlled, 카메라 pos/rot/nearClip/dist(2.92), 근평면 침입 산술(bounds vs near).
  3. **대화 카메라 화면 스크린샷 캡처** (기존 Capture(cam, rt, pos, lookAt, name) 선례) — NPC 3인 각 1장(정면 프레이밍) + 근접 피부 컷 1장(0.8m, 가드 적용 상태) + 후방 참조컷 1장.
  4. End() 후 원복 스냅샷 1세트.
- 산출: `TestOutput/npc136_facing.txt` + `TestOutput/npc136_*.png` (vision 판독은 감독자).
- 제약: 배치모드 전용, flock `/mnt/c/Unity/code/.unity136.lock`, `-logFile TestOutput/npc136_unity.log`, Unity.exe 점유 확인(tasklist.exe) + 잔재 Lockfile 처리, **프로덕션 코드 수정 금지**(에디터 도구 1파일만).

## §4 Figma 진실치 (2026-10-30 API 재실측 — docs/figma_truth_136/ raw JSON+렌더 PNG 동봉)

> 스케일 k=0.75(1920 디자인공간 → 게임 창박스=figma×k 계약 유지). 렌더 PNG: inventory_15-5.png / itemdesc_15-141.png / loot_70-4.png.

### ④-1 인벤토리 15:5 (504×912) — 패널-로컬
| 요소 | 값 |
|---|---|
| 창박스 | 504×912 r14.4 fill #161B22@0.702 stroke #30363D@0.502 w1.2 |
| Header | (24,24) 456×52.8; Title '개인 소지품' 24/700 #F0F6FC @(0,5.2 in header); Subtitle 'INVENTORY' 14.4/400 #8B949E @(129,14.2); CloseBtn (424.8,3.6) 31.2×31.2 r7.2 #FFF@0.059 stroke, x-circle 16.8 #8B949E |
| Tabs | (24,84) 5개 87.36×36.2 r7.2 **stride 92.16(gap 4.8)**, 활성 #58A6FF/텍스트 #0B0E14 14.4/700, 비활성 stroke #30363D@0.502/텍스트 #8B949E — 전체/무기/방어구/소모품/재료 |
| EquippedSection | **(24,144)** 456×230.4 r9.6 #FFF@0.02 stroke; 라벨 '장착 중인 장비' (14.4,14.4) 14.4/700 #58A6FF |
| EquipGrid | **섹션-로컬 (14.4,43.4)** 427.2×172.8 — 셀 81.6, x-stride 86.4(gap 4.8), y-stride 91.2(gap 9.6), r9.6 #21262D@0.6 stroke #30363D@0.502; TierStrip top h3.6; EquipBadge 'EQ' (4.8,4.8) 22.6×15.8 r3.6 #58A6FF 텍스트 9.6/700 #0B0E14; 아이콘 38.4 중앙(21.6,21.6) |
| WeaponNumbers | (14.4,210) — 4·5번 셀에 HotkeyBadge 18×18 r3.6 #F0F3F5 stroke #0D1015, Geist Mono/900 10.8 #1A1D20 |
| Divider | (24,381.6) 456×1.2 #30363D@0.502 |
| BagGrid | **(24,390)** 456×446.4 — 5행 y 0/91.2/182.4/273.6/364.8, 셀 81.6 x-stride 91.2(gap 9.6), r9.6 #21262D@0.6 stroke(선택셀 #30363D@0.8 stroke #58A6FF), Count 우하단 Geist Mono/500 13.2 #8B949E (셀-로컬 (66,60)) |
| Footer | (24,843.6) 456×48.2 stroke — WeightRow (0,7.2): GoldBadge 46.2×26.6 r4.8 #FFF@0.039 '골드' 14.4/400 @(9.6,4.8) + 값 Geist Mono/700 14.4 @(53.4,3.8); '인벤토리 적재량' 15.6/400 #8B949E @(141.7,4.3) + 아이콘 16.8; 값 '34.8 / 120.0 kg' Geist Mono/700 15.6 #58A6FF 우측(315,3.3); ProgressBar (0,41) 456×7.2 r3.6 #FFF@0.078 fill #58A6FF |

**현재 소스 델타(확정)** — InventoryClusterPanelRegions.cs:
- L48 InventoryEquipmentBody y 127.4 → **144** (+16.6) / L49 InventoryEquipGridBody (38.4,170.8) → **(38.4,187.4)** / L50 InventoryBagViewport y 373.4 → **390** (+16.6) / 외부좌표 세트 L14 EquippedSection y 211.4→228, L16-21 BagGrid·Row1~5 457.4→474 (+16.6 등간격). Footer(911/843.6) 무수정.
- 렌더 근거(08:17 세션 `[134차 F] 인벤 기하(250ms)` 2차 개방): equipPanel 윈도우-로컬 y=156.19(k 역산 208.25) = 진실 144×0.75=108 대비 하부 블록 전체 이탈; equipGrid 폭 341.42 = 456×0.75(진실 427.2×0.75=320.4 대비 +21) — **그리드 폭도 456 전폭으로 잘못 적용 중**.
- 계약 갱신 대상(소멸 예정 수치): FigmaCanvasLayoutTests.cs L191(38.4,170.8) L192-193(24,373.4) L388(24,127.4) L390(24,373.4) → 신값(187.4/390/144/390).

### ④-2 아이템 설명 15:141 (576×729.6) — 패널-로컬
- 섹션 RECT는 현재 Regions(DetailItemName/Image/Description)와 **이미 일치**((24,96,528,67.2)/(24,182.4,528,384)/(24,585.6,528,120)) — 셸 무수정.
- 내부 타이포/패딩 진실치(63:101 마스터 템플릿 실측): Header Title 24/700 #F0F6FC, Subtitle 14.4/500 #8B949E @(107,9.2); ItemNameSection 내부 (14.4,14.4) — 이름 21.6/700 #F0F6FC lh25.31, 영문 14.4/400 #8B949E, TierBadge 우측 (451,25.5) 48.2×24.6 r4.8 bg 티어색@0.122 stroke 티어색, 텍스트 13.2/700; ItemImageSection — 메시그리드 라인 #FFF@0.02(간격 54.86), 중앙 Ellipse 216 #58A6FF@0.102 + 96 @0.122, RenderGraphic 240×240 중앙; DescriptionSection — 내부 pad **19.2**, Heading 15.6/700 #58A6FF @(19.2,19.2), StatRow 라벨 Inter 15.6/400 #8B949E / 값 Geist Mono/700 15.6 우측정렬(#FF7B72/#56D364/#BC8CFF), 행 pitch 29.6 h20, Divider 489.6×1.2 #30363D@0.502, BioText 15.6/400 #8B949E lh23.4, 블록 gap 14.4.
- ⚠️ **현재 렌더 델타(08:17 세션 `[134차 F] 설명창 기하(250ms)`)**: nameSection 렌더 폭 297 = 528×**0.5625**(=0.75²) — winStyle 432×547.2(셸 정상 단일 k)인데 **내부 섹션이 이중 스케일** 의심(descSection 297×75.77 동일 비율). 구현의 스케일 경로(윈도우 스케일 × 콘텐츠 스케일 중복)를 확인하고 단일화할 것 — 이것이 "여백/배열 살짝 다름"의 유력 기구적 원인.

### ④-3 전리품 70:4 (504×432) — 패널-로컬
| 요소 | 값 |
|---|---|
| 창박스 | 504×432 r14.4 #161B22@0.702 stroke — FigmaBounds(708,324) 현재와 일치 |
| Header | (24,24) 456×**48**; Title '전리품' 24/700 #F0F6FC; Subtitle 'LOOT SECURED' 13.2/700 #8B949E @(79,10.2); CloseBtn (424.8,3.6) 31.2×31.2 r7.2 |
| LootStats | (24,86.4) 456×26.6 — BadgeItem (0,0) 76.2×26.6 r4.8 #FFF@0.039, '습득 가능' 14.4/700 #58A6FF @(9.6,4.8); SlotCapacity Geist Mono/400 15.6 #8B949E @(300,3.3) 156×20 우측 |
| Grid | (24,127.4) 456×172.8 — 2행(y 0/91.2) × 5열, 셀 81.6 stride 91.2, r9.6 #21262D@0.6 stroke #30363D@0.502, TierStrip h3.6, 아이콘 38.4 @(21.6,21.6), Count Geist Mono/500 13.2 #8B949E 셀-로컬 (66,60) |
| Footer | (24,314.6) 456×61.2 stroke — ActionButtonGroup (0,14.4) 456×46.8: 전부 습득하기 x0 222×46.8 r7.2 #58A6FF 텍스트 15.6/700 #0B0E14 / 닫기 x231.6 224.4×46.8 r7.2 #FFF@0.059 stroke 텍스트 15.6/700 #F0F6FC |

- ShopLootFigmaPhase3Tests: FigmaBounds/GridViewportHeight/SlotSize/SlotGap/CompositionBounds(1272,84) 계약 현값 유지(진실치와 동일) — 창박스·그리드 수치는 무수정, **헤더 높이(현재값 확인 필요 — 진실 48)/stats y 86.4/footer y 314.6·높이 61.2/버튼 폭·간격** 등 내부 배열만 진실치 대조 수리.

## §5 Phase 구성

- **P1 [QA 위임]** §3 NPC 진단 프로브 실행(수리 없음) → 산출물로 감독자 vision 판정 → 사용자 보고 + 수리 방향 제안(승인 대기).
- **P2 [구현 위임 2분할 — 파일 소유권 절대 분리]**
  - B1(인벤): InventoryClusterPanelRegions.cs 수치 갱신 + InventoryWindowUTK.cs 그리드 폭/섹션 배치 진실화 + FigmaCanvasLayoutTests/InventoryFigmaTabsFooterTests 계약 갱신 → 컴파일(flock) → 증거 제출.
  - B2(설명+전리품): ItemDescriptionWindowUTK.cs 스케일 단일화+내부 패딩 진실화 + LootWindowUTK.cs 내부 배열 진실화 + ShopLootFigmaPhase3Tests(필요 시) → 컴파일(flock 동일 락) → 증거 제출.
  - 공통: 기존 계약(창박스=figma×k, 글래스/백드롭 계약, 5열, 드롭 양보, CommandMarker) 무변경. baseline 11 실패군 유지.
- **P3 [QA 위임]** 컴파일 0 + EditMode 전수 baseline 1:1 대조(Temp/unity-editmode-results.xml 판독, TestOutput 스테일 금지).
- **P4** 사용자 재Play 판정: ①NPC 대화 정면 ②피부 ③3창 여백/배열. 커밋은 별도 승인(128~135 누적 포함).

## §6 감독자 실측 데이터 (위임 인계용)
- Figma raw: docs/figma_truth_136/figma_{inv,loot,desc}{,_deep}.json / 렌더 PNG 3종 동일 폴더.
- 오늘 아침 Play 로그: /mnt/c/Users/korea/AppData/Local/Unity/Editor/Editor.log (세션 08:17~08:28 KST, 종료됨 — Unity.exe 현재 미점유).
