# 계획 v2 (2026-10-11): LivelyHood 생활기술 통합 검증 씬 + 실내 조명(예시 정합) + HUD 표시 + 시간 UI + 제작소 목재 공개

> 상태: 계획 전용 (구현 미착수 — 사용자 "진행" 명시 시만 실행)
> 사용자 결정 반영(v2): ①제작소 = **A안 확정**(게임 시작 시 기본 발견) ②실내 밝기 = **"실내씬 예시.PNG 정도"** 정합 ③호수 = **원판 확대** ④Test_15에서 **불필요한 장비·몬스터·병사 제거** ⑤신규: **HUD가 Test_10에서도 미표시** · **시간 UI 시간 미흐름** — 판정·수리 추가
> 선행 검증(읽기 전용 완료): 아래 "사전 확인 결과".

---

## 사전 확인 결과 (소스 실측 — 라이브 트리 기준)

1. **제작소 나무 조합: DB에는 전부 있음. 안 보이는 원인 = Milestone D "?" 블라인드**
   - `WeaponCraftDatabase.cs`: dagger/sword/spear/bow_wood(목재+엄니/토끼털) · tool_axe/pickaxe/shovel(목재2+돌1) · fishing_rod(목재2+토끼털1) · 석재/철 티어 목재 포함 — 24차 반영 완료.
   - `WeaponForgeUTK` → `WeaponCraftDatabase.All` 직접 읽음(배선 정상). `IsDiscovered()` → `RecipeDiscoverySystem.IsDiscovered(결과명)` false면 "?" 블라인드(성공 제작 시 MarkDiscovered 공개).
2. **HUD 시계방향 소모: 배선 완료(HUDUTK:187 DrainClockwise=true · 폴링 Fraction 갱신 · ArcStartDegrees) — Play 미판정.** Test_10 HUD 미표시는 별도 문제(아래 6).
3. **벌목 나무: 코드·배선·GLB 모두 정상** (`NaturalResourceSpawner` — "TerrainOnly"만 스킵, `GameSetup.BootstrapTerrainDeco` 멱등 호출, GLB 실존 확인). 원인 판정은 Editor.log `🌳 벌목 나무(Wood) N/200` 로그로.
4. **Test_15_Livelihood 현 구성**: 낚시 ✅(Water 원판 10m @ (0,-22)+FishingSystem+낚시대) / 채광 ✅(프리미티브 큐브) / 채집 ✅(허브) / 농경 ✅(2x2 밭). 없는 것: 실제 나무·돌 GLB, 과일나무, 넓은 호수.
   - **제거 대상 확인**: `Awake`가 모든 테스트 씬에 `SetupTestDummies()`(병사 1+몬스터 1, :59)와 `SetupUITestArena` 내 `SeedPlayerInventory()`(검·가죽갑옷·곡괭이·고기·허브·Gold 등 시딩, :2011~)를 무조건 실행 — Livelihood엔 불필요.
5. **실내 조명 현행(19차 계약)**: IndoorScene 화광 3.0×3 · Sky 0.22/0.15/0.1 / 성내부 빌더 ×1.0 계열 / 상한 계약 1.30·1.25. 예시 이미지 실측값(스킬 기록): **mean 37 / median 34 / p75 50 / p90 75**.
6. **Test_10 HUD 미표시**: `HUDUTK.Bootstrap`은 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` **씬 무관 자가부트** + `SetupUITestArena`가 UTK 루트 명시 준비 — 코드상 부트는 보장. 원인 후보: (a) 20차 `PositionHost()` 복원 **이전** 판정이었다 (b) UIRoot/PanelSettings 부재로 Ensure 실패 (c) 오프스크린(부트는 되나 위치 산출 실패). → 로그로 3분지 판별.
7. **시간 UI 정지**: `TimeClockGlassUTK`는 300ms 폴링 `TimeManager.GetOrCreate().GetFormattedTime()`(10-10 수리로 씬 무관 생성) / `TimeManager` `_timeScale=60`(현실 1초=게임 1분) Update에서 `GameTime += deltaTime×scale` — 코드상 흐름 보장. 원인 후보: (a) TimeClock 창 자체 미표시/미폴링 (b) TimeManager가 죽은 인스턴스 재생성 반복 (c) 창은 뜨는데 폴링 schedule 미동작. → 진단 로그로 판별.

---

## P1 — Test_15_Livelihood 통합 확장 + 슬림화 (핵심)

파일: `TestTerritoryCombatSetup.cs`, `NaturalResourceSpawner.cs`

1. **슬림화**: `SetupTestDummies()`(병사+몬스터)와 `SeedPlayerInventory()`(검/갑옷/곡괭이/고기/Gold 시딩)를 Livelihood 씬에서 **스킵** (`isLivelihoodScene` 게이트 — Awake 초반 판정 변수 재사용). 스타터 낚시대 1개만 지급(기존 SetupFishingArea 유지).
2. **실제 나무+돌**: `NaturalResourceSpawner.EnsureNaturalResources` 테스트 오버로드 추가 — `(parent, woodTarget, rockTarget, attemptsScale, minDistOverride)`. Test_15에서 나무 ~20 + 돌 ~20 (minDist 축소). 실제 GLB + `ResourceNode(Wood/Stone)` → E 채널(파랑 진행바)·나무 스웨이·도끼 커서·소멸→리스폰 메인씬 동일 경로. 기존 무인자 시그니처는 기본값 위임(메인씬 영향 0).
3. **과일나무 2~4그루**(Fruit_Apple) → 사과 수확.
4. **호수 = 원판 확대**(사용자 결정): `Livelihood_Pond` 반경 5m → **12m**(scale 10→24), 위치 유지(0,-22) 또는 지형 여유 시 (0,-30). Water 태그·Raycast 감지·E 프롬프트 유지. LakeGenerator 미사용.
5. 기존 4활동 배선(낚시대·허브·밭) 유지 — 회귀 금지.

## P2 — 실내 조명: "실내씬 예시.PNG" luminance 정합

파일: `IndoorScene.unity`, `CastleInteriorBuilder.cs`, `PlayerCastleInteriorBuilder.cs` + 계약 4파일

1. **목표 고정**: 예시 이미지 실측 mean 37 / median 34 / p75 50 / p90 75 — "어두운 무드 + 중간톤 존재". 무한 밝힘 금지(예시 초과 시 되돌림).
2. **방법**: ①현재 IndoorScene 캡처 1장 확보(사용자 캡처 또는 배치 캡처) → PIL luminance 실측 ②현재 mean을 예시 mean(37) 대비 비율 계산 ③그 비율을 광원에 일괄 적용(화광·앰비언트·빌더 배율 동률) ④계약 4파일 동기: `IndoorAeroFogTests`(새 값 단정 + 이전값 stale-금지 + **메서드명 값 동기**) / `TimberFrameTests`·`WarehouseStorageFigmaParityTests` 상한(신규 최대×1.15 여유) / `CollisionAndKitchenTests` 입구측 상한.
3. 캡처 확보가 어려우면 우선 19차 대비 ×2(화광 6.0, 삼분광·빌더 배율 ×2) 적용 후 사용자 캡처 1장으로 ②③ 반복 1회 — 소폭 계단 금지 원칙 유지.
4. 유지 규약 불변: 직사광 0(소등)·AERO 포그 0.12·천장 요소 금지·물리 브레이저 금지·Soft 섀도우 금지.
5. ⚠ 2장 협조: 문 진입(IndoorScene)·성내부(PlayerCastle) 양쪽 동시 갱신 — 한쪽만 바꾸면 "변화 없다" 오판.

## P3 — 제작소 목재 레시피 기본 발견 (A안 확정)

파일: `RecipeDiscoverySystem.cs`(초기 discovered 등록), 필요 시 `WeaponForgeUTK.cs`

- 게임 시작 시점(RecipeDiscoverySystem 초기화)에 **목재티어 무기 4종 + 공구 3종(axe/pickaxe/shovel) + fishing_rod** 결과명을 기본 discovered 등록. `DisplayName` 경로 정합 확인(블라인드 판정 키 = 결과명 문자열).
- 석재·철·크리스탈 티어·방어구는 기존 발견 시스템 유지(블라인드 규칙 불변).
- EditMode: 블라인드 판정 테스트가 있다면 A안 기준 갱신.

## P4 — HUD 테스트씬 미표시 판정·수리 (Test_10·15 공통)

1. Editor.log에서 최근 Test_10/15 실행분 grep: `[FigmaV3] HUD gaugeHost` 첫 로그 유무 + `[HUDUTK] 진단` hpFraction/attached 값.
2. 판별: 로그 0건 = 부트 누락(UIRoot/Ensure 실패 — SetupUITestArena 순서·UIToolkitBootstrap 확인) → 로그 있고 attached=True인데 미표시 = 위치/렌더(가제이지Host 좌표·scale·panel 높이) 재판정.
3. 수리 후: Test_10·15·메인씬 3곳 모두 "하단 중앙 하트(빨강)+번개(노랑) 게이지" 표시 확인. 시계방향 소모는 20차 수리분 포함 피격 판정.

## P5 — 시간 UI 정지 판정·수리

1. `TimeClockGlassUTK` 폴링 진단 로그 1회성 추가(또는 Editor.log 기존 로그 확인): GetFormattedTime 반환값이 300ms마다 변하는지.
2. 판별: 값 고정 = TimeManager 미진행(GetOrCreate 인스턴스 파훼/재생성 반복·timeScale 0·Update 미호출 확인) / 값은 흐르는데 화면 고정 = 창 렌더·폴링 schedule 문제.
3. 수리 후 Test_10·15·메인씬에서 시계가 실시간(현실 1초=게임 1분)으로 흐르는지 확인. 침대 세이브 수면 스킵(`Sleep`×60 배속) 연동은 기존 규약 유지.

## P6 — 검증 게이트 (순서 고정)

1. Compile: 신규 `error CS` 0.
2. EditMode 전체: 계약 갱신 반영 후 baseline(624 중 617/7) 대조 — 신규 회귀 0.
3. **Play — Test_15_Livelihood 통합 판정**: ①호수 물가 E→낚시 ②벌목: 도끼 커서→파랑 진행바+나무 흔들림→통나무 드랍→소멸→리스폰 ③돌 채광 동일 채널 ④허브 채집 ⑤밭 파종→성장→수확 ⑥씬에 병사·몬스터·불필요 장비 없음 확인 ⑦HUD 게이지 표시+피격 시계방향 소모 ⑧시계 흐름 ⑨제작소에서 목재 조합 보임(P3).
4. **Play — Test_10**: HUD 표시(P4) + 시간 흐름(P5)만 보조 판정.
5. P6-3 통과 후 메인씬 확인 가능 시점: Editor.log 🌳 로그 대조로 메인씬 나무 원인 최종 판정(테스트씬 통과 = 시스템 정상 → 메인씬은 배치/환경 분리).

## P7 — 커밋

- 명시 요청 시에만 stage/commit/push. 현재 미커밋 47파일(+2,217/−460: 23·22-2·24차)과 함께 정리할지 사용자 지정 대기.

---

## 검증 상태 라벨
- 소스 검증: 위 사전 확인 1~7 전부 — 라이브 트리 기준(이번 턴).
- Compile/EditMode/Play: 미실행 — P6 게이트에서 수행.
- 사용자 보고(실내 어두움·HUD 소모·나무 미표시·Test_10 HUD·시간 정지) 전부 Play 판정 전 — 구현 유실은 소스로 없음 확인, 다만 Test_10 HUD·시간은 20차 수리 이후 미재검증으로 원인 3분지.
