# 완전한 게임 화면 구현을 위한 Phase 계획

## 현재 상태 분석 (2026-08-27)

### 문제점 (스크린샷 vs 코드 실제 상태)
| 항목 | 코드(저장된 씬) | 사용자 Editor 화면 | 원인 |
|------|----------------|-------------------|------|
| **Player** | 씬에 존재 (Player + PlayerModel) | **안 보임** | Editor에 구형 씬 로드됨 / Fix 미실행 |
| **Environment** | GameObject 존재 | **자식 없음** (m_Children: []) | TerrainModelPlacer가 모델 생성 안 함 |
| **지형 모델(풀/바위/나무)** | Resources에 존재 | **안 보임** | 배치 로직 미작동 또는 Resources 로드 실패 |
| **몬스터** | MonsterSpawner 존재 | **안 보임** | 영지 시스템 미초기화, 스폰 조건 미충족 |
| **영지/건물/깃발** | TerritoryBuilder 있음 | **안 보임** | TerritoryBuilder 미실행 |
| **HUD/미니맵** | GameObject 존재 | **플레이스홀더만** | 런타임 데이터 연결 안 됨 |

---

## Phase 1: 씬 완전 재생성 및 검증 (즉시 실행)

### 1-1. FixMainScene 강제 재실행
```csharp
// Unity Editor 메뉴에서 실행
Tools > Poison > Fix MainScene
```
- 빈 씬에서 모든 시스템 재생성
- Player 강제 생성 (Biped 애니메이션 스택)
- TerrainModelPlacer.Place() 실행 확인
- 씬 저장 강제 (EditorSceneManager.SaveScene)

### 1-2. 배치모드 검증으로 강제 동기화
```bash
# 터미널에서 실행 (Editor 캐시 무시)
cd /mnt/c/Unity/code
./run_unity_fix.sh
```

### 1-3. Environment 자식 확인
```csharp
// FixMainScene.Verify() 에 추가할 검증 코드
var env = GameObject.Find("Environment");
Debug.Log($"Environment children: {env.transform.childCount}"); // 0이면 실패
```

**성공 기준**: Environment 하위에 grass/rock/tree GameObject 1000개 이상 생성됨

---

## Phase 2: TerrainModelPlacer 디버깅 및 수정

### 2-1. Resources 로드 확인
```csharp
// TerrainModelPlacer.Place() 시작 부분에 추가
var grassModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/grass");
Debug.Log($"[TerrainModelPlacer] grassModels: {grassModels.Length}"); // 7개여야 함
var rockModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/rocks");
Debug.Log($"[TerrainModelPlacer] rockModels: {rockModels.Length}");  // 5개여야 함
var treeModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/trees");
Debug.Log($"[TerrainModelPlacer] treeModels: {treeModels.Length}");  // 6개여야 함
```

### 2-2. Raycast 실패 방지
- MeshCollider가 ground에 있는지 확인
- Raycast 거리 2000f → 5000f 증가
- 높이 오프셋 조정 (yMinOffset/yMaxOffset)

### 2-3. 국가별 필터링 문제 해결
- `NationGrassPrefix`에 "East" 키가 있지만 실제 모델명은 "grass1", "grass 2" 등임
- 필터링 로직 수정: 국가별 접두사 없을 때 기본 모델 사용

### 2-4. GPU Instancing 활성화 확인
- Material에 `enableInstancing = true` 설정
- URP Lit 쉐이더에서 `_TERRAIN_NORMAL_MAP` 키워드

---

## Phase 3: Player 완벽 생성 및 카메라 설정

### 3-1. Player 생성 검증
```csharp
// CreatePlayer()에서 반드시 실행되어야 할 것들
1. GameObject "Player" 생성 (tag="Player", layer=8)
2. CharacterController 설정 (height=1.8, radius=0.4)
3. Player_Rigged.glb 로드 및 인스턴스화
4. ModelAnimatorAssigner + ForceBiped(true)
5. SkinnedMeshRenderer bounds 강제 설정 (컬링 방지)
6. PlayerInput + PlayerControls.inputactions 연결
```

### 3-2. 카메라 시스템 (Cinemachine 3.x)
- Main Camera: CinemachineBrain, ClearFlags=Skybox
- Player Camera: CinemachineCamera + ThirdPersonFollow
  - CameraDistance: 25m
  - VerticalArmLength: 8m
  - ShoulderOffset: (2.5, 0, 0)
- CameraZoomControllerRuntime 부착 (마우스 휠 줌)

---

## Phase 4: 몬스터 스폰 시스템 완성

### 4-1. MonsterSpawner 영지 연동
```csharp
// MonsterSpawner.cs 핵심 로직
- TerritoryManager.CurrentTerritoryId 확인
- RingDifficultyData.GetMonsterTiersForDifficulty(territoryDifficulty)
- RingDifficultyData.GetMonsterCountRange(territoryDifficulty)
- ModelAnimatorAssigner 자동 부착 (몬스터 타입별 Biped/Quadruped/Fly/Swim)
```

### 4-2. 몬스터 GLB 22종 매핑
| 몬스터 | 타입 | 애니메이션 |
|--------|------|-----------|
| Rabbit, Boar, Deer | Quadruped | QuadrupedProceduralLocomotion |
| Banshee, Crow, Griffon | Fly | SpecialCreatureAnimator (Fly) |
| Golem, Minotaur | Biped | ProceduralAnimationController |
| Bat, Fire_Lizard | Special | Custom |

### 4-3. 스폰 테스트
- Play Mode 진입 시 현재 영지에서 몬스터 5-10마리 스폰 확인

---

## Phase 5: 영지 시스템 (건물, 깃발, NPC)

### 5-1. TerritoryBuilder 실행
```csharp
// Tools > Poison > Build All Territories
// 또는 FixMainScene 내 자동 실행
TerritoryBuilder.BuildAllTerritories();
```

### 5-2. 영지별 건물 배치
| 건물 타입 | GLB | 국가별 변형 |
|----------|-----|-------------|
| 성(Castle) | blue_castle/west_castle/etc | 국가별 색상 |
| 상점 | craft_equipment, craft_cook, craft_blend | 공통 |
| 교회 | kingdom | 공통 |
| NPC 주택 | hut | 공통 |
| 깃발 | east_flag/west_flag/north_flag | 국가별 |

### 5-3. FlagPoleDisplay 페이드 트랜지션
- 영지 진입 시 깃발 페이드 인 (1초)
- 영지 변경 시 페이드 아웃 → 인

---

## Phase 6: GuardManager 병사 시스템

### 6-1. 병사 GLB 레벨별 스폰
```csharp
// GuardManager.TryRefillTerritory()
if (level <= 20) model = "Soldier_Lv1-20_Rigged.glb";
else if (level <= 40) model = "Soldier_Lv20-40_Rigged.glb";
else model = "Soldier_Lv40-50_Rigged.glb";
```

### 6-2. 장비 파츠 자동 장착
```csharp
// 레벨별 티어
1-10: wood_* (armor, helmet, boots, gloves, sword, shield)
11-25: steel_*
26-40: crystal_*
41-50: stone_*
```

### 6-3. ModelAnimatorAssigner.ForceBiped() 부착

---

## Phase 7: HUD & 미니맵 런타임 연동

### 7-1. HUD (BotW 스타일)
- 좌상단: 하트 5개 (1개 = 20HP, 최대 100HP)
- PlayerHealth.OnHPChanged 이벤트 연결
- 하트 스프라이트: Full/Half/Empty 프로그래매틱 생성

### 7-2. 미니맵
- 우하단: 200x200 RenderTexture
- 별도 Orthographic 카메라 (Player 추적, 회전 동기화)
- 마커: Player(삼각형), 몬스터(빨간점), 병사(파란점), 건물(아이콘)

### 7-3. 버프 UI
- 우상단: 버프 아이콘 + 남은 시간

---

## Phase 8: 포스트프로세싱 & 라이팅 완성

### 8-1. Volume 프로파일
- **Tonemapping**: ACES
- **Bloom**: Intensity 1.5, Threshold 1.0, Scatter 0.7
- **ColorGrading**: Temperature +20, Tint +5 (따뜻한 톤)
- **Fog**: Exponential, Density 0.008, Color 따뜻한 회색
- **Vignette**: Intensity 0.2, Smoothness 0.4

### 8-2. 라이팅
- Sun: Directional, 1.2 intensity, Soft shadows, 50°/-30°
- Moon: Directional, 0.15 intensity, 야간용
- Skybox: Procedural (DayNightCycle 연동)

---

## Phase 9: 최종 통합 테스트 (30초 Play Mode)

### 9-1. 체크리스트
| 항목 | 확인 방법 | 성공 기준 |
|------|----------|----------|
| **Player 이동** | WASD 30초 | 부드러운 이동, 애니메이션 재생 |
| **카메라** | 마우스 우클릭 드래그 + 휠 | 25m 거리, 어깨 시점, 줌 작동 |
| **지형 모델** | Scene/View 거리별 | 3링(0-350/700/1000m) 모델 보임 |
| **몬스터** | 현재 영지에서 | 5-10마리 스폰, 애니메이션, AI 작동 |
| **병사** | 플레이어 영지에서 | 레벨별 GLB + 장비, 순찰/전투 |
| **영지 건물** | 각 영지 중심 | 성/상점/교회/깃발/NPC 하우스 |
| **HUD** | HP 변경 시 | 하트 실시간 업데이트 |
| **미니맵** | 이동/회전 시 | 플레이어 추적, 마커 표시 |
| **콘솔 에러** | Play Mode 30초 | **0개** (Job 메모리 누수 없음) |

### 9-2. Job 메모리 누수 검증
```bash
# Unity 로그에서 확인
# "NativeArray has not been disposed" 없음
# "JobTempAlloc has allocations" 없음
```

---

## Phase 10: Git 커밋/푸시 및 문서화

### 10-1. 변경사항 저장
```bash
git add -A
git commit -m "Complete Game View Implementation: Phase 1-9

- FixMainScene: 완전 자동 씬 생성 (Player, Terrain, Camera, Lighting, HUD, Systems)
- TerrainModelPlacer: 3링 GPU Instancing 환경 모델 배치 (풀/바위/나무 1000+)
- MonsterSpawner: 영지 난이도 기반 스폰 + ModelAnimatorAssigner 타입별 분기
- GuardManager: 병사 레벨별 GLB + 장비 파츠(wood→steel→crystal→stone)
- TerritoryBuilder: 국가별 성/상점/교회/깃발/NPC 하우스 + FlagPoleDisplay 페이드
- HUD: BotW 하트(좌상단), 버프(우상단), 미니맵(우하단, 회전 추적)
- PostProcessing: ACES + Bloom 1.5 + Warm ColorGrading + Fog 0.008 + Vignette
- Verify: EditMode 테스트 통과, 배치모드 Fix/Verify exit code 0
- 30초 Play Mode: 콘솔 에러 0개, 모든 시스템 작동 확인"

git push origin master
```

### 10-2. QAPROGRESS.md 업데이트
- Phase별 완료 체크
- 메트릭스 기록 (모델 수, 스폰 수, FPS 등)

---

## 즉시 실행 명령어

```bash
# 1. Unity Editor에서 강제 재생성
# Tools > Poison > Fix MainScene

# 2. 배치모드 검증 (CI 방식)
cd /mnt/c/Unity/code
/mnt/c/Program\ Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe \
  -batchmode -projectPath "C:/Unity/code" \
  -executeMethod FixMainScene.Fix -quit -logFile -

# 3. 검증
/mnt/c/Program\ Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe \
  -batchmode -projectPath "C:/Unity/code" \
  -executeMethod FixMainScene.Verify -quit -logFile -

# 4. 환경 모델 수동 배치 (필요시)
# Tools > Poison > Place Environment Models

# 5. 영지 건물 배치
# Tools > Poison > Build All Territories
```

---

## 예상 소요 시간
| Phase | 예상 시간 |
|-------|----------|
| 1. 씬 재생성 | 5분 |
| 2. TerrainModelPlacer 수정 | 10분 |
| 3. Player/카메라 검증 | 5분 |
| 4. 몬스터 스폰 | 10분 |
| 5. 영지 건물 | 10분 |
| 6. 병사 시스템 | 5분 |
| 7. HUD/미니맵 | 10분 |
| 8. 포스트프로세싱 | 5분 |
| 9. 통합 테스트 | 10분 |
| 10. 커밋/문서화 | 5분 |
| **총계** | **~75분** |

---

## 성공 보장 포인트
1. **FixMainScene.Fix()를 Editor에서 반드시 실행** (배치모드만으론 Editor 씬 동기화 안 됨)
2. **TerrainModelPlacer의 Resources.LoadAll 경로 정확히 일치** (Models/UserProvided/terrain/grass 등)
3. **Player_Rigged.glb가 Biped로 강제 설정** (ForceBiped(true))
4. **NationTerrainController가 CurrentNation 반환** (영지별 텍스처/모델 블렌딩)
5. **모든 GLB에 ModelAnimatorAssigner 부착** (자동 타입 감지 또는 강제)