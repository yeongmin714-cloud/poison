# 📊 포이즌 (Poison) — 게임 개발 진행 상황

> 마지막 업데이트: 2026-06-17

## 완료된 Phase

| Phase | 내용 | 상태 |
|:-----:|:-----|:----:|
| Phase 1 | 🏗️ 기반 공사 (Unity/패키지/Git/CI) | ✅ |
| Phase 2 | 🎮 3인칭 플레이어 조작 (WASD/카메라) | ✅ |
| Phase 3 | ⌨️ UI 프레임워크 & 키 설정 (UIManager/윈도우) | ✅ |
| Phase 4 | ⚔️ 마우스 공격 & 전리품 시스템 | ✅ |
| Phase 5 | 🏠 튜토리얼 — 추방지 | ✅ (22/22) |
| Phase 6 | 🤖 Animation Rigging (22사이클) | ✅ |
| Phase 7 | 🌍 월드맵 & 지형 | ✅ (23/23) |
| Phase 8 | 🧪 크래프트 & 레시피 (37사이클) | ✅ |
| Phase 9 | 🏰 영지 & 부하 관리 (32사이클) | ✅ |
| Phase 10 | ⚔️ 전쟁/맵전환/연출/배포 (25사이클) | ✅ |
| Phase 11 | 🚪 실내 맵 — 벽+타일 (14사이클) | ✅ |
| Phase 12 | ⏳ 로딩 화면 시스템 (5사이클) | ✅ |
| Phase 13 | ⏰ 시간 & 주야 시스템 (5사이클) | ✅ |
## Phase 16 진행 상황

### C16-01~03 완료: HUD 날짜 표시, 침대 상호작용, 수면 시간 가속
- TimeManager에 Day 카운터 추가 (게임 시작=1, 자정+1)
- Bed.cs + SleepUI.cs — E키로 수면 옵션 (2h/4h/6h/8h/아침까지)
- TimeManager.SleepFor/WakeUp — TimeScale 가속, 기상 콜백

### C16-04 완료: SaveManager — 저장/로드 코어
- SaveData.cs — 7개 직렬화 데이터 클래스 (Player/Inventory/Time/Territory/Quest)
- SaveManager.cs — Singleton, 3개 슬롯, JSON (JsonUtility)
- SaveSlotUI.cs — IMGUI 슬롯 선택 UI (저장일시/레벨 표시)
- 11개 EditMode 테스트 (Singletone/파일생성/로드복원/다중슬롯/HasSave/Delete/이벤트)

### 진행 중
- C16-07: 저장/로드 테스트 — 직렬화/역직렬화 무결성 검증
- C16-08: 통합 — 침대자고→저장→로드→이전 상태 복원

## 최근 진행 상황

### Phase 10 완료 (2026-06-16)
- 🎉 **모든 Phase 완료!** v1.0 출시
- Part A: 실내외 맵 전환 (Additive Loading/BuildingTrigger/Fade)
- Part B: 전쟁 & 영지 점령 (경보/항복/처형/독살/암살/AI전쟁/알림/영토)
- Part C: 연출 & 퀄리티 (오프닝/BGM/효과음/UI사운드/모델교체)
- Part D: 통합 테스트/빌드 검증/Windows 빌드
- Git 태그 v1.0

### GLB 런타임 교체 (2026-06-16)
- C-GLB-01: RuntimeModelLoader — Resources로 61개 GLB 로드
- C-GLB-02~03: PlayerPlaceholder/TerritoryBuilder GLB 우선 로드
- C-GLB-04: 18개 EditMode 테스트
- C9-30: NPC 퀘스트 시스템 — 말풍선 UI, QuestManager (60개 EditMode 테스트)
- C9-31: 아바타 레벨 그룹 데이터 — 5단계 레벨 범위 (Novice→Legendary), Placeholder 색상
- C9-32: ModelSwapper 확장 — 레벨별 GLB 매핑 (_tier1~5), LevelModelSwapper Editor 도구

### Phase 6 완료 (Animation Rigging)
- C6-02~07: 코어 시스템 (BoneDefs/IK/MultiAim/FABRIK + 25개 테스트)
- C6-08~17: 10개 모션 (Idle/Walk/Run/Jump/Gather/Craft/Attack/Throw/Kneel + 20개 테스트)
- C6-18~22: 4족보행/뱀기어가기/GLB파이프라인/MotionDetector + 20개 테스트

> **🎉 모든 Phase 완료!** v1.0 배포 준비 완료