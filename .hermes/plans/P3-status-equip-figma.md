# P3 — 캐릭터 상태/장비 창 Figma 재구성 (UI-F-COMPLETE)

> 2026-09-27 — "피그마에 맞게 게임 로직 고친다" 원칙(사용자 확정). P1(상점6탭)·P2(병사3열) 완료 후속.

## Figma 구조 (character-status-panel / equipment-inventory-panel 실측)
```
[PanelHeader] 상태 정보 / CHARACTER STATUS  [닫기]
[Identity] RarityRow(등급+클래스) + 캐릭터명
[Portrait] 초상화(3D 뷰포트 placeholder) + HP 오버레이
[Stats]
  ├ [LevelBlock] LEVEL + <값> + EXP <%%>
  └ [CoreStatsGrid] 4행: 공격/방어/최대체력/민첩 (이름 | 값 | 구분선)
[SpecialStatsSection] 특수 상태 게이지 2개
  ├ 호감도(Affection)  게이지 + Lv — 플레이어용 실존 게이지로 대체
  └ 중독도(Toxicity)   게이지 + % + 상태명
```

## 게임 매핑 (게임 로직 소스 유지 — 표시 구조만 Figma로)
| Figma 요소 | 게임 소스/API |
|:--|:--|
| 캐릭터명 | PlayerStats (플레이어명 시스템 없음 → 칭호/레벨로 대체) |
| 등급/클래스 | TitleManager.GetTitleText / PlayerStats.Level |
| LEVEL/EXP | PlayerStats.Level / CurrentEXP / GetExpForLevel |
| 공격/방어/최대체력/민첩 | FinalAttackDamage / FinalDefense / HPBase / FinalMoveSpeed |
| 특수상태 게이지 1 (호감도 자리) | HungerSystem.Hunger/MaxHunger (플레이어 실존 "허기") |
| 특수상태 게이지 2 (중독도) | DrugEffectSystem.DrugAddictionLevel / GetAddictionLabel |

## 보존 (손대지 않음)
- 주스탯 4종 분배(힘/민첩/지능/체력 + [+] 버튼, 남은 포인트) — 로직 보존
- 장비슬롯 6개 + 장비보너스 내역 — 로직 보존
- 칭호 변경 / 감사 리포트 — 보존
- PlayerStats/PlayerHealth/DrugEffectSystem/EquipmentManager 공개 API 소비만
- 공용 Theme.uss / UTKControls / 타 창 — 무수정

## 검증
- bash compile_test.sh → grep -c "error CS" = 0
- EditMode 재실행 (기존 요리 2건 실패는 무관, 확률 크래프트 1회 랜덤 가능 → 재실행 판정)
- 신규 변경 .cs + .meta 동반 파일 지정 git add
- 커밋 → push → QAPROGRESS.md 갱신 → ROADMAP 체크

## 커밋 대상
- Assets/Scripts/UI/Toolkit/StatusWindowUTK.cs (+.meta)
- Assets/Scripts/UI/Toolkit/EquipmentWindowUTK.cs (+.meta)