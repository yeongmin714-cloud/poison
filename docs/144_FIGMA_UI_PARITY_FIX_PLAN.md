# 144차 계획 — 전 UI Figma 1:1 정합 수정계획 (감사 결과 기반, 승인 대기)

작성: 2026-10-31. 근거: `docs/144_figma_parity_audit.md` (전수 감사 완료) + 배치 보고서 21개(`TestOutput/figma_audit_144/`). **계획 전용 — 승인 전 구현 없음.**

## §0 원칙 (불변)

1. Figma 단일 진실(재실측 원본 우선) — 게임이 맞춘다. 단 §5 계약 우선 항목은 유지.
2. 5축(그리드/배열/여백/플루언트/폰트) 1:1 — 감사 등급(P0/P1) 순 수리, P2(≤1px)는 원칙 유지(수리 시 발생 리스크>편익).
3. 1창 = 1사이클: 위임 구현 → 컴파일 CS 0 → EditMode baseline(집합 1:1) → 계약테스트 같은 차 갱신 → (승인 시) Play 캡처 픽셀 비교.
4. Figma에 없는 게임 전용 요소는 삭제 금지(기능 회귀) — 유지하고 "역반영 제안" 목록으로.
5. 구현/QA 서브에이전트 분리 위임(docs/AGENT_DELEGATION_POLICY.md).

## §1 사용자 결정 (2026-10-31 수신 완료)

| # | 결정 | **사용자 답변 (확정)** |
|---|---|---|
| D1 | 글래스 알파 0.85 vs Figma 0.702 | **피그마 스타일 — 0.702로 전 창 통일** |
| D2 | 컴팩트 상호작용 패널 4종(fishing/sleep/lordroom/lockpick 288px) | **구현** |
| D3 | 전투일지 창(daily-combat-log 94:6) | **구현** — 신규 DailyCombatLogUTK |
| D4 | 영지별 주간 세금 패널(243:3) | **구현** — 신규 창 |
| D5 | EquipmentWindowUTK | **폐기**(삭제 — 실구현은 StatusWindow 임베드) |
| D6 | Figma 부재 창 ~40종 | **확정: A그룹(실배선 24종)=현상 유지 기록 / B그룹(미배선 레거시 22종+FastTravel+SleepUTK)=폐기** |
| D7 | 문구/단위 차이("kg"→"칸", "수정석"→"마력석", "습득하기"→"인벤토리에 놓기" 등) | **피그마 스타일로 모두 교체** — 단 실데이터 부재 항목(무게 kg 등 목업 데이터)은 슬롯 표기 유지(데이터 위조 금지 계약) |

### §1-1 D6 — Figma 부재 창 전체 목록 (배선 실측 기준, 2026-10-31)

**A. 실배선 활성 — Figma 프레임 없음 (역반영 여부 지명 대기, 24종)**
- 메뉴/시스템: EscMenuUTK(ESC 메뉴) · OptionsUTK(옵션) · SettingsMenuUTK(설정) · LoadGameUTK(불러오기)
- 창: CraftingWindowUTK(허브 조합 1060×660 — v1~v3와 별개) · EncyclopediaWindowUTK(도감) · LordFeedWindowUTK(영주 피드) · InvadedLordDialogueUTK(침략 영주 대화) · GameStatsUTK(게임 통계)
- HUD/오버레이: TimeClockGlassUTK(시계) · TimeDisplayUTK(시간 표시) · NameplateOverlayUTK(이름표) · GuardSquadHotbarUTK(부대 핫바) · QuestChoiceUTK(퀘스트 선택) · MonsterInfoUTK(몬스터 정보) · SoldierInteractUTK(병사 상호작용) · EInteractionPromptUTK(E키 프롬프트) · BowAimReticleUTK(활 조준) · FarmPlateOverlayUTK(농경 타일) · GasSprayUTK(가스) · PlayerFlagUTK(깃발) · RestaurantCoinPopupOverlayUTK(코인 팝업) · TutorialGuideUTK(튜토리얼) · WarNotificationUTK(전쟁 알림) · TerritoryInfoPopupUTK(영지 정보 팝업)

**B. 미배선/레거시 — 프로덕션 호출부 0 (폐기 후보, 22종)**
AchievementUTK · AlchemyStationUTK · ArenaMenuUTK · AutoMoveUTK · ChurchUTK · DeathScreenUTK · EndingCreditsUTK · EnvoyMissionUTK · FestivalUTK · LoadingScreenUTK · MainMenuUTK · MercyUTK · MissionResultUTK · NPCDailyUTK · NPCDialogueUTK(레거시 통합 대화) · RTSSelectionOverlayUTK · ReadDocumentUTK · RepairStationUTK · RevengeListUTK · RouteConfirmationUTK · SaveSlotUTK · SpyMissionUTK · FastTravelUTK(문자열 참조뿐) · SleepUTK(폐기 주석 존재 — UTKWireUp)

## §2 Phase 0 — 공용 어휘 정비 (1사이클, 전 창 선행)

반복 P0의 근원이 공용 어휘 부재 — 1회 정비로 수십 창의 P0/P1이 동시 소멸.

| # | 어휘 | 근거(반복 발생) | 구현 위치 |
|---|---|---|---|
| V1 | **코너 데칼 4각 헬퍼**(14×14/10×10, #58A6FF 1.8px, 코너 밖 걸침 옵션) | CSP/EP/퀘스트12/공격조8/ArenaBattle/고용비/포션/낚시/상호작용7/병사관리(오배치 수리)/대화(색·위치) | UTKTheme 또는 신규 UTKDecals.cs — NPCDialoguePanelUTK.AddCornerDecals 선례 이식 |
| V2 | **패널 테두리 스타일 상수**(#30363D@0.502, w1.2) + 요소 테두리 헬퍼 | 크래프트/건설/수면/음식점/퀘스트/세금(0.5 고정 폐기)/로드아웃 | UTKTheme |
| V3 | **UTKSlot 슬롯 어휘 보강**: 빈셀 프레임+square-dashed 아이콘 옵션, TierStrip(h3.6) 옵션, Count(13.2 GeistMono600) 지원 | 상점 빈셀 TierStrip 제거(P0), 전리품/음식점 빈셀 아이콘(P0), 크래프트 TierStrip·Count(P0) | UTKControls.cs |
| V4 | **헤더 2행(Title+Subtitle 영문)** 어휘 | 인벤 "INVENTORY" 등 | UTKTheme 헬퍼 |
| V5 | **CloseButton 32×32(288:7/9)** 어휘 | 월드맵 좌우, 베팅창 | UTKControls |
| V6 | **배지/칩 스타일**(TierBadge r4.8 fill@0.122+stroke, CountBadge #000@0.251 r7.2, StatusPill @0.251) | 크래프트 EPIC 배지, 병사관리, 등 | UTKTheme |
| V7 | **stale 리전 정리** — InventoryFooter(168,911) 삭제/갱신 | inventory.md P1 | InventoryClusterPanelRegions.cs |
| V8 | 글래스 알파 단일값 (D1 결정 반영) | 전 창 | UTKTheme.ApplyFigmaGlass |

검증: 컴파일 0 + EditMode baseline + 기존 계약테스트(UTKTheme/ShopLootFigma 등) 갱신.

## §3 Phase 1 — 사용자 지명 창군 (상점/인벤/장비+설명+창고)

**상점 ShopWindowUTK** (P0 1 · P1 19 · P2 17)
- P0: `BuildEmptyBagCell` 빈셀 TierStrip 제거(Figma ItemSlotEmpty=프레임만) — V3 어휘 사용.
- P1: 판매 모드 액션 버튼 #F85149(Danger)→#58A6FF(Figma 76:7 SellButton 라벨만 교체), 활성 탭에 없는 테두리 제거, 액션 버튼 텍스트 웨이트 700→400, 패널 fx(블러36+그림자 38.4@0.251) 계약 반영(D1), "kg"→"칸"은 D7.
- 근거: shop.md (142차 G/H 수리분 재검증 포함 — 80칸 그리드·셀 81.6/피치 91.2는 PASS 유지).

**인벤 InventoryWindowUTK** (P0 2 · P1 11 · P2 9)
- P0: 헤더 Subtitle "INVENTORY"(14.4/400 #8B949E) 추가(V4), 코너 데칼 4개 추가(V1).
- P1: 가방 셀 배경 #30363D→#21262D, 전 셀 테두리 #30363D@0.502 1.2(현재 JSON 진실 — 68차 "테두리 없음" 기록 폐기), 무기 핫키 배지 중앙정렬, 활성/비활성 탭 테두리, CloseBtn 28→31.2, EquippedSection 카드 테두리+r9.6, stale 리전(V7).

**아이템 설명 ItemDescriptionWindowUTK** (P1 3 · P2 2) — 타이포/패딩 3건(보고서 참조).
**창고 WarehouseWindowUTK** (P1 2 · P2 3) — BadgeText 12/Bold→14.4/400 등.
**장비(84:55)** — StatusWindowUTK 장비구획 P0 2(EP 코너 데칼, 전술 배낭 TierStrip 25개 — V1/V3 선행 필요) + P1 18. EquipmentWindowUTK는 D5 결정에 따름.

## §4 Phase 2 — 크래프트 계열 (CraftBenchBase 기반 1회 + 3클래스)

- **기반 CraftBenchBaseUTK 선행**(v1/v2/v3 공통 괴리 집약): 패널 글래스(알파 D1)·테두리(V2), 조합섹션(24,152,456,194.4)·레시피섹션(24,386)·푸터(24,825,63.2)·재료슬롯 76.8×3+결과 91.2·행 h72·Detail 4섹션(96/191/594, 폭 528)·Storage 25셀(81.6·gap9.6·5열)+StorageFooter(24,697,456,56 — 푸터 전체 누락 P0).
- **WeaponForgeUTK** P0 10: TierStrip(전 슬롯, V3), 재료 Count 표시, 성공확률 라벨, Divider(63:64), SELECTED/LV. 배지, EN 아이템명, TierBadge(48.2×24.6 r4.8), BlueprintVisual(그리드 20라인+타원 216/96+RenderGraphic 240×240 — 아이콘 160→240), StatsGrid 3행, BadgeItem, StorageFooter.
- **CookingWindowUTK** P0 6: TabContainer를 (24,96) 상단으로 이동(현 (24,413.4) 하단 이중 배치 — TabsLocalBounds 정의만 존재), EstimatedRate 좌 라벨 분리, 값색 #3FB950→#56D364, 우측 정렬.
- **AlchemyBenchUTK** P0 3(v3 고유): 탭 6개 폭 70/72.4·fs14.4, 섹션 −1px 체인, BioText h69.
- 검증: Figma123 계약테스트 계열 갱신(3판본 텍스트 매칭 근거는 crafting.md §0).

## §5 Phase 3 — 전리품/결과/낚시

- **LootWindowUTK** P0 1: 빈셀 square-dashed 아이콘 38.4(V3). P1 10: Count "x" 접두사·우하단 앵커(right0/bottom2 vs 7.6/4.6)·헤더 pb9.6(Subtitle y 10.2→15.2 — 재실측 정정분)·푸터/헤더 스트로크·데칼색 #2E343D→#58A6FF. 136차 진실치(헤더48/stats y86.4/그리드 y127.4/푸터 y314.6 등)는 재실측과 일치 — 유지.
- **HarvestResultUTK** P0 1: SystemTip info 아이콘 16.8. P1 31: 내부 gap 20→24(6개소), head mb20+verb mt20 이중마진 40 해소, 인셋박스/설명섹션 알파@0.5~0.6+스트로크+r, Badge 스타일 반전 교정(반투명+액센트 글자), Subtitle 판본별 14.4~15.6, 아이템명 CENTER+색, Keep 라벨 D7. k=min 균일 계약 유지(이미 준수).
- **FishingUTK** P0 5: game-header-bar(퀘스트 헤더)·HUD-tip-bar(장력 경고) 섹션 신설, ActionGrid 액션 카드 2세트(릴 감기+SPACE/취소+Q, keycap), 데칼 4코너(V1), 창 크롬(336×345.6 r14.4+스트로크+글래스). P1 23: 내부 패딩 20→24, 섹션 간격 24px 체계(구판 280×289 잔재 제거). **163:4 컴팩트 패널은 D2 결정에 따름.** 장력 바 색은 의미색 유지.

## §6 Phase 4 — 상태/포션/대화/상호작용

- **StatusWindowUTK CSP** P0 9: 코너 데칼 4개(V1), CharacterIdentitySection 카드(528×84 — 코드네임은 데이터 소스 부재로 슬롯 표기, ClassType 추가), SpecialStatsSection VERTICAL 게이지 2단 재구성(라벨행+BarBg, Ellipse 도트, 괄호 서브라벨, (24,724) 528폭), StatsSection 카드 프레임 #FFFFFF@0.012. P1 23: 알파(D1), 상단 패딩 24→18, 타이틀 21.6→24, EXP 색 골드→#56E39F, CoreStatsGrid 다색, 특수게이지 트랙색 반전 교정.
- **PotionStatusUTK** P0 3: 데칼(V1), potion-image-wrapper(187.2×274.8 단독 이미지 구조), btn-spray-toggle("G키 분사/종료" — 현 첫 버튼 "복용"과 병존/대체는 짧은 의도 확인 후). P1 12: 배지 "IN BAG"→"ACTIVE" #58A6FF, 트랙/필 코너·색, 닫기 버튼.
- **NPCDialoguePanelUTK** P1 10 (P0 0 — 기준 양호 창): 루트 테두리 #30363D/0.3 w1.2, 패딩 12→14.4, gap 9.6, NpcNameRow h15, ContinueHint 폭 %→(시각 동등하면 유지), 187:14 힌트 순서(선택지 위), ChoiceCard 테두리+패딩, 데칼 위치(코너 밖 걸침)+색 #58A6FF.
- **InteractionPanelUTK** P0: 81:51 `Ally` 분기 = 물약주기/음식주기로 교체(현 뇌물/포섭 — AddAction("물약주기") 호출부 신설, IsActionSupported dead case 활성화), 버튼 아이콘 12종 16.8px(V3 계열 아이콘 어휘), 데칼 4각(V1). P1: 하이라이트 배경 #30363D@0.8+액센트 테두리, 비활성 fill@0.6·stroke·r4.8, 93:185 그리드 h36. 142차 B' 배경판 유지.

## §7 Phase 5 — 퀘스트/전투일지/월드맵

- **QuestWindowUTK** P0 10: 루트 디밍 #0B0E14@0.82, 데칼 12개(V1), Figma에 없는 닫기×3·수락/완료 버튼·체인 UI → **기능 유지+역반영 제안 목록**(삭제 금지 — D6 방침), 히어로 부제 행, 보상 설명문·부제, DeploymentFooter(추적 토글 행+포기/추적 버튼). P1 51: 글래스(D1), 제목색 #F0F6FC, 슬롯/행 배경 #FFFFFF@0.02, r9.6, 카드 간격 14.4, 라벨 폰트(모노/굵기), 행간 12, 그림자/블러.
- **전투일지(D3 승인 시)**: 신규 DailyCombatLogUTK — BattleLogPanel 744×984@(588,48), 전투 소식 헤더/시간·장소 메타/교전 세력 vs/WIN·DEFEAT 배지/"6 / 50 세션 로드됨". QuestJournalUTK 무수정(별개 창).
- **월드맵** P0 7: **좌(지역 탐색 리스트)/우(선택 지역정보) 독립 창화** — 125차 후속38 재보고 함정의 3회 반복 방지. WorldMapWindowUTK의 BuildQuestListPanel/BuildQuestDetailPanel을 독립 UTKWindow(95:188 456×888 / 95:255 480×888)로 분리 + CloseButton(V5) + PanelHeader TitleGroup 가로(gap12 BASELINE). P1: 퀘스트 카드 2·3 스타일(선택카드 오적용 해소), 카드 간격 9.6, 우 TrackingRow pt14.4, 브리핑 lineH 21.6, 영지핀 "Lv.45" 라벨(95:249), 현재지핀 라벨 15→13.2. 본체 P1 5: 루트 fill #0B0E14, SystemGroup y+3px(ay 절대좌표 오인 정정), letterSp 1.2. 배경 2레이어 계약 PASS 유지. FastTravelUTK는 D6(미배선 레거시 기록).

## §8 Phase 6 — 투기장/병사/훈련소

- **ArenaResultWindowUTK** P0 8: 좌판 베팅한 선수 가이드·레벨 칩·전투 특성, 상대 썸네일·결과 텍스트, 우판 골드 항목 라벨·순이익 라인·정산 요약 2행. P1 38: 폰트 축소 교정(22→20 등 Figma 기준), 반투명 배지(@0.078) 솔리드 반전 해소, 칩 x −24px, TextMain→#F0F6FC.
- **ArenaWindowUTK** P0 13: 게임 헤더는 [125차 후속13] 사용자 지시 제거 이력 — **재도입 금지, 계약 우선 기록**(Figma와 의도적 차이). 닫기 버튼, 크레스트/배지, 선택 선수 개요 카드, 기록 요약 3칸, 전투 능력치 가로 3열 그리드, 금액 입력 박스, 상한 안내 5000→10,000(게임 밸런스 계약이면 유지 — 확인), 베팅 확인 카드, 유의사항, 사진 카드 "장비" 라벨. P1 38 동일 패턴.
- **SoldierManagementUTK** P0 3: MissionOption 영문 부제 7개+2행 MissionText, 라디오 원+check는 39차 사용자 의도 제거 — **유지(계약 우선 기록)**, 8번째 "대기(해제)" 카드는 역반영 제안(유지). 데칼 오배치(TR/BL/BR 수정 — V1). P1 14: 글래스(D1), 비활성 탭 배경, PanelHeader 테두리, CountBadge/StatusPill, 임무명 5개 문구(D7).
- **TrainingHallWindowUTK** P0 3: 헤더 VECTOR 아이콘 2종(전초기지/시계), 대기중 '취소' 버튼 추가. **P1 핵심: y −36px 전체 시프트**(Figma 루트 pad-top 36 미반영 — 계통 수리 1건으로 51건 중 다수 해소), 크롬(r/테두리/그림자), 폰트 스케일 상향 교정, 4열 우측 정렬, 상태 pill 폭.

## §9 Phase 7 — 건설/음식점/세금/수면/고용비

- **ConstructionWindowUTK** P0 11: **재료표 열 x 체계 전면 수리** — 코드 ColX 14/374/490/606 vs Figma 5열 14/374/490/606/752(ColXSource 시프트 정황 — 상수 재정렬), 충족수 "3 / 4" 라벨, 조건 카드 아이콘 3종, 설계도 벡터, 좌패널 닫기 제거(Figma 무존재 — 기능은 우측 창 닫기로 유지), 필터 '기타' 탭은 역반영 제안(유지). P1 45: 표 스타일 stroke 일괄(V2), 제목 보조텍스트 가로 병렬, 상태 배지 (332,21), 요약 셀 병렬행, '수정석'→'마력석'(D7), DROP_SHADOW. 불투명 예외 계약 PASS 유지.
- **RestaurantWindowUTK** P0 3: 빈슬롯 square-dashed×5, wallet/info 아이콘(V3). P1 핵심: **우 목록 열 x −24px**(134차 패널-로컬 vs 리스트-로컬 측정 기준 혼동 — 리스트-로컬 14/394/500/610/680/768/888로 교정, 관리열 목록 우단 10px 초과 해소), 우패널 하단 스택 −10~13px, 열간격 8(코드 10), 보더 다수(V2), TextMain→#8B949E 6요소, rim 그림자 제거(DisableWindowShadow — arena 선례). 134차 좌패널 진실치 전부 유효(재실측 일치).
- **RecommendedTaxWindowUTK** P1 27: 테두리 0.5 고정→실측(1.2/1.8/2.4/1 요소별, V2), 호감도 라벨 x −33.6, 권장 표시선, 색상 7건(#58A6FF/#F087D/#56D364 체계), "감소 기준" 후단 문구, 버튼 굵기. 구조·좌표 22건 PASS.
- **주간 세금(D4 승인 시)**: 신규 WeeklyTaxWindowUTK — 624×984(패널제목/영지 필터/세금 목록 576×662/주간 요약 576×114), TaxManager 데이터 연결.
- **SleepPanelUTK(624×1101)** P0 3: 회복 항목명 "체력"/"기력" 라벨, 슬롯 정보 3행(저장 상태·PLAY TIME 행), 슬롯 아이콘 글리프. P1 24: 테두리 일괄(V2), 절차 아이콘 채색, 소소 오프셋. 3칩 시간선택·스위치 등 격자 대부분 PASS.
- **MercenaryPaymentNoticeUTK** P0 4: 데칼(V1), 행 등급 바, "Lv.NN" 배지(별표 통합 대체), 지불 요약 카드(divider+단일 라벨 재배치). P1 21: 루트 배경 #161B22→#13181F+테두리·r, 선택 탭 스타일, 폰트 일괄 축소(21.6/16.8/14.4), 행 r/pad, 아바타 원형 r20.

## §10 Phase 8 — HUD 계열

- **HotbarUIUTK** P0 2: corner-brackets 8선(#8C734B@0.251)+캡션 텍스트. P1: 핫키 배지 색 반전 교정(#F0F3F5 밝은 배지), 슬롯 r9.6. 8슬롯/96px/19.2gap/FigmaWidth 902.4 PASS 유지.
- **QuickSlotUTK** P0 2: 6슬롯 56px vs 8슬롯 96px 체계 불일치 — 단 **U8 은퇴 게이트(런타임 비부착)** → 정합 수리는 HotbarUIUTK 기준으로 어휘 정리(폐기/병합은 짧은 의도 확인).
- **TabRotationHint** P0 1: HintIcon 밝은 배지. TabBadge 골드 #D4AF37.
- **MinimapUTK** P1 3: 지름 220→264, 여백 24(FigmaBezel* dead const 살리기), 3링→텍스처.
- **NameplateOverlayUTK** P0 1: floating-stats 수직 3행화(Figma). BarW78/BarH9.6 상수 유지([80차] PlateW96 통일 계약 우선 — 실적용 폭 계약 기록).
- **DynamicEventUTK** P0 3: 영상 플레이어 체계(장면/재생/탐색/볼륨), 유형 배지+발생시각, 데칼(V1), 크기 312×396.
- **HUDUTK** P1 4: 알파 계열(0.62/0.149 등). Y하단 앵커 계약 유지.
- UTKGaugeStripArc: 계약 PASS — 무수정.

## §11 Phase 9 — Figma 부재 창 (D6) + 마무리

- D6 기본안: 현상 유지 기록(감사 §3 목록). 사용자가 특정 창 지명 시 그 창만 역반영(Figma 프레임 신규 작성 제안) 또는 신규 프레임 기반 정합 — 별도 사이클.
- 최종: 전 창 상태표 갱신(docs/143_figma_parity_audit.md → 144 감사문서로 승계), QAPROGRESS 기록.

## §12 실행 리듬·검증 (전 Phase 공통)

1. 사이클 시작 전 창당 짧은 의도 확인(무응답 시 Figma 진실치 최소 수정 — 기존 계약).
2. 구현 위임(좌표·근거 동봉) → 컴파일 CS 0 → EditMode 전수(baseline 11+병행WIP 제외, 실패집합 1:1) → 계약테스트 같은 차 갱신(소스-어서션 갱신 원칙).
3. Phase 0 공용 어휘는 전 창 회귀 위험이 커서 **단독 사이클+전수 테스트** 후 진행.
4. Play 캡처(1920×1080) 픽셀 비교 게이트는 사용자 승인 — 창 영역 크롭 diff.
5. 커밋은 사용자 승인(별도 게이트, 지정 파일만).

## §13 우선순위 요약 (승인 범위 제안)

- **즉시**: §1 D1~D7 답변 → Phase 0(공용 어휘) → Phase 1(상점/인벤/설명/창고/장비구획)
- **이후**: Phase 2(크래프트) → 3(전리품/결과/낚시) → 4(상태/포션/대화/상호작용) → 5(퀘스트/월드맵/전투일지) → 6(투기장/병사/훈련) → 7(건설/음식점/세금/수면/고용비) → 8(HUD) → 9(부재 창 마무리)
- 기대 효과: Phase 0 단독으로 P0 약 40건+P1 약 150건이 소멸(데칼/테두리/글래스/빈셀 어휘 반복분).
