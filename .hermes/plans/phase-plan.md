# MainScene 실제 게임 월드 구현 - 페이즈별 계획

## 목표
예시.jpg(젤다 BotW 스타일) 같은 탑다운 뷰 게임 화면 구현:
- 플레이어 1명 (탑다운 뷰, 카메라 추적)
- 지형 (평원, 나무, 바위, 물, 건물)
- 주변 병사들 (가드 플레이스홀더)
- 몬스터들 (토끼, 멧돼지, 늑대, 독사 등)
- BotW 스타일 HUD (좌상단 하트, 우하단 미니맵)

---

## Phase 1: 런타임 진단 및 카메라/플레이어 기초 설정
**목표**: Play 모드 진입 시 화면에 플레이어와 지형이 보이게

### 1.1 카메라 설정 검증/수정
- Main Camera가 TopDownCameraController를 가지고 있는지
- 카메라가 플레이어를 추적하는지 (오프셋, 피치, 요 설정)
- 카메라 시작 위치가 플레이어 위/뒤에 있는지

### 1.2 플레이어 설정 검증
- Player 태그 설정 확인
- CharacterController + PlayerMovement 정상 작동
- MeshRenderer가 실제로 렌더링되는지 (머티리얼, 레이어)

### 1.3 지형 렌더링 확인
- Ground_Inner가 화면에 보이는지 (메시, 머티리얼, 레이어)
- URP Lit 셰이더로 정상 렌더링되는지

### 1.4 라이트/스카이박스
- Directional Light 강도/각도 (intensity 1.5, 피치 ~50도)
- Procedural Skybox 할당
- 안개 설정 (거리 기반)

**검증 방법**: Play 모드 진입 → Scene 뷰에서 카메라 프리뷰 확인 → Game 뷰에서 실제 화면 확인

---

## Phase 2: TerritoryBuilder 실제 건물/병사 스폰
**목표**: 82개 영지의 건물과 병사들이 월드에 생성되게

### 2.1 TerritoryBuilder 동작 확인
- BuildAllTerritories()가 실제로 호출되는지
- BuildingPlaceholder 프리팹이 존재하는지
- GuardPlaceholder 프리팹이 존재하는지

### 2.2 영지별 스폰 로직
- TerritoryDatabase의 worldPosition이 올바른지
- 각 영지 중심에서 반경 내 건물 배치
- 병사 원형 배치 (guardCircleRadius)

### 2.3 프리팹/모델 확인
- BuildingPlaceholder가 메시/머티리얼을 가지는지
- GuardPlaceholder가 메시/애니메이터를 가지는지

---

## Phase 3: 몬스터/동물 스폰 시스템
**목표**: 필드에 토끼, 멧돼지, 늑대, 독사 등 몬스터 스폰

### 3.1 MonsterSpawner 동작
- MonsterSpawner가 씬에 있는지 (GameManager가 생성하는지)
- 영지 난이도별 몬스터 티어/마리수 매핑
- 스폰 위치가 지형 위(NavMesh 위)인지

### 3.2 몬스터 프리팹
- AnimalAI 프리팹들 (토끼, 멧돼지, 늑대, 사슴, 독사, 자이언트랫)
- 각 프리팹에 MeshRenderer, Animator, AnimalAI 컴포넌트 있는지

---

## Phase 4: 병사/가드 시스템
**목표**: 각 영지에 가드 플레이스홀더(병사) 배치

### 4.1 GuardManager
- GuardManager가 병사 풀 관리하는지
- 영지별 병사 수/레벨/장비 매핑 (RingDifficultyData)

### 4.2 GuardPlaceholder
- 병사 모델(메시) + 애니메이터 + GuardCombatAI
- 장비 시스템 연동 (GuardEquipmentSystem)

---

## Phase 5: HUD/미니맵 최종 정비
**목표**: BotW 스타일 UI 완성

### 5.1 하트 시스템
- 좌상단 하트 컨테이너 (5개 = 100HP)
- 데미지 시 흔들림, 임시 하트(노랑)

### 5.2 미니맵
- 우하단 원형 미니맵 (400x400)
- 플레이어 화살표, 영지 점들, 줌 기능

### 5.3 기타 HUD
- 버프 아이콘 (우상단)
- 가스 분사기 타이머 (상단 중앙)
- 은신 HUD (하트 아래)

---

## Phase 6: 전체 통합 테스트
**목표**: 예시.jpg와 유사한 게임 화면 완성

### 6.1 Play 모드 전체 플로우
1. 씬 로드 → CoreSystemsBootstrap 초기화
2. GameManager.Start() → 모든 시스템 생성
3. TerritoryBuilder → 건물/병사 스폰
4. MonsterSpawner → 몬스터 스폰
5. 카메라 → 플레이어 추적 시작
6. HUD 표시

### 6.2 성능/안정성
- 프레임레이트 60fps 유지
- 메모리 누수 없음
- 세이브/로드 정상

---

## 즉시 시작: Phase 1 실행

현재 가장 시급한 건 **Phase 1** - Play 모드에서 아무것도 안 보이는 근본 원인 파악.

의심되는 원인들:
1. 카메라가 플레이어를 안 보고 있음 (위치/회전 오류)
2. 플레이어 MeshRenderer가 레이어/머티리얼 문제로 안 보임
3. 지형이 있지만 카메라 컬링마스크에 안 걸림
6. 라이트/스카이박스가 없어서 검은 화면

**다음 액션**: Phase 1 진단 스크립트 만들어 Play 모드 실제 상태 확인