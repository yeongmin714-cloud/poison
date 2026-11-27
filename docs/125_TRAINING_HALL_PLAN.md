# 125차 계획 — 훈련소(건설→실내 배치→병사 훈련) 시스템 + 훈련소 UI Figma 정합

제정: 2026-10-24 (125차) / Figma 노드: `02vPXOGzFJUUYTaEPSVz3R/269:322` "훈련소 병사 훈련 관리" 1600×1040 (2026-10-24T01:41Z 수정 — 전수 재실측 완료)
상태: **구현 전 대기 — 사용자 승인 후 구현**

---

## 발단

사용자: "건설중에 훈련소를 추가하고 실내에 훈련소를 배치한 후, 실내에 남아있는 병사들을 훈련시킬 수 있는 시스템을 구현해줘. 훈련소를 E로 상호작용하면 훈련소 UI가 뜨고, 훈련을 시키면 레벨도 상승하고 그에 상승하게 랜덤으로 능력치도 상승. 훈련소 UI는 피그마에 만들어뒀으니 그대로 로직을 만들도록 계획해줘."

**clarify 확정 (2026-10-24)**:
1. 배치 경로 = **ConstructionManager 건설 설계도 추가** — bp_훈련소 설계도(골드+재료) + 실내 배치 허용 + 실물 시각(음식점 PropBuilder 패턴).
2. 훈련 규칙 = **골드 비용(레벨 비례 Lv×20G) + 게임시간 1시간/1회** — 완료 시 +1레벨 + 능력치 랜덤 상승.
3. 기본안(보고에서 거부권): ①훈련 대상 = 영지 주둔 병사(GuardManager.GetGuardsInTerritory — "실내에 남아있는 병사") ②동시 훈련 정원 20명(Figma 문구) ③훈련소는 영지당 1개 ④훈련 진행 상태는 세션 런타임 지속(Figma 안내 "훈련소를 닫아도 훈련은 계속" — 세이브 미영속, 병사 레벨/능력치 자체는 기존 AIHiredGuardSaveEntry(v5 atk/def/vit/level 필드 실측) 경로로 자동 영속) ⑤훈련소 레벨 표기는 v1 고정 "Lv.1"(Figma Lv.3는 샘플 — 레벨 확장 후속).

## 실측 근거 (계획 전 조사)

### A. Figma 진실치 (269:322 — 1600×1040, 게임 좌표 1:1)
- 루트: 1600×1040 (음식점창 동일 골격 — Frameless+투명루트+ApplyCanvasFit 16/120/400 선례).
- 헤더 (64,39) 1472×18: 현재 위치(좌) + 게임 시간(우) — ArenaWindowUTK 패턴(TimeManager 실데이터).
- 병사 훈련 관리 (64,81) 1472×882 = 좌 **병사 명부** 480×882 #13181F + 우 **훈련 진행 현황** 968×882 #13181F (x 오프셋 504).
- 화면 도움말 (64,987) 1472×14: "병사 목록에서 훈련 중인 병사를 확인하고, 오른쪽 창에…" + 단축키 "클릭 병사 선택 · Enter 완료 확인 · Esc 닫" (#8B949E 13px).

**좌측 병사 명부** (패딩 24):
- 패널 제목 (24,24) 432×48: users-round 아이콘 23×23 + "병사 명부" 26px #F0F6FC + "훈련 중인 병사 목록" 13px #8B949E + 닫기 30×30 #191F27(× 9×9) @ (426,24).
- 필터 (24,88) 3탭 140×36 (x=24/170/316, gap 6): **대기 중**(active #58A6FF fill, text #0B0E14) / **훈련 중**(#191F27, #8B949E) / **완료**(#191F27, #8B949E) — 클릭 전환.
- 병사 검색 (24,140) 432×34 #0F141B: search 아이콘 15×15 + "병사 이름 검색" placeholder #586473 + 정렬 아이콘 16×16 우측.
- 병사 목록 영역 (24,190) 432×668 ScrollView: 섹션 제목("대기 중인 병사" 14px #F0F6FC + "N명" 13px #8B949E 우측) + 병사 항목 408×86 #FFFFFF/0.08(r 미측정 — 카드 언어 r9.6 재사용):
  - 등급 표시 5×86 #58A6FF / 병사 아바타 53×53 #21262D/0.60(아이콘 24×24) / 병사 정보: 이름 20px #F0F6FC + "Lv.N" 19px #58A6FF / 병과 17px #8B949E / 상태 pill 50×19 #000000/0.25(상태 텍스트 14px #58A6FF — 대기 중/훈련 중/완료) + 소형 텍스트 "Lv.N" 16px #58A6FF(훈련 티어 — 누적 훈련 완료 횟수, 기본안)
  - 버튼 130×36 (x=300): **대기 중**=[훈련] 60×36 **#8B5CF6**(바이올렛) text 17px #F0F6FC / **훈련 중**=[완료] 60×36 #58A6FF text #0B0E14 + [취소] 60×36 #FFFFFF/0.02 text #F0F6FC
  - 샘플 행(대기 중 2 + 훈련 중 2): 박지훈 Lv.38 기갑 / 최유나 Lv.31 공병 / 김민수 Lv.45 보병…

**우측 훈련 진행 현황**:
- 패널 제목: castle 아이콘 + "훈련 진행 현황" 26px + "훈련 진행률, 레벨, 완료 상태" + 닫기 30×30.
- 훈련소 운영 정보 (24,88) 920×62 #191F27: 시설 아이콘 36×36 #20262E(flag 20×20) + 시설명 "전초기지 · 제1 훈련소  Lv.3" 17px #F0F6FC + 운영 설명 "한 번에 최대 20명 · 훈련 중인 병사와 완료 병력을 확인 · 완료 후…" 13px #8B949E + 운영 상태 pill 62×23 #58A6FF/0.08(dot 5×5 + "훈련 중" 13px #58A6FF).
- 훈련 진행 목록 (24,166) 920×464: 열 제목 920×30 #0F141B — 병사(x38,w238)/병과(290,120)/레벨(424,60)/훈련 진행(498,110)/남은 시간(622,112)/완료 예정(748,84)/상태(846,84) 13px #8B949E.
- 훈련 항목 행 920×62 (#58A6FF/0.08·0.04 교대): 병사 아이콘 36×36 #20262E + 이름 16px + "Lv.N" 13px / 병과 16px #8B949E / 레벨 17px #F0F6FC / **진행률 "70%" 16px #58A6FF + 진행 막대 110×4 #20262E(진행량 fill #58A6FF — 70%=77px)** / 남은 시간 "00:12:00" 17px #F0F6FC / 완료 예정 "14:42" 17px #8B949E / 상태 pill 50×19 #000000/0.25.
- 훈련 완료 병력 (24,646) 920×80 #22C55E/0.08: badge-check 26×26 + "훈련 완료 정찰병 8명" 16px #F0F6FC + "배치 후 주둔 병력 128 → 136 / 240명" 13px #8B949E + **완료 병력 배치 버튼** 220×42 #22C55E/0.08(user-round-check + "완료 병력 배치" 16px #22C55E).
- 운영 안내 (24,742) 920×13: info 아이콘 + "훈련소를 닫아도 훈련은 계속되며, 완료된 병사는 바로 배치할 수 있습니다" 13px #8B949E.

### B. 코드 접점 (기존 시스템)
- **건설**: ConstructionManager — BlueprintData(bp_*) 기반, 음식점(bp_restaurant) 전용 분기 선례(마을 검증/1개 제한/PropBuilder), **실내 배치는 음식점 외 허용(90차)**, 재료 일괄 판정(부족 시 미소비 원칙). ConstructionWindowUTK — 시설 타입 매핑("cooking_facility"→CookingWindowUTK.Open()) — 훈련소 매핑 추가 지점.
- **실내 시각**: RestaurantPropBuilder(StructureCompleted 구독) 선례 — TrainingHallPropBuilder 동일 패턴. 실내 배치 시 실내 소품(단련 더미/무기 걸이 등 — IndoorFurnitureCatalog 조합), 탑다운 원칙(천장/걸레받이 금지) 준수.
- **E 상호작용**: UI측 스테이션 컴포넌트(CookingStation.cs L84 — CookingWindowUTK.Open() 자가 호출) + HoverTargetClassifier GetComponentInParentUi 리플렉션(L116 Cook 패턴) + EInteractionPromptBridge( Systems→UI 이벤트 프롬프트) + UTKCursorOverlay 커서(kind별 아이콘).
- **병사 데이터**: GuardPlaceholder — GuardName/Level(읽기 전용)/JobTitle("병사")/Loyalty/GetStatAttack/GetStatDefense/GetStatVitability? — **GetStatAttack/GetStatDefense/GetStatVitality(L955~959, EnsureStatsRolled lazy 롤)**, _statAttack/_statDefense/_statVitality private(L29~31), MaxHP 재계산식 `e.maxHP - _statVitality * 2f`(L903). **훈련 적용용 public API 신설 필요**(레벨 세터 없음).
- **병사 집계**: GuardManager.Instance.GetGuardsInTerritory(TerritoryId)/GetAllPlayerGuards — 훈련 대상/주둔 병력 소스.
- **영속**: SaveData v5 AIHiredGuardSaveEntry — stableId/guardName/**level**/hp/maxHP/loyalty/**atk/def/vit** 필드 존재 → 훈련 결과(레벨/능력치)는 기존 로스터 경로로 자동 영속. 훈련 진행 중 상태는 런타임 전용(주석 명시).
- **게임시간**: TimeManager.Instance/GetOrCreate — GameTime(초)/Hour/Minute/CurrentDay. 훈련 종료 = GameTime + 3600초, 남은 시간 HH:MM:SS, 완료 예정 = 종료 시각(Hour:Minute).
- **결정론 랜덤 규약**: 신규 시스템 결정론(System.Random 인스턴스 시드) — WandererDriver(GetInstanceID 시드) 선례 준용. "랜덤 능력치"도 결정론 랜덤으로 구현(재현 가능 — QA/세이브 정합).

## 계획 (Phase별)

### P1 — 훈련소 건설 설계도 (BlueprintData + ConstructionManager + PropBuilder)
- BlueprintData에 `bp_traininghall` 신설: 골드 **600G** + 재료 **목재 12 + 돌 8**(현실감 선호 — 기존 시설 재료 규약 준용, 구현 시 기존 bp_ 항목 필드 구조 그대로).
- ConstructionManager: 훈련소 배치 검증 — 실내 배치 허용(음식점 금지 규약과 반대, 90차), **영지당 훈련소 1개 제한**(시공 중 포함 — 음식점 마을당 1개 판정 패턴 준용), 골드/허가세/재료 일괄 판정(부족 시 미소비).
- StructureCompleted → `TrainingHallPropBuilder.cs` 신설(음식점 RestaurantPropBuilder 패턴): 실물 시각 생성 + **TrainingHallStation(UI 컴포넌트) 부착** + EInteractionPromptBridge 프롬프트("훈련소"). 실내 배치면 실내 소품(단련 더미+무기 걸이 — IndoorFurnitureCatalog 조합, 팀버 0.60/보 0.50·탑다운 계약 준수), 실외 배치면 실외 시각(동일 소품+기둥).
- ConstructionWindowUTK 시설 매핑에 traininghall 케이스 추가(필요 시 — 시설 타입별 창 매핑 표에 훈련소=훈련소 UI가 아니라 건설 완료 처리).

### P2 — E 상호작용 (훈련소 → 훈련소 UI)
- `Assets/Scripts/UI/TrainingHallStation.cs` 신설(CookingStation 패턴 — E 상호작용 → `TrainingHallWindowUTK.Open()`).
- HoverTargetClassifier: `TargetKind.TrainingHall` 신설 + GetComponentInParentUi 리플렉션 분기(Cook 패턴 L116 동일).
- UTKCursorOverlay: TrainingHall 커서 아이콘 매핑(Cook 패턴).
- InteractionPanelUTK: TrainingHall kind 지원(단일 액션 "훈련소 열기" — 기존 단일 액션 레이아웃 재사용).

### P3 — TrainingHallSystem (Systems, 훈련 로직 코어)
- `Assets/Scripts/Systems/TrainingHallSystem.cs` 신설(정적 레지스트리 + Update 폴링 300ms — ArenaSystem/InteriorStationPlacer 규약):
  - `TrainingSession`: guard(StableId 키)/territoryKey/startGameTime/endGameTime/cost/atkGain/defGain/vitGain(**시작 시점에 결정론 랜덤 확정 — System.Random(guard.StableId+훈련횟수 시드)**, 재현 가능).
  - `StartTraining(GuardPlaceholder guard, string territoryKey)`: 가드 체인 — ①병사가 해당 영지 소속(GetGuardsInTerritory 소속 판정) ②이미 훈련 중 아님 ③정원 20명 미달 ④골드 충분(비용 = **guard.Level × 20G**) → PlayerStats.SpendGold("training_hall") → 세션 등록. 거부 사유 반환 문자열(InteriorStationPlacer.TryPlace 패턴).
  - `CancelTraining(session)`: 취소(비용 환불 없음 — 기본안, 훈련 중단).
  - 완료 폴링: GameTime ≥ endGameTime → 완료 목록 이동 + `GuardPlaceholder.ApplyTrainingLevelUp(atkGain, defGain, vitGain)` 호출.
  - `GuardPlaceholder.ApplyTrainingLevelUp(int atk, int def, int vit)` 신설: EnsureStatsRolled 선처리 → level++ → _statAttack/Defense/Vitality 가산 → MaxHP 재계산(L903 규약 동일: maxHP - vit×2) → StatusMessage 갱신. (public API — private 필드 직접 접근 대신 계약 메서드.)
  - 능력치 상승 산식(확정): 3종 각각 0~2 랜덤, 합계 최소 1 보장(0,0,0 방지 — 결정론 랜덤으로 재설정 루프).
  - 완료 병력 집계: "훈련 완료 병사 N명"/"배치 후 주둔 병력 A → B / 240명"(좌측 정원 상수 240 — 실측 기준 기존 수비대 상한 소스가 있으면 그 값, 없으면 Figma 240 고정 — 구현 서브 실측 후 결정, 주석 문서화).
  - 훈련소 레벨: v1 고정 Lv.1(표기만 — 확장 후속).
- 세이브: 병사 레벨/능력치 = 기존 AIHiredGuardSaveEntry 경로 자동 영속(무수정). 훈련 진행 상태 = 런타임 전용(헤더 주석 명시 — 세이브 연동 후속).

### P4 — TrainingHallWindowUTK (UI, Figma 전정합)
- `Assets/Scripts/UI/Toolkit/TrainingHallWindowUTK.cs` 신설 — 1600×1040 골격(음식점창 동일: Frameless+투명루트+ApplyCanvasFit 16/120/400), Figma 진실치 전부 반영:
  - 헤더(현재 위치+게임 시간 — TimeManager 실데이터, ArenaWindowUTK 패턴) / 도움말 바(안내+단축키 문구).
  - 좌측 병사 명부: 필터 3탭(대기 중/훈련 중/완료 — 클릭 전환)+검색(이름 contains 필터)+섹션 제목(N명)+병사 항목 408×86(등급 5px/아바타/이름/Lv/병과/상태 pill/버튼 — 대기중=[훈련 #8B5CF6], 훈련중=[완료 #58A6FF]+[취소], 완료=[배치 #22C55E]).
  - 우측 훈련 진행 현황: 운영 정보(시설명 "현재영지 · 제1 훈련소 Lv.1"+정원 문구+운영 상태 pill)+진행 목록(열제목 7칼럼+행 920×62: 진행률%+진행 막대 fill/남은 시간 HH:MM:SS/완료 예정 시각 — 300ms 폴링 실시간)+훈련 완료 병력(#22C55E 섹션+완료 병력 배치 버튼)+운영 안내.
  - **데이터 소스**: 병사 목록 = GuardManager.GetGuardsInTerritory(현재 영지) — "실내에 남아있는 병사"=영지 주둔 병사(기본안). 훈련 대상 선택=행 클릭.
  - 단축키: Enter=선택 병사 훈련 시작(대기 중)/완료 수령(완료), Esc=닫기(기존 창 규약).
  - 완료 병력 배치 버튼 = 완료 병사를 주둔 병력으로 복귀 처리(훈련 세션 완료 → 영지 병력 카운트 반영 — 기존 배치 시스템 접점 실측 후 최소 배선).
- **병과 표기**: 기존 병사 무기 계열 소스 실측(활/대검/창 방패 컨트롤러) 후 매핑 표기 — 소스에 없으면 JobTitle("병사") 그대로(위조 금지).
- 아이콘 export: users-round/castle/flag/search/arrow-down-wide-narrow/badge-check/user-round-check/info + 병사 아이콘(Figma 벡터 → PNG, 124차 export 파이프라인 재사용).

### P5 — 테스트
- `TrainingHallSystemTests.cs` 신설(순수 산식): 비용(Lv×20)/진행률 계산/남은 시간 포맷/정원 20 가드/능력치 상승 결정론(시드 고정 재현)/합계 최소 1 보장/가드 체인(중복·정원·골드 부족)/ApplyTrainingLevelUp(레벨+1·능력치 가산·MaxHP 재계산).
- 소스-텍스트 계약(Figma 진실치): 1600×1040 골격/좌 480 우 968/필터 3탭 문구/#8B5CF6 훈련 버튼/진행 막대 110×4/완료 병력 #22C55E/열제목 7칼럼/단축키 문구/E키→Open 배선/TrainingHall kind 분기.
- 인접 회귀: ConstructionManagementFigmaTests(매핑 표 갱신 시 — 소멸 예정 문자열 사전 점검), InteractionPanel/Classifier 기존 계약 무손상.

## 검증
- 구현 서브 단계별 배치컴파일 error CS=0 → QA 서브 EditMode 전수(baseline 11건 1:1, 신규 회귀 0).
- **Play 게이트(별도 승인)**: ①bp_훈련소 건설(재료/골드 소비+실물 가시) ②실내 배치+훈련소 프롬프트 ③E키 → 훈련소 UI ④병사 선택→훈련 시작(골드 차감 Lv×20G) ⑤진행률/남은 시간 실시간 ⑥게임시간 1시간 경과 → 완료(레벨+1·능력치 랜덤 상승 로그) ⑦완료 병력 배치 ⑧훈련소 닫아도 훈련 지속 ⑨Esc/Enter 단축키.

## 승인 게이트
- **본 계획서 사용자 승인 후 구현 착수** / Play 검증 전 사용자 승인 / 커밋 전 사용자 승인.