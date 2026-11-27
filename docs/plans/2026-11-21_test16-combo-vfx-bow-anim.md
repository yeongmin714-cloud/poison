# [168차] 공격씬 테스트 16 피드백 — 피격 백색화·스윙 타이밍/속도·콤보 클릭 재절단·활 조준/사거리·활쏘기 애니 교체

상태: **계획 승인 대기** (구현 0건)
참조 영상: `Screenshots/공격씬 테스트 16.mp4` (57.1s — 프레임 추출 완료. ⚠️ vision 프로바이너 미설정으로 프레임 유의 판독은 픽셀 포렌식+배치 프로브로 대체 — 유의 판독 복구 원하면 `hermes setup` 재실행)
신규 에셋: `Assets/플레이어 애니메이션/플레이어 활쏘기.fbx` · `Assets/Animations/병사 활쏘기.fbx` (둘 다 Take 001 실측)

---

## 사용자 요구 재확인 (이해 설명)

1. **피격 이펙트를 흰색으로** — 167차에서 Magic Hit 2 에셋 원형(보라)으로 복원했으나, 화이트 틴트로 변경. 스윙 아크(흰)와 통일.
2. **스윙 슬래시 VFX가 스윙에 비해 살짝 늦게 터진다** — 아크 발화 타이밍을 스트라이크(데미지)보다 앞당겨 블레이드 궤적과 일치시킴.
3. **휘두르기 클릭 재구성** — 지금 1클릭에 휘두르기가 2회 이어지는(혹은 세그먼트가 동작과 안 맞는) 느낌 → **클립을 실제 휘두르기 동작 경계에서 재절단**: 클릭 1회 = 휘두르기 1회 후 보유, 0.85초 내 클릭 2회 = 전체 휘두르기.
4. **휘두르기 애니메이션 속도 소폭 상향** — 공격 상태 재생 배율 상향(≈1.2×).
5. **활 조준선 = 실제 커서 일치** — 화살 발사 시 조준 표시가 커서와 어긋남 → 단일 좌표 소스로 정합.
6. **화살 사거리 상향** — 발사 속도 상향.
7. **활쏘기 애니메이션 교체** — 플레이어: 신규 "플레이어 활쏘기.fbx"로 교체 / 병사(아처): 신규 "병사 활쏘기.fbx"를 사격 동작에 부착.

## 실측 근거 (읽기전용)

- **피격 색 경로**: PlayImpactMulti는 167-A로 에셋 원색 복원됨. 흰색화 = 45차 BOTW 선례인 `TintParticles(instance, CoreTint 흰)` + `NormalizePurpleParticles` 재투입(두 함수 모두 생존 — 스윙 경로 사용 중). URP 정규화 안전망 유지.
- **스윙 VFX 타이밍**: `HumanoidClipDriver.MonitorMeleeComboClip`이 normT가 `(segment + ComboSegmentStrikeNormT=0.32)/3`을 넘을 때 FireComboSlash 발화(L1631) — **VFX가 데미지 스트라이크와 같은 타이밍에 묶여 있음** → VFX 트리거를 스트라이크에서 분리해 조기 발화(별도 상수).
- **콤보 구조**: `MeleeComboStateMachine` — Weapon_Combo_2 클립(휘두르기 2회 저작)을 클릭 수/3로 절단(1/3·2/3·1), 보유-재개 구조, `MeleeComboClickWindow=0.85s` 이미 존재(L131), `MaxMeleeComboClicks=3`(L128). 3절단이 실제 2동작과 어긋나는 게 "1클릭=2휘두르기" 체감 유력 원인. 재절단 경계는 클립 실측으로 확정(아래 P1).
- **스윙 속도**: `Player_AC.controller`의 Sword 클릭 세그먼트 상태(Weapon_Combo_2 guid cf0d6240...) 전부 `m_Speed: 1` → 상태 배율 상향이 레버(RegisteredMeleeComboTests는 클립 guid 등가 계약이라 배율 변경 무충돌).
- **활 조준 경로**: OS 커서는 숨김(CursorVisibilityController) → `UTKCursorOverlay` 스프라이트가 마우스 중심 앵커로 표시(L99-100). 활 리티클(`BowAimReticleUTK`)은 `ArrowManager.GetAimScreenPoint()`=**raw 마우스 픽셀** → ScreenToPanel. 발사 방향은 리티클 화면좌표의 카메라 레이(TrySolveAim — 머즐 페럴럭스 무시). 즉 좌표 소스는 같은데도 어긋난다면 ①두 UI의 패널 스케일/앵커 차이 ②커서 스프라이트 핫스팟(끝점≠중심) ③화살 중력 낙하(GavityScale 수동, L160) 체감 분리. → 드로 상태 RT 캡처 1프레임(오버레이+리티클 동시) 픽셀 오프셋 측정으로 확정 후 수리.
- **화살 사거리**: `ArrowProjectile.BaseSpeed=84`(L16), 속도=84×(0.7+0.5×power), 수명 10s, 중력 수동 스케일(L157-160) — 사거리 레버=BaseSpeed 상향(예: ×1.2 → ~100).
- **활쏘기 배선**: 플레이어 = 2단계(Draw 홀드 → ArcheryShot 릴리즈 트리거, HumanoidClipDriver:377-384/1047-1053) — Player_AC의 ArcheryShot/Draw 상태 클립 guid 교체. 병사 = **SoldierArcher_AC.controller 존재**(Controllers/, 15 상태) — 사격 상태 클립을 "병사 활쏘기" guid로 교체. 기존 병사 아처 클립 guid 실측 후 스왑.

## 계획

- **P0 — FBX 수용 감사**: fbx_audit 파서 검증 2종(테이크/본 계층/휴머노이드 리그/클립 길이) + 기존 Archery_Shot·병사 사격 클립과 길이 대조. 실패 시 원본 보존+보고.
- **P1 — 전투 체감 묶음(콤보+VFX+속도)**: ①세그먼트 재절단 — 실측 프로브(스윙 클립 고정재생 프레임 캡처로 동작 경계 normT 확정) → `MaxMeleeComboClicks=2` + 경계 상수(초기 0.5) + strike 재계산 ②FireComboSlash 조기 발화(분리 상수, 초기 strike보다 0.12~0.15 앞) ③공격 상태 m_Speed 1.2. 계약 갱신: playbackLimit 1/3·2/3·1.0 계약(MeleeClickOwnership류)·RegisteredMeleeComboTests 세그먼트 수 · 영상 프레임 모션-에너지 실측(클릭당 스윙 수 검증).
- **P2 — 피격 백색화+활 정합**: ①PlayImpactMulti에 TintParticles(흰)+NormalizePurple 재투입(167 계약 갱신 — 소멸 어서션은 유지, 화이트 틴트 신규 마커) ②조준 오프셋 진단 캡처 → 수리(단일 소스/핫스팟 정렬/필요 시 머즐 정렬 발사 옵션) ③BaseSpeed 상향.
- **P3 — 활쏘기 애니 교체**: Player_AC ArcheryShot(+Draw 필요 시) 클립 guid → "플레이어 활쏘기" / SoldierArcher_AC 사격 상태 → "병사 활쏘기". 컨트롤러 YAML 편집+테스트씬 패리티 게이트(125차: 전 씬 본씬 동일 애니) 준수.
- **P4 — 검증**: 컴파일 CS0 + EditMode 전수(baseline 10 부분집합 대조, 신규 0) + 프로브 캡처(스윙-아크 정합 1프레임/드로 조준 1프레임/피격 백색 1프레임) → **사용자 Play 일괄 판정**. 커밋 별도 승인.

## 판정 지점 (구현 중 확정)
- 콤보 경계 normT(프로브 실측값)와 VFX 조기량(0.12~0.15)은 Play 판정 후 트림.
- 조준 수리 방식은 진단 캡처 결과로 선택(단일 소스 정합 vs 핫스팟 보정 vs 발사 정렬).
- 스윙 속도 1.2 체감은 Play 판정 후 트림(1.1~1.3).

## 추가 항목 (11-21 2차 사용자 요구)

- **[P2+] 피격 임팩트 축소**: `ImpactBaseScale 2.5 → 1.6` 초기값(166차 무가시 1.0과 유가시 2.5의 중간 — 백색 룩은 가독성이 높아 축소 가능). Play 판정 후 1.4~2.0 트림.
- **[P5] 말(갈색말) 자세 수리**: "네발로 안 서고 이상하게 선다" — Idle 클립은 말 전용(`Assets/Animations/말/갈색말 대기.fbx`)이라 소스 불일치가 아니라 **본경로/아바타 정합 후보**: 백마.glb 본이름은 Blender식(Root/spine.001-006/neck.001-004 실측) — FBX 클립 본경로 일치 여부 확인 필수 → 미일치면 클립 미바인딩으로 GLB 바인드 포즈 그대로 서 있는 것. MountSystem.anim 바인딩(L110-117) + Idle 캡처 프로브로 확정 후 수리(본경로 리매핑/아바타 교정/클립 재임포트 중 선택).

## [169-P6] 공격 범위 호(arc) 텔레그래프 + 구르기 회피 (11-21 3차 사용자 요구)

- **요구 재확인**: ①몬스터/병사 공격 시 공격 범위 표시 ②플레이어 구르기로 회피 ③몬스터 적색 링 삭제(느낌표만 유지) ④현재 공격 시 나타나는 반투명 링을 공격범위 표시로, 원→호.
- **실측 근거**:
  - 적색 링 = `AttackTelegraphIndicator`(반투명 링 쿼드, Elite/Normal 적색 2종) — `AnimalAI.cs:1019`·`AfterlifeEnemy.cs:195`에서 windup(0.5s) 동안 발밑 원형으로 표시.
  - 느낌표 = [무번호 19-E1] `AttackTelegraphIndicator.SpawnAlert` — Alert 진입 시 발밑 "!" 링(3s 게이지) **이미 존재 — 유지 대상**.
  - 타격 판정 = windup 후 "거리 재확인" **원형 판정** (AfterlifeEnemy.MeleeTelegraphRoutine 실측) — 호 표시와 정합하려면 판정도 부채꼴 필요.
  - 구르기 무적 = **이미 존재**(`PlayerHealth.ApplyDamage` — C21-02: IsRolling 중 피해 무시) — 신규 구현이 아니라 전 공격 소스 커버 감사만 필요.
  - 병사 = GuardCombatAI에 텔레그래프 배선 0건 — 신규 배선 대상.
- **작업**:
  - **A1 호 텔레그래프**: `TelegraphArcIndicator` 신설 — 절차 부채꼴 메시(중심각/반경 파라미터, TelegraphRing 재질 계열 반투명 펄스, windup 0.5s 동안 표시). 몬스터 공격 원형 링 발화선(AnimalAI:1019·AfterlifeEnemy:195)만 교체 — SpawnAlert("!" 링) 무수정.
  - **A2 판정=표시 정합**: 몬스터 근접 타격을 부채꼴 판정으로 변경 — `dist ≤ attackRange && 전방 중심각 ±60° 내` (초기값, Play 트림). 몬스터는 타격 시점 플레이어 방향을 바라보므로 호가 항상 플레이어 쪽을 향함.
  - **A3 병사 동일 적용**: 병사 근접 타격 지점(GuardCombatAI/병사 공격 경로)에 동일 호 텔레그래프 + 부채꼴 판정 배선.
  - **A4 구르기 회피 보증**: 기존 C21-02 롤 무적 유지 + 몬스터/애프터라이프/병사 전 타격 경로가 ApplyDamage 공통 경유 확인(우회 경로 발견 시 동일 무적 게이트 배선). 회피 전술 2종: windup 0.5s 내 호 밖 이동, 또는 타격 순간 롤(무적).
  - **A5 검증**: 호 렌더 캡처(정면/측면) + 판정 정합 프로브(호 안=피해·호 밖=무피해·롤 중=무피해) + EditMode — Telegraph 계약테스트 갱신(원형 링 계약 → 호 계약) + AttackTelegraphIndicator 본체 무수정(SpawnAlert 유지).
- **판정 지점**: ①호 색상 — 기본안: 웜 화이트/골드 반투명(공격 통일색 계열, 위험도 무관). 적색 유지 원하면 판정. ②호 중심각 초기 ±60°. ③"느낌표만 남긴다" = Alert "!" 링 유지 확정(공격 링은 호로 대체되므로 결과적으로 느낌표만 원형으로 남음).
