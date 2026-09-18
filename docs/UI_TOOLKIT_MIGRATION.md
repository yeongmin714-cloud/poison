# 🎨 UI Toolkit 전면 마이그레이션 계획서 (UI-T)

> **결정 (2026-09-17, 사용자 확정):** 앞으로 모든 UI는 **UI Toolkit(UXML/USS)** 방식으로 작업.
> IMGUI(OnGUI) 신규 작성 금지. 기존 IMGUI 창은 본 계획의 Phase 순서로 단계적 전환.
>
> **우선순위:** 전체 UI 개편 = 현재 로드맵 1순위 (화살/RTS 버그 수정 계획서 A~F는 그 다음).
> **대상 스코프:** OnGUI 사용 파일 112개 전수 조사 기반 — 전환/부분/유지 3분류.
> **플랫폼:** Unity 6000.4.10f1 / URP 17.4.0 / com.unity.ui (Unity 6 내장, 런타임 지원 성숙)

---

## 0. 불변 규칙 (모든 세션 준수)

1. **신규 UI = 100% UI Toolkit** — UXML(구조) + USS(스타일) + C# 컨트롤러. OnGUI 신규 금지.
2. **스타일은 Theme.uss 변수로만** — 하드코딩 색상 금지. UI_DESIGN_GUIDELINES 62차 팔레트(양피지 0.96,0.94,0.88 / 딥차콜 0.11 / 브론즈 0.55,0.42,0.25 / 골드)를 USS 변수로 이식.
3. **공존 시 렌더 순서** — OnGUI는 항상 최상위에 그려짐 → **그룹 단위 완전 전환**으로 충돌 회피 (한 창이 절반 UTK+절반 IMGUI면 안 됨).
4. **스케일** — PanelSettings.referenceResolution 1920×1080 + match로 통일 (기존 `_uiScale = sqrt((W/1920)*(H/1080))` 수동 공식 대체).
5. **폰트** — 기존 UIFont 체계(60/38/24/17/13)를 UTK 폰트 정의로 승격.
6. **월드스페이스 예외** — DamageNumber, 이름표(HeadUI/Nameplate), ScreenFlashFX, 디버그/Test 셋업 IMGUI는 전환 제외 (3D 앵커 전용).
7. **Phase 완료 시** — 이 문서 체크표시 + QAPROGRESS.md 스냅샷 + git commit 3종 세트.

---

## 1. 현황 조사 (2026-09-17 실측)

- OnGUI 사용 파일: **112개** (UI/ 60+, Systems/ 40+, Functions/ 일부)
- 핵심 게임창: Inventory(통합그리드+DnD), Loot(우측 2S/3+6), Shop(화술 할인/판매), Equipment(우측 임베드), Warehouse, WorldMap(정규화 u=0.5+x/3200), Map, GuardInfo(2분할), TerritoryDeployment(역할 5버튼), Crafting, Hotbar(아이콘), GuardSquadHotbar(부대 F+1~8)
- DnD 코어: `ItemDragContext`(Source.Loot/Inventory 등), 드래그→MouseUp TakeItem 체인
- 팔레트/톤 관리: UIStyleManager + UIFont + UI_DESIGN_GUIDELINES.md

---

## 2. Phase 계획

### Phase U0 — 인프라 구축 (모든 Phase의 전제)
- [x] **PanelSettings 에셋 생성** — 에디터 배치 스크립트(UIToolkitSetup.Recreate -executeMethod)로 생성 완료 (ScaleWithScreenSize 1920×1080 match 0.5)
- [x] **UIDocument 부트스트랩** — UIToolkitBootstrap(BeforeSceneLoad 자가 Ensure, UTKRoot DontDestroyOnLoad) + UTKWindowManager(ESC 스택)
- [x] **Theme.uss** — 팔레트 USS 변수 18종 + 폰트 5단 + 공통 클래스
- [x] **공통 컨트롤 라이브러리** — UTKWindowBase(타이틀바 드래그·ESC), UTKButton 3변형, UTKSlot(등급 테두리·호버), UTKTooltip, UTKModal, UTKToastService
- [x] **희귀도 슬롯 오라** — 등급별 테두리 클래스(.utk-rank--common~unique)
- [x] **IMGUI 공존 가이드** — 그룹 단위 완전 전환 규칙 문서화(본 계획서 §0 규칙 3)

### Phase U1 — 파일럿 창 2개 (패턴 확립)
- [x] **StatusWindowUI** → **StatusWindowUTK.cs**(581줄) — 스탯 4분배/전투 스탯 8행/장비 6슬롯+보너스/게이지/중독/레벨업 토스트 (3D 뷰포트는 Placeholder 축약 — 후속)
- [x] **ShopWindow** → **ShopWindowUTK.cs**(564줄) — 구매/판매 2탭+씨앗 랜덤 재고+가격 병기(할인가/원가)+골드 잔액. 가격 로직은 PlayerStats 소스 직접 호출
- [x] 기존 IMGUI 무변경(additive 원칙 확립 — LEGACY_UI 게이트 불필요, 전환 스위치는 U7에서 일괄)
- [x] 검증: 배치컴파일 error CS=0 + 서브에이전트 자체 검증(괄호 균형/OnGUI 부재/클래스 중복 0). Play 렌더 판정은 다음 세션

### Phase U2 — 인벤/전리품 코어 루프 (최대 리스크 — DnD)
- [x] **UTKDragDrop**(348줄) — UTKDragPayload/IUTKDragSource/IUTKDropTarget/고스트 아이콘/MakeDraggable(임계 6px)
- [x] **InventoryWindowUTK**(451줄) — 통합 그리드 7열(GetAllSlots)+Loot 수령/스왑/땅 바구니+250ms 재조회 즉시 갱신
- [x] **LootWindowUTK**(320줄) — 행 드래그+우클릭 즉시 획득+빈바구니 자동 Hide+우측 2S/3+6 관례
- [x] **EquipmentWindowUTK**(363줄) — 8슬롯+해제(UnequipSlot 실호출)→인벤 복귀+이벤트 구독
- [x] **HotbarUIUTK**(286줄) — 8슬롯+PlayerPrefs 원본 데이터 소스 연동+우클릭 해제+GLB 1초 재시도
- [ ] QuickSlotUI 포팅 — U3 경제 라운드로 이월
- [x] 회귀: 원본 5파일 0변경(additive) — 배치컴파일 error CS=0. Play 렌더 판정은 다음 세션

### Phase U3 — 경제/제작 루프
- [x] **WarehouseWindowUTK**(629줄) — 좌:인벤/우:창고 양방향 DnD 입고/출고+우클릭 즉시 이동+territory 메뉴
- [x] **CraftingWindowUTK**(837줄) — 레시피 목록/상세/재료/제작+프리셋 저장·로드·즐겨찾기(CraftPresetManager 직접 호출)+결과 토스트
- [x] **AlchemyStationUTK**(358) + **CookingWindowUTK**(393) + **RepairStationUTK**(297) — 스테이션 3종
- [x] **QuickSlotUTK**(236) — 퀵슬롯(U2에서 이월)
- [ ] CompareTooltip — U7 툴팁 통합 라운드로 이월 (UTKTooltip 공통 인프라 존재)

### Phase U4 — 전략/영지 창 (13창 전부 ✅)
- [x] **WorldMapWindowUTK**(659) — 정규화 좌표 마커/절차 양피지/M키+구핫키 무력화/소유 실시간
- [x] **TerritoryDeploymentUTK**(298)+**TerritoryInfoPopupUTK**(284) — 6역할 배치+특사 pick/상세
- [x] **QuestWindowUTK**(419)+**QuestJournalUTK**(320)+**EncyclopediaWindowUTK**(384) — Q/J/L키
- [x] **SpyMissionUTK**(636)+**EnvoyMissionUTK**(523) — 첩보/특사 임무
- [x] **MercenaryHireUTK**(296)+**RevengeListUTK**(385)+**FastTravelUTK**(296)+**RouteConfirmationUTK**(207)+**AutoMoveUTK**(258)
- [ ] 원본→UTK 호출 전환(Show 호출부 배선) — U7 일괄 전환 라운드로

### Phase U5 — 메뉴/시스템 화면 (13창 전부 ✅)
- [x] **GuardInfoUTK**(480) — 2분할(장비 6슬롯/스탯 분해/물약 버프/전투력)
- [x] **OptionsUTK**(391)+**SettingsMenuUTK**(463) — 역할 분담+PlayerPrefs 키 공유+접근성 9API
- [x] **MainMenuUTK**(379)+EscMenuUTK(156)+SaveSlotUTK(206)+LoadGameUTK(207)+LoadingScreenUTK(237)+DeathScreenUTK(timeScale 홀드)
- [x] **EndingCreditsUTK**(189 자동스크롤·스킵 — 4-phase 머신은 크레딧 롤만 이식, 통계 요약 스텁)
- [x] **GameStatsUTK**(242, U키)+AchievementUTK(228, A키)+TutorialGuideUTK(279, T키)
- [ ] 크레딧 phase 머신(통계 요약→뉴게임+ 선택) 완성 — U8 회귀 라운드 검토

### Phase U6 — 미니게임/대화/이벤트 (17창 전부 ✅)
- [x] **NPCDialogueUTK**(352)+QuestChoiceUTK(306)+ReadDocumentUTK(186)+LordAudienceUTK(242)
- [x] **LockpickingUTK**(454)+FishingUTK(238)+MercyUTK(286)+SleepUTK(수면 위임+침대세이브)
- [x] **ArenaMenuUTK**(431)+ArenaBattleUTK(373)+MissionResultUTK(자체 큐 — 피드 배선 U7)
- [x] **FestivalUTK**(276)+DynamicEventUTK(252)+NPCDailyUTK(193)
- [x] **PlayerFlagUTK**(356)+GasSprayUTK(280)+ChurchUTK(295 — 2원본 통합)
- [ ] 원본→UTK 호출부 배선 — U7 일괄 전환 라운드

### Phase U7 — HUD/오버레이 + 유지 판정 (HUD 7창 ✅ / 배선 U8 이월)
- [x] **HUDUTK**(415) — 체력/퀵슬롯 6/경험치 바 (⚠️ QuickSlotUTK와 하단 겹침 — U8에서 은퇴 대상 결정)
- [x] **GuardSquadHotbarUTK**(428) — 원본 _slots 리플렉션 공유(키 입력은 원본 담당)
- [x] **MinimapUTK**(440) — 월드 스플랫+영지/퀘스트 마커+날씨
- [x] **TimeDisplayUTK**(162)+WarNotificationUTK(173)+CombatLogUTK(224, L키)+HerbRespawnUTK(233)
- [x] **유지 판정 확정**: DamageNumber·이름표 4종·ScreenFlashFX·GuardWorldSpaceHUD·컷신 2종·디버그/Test IMGUI — 전환 제외
- [ ] **호출부 배선(원본 Show()→UTK 전환) — U8에서 창 단위 점진 적용**

### Phase U8 — 최종 회귀 + 표준화 (폐기 1단계 완료 — Play 검증 후 2단계)
- [x] **호출부 배선 1차** — 침대 수면(SleepUTK)+전리품 바구니(LootWindowUTK): 이벤트 브리지 패턴(UTKWireUp) — UTK 미준비 시 원본 자동 폴백, 한 줄 제거로 완전 회귀
- [x] **배선 확대** — 상점(ShopPlaceholder.ToggleShop)+인벤 I키(UIInventoryHotkey): 총 4경로 브리지
- [x] **UI 가이드 UTK 표준 확정** — UI_DESIGN_GUIDELINES.md UTK 섹션(팔레트 매핑/폰트/레이아웃/엔진 규약 8종)
- [ ] 배선 확대 — 상점/상태창/인벤 키/가드정보 등 창 단위 점진 전환
- [ ] HUDUTK↔QuickSlotUTK 하단 겹침 해소
- [ ] **Play 렌더 종합 검증** — 에디터에서 전 UTK 창 확인 후 진행
- [ ] 구 IMGUI 폐기(LEGACY 제거)+git 태그 — Play 통과 후

---

## 3. 리스크 & 대응

| 리스크 | 대응 |
|:--|:--|
| IMGUI 최상위 렌더 → 공존 시 겹침 | 그룹 단위 완전 전환 (규칙 3) |
| DnD 회귀 (인벤/전리품) | Phase U2 단독 분리 + 회귀 4경로 테스트 |
| PanelSettings 에셋 필요 | 에디터 배치 스크립트 1회 생성 (U0) |
| 폰트/스케일 불일치 | PanelSettings referenceResolution 통일 (규칙 4) |
| 컨트롤러/접근성 회귀 | U5에서 AccessibilityManager/HapticFeedback 재연동 검증 |
| 마이그레이션 중 기능 누락 | 창별 "이전 기능 체크리스트"를 각 Phase 검증 항목에 명시 |

---

## 4. 진행 로그

| 날짜 | Phase | 내용 | 상태 |
|:--|:--|:--|:--|
| 2026-09-17 | — | 계획서 작성 (112개 OnGUI 파일 전수 조사 기반) | ✅ |
| 2026-09-18 | U0 | 인프라 구축 완료 — Theme.uss/TSS/PanelSettings(배치 생성)/UIToolkitBootstrap/WindowManager/WindowBase/Controls 7파일, 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U1 | 파일럿 완료 — StatusWindowUTK(581줄)+ShopWindowUTK(564줄), 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U2 | DnD 코어 완료 — UTKDragDrop+Inventory/Loot/Equipment/Hotbar UTK 5파일(1,768줄), 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U3 | 경제/제작 완료 — 6파일 2,750줄(창고/크래프트/연금/요리/수리/퀵슬롯), 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U4 | 전략/영지 13창 완료 — 4,417줄(월드맵/영지배치/퀘스트/도감/임무/용병/복수/이동), 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U5 | 메뉴/시스템 13창 완료 — 3,407줄(가드정보/옵션/설정/게임흐름 6종/잔여 4종), 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U6 | 미니게임/대화/이벤트 17창 완료 — ~4,030줄, 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U7 | HUD 7창 완료(2,075줄)+유지 판정 확정, 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | U8 | 1차 배선(수면/전리품 브리지)+가이드 UTK 표준 확정, 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | Play 1차 | Editor.log 실측 — Status/Quest/Squad UTK 정상 렌더·토글, UTK 예외 0. ProceduralAnimationController 파괴 가드 수리 | ✅ |
| 2026-09-18 | Play 1차 | Editor.log 실측 — Status/Quest/Squad UTK 정상 렌더·토글, UTK 예외 0. ProceduralAnimationController 파괴 가드 수리 | ✅ |
| 2026-09-18 | 배선 확대 | 4경로(침대 수면/전리품 바구니/상점/인벤 I키) — 브리지 패턴 동일 적용, 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | Play 1차 | Editor.log 실측 — Status/Quest/Squad UTK 정상 렌더·토글, UTK 예외 0. ProceduralAnimationController 파괴 가드 수리 | ✅ |
| 2026-09-18 | 배선 확대 | 4경로(침대 수면/전리품 바구니/상점/인벤 I키) — 브리지 패턴 동일 적용, 배치컴파일 error CS=0 | ✅ |
| 2026-09-18 | Play 수리 2건 | ①인벤 I키 미표시(Toggle이 Ensure만 하고 Show 누락 → Open 위임) ②미니맵 우상단 이동(사용자 지정) | ✅ |
| 2026-09-18 | Play 수리 3건 | ①창고 UTK 배선(TerritoryWarehouse→Inventory+Warehouse UTK) ②인벤창 위 장비창 임베드(8슬롯+우클릭 해제) ③EquipmentManager 단일소스 | ✅ |
| 2026-09-18 | 3분할 레이아웃 | 예시 2 정합 — 좌(장비 2x5+가방 6x5)/중(설명창)/우(창고, 상호작용시). 1080×620 확장. ShowItemDescription/SetWarehouseMode 공개 | ✅ |
| 2026-09-18 | 독립 창 개편 | 요구 반영 — 인벤(460×640)+설명(320×640) 독립 창 쌍(I키), 창고/전리품은 컨텍스트별 우측 독립 창. 창고→인벤 DnD 수리(Warehouse 수용)+가방 우클릭 수리(소모품 UseItem/장착 TryEquipItemPublic 위임). 원본 키 은퇴 게이트 4종(P/L/U/J) | ✅ |
| 2026-09-18 | 폐기 1단계 | 은퇴 게이트 전면 확장 — Core.UITransitionState.UtkActive 플래그+원본 HUD류 8종 자가 은퇴(HUD 부분: 체력/EXP 은퇴·버프/가스/은신/사망 유지). git 태그 phase68-ui-toolkit | ✅ |
| 2026-09-18 | 드래그 뿌리 수리 | UTKDragDrop evt.position은 캡처 엘리먼트 로컬 좌표 → ToRootPos(로컬→루트) 변환 수리 — 고스트/드롭 판정 정합 | ✅ |
| 2026-09-18 | 폐기 2단계(1/2) | 참조 0 원본 13종 아카이브(Assets 밖 LegacyUI_Archive/) — WorldMap/TerritoryDeploy/Envoy/Spy/Mercenary/LordAudience/Festival/MissionResult/NPCDaily/PlayerFlag/ChurchSystem/CombatLog/HerbRespawn. WorldMapUTK 폴백 코드 정리 | ✅ |
| 2026-09-18 | 드래그 마지막 부착 수리 | OnDragPointerUp 순서 버그 — ReleasePointer 동기 발화 PointerCaptureOut이 Complete를 섭취 → 세션 선분리 수리 | ✅ |
| 2026-09-18 | 폐기 2단계(2/2) 결론 | 잔존 원본 전수 참조 분석 결과 전부 라이브 시스템 참조(ArenaSystem/FishingSystem/GameManager 등 35종) — 무리한 삭제 시 시스템 대규모 수정 필요 → **파일 유지·런타임 은퇴(게이트/브리지)로 전환 완결**. 참조 제거 아카이브는 개별 시스템 개편 시 수행 | ✅ |
| 2026-09-18 | 요구 대량 반영 | ①좌클릭 공격 차단 게이트(PointerOverUI 플래그+PlayerCombat/카메라) ②우클릭 드래그+우클릭 클릭 분리 ③행 배치(인벤 16/설명 544/창고 892)+크기 확대(슬롯 64px) ④설명창 아이콘 프리뷰 ⑤구 uGUI 핫바 은퇴 ⑥글래스모피즘 다크네이비 테마 | ✅ |
| 2026-09-18 | Play 수리 3종 | ①드래그 도중 취소 뿌리(폴링 셀 재생성→캡처 상실) → 드래그 중 재생성 금지 가드 ②창고창 전용화+카테고리 탭 5종(전체/무기/방어구/재료/소모품) ③핫바 Tab 전환(IsSquadMode 연동 표시/숨김) | ✅ |
| | 후속 | HUDUTK 미이식 기능 이식(스태미나/버프 아이콘/가스 타이머/은신 HUD) | ⏳ |
| | 후속 | 월드맵 UTK 렌더 불능 원인 규명·재구축 | ⏳ |
