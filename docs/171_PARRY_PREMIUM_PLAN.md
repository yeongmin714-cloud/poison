# 171차 — 패링 고품질화 계획

> 사용자 요구: "패링 구조 확인하고 더 고품질화". 품질 참조 = 젤다(판정 명료성·플러리러시 힌트) / 세키로(반격 리듬) — 사용자 선호 계열. 본 문서는 계획만(구현 미착수).

## 1. 현황 실측 (2026-11-22 판독 근거)

- **입력**: V키(`PlayerCombat._parryKey` L81 — "KeyBindings에는 전투 액션이 없어 인스펙터에서 변경 가능"). `HumanoidClipDriver` L399의 "좌클릭 패링" 주석은 구시스템 잔재(실측 V키) — 정정 대상.
- **창**: ParryWindow 0.45s(unscaled) / PERFECT ≤ 0.15s 창 경과(`EvaluateParryGrade` 순수 함수) / GOOD 그 외. 창 중 좌클릭 3회 큐잉 + Bow 보류.
- **판정 경로**: PlayerHealth.TakeDamage 진입점 → `IsParryEligibleDamageType`("executed" 등 시스템 타입 제외) → `TryParryReflection()`(**리플렉션** — MethodInfo 캐시 없음, hot path) → `PlayerCombat.TryParry()` → 림 해제 → 런타임 연출.
- **성공 연출 (95차 F1~F4)**: 히트스톱(scale 0.05·0.05s), FOV 펀치 45→38(0.08s)+복원 0.25s, 셰이크 0.25, 공격자 백색 플래시(AnimalAI.PlayParryFlash), ParryArcWisps(흡수점 y+0.9 지면 기준), 방어 림(GuardRim 1.6), ParryFlashFader 화면 플래시, BulletTime 0.35/0.10/0.38, **PERFECT = 큐 해방 첫 1타 1.5×** / GOOD = 경계 통과 청색 틱 1회.
- **애니**: Player_AC 'Parry' 트리그 → MeshyUser Sword_Parry_Backward_1 슬롯(+Mixamo Block/Slash 리소스 키 확보)·Speed 0 홀드 0.45s.
- **적 텔레그래프**: TelegraphWindupSeconds 0.5s(+카드 보너스) — 예고 존재.
- **ParadoxTimeStop**: PERFECT 패링 타임스톱 — Test_10 한정 opt-in(전역 미확장).
- **⚠ 최대 공백 — SFX 전무**: PlayerCombat Audio 0건, AnimalAI는 hit/death만. 패막 성공/실패/입력 전부 무음.
- **기존 테스트**: ParryGauge_61Tests·ParryWindowVFXPhasePWTests·PlayerCombatParryInputTests 3파일 존재.

## 2. 품질 공백 → 개선 방향 (우선순위)

| # | 공백 | 방향 | 우선순위 |
|---|---|---|---|
| 1 | 패링 무음 | SFX 4종(성공 PERFECT/GOOD·입력·실패) | **P0** |
| 2 | 스파크가 지면 기준 | 적 무기 팁 접촉점 기준으로 격상 | P1 |
| 3 | PERFECT→반격 리듬 부재(큐 해방만) | 스텝인+반격 애니(세키로식) | P2 |
| 4 | 패링 타이밍 학습 곡선 | 텔레그래프 임팩트 마커(젤다식 수축 힌트) | P3 |
| 5 | 키맵 미등록·리플렉션 hot path | KeyBindings 등록·MethodInfo 캐시 | P4 |

## 3. Phase 분해

### Phase A — 패링 SFX (P0, 최대 공백)

- **A1** 소스 수용: CC0(Kenney Impact/Science 등 — 사운드는 크레딧 1줄 계약) → `Assets/Audio/Parry/` 수용+meta 규약 동일. (사용자 제공 선호 시 대기)
- **A2** 재생 경로: `PlayerCombat` 성공 흐름에 SFX 4종 — PERFECT(고음 클랭·긴 decay), GOOD(저역 클랭), 입력 스윙(패링 시작), 실패/빈패링(낮은 스윕). 재생은 `AudioSource.PlayClipAtPoint` 선례(AnimalAI L1314) 준용.
- **A3** 파일: `PlayerCombat.cs`(재생 배선) + 신규 `ParrySfx.cs`(클립 로드·선택 헬퍼 — 정적 캐시).

### Phase B — 임팩트 싱크 (P1)

- **B1** 접촉점 격상: 패링 성공 스파크/위스프 위치를 지면 흡수점(y+0.9) → **적 무기 팁**(공격자 전방 끝단 — `MeleeSweepFallback`로 획득한 공격자 위치+전방 1m) 기준. 기존 arc wisps 계약과 병존(접촉점 메인+지면 링 보조).
- **B2** GOOD 분리 강화: 청색 틱(기존) + 소형 스파크(PERFECT 대비 0.6배 — 타격 히트스톱 0.6배 선례 정합).

### Phase C — 반격 리듬 (P2 — 확인 포인트 1)

- **C1** PERFECT 성공 → **스텝인 0.4m**(공격자 방향, 0.1s — 넉백 역방향 kinematic jolt 선례 준용) + Mixamo Slash 반격 애니 트리거. 첫 타 1.5× 배율 계약 유지. **발사는 수동 큐 유지**(자동발사 아님 — 기본값).
- **C2** 창 중 큐잉 힌트: 성공 시 "반격 준비" 큐 표시(UTK 토스트/게이지 계약 — 상호작용불가 피드백 필수 정합).

### Phase D — 텔레그래프 임팩트 마커 (P3)

- **D1** TelegraphArcIndicator에 수축 링+임팩트 순간 점등(0.5s windup 동안 반경 수축 → 0점 점등) — 패막 PERFECT 창(입력~0.15s)과 임팩트 시각의 대응을 플레이어가 학습 가능하게.
- **D2** 적별 windup 계약(0.5+보너스) 유지 — 마커는 windup 종료 시각에서 파생(하드코딩 금지).

### Phase E — 정리 (P4)

- **E1** KeyBindings에 패막 키 등록(V) — 충돌 셀프감사 완료: Q(퀘스트)·Shift(구르기)·더블탭WASD·F7~F9와 무충돌.
- **E2** `TryParryReflection` MethodInfo 캐시 — hot path 리플렉션 1회 탐색 후 정적 캐시(선례 경로 유지, Core→Systems 제약 유지).
- **E3** HumanoidClipDriver L399 "좌클릭 패링" 구주석 → V키 정정.

### Phase F — 테스트·QA

- **F1** 테스트: SFX 소스 계약(클립 존재·등급별 선택), 접촉점 계약(소스 매칭), 반격 스텝 소스 계약, 텔레그래프 마커 소스 계약, KeyBindings 등록, MethodInfo 캐시. 기존 패링 테스트 3파일 회귀.
- **F2** QA: 컴파일(error CS=0) + EditMode 전수(기존 실패 20건 baseline 대조) + **Play 투어(별도 승인)** — PERFECT/GOOD/실패 3시나리오 캡처+사운드 확인.

## 4. 사용자 확인 포인트 (무응답 시 기본값)

1. **반격 자동발사**: PERFECT 시 첫 타 자동 발사 vs 수동 큐 유지 — 기본: **수동 유지**(스텝인+애니만 추가).
2. **SFX 소스**: CC0 무료 수용+크레딧 vs 사용자 제공 — 기본: **CC0 수용**.
3. **타임스톱 전역 확장**(ParadoxTimeStop Test_10 → 전战斗) — 기본: **미확장**(로컬 히트스톱 유지).

## 5. 완료 기준 (전체)

- 패링 4이벤트(입력/PERFECT/GOOD/실패) 전부 청각 신호 보유.
- 성공 스파크 = 적 무기 팁 접촉점 기준 + GOOD 소형 분리.
- PERFECT 스텝인+반격 애니(발사 계약 유지).
- 텔레그래프 임팩트 마커(windup 파생).
- KeyBindings 등록+리플렉션 캐시+구주석 정정.
- 컴파일 0 + EditMode 신규실패 0 + Play 캡처(승인 후).

## 6. [사용자 확정 2026-11-22] 팔레트 통일 + 보유 VFX 에셋 통합

> 사용자 지시: "색상을 통일하는 게 더 좋을 것 같애" — 패막 FX 색상을 **금색 단일 팔레트**로 통일한다.

- **색 원칙** [사용자 확정 2차 2026-11-22 — 흰색]: 패막 전 채널 = **흰색 단일**(FXPalette 신설 `ParryWhite ≈ (1,1,1)` — 알파는 용도별). 기존 `ParryGold(1,0.95,0.55)`는 패막 경로에서 전량 교체 — 팔레트 정의는 회귀 대비 보존하되 패막 경로 미사용(ParryCyan 동일 취급). **감사: ParryGold 사용처 9개소**(PlayerCombat L495·L826·L1269·L1306·L1361·L1371·L1383·L1920 + HUDUTK L168) 전부 ParryWhite 갱신 대상. `AnimalAI` 등 "패막 금색" 주석도 정정 목록.
- **VFX 에셋 틴트**: Hit_01 flash/Guards_01·Holy Circle = 흰 틴트, Sparks = **`Sparks white`** 채택(yellow 폐기). 명중(적 공격) 붉은 지면링과의 흰/적 대비는 유지 — 오히려 흰색이 더 선명.
- **등급 구분 = 색이 아니라 강도**: PERFECT = 섬광/링/스파크 크기 ×2·지속 ×1.5 + 기존 히트스톱/FOV 펀치/셰이크. GOOD = 소형·단명(타격 히트스톱 0.6배 선례 정합).
- **창 중 타이밍 큐 재설계**: 기존 [36-PW3] 림 골드→청 전환 폐기 → 림 **흰 유지** + GOOD 진입 시 **림 맥동(펄스)+수렴 링 1회**로 경계 신호(크기·명도 기반). HUD 게이지도 흰 단일.
- **보유 에셋 통합(이미 수입 — 추가 비용 0)**:
  - 찰나 클랭 섬광 = Travis `Guards/Guard_01` 1차 검증 / `Hits/Hit_01`(X자 플래시) — 접촉점(적 무기 팁) 배치, 흰 틴트.
  - 확산 링 = Hovl `Holy hit`의 Circle — PERFECT 흰 대형 / GOOD 흰 소형.
  - 튀는 스파크 = Hovl `Sparks flashing yellow/white` — 접촉점 분출.
  - 지면 링 보조 = Free Slash `Impact` 시트 — 기존 절차 붉은 지면링은 명중(공격) 전용으로 역할 분리.
  - 절차 ShockwaveRingFX는 폴백(46차 asset-only FX 도ctrine — 메인 연출 에셋 전환).
- **감사 결과(청색 사용처 전수)**: PlayerCombat L785(수렴링)·L842(림 전환)·L846(틱 링)·HUDUTK L565(게이지) — 4개소 전부 금 단일로 갱신 대상.
- 테스트: 기존 [36-PW3] 청 전환 어서션은 소멸 예정 문자열 — 통일 계약(금 단일+맥동)으로 갱신.
