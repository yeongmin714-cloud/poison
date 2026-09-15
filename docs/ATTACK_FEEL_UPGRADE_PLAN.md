# ⚔️ 공격 액션감 + UI/장비 종합 계획 (ATTACK-FEEL + UI/EQUIP 통합 로드맵)

> **요구(사장님):** 테스트 18 검증 결과 반영 + 느낌표 축소 + 인벤창 해상도 비례 + 장비 착용 + 무기(검/창/활) 정확한 손 부착.
> **방식:** 기존 전투 체인을 폐기하지 않고 모션·타격·카메라·FX·사운드 + UI/장비 레이어로 보강.
> **이 문서는 2026-09-15 Phase E 계획을 신규 이슈와 병합한 통합 로드맵이다.**
> 최종 갱신: 2026-09-15 (52차)

---

## 0. 테스트 18 검증 결과 (의도 대비)

| 항목 | 판정 | 근거 |
|:--|:--:|:--|
| 콤보 3단 모션 개성화 (Phase B) | ✅ **일치** | 1타 횡베기 → 2타 대각 하강 → 3타 회전/오버헤드, wind-up→strike→recovery 존재, 전방 lean 확인 |
| 피격 리액션 (Phase C) | ❌ **미반영** | 타격 순간 적이 전혀 젖혀지지 않음(정지). HP바·데미지 숫자만 감소 → 원인 2건 확정: ① 경타 강도 과소(5°/0.05m — 육안 불가) ② 시각 자식 탐지 실패 시 적용 자체 스킵 |
| 느낌표 크기 | ❌ **과대** | 적 신장의 1/3~1/2 (사용자 리포트 일치) |
| 무기 손 부착 | ⚠️ **부정확** | 저해상 판독은 "부착됨"이나 사용자 육안은 "정확히 손에 없음" — bounds 그립부 휴리스틱이 블레이드 끝을 손잡이로 오판 가능 |
| 인벤토리 창 | ❌ **깨짐** | 해상도 변경 시 슬롯/텍스트 클리핑, 패널 좌측 밀림, 오버레이 불균일 |

### 이번 세션 즉시 수정분 (Phase F/G/H 일부)
- **Phase F(느낌표):** `MonsterAggroSystem` — `textMesh.characterSize = 0.30f` (기본 1.0 → 3.3배 축소), fontSize 48(글리프 해상도용). ✅
- **Phase F(플린치):** `HitReactionDriver` 강도 상향(경 9°/0.09m, 중 16°/0.22m, 크리 22°/0.32m) + 시각 자식 탐지 폴백 3단(Animator자식 → 이름힌트 → **렌더러 최다 자식**) + 실패 1회 로그. ✅
- **Phase G(인벤 비례):** `InventoryWindow` — `_uiScale = sqrt((W/1920)*(H/1080))` (HUD와 동일 산식), 레이아웃 상수 11종을 `_uiScale` 배수 프로퍼티로 전환(사용처 무수정 자동 비례), 로컬 상수 6종·폰트 15곳 스케일 반영, 해상도 변경 감지 시 스타일 재생성. ✅
- **Phase H(그립):** `WeaponEquipManager` — **피벗=그립부 신뢰 규칙** 추가(무기 GLB는 피벗이 손잡이 관례 → pivotT ≤0.15 또는 ≥0.85면 bounds 오프셋 보정 스킵, 테이블 튜닝 포즈 그대로 = 손에 정확히) + 타입별 `GripEnd(±1)` 강제값 지원. ✅ (추가 튜닝 여지)

---

## Phase E — FX를 모션 strike 프레임에 동기 (기존 계획 승계)
**파일:** `Systems/SlashVFXRunner.cs`, `Systems/HumanoidClipDriver.cs`, `Systems/CombatFXGate.cs`
- [ ] E-1 아크 발화 시점을 **클릭 즉시 → strike 프레임**으로 이동 (Phase B `StageCancelGate`와 같은 테이블 값 사용)
- [ ] E-2 임팩트(MagicHit) 스케일/수명 무기별 + 크리 1.5배
- [ ] E-3 킬 버스트 = `Travis Hit Impact` 전용 강화
- [ ] E-4 트레일 무기별 색(Phase A) + 콤보 스테이지 색 오버라이드 검증
- [ ] E-5 저사양 캡(프레임당 ≤10) 유지

## Phase F — 어그로/피격 시각 (이번 세션 대부분 완료)
- [x] F-1 느낌표 크기 축소(characterSize 0.30) — **Play 재판정 필요**
- [x] F-2 플린치 강도 상향 + 탐지 폴백 — **Play 재판정 필요**
- [ ] F-3 붉은 오라 강도/반경 재확인(과하면 축소), 느낌표 위치(2.5 → 종별 높이차) 보정
- [ ] F-4 넉백이 AI 이동/어그로와 충돌하지 않는지 확인(과하면 mag 하향)

## Phase G — UI 해상도 비례 (이번 세션 인벤토리 적용)
- [x] G-1 InventoryWindow `_uiScale` 도입 + 상수/폰트/로컬 스케일 전환 + 스타일 재생성
- [ ] G-2 **EquipmentWindow / LootWindow / WorldMapWindow / WarehouseUI 동일 적용** (창마다 자체 상수 보유 — 사용자 보고는 인벤이지만 나머지도 동일 취약)
- [ ] G-3 툴팁/드래그 고스트 좌표 클램프가 스케일 후에도 정확한지 검증(드래그 DnD hit-test)
- [ ] G-4 4:3 / 21:9 등 비16:9에서 패널 3분할(Screen.width/3)과 `_uiScale` 불일치 완화(비율 기반 클램프)

## Phase H — 장비 착용 & 무기 부착 (신규)
- [x] H-1 무기 그립: 피벗=그립부 신뢰 + GripEnd 강제값 (검/창/활 공통)
- [ ] H-2 **장비(방어구) 착용 동작 수리** — 우클릭/드래그 장착 경로 실측 로그로 실패 지점 판별(`[Inv] 우클릭 게이트` → `TryEquipItem` → `EquipmentManager.EquipItem` → `OnEquipmentChanged`)
- [ ] H-3 `ArmorVisualAttachSystem`(45차) 실제 부착 확인 — Helmet/Armor/Shoes/Gloves/Back 본 부착 + 해제
- [ ] H-4 장비칸 2×5 셀 클릭 해제/드래그 해제 회귀 확인
- [ ] H-5 그립 정밀 튜닝: Play 로그(`[Weapon] 그립 정렬(...)`)의 offset/pivotT 값으로 검/창/활 상수 미세조정

## Phase I — 사운드 4레이어 (기존 Phase F 승계 → I로 이동)
- [ ] I-1 스윙/임팩트/서브베이스/보이스 동기 (기존 `attack_swing`/`attack_hit` 재활용)
- [ ] I-2 무기별 피치/레이어 (창=금속, 대검=둔탁, 활=시위)
- [ ] I-3 히트스톱 중 임팩트 사운드 선행 정렬

## Phase J — 데미지 넘버 juice (기존 Phase G 승계)
- [ ] J-1 타입별 크기 5단×색(Phase A 완료분 확장) + 팝(1.35→1.0/0.15s) + 크리 아웃라인
- [ ] J-2 연타 시 넘버 스택(겹침 방지)·위치 오프셋
- [ ] J-3 초록 지형 대비 가독성(그림자/외곽선)

## Phase K — 신규 메커닉 (기존 Phase H 승계, 옵션)
- [ ] K-1 회피(롤) — `Roll_Dodge`/`Standing Dodge Right`, i-frame 0.25s
- [ ] K-2 차지 강공 — 홀드 → `Charged_Upward_Slash` + 오라 + 히트스톱 ×1.5
- [ ] K-3 발도/납도 — `Draw Sword 1`/`Withdrawing Sword`
- [ ] K-4 방어/패링 — `Sword_Parry_Backward_1` + `Sword And Shield Block`

## Phase L — 적 사망 다운 모션 (Phase C 잔여)
- [ ] L-1 `AnimalAI.Die()` 즉시 파괴 → 다운 클립/절차 눕힘 후 0.6~1.2s 지연 파괴 (전리품 타이밍 회귀 가드)

## Phase Z — 통합 검증 & 3중 저장
- [ ] Z-1 배치컴파일 CS=0 (**에디터 닫힌 상태에서** — 현재 에디터 점유로 잠김) + 정적 QA
- [ ] Z-2 Play 눈검증 체크리스트(§5)
- [ ] Z-3 QAPROGRESS + ROADMAP + 영구메모리 + git commit/push

---

## 5. Play 검증 체크리스트
1. [ ] 1/2/3타가 서로 다른 모션 (Double → Triple → Weapon_Combo_2) — **18차 통과**
2. [ ] 타격 시 적이 젖혀짐(flinch) + 강타/크리 넉백 — **18차 실패 → F-1/F-2 후 재검증**
3. [ ] 느낌표가 적 신장 대비 작게 표시 — **재검증**
4. [ ] 해상도 변경(창 크기 변경) 후 인벤/장비/전리품 창 텍스트·슬롯 비례 유지 — **재검증**
5. [ ] 방어구 우클릭/드래그 장착 + 캐릭터에 부착/해제 — **미검증**
6. [ ] 검/창/활이 손잡이에 정확히 부착 (로그 pivotT)·스윙 시 이탈 없음 — **재검증**
7. [ ] 카메라 펀치 히트/미스 강도 일치, 연타 차등 정상
8. [ ] 콘솔 로그: `[Combo] path=stageClip`, `[CombatFX] 피격 리액션 스킵`(뜨면 탐지 실패), `[Weapon] 그립 정렬(...)`

---

## 6. 실행 순서
```
[완료] F(느낌표/플린치) · G-1(인벤 비례) · H-1(그립 피벗 신뢰)
  ↓
G-2 (타 UI창 비례 확산) ─┬─ H-2~H-4 (장비 착용 수리)
H-5 (그립 튜닝)          └─ F-3/F-4 (오라·넉백 재조정)
  ↓
E (FX strike 동기) ─ I (사운드) ─ J (넘버 juice) ─ L (사망 다운)
  ↓
K (신규 메커닉, 옵션) → Z (통합 검증·3중 저장)
```

## 7. 리스크
| 리스크 | 대응 |
|:--|:--|
| 인벤창 스케일 전환이 드래그 DnD hit-test를 깨뜨림 | 마우스 좌표는 화면 px 그대로, 레이아웃만 비례 → 슬롯 Rect 캐시가 같은 스케일 상수 사용하므로 일관. G-3에서 검증 |
| GLB별 피벗 위치가 제각각 | pivotT 로그로 판별 → 필요 시 타입별 `GripEnd` 강제값(±1) 지정 |
| 플린치가 AI 이동/어그로 방해 | 시각 자식 트랜스폼만 조작(루트/AI 무관), 0.5s 이내 자동 복원 |
| 에디터 점유로 배치컴파일 잠김 | 에디터 포커스 시 자동 재컴파일 / 에디터 종료 후 배치 재검증 |
| 넉백 과다로 몬스터가 밀려남 | mag 상수(0.9/1.5) → Play 후 하향 |

## 8. 에셋 매핑(승계) — 상세는 `docs/ASSET_INVENTORY.md`
공격 모션(Meshy: Weapon_Combo_2 · Double/Triple_Combo_Attack · Right_Hand_Sword_Slash · Thrust_Slash · Charged_Upward_Slash / Mixamo: One·Two Hand Sword Combo · Standing Melee Combo Ver1·2 · Great Sword·Sword&Shield Slash · Draw/Withdraw Sword / DoubleL: 1Hand_Up_Attack_A1~3·B1~3 · Bow_Attack) · 피격/사망/회피(Meshy Hit_Reaction·Slap·Electrocution·Dead / DoubleL Hit_F_1·2 / Mixamo Standing React Small·Large · Great Sword Impact · Dying·Falling Back Death · Roll_Dodge·Standing Dodge Right) · FX(StylizedSlash·MagicHit·WeaponSwingTrail·Travis Hit) · 사운드(기존 attack_swing/attack_hit 재활용).