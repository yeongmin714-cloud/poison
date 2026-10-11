# ✅ P5 — 전투 로그 창 Figma 재구성 (UI-F-COMPLETE)

> 2026-09-27 — "피그마에 맞게 게임 로직 고친다" 원칙(사용자 확정). P1~P4 완료 후속.

## Figma 구조 (daily-combat-log — 실측)
```
[PanelHeader] 전투 소식
[LogList] — BattleLogItem 카드 (스크롤, depth6)
  각 카드:
    MetaInfo: HH:MM:SS(파랑) + 교전 지역명(흰/굵게)
    BattleResultRow: [FactionCol: "교전 세력" + 세력] [OutcomeCol: "결과" + 승패배지]
  최신 로그 = 파란 테두리 강조
[SummaryFooter] "기록된 최근 교전 수" + "N / 50 세션 로드됨"
```

## 게임 데이터 (CombatLog — 단일 message 문자열 + type, struct 엔트리)
| Figma 요소 | 게임 매핑 |
|:--|:--|
| 타임스탬프 HH:MM:SS | entry.timestamp(세션초) → 시:분:초 |
| 교전 지역명/메시지 | entry.message (굵게) |
| 타입 결과 배지 | entry.type: Damage=레드/Heal=그린/Kill=골드/Warning=옐로/Normal=회색 |
| SummaryFooter "N/50" | CombatLog.Count / 50 (최대 100 → 50 표기, 기존 MAX 유지) |

## 보존 (손대지 않음)
- CombatLog.AddEntry/GetRecentEntries/Clear 데이터 경로, L키/ESC, 300ms 폴링, DontDestroyOnLoad
- 피그마 SF '교전세력/결과 2열'은 게임 로그가 단일 message라 **강제 파싱 안 함** → 카드형 재배열 + 타입 결과 배지로 매핑
- 공용 Theme.uss/UTKControls/타 창 — 무수정. 원본 CombatLogUI.cs/CombatLog.cs — 무수정

## 검증
- compile_test.sh → error CS 0
- EditMode 299/301 (요리 2건 무관)
- 커밋 → push → QAPROGRESS/ROADMAP 갱신