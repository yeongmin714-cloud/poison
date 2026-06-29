# Phase 40: 자동 이동 시스템 (Auto Move System)

## 개요
지도(MapWindow)에서 목표 영지를 클릭하면 플레이어가 자동으로 경로를 따라 이동합니다.
편의성 기능으로, WASD 입력 시 즉시 취소됩니다.

## 구현 파일

### 1. AutoMoveManager.cs (Systems)
- **경로**: `/mnt/c/Unity/code/Assets/Scripts/Systems/AutoMoveManager.cs`
- **네임스페이스**: `ProjectName.Systems`
- **설명**: 자동 이동 상태를 관리하는 싱글톤 매니저
- **주요 기능**:
  - `SetDestination(Vector3 worldPos)` — 목표 설정 및 이동 시작
  - `CancelAutoMove(string reason)` — 이동 취소 (WASD 입력 감지)
  - `PauseAutoMove()` / `ResumeAutoMove()` — 일시 정지/재개 (전투 상태)
  - CharacterController 기반 이동 (PlayerMovement와 동일한 방식)
  - 이동 속도: 플레이어 걷기 속도 × 2 (`_autoMoveSpeedMultiplier = 2f`)
  - Debug.DrawLine으로 경로 시각화 (점선)
  - 도착 판정: 목표 반경 `_arrivalDistance = 1.5f` 이내
  - `OnAutoMoveNotification` 정적 이벤트 — AutoMoveUI에서 구독

### 2. AutoMoveUI.cs (UI)
- **경로**: `/mnt/c/Unity/code/Assets/Scripts/UI/AutoMoveUI.cs`
- **네임스페이스**: `ProjectName.UI`
- **설명**: IMGUI 기반 자동 이동 HUD 오버레이
- **주요 기능**:
  - 이동 중: 화면 상단에 목적지 + 남은 거리 + 취소 안내 표시
  - 도착 시: 화면 중앙에 "✅ 도착했습니다!" 알림 팝업
  - 일시 정지 시: "⏸️ 전투 중 - 자동 이동 일시 정지" 표시
  - 취소 시: 하단 알림 메시지
  - 페이드 인/아웃 효과

### 3. MapWindow.cs (UI) — 수정
- **경로**: `/mnt/c/Unity/code/Assets/Scripts/UI/MapWindow.cs`
- **설명**: 기존 MapWindow에 자동 이동 관련 UI 추가
- **추가 기능**:
  - **우클릭 컨텍스트 메뉴**: 영지 셀 우클릭 시 "📌 영지명 → 🚶 자동 이동" 팝업
  - **좌클릭 선택 + 버튼**: 영지 좌클릭 선택 후 하단 "🚶 영지명으로 이동" 버튼
  - **점선 경로**: 지도 위에 현재 위치 → 목표까지 청록색 점선 경로 표시
  - **목표 마커**: 목표 지점에 📍 마커 표시

### 4. Phase40Readme.md
- 본 문서

## 사용 방법

1. **씬 설정**: `AutoMoveManager` 컴포넌트를 씬의 적절한 GameObject에 추가
   - 싱글톤 자동 생성 지원 (다른 시스템과 동일한 패턴)
2. **AutoMoveUI**: Canvas 아래에 AutoMoveUI 컴포넌트 추가
3. **지도(MapWindow)**: 기존 MapWindow에 자동 이동 버튼이 자동으로 추가됨

## 이동 중단 조건
| 조건 | 동작 |
|------|------|
| WASD 키 입력 | 즉시 취소 (AutoMoveManager.DetectWASDInput) |
| 전투 상태 진입 | 일시 정지 (PauseAutoMove — TODO: 전투 시스템 연동) |
| 목표 도착 | 자동 완료 + 알림 표시 |

## 의존성
- `ProjectName.Systems.PlayerMovement` — WalkSpeed 참조
- `ProjectName.Systems.AutoMoveManager` — 싱글톤
- `ProjectName.UI.MapWindow` — 지도 UI
- `ProjectName.Core.Data.TerritoryId` / `TerritoryDefinition` — 영지 데이터
- `CharacterController` — 이동 (PlayerMovement와 동일)

## TODO
- [ ] 전투 시스템(CombatManager) 연동 — IsInCombat() 구현
- [ ] NavMesh 기반 경로 탐색 (현재는 직선 Lerp 이동)
- [ ] 영지별 실제 월드 좌표 설정 (현재는 ID 기반 추정 좌표)
- [ ] SoundManager 연동 (이동 시작/도착/취소 사운드)