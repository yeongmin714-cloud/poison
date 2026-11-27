# 124차 계획 — 월드맵 UI Figma 전면 정합 (95:167 medieval-world-map-ui)

제정: 2026-10-24 (124차) / Figma 노드: `02vPXOGzFJUUYTaEPSVz3R/95:167` "medieval-world-map-ui" 1920×1080 (lastModified 2026-10-24T00:36Z — 전수 재실측 완료)
상태: 구현 전

---

## 발단

사용자: "피그마에 월드맵 UI가 있어 — 플루언트 구조고 왼쪽 퀘스트/오른쪽 지역 정보, 각 영지 마크도 현재 구현과 다름. 퀘스트는 나중에 구현할 거지만 일단 월드맵을 피그마와 완전히 일치시켜줘. 로직도 같이 일치해야 해."

**clarify 확정 (2026-10-24)**:
- 배경 = **Figma 아트 2레이어 그대로 + 기존 지형 베이크(호수17·숲·국가색) 폐기** (완전 일치 — 그려진 지형/지명/나침반/범례는 아트 장식, 마커는 실제 좌표 배치).
- 핀 배색 = **소유 상태 색 계약 유지**(플레이어=#39D353/영주=기존 빨강/분쟁=#FF9E2C/무주=회색), 핀 형태=Figma 신형.
- 기본안(보고에서 거부권): ①우측 패널 진입=마커 **클릭 선택** ②123차 호버 툴팁은 유지 ③좌패널 퀘스트 탭=Figma 플레이스홀더 카드(정적)+지역 탭=실제 영지 리스트(82) ④QuestPin·경로선=퀘스트 시스템 후속이므로 이번 제외.

## 실측 근거 (진실치 전수 — Figma px = 게임 px 1:1, 1920×1080)

### 전역 구조 (루트 95:167, 절대좌표 −36226/−3 보정)
- 배경 2레이어(풀스크린 1920×1080): ①`MedievalWorldMapBackground`(95:168 — IMAGE + SOLID **#0B0E14/0.45** 오버레이) ②`ParchmentTextureBackground`(97:5 — IMAGE, 위에 얹힘). PNG export 완료: **/tmp/figma_bg_95_168.png**, **/tmp/figma_bg_97_5.png**.
- HUDHeader: (0,0) 1920×77, 하단 보더 #30363D/0.50 w1.2.
- HUDBody: (0,80) 1920×1003 — 좌패널/핀/우패널.
- 경로선 2본(#FF9E2C w3 / #58A6FF w2.4, dash [4.8,7.2]) — 퀘스트 후속, 이번 제외.

### HUDHeader
- SystemGroup (48,29) 299×25: "세계 지도" Roboto 21.6/700 #F0F6FC + Divider (104,3) 1×19 #30363D/0.50 + "WORLD MAP SYSTEM" (125,4) Roboto 15.6/500 #58A6FF.
- CoordinatesGroup (1416,31) 456×20: "현재 위치:" Roboto 14.4/400 #8B949E + 값(현재 영지명) **Geist Mono 15.6/700 #39D353** + "좌표:" + "X: {player.x:F2} / Y: {player.z:F2}" Geist Mono 15.6/700 #58A6FF — 실시간 갱신.

### 좌측 QuestListPanel (48,109) 456×888 — r14.4, fill #161B22/1.0, stroke #30363D/0.50 w1.2
- Decal 코너 4개 14×14 (TL(0,0)/TR(442,0)/BL(0,874)/BR(442,874)) — L자 2라인 #58A6FF w1.8.
- PanelHeader (24,24) 408×37: "지역 탐색 리스트" 19.2/700 #F0F6FC + "REGIONS" 13.2/500 #8B949E.
- FilterTabs (24,81) 408×36: TabActive "퀘스트" 200×36 r7.2 fill #58A6FF text 14.4/700 #0B0E14 / TabInactive "지역" 203×36 r7.2 stroke #30363D/0.50 w1.2 text 14.4/700 #8B949E. 클릭 전환 로직 포함(퀘스트/지역 2탭).
- QuestSlotsContainer (24,136) 408×728 = **ScrollView**:
  - 퀘스트 탭: Figma 그대로 정적 플레이스홀더 카드 3장 — QuestSlot 408×121 r9.6 fill **#FFFFFF/0.08** stroke **#58A6FF/1.0 w1.8**(테두리는 선택강조색 고정 아님 — 카드 3장 전부 동일 실측), 내부: TierStrip (0,0) 5×121 색 #39D353/#FF9E2C/#58A6FF, CardHeader (14,14) 379×19 = CategoryPill 62×19 r4.8 fill #0B0E14 stroke #30363D/0.5 w1.2 + text 12/700(현재 진행=#39D353/메인 임무=#FF9E2C/보조 임무=#8B949E) + "Lv…" 14.4/700(#58A6FF/#FF9E2C/#8B949E), CardBody (14,48) 379×59 = 제목 16.8/700 #F0F6FC + 설명 14.4/400 #8B949E. 텍스트 원문: ①"현재 진행"/"Lv.35 - 45"/"고원 레이더 방해 해제"/"잊혀진 혹성 고원에서 송출되는 레이더 방해 전파를 차단하고 관측망을 복구" ②"메인 임무"/"Lv.45"/"차원 문 요새 작전"/"부서진 차원의 문 요새에 침투해 크로노스 연료 장치를 회수하고 작전지 지..." ③"보조 임무"/"Lv.25 - 30"/"공업지구 보급 지원"/"엘리시온 정착 공업지구로 중수소 운송선과 보급 물자를 안전하게 인도하라."
  - 지역 탭(기본안): 동일 카드 언어(#FFFFFF/0.08, r9.6)의 컴팩트 행 82개 — 영지명 16.8/700 #F0F6FC + 소유 상태 13.2(#계약색) + 영주명 13.2/400 #8B949E, 클릭=선택(우패널 갱신). 소유색 TierStrip 적용.

### 우측 QuestDetailPanel (1392,109) 480×888 — r14.4, fill #161B22, stroke #30363D/0.50 w1.2
- Decal 코너 동일(TR/BR x=466).
- PanelHeader (24,24) 432×37: "선택 지역 정보" 19.2/700 #F0F6FC + "TACTICAL INTEL" 13.2/500 #8B949E.
- QuestHeroHeader (24,81) 432×80 r9.6 fill **#FFFFFF/0.02** stroke #30363D/0.5 w1.2: 영지명 21.6/700 #F0F6FC @ (14,14) + 배지 r4.8 fill {소유색} text 12/700 #0B0E14(우측 정렬) + 부제 14.4/400 #8B949E @ (14,49).
- BriefingCard (24,180) 432×119 r9.6 fill #FFFFFF/0.02 stroke 동일: "해당 구역 주요 상태" 15.6/700 #58A6FF @ (14,14) + 본문 14.4/400 #8B949E lineHeight 21.6 @ (14,40).
- LordSection (24,318) 432×210: 제목 "영주 정보 (LORD STATUS)" 15.6/700 #58A6FF + LordItems 4행(각 432×35.4, y=0/47.4/94.8/142.2): IconCol 29×29 r7.2 fill #161B22/0.80 stroke #30363D/0.5 w1.2 + 아이콘 17×17 + 라벨 15.6/700 #F0F6FC + 값 13.2/400 #8B949E. 행: ①crown "영주 이름" ②users "병사 수" ③star "병사 평균레벨" ④shield "전투력".
- DeploymentFooter (24,548) 432×124:
  - TrackingRow 432×48 r9.6 fill #FFFFFF/0.02: TrackLeft (14,14) = radar 아이콘 19×19 + "지도 상 실시간 좌표 추적" 15.6/700 #F0F6FC / TogglePill (365,10) 53×29 r14.4 fill #58A6FF + knob 24×24 #0B0E14 — **knob x=26.4 = 오른쪽(ON)**.
  - ButtonContainer (0? ) 432×47: AbortButton (0,0) 212×47 r7.2 stroke #30363D/0.5 w1.2 "닫기" 15.6/700 #8B949E / ApplyButton (222,0) 210×47 r7.2 fill #58A6FF "지점 전송 (Fast Travel)" 15.6/700 #0B0E14.
- 아이콘 PNG export 완료(scale=2): **/tmp/figma_icon_crown.png, users, star, shield, radar** (표시 17×17/19×19).

### 핀 신형 (마커)
- 핀 프레임: PinBase 38×38 r19.2(원) fill {소유색}/0.10 + stroke {소유색} w1.8 — 중심=월드좌표 앵커. 내부 도트 ellipse 10×10 solid(플레이어 핀만 14×14 + 외곽 링 ellipse 53×53 stroke #39D353 w1.2, rel(-7,-7) 중앙 헤일로).
- PinLabel: base 하단 rel (0,46), r4.8 fill #161B22/0.80 stroke #30363D/0.50 w1.2, text (10,5) 13.2/700 #F0F6FC = 영지명.
- 소유색 매핑: PlayerOwned=#39D353(초록, 도트 14+링), LordOwned=기존 빨강 상수 재사용, Contested=#FF9E2C, Unoccupied=#8B949E(회색).
- 기존 마커(점+라벨) 폐기 → 신형 핀으로 교체. 250ms 폴링 RefreshVisual로 소유색/라벨 갱신 유지.

### 로직 계약 (실데이터 — 위조 금지)
1. 클릭 선택: 핀/지역탭 행 클릭 → 선택 영지 저장 + 우측 패널 전면 갱신 + 핀 선택 강조(기존 선택 표현 없음 — 카드 stroke #58A6FF w1.8 재사용).
2. 우측 패널 데이터: 영지명=def.territoryName / 배지=소유 상태 텍스트+계약색 / 부제=def.description(기존 필드) / 주요 상태=기존 데이터 조합(소유 상태·난이도·수비대 수·전쟁 중 여부 — TerritoryWarManager/_currentNationOwners 실측) / **영주 이름=state.ownerLordName(PlayerOwned→"플레이어" — 123차)** / 병사 수=실배치 병사 집계 소스 우선(GuardPlaceholder 소속 집계), 없으면 def.guardCount / 평균레벨=GuardPlaceholder.Level 평균(집계 0이면 "—") / 전투력=기존 전투력 산식 실측 재사용(구현 서브가 코드 확인 후 기존 소스 매핑 — 신산식 신설 금지).
3. 실시간 좌표 추적 토글: ON(기본) = PlayerPin+좌표 텍스트 실시간 갱신(기존 250ms PollTick 재사용), OFF = 정지. PlayerPin=플레이어 현재 위치 핀(초록, 링 포함, 라벨 "{영지명} (현재지)").
4. 지점 전송 (Fast Travel): 클릭 → `FastTravelSystem.ExecuteFastTravel(선택 TerritoryId)` 기존 API(가드·비용 처리는 기존 시스템). 접근 불가/비용 부족 시 기존 시스템 응답 존중(신규 토스트 로직 신설 금지 — 기존 토스트 계약 재사용).
5. 닫기 버튼 = 창 닫기(기존 Close/M키 토글과 동일 경로). ESC 유지.
6. 123차 호버 툴팁 유지(UTKTooltip) — 우패널과 병행.
7. 좌표 갱신: CoordinatesGroup 실시간(토클 OFF여도 좌표 텍스트는 갱신 — "추적"은 핀 위치 고정 의미).

### 폐기 목록
- 범례 스트립(L134~163 블록), 위치줄 글래스 스트립(121차), 지형 베이크 호출(CreateTerrainOverlay/RefreshTerrainOverlay — **WorldMapTerrainBaker.cs 파일 자체는 잔존**, 호출부만 제거), 구형 마커 렌더.

## Phase 분할

- **Impl-A**: 배경 PNG 2장+아이콘 5장 리소스화(Assets/Resources/UI/WorldMap/ 복사, meta는 Unity 생성 맡김) + 배경 2레이어 교체 + HUDHeader(세계 지도/좌표 실시간) + 폐기(범례/위치줄/지형베이크) + 좌측 QuestListPanel(탭 전환+퀘스트 플레이스홀더 3카드+지역 탭 실데이터 82행 ScrollView). 컴파일 확인.
- **Impl-B**: 핀 신형 마커(소유색+헤일로+라벨칩+클릭 선택) + PlayerPin + 우측 QuestDetailPanel 전체(실데이터+토글+FastTravel+닫기). 컴파일 확인.
- **QA(별도 서브)**: 배치컴파일 + EditMode 전수(baseline 11건 1:1) + 신규/인접 패밀리.
- 테스트: 소스-텍스트 계약(Figma 진실치 — 456×888/480×888/#161B22/r14.4/Geist Mono 좌표/토글 knob 우측/FastTravel ExecuteFastTravel 배선/ownerLordName 매핑 등) + 우패널 데이터 포맷터 순수 함수 테스트(WorldMapTerritoryTooltipContentTests와 별개 파일).

## 검증

- 구현 서브 각 단계: 배치컴파일 error CS=0.
- QA: EditMode 전수 — 기존 baseline 11건(AeroLocalizedGasPhase4B/CraftBench/Instrument×3/InvadedLord×2/MeleeClick×2/RecipeCatalog/ThemeUSS) 1:1, 신규 회귀 0.
- **Play 게이트(별도 승인)**: ①M키 → AETHEL 아트 배경+신형 헤더/패널 가시 ②핀 클릭 → 우패널 실데이터(영주 이름=ownerLordName) ③토글 ON/OFF → 플레이어 핀 실시간 추적 ④지점 전송 → Fast Travel 실행 ⑤지역 탭 전환 ⑥퀘스트 탭 플레이스홀더 3카드.
- 커밋: 별도 승인.