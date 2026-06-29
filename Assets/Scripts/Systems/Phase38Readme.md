# Phase 38: 영지별 축제 시스템 (Territory Festivals)

81개 영지 중 6개 주요 영지에 각각 특색 있는 축제를 추가합니다.
기존 `TerritoryManager`, `DayNightCycle`, `TimeManager`와 연동됩니다.

---

## 생성된 파일 (7개)

| # | 파일 | 네임스페이스 | 설명 |
|---|------|-------------|------|
| 1 | `Systems/FestivalData.cs` | `ProjectName.Systems` | 축제 정의 ScriptableObject + FestivalEffect 구조체 |
| 2 | `Systems/FestivalDefinitions.cs` | `ProjectName.Systems` | 6개 축제 코드 기반 정의 |
| 3 | `Systems/FestivalManager.cs` | `ProjectName.Systems` | 축제 관리자 싱글톤 (이벤트 발행 + Day 체크) |
| 4 | `Systems/FestivalNPC.cs` | `ProjectName.Systems` | 축제 기간 전용 임시 NPC |
| 5 | `UI/FestivalUI.cs` | `ProjectName.UI` | IMGUI 축제 정보 창 + 알림 |
| 6 | `UI/FestivalMapIndicator.cs` | `ProjectName.UI` | 지도 위 축제 영지 아이콘 표시 |
| 7 | `Systems/Phase38Readme.md` | — | 본 문서 |

---

## 6개 영지 축제 데이터

| # | 영지 | 축제명 | Day | 핵심 효과 |
|---|------|--------|-----|-----------|
| 1 | 🏔️ Ice_Crown (North_01) | 얼음 축제 | 3~5 | 냉기 저항 +30, 힘 +3 |
| 2 | 🏜️ Sand (West_01) | 사막 카니발 | 8~12 | 상점 30% 할인, 은신 +5 |
| 3 | 🌋 Red_Desert (South_01) | 불의 축제 | 15~20 | 화염 데미지 +20%, 힘 +5 |
| 4 | 🌳 East_Forest (East_01) | 수확제 | 22~28 | 요리 성공률 +15%, 약초 2배 |
| 5 | 👑 Empire (Empire_01) | 제국의 날 | 35~40 | 모든 능력치 +5, 전설 아이템 판매 |
| 6 | ⚓ Port_Town (East_02) | 바다 축제 | 45~50 | 이동속도 +20%, 물고기 3배 |

---

## 주요 클래스 사용법

### FestivalManager (싱글톤)
```csharp
// 축제 시작 시 이벤트 구독
FestivalManager.OnFestivalStarted += (festival) => {
    Debug.Log($"축제 시작: {festival.festivalName}");
};

// 현재 활성 축제 확인
bool hasFestival = FestivalManager.Instance.HasAnyActiveFestival;
var activeList = FestivalManager.Instance.ActiveFestivals;

// 특정 영지의 축제 확인
var festival = FestivalManager.Instance.GetActiveFestivalAtTerritory("North_01");
```

### FestivalNPC (컴포넌트)
```csharp
// NPC를 축제와 연결 (Inspector에서 _festivalId 설정)
// 축제 시작 시 자동 활성화, 종료 시 자동 비활성화

// 대화 텍스트 획득
string dialogue = npc.GetComponent<FestivalNPC>().GetDialogueText();

// 아이템 구매 시도
npc.TryBuyItem(0, out string message);

// 미니게임 시작
npc.TryStartMiniGame(out string message);
```

### FestivalUI (정적 클래스)
```csharp
// MonoBehaviour.OnGUI()에서 호출
FestivalUI.OnFestivalNotifGUI();     // 축제 시작 알림
FestivalUI.OnFestivalPanelGUI();     // 축제 정보 패널
FestivalUI.OnActiveFestivalsHUD();   // 활성 축제 HUD 목록

// 외부에서 패널 열기
FestivalUI.ShowFestivalInfo(festivalData);
```

### FestivalMapIndicator (정적 클래스)
```csharp
// MapWindow.OnGUI() 등에서 호출
FestivalMapIndicator.OnFestivalMapIcons(territoryScreenPositions);
FestivalMapIndicator.OnFestivalTooltip(mousePos, territoryScreenPositions);
```

---

## 확장 가이드

### 새 축제 추가
1. `FestivalDefinitions.cs`에 새 정적 메서드 추가
2. `CreateAll()`에 추가
3. 또는 `FestivalData` ScriptableObject를 `Resources/Festivals/` 폴더에 생성

### Runtime 축제 등록
```csharp
var newFestival = ScriptableObject.CreateInstance<FestivalData>();
newFestival.Initialize(id, name, desc, nation, index, startDay, endDay, ...);
FestivalManager.Instance.AddFestival(newFestival);
```

### NPC 기능 확장
- `FestivalNPC`에 새로운 상호작용 메서드 추가
- 추가 미니게임: `TryStartMiniGame()`에서 실제 미니게임 로직 호출
- `NPCDialogueWindow`와 연동하여 대화 UI 표시

---

## 의존성

- `ProjectName.Systems.TimeManager` — `CurrentDay`, `Hour`
- `ProjectName.Systems.FestivalManager` — 싱글톤
- `ProjectName.Core.Data.TerritoryId` — 영지 식별자
- `ProjectName.Core.Data.NationType` — 국가 열거형
- `ProjectName.UI.DynamicEventUI` — 참고 패턴 (Phase 36)

---

## 주의사항

- `FestivalManager`는 `DefaultExecutionOrder(100)`로 GameManager 이후 실행됩니다.
- `FestivalUI`와 `FestivalMapIndicator`는 `RuntimeInitializeOnLoadMethod`로 자동 초기화됩니다.
- 모든 IMGUI 렌더링은 `MonoBehaviour.OnGUI()`에서 호출해야 합니다.
- `FestivalNPC`의 `gameObject.SetActive(false)`는 씬에 미리 배치된 NPC를 축제 기간에만 활성화합니다.
