# 주간 세금 시스템 & 상전 NPC 세금제안 UI 구현 계획

> **For Hermes Agent:** 이 계획을 단계별로 실행하세요. `writing-plans` 스킬 기준 작성.

**Goal:** 마을 상전(Shop NPC) 상호작용 패널의 "밀매제안"을 "세금제안"으로 교체하고, 클릭 시 권장세금 UI를 표시하도록 구현합니다. 또한 게임 내 시간 7일마다 주간 세금 징수가 자동으로 발생하도록 로직을 구현합니다.

**Target Files:**
- `Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs` (라벨 교체 + 라우팅)
- `Assets/Scripts/Systems/` (신규 주간 세금 로직 및 트리거)
- `Assets/Scripts/UI/` (신규 권장세금 UI 윈도우)

---

### Task 1: 상호작용 패널 액션 라벨 교체 (UI)

**Objective:** 상점 NPC(ShopNPC) 패널에서 "밀매제안"을 "세금제안"으로 교체하고, 액션 라우팅 로직을 준비합니다.

**Files:**
- Modify: `Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs`

**Step 1.1: 라벨 정의 수정**
- **Location:** `InteractionPanelUTK.cs` 내 `AddAction` 메서드 또는 액션 목록 정의부 (Line ~852).
- **Action:** 문자열 `"밀매제안"`을 `"세금제안"`으로 교체.

```csharp
// Before (Line 852)
AddAction("밀매제안", 3);

// After
AddAction("세금제안", 3);
```

- **주의:** `IsActionSupported` 로직 검증. `"상점"` 케이스 옆의 `"밀매제안"` 관련 처리 로직이 있다면 `"세금제안"`으로 수정 필요.

**Step 1.2: 액션 라우팅 수정 (OnActionRequested)**
- **Location:** `InteractionPanelUTK.cs` (Line ~1092, `case "상점":` 주변)
- **Action:** `"세금제안"` 선택 시 `RecommendedTaxUIManager` (또는 신규 Tax UI Manager)를 호출하도록 수정.

```csharp
// Before
case "밀매제안":
    Debug.Log("밀매제안은 미구현입니다.");
    break;

// After
case "세금제안":
    RecommendedTaxUIManager.Show(); // or SpecificTaxWindow.Open();
    break;
```

---

### Task 2: 권장세금 UI (Recommended Tax UI) 구현

**Objective:** 클릭 시 표시될 "권장세금 UI"를 구현합니다. Figma 디자인(`244:281` - "단일 영지 세금 관리")을 기반으로 합니다.

**Files:**
- Create: `Assets/Scripts/UI/Toolkit/RecommendedTaxWindowUTK.cs`
- Create: `Assets/Scripts/Systems/TaxProposalManager.cs`

**Step 2.1: UI 윈도우 클래스 생성**
- Figma 디자인(`244:281`)을 기준으로 UTK 패턴(`UTKWindowBase` 상속 또는 `VisualElement` 사용)의 신규 UI를 구현합니다.
- 주요 데이터 표시: 영지 이름, 현재 세율, 권장 세율, 호감도(72/100).

**Step 2.2: 매니저(Manager) 로직 생성**
- `TaxProposalManager`를 생성하여, `InteractionPanelUTK`의 액션 요청을 수신하고 UI를 오픈합니다.
- 현재 선택된 영지(NPC가 속한 영지) 정보를 UI에 전달합니다.

---

### Task 3: 주간 세금 로직 (7일마다 자동 실행)

**Objective:** 게임 시간 7일마다 세금 징수 이벤트가 발생하도록 구현합니다.

**Files:**
- Create: `Assets/Scripts/Systems/WeeklyTaxSystem.cs`
- Modify: `Assets/Scripts/Systems/TimeManager.cs` (필요시 시간 이벤트 구독)

**Step 3.1: 주간 세금 시스템 구현**
- 시간 시스템(예: `DayNightCycle` 또는 `TimeManager`)에서 날짜 변경 이벤트를 구독합니다.
- 현재 게임 일자가 7의 배수일 때(또는 마지막 징수일로부터 7일 경과 시) 세금 징수 로직을 실행합니다.
- 징수된 세금 데이터를 `PlayerInventory` 또는 영지 데이터에 반영합니다.

```csharp
public class WeeklyTaxSystem : MonoBehaviour
{
    private const int TAX_CYCLE_DAYS = 7;
    private int _lastTaxDay = 0;

    public void OnDayChanged(int currentDay)
    {
        if (currentDay >= _lastTaxDay + TAX_CYCLE_DAYS)
        {
            _lastTaxDay = currentDay;
            CollectTaxes();
            // Notify UI or Log
        }
    }
}
```

---

### Verification (검증)

1.  **컴파일 확인:** `run_tests.sh` 실행 (EditMode 전수) — 신규 코드 빌드 에러 없는지 확인.
2.  **UI 테스트:** Play 모드에서 상점 NPC 우클릭/상호작용 → 패널에 "세금제안" 표시 확인.
3.  **로직 테스트:** 시간을 7일 진행시켜 세금 징수 이벤트 발생 로그 확인.
4.  **Figma 정합성:** 신규 UI가 Figma (`244:281`)와 일치하는지 스크린샷 비교.
