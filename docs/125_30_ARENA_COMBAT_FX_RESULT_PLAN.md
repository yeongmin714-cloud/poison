# [125차 후속30 계획] 결투 타격감(피격 이펙트+넉백 애니) + 투기장 승/패 결과 UI + 골드 정산 연동

> 상태: **계획 수립 — 구현은 사용자 승인 후.** 사용자 요구 3종:
> ①병사끼리 피격 이펙트+넉백 애니메이션 ②Figma의 투기장 승리/패배 UI를 로비 복귀 직후 표시 ③승리 골드 획득/패배 골드 차감 실구현.

---

## 현 상태 실측 (확정 — 재검증 불필요)

| 항목 | 실측 |
|---|---|
| 골드 정산 | **이미 구현** — 베팅 확정 시 `SpendGold(amount, "arena_bet")` 즉시 차감(L184) / 승리 시 `AddGold(payout, "arena_bet_payout")`(L334) / 패배 시 원금 소실(반환 없음) = Figma 패배 문구 "패배 시 베팅 원금은 반환되지 않습니다"와 정확 일치. 전환 실패 시 환불 경로 존재(L269) |
| 정산 이벤트 | `OnDuelSettled(승자명, payout, playerWon)` 존재(L79/L338) — **구독자 0건**(미사용). `OnRoundEnd`도 UI 미구독 |
| 피격 이펙트 표준 경로 | **CombatFXGate.PlayHitFX** — 히트 플래시+스파크+임팩트+출혈+데미지넘버+카메라 히트 일괄(플레이어 피격 L296 실사용 경로). 시그니처 2종(target pivot / target+hitPos 정밀) |
| 피격 반응 애니 | HumanoidClipDriver 공개 트리거 존재: `TriggerHitLight()`(HitLight) / `TriggerStun()`(Stun) / `TriggerKnockdown()`(Knockdown). 데미지 티어 계약 선례: ≥40 Knockdown / ≥25 Stun / 그 외 HitLight(ProceduralAnimStateMachine L248~253) |
| 결과 UI 디자인 | Figma 확인 완료 — **투기장 승리 결과 280:163 / 투기장 패배 결과 280:381**(1600×1040 풀스크린 오버레이, 좌판 480×882 승·패 선수 / 우판 968×882 정산). Accent: 승리 (0.35,0.65,1.00) / 패배 (0.94,0.47,0.47) / 골드 (0.91,0.75,0.44) |
| 결과 창 골격 | ArenaWindowUTK와 동일 골격 재사용 가능(UTKWindowBase — Frameless+투명 루트+raw 1600×1040+ApplyCanvasFit 등배수, UI→Systems 직접 참조 허용 실측: `_pair = ArenaBettingSystem.OfferPair()`) |
| 초상화 | ArenaWindowUTK.RenderFighterPortrait 존재(팀색 배경 렌더텍스처 캐시) — 결과창 "선수 사진" 재사용 대상 |
| 로비 복귀 | 듀엔 종료 → 선수 파괴 → `EnterBuildingViaReflection(TYPE_ARENA)`(L351) — 로비 준비 완료 시점 훅 없음(신설 필요) |

---

## 수리 항목 (판정 기준 사전 정의)

### K1. 피격 이펙트 — CombatFXGate 표준 경로 연결
- AttackRound의 **타격 순간**(돌진 접촉 시점)에 `CombatFXGate.PlayHitFX(defenderGO, hitPos, dir, CombatHitType.Organic, isCrit=false, damage, Color.white)` 발화.
- 데미지넘버=실제 라운드 데미지 값 — 피격 숫자가 전투 로그와 일치.
- 신규 VFX 제작 없음 — 본씬 전투와 동일 이펙트 언어(사용자 계약 "테스트씬도 본씬 동일").
- **판정**: 재Play 영상에서 타격 순간 스파크/플래시+데미지넘버 출력.

### K2. 넉백 애니메이션 — 티어 계약 재사용 + 트랜스폼 물리 없는 밀림
- 피격 반응 티어(선례 계약 동일): damage ≥40 → TriggerKnockdown / ≥25 → TriggerStun / 그 외 TriggerHitLight.
- **사전 실측 항목(구현 첫 단계)**: SoldierShield_AC에 HitLight/Stun/Knockdown 상태 존재 실측(런타임 애니 상태 로그 or 에디터 덤프).
  - 미존재 시 폴백: 트리거는 무해(SetTrigger 미정의 트리거 무시) + 트랜스폼 밀림만으로 타격감 유지. 신규 클립 제작 없음.
- 넉백 이동: 트랜스폼 구동 코루틴(물리 금지 — G3 kinematic 계약 유지). 공격자 반대 방향으로 티어별 거리(HitLight 0.25m / Stun 0.4m / Knockdown 0.7m), 0.12s 밀림+0.2s 복귀(원 위치 복귀 — 다음 라운드 간격 1.6m 계약 유지).
- 회피(dodged) 시: 이펙트/넉백/데미지넘버 모두 무발화 — "회피!" 표시만(기존 로그 유지).
- **판정**: 타격 피격자가 밀렸다 복귀 + 티어별 반응 애니(상태 실측 통과 시).

### K3. 타격 순간 판정 재구성 (수치 불변)
- 현재: 라운드 시작에 데미지 확정→HP바 즉시 반영→돌진은 장식. 변경: 데미지 산식은 동일하되 **적용 시점을 돌진 접촉(0.25s)으로 이동** — 접촉 순간 데미지 확정+CombatFX+넉백+HP바 갱신.
- 라운드 간격/확률/데미지 산식 무변경 — 타이밍만 이동(플레이어 체감 개선 목적).
- **판정**: HP바가 타격 순간에 떨어짐 + 피격자 반응 동기화.

### K4. 승/패 마무리 연출
- 최후 타격으로 HP 0 → 패자 TriggerKnockdown + 다운 상태 유지(관람 유예 2.5s와 정합), 승자 Idle.
- 듀엔 종료 후 기존 파괴/로비 복귀 흐름 무변경.

### K5. 투기장 승/패 결과 UI — ArenaResultWindowUTK 신설 (Figma 패리티)
- 신규 `ArenaResultWindowUTK`(ProjectName.UI.Toolkit) — ArenaWindowUTK 동일 골격(1600×1040 raw+등배수, 창박스=figma×k·글래스0.85 계약).
- **Figma 단일소스**: 280:163(승리)/280:381(패배) — 구현 전 노드 재실측(세션 간 개정 계약). 변형 2종은 레이아웃 공유+Accent/문구만 분기.
- 좌판(480×882): 패널 제목("경기 결과"/"2인 난투 · 경기 종료"), 선수 사진 432×514(RenderFighterPortrait 재사용 — 접근자 internal 승격), 승/패 표시 배지, 승(패) 기록=통산 전적(ArenaBetFighter.RecordText — OnDuelSettled 시점에 이미 갱신 완료 실측), 상대 선수 카드.
- 우판(968×882): 패널 제목("전투 결과"/"2인 난투 · 승리(패배) 베팅 정산"+닫기 30×30), 승/패 안내 배너, 획득(수령) 골드 박스(금색 골드 문장+금액+지급 상태), 배당금 계산(베팅금 × 배당 = 수령액 — 실제 값), 골드 정산 내역(베팅 차감→정산→최종 보유 3~4줄), 결과 확인 행(정산 요약+"투기장으로 돌아가기" 244×44), 하단 지급 안내 문구(Figma 원문 사용).
- 입력: 닫기 버튼+"투기장으로 돌아가기" 버튼+Enter(확인)/Esc(닫기) — Figma 도움말 준 호환.
- **판정**: 승리 시 파란 Accent 창 / 패배 시 빨간 Accent 창 — Figma 렌더와 육안 정합.

### K6. 로비 복귀 직후 개방 타이밍
- ArenaBettingSystem에 정산 페이로드 보관 + 신규 이벤트 `OnResultReady(승자명, 승패여부, 베팅금, 배당, payout, 최종골드)`.
- 발화 시점: `EnterBuildingViaReflection(TYPE_ARENA)` 호출 후 **로비 준비 폴링**(투기장 룸 소멸+로비 룸 존재, 상한 10s — 초과 시 즉시 발화 폴백) → 결과창이 전환 도중에 뜨지 않음을 보장.
- ArenaResultWindowUTK가 OnResultReady 구독(UI→Systems 직접 참조 — ArenaWindowUTK 선례). 기존 토스트(투기장 관람 중 표시)는 유지 — 역할 분리(관람 중 요약 vs 로비 정산 상세).
- **판정**: 로비 화면 안정 직후 결과창 표시(전환 중 미표시).

### K7. 골드 정산 — 실측 확인 + UI 연동 (신규 로직 변경 없음)
- 이미 구현된 선행 차감/승리 지급/패배 원금 소실을 실측 확인(로그 3종: arena_bet/arena_bet_payout/잔액) — 사용자 요구 "실구현"은 이미 충족 상태로 판정.
- 결과창 정산 내역이 실제 PlayerStats 값 기반으로 표시(베팅 차감 전 잔액→차감→지급(또는 소실)→최종 보유) — 창의 숫자가 로그와 1:1.
- **판정**: 승리 후 골드 증가액 = payout, 패배 후 = 베팅금 그대로 소실 — UI 숫자와 PlayerStats 일치.

---

## 검증 절차 (재Play 1회)
1. 듀엔 중: 타격마다 스파크/플래시/데미지넘버 + 피격자 밀림+반응 애니 + HP바 접촉 시점 갱신
2. 종료: 패자 다운 유지 → 로비 복귀 → 결과창(승=파랑/패=빨강) 표시 — 정산 내역=로그 일치
3. 골드: 승리 시 증가액=payout / 패배 시 원금 소실 — PlayerStats 실측
4. 로그: 예외 0 / EditMode baseline 1:1 / SoldierShield_AC 상태 실측 로그

## 열린 질문 → 해결 (사용자 답변, 2026-10-25)
- "투기장으로 돌아가기" 버튼: **사용자가 Figma를 직접 수정 — 버튼 문구 "확인"으로 변경(레이어명은 잔존), 창 닫기 동작 확정.** 구현은 텍스트 "확인"+Hide()로 반영(재실측 기준).
## 비고
- 전체 워킹트리 미커밋 상태 유지(기존 계약). Play/커밋은 별도 승인 게이트.
