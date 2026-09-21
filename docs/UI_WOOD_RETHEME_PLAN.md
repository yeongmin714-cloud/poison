# 🎨 UI 우드/양피지 전면 개편 계획 (P33~P38 — 저장본)

> **생성:** 2026-09-22 · **기반:** P32 Figma→Unity 파이프라인 테스트 성공(커밋 e6c92a9c)
> **상태:** 계획 확정 — "진행" 시 P33부터 순차 실행
> **디자인 시스템:** 우드/양피지(Figma wood-rpg-inventory 스펙) — 패널 #F4E7D8 r16 / 라인 #D8C1A8 / 잉크 #6B4A36 / 액센트 앰버 #E4A163 / 빈슬롯 패턴 #5A3B2B
> **파이프라인:** Figma 노드(표준명, 배경 없음) → API 렌더 @2x → Resources/UI 배선(isReadable:1) → 컴파일/테스트/커밋/푸시/텔레그램

---

## 접근 전략
63개 창을 개별 리스타일하지 않는다. **UTKWindowBase + Theme.uss 전역 팔레트 교체 1회**로 다수 창이 자동 테마 변경(베벨/글로우/배경/텍스트 전부 Theme 공유). 이후 에셋 필요 창만 순차 작업. 인벤(P32 인라인)은 Theme 승격 시 정렬.

---

## P33 — 공용 테마 개편 (1회, 전 창 자동 적용)
| 대상 | 작업 |
|:--|:--|
| Theme.uss | 창 배경(딥우드 그라데이션)→양피지 #F4E7D8, 베벨→1px 라인 #D8C1A8, 글로우/텍스트/슬롯/버튼 팔레트 전면 교체 |
| UTKWindowBase | 그림자 유지 + 라운드 r16 기본화 |
| InventoryWindowUTK | P32 인라인 리스타일을 Theme 기반 정리(중복 제거) |

## P34 — 게임플레이 HUD (상시 표시 — 최우선)
| UI | 작업 | Figma 에셋 |
|:--|:--|:--|
| StatusGaugesUTK | Figma 게이지 재설계(HP 막대형+도넛 — 이전 합의) | GaugeHPFrame(슬롯 rect 유의: x 38.4~98.9%, y 6.9~93.1%), GaugeHPFill, GaugeStaminaIcon |
| UTKCursorOverlay | 커서 5종 표준명 교체 | cursor_arrow / cursor_sword / cursor_pickaxe / cursor_shovel / cursor_hoe (128×128) |
| HotbarUIUTK / QuickSlotUTK | 퀵슬롯 슬롯 스타일 | hotbar_slot / hotbar_slot_active |
| GuardSquadHotbarUTK | 부대 핫바 (슬롯 재사용) | — |
| BowAimReticleUTK | 리티클 톤 정합(현재 베이크 유지 가능) | aim_reticle(선택) |
| MinimapUTK / TimeClockGlassUTK | 원형 프레임 톤 | minimap_frame / clock_frame(선택) |
| NameplateOverlayUTK / CombatLogUTK | 팔레트 (Theme 자동) | — |

## P35 — 주요 게임 창
WarehouseWindowUTK(창고) · ItemDescriptionWindowUTK(설명) · LootWindowUTK(전리품) · ShopWindowUTK(상점+밀매탭) · EquipmentWindowUTK(장비) · StatusWindowUTK(P 스탯) · QuestWindowUTK+QuestJournalUTK(Q) · SoldierInteractUTK(F 병사) · MonsterInfoUTK · GuardInfoUTK · LordFeedWindowUTK(영주 음식) · LordAudienceUTK(접견) · TerritoryInfoPopupUTK · TerritoryDeploymentUTK(영지 배치) · WorldMapWindowUTK(M — 이미 절차 양피지, 정합 확인만)

## P36 — 크래프트/제작
CraftBenchBaseUTK(공용 베이스 1회 = 무기/연금/요리 자동) + WeaponForgeUTK · AlchemyBenchUTK · AlchemyStationUTK · CookingBenchUTK · CookingWindowUTK · CraftingWindowUTK · RepairStationUTK · ConstructionWindowUTK

## P37 — 메뉴/시스템
MainMenuUTK · EscMenuUTK · LoadGameUTK · SaveSlotUTK · SettingsMenuUTK+OptionsUTK · DeathScreenUTK · LoadingScreenUTK · EndingCreditsUTK · EncyclopediaWindowUTK(도감) · GameStatsUTK · AchievementUTK

## P38 — 이벤트/미니게임/특수 (통합 정리)
FishingUTK(이미 고품질 — 톤 정합만) · LockpickingUTK · ArenaMenuUTK/ArenaBattleUTK · EnvoyMissionUTK · SpyMissionUTK · FestivalUTK · DynamicEventUTK · NPCDialogueUTK/NPCDailyUTK · QuestChoiceUTK · ReadDocumentUTK · WarNotificationUTK · RevengeListUTK · RouteConfirmationUTK · FastTravelUTK · MercyUTK · GasSprayUTK · SleepUTK · ChurchUTK · PlayerFlagUTK · MissionResultUTK · TutorialGuideUTK

---

## Figma 에셋 총괄 체크리스트 (표준명 프레임, 배경 없음)
1. 커서 5종: cursor_arrow / cursor_sword / cursor_pickaxe / cursor_shovel / cursor_hoe — 128×128, 스타일 통일
2. 게이지 세트: GaugeHPFrame / GaugeHPFill / GaugeStaminaIcon
3. 슬롯 세트: hotbar_slot / hotbar_slot_active (인벤 슬롯은 확보됨)
4. 컨트롤 세트: button(기본/호버/눌림) / tab(active/inactive) / slider / checkbox / input_field / progress_bar
5. 프레임 장식: window_corner_decal(범용) / minimap_frame / clock_frame(선택)

## 보류 중 결정 사항 (인벤 구조 1:1 관련 — P35 Warehouse 등 적용 시 재질의)
- 루비 재화: 게임에 없음 → 골드만 표시(기본) or 기존 재화 매핑
- 무게 게이지: 시스템 없음 → 생략(기본) or 신규 시스템
- 탭: P16-16 통합그리드 사유와 충돌 → 탭 없이 통합 유지(권장) or "전체" 기본 탭 재도입

## 진행 규칙
- Phase별 커밋 분리(롤백 지점) · 배치컴파일 CS=0 + EditMode 필수 · QAPROGRESS/ROADMAP 기록 · 텔레그램 통보
- 공용 컨트롤(UTKSlot/UTKControls) 수정 시 타 창 회귀 검증 필수
