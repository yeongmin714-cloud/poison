# 무기/핫바 시스템 계획 — 퀵슬롯 + 정식 활·폭탄·장착 연동
작성: 2026-09-08 | 트리거 대기(Phase 지정 또는 "진행")

## 참고: 장비 예시.PNG 분석 (vision)
- 하단 8슬롯 핫바, 각 슬롯 하단에 1~8 숫자 박스, 1:1 키 매핑
- 선택 슬롯 = 흰색 두꺼운 테두리 하이라이트 + 상단에 장비 정보 팝업(잔탄수 등)
- 스택 수량 = 슬롯 우상단 소형 숫자 / 다크 반투명 블랙 테마 + 라운드 코너

## 기존 자산 (재사용)
- WeaponData: WeaponType { Fist, Sword, Spear, Bow } + 정적 인스턴스(Sword/Spear/Bow) **이미 정의**
- Bomb.cs: 퓨즈·폭발 반경·폭발력·사운드 **이미 구현** — 스폰만 하면 됨
- ArrowProjectile.Spawn(position, direction, speed, damage, trailColor) 공개 API 존재
- WeaponEquipManager: Equip(id, player) — 검 전용 프리팹 경로(Models/UserProvided/{id}_sword), IsEquipped 정적
- HumanoidClipDriver: 이동 변형 26클립 체제(IsCombat/IsBow/IsSpear/IsThrowing 게이트 구조 이미 존재)
- 임시 토글(B/V/G) → 정식 연동으로 교체 대상

---

## Phase M1 — 퀵슬롯 핫바 UI (8슬롯) | 小~中
- 신규: HotbarUI.cs(UI asmdef) + HotbarSlot 8개(아이콘/수량/선택 하이라이트/숫자 라벨)
- 입력: Alpha1~Alpha8 → 슬롯 선택 → **장비 슬롯이면 WeaponEquipManager.Equip 호출(장착/해제 토글), 소비 아이템이면 사용(Consume), 폭탄이면 투척 준비 모드**
- 데이터: 플레이어 인벤/장비 데이터와 연결(기존 InventoryWindow 데이터 소스 재사용)
- UI 스타일: 예시 사진 스펙(다크 반투명, 선택 흰 테두리, 우상단 스택 수량)
- 발화: 애니 연계는 아래 M2/M3/M4에서 처리

## Phase M2 — 무기 장착/해제 연동 (임시 토글 제거) | 中
- WeaponEquipManager 확장: Equip(id)를 타입 대응으로 — 프리팹 경로 {id}_sword/{id}_bow/{id}_spear, 없으면 경고+클립 모드만 전환
- CurrentType 노출(WeaponType) → HumanoidClipDriver가 IsCombat/IsBow/IsSpear 대신 **CurrentType 기반** 게이트로 교체
- PlayerMovement의 임시 B/V/G 토글 제거(핫바 장착이 유일한 모드 전환 경로)
- 해제 = Fist 모드(일반 이동 클립 복귀)
- 애니: Sword=기존 26클립 체제 / Bow=M3 / Spear=M5 / Fist=일반

## Phase M3 — 활 정식 시스템 (5클립) | 中~大 | 의존: 활 프리팹(유저 제공 필요)
- 장착: 활 모드 진입 → DrawShoot(장전 클립) 1회 → BowAimedF 유지
- 발사: 좌클릭(공격키) → DrawShoot(장전 연출, 짧은 홀드) → ArcheryShot(발사) 시점에 **ArrowProjectile.Spawn(origin, dir, speed, damage=WeaponData.Bow.damage, trailColor)** 호출 — 데미지 연동 완료
- 이동: BowAimedF(전진)/BowBackAimed·BowBack1(후진) — IsBow 게이트(기존 구조 유지)
- 조준: 마우스 커서 방향(기존 에임 회전 로직 재사용) + 화살 방향 = 커서 방향
- 입력: PlayerCombat의 활 타입 분기(근접 스윙 대신 발사) — PlayerCombat.CanAttack 쿨다운(attackSpeed 2.0) 사용
- 후퇴: BowBack1(일반 후퇴)/BowBackAimed(조준 중 후퇴) 구분 — 조준(마우스 홀드) 여부 파라미터

## Phase M4 — 폭탄 투척 정식 (3클립) | 中 | 의존: 폭탄 아이템(인벤 데이터 추가)
- 투척: 폭탄 선택 상태에서 좌클릭(또는 핫바 G) → Throw/ThrowPitch(랜덤) 클립 발화 → **클립 하이포인트 시점(0.4s)에 Bomb.Create(transform.position + forward*0.5) 스폰 + 포물선 Velocity(전방 8m/s + 상승 5)**
- Bomb.cs 활용: 기존 퓨즈(0.5s)→폭발(반경 3m, 데미지, 넉백) 그대로 — 데미지 연동 완료
- 수량: 인벤에서 폭탄 1개 소모(수량 0이면 투척 불가 + 경고)
- 이동: Throw 모드 중 GrenadeBack(후진) — IsThrowing 게이트(기존 구조 유지)
- Bomb.Create 스태틱이 없으면 신설(기존 생성자 확인 후)

## Phase M5 — 창 (1클립) | 中 | 의존: 창 프리팹(유저 제공 필요)
- 장착: Spear 모드 → SpearWalk 이동 + 근접 range 4m(WeaponData.Spear) 자동 적용(PlayerCombat range 분기)
- 공격: 기존 근접 스윙(공격 시스템) 재사용 + 찌르기 클립(Thrust_Slash) 우선 사용

## Phase M6 — 활/창/폭탄 모델 프리팹 (에셋) | 의존: 외부
- 활/창/폭탄 3D 모델 필요 — 유저 제공(Meshy 생성 GLB/FBX) 또는 무료 에셋
- 배치 경로: Assets/Resources/Models/UserProvided/{id}_bow / {id}_spear (WeaponEquipManager 로드 규약)
- 프리팹 없이도 M1~M2는 동작(클립 모드만 전환, 모델 미표시)

---

## 실행 순서 권장
1. **M1 핫바** (UI 독립 — 즉시 체감) → 2. **M2 장착 연동** (임시 토글 제거 — 핫바 1~8키가 모드 전환) → 3. **M4 폭탄** (Bomb 이미 구현돼 소규모) → 4. **M3 활** (데미지 연동) → 5. **M5 창** → 6. **M6 모델** (유저 제공 시점에)

## 진행 규칙
- 각 Phase: 배치컴파일 CS=0 ×2 + GUID/상태 검증 + 커밋/푸시/텔레그램/QAPROGRESS (기존 사이클)
- M3/M4 발화는 HumanoidClipDriver 퍼블릭 트리거 경유(기존 패턴)
- asmdef: UI(핫바)는 Core 참조 가능 — Systems 역참조 금지(기존 규약)
