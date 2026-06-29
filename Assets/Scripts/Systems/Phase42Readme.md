# Phase 42: 📖 백과사전/도감 시스템

발견한 콘텐츠를 기록하고 열람하는 게임 내 도감 시스템입니다.
8개 카테고리(약초/몬스터/요리/약물/영주/영지/문서/업적)로 구성되며,
각 항목의 발견 상태, 등급, 설명을 추적하고 수집률에 따른 보상을 제공합니다.

---

## 생성된 파일 (5개)

| # | 파일 | 네임스페이스 | 설명 |
|---|------|-------------|------|
| 1 | `Core/Data/EncyclopediaData.cs` | `ProjectName.Core.Data` | 도감 데이터 구조 (ScriptableObject, enum, 카테고리 컨테이너) |
| 2 | `Systems/EncyclopediaManager.cs` | `ProjectName.Systems` | 도감 관리자 싱글톤 (발견/저장/로드/수집률/보상) |
| 3 | `UI/EncyclopediaWindow.cs` | `ProjectName.UI` | IMGUI 백과사전 윈도우 (L 키 토글, 8개 탭, 검색, 프로그레스 바) |
| 4 | `Systems/EncyclopediaDataInitializer.cs` | `ProjectName.Systems` | 초기 데이터 생성기 (에디터 메뉴 Tools/Encyclopedia/Generate Initial Data) |
| 5 | `Systems/Phase42Readme.md` | — | 본 문서 |

---

## 주요 클래스

### EncyclopediaData.cs (`ProjectName.Core.Data`)

```csharp
// 도감 항목 ScriptableObject
EncyclopediaEntry:
  - entryId (string), category (EncyclopediaCategory)
  - entryName, description, rarity, location
  - IsDiscovered (bool), DiscoveryDate (string)
  - Discover() / GetRarityColor() / GetRarityName()

// 카테고리별 데이터 컨테이너
EncyclopediaCategoryData:
  - category, categoryName, categoryIcon
  - entries (List<EncyclopediaEntry>)
  - TotalCount / DiscoveredCount / CompletionRate

// 전체 데이터베이스
EncyclopediaDatabase (ScriptableObject):
  - categories (List<EncyclopediaCategoryData>)
  - TotalEntryCount / TotalDiscoveredCount / OverallCompletionRate
  - FindEntryById(id) / GetCategory(cat)
```

### EncyclopediaManager.cs (`ProjectName.Systems`)

```csharp
// 싱글톤 접근
EncyclopediaManager.Instance

// 발견 관리
bool DiscoverEntry(string entryId)      // 항목 발견 처리
bool IsDiscovered(string entryId)       // 발견 여부 확인

// 수집률 조회
float OverallCompletionRate              // 전체 수집률 (0.0 ~ 1.0)
float GetCategoryCompletionRate(cat)     // 카테고리별 수집률
int GetCategoryDiscoveredCount(cat)      // 카테고리별 발견 수
int GetCategoryTotalCount(cat)           // 카테고리별 전체 수
List<EncyclopediaEntry> GetCategoryEntries(cat)  // 카테고리 항목 리스트

// 이벤트
static event Action<EncyclopediaEntry> OnEntryDiscovered  // 항목 발견 시

// 외부 연동 헬퍼
OnHerbCollected(herbId)         // 약초 채집
OnMonsterKilled(monsterId)      // 몬스터 처치
OnCookingCreated(dishName)      // 요리 제조
OnPotionCreated(potionName)     // 약물 제조
OnLordMet(lordId)               // 영주 접촉
OnTerritoryVisited(territoryId) // 영지 방문
OnDocumentFound(documentId)     // 문서 발견
OnAchievementUnlocked(achievementId)  // 업적 달성
```

### EncyclopediaWindow.cs (`ProjectName.UI`)

```csharp
// L 키로 토글 (기본값)
EncyclopediaWindow.Show()       // 열기
EncyclopediaWindow.Hide()       // 닫기
EncyclopediaWindow.Toggle()     // 토글

// 좌측: 8개 카테고리 탭 (🌿🥩🍲🧪👑🏰📜🏆)
// 중앙: 항목 리스트 (✅ 발견 / ❌ 미발견, 검색 필터, 발견만 보기 토글)
// 우측: 상세 정보 패널 (이름, 분류, 등급, 상태, 설명, 위치)
// 하단: 수집률 프로그레스 바 + 보상 임계값 표시
```

---

## 수집률 보상 시스템

| 수집률 | 보상 | 설명 |
|--------|------|------|
| 10% | 기본 정보 잠금 해제 | 미발견 항목의 기본 설명과 등급을 볼 수 있음 |
| 25% | 제작 성공률 +5% | 모든 제작(요리/연금술) 성공률 5% 증가 |
| 50% | 특수 레시피 잠금 해제 | 숨겨진 레시피 사용 가능 |
| 75% | 제작 성공률 +10% | 제작 성공률 추가 10% 증가 |
| 100% | 전설 아이템/업적 해금 | 전설 등급 아이템 제작/획득 가능 |

---

## 발견 연동 가이드

각 시스템에서 도감 발견을 연동하는 방법:

### 약초 채집 시:
```csharp
EncyclopediaManager.Instance?.OnHerbCollected("A1");
```

### 몬스터 처치 시:
```csharp
EncyclopediaManager.Instance?.OnMonsterKilled("M01");
```

### 요리/약물 제조 시:
```csharp
EncyclopediaManager.Instance?.OnCookingCreated("01");
EncyclopediaManager.Instance?.OnPotionCreated("H01");
```

### 영주 접촉 시:
```csharp
EncyclopediaManager.Instance?.OnLordMet("01");
```

### 영지 방문 시:
```csharp
EncyclopediaManager.Instance?.OnTerritoryVisited("01");
```

### 문서 발견 시 (Phase 37 연동):
```csharp
EncyclopediaManager.Instance?.OnDocumentFound("01");
```

### 업적 달성 시 (Phase 36 연동):
```csharp
EncyclopediaManager.Instance?.OnAchievementUnlocked("01");
```

---

## 초기 데이터 설정 방법

1. Unity 에디터에서 `Tools > Encyclopedia > Generate Initial Data` 메뉴 실행
2. 또는 `EncyclopediaDataInitializer.cs` 상단의 `#define RUN_INITIALIZER` 주석 해제 후 Play
3. `Assets/Resources/Encyclopedia/EncyclopediaDatabase.asset` 생성 확인
4. 도감 데이터 커스터마이징은 생성된 ScriptableObject에서 직접 편집 가능

---

## 의존 관계

- `EncyclopediaData.cs` — Core.Data (독립적)
- `EncyclopediaManager.cs` — Systems, Core.Data 참조
- `EncyclopediaWindow.cs` — UI, Systems.Core.Data 참조, EncyclopediaManager 의존
- `EncyclopediaDataInitializer.cs` — Systems (에디터 전용, #if UNITY_EDITOR 조건부)