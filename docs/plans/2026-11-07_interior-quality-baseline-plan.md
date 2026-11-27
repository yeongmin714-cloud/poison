# 실내씬 고품질화 계획서 — 공통 골격 + 영지 건물 실내부터 (2026-11-07)

## 0. 목표 정의 — 기준 2건 판독(실측)
- **품질바 = Screenshots/내부씬 예시.PNG(9/20)**: ①1×1m 타일 그리드(줄눈 명확) ②벽 두께 ~0.25m·높이 ~2.5m·투톤 배색+걸레받이+벽톱 캡 마감 ③가구 GLB(베벨 모서리 — 프리미티브 아님) 바닥 점유율 40~50%, 벽면 L/U자+중앙 아일랜드 혼용 ④조명 포인트/스포트 중심, 냉:온 70:30, 소프트 그림자 ⑤소품 "사용 흔적"(도구·상자·전선형 디테일) ⑥방 비대칭 구획+기능 공간 분리 ⑦인형의 집(전면 벽 절단 — **DioramaKit 이미 보유**).
- **레이아웃 = Screenshots/실내 영주씬 예시 설계도.png(9/29)**: 팔각형 중앙 로비 + 5방(N 침실/NW·W 제작소/NE·E 창고/SW 병영/SE 연금술소)+남측 입구 홀, 석재 타일+로비/침실 붉은 카펫, 방별 기능 조명(제작소 붉은 화로/연금 보라·초록), 벽면 따라 가구+중앙 동선.
- **사용자 요구(계약)**: 자기/타 영주 지형 구조는 달라도 **분위기·규격(맵 크기·타일 모듈)은 통일** — 모듈은 단일 소스, 영지/국가 차이는 팔레트·깃발·장식색으로만.

## 1. 현황 실측(갭 정의)
- **토폴로지 = 이미 존재**: InteriorLayoutDefinition이 설계도 구조 그대로(팔각 로비 8개 포탈 실측 — N Bedroom/NW·W Craft/NE·E Storage/SW Barracks/SE Alchemy/S EntryHall). PlayerCastleInteriorBuilder(1,823행)가 WallThickness 0.45·국가별 벽 텍스처(IndoorTextureGenerator)·석기둥 보유. **레이아웃 재작업 불요 — 표면·가구·조명만 고도화.**
- **현재 렌더(tour_castle_player_view 판독)**: 바닥 타일+카펫만 있고 벽 두께감·가구·조명·소품 전무 — "바닥 텍스처 테스트" 수준(사용자 판정과 일치).
- **가구 소스 보유**: `Assets/새로운 glb/쓸만한/Medieval Megapack - Low Poly.zip` **미임포트 1,265 GLB**(Other 148/Food Props 182/Tools 51/Weapons 33 = 실내 소품 풀), Restaurant GLB(159차 아레나에 이미 사용 — 탁자/의자/주방), 마을 GLB 46종.
- **빌더 보유 현황**: CaveInteriorBuilder(1909행)/PlayerCastle(1823)/InteriorLayoutDefinition(596)/InteriorStationPlacer(338)/InteriorNationFlagSystem(324)/Barn(206)/House(209)/Restaurant(178)/Tavern(190)/Arena/ArenaPit/Afterlife/CastleInteriorBuilder(**166행 — 타 영주 성은 셸 수준**). shop/crafthouse/church 빌더 파일 별도 확인 필요(EnterBuilding case는 존재).

## 2. 공통 골격 계약 — `Systems/InteriorQualityBaseline.cs` 신설(단일 소스)
모든 실내 빌더가 호출하는 공용 모듈. 수치는 기준 사진 판독값.

| 항목 | 규격 | 근거 |
|---|---|---|
| 타일 모듈 | 1.0m 그리드 스냅(바닥 쿼드 분할+줄눈 절차 텍스처) | 9/20 사진① |
| 벽 | 두께 0.25m(현행 0.45 → 0.25 통일)/높이 2.5m | 사진②·캐릭터 1.8m 계약 |
| 벽 마감 | 하단 걸레받이 밴드(높이 0.15m 짙은톤)+상단 평면 캡(바닥 오독 방지 짙은돌선 — 성내부/동굴 156차 계약 승계) | 사진②·탑다운 원칙 |
| 바닥 팔레트 | 건물 톤 1개+방별 톤(침실=목재/창고=거친돌/제작=석재+그을음 등) — 결정론 절차 텍스처(FarmPlotVisuals.GetOrCreateSoilTexture 단일소스 선례) | 사진①·설계도 |
| 조명 레시피 | 방당 웜 포인트 1~2+기능 컬러 포인트 0~1, 광 예산 방당 ≤4·로비 ≤6, Soft 그림자 | 사진④·125차 광원 밀도 교훈 |
| 가구 밀도 | 방 바닥 면적 40~50%(통로 제외) — GLB 우선, 부재 시 프리미티브 폴백(소실 금지 계약) | 사진③ |
| 배치 결정론 | StableHash 기반 변형(159차 ArenaInteriorBuilder 패턴) — Random 0건 | 결정론 계약 |
| 소품 층 | 카펫(로비/침실 붉은 러그)+국가 깃발(InteriorNationFlagSystem 재사용)+촛대/횟불+상자·도구 "사용 흔적" | 사진⑤·설계도 |
| 인형의 집 | DioramaKit 훅 유지(천장 제거+남벽 시각 절단) — 관람 모드와 정합 | 기존 계약 |

**국가/영지 차등 = 팔레트 톤+깃발+장식색만**(구조·타일 모듈·벽 체계·조명 형식 공유 — 사용자 분위기 통일 계약).

## 3. Phase 0 — Medieval Megapack 수용+인테리어 카탈로그
- 전수 감사 후 임포트(147차 계약: 신규 에셋팩=계획 전 glTF 전수 감사 — 1,265 GLB 중 **실내 풀만 선별 수용**: Other/Food Props/Tools/Weapons 중 가구·소품 예상 200~300종).
- GUID 충돌 리맵 준비(155차 Necropoly 선례 — 204건 재생성→698파일 리맵 사례).
- 산출: `Resources/Models/Interior/`+`InteriorPropCatalog.cs`(카테고리별 로더 3차 폴백 — AfterlifeCemeteryCatalog 선례)+계약테스트(선별 수수/mat=NULL 0건).

## 4. Phase 1 — 플레이어 영주 성 표면 고도화(최우선)
토폴로지 무수정(InteriorLayoutDefinition 계약). PlayerCastleInteriorBuilder 내부 교체.
- **Lobby**: 1m 석재 타일(짙은 회색·줄눈)+중앙 붉은 카펫(설계도)+국가 깃발 2(InteriorNationFlagSystem)+탁자/의자 GLB(접견 세트)+횟불 4(웜)+촛대 소품. 광 ≤6.
- **Bedroom(N)**: 목재 바닥 톤+침대 GLB+러그+옷장/서랍+촛대 웜 1.
- **Crafting(NW/W)**: 석재+그을음 톤+작업대 GLB+화로(붉은 포인트 1)+도구 소품(Tools 카탈로그).
- **Storage(NE/E)**: 거친 돌 톤+선반/상자열(벽면 L자)+창고 조명 dim 웜 1.
- **Barracks(SW)**: 돌 톤+침대열(설계도)+무기 거치(Weapons 카탈로그)+웜 1.
- **Alchemy(SE)**: 돌 톤+작업 테이블+병 소품(Food Props/기타)+보라/초록 포인트 1.
- **EntryHall(S)**: 돌판(158차 돌판 경로 패턴 승계)+횟불 2.
- 검증: 계약테스트(타일 모듈 스냅/벽 규격/방별 광 예산/가구 밀도 하한/카펫 존재/결정론 grep)+Play 프로브(탑다운 캡처 6컷 — 방별 1컷)+사용자 Play.

## 5. Phase 2 — 타 영주 성(CastleInteriorBuilder 리빌드)
- 동일 골격+동일 방 배정(설계도 구조 공유 — 사용자 "분위기 비슷" 계약), **차등=국가 팔레트(GenerateCastleWall{Nation} 재사용)+깃발+장식색**.
- 166행 셸 → Phase 1 모듈 호출로 전면 교체(+방별 가구는 Phase 1 목록 재사용 — 이중 소스 금지).
- 검증: Phase 1 동일+**플레이어 성과 탑다운 캡처 나란히 비교(규격 일치 계약테스트 — 타일 스냅/벽 규격/광 예산 동일값)**.

## 6. Phase 3 — 일반 건물 실내 순차 고품질화(성 완료 후)
| 건물 | 바닥 톤 | 핵심 가구(카탈로그) | 기능 조명 |
|---|---|---|---|
| house | 목재 | 침대/식탁/선반 | 웜 촛불 |
| shop | 돌+목재 | 진열대/카운터/상자 | 웜+상점 간판광 |
| restaurant | 돌 | **기존 GLB 재사용(159차)** — 바닥/벽/조명만 보강 | 웜 화로 |
| tavern | 목재 | 탁자/부스(159차 승계)+바 | 웜+횟불 |
| crafthouse | 석재+그을음 | 작업대/도구 | 붉은 화로 |
| church | 석재+러그 | 벤치열/제단 | 청/금 스포트 |
| barn | 흙+짚톤 | 사료통/건초 | 웜 1 |
- P3-0 사전 감사: shop/crafthouse/church의 현행 빌더 소유 지도(EnterBuilding case → 호출 경로 전수 grep).

## 7. Phase 4 — 통합 검증
- 계약테스트 신설: InteriorQualityBaselineTests(모듈 수치/결정론 grep/광 예산/가구 밀도 하한/카펫·깃발 존재/국가 차등=색만 — 구조 해시 동일 비교).
- **Test_23 관람 모드 전 엔트리 탑다운 캡처**(기존 [ ] 순회 재사용) — 전 실내 나란히 몽타주로 규격 일치 육안 판정+사용자 Play.
- 전수 게이트: compile CS 0 + EditMode baseline 10건 1:1.

## 8. 리스크
- 임포트 1,265 GLB 중 선별 실패/GUID 충돌(155차 수리 절차 준비)·GLB 원본 스케일 과대(155차 실측 스케일 테이블 선례).
- 광 예산 초과 시 성능(125차 대형 홀 교훈 — 방당 상한 계약으로 방어).
- PlayerCastle 1,823행 수정 범위 확대 위험 — 표면/가구/조명만, 토폴로지·이동/포탈 계약 무수정.

## 9. 진행 방식
- Phase 순서: P0 → P1 → (사용자 Play 판정) → P2 → P3(건물당 Play 판정) → P4.
- 구현=서브에이전트 위임(600s 타임아웃 시 부모 폴백), 게이트/프로브=부모 직접, Play·커밋=별도 승인.
- 대기: "진행" 승인(Phase 0+1 먼저 착수 — 성내부가 사용자 지정 최우선).
