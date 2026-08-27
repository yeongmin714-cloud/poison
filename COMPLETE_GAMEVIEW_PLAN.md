# 🎯 **완전한 게임 화면 구현을 위한 상세 실행 계획**

## 📋 **현황 정확한 분석 (2026-08-27)**

### ❌ **핵심 문제: Editor 씬 동기화 실패**
| 항목 | 디스크(저장된 씬) | 사용자 Editor 화면 | 원인 |
|------|------------------|-------------------|------|
| **Player** | 존재함 (line 119987) | **안 보임** | Editor에 구형 씬 로드됨 |
| **Environment** | 1310개 모델 존재 | **자식 없음** | 동일 - 씬 미리로드 |
| **지형 모델** | 배치 완료 (TerrainModelPlacer) | **안 보임** | 씬 미리로드 |
| **몬스터/영지** | 시스템 존재 | **안 보임** | 씬 미리로드 |

**진단**: `FixMainScene.Fix()`는 **배치모드**로만 실행되어 디스크에 저장만 함. **Unity Editor가 열려있는 상태에서 씬이 자동 리로드되지 않음**. 사용자는 여전히 구형 씬을 보고 있음.

---

## 🎯 **최종 목표 상태 (예시.PNG와 일치)**
- **지형**: 2km × 2km 하이트맵 + 1310개 GLB 모델(풀/바위/나무) 3링 배치
- **플레이어**: Player_Rigged.glb (Biped 애니메이션 스택) - 이동/전투/상호작용
- **카메라**: Cinemachine 3.x ThirdPersonFollow (25m 어깨 시점, 마우스 휠 줌)
- **몬스터**: 현재 영지 난이도별 5-10마리 스폰, AI 작동
- **병사/영지**: 레벨별 GLB + 장비, 성/상점/교회/깃발/NPC 하우스
- **HUD**: 좌상단 BotW 하트(1=20HP), 우상단 버프, 우하단 회전 미니맵
- **포스트프로세싱**: ACES + Bloom 1.5 + Warm ColorGrading + Fog 0.008 + Vignette

---

## 📦 **Phase별 상세 실행 계획**

---

### **Phase 0: Editor 씬 강제 동기화 (가장 중요 - 5분)**

#### 0-1. Editor에서 씬 강제 리로드
```csharp
// Unity Editor 메뉴에서 실행 (배치모드로는 Editor 씬 갱신 안 됨)
Tools > Poison > Fix MainScene
```
- **필수**: Unity Editor가 열려있는 상태에서 실행해야 함
- 빈 씬에서 모든 시스템 재생성 → 씬 저장 → Editor 뷰 즉시 반영

#### 0-2. 대안: 수동 씬 열기
```
File > Open Scene > Assets/Scenes/MainScene.unity
```
- 저장된 최신 씬 강제 로드

#### 0-3. 검증 체크리스트 (Editor에서 실행 후 즉시 확인)
- [ ] Hierarchy에 `Player` 존재
- [ ] `Player > PlayerModel` 존재 + `ModelAnimatorAssigner` + `ForceBiped`
- [ ] `Ground_Inner > Environment` 하위 1310개 모델 존재
- [ ] `Main Camera` + `Player Camera` (Cinemachine)
- [ ] `MonsterSpawner`, `GuardManager`, `TerritoryBuilder` 등 모든 시스템

---

### **Phase 1: Player 완벽 생성 및 카메라 연동 (15분)**

#### 1-1. Player 생성 로직 검증 및 강화 (`FixMainScene.cs > CreatePlayer()`)
```csharp
// 핵심 체크포인트
1. GameObject "Player" 생성 (tag="Player", layer=8)
2. CharacterController (height=1.8, radius=0.4, center=0,0.9,0)
3. Player_Rigged.glb 로드 → Instantiate → PlayerModel
4. ModelAnimatorAssigner + ForceBiped(true) 강제
5. SkinnedMeshRenderer bounds 강제 설정 (컬링 방지)
   - smr.localBounds = new Bounds(Vector3.zero, new Vector3(2, 4, 2))
6. PlayerInput + PlayerControls.inputactions 연결
7. 모든 컴포넌트 부착: Movement, Health, Stats, Inventory, Combat, BuffManager
```

#### 1-2. 카메라 시스템 검증 (`CreateCameraSystem()`)
```csharp
// Main Camera
- CinemachineBrain, ClearFlags=Skybox, CullingMask=Everything

// Player Camera (Virtual Camera) - **별도 루트 오브젝트**
- CinemachineCamera + ThirdPersonFollow
- CameraDistance: 25f, VerticalArmLength: 8f
- ShoulderOffset: (2.5, 0, 0), CameraSide: 1 (우측)
- Damping: (1, 0.5, 1)
- CameraZoomControllerRuntime 부착 (마우스 휠 줌)
```

#### 1-3. Player 초기 위치 보장
```csharp
player.transform.position = new Vector3(0, 2, 0); // 지형 중앙, 지면 위
```

---

### **Phase 2: 지형 및 환경 모델 완벽 배치 (15분)**

#### 2-1. 하이트맵 생성 검증 (`CreateHeightmapTerrain()`)
```csharp
// TerrainGenerator.GenerateTerrain(BiomeType.Plains, 42, 100, 2000f)
// 반환: terrainMesh(2000x2000), waterMesh
// MeshCollider 필수 (TerrainModelPlacer용)
```

#### 2-2. 프로시저럴 텍스처 & 머티리얼 (`CreateProceduralControlMap` 등)
```csharp
// 4개 텍스처 생성 후 Asset 저장 → 리로드 → 머티리얼 할당
1. ControlMap (256x256) - 스플랫맵
2. Grass Texture - _Splat0
3. Dirt Texture - _Splat1  
4. Normal Texture - _Normal0
// URP Terrain/Lit 쉐이더 + _TERRAIN_NORMAL_MAP 키워드
```

#### 2-3. 환경 모델 배치 - **Perlin Noise 직접 샘플링 방식** (`TerrainModelPlacer.Place()`)
```csharp
// 핵심: Raycast 대신 수학적 높이 계산
float height = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
float y = height + Random.Range(yMinOffset, yMaxOffset);

// 3링 구성 (거리 기반 밀도)
Ring 1: 0-350m   - grass 500, tree 80, rock 100
Ring 2: 350-700m - grass 300, tree 50, rock 60
Ring 3: 700-1000m - grass 150, tree 30, rock 40

// 국가별 잔디 필터링 (Empire/West/South/North/East)
// LODGroup 3단계 (0.6/0.3/0.0 거리별)
// GPU Instancing 활성화 (Material.enableInstancing = true)
```

#### 2-4. 물 시스템 (`CreateWaterSystem()`)
```csharp
// LakeGenerator + WaterBody
// 저지대(waterThreshold=0.4f 이하) 자동 물 메시 생성
// 투명 머티리얼 + 반사
```

---

### **Phase 3: 스포너 및 AI 시스템 완성 (20분)**

#### 3-1. MonsterSpawner - 영지 난이도 기반 (`MonsterSpawner.cs`)
```csharp
// 핵심 로직
TerritoryId currentId = TerritoryManager.CurrentTerritoryId;
TerritoryDifficulty diff = TerritoryDatabase.GetDefinition(currentId).difficulty;
var tiers = RingDifficultyData.GetMonsterTiersForDifficulty(diff);
var countRange = RingDifficultyData.GetMonsterCountRange(diff);

// 22종 몬스터 타입별 ModelAnimatorAssigner 자동 부착
// Biped/Quadruped/Fly/Swim/Special 분기
```

#### 3-2. GuardManager - 병사 레벨별 GLB + 장비 (`GuardManager.cs`)
```csharp
// 레벨별 모델
Lv 1-20: Soldier_Lv1-20_Rigged.glb
Lv 21-40: Soldier_Lv20-40_Rigged.glb
Lv 41-50: Soldier_Lv40-50_Rigged.glb

// 장비 파츠 (레벨별 티어)
1-10: wood_* (armor, helmet, boots, gloves, sword, shield)
11-25: steel_*
26-40: crystal_*
41-50: stone_*

// ModelAnimatorAssigner.ForceBiped() 부착
```

#### 3-3. 기존 플레이스홀더 마이그레이션 완료 확인
- `SkeletonGuardPlaceholder`, `PlayerPlaceholder`, `TerritoryNPCSpawner`, `ModelSwapper`
- 모두 `ModelAnimatorAssigner` 새 방식 사용 중인지 확인

---

### **Phase 4: 영지 시스템 - 건물/깃발/NPC (20분)**

#### 4-1. TerritoryBuilder 실행 (`TerritoryBuilder.cs`)
```csharp
// Tools > Poison > Build All Territories
// 또는 FixMainScene 내 자동 실행

// 영지별 배치
- 국가별 성: blue_castle/west_castle/green_castle 등
- 상점: craft_equipment, craft_cook, craft_blend
- 교회: kingdom
- NPC 주택: hut
- 깃발: east_flag/west_flag/north_flag + FlagPoleDisplay 페이드 트랜지션
```

#### 4-2. NationTerrainController - 3링 텍스처 블렌딩
```csharp
// Inner Ring (0-500m): 국가 고유 텍스처 100%
// Middle Ring (500-1000m): 블렌딩 존
// Outer Ring (1000m+): 인접 국가 텍스처 블렌딩
// DecalSpawnerIntegration으로 경계 데칼
```

---

### **Phase 5: HUD & 포스트프로세싱 완성 (15분)**

#### 5-1. HUD 시스템 (`HUDSystem.cs` + `FixMainScene.CreateHUDSystem()`)
```csharp
// BotW 스타일 하트 (좌상단)
- 5개 하트, 1개 = 20HP, 최대 100HP
- Full/Half/Empty 프로그래매틱 스프라이트 생성
- PlayerHealth.OnHPChanged 이벤트 연동

// 버프 UI (우상단)
- 6개 슬롯, 아이콘 + 남은 시간 표시
- BuffManager 이벤트 연동

// 미니맵 (우하단)
- 200x200 RenderTexture + Orthographic 카메라
- 플레이어 추적 + 회전 동기화
- 마커: 플레이어(삼각형), 몬스터(빨간점), 병사(파란점), 건물(아이콘)
```

#### 5-2. 포스트프로세싱 Volume (`CreatePostProcessingVolume()`)
```csharp
// Global Volume (isGlobal=true, priority=100)
- Tonemapping: ACES
- Bloom: Intensity 1.5, Threshold 1.0, Scatter 0.7
- ColorAdjustments: PostExposure 0.2, Contrast 10, ColorFilter Warm(1,0.95,0.85), Saturation 15
- LiftGammaGain: Gamma (1.05, 1.02, 0.98) 따뜻한 톤
- Vignette: Intensity 0.15, Smoothness 0.4
// Fog: RenderSettings (URP 17+ Volume Fog 제거됨)
- FogMode.Exponential, Density 0.0008, Color (0.6, 0.7, 0.85)
```

---

### **Phase 6: 최종 검증 및 Play Mode 테스트 (15분)**

#### 6-1. 배치모드 검증 (CI 파이프라인)
```bash
# 1. Fix 실행
/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe \
  -batchmode -projectPath "C:/Unity/code" \
  -executeMethod FixMainScene.Fix -quit -logFile -

# 2. Verify 실행
/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe \
  -batchmode -projectPath "C:/Unity/code" \
  -executeMethod FixMainScene.Verify -quit -logFile -
```

#### 6-2. **Editor 내 Play Mode 30초 테스트 (필수 - 시각 확인)**
```
☐ Play 버튼 클릭
☐ 30초간 WASD 이동 - 부드러운 이동, 애니메이션 재생 확인
☐ 마우스 우클릭 드래그 + 휠 - 카메라 25m 거리, 어깨 시점, 줌 작동
☐ 지형 모델 확인 - 3링(0-350/700/1000m) 풀/바위/나무 보임
☐ 현재 영지에서 몬스터 5-10마리 스폰, 애니메이션, AI 작동
☐ 플레이어 영지에서 병사 레벨별 GLB + 장비, 순찰/전투
☐ 각 영지 중심에 성/상점/교회/깃발/NPC 하우스
☐ HP 변경 시 하트 실시간 업데이트
☐ 이동/회전 시 미니맵 플레이어 추적, 마커 표시
☐ 콘솔 에러 0개 (Job 메모리 누수 없음)
```

---

## ⚠️ **예상 리스크 & 대응 방안**

| 리스크 | 발생 가능성 | 대응 방안 |
|--------|-------------|-----------|
| **Editor 씬 미리로드** | 🔴 매우 높음 | **Phase 0 필수 실행** - Editor에서 FixMainScene 실행 |
| **Player_Rigged.glb Biped 미인식** | 🟡 중간 | `ForceBiped(true)` 강제 + `Avatar.isHuman=false` 처리됨 |
| **Resources.Load 경로 불일치** | 🟡 중간 | `Models/UserProvided/terrain/grass` 등 경로 정확히 일치 확인 |
| **MonsterSpawner 영지 ID 미획득** | 🟡 중간 | `TerritoryManager.Instance` 초기화 순서 보장 |
| **HUD/미니맵 런타임 NullReference** | 🟢 낮음 | `HUDManager.Instance` 싱글톤 패턴 + null 체크 |
| **Job 메모리 누수** | 🟢 낮음 | `Allocator.TempJob` using 블록 + try-finally 강제 |

---

## 📊 **성공 판정 기준 (Definition of Done)**

| 기준 | 확인 방법 | 목표값 |
|------|-----------|--------|
| **씬 동기화** | Editor Hierarchy Player 존재 | ✅ O |
| **지형 모델** | Environment children ≥ 1000 | ✅ 1310 |
| **플레이어 애니메이션** | ModelAnimatorAssigner + 3개 컨트롤러 | ✅ O |
| **카메라** | 25m 어깨 시점 + 줌 작동 | ✅ O |
| **몬스터 스폰** | 현재 영지 5-10마리 | ✅ O |
| **병사/영지** | 레벨별 GLB + 장비 + 건물 | ✅ O |
| **HUD/미니맵** | 하트/버프/회전 미니맵 작동 | ✅ O |
| **포스트프로세싱** | ACES/Bloom/Warm/Fog/Vignette | ✅ O |
| **Play Mode 30초** | 콘솔 에러 0개 + 시각 확인 | ✅ O |
| **배치모드 검증** | Fix/Verify exit code 0 | ✅ O |

---

## ⏱️ **전체 예상 소요 시간: ~90분**

| Phase | 작업 | 예상 시간 |
|-------|------|-----------|
| 0 | Editor 씬 강제 동기화 | 5분 |
| 1 | Player/카메라 검증 | 15분 |
| 2 | 지형/환경 모델 | 15분 |
| 3 | 스포너/AI | 20분 |
| 4 | 영지 시스템 | 20분 |
| 5 | HUD/포스트프로세싱 | 15분 |
| 6 | 최종 검증/Play Mode | 15분 |
| **합계** | | **~90분** |

---

## 🚀 **즉시 실행 명령어 (순서대로)**

```bash
# 1. Editor에서 실행 (가장 중요!)
# Unity Editor 메뉴: Tools > Poison > Fix MainScene

# 2. 배치모드 검증 (CI)
cd /mnt/c/Unity/code
/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe \
  -batchmode -projectPath "C:/Unity/code" \
  -executeMethod FixMainScene.Fix -quit -logFile -

/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe \
  -batchmode -projectPath "C:/Unity/code" \
  -executeMethod FixMainScene.Verify -quit -logFile -

# 3. Editor에서 Play Mode 30초 시각 테스트 (필수)

# 4. 커밋/푸시
git add -A && git commit -m "Final Game View: Player+Terrain+Monsters+Territories+HUD+PP" && git push origin master
```

---

## 📤 **텔레그램 알림 전송**

이 계획을 텔레그램(챗 ID: 6847418902)으로 전송합니다.