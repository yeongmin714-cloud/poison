# P4 — 퀘스트 창 3열 Figma 재구성 (UI-F-COMPLETE)

> 2026-09-27 — "피그마에 맞게 게임 로직 고친다" 원칙(사용자 확정). P1~P3 완료 후속.

## Figma 구조 (quest-window-ui — 3열 분할, 노드렌더+vision 실측)
```
[좌 QuestListPanel]
  ├ PanelHeader "퀘스트 창 / QUEST DECK"
  ├ FilterTabs: [진행 중] [완료]
  ├ QuestSlotsContainer(카드 스크롤) — 카드마다:
  │   TierStrip(좌 세로 등급색) + CategoryPill(카테고리) + Lv.N + 제목 + 설명 + 진행게이지(GaugeTrack>Fill) + %
  └ SummaryFooter: "동시 추적 가능 제한 2 / 5 개 등록됨"
[중앙 QuestDetailPanel] — 선택 퀘스트 상세
  ├ PanelHeader + QuestHeroHeader(제목 + 등급태그 + 한줄요약)
  ├ StorySection(작전 브리핑 텍스트)
  └ ObjectivesSection(달성조건 체크리스트 — 완료=초록 체크/미완료=수치 0/1)
[우 DeploymentPanel(Rewards)] — 보상
  ├ PanelHeader + SectionDescription
  ├ RewardsList(아이템 카드: 등급테두리 아이콘 + 이름 + 종류/설명 + 수량)
  └ DeploymentFooter: [추적토글][포기][추적 적용]
```

## 게임 매핑 (QuestManager/Core.Data 소비, 로직 보존)
| Figma 요소 | 게임 API |
|:--|:--|
| 필터 탭 진행중/완료 | QuestManager.GetActiveQuests() / GetCompletedQuests() |
| 수락가능 퀘스트 | QuestManager.GetAvailableQuests(PlayerStats.Instance.Level) |
| 카테고리필 (MAIN OPERATION 등) | QuestData.targetTerritoryId/giverNpcId (게임에 카테고리 enum 없음 → 목적지/상태로 대체) |
| 등급 태그 / 티어스트립 | 상태색: 진행=Accent #58A6FF / 완료=Success #3FB950 / 수락가능=Gold #E3B341 |
| Lv.N | QuestData.requiredLevel |
| 진행게이지 % | objectives 진행률 = Sum(IsMet)/Count (활성/완료만) |
| 상세 목표 체크리스트 | quest.objectives[]: description + currentCount/requiredCount + IsMet |
| 보상 목록 | QuestRewardPreview.GetRewardSummary(quest) + reward.gold/exp/items |
| 수락/완료 버튼 | QuestManager.AcceptQuest(questId) / TryCompleteQuest(questId) |
| 활성 체인 | QuestChainManager GetChainProgress/GetChainData/CompleteCurrentNode (기존 유지) |

## 보존 (손대지 않음)
- 기존 로직 전부: 수락가능/진행/완료/체인 목록, 수락/완료/체인진행 버튼 로직, 통계
- QuestManager/QuestChainManager/QuestRewardPreview 공개 API 소비만
- 공용 Theme.uss / UTKControls / UTKButton / UTKWindowBase / 타 창 — 무수정
- 원본 QuestWindow.cs(IMGUI) — 무수정

## 검증
- bash compile_test.sh → grep -c "error CS" = 0
- EditMode 재실행 (299/301 — 요리 2건 무관)
- 커밋 → push → QAPROGRESS/ROADMAP 갱신

## 커밋 대상
- Assets/Scripts/UI/Toolkit/QuestWindowUTK.cs (+.meta)