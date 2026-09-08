# 애니메이션 시스템 구현 계획 — 미사용 39클립 전량 활성화
작성: 2026-09-08 | 기준: MeshyUser 68클립 중 29클립 사용 중, 39클립 미사용
트리거 대기(유저가 Phase 지정 또는 "진행" 시 순서대로 실행)

## 공통 구현 패턴 (모든 Phase 동일)
1. 컨트롤러 등록: MixamoControllerBuilder에 meshy: 슬롯/파라미터/전이 추가 (기존 패턴)
2. 발화: HumanoidClipDriver 퍼블릭 트리거 경유(TriggerXxx — 4종 선례 존재) 또는 MoveX/MoveY/IsCombat 파라미터
3. 시스템 스크립트: 신설 필요 시 Systems/ 하위에 추가
4. 검증: 배치컴파일 error CS=0 ×2 + 상태/GUID 확인 → 커밋/푸시/텔레그램/QAPROGRESS

---

## Phase A — 줍기/수집 확장 (2클립) | 규모: 小 | 의존: 없음
- 클립: Collect_Object, Male_Bend_Over_Pick_Up
- 현황: HerbPickup.Harvest()가 이미 TriggerHarvest()(Pull_Radish) 발화 — 2곳
- 구현: 아이템/상황별 클립 분기(약초=Pull_Radish 유지, 일반 오브젝트=Collect_Object 또는 Bend_Over_Pick_Up) — HerbPickup에 클립 선택 파라미터 추가
- 확장: LootBasket 줍기(바구니 클릭 대신 E키 줍기)에도 Pick_Up 발화

## Phase B — 문 상호작용 (1클립) | 규모: 小 | 의존: 없음
- 클립: open_door_3
- 구현: DoorInteractable 신설(문 오브젝트+E키 근접 판정+열림 애니) → 플레이어 open_door 트리거 → 문 회전 애니메이션
- 배치: 성내부(PlayerCastleInteriorBuilder)/추후 건물에 문 오브젝트 추가

## Phase C — 대화·승리 연출 (3클립) | 규모: 小 | 의존: NPC 대화 시스템(구현됨)
- 클립: Talk_Passionately, Talk_with_Hands_Open, victory
- 구현: 대화 시작 시 플레이어 Talk 트리거(대화창 열림 훅에서 발화, 랜덤 2종) / 전투 승리·영지 점령 판정 시 victory 1회

## Phase D — 좌석/침대/음료 (4클립) | 규모: 中 | 의존: 없음
- 클립: Sit_Lie_Bed, Sit_to_Stand_Transition_M, Stand_to_Sit_Transition_M, Toss_and_Turn, Stand_and_Drink
- 구현: SeatInteractable(의자/침대 오브젝트+E키 착석→Stand_to_Sit→Sit 상태 유지→이동 입력 시 일어남) / 침대는 수면(체력·스태미너 회복 게이지) / Toss_and_Turn=수면 중 랜덤 / Stand_and_Drink=소비 아이템 사용 모션(인벤 사용 훅)

## Phase E — 웅크림·잠입·기어 (6클립) | 규모: 中 | 의존: 없음(잠입 인지는 후속)
- 클립: Cautious_Crouch_Walk_Forward/Backward/Left/Right_inplace, Sneaky_Walk, Crawl_Backward
- 구현: Crouch 토글(Ctrl) — PlayerMovement에 자세 상태+이동 속도 절감(50%)+캡슐 높이 축소 / 이동 클립 4방향 스왑(MoveX/MoveY 게이트 — 이동 확장 패턴 재사용) / Sneaky_Walk=은신 상태(기존 은신 시스템 314행 존재 — 연결) / Crawl_Backward=기어 상태
- 후속: 적 인지 게이지(잠입 메커니즘) — 별도 설계

## Phase F — 기동(계단/사다리/벽) (3클립) | 규모: 中 | 의존: 오브젝트 배치
- 클립: Climb_Stairs, Ladder_Climb_Finish, climbing_down_wall
- 구현: LadderInteractable(사다리 오브젝트+진입 판정→클라이밍 상태머신(상승/하강/종료)→y 이동) / 계단=경사 자동 판정 보강 / climbing_down_wall=벽 가장자리 판정
- 배치: 성/건물에 사다리 오브젝트 필요

## Phase G — 전투 변형 확장 (4클립) | 규모: 中 | 의존: 전투 시스템(구현됨)
- 클립: Attack, Charged_Upward_Slash, Thrust_Slash, Sword_Parry_Backward_1
- 구현: ①공격 변형 풀 — 1단 공격을 Sword_Slash/Thrust_Slash/Attack에서 랜덤(또는 무기별) ②차지 공격 — 공격키 홀드 0.6s+ → Charged_Upward_Slash+데미지 2배 ③패리 — 방어 키(우클릭) 입력+적 공격 타이밍 0.3s 창 → 패리 성공 시 Sword_Parry+넉백
- 파라미터: ChargeLevel(float) / Parry(Trigger) / ParryHold(Bool)

## Phase H — 수영 (2클립) | 규모: 中 | 의존: WaterBody(구현됨 — T-D3 하천 포함)
- 클립: Swim_Forward, Swim_Idle
- 구현: 물 판정(플레이어 y < waterLevel-0.5 && WaterBody 내부) → Swim 상태머신(수면 y 고정+이동 속도 절감 40%+수영 클립 스왑+육지 복귀 판정) / Swim_Idle=정지, Swim_Forward=이동
- 파라미터: IsSwimming(Bool)

## Phase I — 투척 (3클립) | 규모: 中 | 의존: 투사체 시스템(화살 등 참고)
- 클립: Crouch_Pull_and_Throw, baseball_pitching, Walk_Backward_with_Grenade
- 구현: ThrowInteractable(투척 아이템+포물선 투사체+착지 효과) / Walk_Backward_with_Grenade=투척 아이템 장착 후진
- 파라미터: IsThrowing(Bool)

## Phase J — 활/창 무기 (6클립) | 규모: 大 | 의존: 무기 시스템 확장
- 클립: Archery_Shot, Draw_and_Shoot_from_Back_1, Walk_Backward_with_Bow_1, Walk_Backward_with_Bow_Aimed, Walk_Forward_with_Bow_Aimed, Spear_Walk
- 구현: ①활 — WeaponData에 활 추가+조준 모드(에임 카메라 전환)+Draw(홀드)→Shot(발사)+화살 투사체+장전 상태, 이동 클립은 Bow 계열로 스왑(IsCombat 확장 — WeaponType별 게이트) ②창 — WeaponData 창+Spear_Walk 이동 클립
- 주의: 궁수 병사(Standing Draw Arrow 믹사모)와 시스템 공용 설계 권장

## Phase K — 사방 주행 블렌드 (2클립) | 규모: 小 | 의존: 이동 파라미터(구현됨)
- 클립: BackLeft_run, BackRight_Run
- 구현: 후진+측면 동시 입력(MoveY<-0.3 && |MoveX|>0.45) 시 WalkBack에서 BackLeft/Right_run으로 스왑 — 이동 확장 패턴 그대로, 상태 2종+전이 4건만 추가

## Phase L — 운반·마법 (2클립) | 규모: 大 | 의존: 로드맵 미정
- 클립: Carry_Heavy_Object_Walk, mage_soell_cast_4
- 구현: 운반=자원/오브젝트 들기 상태(손 부착+속도 절감+내려놓기) — 채집/건설 시스템과 연계 예정 / 마법=스킬 시스템 설계 후 — 둘 다 로드맵 명시 전까지 후순위

---

## 실행 순서 권장 (투자 대비 효과)
1. **A 줍기** (小 — 훅 이미 존재) → 2. **K 사방 주행** (小 — 패턴 재사용) → 3. **B 문** (小) → 4. **C 대화·승리** (小) → 5. **D 좌석/음료** (中) → 6. **G 전투 변형** (中) → 7. **E 웅크림** (中) → 8. **H 수영** (中) → 9. **F 기동** (中) → 10. **I 투척** (中) → 11. **J 활/창** (大) → 12. **L 운반·마법** (大)

## 진행 규칙
- 각 Phase 완료 시: 배치컴파일 error CS=0 ×2 + GUID/상태 검증 + 커밋/푸시 + 텔레그램 알림 + QAPROGRESS 기록 (기존 사이클 동일)
- Phase는 독립적 — 순서 변경/부분 실행 가능
- 시스템 신설 스크립트는 Systems/ 하위, asmdef 경계 준수(Systems→UI 참조 금지)
