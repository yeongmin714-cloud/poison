# ⚔️ 공격 액션감(Attack Feel) 전면 개선 계획

> **요구 (사장님):** 테스트 17 영상의 공격씬이 여느 3D RPG 대비 액션이 부족하다. 받아놓은 에셋을 써도 좋으니 **공격 액션을 액션감 있게**.
> **방식:** 기존 전투 체인(PlayerCombat / HumanoidClipDriver / CombatFXGate / CombatCameraEffects / HitStopManager)을 폐기하지 않고 **모션·타격·카메라·FX·사운드 5레이어로 보강**. Test_10(경량 전투씬)에서 눈검증 → 메인씬 확산.
> 작성일: 2026-09-15 · 근거: 테스트 17.mp4 프레임 실측 + Player_AC.controller + 에셋 전수 스캔

---

## 0. 진단 (테스트 17 프레임 실측 + 코드 분석)

### 0-1. 영상 실측 소견 (53프레임 전수 스캔)
| # | 소견 | 뿌리(코드/에셋) |
|:--|:--|:--|
| 1 | **몸통 전체가 앞으로 쏠리는 단일 런지** — 골반 회전/팔로스루/스텝 없음, 발 미끄러짐 | WeaponCombo = 단일 클립 `Weapon_Combo_2.fbx`(136f) 슬라이스 3연타. 스테이지 1/2/3이 **같은 테이크의 구간**이라 타별 개성이 약함 |
| 2 | **피격자가 반응 없음** — HP바만 감소, flinch/넉백/사망 모션 없음 | `AnimalAI/GuardPlaceholder` TakeDamage에 피격 모션 트리거 부재 (FX만 발화). *최대 갭* |
| 3 | **카메라 정적** — 셰이크/줌/슬로우 체감 없음 | `CombatCameraEffects` 임펄스 프로파일이 **미커밋 Phase A에 막 추가된 상태** (검증 전) |
| 4 | **데미지 넘버 작고 팝 없음** | Phase A에서 타입(Normal/Crit/Back/Heal/Mana) 추가됨(미커밋) · 폰트 크기/바운스 미비 |
| 5 | **VFX가 평면 링** — 급작 등장/소멸, 라이팅 미반응 | SlashVFXRunner 아크 + MagicHit. 트레일 차별화는 Phase B 미커밋 |
| 6 | **사운드 동기화 체감 없음** | `attack_swing`/`attack_hit` 재활용(신규 0) |
| 7 | **무기 모델 미표시(맨손처럼 보임)** | 테스트 씬 시딩/장착 상태 문제 가능 — Play에서 `[Equip]` 로그로 별도 확인 |

### 0-2. 현재 파이프라인 요약
```
좌클릭 → PlayerCombat.TryAttack → HumanoidClipDriver(WeaponCombo 슬라이스 진행)
        → AttackTarget 판정 → CombatFXGate.PlayHitFX(임팩트/넘버/히트플래시)
        → CombatCameraEffects(셰이크/HitStop/슬로우) + HitStopManager(timeScale 0.08/45ms)
        → SlashVFXRunner(아크) + WeaponSwingTrail(트레일)
```

### 0-3. ⚠️ 미커밋 진행분 (사실상 52차, git 미기록)
`git diff` **15파일 / +1411 −175** — `[2026-09-15 Phase A/B/C]` 태그: 데미지 넘버 타입, 무기별 트레일 색/폭, 무기별 카메라 임펄스 프로파일, 몬스터 어그로 시각화, 인벤 드래그 스냅/하이라이트. **이 계획의 Phase A는 이 진행분을 커밋·검증하는 것으로 시작**하며, 이후 Phase는 그 위에 적층한다.

---

## 1. 목표 & 정량 기준 (Numbers, not vibes)

| 항목 | 현재 | 목표 |
|:--|:--|:--|
| 타격 순간 정지(히트스톱) | 45ms / timeScale 0.08 (전 무기 동일) | 무기별 **40~90ms** (권 40 / 검 60 / 창 75 / 활 50) |
| 카메라 진폭 | 단일 | 무기×크리×킬 3축 프로파일 (진폭/주파수/지속 표로) |
| 피격자 반응 | **0%** | **100%**(flinch) + 강타 20% 넉백 + 킬 다운 |
| 콤보 스테이지 클립 | 단일 테이크 슬라이스 | **스테이지별 전용 클립 3종** + 무기타입별 콤보 테이블 |
| 콤보 연결 입력 버퍼 | 0.12s(1슬롯) | 0.15~0.20s 유지 + **캔슬 윈도** 명시(모션별 시작/끝 %) |
| 사운드 레이어 | 1(swing) + 1(hit) | 스윙/임팩트/보이스/서브베이스 **4레이어 동기** |
| 데미지 넘버 | 고정 소형 | 타입별 크기×색 + 팝(1.35→1.0, 0.15s) + 크리 아웃라인 |

**벤치마크:** 젤다 BOTW(무게감·히트스톱), 하데스(피드백 밀도), 다크소울(무기별 개성·피격 리액션).

---

## 2. 사용 가능 에셋 (실측 — 이미 프로젝트 내 임포트 완료)

### 2-1. 공격 모션 (FBX)
| 에셋 | 위치 | 계획 용도 |
|:--|:--|:--|
| `Weapon_Combo_2` | Animations/MeshyUser | 현행 검 3연타 (기준선) |
| `Triple_Combo_Attack` / `Double_Combo_Attack` / `Attack` | MeshyUser | 검 콤보 대안·스테이지별 분리 후보 |
| `Right_Hand_Sword_Slash` / `Thrust_Slash` / `Charged_Upward_Slash` | MeshyUser | 검 1타 횡베기 / 창 찌르기 / **차지 강공(신규 메커닉)** |
| `Sword_Parry_Backward_1` | MeshyUser | 방어/패링(신규) |
| `One Hand Sword Combo` / `Two Hand Sword Combo` / `Standing Melee Combo Attack Ver.1·2` | Animations/Mixamo | 한손/양손 검 콤보 (품질·개성 상향 주력) |
| `Great Sword Slash` / `Sword And Shield Slash` | Mixamo | 대검/검방패 세트 |
| `Standing Melee Attack Horizontal` / `Standing Melee Run Jump Attack` | Mixamo | 횡베기 / 러닝 점프 공격 |
| `Draw Sword 1` / `Withdrawing Sword` | Mixamo | 발도/납도 (전투 진입·이탈 연출) |
| `1Hand_Up_Attack_A_1~3` / `Attack_B_1~3` | DoubleL | 한손 A(세로)/B(가로) 3연타 세트 — **스테이지별 분리에 최적** |
| `Bow_Attack_A/B` | DoubleL | 활 (현행 ArcheryShot 보강) |

### 2-2. 피격/사망/회피 (현재 미사용 = 최대 갭 충전)
| 에셋 | 위치 | 용도 |
|:--|:--|:--|
| `Hit_Reaction` / `Slap_Reaction` / `Electrocution_Reaction` | MeshyUser | 피격 flinch (경/중/상태이상) |
| `Hit_F_1` / `Hit_F_2` | DoubleL | 병사 피격 |
| `Standing React Small From Right` / `Standing React Large From Right` | Mixamo | 경/강 피격 리액션 |
| `Great Sword Impact` / `Sword And Shield Impact` | Mixamo | 방어구 타격 반동 |
| `Dead` (MeshyUser) / `Dying` / `Falling Back Death` / `Standing Death Forward·Backward 01` / `Two Handed Sword Death` | MeshyUser·Mixamo | 사망 다운 (방향별) |
| `Roll_Dodge` / `Quick Roll To Run` / `Stand To Roll` / `Run To Rolling` / `Standing Dodge Right` / `Standing Dive Forward` | MeshyUser·Mixamo | 회피(신규 메커닉 — 액션감 핵심) |

### 2-3. FX / 사운드
| 에셋 | 위치 | 용도 |
|:--|:--|:--|
| `StylizedSlashVFX`(파랑 아크) | Resources/FX/Slash | 스윙 아크 (기존) |
| `MagicHit`(Guz Magic Hit2) | Resources/FX/Impact | 타격 임팩트 (기존) |
| `WeaponSwingTrail`(Phase B 무기별 색/폭) | Systems | 무기 잔상 (기존+확장) |
| `Travis Hit Impact` | Travis Game Assets | 임팩트 보조/킬 버스트 |
| `Hovl Studio/Magic effects pack` | 대기 | 차지 강공/스킬 오라 |
| `Vefects Free Fire VFX URP` | 대기 | 화염 속성 공격(향후) |
| `GabrielAguiar FreeQuickEffectsVol1` | **미임포트** (URP unitypackage) | 퀵 히트/스윙 보조 |

---

## 3. 설계 원칙
1. **모션 우선** — FX/카메라는 증폭기일 뿐, 액션감의 80%는 모션 타이밍(wind-up→strike→recovery)과 피격자 반응에서 나온다.
2. **단일 게이트 유지** — 새 FX/카메라 발화는 `CombatFXGate` 한 곳으로만 (중복 발화 금지, 46차 교훈).
3. **무기별 개성 테이블** — 히트스톱·카메라·트레일·콤보를 `WeaponType` 단일 테이블에서 파생 (하드코딩 산개 금지).
4. **저사양 가드** — 프레임당 이펙트 캡, GC 할당 최소(기존 static 캐시 패턴), 모션은 Humanoid 리타깃 클립만(절차 대체 금지).
5. **에디터 잠금 대응** — 배치컴파일 CS=0 선행 → 에디터 포커스 시 자동 재컴파일 / DLL strings grep 판정.

---

## 4. Phase 계획

### ✅ Phase A — 진행분 커밋·검증 (기반 정착)
> 대상: 미커밋 15파일(데미지 넘버 타입 / 무기별 트레일 / 무기별 카메라 임펄스 / 어그로 시각화 / 드래그 스냅)
- [ ] A-1 배치컴파일 `error CS=0` 재확인 (현재 미검증)
- [ ] A-2 정적 QA 서브에이전트 — 시그니처/회귀/균형/중복 발화
- [ ] A-3 Play 눈검증 (트레일 색 4종 / 임펄스 프로파일 / 넘버 타입 색)
- [ ] A-4 QAPROGRESS + ROADMAP(52차) + 영구메모리 3중 저장 + git commit/push

### ⭐ Phase B — 모션 코어: 무기별 콤보 테이블 & 스테이지 클립 분리 (최우선)
**파일:** `Systems/HumanoidClipDriver.cs`, `Resources/Animation/Controllers/Player_AC.controller`(+AnimatorOverrideController), 신규 `Systems/WeaponComboLibrary.cs`
- [ ] B-1 **AnimatorOverrideController 기반 무기별 콤보 세트** — WeaponCombo 상태의 클립을 무기 장착 시 오버라이드:
  - 검(한손): `1Hand_Up_Attack_A_1/2/3` 또는 Mixamo `One Hand Sword Combo` 슬라이스
  - 대검(양손): Mixamo `Two Hand Sword Combo`
  - 창: `Thrust_Slash` 3단 (찌르기 리듬)
  - 활: `Bow_Attack_A/B`
  - 맨손: `Attack` (Meshy)
- [ ] B-2 **스테이지별 클립 분리** (단일 테이크 슬라이스 → 3개 전용 클립) — `ComboEndNormT` 의존 제거, 클립별 wind-up/strike/recovery 자동 길이 사용
- [ ] B-3 **캔슬 윈도 테이블** — 각 클립의 `[strikeStart%, strikeEnd%]`를 `WeaponComboLibrary`에 정의 → 입력 버퍼 소비 구간을 클립 실측 기반으로
- [ ] B-4 **wind-up 가중** — 클릭→strike 사이 3~5프레임 예비동작 확보(현 3f delay 유지/확장), recovery는 캔슬 가능
- [ ] B-5 검증: 무기 4종 × 콤보 3타 = 12조합 Play 로그(`[Combo] weapon= clip= stage=`) + 배치컴파일 CS=0

### ⭐ Phase C — 피격자 리액션 (최대 갭, 액션감 급상승)
**파일:** `Systems/AnimalAI.cs`, `Systems/GuardPlaceholder.cs`, `Systems/DraculaLord.cs`, 신규 `Systems/HitReactionDriver.cs`, `Player_AC/Soldier_Animator.controller`
- [ ] C-1 **flinch 3단** — 경타=`Standing React Small From Right`/`Hit_Reaction`, 중타=`Standing React Large`, 강타·크리=`Great Sword Impact`+넉백. 피격 시 상체 0.15~0.25s 가중
- [ ] C-2 **넉백 물리** — 강타/크리 시 타겟 Rigidbody/CC에 `hitDirection × (0.8~1.5m)` 임펄스 (기존 fx 넘버와 독립, 애니 무중단)
- [ ] C-3 **사망 다운** — `AnimalAI.Die()`에서 즉시 파괴 대신 `Dead`/`Falling Back Death` 재생 후 0.6~1.2s 지연 파괴(방향=마지막 hitDirection)
- [ ] C-4 **방어/패링 반응** — `Sword And Shield Impact` + 패링 성공 시 상대 flinch 강화 (GuardPlaceholder)
- [ ] C-5 **HitReactionDriver 단일화** — TakeDamage → 이 드라이버로 위임(모든 IDamageable 대칭). 회귀: HP차감/Die()/어그로는 try 밖 유지
- [ ] C-6 검증: 몬스터/병사/영주 각각 경·중·강·크리 피격 Play 로그 + 다운 후 파괴 확인

### ⭐ Phase D — 카메라 & 히트스톱 강화 (무게감)
**파일:** `Systems/CombatCameraEffects.cs`, `Systems/HitStopManager.cs`, `Systems/PlayerCombat.cs`
- [ ] D-1 **히트스톱 무기별 차등** — 권 40ms/검 60ms/창 75ms/활 50ms, 크리 ×1.3, 킬 0.35s 슬로우(0.25 배). `_recoilActive`식 플래그로 데드락 방지
- [ ] D-2 **카메라 임펄스 3축** — 무기(진폭/주파수) × 크리(×2) × 킬(감쇠 슬로우). Phase A 프로파일 검증 후 미세튜닝
- [ ] D-3 **줌 펀치 / FOV** — 스트라이크 순간 FOV -1.5°(0.15s 복귀, 기존) → 무기별 -1~-3° 차등
- [ ] D-4 **런지 & 리코일 미세조정** — 48차 테이블(검0.6/창1.2/활0.5/권0.4) 유지 + 가속 곡선 무기별(창=강한 ease-in)
- [ ] D-5 검증: 히트/미스 강도 불일치(48차 비고 — TriggerCameraEffects 2회 호출) **단일화** 필수

### Phase E — FX/트레일/임팩트 정합
**파일:** `Systems/SlashVFXRunner.cs`, `Systems/WeaponSwingTrail.cs`, `Systems/CombatFXGate.cs`
- [ ] E-1 아크를 **모션 strike 프레임에 동기** (Phase B 캔슬윈도와 같은 테이블 값 사용) — 클릭 즉시 발화 → strike 프레임 발화로 이동
- [ ] E-2 임팩트(MagicHit) 스케일/수명 무기별 + 크리 1.5배
- [ ] E-3 킬 버스트 = `Travis Hit Impact` (현 MagicHit 단일 → 킬 전용 강화)
- [ ] E-4 트레일 Phase B(무기별 색) + 콤보 스테이지 색 오버라이드 검증
- [ ] E-5 저사양 캡(프레임당 ≤10) 유지

### Phase F — 사운드 4레이어
**파일:** 신규 `Systems/CombatAudioDirector.cs` + `Resources/Audio/` (기존 attack_swing/attack_hit 재활용)
- [ ] F-1 스윙(모션 시작) / 임팩트(hit) / 서브베이스(hit, 저역 펀치) / 보이스(선택, 캐릭터별) 동기
- [ ] F-2 무기별 피치/레이어 (창=금속 관통, 대검=둔탁, 활=시위)
- [ ] F-3 히트스톱과 오디오 오프셋 정렬(정지 중 임팩트 사운드 선행)

### Phase G — 데미지 넘버 juice
**파일:** `Systems/CombatVFXController.cs` (Phase A 타입 확장 위)
- [ ] G-1 크기 5단×타입 색 + 팝(1.35→1.0/0.15s, 기존 확장) + 크리 아웃라인/기울기
- [ ] G-2 크리/백어택 위치 오프셋 및 연타 시 누적 스택(겹침 방지)
- [ ] G-3 가독성: 폰트 UIFont 상향 + 배경 대비(초록 지형 대비 그림자)

### Phase H — 신규 메커닉 (액션감 확장)
- [ ] H-1 **회피(롤)** — `Roll_Dodge`/`Standing Dodge Right`, Space+방향 또는 우클릭 더블탭, i-frame 0.25s
- [ ] H-2 **차지 강공** — 좌클릭 홀드 → `Charged_Upward_Slash` + Hovl 오라 + 히트스톱 ×1.5
- [ ] H-3 **발도/납도** — `Draw Sword 1`/`Withdrawing Sword`로 전투 진입/이탈 연출
- [ ] H-4 **방어/패링** — `Sword_Parry_Backward_1` + `Sword And Shield Block` (상대 공격 시)

### Phase Z — 통합 검증 & 저장
- [ ] Z-1 Test_10 배치컴파일 CS=0 + 정적 QA 서브에이전트(전 Phase diff 전수/회귀/균형/중복 발화)
- [ ] Z-2 Play 눈검증 체크리스트 (아래 §5)
- [ ] Z-3 QAPROGRESS(신규 회차) + ROADMAP(체크) + 영구메모리(치명기법/제약) + git commit/push **3중 저장**
- [ ] Z-4 ASSET_INVENTORY.md 갱신 (신규 사용 에셋 등록)

---

## 5. 검증 체크리스트 (Play 눈검증)
1. [ ] 검/창/활/맨손 각각 좌클릭 → **무기별 콤보 모션**이 다르게 보임
2. [ ] 3연타가 wind-up → strike → recovery 리듬으로 읽힘 (쏠림/미끄러짐 없음)
3. [ ] 타격 순간 **몬스터/병사가 flinch**함 (강타=넉백, 크리=대형 리액션)
4. [ ] 처치 시 **다운 모션 후 파괴** (즉시 소멸 아님)
5. [ ] 히트스톱·카메라 진동·FOV 펀치가 **무기별로 체감 차이**
6. [ ] 데미지 넘버가 타입별 색/크기/팝으로 뜸 (크리=골드 대형)
7. [ ] 임팩트·아크가 **strike 프레임에 동기** (클릭 즉시 아님)
8. [ ] 사운드 4레이어가 타격과 어긋나지 않음
9. [ ] 회피(롤)·차지 강공·발도/납도 동작
10. [ ] 저사양(Test_10)에서 프레임 드랍/마젠타 0건

---

## 6. 실행 순서 & 병렬 배치
```
Phase A (커밋·검증) ─┬─ Phase B (모션 코어)   ← 최우선
                     ├─ Phase C (피격 리액션) ← 최우선
                     └─ Phase D (카메라·히트스톱)
        ↓ (B/C/D 검증 후)
Phase E (FX) ─ Phase F (사운드) ─ Phase G (넘버) ─ Phase H (신규 메커닉)
        ↓
Phase Z (통합 검증 · 3중 저장)
```
- **배치(3병렬):** B·C·D는 파일 상호 독립 → `delegate_task` 3병렬 동시 진행 가능 (B=HumanoidClipDriver/컨트롤러, C=AnimalAI/GuardPlaceholder/HitReactionDriver, D=CombatCameraEffects/HitStopManager).
- 각 Phase: 코드는 code 서브에이전트, 검증은 QA 서브에이전트 위임(사용자 규칙). 배치컴파일 CS=0 선행.
- Phase 완료마다 ROADMAP 체크 + QAPROGRESS 기록.

---

## 7. 리스크 & 오픈 이슈
| 리스크 | 대응 |
|:--|:--|
| 리타깃 클립(Mixamo/DoubleL)이 플레이어 아바타에 어긋남 | Phase B-1에서 Humanoid 리타깃 매핑 명시 주입(2026-09-05 애니8차 선례) + Test_10 눈검증 |
| 스테이지 클립 분리로 콤보 타이밍 회귀 | 캔슬윈도 테이블을 클립 실측 기반으로, 기존 0.18s 홀드 유지 |
| 피격 모션이 어그로/Die() 체인 방해 | HitReactionDriver는 try-catch 격리, HP차감/Die/어그로는 try 밖 (39차 교훈) |
| 카메라 2회 발화(48차 비고) | D-5 단일화 선행 |
| 무기 모델 미표시(진단 #7) | Phase A-3에서 `[Equip]` 로그로 원인 확정 후 별도 수리 |
| 에디터 잠금 | 배치컴파일 CS=0 → 자동재컴파일/DLL grep 판정 |
| 신규 메커닉(롤/차지) 스코프 팽창 | Phase H는 B~G 검증 후 착수, 옵션 |

## 8. 오픈 질문 (사장님 결정 필요)
1. 무기 세트 우선순위: **검(한손) 중심**으로 먼저 완성할지, 4종 동시?
2. 신규 메커닉(회피 롤·차지 강공·패링) 포함 여부 — 포함 시 Phase H 착수.
3. 피격 모션 톤: 사실적(다크소울) vs 경쾌(젤다/BOTW) — 현재 아트 톤 기준 권장 = **BOTW 경쾌+무게감**.
