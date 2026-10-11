# 전투·궁술 조준·패링·AERO 연속 가스·Figma 월드 링/HUD 개선 계획

> 계획 전용 문서. 사용자 요청에 따라 현재 단계에서는 계획만 작성한다. 게임 소스, 씬, 에셋은 수정하지 않는다.

**목표:** 활 조준 오류와 노란 선, 검 클릭당 중복 공격, 작동하지 않는 패링을 고치고, AERO volumetric fog로 고품질 연속 가스 분사를 구현한다. G키는 첫 입력으로 지속 분사를 켜고 다음 입력으로 끄며, 분사 중 포션 잔량 UTK를 표시한다. Figma 설계에 맞춰 병사 선택에는 파란 발밑 링, 공격 지정 몬스터에는 빨간 발밑 링을 표시한다.

**접근:** 입력 producer를 단일화하고 테스트로 재현한다. 조준선/UTK 포인터/muzzle/projectile은 단일 조준 계산을 공유한다. AERO를 기본 시각효과 기술로 우선 사용하고 GPU 사양을 목표 품질의 제한으로 두지 않는다. 화면 전체 RendererFeature를 사용할 경우에도 가스가 있는 위치만 밀도를 갖도록 world-space density mask를 설계한다. Figma의 두 UI를 기존 입력/시스템 이벤트에 연결한다. Play 검증 전 완료로 선언하지 않는다.

**현 상태 근거:** `PlayerCombat.OnGUI()`가 수평 `transform.forward` 노란 선을 직접 렌더하고, 활 발사는 별도 마우스 ray와 전방 muzzle offset을 쓴다. Test_10은 `PlayerCombat`와 `AttackSystem`이 함께 좌클릭을 감지한다. 패링은 좌클릭 공격 큐와 결합돼 있다. 가스 시스템에는 서로 다른 두 G 토글 입력자가 있다: GameSetup이 `GasSprayer`와 `SprayInputHandler`를 둘 다 부착하고, `GasSprayer.Update()`와 `SprayInputHandler.Update()`가 각각 G 입력을 처리한다. AERO 패키지는 URP FullScreenPassRendererFeature/Material 적용을 안내하지만 화면 전체 패스를 사용한다. 기존 Figma source는 현재 Unity 저장소 안에서 확인되지 않았으므로 실제 frame/export가 접근 가능한 프로젝트 소스에서 확인될 때 디자인대로 매핑한다.

**엔진/사용자 방향:** Unity 6000.4.10f1, URP 17.4, UI Toolkit, C#. 메인씬은 별도의 데스크톱에서 실행될 예정이므로 GPU/기기 사양을 품질 제한으로 삼지 않는다. Unity Editor Play는 사용자 환경에서 검증해야 한다. 사용자 소유의 미추적 자료/에셋은 보존하며 일괄 add/정리하지 않는다.

---

## Phase 0 — 증거 및 기준선 고정 (읽기 전용)

1. `PlayerCombat.cs`, `AttackSystem.cs`, `HumanoidClipDriver.cs`, `TestPlayerAnimatorBoot.cs`, `TestTerritoryCombatSetup.cs`의 실제 씬 배선을 대조한다. Test_10과 MainScene에서 활성 플레이어 오브젝트의 `PlayerCombat`, `AttackSystem`, 애니 드라이버 개수를 기록한다.
2. Animator Controller의 각 사용 상태와 motion/전이, `_comboStage` 직접 상태 재생 및 `LastAttackTime` 폴링을 추적한다. 별도 확인 전까지 문서의 과거 콤보 모델을 현재 동작으로 가정하지 않는다.
3. 기존 Play 재현 증거가 있으면 사용하되, 없다면 재현표를 준비한다: 검 1/2/3 클릭별 타격/HP 변화, 활 중앙·가장자리·고저 표적, 패링 성공/실패, 가스 분사 화면과 FPS.
4. 사용자 미추적 스크린샷·영상·로그·임포트 에셋을 보존한다. 컴파일·EditMode 산출물은 생성 시 git 추적 대상에 넣지 않는다.

**완료 기준:** 단일 입력 소유자 후보와 런타임 재현 경로가 확정되고, 미확정 사항은 명시되어 있다.

## Phase 1 — Test_10 및 MainScene 검 재공격 제거

1. `PlayerCombat.Update()`의 좌클릭 경로와 `AttackSystem.Update()`의 `Input.GetKeyDown(Mouse0)` 경로가 같은 클릭에 모두 반응하는지 검증하는 EditMode/PlayMode 가능한 회귀 테스트를 먼저 추가한다.
2. 한 클릭에 공격 입력 ID가 하나만 생성되고, 타깃에 피해 판정이 한 번만 전달되는지 검사한다. 시각 애니메이션 두 번과 실제 HP 감소 두 번을 별개 관측값으로 기록한다.
3. 플레이어 전투의 단일 권위 입력 경로를 정한다. 기존 프로젝트 계약상 `PlayerCombat`이 combo/weapon 분기와 데미지 판정 소유자가 되도록 하고, Test_10의 `AttackSystem`은 중복 좌클릭 공격 입력을 소비하지 않게 하거나 테스트 전용 비공격 기능으로 제한한다. MainScene/Test_10이 동일한 공격 입력 파이프라인을 사용하도록 배선을 정리한다.
4. 일반 검의 수락된 클릭 1회가 공격 이벤트/피해 1회만 만들도록 한다. 애니 드라이버, combo stage hit, PlayerCombat fallback, legacy/procedural animator가 동시에 같은 타격을 만들어내지 않도록 타격 판정 소유자를 단일화한다.
5. 콤보 규칙을 acceptance test로 고정한다: 빠른 1회 클릭=1 스윙/최대 1회 타격, 두 번의 개별 클릭=두 단계/최대 2회 타격, 세 번의 클릭=세 단계(기존 사용자 요청 범위). 쿨다운·입력 버퍼 경계에 따른 클릭 누락/합치기도 테스트한다.

**검증:** 관련 EditMode 테스트 RED→GREEN, 전체 EditMode 결과 XML의 총계와 실패 사례 기록, Unity 컴파일 성공. Test_10과 MainScene Play에서 1/2/3 클릭 타수·애니메이션·표적 HP를 각각 확인한다.

## Phase 2 — 활 조준선과 UTK 커서 방향을 하나로 통합

1. `PlayerCombat.OnGUI()`에서 그리는 기존 노란 IMGUI 선을 제거한다. `BowTrajectoryPreview`의 host 정리 코드는 과거 월드 궤적 host에 한정되므로, 그것을 노란 선 제거 완료의 근거로 쓰지 않는다.
2. UTK `BowAimReticleUTK`의 실제 중앙점을 화면 좌표로 정규화해 단일 `BowAimSolution` 입력으로 사용한다. UI Toolkit panel scale/DPI 변환을 역변환해, 발사 계산이 조준점과 같은 화면 픽셀을 읽는지 단위 테스트/로그로 확인한다.
3. 단일 조준 계산 함수가 `ScreenPointToRay`를 만들고, 발사 원점(활/muzzle)에서 카메라 ray의 대상점으로 향하는 방향을 산출한다. 카메라-ray 원점과 muzzle 위치가 달라 생기는 parallax를 방치하지 않는다. 월드 충돌/지형을 맞추면 해당 hit point를 사용하고, 미적중이면 일관된 유한 조준 거리 plane을 사용한다. 수직 발사 clamp가 있다면 시각 조준점과 발사 방향 모두에 같은 규칙을 적용한다.
4. 플레이어 yaw, muzzle offset, projectile initial velocity, 발사 로그가 모두 동일한 `BowAimSolution`을 사용하도록 연결한다. 화살은 UI 커서가 가리킨 위치를 향해 활 muzzle에서 출발해야 한다. 중력 때문에 먼 거리에서 착탄이 떨어지는 현상은 초기 조준 벡터 오차와 분리해 측정한다.
5. 활 조준 UI는 화면 커서를 가리키는 작은 dot/브래킷으로 유지하되, 월드 탄도선을 새로 만들지 않는다. 노란 선은 드로/릴리즈/탭 취소/씬 전환을 거쳐도 0개여야 한다.
6. “사거리 확장”은 코드 숫자만으로 완료 처리하지 않는다. 현재 발사 속도/중력/수명/화살 파워와 실제 충돌을 baseline으로 측정하고, 최소·최대 드로 파워의 평지 도달거리 목표를 명시한다. `ArrowProjectile`의 공통 물리 상수를 발사/사거리 측정에 일관되게 쓰고, 충분한 수명 또는 거리 기반 소멸을 보장한다.

**검증:** 카메라 ray/UTK 화면점/muzzle 방향 사이 단위 검증, Test_10 가까운·먼·고저차 표적을 각각 중앙 및 화면 가장자리에서 쏴 조준 편차 기록, 화살 충돌/지형 관통/수명 정리 확인. MainScene에서 회귀 확인.

## Phase 3 — 패링을 공격 입력과 분리하고 end-to-end 구현

1. 현재 좌클릭은 `_parryActive` 창을 열면서 첫 타를 `_queuedWeaponAttacks`에 넣는다. 이 동작을 명시적으로 변경해 **공격 입력과 패링 동작이 서로 다른 액션**이 되게 한다. 일반 좌클릭은 근접 공격만 처리하고, 패링은 별도의 remappable action으로 만든다. 키 충돌 조사 후 기존 입력 설정 체계에 등록한다.
2. 테스트 우선 작성: 패링 입력 시 Parry 활성화/애니 요청, 공격 입력은 공격만 수행, 패링 중 유효 피해 한 번 흡수, 창 밖 피해는 정상 적용, 시스템/상태 피해는 기존 제외 정책 유지, 성공 1회만 발화한다.
3. `PlayerCombat` 패링 입력→`HumanoidClipDriver.TriggerParry()`→실제 Player_AC Parry state/clip→`PlayerHealth.TakeDamage()`의 typed/untyped 경로→`TryParry()` 호출이 전부 이어지는지 검사한다. 플레이어 루트/자식 animator 위치 차이와 `PlayerHealth`의 같은 오브젝트 우선 탐색을 검증한다.
4. Parry 클립 재생 동안 정상 공격 트리거가 덮어쓰지 않게 하고, 성공/실패/창 만료/피격 전/입력 연타/씬 전환 후 입력 상태와 애니메이터 속도를 원래대로 복원한다.
5. Parry 성공 VFX/슬로우는 판정 이후에만 발동한다. EditMode의 `DestroyImmediate` 관련 로그 기대값은 정리하되, 내부 플래그 강제 설정 테스트만으로 실제 입력을 검증했다고 기록하지 않는다.

**검증:** 해당 EditMode 테스트, 컴파일, 이후 Test_10/MainScene Play에서 입력→실제 피격→피해 흡수→애니메이션/VFX/속도복원 확인.

## Phase 4 — G키 연속 분사 + AERO 고품질 월드-스페이스 가스

### 분사 입력/상태

현재 `GameSetup.EnsureGasSystem()`이 `GasSprayerController`, `GasSprayer`, `SprayInputHandler`를 같은 Player에 부착한다. `GasSprayer.Update()`와 `SprayInputHandler.Update()`가 둘 다 G키를 읽어 각자 `StartSpray/StopSpray` 토글을 시도한다. 이중 입력 consumer를 하나로 정리한다.

1. `GasSprayerController.IsSpraying`를 유일한 분사 상태 원천으로 유지한다. G pressed edge에서 장착/가스 잔량/재장전 조건을 확인한 후 `StartSpray` 또는 `StopSpray`를 딱 한 번 호출한다. 첫 G 입력은 계속 분사 상태로 전환하고, 다음 G 입력은 정지한다. G를 누르고 있는 동안 매 프레임 토글하거나 plume를 재시작하지 않는다.
2. G ON 동안 연속 emitter를 작동시킨다. 화면에는 짧은 plume/puff들이 연속으로 나오도록 각 visual puff lifetime을 짧게 두되 emission host는 G를 다시 눌러 끌 때까지 유지한다. G OFF 즉시 새 emission을 중단하고 이미 방출된 짧은 gas wisps만 자연스럽게 fade-out한다.
3. 물약은 장전 시 인벤토리에서 1개를 즉시 차감해 분사기에 현재 사용 중인 1회분으로 넣는다. 해당 dose는 G 분사 동안 시간에 따라 연속/점진적으로 소모되고, Potion Status UTK 게이지가 가득 찬 상태에서 서서히 0까지 내려간다. G를 잠시 끄면 그 dose의 잔량을 보존하고, 다시 켜면 남은 양부터 이어 쓴다. 게이지가 바닥나면 인벤토리에서 같은 종류의 물약 1개를 자동으로 꺼내 다음 dose로 교체하고 게이지를 다시 채운다. 이 자동 교체 때도 인벤토리 수량이 정확히 1 감소한다. 사용 가능한 물약이 없으면 포션 효과/분사를 안전하게 멈추고 UI에 소진 상태를 표시한다(가스통의 별도 연료 잔량 규칙과 분리한다).
4. `CurrentSprayTimeRemaining` 가스통 연료는 별도 자원으로 계속 줄어든다. 물약 dose timer와 가스 연료 timer를 하나의 게이지/카운터로 합치지 않는다. 재장전/연료 소진/장비 해제/씬 전환은 emitter와 loop audio를 정리한다.
5. 독/정신 효과는 연속 분사 중 적절한 별도 effect tick으로 적용해 plume emission cadence마다 과도 중복되지 않게 한다. 방독면 면역, 아군/적 분기, 기존 물약 분류는 회귀 테스트로 유지한다.

### AERO 적용 아키텍처

사용자는 메인씬을 다른 데스크톱에서 실행한다고 명시했다. 성능 예산은 제한조건으로 사용하지 않는다. AERO를 고품질 방향의 기본 렌더러로 적용한다. 고정된 한 장소에만 fog를 놓는 방식으로 한정하지 않고, player transform을 중심으로 움직이는 gas field volume/mask를 설계한다. 화면 전체 pass는 구름을 볼륨으로 렌더하는 수단이고, 위치 제한은 density function의 world-space mask가 담당해야 한다.

1. AERO `FullscreenPassRendererFeature` 및 `Volumetric Fog PC` material을 격리된 Test_10 렌더 설정에 먼저 연결한다. PC asset/renderer 교체나 main global renderer 수정은 스파이크에서 바로 적용하지 않는다.
2. HLSL density에 player/emitter 위치, world-space 반경, 높이/세로 두께, 연령/수명, potion type을 전달한다. density는 부드러운 radial falloff와 height mask, animated 3D/월드 노이즈를 결합해 지정한 가스 영역 밖은 0이 되도록 한다. 매 frame material을 공유 asset에 직접 덮어 써 다른 카메라/씬을 오염시키지 않게 per-camera/per-emitter material state를 둔다.
3. 연속 가스 토글 ON 동안 density volume은 플레이어/분사 emitter를 따라가고, emitter에서 짧게 보이는 AERO plume/noise streak를 방출한다. OFF 시 density를 빠르게 낮춰 volume을 제거하되, 이미 생성된 짧은 wisps만 짧은 fade-out을 갖는다. 구름이 플레이어 주위에만 균일한 구가 아니라, nozzle 방향/바람에 따라 국소 덩어리와 흐름이 있는 실루엣이 되도록 한다.
4. 재사용되는 `Volumetric Fog PC` asset을 직접 변형하지 말고 게임용 material/shadergraph/HLSL 인스턴스/복제본을 분리한다. AERO 기본 `GetVolumetricFogDensity(positionWS)`가 현재 상수 `1.0`/TODO 상태인지 기준 삼아, 새 density mask가 실제 renderer 경로에서 호출되는지 로그와 시각 검증을 추가한다.
5. AERO 기본 fog를 게임의 전역 world fog로 상시 켜는 것은 별도 요구로 취급한다. 이 작업은 G toggle로 구동되는 가스 volume만 제어하며, 분사하지 않을 때 MainScene 전체에 fog가 남으면 안 된다.
6. AERO full-screen pass가 URP 17.4에서 정확한 depth/color input으로 연결되는지, scene renderer의 기존 renderer features / camera stack에 끼치는 영향이 없는지 확인한다. 필요한 경우 RenderGraph 호환 adapter를 추가하되 기존 프로젝트 renderer asset을 파괴적으로 교체하지 않는다.

### Potion Status UTK (G 분사 중에만 표시)

이미 `GasSprayUTK`가 UTK로 존재하고 장전 potion ID/count 및 가스 잔량을 표시할 수 있다. 현재 static `Open/Toggle`이며 Main HUD/키 입력에서 `GasSprayUTK.Open()`으로 호출된다고 보장되지 않고, UI 본문에는 항상 visible한 계기판 성격이다. 기존 기능을 재사용하고 Figma source를 확인해 별도 compact potion HUD로 정리한다.

1. `GasSprayUTK`를 창 방식이 아니라 Figma 설계에 맞는 compact HUD widget/API로 연결하거나 기존 창을 적절히 축소한다. 새 UI 추가 전 기존 frame/data binding을 먼저 추적하고 동일 UI를 중복 생성하지 않는다.
2. `GasSprayerController.IsSpraying` true일 때만 표시하고 false/미장착/씬 전환 시 닫는다. G 첫 press 성공 시 UI를 열고 분사하며, 두 번째 press로 정지와 동시에 UI를 닫는다.
3. 분사기를 가스통에 넣어 장전하는 순간 포션은 인벤토리에서 1개 소모되어 사라진다. 분사기에는 그 1개가 현재 활성 dose로 장전되고, potion status UI는 이 dose 하나의 잔여량을 연속 게이지(예: 100%→0%)로 표시한다. 인벤토리 보유량이나 가스통 연료 게이지와 혼합하지 않는다.
4. dose 게이지가 0이 되는 순간, 인벤토리에서 같은 종류 포션 1개를 자동으로 꺼내 다음 dose로 교체한다. 이 때 인벤토리 수량이 즉시 1 감소하고 UI 게이지를 다시 채운다. 자동 교체 가능한 포션이 없으면 현재 dose 종료 후 분사를 멈추고 잔량 0/소진 상태를 표시한다.
5. 분사를 사용자가 일시 정지했을 때 현재 dose의 잔량을 보존하고 다음 G 시작 때 이어서 감소시킨다. AERO continuous density/plume duration과 potion dose duration은 하나의 timer로 오용하지 않는다.
6. UTK 요소는 GitHub-dark/Fluent 및 Figma frame 치수·위계·아이콘을 따르고, `GasSprayUI`의 IMGUI는 신규 작업에서 사용하지 않는다. 외형 이미지/아이콘이 필요하면 베이크 PNG, 셰이더 또는 디자인 source에서 export한 genuine alpha texture를 사용한다.

**검증:** 물약 장전 시 인벤토리 수량이 즉시 1 감소하고 현재 dose 게이지가 가득 참 → G 첫 press에서 연속 emission/UI 표시 및 dose 게이지가 시간에 따라 감소 → G로 일시 정지 후 재개 시 남은 게이지부터 이어짐 → 게이지 0에서 인벤토리의 동일 타입 물약 1개를 자동 소모·교체하고 게이지가 다시 참. 포션 재고가 없을 때는 분사 안전 정지. 가스통 연료 게이지와 포션 dose 게이지는 독립적으로 감소. G 두 번째 press 시 emission 정지와 UI 숨김, gas depletion/reload/equipment unequip에서도 안전 정리, 효과 tick 및 방독면 면역 유지. MainScene과 Test_10 모두 확인.

## Phase 5 — Figma 지정/공격 월드 링과 포션 UI 디자인 확인

### Blue friendly selection ring
1. 기존 `GuardSelectionManager`/`SelectionRingController`를 재사용한다. 병사 선택 직후 collider 없는 얇은 Fluent blue `#58A6FF` annulus가 발 밑 terrain 높이를 따라가며 보이도록 한다. 선택 해제/사망/씬 변경 즉시 정리한다. 기존 국가색 요구·selection box·Figma의 세부 ring design을 확인하고 링만 파란색이라는 사용자의 최신 결정이 우선한다.
2. 외형은 `SelectionRing.shader`/사용자 Figma artwork에 맞춘다. 내부가 채워진 원반으로 보이지 않게 inner alpha 0, 얇은 바깥 annulus, 낮은 glow/pulse를 유지한다. 라인/비대화 IMGUI 표시를 새로 추가하지 않는다.

### Red attack target ring
1. `PlayerCombat.CurrentTarget`는 플레이어의 자동조준 표적을, `GuardPlaceholder.IsAttackCommand/CommandTarget`는 병사의 RTS 공격 지시를 나타낸다. 사용자의 “공격지정시 몬스터 밑” 뜻과 현재 우클릭 병사 공격 target 경로를 그대로 조사해 어느 target selection event가 UI에서 소비될지 고정한다.
2. 공격 대상으로 지정된 살아있는 몬스터 밑에 colliderless red `#F85149` world ring을 표시하고, target 변경/공격 명령 취소/사망/거리 이탈/일시 명령 종료 시 링을 정확히 정리한다. 병사 파란 선택 링과 빨간 몬스터 공격 링은 각자 별도 lifecycle/source를 가지며 동일 대상 계층의 자식 collider가 아니라 `GetComponentInParent<AnimalAI>()`로 정식 몬스터 root를 찾는다.
3. 플레이어가 단순 hover하는 모든 몬스터를 attack-designated로 오인해 빨간 링을 표시하지 않는다. 실제 player attack target/selected guards' attack target event만 표시 대상으로 한다. 복수 병사가 한 몬스터를 지정하는 경우 링은 하나로 deduplicate하고 어떤 지시가 남아있는지 추적한다.
4. Figma “selection ring UI” frame을 찾아 색/두께/halo/애니메이션/표시 시점을 추출해 월드 ring 표현과 연결한다. 화면 overlay mockup을 월드 지면 decal로 잘못 해석하지 않도록 frame과 실제 플레이 context를 대조한다.

### Figma source asset / plan gate
- 이 검색에서는 `.fig/.figma` 파일이나 이름이 명확한 ring/potion frame asset을 프로젝트 트리에서 확인하지 못했다. 사용자 소유 Figma 링크가 현재 채팅에서 제공되면 asset/node tree를 직접 확인해 정확한 scale/layout/token을 반영한다. source가 없으면 명시된 기능/색/트리거는 계획 acceptance criteria로 고정하고, 임의로 frame pixel geometry를 지어내지 않는다.
- Selection UI와 Potion Status UI의 디자인 image/ring remains user-owned; Figma PNG export가 필요하면 genuine alpha와 Unity texture import setting/readability를 검증한다.

**검증:** 부대 선택/해제에서 파란 링 단일 표시, 실제 RTS attack issue에서 해당 몬스터에 빨간 링 단일 표시, 공격 취소/표적 사망 후 제거. G 분사 중 Potion HUD 개폐/잔량 추적. Figma 원본과 Unity Game view를 나란히 비교.

## Phase 6 — 통합 QA 및 문서 반영

1. 순서대로 Phase 1–5 구현/검증을 실행한다. 코드 수정은 회귀 테스트를 먼저 추가하고 RED→GREEN으로 한다.
2. 별도 QA agent가 변경 파일/호출 체인/회귀를 검토한다. Code agent와 QA agent 결과를 분리해 기록한다.
3. Unity compile, EditMode XML 총계/실패, Test_10 Play, MainScene Play, AERO URP renderer/runtime, Figma screenshot checks를 구분해 기록한다. 수행하지 않은 검증은 pending으로 유지한다.
4. 최종 결과에 맞춰 `QAPROGRESS.md`, `ROADMAP.md`, `PROGRESS.md`/`CYCLE.md`를 갱신한다. 사이클 완료 전 checkbox를 완료로 올리지 않는다. 변경 종료 때만 승인 범위에 맞는 문서/커밋·푸시를 처리한다.

## 인수 기준

- 드로 중 기존 노란 IMGUI/world trajectory line이 보이지 않는다.
- UTK 커서 중심/화면 ray와 활 muzzle에서 출발하는 화살이 같은 aim solution을 사용하고 근·원·고저차 표적 결과를 기록한다.
- 사거리 확장은 비교 가능한 baseline과 실제 Play 결과로 입증한다.
- 한 번 클릭 시 한 공격 입력/히트, 두 번 클릭은 두 번, 세 번 클릭은 최대 세 번의 개별 공격으로 재현한다.
- 일반 공격과 패링은 별도 입력. 패링은 유효 피해를 창 안에서 정확히 한 번 흡수하며 상태를 종료/복원한다.
- G toggle 첫 press가 지속 AERO volumetric gas spray를 시작하고, 두 번째 press가 emitter를 끈다. OFF에서 화면 전체 fog가 남지 않는다.
- G spray ON 동안 Figma Potion Status UTK가 보이고, 현재 활성 dose의 남은 양을 연속 게이지로 표시한다. 포션 장전 시 인벤토리에서 1개 차감, dose 종료 때 다음 인벤토리 포션 1개 자동 차감/교체, 포션 재고 소진 시 안전 정지한다. G로 일시 정지·재개해도 활성 dose 게이지는 보존된다. 가스통 연료량과 포션 dose량은 독립 표시/소모된다.
- 지정 병사 발밑은 파란 링, 공격 지시 몬스터 발밑은 빨간 링. 둘 다 collider 없는 지면 링이고 해제 조건에서 제거된다.
- Compile/EditMode/Play/Figma visual 결과를 서로 분리 보고하며 Play 이전에 런타임 완료 판정하지 않는다.

## 알려진 리스크와 현재 코드 변경 대상 후보

- MainScene과 Test_10은 플레이어/애니 배선이 다를 수 있어 둘 다 확인해야 한다.
- Test_10에서 `AttackSystem` 중복 부착/두 좌클릭 consumers를 단일화해야 한다.
- `SprayInputHandler`와 `GasSprayer`가 모두 G를 listen한다. AERO toggle이 추가돼도 기존 입력 consumer를 끄거나 라우팅하지 않으면 한번 누를 때 두 번 토글되어 바로 꺼질 수 있다.
- AERO FullScreenPass는 화면 전체 렌더 패스지만 gas volume은 world-space density mask/shape를 사용해 플레이어 주변에서만 나타나게 설계한다. 평면 고정 위치 제한이라고 가정하지 않는다.
- `GasSprayUTK`는 이미 있지만 현재 G pressed/open binding이 이 UI에 연결되어 있는지 코드상 증거를 찾지 못했다. 기존 창을 연결할지 Figma의 별도 compact widget으로 전환할지 실제 source frame과 현재 hierarchy를 확인하고 결정한다.
- 검색한 프로젝트 내에서 별도 Figma 파일/frame inventory는 확인하지 못했다. 코드 주석에 frame명이나 asset export가 있는지 추가 확인하고, 없으면 사용자 Figma 링크/캡처가 제공되기 전까지 상세 치수는 추정하지 않는다.
- 요청은 계획 수정이며, 이번 변경은 plan 문서만 수정한다. gameplay code, assets, scenes, tests, compile, EditMode, Play, commit은 변경/실행하지 않는다.

---

## 다음 실행 단위

1. AttackSystem/PlayerCombat 중복 입력과 클릭별 melee hit RED test.
2. 노란 IMGUI line 제거 및 UTK pointer-to-muzzle aim solution 공유.
3. 패링을 공격과 다른 remappable action으로 분리하고 피해 interception을 end-to-end 검증.
4. G키 consumer를 하나로 정리하고 AERO world-space density mask 기반 지속 분사 lifecycle 구현.
5. Figma Potion Status UTK를 G toggle에 연결하고 장전 잔량을 표시.
6. Blue guard-selection ring / red designated-monster ring을 Figma spec에 따라 통합.
7. QA, Test_10/MainScene Play, 문서 완료 처리.

구현은 본 계획에 대한 사용자의 “진행/실행” 지시에 따라 시작한다.
