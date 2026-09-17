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

### Phase U5 — 메뉴/시스템 화면
- [ ] MainMenuUI + EscMenuUI
- [ ] OptionsUI + SettingsMenuUI (접근성 탭 포함 — AccessibilityManager 연동)
- [ ] SaveSlotUI + LoadGameUI + LoadingScreenUI + DeathScreenUI + EndingCreditsUI
- [ ] GuardInfoWindow (2분할) + GameStatsWindow + AchievementSystem
- [ ] TutorialGuideSystem

### Phase U6 — 미니게임/대화/이벤트
- [ ] NPCDialogueWindow + QuestChoiceUI + ReadDocumentWindow + LordAudienceUI
- [ ] LockpickingUI + FishingUI + MercyUI + SleepUI
- [ ] ArenaMenuUI/ArenaBattleUI + FestivalUI + DynamicEventUI + MissionResultUI + NPCDailyUI
- [ ] PlayerFlagRegistrationWindow + GasSprayUI + ChurchSystemUI/ChurchNPCInteraction

### Phase U7 — HUD/오버레이 + 잔여 정리
- [ ] HUD + GuardSquadHotbar(부대 F+1~8/토글/우클릭 해제) + MinimapUI
- [ ] TimeDisplayUI + WarNotificationUI + CombatLogUI + HerbRespawnUI + AutoMoveUI
- [ ] **유지(전환 제외) 확정**: DamageFont/DamageNumber, Nameplate/HeadUI/레벨라벨(월드스페이스), ScreenFlashFX, GuardWorldSpaceHUD, 디버그/Test 셋업 IMGUI
- [ ] 중복 경로 정리: UIStyleManager → UTK Theme 흡수, 구 IMGUI 코드 제거

### Phase U8 — 최종 회귀 + 표준화
- [ ] 전 창 회귀 QA (서브 QA 에이전트 — 키/스케일/DnD/접근성/컨트롤러)
- [ ] UI_DESIGN_GUIDELINES.md 갱신 — UTK 표준으로 재작성
- [ ] `LEGACY_UI` 게이트 제거 + 폐기 코드 정리 + git 태그

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
| | U5~U8 | 대기 — U5 메뉴/시스템(메인/옵션/세이브/사망)부터 | ⏳ |
