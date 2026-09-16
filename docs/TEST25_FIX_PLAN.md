# 📋 TEST25 수정 계획 — 66차 (2026-09-16)

> **배경**: 타 모델 세션(64~65차) 후 사용자 리포트 5건. Editor.log 실측 + GLB 바이너리 파싱 + 코드 전수로 뿌리 원인 전부 확정 후 수리.
> **원칙**: 기존 동작(좌클릭 공격·Ctrl 드래그·탭 부대) 회귀 금지, 변경 파일 최소화, 전부 실측 기반.

---

## #1 병사 애니메이션 미재생 (뿌리 확정 — 최우선)

**뿌리 (GLB 바이너리 파싱 실측)**:
- `Soldier_Lv*.glb` = 임베디드 애니메이션 **0개**, 뼈대 계층은 중첩(`metarig/Root/spine/spine.001/pelvis.L/thigh.L/shin.L/foot.L`).
- `Soldier_*.anim` 클립 바인딩 경로는 **flat 단일 세그먼트**(`foot.L`, `shin.L`, `shoulder.L`, `spine`, `Root`).
- Unity 제너릭 애니는 Animator 루트 기준 **전체 경로** 일치 필요 → GLB 중첩 경로와 불일치 → **바인딩 실패 = 무동작(T포즈)**.
- FBX(fbx/soldier_lv*_rigged)는 flat 계층 → 클립 경로 일치 → 애니 재생(59차 Editor.log 실측 "애니 동작 확인").
- 63차가 GLB 우선 로드로 바꾸면서 뿌리가 깨짐. 65차-2의 avatar 롤백은 증상만 제거(본질은 GLB↔클립 경로 미스매치).

**수리 (파일: `Systems/TestTerritoryCombatSetup.cs` CreateGuard / `Systems/GuardManager.cs` LoadSoldierModel)**:
1. CreateGuard 본 분기 순서 교체: **FBX 우선**(SoldierShield_AC + HumanoidClipDriver(Soldier) + `CopyMaterialsFromGlb` GLB 재질 이식 — 59차 검증 조합) → GLB는 **재질 소스 전용**. GLB 로드 성공 여부와 무관하게 몸통은 FBX.
2. GuardManager.LoadSoldierModel(프로덕션)도 동일 순서(FBX 우선 + GLB 재질 이식)로 통일.
3. 부착 완료 로그 강화: avatar name/isValid, controller, 드라이버 모드 + FBX/재질이식 성공 여부.
4. `GroundModelToY(SurfaceY)` 접지·콜라이더 정리·태그(RecruitedSoldier/Guard) 로직 기존 그대로 유지.

## #2 활 전용 애니 전환 + 화살 비행

**실측**: Player_AC에 BowEnter 트리거 파라미터+전이(L5130)+IsBow 조건 전이 다수 + 상태(BowAimedF/BowBack1/BowBackAimed/DrawShoot) 존재. HumanoidClipDriver L423-436이 CurrentType 변화 시 SetTrigger("BowEnter")/SetBool("IsBow") 자동 발화. 마지막 세션에서 활 발사 4회 + ArrowProjectile 적중 로그 확인 — **발사·적중은 동작 중**.
**수리 (파일: `Systems/HumanoidClipDriver.cs`, `Systems/PlayerCombat.cs`, `Systems/WeaponEquipManager.cs`)**:
1. CurrentType이 Bow로 바뀐 프레임에 `[Bow] 장착 전환: BowEnter 발화` 로그 추가(전이 발화 증거 고정).
2. 코드 에이전트가 Player_AC YAML의 BowEnter 전이(from/to/condition/Has Exit Time)를 검증 — 전이가 조건 미충족으로 막혀 있으면 상태 머신 수정(트리거 전이는 AnyState 또는 Idle→활 스탠스 조건 정리).
3. 발사 시 ArcheryShot → DrawShoot 상태 도달 검증 로그. 발사와 애니 동기(기존 _attackHoldUntil Speed 홀드 유지).
4. 화살 시각: ArrowProjectile 스폰에 샤프트 메쉬/트레일 존재 확인 — 없으면 최소 화살 메쉬(얇은 원뿔+트레일) 보장. 기존 적중 로직 무변경.

## #3 장비 장착 후 가시 부착 (뿌리 확정)

**뿌리 (Editor.log 실측)**:
- 부트(Awake) `EquipDemoStarterGear` → ArmorVisualAttachSystem이 6슬롯 전부 "부착 시작" 수신 후 **성공(✅)·본 미발견·로드 실패 로그 전무** — AttachRoutine 코루틴이 첫 yield 후 재개되지 않고 침묵 사망(부팅 프레임 내 코루틴 호스트 파괴/비활성 유력).
- wood_*.glb 전부 존재 확인(126개 GLB) — 로드 문제 아님. 플레이어 avatar는 Humanoid 유효(Player_Rigged_HeatAvatar) — 본 조회도 가능.

**수리 (파일: `Systems/ArmorVisualAttachSystem.cs`, `Systems/TestTerritoryCombatSetup.cs`)**:
1. AttachRoutine을 **동기 우선** 변경: bones+GLB가 즉시 해결되면 코루틴/폴링 없이 그 자리에서 즉시 부착. 실패 시에만 기존 폴링 유지.
2. 결과 무조건 로그: 성공(✅ slot→bone 목록+bounds 실측) / 본 미발견 / 로드 실패 — 침묵 불가 구조.
3. 부팅 레이스 흡수: `EquipDemoStarterGear` 장착 시점을 Awake 직후가 아니라 **첫 프레임 지연**(코루틴 1프레임 또는 SetupPlayer 완료 후)으로 이동 — Awake 중 이벤트 발화로 생긴 코루틴 사망 차단.
4. 가시성 보정: 부착 직후 renderer bounds를 손/부위 위치와 비교하는 로그 추가 + 포즈 테이블(Scale) 기본값 유지, 본 원점이 비정상(극단 offset)이면 로그로 노출해 후속 튜닝 루트 확보.
5. 파괴된 슬롯 재부착 방지(기존 DestroySlotVisuals 로직 유지).

## #4 Ctrl+드래그 사각형 선택

**실측**: 좌표계 통일(65차 E)·태그·IsRecruited 조건·박스 그리기 모두 정상 구현. 마지막 세션에 [RTS] 로그 0건 = 드래그 완료 자체가 로그에 없음(재판정 필요).
**수리 (파일: `Systems/GuardSelectionManager.cs`)**:
1. 진단 로그 보강: 드래그 시작(Ctrl+좌클릭) / 드래그 확정(rect 크기) / 선택 결과(N명) — 침묵 구간 제거.
2. 드래그 시작 후 **Ctrl을 떼어도 드래그 지속**(현재는 Update 초반 `if (!ctrl) return;`로 드래그 중 Ctrl 해제 시 즉시 취소) — 드래그 시작 조건으로만 Ctrl 사용.
3. `_mainCamera` null 얼리리턴 시에도 로그(원인 가시화) + 카메라 캐시 재갱신.
4. 선택 표시(파란 원)·우클릭 명령·H 정지 기존 유지. PlayerCombat의 Ctrl 직접 판정 스킵(공격 안 나가게) 로직 유지.

## #5 무기 손 그립 정밀화

**뿌리 (Editor.log 실측)**:
- 검: `[Weapon] bounds 비신뢰(비대칭 1.06 미달) — 테이블 포즈 사용` → wood_sword GLB bounds가 거의 대칭이라 자동 정렬 스킵 → 타입 공용 테이블 포즈로 부착(어긋남).
- 활: `그립 정렬 offset=(0.295, 0.811, -0.355)` — Y +0.811 = 손에서 0.8m 위(명백 이상). 활은 손잡이(bounds 중앙)를 손에.
- 창: GripEnd=-1(자루 끝 강제) 로직 존재 — 로그로 실측 검증.
- 스윙 트레일 부착 로그가 "타입=Fist"(검/활 장착 직후 모두) — 무기 타입 전파 누락 버그.

**수리 (파일: `Systems/WeaponEquipManager.cs`, `Systems/WeaponSwingTrail.cs`)**:
1. **무기 id별 그립 오버라이드 테이블 신설**(`_gripTableById`: wood_sword/wood_spear/wood_bow/wood_shield + steel/stone/crystal 계열 확장 가능 구조) — LocalPos/LocalEuler/GripEnd/클램프를 id 단위로 제어. 타입 테이블보다 우선.
2. 활: 그립점 = bounds **중앙 손잡이**(GripEnd=0, 피벗 중앙 스냅), offset 클램프 1.0 유지 — offset |Y|>0.3이면 경고 로그.
3. 검: id 오버라이드로 손잡이(bounds 하단) 그립 — bounds 비신뢰 가드 우회 경로 명시(id 테이블 있으면 테이블 포즈+미세 오프셋 사용).
4. 창: 기존 GripEnd=-1 유지 + 로그 실측.
5. 방패: ArmorVisual Back 슬롯 pose(좌팔 외측)를 wood_shield에 맞게 보정.
6. 스윙 트레일 타입 전파 수리(부착 시점 무기 타입 전달).
7. 그립 완료 후 `손 본 ↔ 무기 그립점 거리` 실측 로그 추가 — 다음 튜닝의 판정 근거 고정.

---

## 실행/검증 순서
1. 단일 코드 에이전트가 #1→#3→#5→#2→#4 순으로 수리(파일 충돌 방지 — TestTerritoryCombatSetup/HumanoidClipDriver 중복 편집 회피).
2. 부모가 배치컴파일(에디터 점유 확인 후) — **error CS=0** 필수.
3. QA 에이전트: diff 전수 검증(뿌리-수리 대응, 회귀 토큰, 괄호 균형, 로그 존재).
4. ROADMAP.md / QAPROGRESS.md 기록 + git commit+push.
5. Play 판정 항목: ① 병사 걷기/대기 애니(FBX+GLB 재질) ② 활 장착 시 스탠스 전환+발사 애니 ③ 시작 즉시 투구/갑옷/장갑/부츠/방패 가시 ④ Ctrl+드래그 박스+파란 원 선택 ⑤ 검/창/활/방패 손 그립.

## 금지 사항
- GLB에 FBX avatar 재지정 금지(65차-2 롤백 사유 — T포즈 유발).
- GLB 몸통 우선 로드 금지(본질 미해결) — FBX 우선 원칙 고정.
- UI/팔레트/전투 수치 임의 변경 금지.
- 코루틴 침묵 허용 금지(모든 비동기 경로에 결과 로그 의무).
