# Phase 3.4: 🏁 국기 & 영지 소유 표시 시스템

## 작업 분할 (3개 병렬 태스크)

### Task A: Core Data + FlagManager (시스템)
- **NationFlagData.cs** (Core/Data) — 국가별 국기 데이터 (NationFlagDefinition: 국가별 색상/문양/설명)
- **NationFlagDatabase.cs** (Core) — 5개 국가 국기 정의 정적 데이터베이스
- **FlagManager.cs** (Systems) — 중앙 관리자: 깃대 생성/소유권 교체/반기상태 관리
- 기존 `TerritoryBannerSystem` 확장 (FlagManager 연동)

### Task B: 3D 깃대/깃발 표시
- **FlagPoleDisplay.cs** (Systems) — 영지 건물 위 절차적 3D 깃대+깃발
  - 깃대: Cylinder (흰색/회색)
  - 깃발: Plane (국가 색상 머티리얼)
  - 반기(半旗): Y축 이동
  - 소유권 변경 시 페이드 교체
  - 깃발 흔들림: 간단한 Sin 파동 (코드 애니메이션)

### Task C: 플레이어 국기 등록 UI
- **PlayerFlagRegistrationWindow.cs** (UI) — 게임 시작 등록/변경 UI
  - EmblemManager.Canvas 기반
  - 배경색 8종 선택 (EmblemColor)
  - 문양 10종 선택 (EmblemShape)
  - 국기명 입력 (최대 8자)
  - 미리보기 렌더링
  - 완료 버튼

### 테스트 (모든 태스크 통합 후)
- **Phase34_FlagSystemTests.cs** — 15개+ EditMode 테스트

## 기존 코드 참조
- `EmblemManager.cs` (Systems) — 플레이어 문장 데이터, 이미 구현됨
- `TerritoryBannerSystem.cs` (Systems) — 점령 시 깃발 교체, 이미 구현됨 (Phase 31)
- `TerritoryManager.cs` (Systems) — 영지 건물/병사 관리
- `MapWindow.cs` (UI) — 지도 창 (Phase 3.5에서 확장)
- `NationType` enum (TerritoryData.cs) — 5개 국가