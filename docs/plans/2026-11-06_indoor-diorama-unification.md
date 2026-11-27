# 실내 수조(디오라마) 통일 계획 — 비던전 실내 전체 (2026-11-06)

> 사용자 요구: "던전 제외, 모두 같은 탑다운뷰의 수조의 실내씬" — 9/20 실내씬 예시(Screenshots/실내씬 예시.png·영지 내부 예시.PNG·내부씬 예시.PNG·자기소속 영주 실내 예시.png) 기준.
> 사용자 확정: ①이계(afterlife) 제외 ②아레나 실외 제외 ③남벽 처리=시각만 절단+콜라이더 유지(권장 채택).

## 수조 기준정의 (5항목 — 예시 판독 기반)

1. **천장 부재** — CreateRoom이 만든 Ceiling 전수 제거
2. **카메라 측(남측, −z) 벽 시각 절단** — IndoorCameraFollow 피치65°/거리11m 고정 yaw=0(북쪽 바라봄)이므로 남측 벽만 시야 차단. **시각만 off, 콜라이더/메시/GO 활성 유지** (플레이어 탈출 불가 + 미니맵 bounds 불변)
3. **벽톱캡 윤곽 유지** — room 레벨 형제인 톱캡은 살려서 탑다운 원칙의 "벽윗면 평면캡" 설계도 룩 유지
4. **문/출입구 시각 유지** — 이름 게이트(Entry/Door/Gate/Exit 포함 제외) — 출구 인지성 보장
5. **void 배경/조명** — 1차 라운드 미수정(실측: IndoorScene 배경 이미 암색). 재촬영 스크린샷 판정 후 별도 후속

## 현재 상태 (빌더별 실측 — 2026-11-06)

| 빌더 | 천장 | 벽 구조 | 수조 판정 | 조치 |
|---|---|---|---|---|
| PlayerCastleInteriorBuilder | **있음**(ceilingMat 생성) | KeepOctagon 토폴로지 풀하이트+문굽이 | 박스 | **Kit 훅**(cutSouth=true) |
| HouseInteriorBuilder | 있음 | CreateRoom 4면 | 박스 | **Kit 훅** |
| ShopInteriorBuilder | 있음 | CreateRoom 4면 | 박스 | **Kit 훅** |
| ChurchInteriorBuilder | 있음 | CreateRoom 4면 | 박스 | **Kit 훅** |
| CraftHouseInteriorBuilder | 있음 | CreateRoom 4면 | 박스 | **Kit 훅** |
| RestaurantInteriorBuilder | 있음 | CreateRoom 4면 | 박스 | **Kit 훅** |
| TavernInteriorBuilder | 있음 | 자체 3면+무대 | 박스 | **Kit 훅** |
| BarnInteriorBuilder | **있음**(BuildCeiling) | 자체 3면(남=뒷벽)+문(+z) | 박스 | **Kit 훅** |
| ArenaInteriorBuilder(여관) | 없음(자체 파괴) | 남측 입구 구간+Wall_Back 파괴 | 부분 수조 | 무수정(이미 정합 — 중복 훅 금지) |
| ArenaPitInteriorBuilder | — | 관전 ortho(0,26,0) | 관전 특수 | 무수정(제외) |
| AfterlifeInteriorBuilder | — | 웨이브 전투 | **제외(사용자 확정)** | 무수정 |
| CaveInteriorBuilder | — | 던전 | **제외(사용자 확정)** | 무수정 |

## Phase

- **P0** 본 문서 — 기준정의서+빌더 준수표 (사용자 승인: 확정 질의 3건 답변으로 대체)
- **P1** `Systems/IndoorDioramaKit.cs` 신설 — Apply(room, cutSouthWalls) — 멱등+try-catch 무음 + `IndoorDioramaKitTests.cs` (행위 5건+소스계약 2건)
- **P2** 성내부 훅 — PlayerCastleInteriorBuilder 핵심 return 직전 1줄 (J2 잠긴 4문·토폴로지 무수정)
- **P3** 소형 실내 훅 — house/shop/church/crafthouse/restaurant/tavern return 직전 1줄 + barn SetupLighting 후 1줄
- **P4** 검증 게이트 — 컴파일 CS=0 → EditMode 전수(baseline 1:1) → 투어 프로브 재실행(14엔트리) → 몽타주 재촬영 사용자 판정
- **P5** 후보(판정 후) — void 배경 카메라 clear색 · 남측 절단 폭(size.z 비례) · tavern EnterBuilding case 추가(게임플로우 변경 — 별도 승인)

## 무손상 계약

- 던전(CaveInteriorBuilder/CaveLevelDirector) 전체 미적용 — 미니맵/포탈/층전환 무수정
- 미니맵(146차): CaptureFor는 렌더러+콜라이더 전수 AABB — 콜라이더 유지로 bounds 불변, 평면도는 남벽 없는 수조와 자연 정합
- IndoorCameraFollow(65°/6~20m) 무수정 · 실내조명 Flat 강제 계약 유지 · Figma UI(144차) 무수정
- 토폴로지/포탈 그래프/잠긴 문(J2) 무수정 — 시각 레이어만 변경
- Barn 짚더미 Random.Range 회전(설계상 허용 3종 예외 — 9/21 Q&A 기록) 무수정
