# 병사 입단비 · 용병 주간 고용비 설계 스펙

- 상태: 구현 반영 및 핵심 집중 EditMode QA 통과 (2026-10-13). 최신 전체 EditMode 731/721/10, 실패 이름 집합은 이전 전체 실행과 동일(새 실패 0, 해소 0). Play 미실행.
- 상위 계획: `.hermes/plans/2026-10-12-mercenary-payroll-ai-tax-difficulty-plan.md`
- 범위: 병사 입단비, AI 재고용 입단비, 용병 입단/주간 계약비, 지급 멱등성과 저장. 영지 세금/전쟁 상한은 상위 계획 참조.

## 1. 비용 정책

- 플레이어 병사 편입 시 병사 입단비를 1회 지불. 입단비는 `SoldierWageSystem` 산식으로 레벨·HP·스탯에 따라 산출되고 stableId/입단 순번 기준 ±15% 결정론적 변동이 있다.
- AI 시장 재고용도 동일 산식의 입단비를 적용하며, 플레이어 지갑이 아니라 해당 AI 영지 금고에서 차감한다. 비용 부족이면 roster 이전 및 고용 카운터 갱신을 하지 않는다.
- 일반 병사 일일 급료와 AI 영지 병사 일일 유지비는 기존 일일 처리로 유지한다.
- 용병 고용은 입단비 + 첫 주 계약비를 고용 시 함께 지불한다. 이후 `MercenaryData.hireCost`를 해당 용병별 주간 계약비로 7게임일마다 청구한다. 잔액 부족 시 용병 계약을 종료한다.
- 현재 카탈로그 주간비는 100/400/800/2000G 값이며 주간 단가로서의 경제 밸런스는 아직 Play/장기 경제 시뮬레이션으로 검증되지 않았다.
- 플레이어 용병은 카탈로그 내 고유 ID별 1명만 고용할 수 있으며, 기존의 임의 최대 10명 상한은 제거됐다. UI는 고용 수/서로 다른 카탈로그 ID 수를 표시한다.

## 2. 저장/로드

SaveData v5는 용병별 다음 계약 청구일/로스터 상태, 병사 입단 기록, AI 금고와 세금·유지비 멱등 마커, AI 시장 고용 통계를 저장한다. 또한 AI 시장에서 고용된 GuardPlaceholder만 stableId, 영지, 레벨, 스탯, HP, 생존 여부 등으로 저장·복원한다. 전투 proxy 결과(생존 HP/사망)는 정상 전투 종료 시 원본 AI 고용 roster에 반영하며, 포획된 원본은 AI roster에서 분리해 standalone 플레이어 병사로 등록한다.

기본 영지 수비병 전체가 개체 단위로 저장된다고 해석하면 안 된다. 구버전(v3/v4)에는 AI 고용 병사 엔트리가 없으므로 해당 roster를 빈 목록으로 처리하며, 레거시 경제 처리 marker는 현재 저장일 기준으로 시딩해 즉시 소급 청구를 막는다. 실제 저장파일 Play 왕복은 아직 검증되지 않았다.

## 3. 검증 상태와 남은 항목

- 최신 관련 집중 EditMode: 8개 fixture, 107/107 통과. XML: `/mnt/c/Unity/code/TestOutput/subagent_focused_editmode_20261013_031528.xml`
- Compile: 0 C# errors. Log: `/mnt/c/Unity/code/TestOutput/subagent_compile_20261013_031528.log`
- 전체 EditMode는 새 AI roster/proxy/v5 변경 이후 재실행되지 않았다. 이전 전체 결과는 722 total, 712 passed, 10 failed이며 기준선과 실패군 비교도 미확정이다.
- Play는 사용자 승인 전 미실행.
- 주간 용병 단가, AI 세금/유지비/고용의 장기 밸런스는 정적 공식만 살펴봤고 플레이로 확인하지 않았다.

## 4. 원장 태그

플레이어 편입/용병 결제는 `PlayerStats` 원장에 `soldier_hire_fee`, `mercenary_hire_fee`, `mercenary_contract_fee`, `mercenary_hire_refund` 태그를 쓴다. AI 재고용은 AI 영지 금고를 별도 장부로 직접 차감하므로 PlayerStats 지출로 표시되지 않는다.

## 5. 주의

Compile/집중 EditMode 통과는 전체 EditMode 통과나 Play/경제 밸런스 검증을 의미하지 않는다. 변경은 미커밋이며 기존 더티 파일을 포함한다. 커밋·Play는 별도 승인 게이트다.
