# 📋 TEST26 수정 계획 — 67차 (2026-09-16, 2차 라운드)

> **전제**: 66차 수리 후 Play 실측(Editor.log + 사용자 그립 실측 로그). ①병사 애니는 해결(추종·합세 확인). 나머지 뿌리를 2차 실측으로 재확정.

## #1b 병사 공격이 몬스터에 데미지 없음 (뿌리 확정)

**뿌리 (코드 실측)**: `GuardCombatAI.UpdateGuardBehavior`의 공격 분기(L88-92)는 `driver.TriggerAttack()` — **공격 애니메이션만 발동, 데미지 적용 코드가 존재하지 않는다**. 병사는 흉내만 내고 몬스터 HP는 무관.
**수리 (GuardCombatAI.cs + GuardPlaceholder.cs)**:
1. NotifyPlayerAttack이 IDamageable을 이미 조회 — 병사별 `SetCombatDamageTarget(damageable, 포진오프셋)` 저장.
2. 공격 범위(≤2.5m) 도달 시 TriggerAttack(애니) + `TryStrikeCombatTarget()` — 쿨다운 1.3s, 데미지 = 8 + Level×1.5, `IDamageable.TakeDamage(dmg, 대상위치+up0.9, 병사이름)`. 전투 종료 시 타겟 해제.
3. 이동 타겟 갱신: 몬스터가 움직이면 명령 위치를 타겟 실시간 위치+오프셋으로 갱신(추격).

## #2 활: 스탠스는 이미 재생 중 — "서 있을 때" 문제

**실측**: BowEnter 트리거 발화 ✓, 전이 후 클립 = `Walk_Forward_with_Bow_Aimed`(human=True, 다리 회전 Δ27°) — **활 조준 걷기 클립이 제자리에서 루프** → 서 있으면 "제자리 걷기"처럼 보여 전환된 것처럼 안 보임.
**자산 실측**: MeshyUser 클립 라이브러리에 활 전용 **Idle 클립이 없음**(Walk_F/B_with_Bow_Aimed, Draw_and_Shoot_from_Back_1뿐). 활은 손에 든 **실물 프롭**(그립 66차 수리로 손위치 0.078m — 정상)이므로 대기 클립은 일반 Idle로 충분.
**수리 (Player_AC.controller YAML)**:
1. `BowAimedF → Idle` 전이 신설(조건 Speed < 0.2, 모드 4/Less, 지속 0.15s) — 서면 자연스러운 대기+손에 활 프롭.
2. 이동 시 기존 Idle→Walk(Speed>0.55) 경로 유지, 발사 시 기존 DrawShoot(Draw_and_Shoot) 경로 유지. 파라미터/상태 삭제 없음(추가만).
3. 화살 발사 경로(TryBowShot→ArrowProjectile)는 65차 세션 로그로 발사·적중 실측됨 — 이번 세션은 활 장착 직후 종료라 미클릭. 추가 수정 없음, 발사 성공/실패 로그로 다음 판정.

## #3 장비 비가시 — 이벤트가 핸들러에 도달하지 않음 (구독 유실)

**실측**: ArmorVisual 구독 완료(L1774) → 데모 장착(L5374)·수동 장착(최소 7회) 전부 `OnEquipmentChanged 발화` 로그는 있으나 **수신 로그 0건** — 핸들러가 구독 목록에 없음. OnEquipmentChanged는 필드라 중간에 구독이 유실되면 침묵(예외도 없음). 유실 경로: 공유 GO 파괴/인스턴스 교체(정확 파괴자 불명 — GameManager 교체 파괴 경로 존재).
**수리 (ArmorVisualAttachSystem.cs) — 자가 치유 구조**:
1. `public static ArmorVisualAttachSystem Instance` 싱글턴화.
2. 전용 **Watchdog**(자체 GO + DontDestroyOnLoad, Awake에서 생성): 0.5s 주기로 ① Instance 파괴 시 재생성 ② `SyncTick()` 호출.
3. `SyncTick()`: (a) 구독 인스턴스 ≠ 현재 Instance → 재구독+InitialSyncAll (b) 슬롯 리컨실레이션: 장착 itemId 대비 부착 비주얼 부재/불일치 → 즉시 재부착(TryAttachImmediate). **이벤트 유실과 무관하게 0.5s 내 장비가 붙는다** (성공/실패 전부 로그 — 66차 유지).

## #4 Ctrl+드래그 무반응 — 진단 불가 구간 제거 + 입력 순서 흡수

**실측**: GSM 생성 ✓, [RTS] 로그 0건 — 드래그 시작 자체가 로그에 없음(사용자 입력 패턴 미확인).
**수리 (GuardSelectionManager.cs)**:
1. **모든 좌클릭에 `[RTS] 좌클릭 감지 ctrl={}` 진단 로그** — 다음 Play에서 원인 즉시 확정.
2. **늦은 Ctrl 흡수**: 좌클릭 홀드 중 Ctrl을 나중에 눌러도 드래그 시작(입력 순서 무관) — `_leftDownWithoutCtrl` 추적.
3. 기존: Ctrl+클릭 down 시작, 드래그 중 Ctrl 해제 허용(66차) 유지.

## #5 무기 그립 — world AABB가 가짜값 (정점 기반으로 전면 교체)

**실측 (사용자 제공 그립 실측 로그)**:
- wood_sword bounds=(1.38,1.35,1.47) — GLB 원본 (1.0,0.98,0.56)과 완전 불일치(거의 정육면체로 부풀어 오름)
- wood_spear bounds=(0.76,0.55,0.68) — GLB 원본 길이 1.0m가 최대 extent 0.68로 축소 표시
- → **renderer.bounds(월드 AABB)는 glTFast 임포트 구조에서 신뢰 불가**(스킨/자식 계층 왜곡). 이 값으로 그립 계산한 것이 66차 한계.
**정점 분석 (GLB 바이너리 직접 파싱 — 4,044/4,209 정점 단면 프로파일)**:
- wood_sword: X축 길이 1.0m, x≈0에 가드 최대 단면(0.46), x>0.3 손잡이(단면 0.14~0.2, 가늘다), x<-0.3 칼날(0.33, 끝 테이퍼). **손잡이 = x +0.3~+0.5 구간**.
- wood_spear: Z축 길이 1.0m, z≈-0.9에 창날 최대 단면(0.09), 나머지 균일 샤프트(0.03~0.05). **그립 = 샤프트(버트 방향 70% 지점)**.
- wood_bow: Y축 대칭, 피벗=중앙=손잡이 — 66차 GripCenter로 이미 손오차 0.078m(정상).
**수리 (WeaponEquipManager.cs)**:
1. `GripPose`에 `GripStrategy`(0=off/1=중앙/2=가드기준) + `GripGuardFactor`(가드→손잡이 진행 계수: 검 0.5, 창 0.75) 추가.
2. **`ComputeGripPointLocal`**: mesh 정점을 런타임 슬라이스 분석(20구간 단면) → 최대 단면(가드/창날) 기준 손잡이 방향 계수 지점의 **정점 무게중심**을 그립점으로 산출 — 로컬 공간이므로 월드 AABB 왜곡과 무관, 축 배치/부호 추정 불필요.
3. 배치: `localPosition -= handBone.InverseTransformPoint(weapon.TransformPoint(gripWorld))` — 그립점이 정확히 손 원점에 착지(스케일/회전 체인 무관).
4. id 테이블: sword(전략2·0.5)/spear(전략2·0.75)/bow(전략1·중앙) — LocalPos는 (0,0,0)(그립점이 위치 결정), LocalEuler 기존 유지. world-AABB 기반 오프셋/스케일/클램프 경로는 폴백으로만.
5. 로그: 그립점 좌표 + 조정 후 손↔그립점 오차(0에 수렴해야 정상).

## #1c 초록색 플레이스홀더 (식별 — 코드 결함 아님)

**조사 결과**: 내병사 캡슐=파랑(0.2,0.4,0.9)/적=빨강, 사거리 링=파랑, DraculaLord=흑/적, 토양=갈색, 약초 GLB 정상 부착, 병사 6기 전부 FBX 부착(캡슐 폴백 0건). **남은 후보**: 농작물 구체(FarmPlot 작물 색), 약초 본체, 슬라임 몬스터 자체(녹색). 의도된 오브젝트로 판정 — 다음 Play에서 스크린샷/위치 확인 후 불필요 시 제거.

## 실행 순서
A(#1b) → B(#3) → C(#2 YAML) → D(#4) → E(#5) → 배치컴파일 error CS=0 → ROADMAP/QAPROGRESS/커밋.

## 금지
- 컨트롤러 파라미터/상태 삭제 금지(추가만), 코루틴 침묵 허용 금지, world-AABB 그립 재사용 금지.
