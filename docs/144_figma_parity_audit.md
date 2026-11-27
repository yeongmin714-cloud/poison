# 144차 전 UI Figma 1:1 감사 — 마스터 취합 (전 UTK 창 전수)

작성: 2026-10-31 (Telegram 세션). 사용자 요청: "각 UI 다시 한번 더 피그마 정합 완벽히 됐나 1대1로 모두 확인 — 그리드 배열 여백 폰트크기 플루언트 디자인 모두 정합. 모든 UI, 대충 하지 말고 모두 1대1 대조 + 수정계획".

## §0 감사 방법 (수행 실적)

- **Figma 진실치 전수 재실측**: 파일 `02vPXOGzFJUUYTaEPSVz3R` (lastModified 2026-10-26 — 이후 무개정) → **54 프레임** nodes API 심층 추출 → `docs/figma_truth_144/`(54파일 2.7MB). 좌표/크기/fills(실효알파=colorA×paintOpacity)/strokes+두께/코너 r/그림자/폰트(크기·패밀리·행간)/오토레이아웃(gap·패딩) 전수.
- **추출기 결함 자체 발견·정정**: 1차 추출에서 paint 레벨 알파 유실(전부 1.0) — inventory 배치가 이상 감지 → 재축출 후 **shop/inventory/equipment 3개 보고서 알파 행 정정 완료**(각 보고서 하단 "알파 재검증 정정" 섹션). 플루언트(투명도) 축은 정정본 기준.
- **감사 실행**: 21개 배치 QA 서브에이전트(위임 정책 준수, 코드 수정 일체 금지, 읽기 전용) — 보고서 `TestOutput/figma_audit_144/<배치>.md` 21개, 프로토콜 `docs/figma_audit_144_PROTOCOL.md`.
- **대조 축**: ①그리드(칸수/셀/간격/정렬) ②배열(위치/순서/구조) ③여백(padding/gap) ④플루언트(배경알파/글래스/테두리/코너/그림자) ⑤폰트(크기/행간/정렬/굵기).
- **등급**: P0=구조(누락/미구현/체계 불일치) · P1=수치(≥2px, 폰트≥1, 색상) · P2=≤1px 근사 · PASS=일치 확인.

## §1 전체 집계 (배치 보고서 합산)

**총 P0 약 155건 / P1 약 770건 / P2 약 268건** (감사 대상 창·패널 ~40단위)

| # | 창(클래스) | Figma 프레임 | 판정 | P0 | P1 | P2 |
|---|---|---|---|---|---|---|
| 1 | ShopWindowUTK | game-shop-ui 74:4/76:7 | 조건부 부합 | 1 | 19 | 17 |
| 2 | InventoryWindowUTK | fluent-game-inventory-ui 15:5 | 조건부 PASS(기하 정합) | 2 | 11 | 9 |
| 2a | InventoryClusterFigmaLayout/Regions | 15:5/15:141/15:197 | **PASS**(stale 리전 1) | 0 | 1 | 6 |
| 2b | ItemDescriptionWindowUTK | 15:141 리전/스팟 | 조건부 PASS | 0 | 3 | 2 |
| 2c | WarehouseWindowUTK | 15:197 리전/스팟 | 조건부 PASS | 0 | 2 | 3 |
| 3 | EquipmentWindowUTK | equipment-inventory-panel 84:55 | **미연결 유구 + 대규모 괴리** | 4 | 13 | 0 |
| 3a | StatusWindowUTK 장비구획(계약 임베드) | 84:55 | 조건부 PASS | 2 | 18 | 2 |
| 4 | WeaponForgeUTK(+CraftBenchBase) | crafting-panel v1 63:5 | 괴리 있음 | 10 | 24 | 13 |
| 5 | CookingWindowUTK | crafting-panel v2 64:2 | 괴리 있음 | 6 | 21 | 12 |
| 6 | AlchemyBenchUTK | crafting-panel v3 64:337 | 괴리 있음(v3 고유) | 3 | 4 | 5 |
| 7 | LootWindowUTK | loot-panel 70:4 | 구조 합치 | 1 | 10 | 5 |
| 8 | HarvestResultUTK(3변형) | result-ui 72:4/80/143 | 크롬 괴리 다수 | 1 | 31 | 6 |
| 9 | FishingUTK | fishing-panel 160:4 | 5섹션 미구현+크롬 괴리 | 5 | 23 | 5 |
| 9a | interaction-panel-fishing 163:4 | — | **전체 미구현**(E키 프롬프트 대체) | 1 | 0 | 0 |
| 10 | InteractionPanelUTK(7변형) | 81:4/51/91/134, 82:2/45, 93:185 | 조건부 부합(81:51 제외) | 13 | 55 | 13 |
| 11 | SleepPanelUTK(288 컴팩트 163:45) | interaction-panel-sleep | **미구현**(월드 프롬프트 대체) | 1 | 0 | 0 |
| 11a | LordAudienceUTK(288 컴팩트 163:64) | interaction-panel-lordroom | **미구현**(82:2 호버판 사용) | 1 | 0 | 0 |
| 11b | LockpickingUTK(288 컴팩트 163:83) | interaction-panel-lockpick | **미구현**(분기 제외) | 1 | 0 | 0 |
| 11c | ArenaBattleUTK(261:3) | 대화·베팅 상호작용 | 구현됨(데칼/아이콘 누락) | 2 | 7 | 9 |
| 12 | NPCDialoguePanelUTK | npc-dialogue-panel 163:23/187:14 | **최양호 — P0 0** | 0 | 10 | 14 |
| 13 | StatusWindowUTK(CSP 본체) | character-status-panel 83:4 | 괴리 다수 | 9 | 23 | 4 |
| 14 | PotionStatusUTK | potion-status-panel 173:4 | 괴리 다수 | 3 | 12 | 6 |
| 15 | QuestWindowUTK | quest-window-ui 93:230 | P0 다수 | 10 | 51 | 3 |
| 16 | QuestJournalUTK | daily-combat-log 94:6 | **전투일지 자체 미구현**(퀘스트 저널 클론 존재) | 8 | 8 | 0 |
| 17 | WorldMapWindowUTK 본체 | medieval-world-map-ui 95:167 | 경미(배경 2레이어 PASS) | 0 | 5 | 8 |
| 17a | 좌측 지역 탐색 리스트 | 95:188 | **독립 창 미구현(임베드)** | 3 | 5 | 9 |
| 17b | 우측 선택 지역정보 | 95:255 | **독립 창 미구현(임베드)** | 3 | 6 | 11 |
| 17c | 핀 3종(현재지/영지) | 95:238/244/250 | 영지핀 "Lv.45" 라벨 누락 | 1 | 1 | 13 |
| 17d | FastTravelUTK | — | 미배선 레거시, 판정 불가 | — | — | — |
| 18 | ArenaResultWindowUTK | 투기장 승/패 280:163/381 | 골격 정합+구성 P0 | 8 | 38 | 10 |
| 19 | ArenaWindowUTK(베팅) | 투기장 베팅 255:647 | P0 다수 | 13 | 38 | 2 |
| 20 | ArenaMenuUTK | — | Figma 부재 | — | — | — |
| 21 | SoldierManagementUTK | soldier-management-ui 69:4 | 골격 정합+P0 3 | 3 | 14 | 8 |
| 22 | TrainingHallWindowUTK | 훈련소 269:322 | **y −36px 계통 시프트**+크롬 미적용 | 3 | 51 | 5 |
| 23 | ConstructionWindowUTK | 전초기지 건설 239:683 | **재료표 열 x 전면 어긋남** | 11 | 45 | 8 |
| 24 | RestaurantWindowUTK | 음식점 237:792 | 조건부 부합(우패널 +24px 시프트) | 3 | 62 | 9 |
| 25 | RecommendedTaxWindowUTK | 단일 영지 세금 244:281 | P1 다수(테두리 0.5 고정) | 0 | 27 | 10 |
| 25a | 영지별 주간 세금 패널 243:3 | — | **전체 미구현** | 1 | 0 | 0 |
| 26 | SleepPanelUTK(실체 624×1101) | 수면 시스템 패널 227:262 | P0 3 | 3 | 24 | 12 |
| 27 | MercenaryPaymentNoticeUTK | 고용비 알림 210:169 | P0 4 | 4 | 21 | 8 |
| 28 | AttackFormationUTK | 공격조 전투 배치 204:1963 | 골격 정합+P0 3 | 3 | 61 | 16 |
| 29 | HUDUTK(게이지×2) | HUD_Canvas 23:6 | 알파 계열만 | 0 | 4 | 3 |
| 29a | HotbarUIUTK(+Tab 힌트) | QuickSlotsHUD/TabRotationHint | 코너브래킷·캡션 누락 | 3 | 9 | 5 |
| 29b | QuickSlotUTK | 23:68 | **6슬롯56px vs 8슬롯96px 체계 불일치**(U8 은퇴 게이트) | 2 | 0 | 0 |
| 29c | MinimapUTK | empty-circular-minimap 40:44 | 220 vs 264 등 | 0 | 3 | 0 |
| 29d | NameplateOverlayUTK | monster-health-bar 101:5/110:14 | 수직 3행 vs 수평 1행 | 1 | 9 | 4 |
| 29e | UTKGaugeStripArc | fluent-gauge-bar 175:65 | **계약 PASS**(벡터메시 금지) | 0 | 0 | 0 |
| 29f | DynamicEventUTK | 이벤트 영상 패널 246:141 | 영상 플레이어 체계 전무 | 3 | 4 | 2 |

## §2 공통 패턴 (전 창 반복 — 공용 어휘 1회 정비로 수십 건 일괄 해소)

1. **코너 데칼 4각(또는 8각) 누락/오배치** — 인벤·상세·창고, CSP/EP, 퀘스트 12개, 공격조 8개, ArenaBattle, 고용비, 포션, 낚시, 상호작용 7판본, 병사관리(TR/BL/BR 오배치), 대화(위치·색 괴리) → NPCDialoguePanelUTK `AddCornerDecals` 선례 존재(적용만 누락인 창 다수).
2. **테두리 `#30363D@0.502 1.2px` 일괄 누락** — 크래프트 계열 전 패널/행/버튼, 건설창 표, 수면 카드·버튼, 음식점 다수, 퀘스트, 세금(코드 0.5 고정 vs Figma 1.2~2.4), 로드아웃 카드.
3. **글래스 알파**: UTKTheme 0.85 vs Figma 0.702 — 전 창 공통 (단, 사용자 조정 이력 존재 → 사용자 재판정 항목). `#161B22` hue·r14.4·stroke 1.2는 정합.
4. **빈 셀/슬롯 어휘 불일치** — 상점 빈 가방 셀에 Figma에 없는 TierStrip 추가 렌더(P0), 전리품 빈셀 square-dashed 아이콘 누락, 크래프트 UTKSlot에 TierStrip 전무, 음식점 빈슬롯 아이콘 누락.
5. **버튼/헤더 아이콘 누락** — 상호작용 12종(16.8px), ArenaBattle 2종, 음식점 3종, 훈련소 2종(VECTOR), 건설 조건카드 3종, 결과창 info 아이콘.
6. **헤더 Subtitle(영문 보조텍스트) 누락** — 인벤 "INVENTORY" 등 2행 헤더 구조.
7. **CloseButton(288:7/9 32×32) 누락** — 월드맵 좌우 패널, 베팅창. (역방향: 퀘스트는 Figma에 없는 닫기 3개 보유.)
8. **"Figma에 없는 게임 전용 요소" 다수** — 상점 모드 탭, 퀘스트 수락/완료 버튼·체인 UI, 병사관리 "대기(해제)" 8번째 카드, 훈련소 운영안내·배치 버튼 등 → 삭제 금지(기능 회귀), 역반영 제안 목록화.
9. **개별 계약 우선 항목(정합 유지)** — HUD 하단 앵커(24/12px), 월드맵 패널@0.80, 건설창 불투명 예외, 상호작용 패널 배경판(142차 B'), 게이지 어휘(UTKGaugeStripArc), 시맨틱색(국가/HP/등급).
10. **폰트 패밀리/행간** — 프로젝트 단일 폰트(NotoSansKR)·lineH 무설정은 공용 계약으로 보류 판정, 크기/굵기/색만 등급.

## §3 Figma 부재/판정 보류 창 (구현 존재, 프레임 없음)

- **Figma 부재**: CraftingWindowUTK(허브 조합 1060×660), ArenaMenuUTK(620×560), QuestChoiceUTK, EInteractionPromptUTK(82:45 계열 대체), FarmPlateOverlayUTK, TimeClockGlassUTK, TimeDisplayUTK, UTKGoldBalance, NameplateOverlay(일부), RestaurantCoinPopupOverlayUTK, HerbRespawnUTK, TutorialGuideUTK, RouteConfirmationUTK, WarNotificationUTK, GasSprayUTK, RTSSelectionOverlayUTK, BowAimReticleUTK, PlayerFlagUTK, AutoMoveUTK, LoadingScreenUTK, MainMenuUTK, EscMenuUTK, OptionsUTK, SettingsMenuUTK, LoadGameUTK, SaveSlotUTK, DeathScreenUTK, EndingCreditsUTK, ChurchUTK, EncyclopediaWindowUTK, EnvoyMissionUTK, FestivalUTK, GameStatsUTK, InvadedLordDialogueUTK, LordFeedWindowUTK, MercyUTK, MissionResultUTK, NPCDailyUTK, NPCDialogueUTK(560×460 레거시, 호출부 0), RevengeListUTK, SpyMissionUTK, RepairStationUTK, AlchemyStationUTK, AchievementUTK, SoldierInteractUTK, ReadDocumentUTK, TerritoryInfoPopupUTK — **역반영 제안 또는 현상 유지 기록 대상** (수정계획 §9).
- **폐기 잔존**: SleepUTK(UTKWireUp:39-43 폐기 주석, 라우팅은 SleepPanelUTK).
- **미연결 유구**: EquipmentWindowUTK(프로덕션 호출부 0 — StatusWindowUTK 임베드가 실 구현).

## §4 배치 보고서 인덱스 (요소별 근거는 각 파일)

shop.md · inventory.md · equipment.md · crafting.md · loot_results.md · fishing.md · interaction.md · side_panels.md · dialogue.md · status_potion.md · quest.md · worldmap.md · arena.md · soldier.md · training.md · construction.md · restaurant.md · tax.md · sleep_hire.md · attack.md · hud.md — 전부 `TestOutput/figma_audit_144/`. 진실치: `docs/figma_truth_144/`(54프레임). 프로토콜: `docs/figma_audit_144_PROTOCOL.md`.

## §5 감사 신뢰성 한계 (기록)

- 알파 1차 유실 → 3개 보고서 정정 완료, 나머지 18개는 정정본 기준 감사.
- 유실 항목(arcData/이미지 imageRef/선택 strokes 면정보)은 판정 제외 — 스킬 규약 준수.
- 런타임 USS 상속/동적 계산 요소는 "코드 무상수" 기록 — Play 실측 게이트 별도.
- 스크린샷/Play 판정은 본 감사 범위 밖 (수정 후 픽셀 비교 게이트는 계획 §12).

## §6 [144차 P9] D6 결정 기록 + 전 창 상태표 (2026-11-01)

- **D6 확정(사용자 결정 2026-10-31 수신, 계획 §1-1)**: A그룹(실배선 24종, Figma 프레임 없음)=**현상 유지 기록** — 역반영은 사용자 지명 시 창 단위 별도 사이클. B그룹(미배선/레거시 22종+FastTravel+SleepUTK)=**폐기 후보** — 실제 파일 삭제는 별도 승인 게이트(기록 우선, 144차에서는 실행 안 함). SleepUTK만 [UTKWireUp:39] 선행 폐기 완료(라우팅=SleepPanelUTK 이식 확인).
- **WeeklyTaxWindowUTK(243:3) 배선 보류 [144차 P9]**: Open() 진입점만 존재(프로덕션 호출부 0). 진입 경로 후보 ①단일 영지 창(RecommendedTax) 내부 진입 버튼 ②상점NPC 패널 5번 버튼(81:134 Figma 정합 위반 우려) — 사용자 지명 대기(10분 무응답 → 기존검증 최소설계 원칙에 따라 기록만). 소스 헤더 계약 주석에 동일 기록.
- **환호 GLB 폴백 [144차 P9-ACQ] 수리 완료**: 143 감사표 §2-1① 잔존분 종결 — resolver 실GLB 매핑 3건 추가(fruit_apple/flower_wild/wood_dagger — File.Exists 실측), 매핑 실패 시 생략 계약 폐기 → 아이콘(ProceduralIconGenerator)+아이템명 TextMesh 폴백. 갭 대조표 TestOutput/acq_resolver_gap.txt. GLB 실측 부재 4건(iron_ore 등 광석)은 폴백이 담당(위조 경로 없음).
- **전 창 상태표**: Phase 0(공용어휘)~8(HUD) 전부 완료 — 각 Phase 상세는 docs/QAPROGRESS.md [144차] 항목. 미완료(공통): Play 스크린샷 픽셀 비교(사용자 승인 게이트), 커밋(별도 승인).
