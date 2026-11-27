# 207차 계획 — 말 누움·화살 조준 카메라·활 그립·폭탄 착지 범위 (2026-11-27)

> 사용자 지시(11-27 오전 Play 후 4건): ①말 여전히 찌그러짐(영상 판독 결과 **옆으로 누움**) ②화살 조준 방향 불일치 잔존 ③활쏘기 시 활이 바닥에 쳐짐(애니 미교체 의심 → **기각, 활 메시 크기 근원**) ④폭탄 호만 그려지고 착지 범위 미표출 + 호/착지 플루언트 고급화.
> **계획만 — 구현은 "진행" 승인 후.** Play·커밋은 별도 게이트.

## 영상·로그 판정 근거 (2026-11-27 10:13 세션 — Test_10_TerritoryCombat)

- **말**: 말 테스트.mp4 프레임 판독 — 말이 **옆으로 완전히 누운 상태**(머리 왼쪽·다리 아래, "카펫" 형태). 스케일이 아니라 **회전** 문제. 로그: `[Horse][195차 P5] 크기 정규화 — 원본 수평차원 1.09m → 균일 스케일 ×2.11 (목표 체장 2.3m) [206차]` 정상 실행 → 195~206차 스케일 계열 수리는 전부 오폭이었던 재판정. 백마/적토마 GLB 루트 노드 회전=항등원 직접 파싱 확인(정적 자체는 무죄 후보) → **애니 클립(갈색말 대기) 루트 회전 vs GLB 정적 자세 2분류, 프로브로 확정 필요**.
- **화살**: `[BowAim] screen=(697,300) camera=Main Camera camId=-2578 rayO=(0.03,-0.08,0.06) rayD=(0.31,-0.76,0.58) hit=Ground point=(0.09,-0.22,0.16) muzzle=(9.94,1.94,-8.76)` ×20건 전부 동일. **조준 레이 원점이 세션 내내 월드 원점 고정** — 화살은 muzzle→원점 방향으로 발사(dir=(-0.73,-0.16,0.66) = muzzle→원점 벡터, 발사 수학 무죄 재확인). 반면 `[FollowProbe#1-4] camPos=(0.3,11.5,-6.0)→(5.0,11.2,-9.4)` — 렌더 팔로우 카메라는 정상 추적. 즉 **조준에 쓰인 카메라 인스턴스(-2578)가 렌더 카메라와 별개·원점 매몰**. 로그 2087행 `[TestTerritoryCombat] 씬에 카메라 부재 — 원점에 새 Main Camera 생성` 확인(씬 파일 카메라 0건 실측). P206의 FollowCamera 1순위 로직(PlayerCombat.cs:270-292)은 착지돼 있으나 그 카메라가 원점 인스턴스를 반환 — FollowCamera 스태틱(PlayerMovement.cs:62)이 원점 카메라를 물고 있거나 폴백(Camera.main)이 이김. 정확한 갈라짐 경로는 P0 프로브로 특정.
- **활**: 애니 무죄 입증 — ArcheryShot·DrawShoot 두 상태 모두 디스크상 플레이어 활쏘기.fbx(772a9018) 배선(Player_AC.controller 실측) + 런타임 트리거 실발화 로그 8건+. 근원=**활 GLB 크기·그립 기하**: wood_bow.glb 장축 1.9m(피벗=중앙) × 플레이어 바디 배율 1.56 = **월드 2.96m** 렌더(Editor.log:13274 실측), DATA 그립 모드(GripStrategy=3, WeaponEquipManager.cs:75)는 스케일 정규화 생략 → 중앙 그립(gripT=0.5)이라 하단 1.48m가 손 아래로 → **팁 월드 y=-0.20m(지면 아래)**(:13243 직후 진단). 추가로 '월드 상방' 정렬이 장착 시 1회 베이크라 애니 포즈를 따라가지 못함. 병사 선례(GuardVisualAttachSystem 활 0.75×신장 정규화)와 체계 불일치.
- **폭탄**: 착지 원형은 무번호 181에서 이미 구현(Bomb.cs:42-54 — SpawnCircle(explosionRadius, 폭탄 추적 모드))이나 **y=0.02 하드코딩** → Test_10(지면 y≈0)에선 보이지만 MainScene(지형 y≈42)에서는 **지하 42m에 렌더 = 사용자 시야에 없음**이 유력 근원. 호=BombThrower LineRenderer(cyan 단색, 30점·0.1s) — 플루언트 요소 없음.

## Phase 0 — 진단 프로브 2종 (읽기전용 에디터 프로브, 배치 1회)

- **P0-a 카메라 전수 덤프**: Test_10 배치 실행 1회 — 씬 내 모든 Camera(name/instanceID/position/enabled/tag) + `PlayerMovement._camera·_cameraTransform·FollowCamera` instanceID 덤프 + `[BowAim]` 로그에 camPos(카메라 transform.position) 추가. **목적: 원점 카메라 -2578의 생성 주체와 FollowCamera 갈라짐 경로 확정** (DDOL 잔존 카메라 vs SetupCamera 생성분 vs PlayerMovement 폴백 재바인딩 3후보 판별).
- **P0-b 말 자세 분리 프로브**: 백마 스폰 2종(Animator 부착 유/무) 스크린샷 캡처 + 각각 bounds·루트 본 월드 회전 실측 + 갈색말 대기 클립 0초/0.5초 샘플링 루트 본 회전값 산출. **목적: 누움이 GLB 정적 자세인지 클립 구동인지 1캡처로 판정**.
- 산출물: TestOutput/no207_camdump.txt·no207_horse_a.png·no207_horse_b.png. 수정 0건.

## Phase 1 — 화살 조준 카메라 단일화 (P0-a 결과 반영)

- 파일: PlayerMovement.cs(카메라 채택/할당 지점), TestTerritoryCombatSetup.cs(SetupCamera :1625-1650), PlayerCombat.cs(ResolveBowAimCamera :270-292).
- 방향: ①FollowCamera를 **ApplyFollowCamera가 실제 매 프레임 이동시키는 인스턴스와 동일 보장**(할당 경로 단일화 — Awake 채택과 ApplyFollowCamera 참조가 같은 인스턴스임을 계약으로 고정) ②원점 매몰 카메라 생성 분기 제거 또는 생성 시 태그 미부여/즉시 흡수 ③ResolveBowAimCamera에 유효성 가드 추가 — 후보 카메라가 플레이어와 비정상적으로 멀거나(팔로우 거리 상한 초과) 레이가 셔터 근방(<1m)에서 맞히면 조용히 발사 취소(엉터리 원점 조점 금지) ④[BowAim] 로그에 camPos 추가해 향후 재발 즉시 판별.
- 검증: 신규 계약(카메라 단일소스·가드 순서) + BowAimAlignmentTests 핀 갱신 + Play에서 `[BowAim] rayO == 팔로우 camPos` 일치 로그 실측.

## Phase 2 — 말 누움 수리 (P0-b 판정 분기)

- 파일: HorseMountSystem.cs(SpawnHorse :149·NormalizeHorseScale :184·TestSceneSpawn :133).
- 분기 (a) **GLB 정적 누움** → 스폰 시 루트 보정(경계: bounds 정규화와 곱셈 순서) 또는 임포트 후처리.
- 분기 (b) **클립이 눕힘** → 갈색말 대기(및 걷기) 클립의 루트 회전 커브 제거(에디터 원샷) 또는 Animator 트랜스폰스 보정. 3품종(백마/적토마/황마) 동일 적용.
- 기존 핀 유지: 195차 체장 2.3m 정규화·206차 균일 스케일·204차 Test_10 스폰 훅 무손상. 길들이기→탑승 체인도 누움 해소 후 재검증(10:13 세션에서 길들이기 2회 실패는 누운 말 탓 가능성).
- 검증: HorseTestSceneSpawn204Tests·HorseSpawnSceneGate195Tests 무손상 + 신규 계약(루트 회전 상태) + Play 스크린샷.

## Phase 3 — 활 크기·그립 수리 (애니 무관 확정)

- 파일: WeaponEquipManager.cs(DATA 그립 ComputeWeaponDataGrip :548-640·GripPose 테이블 :75).
- 방향: ①GripStrategy=3 경로에 **스케일 정규화 추가** — 병사 선례 준용(활=0.75×신장, 플레이어 1.8m 기준 ≈1.35m → 월드 2.96m→1.35m) ②중앙 그립(gripT=0.5) 유지 — 하단 리브 침투 = 0.68m로 축소, 손 아래 지면 침투 해소 ③방향: '월드 상방 1회 베이크' → LeftHand 로컬 기준 고정 방향으로 정책 변경(드로우 포즈 추종) ④HumanoidClipDriver.cs:409 stale 주석('Draw_and_Shoot_from_Back_1') 정정.
- 검증: 신규 계약(팁 월드 y>0·스케일 밴드) + 그립 실측 로그([Weapon] 그립 실측 bounds 재확인) + Play 체감.

## Phase 4 — 폭탄 착지 범위 가시화 + 호/착지 플루언트 고급화

- 파일: Bomb.cs(:42-54)·BombThrower.cs(트레젝토리 :24-40)·TelegraphArcIndicator.cs(SpawnCircle :102).
- P4-1 **착지 원형 y 근원 수리**: y=0.02 하드코딩 → 폭탄 x/z의 지면 높이 쿼리(TerrainGenerator.GetHeightAt + 지면 레이캐스트 폴백)로 지면 부착 — MainScene(y≈42)·Test_10(y≈0) 양쪽 표출.
- P4-2 **호 플루언트**: LineRenderer 그라디언트(발사점 투명→착지점 진함)·흐르는 대시/점 애니메이션(텍스처 스크롤 or 정점 알파 펄스)·착지 예상점 마커. 절차 파티클 금지 계약(BombVfxTests) grep 확인 후 설계.
- P4-3 **착지 범위 플루언트**: 외곽 링 펄스(확장 수축 사이클)·내부 채움 알파 페이드·폭발 순간 수렴 플래시. 기존 착지 빨간 링 유지 계약(193차 사용자 결정)과 정합.
- 검증: 기존 폭탄 핀(189·193·195·206차) 전부 무손상 + 신규 계약(y 쿼리·펄스 주기) + Play 체감(강도 트림 후보 상수 보고).

## 공통 검증 게이트

- 컴파일 error CS 0 · 전수 EditMode baseline 1:1(diff 0) · 신규 계약 GREEN · Play 미실행(사용자 게이트).

## Play 확인 목록 (승인 후)

①말이 서서 스폰·길들이기→탑승 체인 ②활 쏘기 시 활이 손에 정위치(바닥 긁힘 소멸) ③화살이 리티클 조준점에 착탄 ④폭탄 던지면 호+착지 범위 원형 동시 표출(MainScene 기준) ⑤호/착지 연출 강도 체감(트림 후보: 그라디언트 알파·펄스 주기·대시 속도).

## 비고

- 오늘 아침 텔레그램 세션의 무번호 라운드(P206 — 활 드로 교체·카메라 단일소스 1차·말 균일 스케일 복귀·폭탄 트레일 강화·창 콤보 속도)가 QAPROGRESS에 미기입 — **206차로 백필 기입 여부 사용자 결정 대기**. 이 계획의 차수는 207차로 가정.
- 10:13 세션에서 길들이기 2회 실패 로그 확인 — 누움 해소 후 재시도 필요.
