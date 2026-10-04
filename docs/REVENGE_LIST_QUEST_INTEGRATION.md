# 《독의 이름》 게임 퀘스트 연계 설계 — 메인/서브 퀘스트 분리

- 작성: 2026-10-04
- 기준: 소설 본편 `REVENGE_LIST_NOVEL.md`, 설정 `..._CONTINUITY.md`, 개인 스토리 `..._20TH_STORIES.md`, 영지 단편 `..._82_TERRITORIES.md`
- 대상 게임 시스템: `/mnt/c/Unity/code` — Unity 6000.4.10f1
- 상태: **설계 문서**. 이 문서는 매핑 확정용이며, 승인 전 게임 소스를 수정하지 않는다.

---

## 0. 목표

소설의 **복수명단 20명(개인 스토리)** 과 **82개 영지(지역 단편)** 를 실제 게임 퀘스트로 나누어,
**메인 퀘스트**(복수명단/독살 수사 수렴)와 **서브 퀘스트**(영지별 지역 문제)로 이중 구조로 만들고,
게임 진행(Ring 1→4→황제국→드라큘라, 영지 점령·영주 처형)에 자연스럽게 이어지게 한다.

## 1. 기존 게임 시스템 실증(코드 확인 기준)

| 시스템 | 파일 | 역할 | 소설 매핑 대상 |
|:---|:---|:---|:---|
| `QuestManager` (static) | `Core/QuestManager.cs` | QuestData 등록/조회, 선행 퀘스트로 잠금 | 메인·서브 퀘스트 상태 |
| `QuestChainManager` | `Systems/QuestChainManager.cs` | 체인형 메인 퀘스트 노드 진행, 선택지·보상·평판·병사 변화, 다이내믹 이벤트 | 메인 스토리 32장 |
| `RevengeListManager` | `Core/Data/RevengeListData.cs` | 82(81)개 엔트리 + **RevengeReasons[20]** + 10명 독살 공모자, 처형 시 이면 공개 | 복수명단 20명 (표면 죄목 vs 이면) |
| `RevengeListIntegration` | `Systems/RevengeListIntegration.cs` | 영주 처형/독살 시 RevealReason+CompleteEntry, AllPoisonFound 이벤트 | 복수명단 완료 조건 |
| `TerritoryQuestDefinitions` | `Systems/TerritoryQuestDefinitions.cs` | Tier1~5 영지 서브퀘스트 풀 | 영지 단편 서브퀘스트 |
| `NpcQuestGiver` | `Systems/NpcQuestGiver.cs` | NPC 퀘스트 부여 | 영지 서브퀘스트 전달자 |
| `TerritoryDatabase` | `Core/Data/TerritoryDatabase.cs` | 82 영지 정의 (동/서/남/북×4링×5 + 황제국 + 드라큘라) | 82 영지 |
| UTK `QuestJournalUTK` `QuestWindowUTK` `QuestChoiceUTK` | `UI/Toolkit/` | 퀘스트 저널/창/선택지 UI | 메인·서브 표시 |

> **교차 검증:** 게임 영지 82개 = 4국×4링×5(80)+황제국(1)+드라큘라(1). 소설 82 영지(동17·서17·남17·북17·황8·드라1·기타3=**80** 정리)와 수가 어긋나는 지점은 문단편 문서 쪽이므로, 게임 82 기준으로 서브퀘스트는 영지 ID를 따라간다.

## 2. 퀘스트 이중 구조

```
메인 퀘스트 (복수명단 20명 + 독살 수사)
   │  QuestChainManager 기반 — 소설 30~31장의 수렴
   ├─ M01 추방과 첫 영지 (튜토리얼, 소설 1~4장)
   ├─ M02 복수명단의 탐구 — 20명 개인 스토리 (영주별)
   │    └─ 각 영주 = RevengeListManager 엔트리. 처형 시 이면 공개
   ├─ M03 세 갈래 표식 / 왕비 서신 (소설 26~30장, 10명 독살 공모자 수렴)
   └─ M04 광장의 선택 → 영지들의 왕 (소설 31~32장, 엔딩 분기)

서브 퀘스트 (82 영지 지역 문제)
   │  TerritoryQuestDefinitions + NpcQuestGiver — 소설 82 영지 단편
   ├─ 각 Ring(1~4)·황제국·드라큘라 영지의 지역 문제
   │    (닫힌 제분소, 밤의 초, 기록실의 빈칸, 불의 날 주 훈련 등)
   └─ 영지 점령 후 열리는 지역 서브퀘스트
```

## 3. 복수명단 20명 ↔ RevengeReasons[20] 매핑

소설 20명의 **표면 죄목**(게임 `RevengeReasons[i]` 채택)과 **진짜 이유**(영주 처형 시 `RevealReason`으로 표시)를 연결한다.

| # | 소설 인물 | 표면 죄목 (게임 Reason) | 진짜 이유 (이면, Reveal) | 게임 등급 |
|:--|:---|:---|:---|:---|
| 1 | 알데바란 | 왕의 재산을 횡령했다 (10) | 수탈을 감사하려던 재무관 | Ring2~3 |
| 2 | 에드릭 | 왕의 군대를 분열시켰다 (8) | 부패한 궁정에 반기든 장군 | Ring3 |
| 3 | 베르나 | 충성스러운 신하를 모함했다 (11) | 숙청 명단을 빼돌린 역참지기 | Ring2 |
| 4 | 라실 | 왕의 식량 보급을 차단했다 (9) | 징수량을 줄인 세금 징수관 | Ring2~3 |
| 5 | 오델 | 왕에게 거짓 보고를 올렸다 (18) | 압박받고 서명한 법관 | Ring3 |
| 6 | 카르덴 | 왕의 사절단을 습격했다 (17) | 명령받아 생포한 사냥꾼 (세 갈래) | Ring3~4 |
| 7 | 이셀드릭 | 적국과 내통했다 (14) | 감염지 경계를 지킨 의사 | Ring2~3 |
| 8 | 가우든 | 왕의 편지를 위조했다 (12) | 지워진 마을 이름을 지킨 도안가 | Ring3 |
| 9 | 마이단 | 왕의 최측근을 매수했다 (3) | 가족 지킨 집행관 | Ring4 |
| 10 | 루왼 | 왕의 성벽 수리를 거부했다 (16) | 광산 사고를 덮고 삯을 남긴 책임자 | Ring2~3 |
| 11 | 타르벤 | 반역자 정보를 숨겼다 (6) | 전염지 병사를 살린 병참 | Ring2~3 |
| 12 | 올브레이 | 왕의 개인 경호를 해체시켰다 (4) | 기록을 사 지킨 서기관 | Ring3~4 |
| 13 | 에몽 | 왕의 병을 더욱 악화시켰다 (5) | 빈 마을 실측한 측량관 | Ring2 |
| 14 | 세레딘 | 암살자 고용 자금을 지원했다 (2) | 빈민에게 물자를 흘린 상인 | Ring4 |
| 15 | 도므나 | 반역자 정보를 숨겼다 (6) | 병사 처벌을 피시켜 준 여장부 | Ring3 |
| 16 | 에일린 | 왕의 편지를 위조했다 (12) | 옛 국경을 그린 지도자 | Ring2~3 |
| 17 | 크라덴 | 충성스러운 신하를 모함했다 (11) | 증언을 옮긴 통역사 | Ring2 |
| 18 | 마르텐 | 왕의 군대를 분열시켰다 (8) | 민병을 무장시킨 대장장이 | Ring2 |
| 19 | 오레몽 | 왕의 칙령을 무시했다 (19) | 아이들의 배움을 먼저 둔 교사 | Ring4 |
| 20 | 발로라 | 적국과 내통했다 (14) | 금서 노래를 기록한 시인 | Ring2~3 |

> 게임 `RevengeReasons[0]` "왕의 독살에 직접 가담했다"는 **10명 독살 공모자 전용**으로 이미 `RevengeListData`가 고정 시드로 선정한다. 소설 미확정 항목(독살범·왕비 생사·세 갈래·도미)은 이 10명에 매핑되되 **범인을 특정하지 않는다.**

## 4. 메인 퀘스트 체인 설계 (QuestChainManager 기준)

체인 노드 = `QuestChainData` (chainId, currentNodeId, 선택지 `QuestChoiceResult`:
`nextNodeId, goldReward, expReward, affinityChanges, reputationChanges, soldierChanges, resultText`).

| chainId | 노드 | 내용 | 트리거 이벤트 | 소설 장 |
|:---|:---|:---|:---|:---|
| `revenge_main` | `m_intro` | 추방·명단 획득 | 게임 시작 | 1~2장 |
| `revenge_main` | `m_first_territory` | 첫 영지 탈환 | 첫 영지 점령 | 4장 |
| `revenge_main` | `m_list_task_{01..20}` | 복수명단 20명 개인 조사·처형/생존 선택 | 영주 조우 | 20명 개인 스토리 |
| `revenge_main` | `m_poison_research` | 세 갈래·왕비 서신·독살 공모자 10명 수렴 | AllPoisonFound | 26~30장 |
| `revenge_main` | `m_plaza_choice` | 광장의 선택 (칼 내려놓기) | 결말 진입 | 31장 |
| `revenge_main` | `m_ledger_king` | 영지들의 왕 — 엔딩 | 엔딩 | 32장 + 후일담 |

- 각 `m_list_task_{01..20}` 완료 조건 = 해당 영지의 `RevengeListManager.CompleteEntry(territoryId)` 후 체인 진행.
- 선택지(처형/생존/추궁)는 `QuestChoiceResult.reputationChanges`·`soldierChanges`·`resultText`로 분기.

## 5. 서브 퀘스트 설계 (TerritoryQuestDefinitions + NpcQuestGiver 기준)

각 영지의 지역 단편 → `QuestData` (questId, objectives, reward, giverNpcId, prerequisiteQuestIds, targetTerritoryId) 로 추가.
`QuestObjectiveType`: GatherItem / KillMonster / TalkToNPC / ExploreTerritory / CraftItem.

| 서브퀘스트 ID | 영지 단편 | 목표 | 보상 | Ring |
|:---|:---|:---|:---|:---|
| `st_이름_없는_다리` | 이름 없는 다리 | ExploreTerritory 다리 조사 | gold/exp/장부 | 1~2 |
| `st_닫힌_제분소` | 닫힌 제분소 | TalkToNPC 제분소 관리 + Explore | gold/affinity | 2 |
| `st_밤의_초` | 밤의 초 | ExploreTerritory 탑 조사 | affinity | 3~4 |
| `st_기록실_빈칸` | 기록실의 빈칸 | TalkToNPC 서기관 + Gather 문서 | affinity/표식 | 3~4 |
| `st_불의_날` | 불의 날 주 훈련 | KillMonster 소환수 + Craft | exp | 1~2 |
| ... (각 영지 단편당 1개, 82 영지) | | | | |

- **연계:** 서브퀘스트 = 복수명단 메인 퀘스트의 **선행 조건**이 될 수 있음.
  예) `닫힌 제분소` 완료 → 영주(복수명단 인물) 조우 가능. `기록실 빈칸` 완료 → 세 갈래 표식 단서.
- NpcQuestGiver는 서브퀘스트를 `giverNpcId`(영지 NPC)가 부여.

## 5-1. 대화 시스템 연계 (NPCDialogue / AmbientDialogue)

퀘스트뿐 아니라 **NPC와의 대화가 소설 장면으로 자연스럽게 들어가도록** 게임의 기존 대화 시스템을 소설에 매핑한다.

### 5-1-1. 기존 대화 시스템 (코드 확인 기준)

| 시스템 | 파일 | 역할 | 소설 활용 |
|:---|:---|:---|:---|
| `NpcDialogueData` (ScriptableObject) | `Systems/NpcDialogueData.cs` | NPC별 시간대(아침/오후/저녁/밤)·날씨(비/눈/축제/전투)·E키 상호작용 대사 | 영지 주민 일상·장면 대사 |
| `NPCDialogueUTK` / `NPCDialoguePanelUTK` | `UI/Toolkit/` | 선택지(질문) 기반 대화창 | 복수명단 인물 조우 대화 |
| `NPCDialogueAdapter` | `Systems/NPCDialogueAdapter.cs` | **LLM 실시간 대화** (데일리 한도 + 규칙 폴백) — `DialogueReady` 이벤트 | 20명 인물과 개방형 대화 |
| `NPCAmbientDialogue` / `AmbientDialogueManager` | `Systems/` | NPC가 걸으며 내뱉는 주변 대사 | 영지 분위기·장면 연출 |
| `NpcQuestGiver` | `Systems/NpcQuestGiver.cs` | NPC 퀘스트 부여 | 서브퀘스트 전달자 |

### 5-1-2. 복수명단 20명 대화 매핑

각 영주(=복수명단 인물)를 조우하면, `NPCDialogueUTK` 선택지 대화로 **표면 죄목 → 이면 조사** 흐름을 연다.

| 대화 단계 | 질문(선택지) | NPC 응답 | 게임 연계 |
|:---|:---|:---|:---|
| 1. 조우 | "당신이 아버지 명단의 (이름)이오?" | 표면 죄목을 인정/회피 | 짧은 대화 후 조사 시작 |
| 2. 장부 | 장부/증언 확인 요구 | 실제 한 일을 드러냄 | `RevengeListManager.RevealReason` |
| 3. 선택 | 처형 / 생존 / 추궁 | 결말 분기 대사 | `QuestChoiceResult` 분기 |
| 4. 이면 | (LLM 개방형) 그 일로 바뀐 주민 이야기 | 자유 응답 | `NPCDialogueAdapter` LLM |

- **LLM 실전:** `NPCDialogueAdapter`에 소설 인물 컨텍스트(이름·표면 죄목·진짜 이유)를 주고, 플레이어의 자유 질문에 그 인물답게 응답하게 한다. `DialogueReady`로 본문 톤("확인되지 않은 이름을 지우지 않는다")을 유지.
- **규칙 폴백:** LLM 실패/한도 시 `NpcDialogueData`의 시간대·E키 대사로 안전 폴백 (기존 규칙 유지).

### 5-1-3. 영지 82곳 대화 매핑

각 영지 주민 NPC = `NpcDialogueData` ScriptableObject. 소설 영지 단편의 장면을 **시간대/분위기 대사**로 입힌다.

| 영지 단편 모티프 | 주민 대사 예시 | 트리거 |
|:---|:---|:---|
| 닫힌 제분소 | "그 문은 몇 해 전부터 안 열렸지. 물레가 돌 때 소리가 났는데..." | 제분소 근처 (E키) |
| 밤의 초 | 탑 아래 밤마다 초가 켜진다 — 밤 시간대 대사 | 밤 슬롯 |
| 기록실의 빈칸 | 서기관 — 빈칸에 관한 익명 진술 | 기록실 조우 |
| 불의 날 주 훈련 | 축제(불의 날) 대사가 소환수 출현 전 암시 | `_festivalDialogues` |

- 주변 대사(`AmbientDialogueManager`)로 영지가 "이름을 지우지 않은 땅" 분위기를 흘린다.
- 서브퀘스트 완료 후에는 해당 영지 주민 대사가 **결과를 반영**하도록 바뀐다 (예: 제분소 열림 → "이제 물레가 돈다").

### 5-1-4. 대화-퀘스트 연동 규칙

- **대사가 단서를 준다:** 주민 대사·LLM 대화 중 소설 핵심 단서(장부 수량·세 갈래 표식·실종 편지)가 등장하면 퀘스트 마커/저널 갱신.
- **선택지가 진행을 바꾼다:** 처형/생존/추궁 선택 → `QuestChainManager` 노드 분기 + 해당 영지 주민 대사 전환.
- **미확정 유지:** 독살범·왕비·세 갈래·도미를 특정하지 않는다 — 주민/LLM 대화도 이를 "확정된 이름"으로 말하지 않는다.

### 5-1-5. 단서 제공자 NPC 아키텍처 (마을 추가 불필요)

**배경(코드 실증):** 퀘스트를 주는 NPC는 마을 주민이 아니라 **영지 NPC**(`TerritoryNPCSpawner`: 82영지 × tier별 2~5명 ≈ **300명**)다. 이미 `QuestIds`·`QuestOfferLine`을 배정받는다. 마을 주민(`VillageNpcSpawner`: 24마을 × 10~15명)은 분위기·상인용 기반이라 퀘스트가 없다.

**설계:** 새 마을을 추가하지 않고, **영지당 기존 NPC 1명을 "단서 제공자"로 지정**한다.

1. **역할 부여** — 영지 대표 NPC(촌장/장로/서기/거상/포고 등) 1명을 퀘스트 단서 제공자로 지정.
2. **단서 대사 슬롯** — `NpcDialogueData`에 "퀘스트 단서" 전용 대사 목록 추가(없으면 일반 대화로 폴백). 단서 NPC는 해당 퀘스트의 암시를 주는 대화를 가짐.
3. **매핑 테이블** — 메인 20명/서브 82영지 퀘스트 각각에 `questId → 단서 territoryNPCId` 연결. `QuestMarkerSystem`/`QuestJournalUTK`와 연동해 "이 퀘스트의 단서는 ○○ 영지 ○○ NPC"임을 제시.
4. **용량 검증** — 메인 20 + 서브 82 ≈ 102 퀘스트, 퀘스트당 단서 1명 = 102 자리. 영지 NPC ≈300명이면 여유 확보. **마을 추가 불필요.**

| 단서 제공자 예시 (영지 단편) | 단서 역할 | 대화 슬롯 |
|:---|:---|:---|
| 닫힌 제분소 | 촌장 | "그 문은 몇 해 전부터 안 열렸지. 물레 돌 때 소리가 났는데..." |
| 밤의 초 | 탑지기 | "밤마다 초가 켜져. 누가 켤는지 아무도 몰라." |
| 기록실 빈칸 | 서기 | "빈칸이라 불리는 자리, 찾는 이가 있더군." |
| 불의 날 주 훈련 | 포고 | "불의 날이 다가오면 이상한 게 나타나곤 했어." |

### 5-1-6. 영지 NPC ↔ 마을 주민 관계형 퀘스트 (상호작용)

**제안:** 마을 주민과 영지 NPC를 별개로 두지 않고, **서로가 서로를 참조**하는 관계형 퀘스트로 만든다. 중개자를 새로 추가하지 않고, 두 기존 NPC 층이 서로에게 단서·물건·의뢰를 주고받는다.

**코드 실증(실현 가능):** `TerritoryQuestDefinitions` 서브퀘스트는 이미 **체인형**(`prerequisiteQuestIds`로 다음 노드 잠금 해제)이고 **`giverNpcId`로 다른 NPC가 부여**한다. 목표 `QuestObjectiveType.TalkToNPC`가 NPC ID를 타게팅하므로, 한 퀘스트가 `마을 주민 → 영지 NPC → 보고`로 목표를 나열하면 왕복 관계형이 된다. `QuestManager`가 `giverNpcId`·`TalkToNPC` 목표를 그대로 처리한다.

**퀘스트 단계 (관계형 예시):**

| # | 목표 | NPC 타겟 | 유형 | 내용 |
|:--|:---|:---|:---|:---|
| 1 | 영지 NPC(촌장)에게 의뢰 수락 | 영지 NPC | TalkToNPC | "광장 주민이 행방이 묘하드라. 가서 물어봐라." |
| 2 | 마을 주민 A에게 단서 청취 | 마을 주민 A | TalkToNPC | 주민이 밤의 초·실종 편지 등 단서 제시 |
| 3 | 마을 주민 B에게 물건 수령 | 마을 주민 B | Gather/Talk | 의뢰에 필요한 증표 획득 |
| 4 | 영지 NPC에게 결과 보고 | 영지 NPC | TalkToNPC | 보고 → 완료 + 다음 체인 노드 잠금 해제 |

**효과:** 마을 주민이 단순 아트 배경이 아니라, **영지 NPC 퀘스트의 연결고리**가 된다. 영지 NPC(촌장)는 퀘스트 이정표, 마을 주민은 그 퀘스트의 단서·증거·사이드 스레드 제공자로 분업된다.

**연동 규칙 (+5-1-4):**
- 목표가 TalkToNPC 왕복이므로, 각 단계 완료 시 `QuestManager` 목표 카운트 증가 → 다음 목표 자동 활성.
- 마을 주민 대사는 `NpcDialogueData`로 단계별 전환(의뢰 전/중/후 대사).
- 완료 후 영지 NPC·주민 대사가 결과 반영 → 소설의 "이름을 지우지 않고 증언을 남긴다" 테마 구현.

### 5-1-7. 보상 설계 (골드 / 장비 / 물약·연금 레시피) — 코드 실증

| 보상 종류 | 게임 코드 경로 | 적용 대상 |
|:---|:---|:---|
| **골드** | `QuestReward.gold` → `PlayerStats.Instance.AddGold(gold, "quest_reward")` | 모든 서브/메인 |
| **EXP** | `QuestReward.exp` → `PlayerStats.Instance.AddEXP(exp)` | 모든 서브/메인 |
| **장비** | `QuestReward.items` → `PlayerInventory.Instance.AddItem(itemData, 1)` — `weapon_sword_steel`/`armor_leather`/`helmet_steel` 등 `WeaponCraftDatabase` 결과 ID | Ring2~4·가치 높은 서브 |
| **물약·연금/요리 레시피** | `Recipe`(ScriptableObject, `RecipeType.Alchemy/Cooking`, `requiredLevel`, `baseSuccessRate`) — 보상 아이템 ID로 지급 후 제작 창에 해금 | 퀘스트 수수료·영장력 |
| **호감도** | `QuestReward.affinity` → 영주 호감도 로그 | 영주 연계 서브 |

**보상 규모 (Ring별):**

| Ring | 골드 | EXP | 대표 장비/레시피 보상 |
|:---|:---|:---|:---|
| Ring1 | 10~15 | 20~30 | (없음 — 튜토리얼) |
| Ring2 | 15~25 | 30~45 | `helmet_wood`/`armor_leather` |
| Ring3 | 25~40 | 45~70 | `weapon_sword_steel`/`armor_stone` |
| Ring4 | 40~60 | 70~100 | `weapon_sword_crystal`/`armor_crystal` |
| 황제국 | 60~100 | 100~150 | `weapon_sword_crystal`/연금 레시피 |
| 드라큘라 | 80~120 | 120~180 | 희귀 연금/밤의 초 연계 보상 |

**20명 복수명단 보상:** 영주 처형/생존 시 — 공모자 10명은 `WeaponCraftDatabase`의 결정(晶) 티어 장비 + 연금 레시피, 나머지 10명은 스틸/스톤 장비 + 골드. (미확정 유지: 최종 독살범은 레시피로 특정하지 않음.)

## 6. 진행 흐름 (Ring 순서 — 소설 순서와 일치)

1. **Ring1 동부** — 튜토리얼 + 첫 영지 4장 (마라·주민 문제 서브퀘스트)
2. **Ring1~2 각국** — 6명 기존 인물(알데바란·에드릭·베르나·라실·오델·카르덴) 조우
3. **Ring2~3** — 신규 14명(이셀드릭·가우든 등) 여정 + 각 영지 서브퀘스트
4. **Ring4·황제국** — 마이단·세레딘·오레몽 등 + 독살 공모자 10명 수렴
5. **황제국·드라큘라** — 세 갈래·왕비 서신·최종 미스터리
6. **광장의 선택 → 영지들의 왕** — 엔딩 분기(결말 생성물)

## 7. 미확정 유지 규칙 (연속성 노트 준수)

- **독살범·왕비 생사·세 갈래 표식·도미 행방** 은 게임에서도 특정 이름으로 확정하지 않는다.
- 10명 독살 공모자는 `RevengeListManager`가 고정 시드로 선정하되, 이들이 소설의 어떤 인물인지는 기술로 열어 둔다.
- 소설 20명 이면(진짜 이유)과 게임 `RevealReason`은 일치하되, 본편 원문은 바꾸지 않는다.

## 8. 구현 범위 (승인 후)

이 문서는 확정 설계다. 승인하면 아래를 code agent(+QA agent)로 구현한다:

1. **`TerritoryQuestDefinitions` 확장** — 82 영지 서브퀘스트 데이터 추가(영지 단편 매핑).
2. **`QuestChainManager` 메인 체인 등록** — `revenge_main` 노드 체인 + 선택지 분기.
3. **`RevengeListManager`/`Integration` 연계** — 영주 처형·독살 공모자 이벤트가 메인 체인 진행을 트리거.
4. **`NpcDialogueData` ScriptableObject** — 82 영지 주민 대사 + 시간대/날씨/E키 매핑 (영지 단편).
5. **`NPCDialogueAdapter` 컨텍스트** — 복수명단 20명 인물 컨텍스트(이름·표면 죄목·진짜 이유) 주입 + LLM 개방형 대화.
6. **`NPCDialogueUTK` 선택지 대화** — 영주 조우 시 4단계(조우/장부/선택/이면) 질문지.
7. **`AmbientDialogueManager`** — 영지 "이름을 지우지 않은 땅" 분위기 주변 대사.
8. **UTK 매핑** — 서브/메인 퀘스트가 `QuestJournalUTK`에 표시되도록 연결.
9. **검증** — Compile(error CS 0) + EditMode 회귀 + Test_10 Play에서 메인/서브 퀘스트 진행과 대화 확인.

## 9. 보호 범위

- `git add -A` 금지 (파일 지정 커밋). 초기화 로그·TestOutput·컴파일 로그는 커밋/추적 안 함.
- 소설 본편·20명·82영지 문서는 **게임 데이터 매핑을 위해 읽기만** 하고 원문 수정하지 않는다(이미 30장 여정 확장은 별도 커밋됨).
- 기존 TerritoryQuestDefinitions Tier1~5 퀘스트는 유지하고, 추가만 한다.

---

*이 설계의 구현 지시가 있기 전에는 게임 소스를 수정하지 않는다.*