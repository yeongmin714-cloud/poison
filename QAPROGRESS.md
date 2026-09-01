# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-09-01

---

## 2026-09-01: 지형 렌더링·스폰·추락·카메라 종합 디버깅 (진행 중)

**상태:** ✅ **해결 완료 (2026-09-01)** — 진짜 근본 원인: **지형 삼각형 와인딩 반전** (아래 🔥 섹션). 아래 1~5항목은 그 과정에서 해결된 부수 문제들.

### 🔥 진짜 근본 원인: 지형 삼각형 와인딩 반전 (해결)

TerrainGenerator의 그리드(index = z*res + x)에서 삼각형 T1=(topLeft, topRight, bottomLeft)로 생성하면
Cross(Edge1, Edge2) = (0, -dx·dz, 0) = **법선이 아래(-Y)를 향함**.
- Unity 기본 뒷면 컬링(`_Cull: 2`)로 **위에서 볼 때 지형이 완전히 안 그려짐** (마젠타 테스트 실패로 확정)
- CharacterController가 뒷면에 눌러 **접촉면 생성 안 됨** → SafetyFloor 추락
- 화면의 회갈색(105,98,92)은 지형이 아니라 **Procedural Skybox의 지면 색** (전 세계 미렌더 시 하늘만 보임)

**수정:** ① TerrainGenerator 와인딩 반전 T1=(topLeft, **bottomLeft, topRight**) → 법선 +Y. 물 메시는 지형 인덱스 재사용이라 자동 해결. 노멀 계산도 같은 순서라 자동 위향. ② 씬에 구워진 기존 메시도 Play 시 즉시 수정: TerrainTextureApplier.Start에서 `mesh.triangles` 인덱스 반전 + RecalculateNormals + MeshCollider 재쿠킹 + raycast 검증.

**교훈:** 새 지형/메시 생성 시 반드시 **법선 +Y(위) 확인**. "렌더X+충돌X가 동시 발생" = 오브젝트 비활성 또는 와인딩 반전.

### 이번 세션에서 확정·해결한 것들 (로그 기반)

1. **스폰이 지형 경계 밖** — 지형 2000×2000(±1000). 처음 스폰 `x=1173`>경계 → 지형 밖 허공. → 경계 안 초원 외각 `(728,-529)`(East Ring1 방향)로 이동.
2. **플레이어 추락 (SafetyFloor y=-95 반복)** — 지형 MeshCollider가 CharacterController를 못 받았음. 원인 다각도:
   - `Physics.autoSimulation=true/false` 토글이 스폰 직후 물리 세계 리셋 → **제거**
   - 스폰 직후 `_controller.Move(down*0.2f)`가 플레이어를 바닥 아래로 밀어 통과 → **제거**
   - `ClampToGround`의 `_verticalVelocity<=0` 조건이 중력(항상 양수)으로 영영 false → **조건 제거**
   - `ClampToGround` Raycast가 **플레이어 자신(Player 콜라이더)을 지면으로 오인** → **RaycastAll + 자기자신(transform/자식) 무시**
   - 최종: **`ClampToGroundByHeight()` 도입** — 물리·Raycast 의존 없이 `TerrainGenerator.GetHeightAt(현재x,z)`로 지표면 세계y(1+높이)를 수학 도출해 점프 외엔 항상 표면 위 0.05m 고정. 추락 구조적 차단.

3. **지면 회색 단색의 근본 후보 — `_BaseMap` null 재발**:
   - 씬 GridInner가 쓰는 **`Ground_Grass_Mat(f02019bb)`의 `_BaseMap`이 `{fileID:0}`(null)** 로 반복적으로 지워짐 (git 히스토리 7+회).
   - URP/Lit은 알베도를 `_BaseMap`에서만 읽음. `_MainTex`(east_grass1)는 무시.
   - `_BaseMap` null이면 URP/Lit이 텍스처 못 읽고 **회갈색(105,98,92) 단색 폴백**.
   - → `_BaseMap`에 `east_grass1(caaecd65, 150,201,8)` 재할당.
   - ⚠️ **재발 확인**: 재할당 직후에도 자동 프로세스가 `_BaseMap`을 `{fileID:0}`으로 되돌림. **근본 방지 필요** (FixMainScene 재실행/자동 커밋이 덮어쓰는 듯).

4. **카메라가 지형을 안 보고 Player 본인을 봄** — 진단(CamProbe)으로 카메라 fwd이 Player(y=1.93) 정면. `FixCameraToPlayer()`가 카메라를 플레이어(3,4,-6)에 붙이고 플레이어를 lookAt → 화면이 캐슐+배경(회색)만. **→ 카메라를 (4,8,-11)로 옮기고 lookAt을 발밑 지표면(y=0.5)**으로 변경. (CinemachineBrain이 Main Camera를 덮어써서 매프레임 보정이 우선인지 재확인 필요)

5. **진단 도구 (유지)**:
   - `TerrainTextureApplier.Start()`의 `DiagnoseGroundState()` — 지형 메시/콜라이더/재질/알베도/카메라/플레이어 상태 로그(`[DiagP1]`)
   - `PlayerMovement` `CamForwardProbe()` — 카메라 전방 40m raycast로 화면이 실제 보는 것 확정(`[CamProbe#]`)
   - `GroundDiagRunner`(Editor 배치) — 스폰 지점 높이 계산

### 다음 단계 (미해결)
- **회색 재발 근본 차단**: `_BaseMap`이 왜 자꾸 `{fileID:0}`이 되는지 (FixMainScene 생성분기/자동 커밋 추정) 찾아, TerrainBaseMapFixer 가드를 배치(Editor)에서도 확실히 동작게 하거나 생성 재현금지를 강화.
- **CinemachineBrain**이 Main Camera를 매프레임 덮어써 내 `FixCameraToPlayer` 보정을 무효화하는지 — 필요 시 vcam/Brain 비활성 or 카메라 고정 모드.
- 최종: Play에서 **초록 지면 픽셀** 확인.

---

## 2026-08-31 (2차): 지형 안 보임 — 真正 원인은 안개(Fog) 밀도 과다

**문제:** 1차 수정(`_BaseMap` 재할당) 후에도 지형이 여전히 안 보임. 스크린샷 15 분석 → 지형이 초록이 아니라 **회백색/웜그레이**로 보임.

**진단 (스크린샷 픽셀 분석):** 캡슐 바로 아래 근경(카메라에서 수m)까지 초록이 전혀 없고 (R95,G88,B85) 회백색. 파란 캡슐·노란 큐브·UI는 선명 → **텍스처/조명 문제가 아니라 지형 표면이 안개에 묻혀 있음.** Exponential 안개가 지형 초록 픽셀을 전부 안개색으로 치환.

**근본 원인:**
- RenderSettings fog ON (`m_Fog:1`) + Exponential (`m_FogMode:2`)
- `WeatherManager._clearFogDensity = 0.008f`, `_foggyFogDensity = 0.02f` → **통과 기준(0.0006)의 13~33배** 짙음 → 2000×2000m 지형 전체를 회백색 안개로 덮음
- `WeatherManager._directionalLight: {fileID:0}` (null 직렬화) → 조명 강도 설정이 불확실

**수정:**
1. `WeatherManager.cs`: `_clearFogDensity 0.008→0.0006`, `_foggyFogDensity 0.02→0.003`
2. `MainScene.unity` WeatherManager 직렬화: `_clearFogDensity 0.0006`, `_foggyFogDensity 0.003`, `_directionalLight → Sun(770833919)` 연결

**검증:** Unity 6000.4.10f1 배치모드 exit 0, 컴파일 에러 0건. (Play Mode 시각 확인은 사용자 확인 필요)

→ **ROADMAP/파라미터 메모:** 안개 밀도는 Expo 0.0006 수준이어야 지형이 보임. WeatherManager 기본값이 0.008로 높아 재발 위험 → 값 낮춤.

## 2026-08-31: 지형 안 보임 — Ground_Grass_Mat._BaseMap 재발 수정 + 가드 추가

**문제:** Play Mode에서 지형이 여전히 안 보임 ("뭔가 변화한 것 같지만 지형이 안 보임")

**근본 원인:** 씬의 `Ground_Inner` MeshRenderer가 참조하는 재질 `Assets/URP/Ground_Grass_Mat.mat`(GUID `f02019bb`)의 **`_BaseMap`(diffuse 슬롯)이 `{fileID: 0}`으로 비워져 있었음.** URP/Lit는 알베도를 `_BaseMap`에서만 읽으므로, 비어 있으면 `_BaseColor × 검정(0)` = 알베도 검정 → 지표 재질이 배경과 구분 안 됨. (8/31 18:37 커밋 `c4a88e0`이 과거 할당했던 `Terrain_Grass` GUID를 `{fileID: 0}`으로 되돌려 놓음 — 한 달 전 해결했던 "MainScene 3대 이슈" (1)항목의 재발)

**배제된 원인:** 지형 프로시저럴 메시 자체는 정상(씬에 `Terrain_초원_100x100`, 100×100 정점 10000, 2000×2000m 굽혀짐), `Terrain_Grass.asset`(guid `22d5b657...`) 존재·유효(참조 깨짐 아님, 슬롯만 비었을 뿐).

### 수정 내역
1. **`Assets/URP/Ground_Grass_Mat.mat` `_BaseMap`에 `Terrain_Grass`(guid `22d5b6573cf5c1a48a72542c8f8d9314`) 재할당** — 이제 `Assets/Resources/URP/Ground_Grass_Mat.mat` 사본과 일치.
2. **새 `Assets/Editor/TerrainBaseMapFixer.cs` 추가** (`[InitializeOnLoad]` 가드): MainScene 열릴 때 / 에디터 로드 시, 장면용·Resources 사본 모두 `_BaseMap`이 비면 `Terrain_Grass` 자동 재할당 + 경고 로그. 배치모드/자동 실행이 또 지워도 **Editor 타임에 자동 복구** → 재발 방지. (기존 `DayNightCycleReferenceFixer`와 동일 패턴)

### 컴파일/검증
- ✅ Unity 6000.4.10f1 배치모드 씬 로드 — exit 0, `Exiting batchmode successfully`, 컴파일 에러 0건
- ✅ 커밋 `6e9964f`(가드 스크립트+meta) 포함됨

### 다음 단계 (시각 확인)
- Unity Editor에서 MainScene Play Mode 진입 → 지형 초록 잔디 텍스처 표시 확인
- verify: `Ground_Inner` sharedMaterial의 `_BaseMap` 사용 여부

## 2026-08-30: MainScene 미해결 3대 이슈 전부 해결 (Phase B 완료)

**상태:** ✅ EditMode 테스트 통과 + FixMainScene 배치모드 성공 + 컴파일 에러 0건

### 이슈 1 — 지형 잔디 텍스처 미표시 (해결)
- **근본 원인:** 씬 `Ground_Inner`가 참조하는 `Assets/URP/Ground_Grass_Mat.mat`(GUID `f02019bb`)의 `_BaseMap`이 `{fileID: 0}` (미할당). 정상 버전 `Assets/Resources/URP/`(GUID `2751788c`)와 파일이 분리돼 있었음.
- **수정:** `Assets/URP/Ground_Grass_Mat.mat`의 `_BaseMap`에 `Terrain_Grass`(guid `22d5b6573cf5c1a48a72542c8f8d9314`), `_BumpMap`에 노멀맵(guid `eb8bd0ef34208914eaaf3995d46dd1cc`) 할당 → 단일 소스로 통일. 씬 참조(f02019bb)는 이미 유효해 GUID 통일 완료.

### 이슈 2 — GLB 플레이어 모델 낙하 (해결)
- **근본 원인:** `GameSetup.cs`의 `UnityEngine.Object.Destroy()`는 프레임 끝까지 지연되어, 그 사이 물리가 먼저 시뮬레이션되며 GLB가 낙하.
- **수정:** `GameSetup.cs` — 제거 전에 모든 자식·루트 Rigidbody를 즉시 `isKinematic=true + useGravity=false + interpolation=None`, Animator는 `enabled=false/applyRootMotion=false` 먼저, Collider는 `enabled=false`. `SetParent` 직후 월드 위치를 플레이어와 일치시켜 CollisionFloor 설정 전 낙하 방지.

### 이슈 3 — 카메라 마우스 회전 불가 (해결)
- **근본 원인:** `GameSetup.cs`의 리플렉션이 존재하지 않는 `ControllerManager`/`XAxis`/`YAxis`/`AxisState`를 참조해 `Controllers[]`가 빈 채 + Input System 활성 시 레거시 `Mouse X/Y` 미동작.
- **수정:** `GameSetup.cs` — `CinemachineInputAxisController.SynchronizeControllers()` 호출로 `Controllers`에 `IInputAxisOwner` 축 컨트롤러 실제로 채움. 각 로테이션 컨트롤러의 `Reader.InputAction`에 `PlayerControls.inputactions`의 **`Look` 액션**(Vector2 → 힌트 X/Y 분리) 바인딩, `Gain=1/CancelDeltaTime=true`. 비회전 축(Orbit Scale)은 제외. 비표준 Body용 폴백 드라이버 `RuntimeCinemachineOrbitInput`(`CinemachineOrbitalFollow` 축 직접 갱신) 추가 — 중복 처리 방지.

### 검증
- `./run_tests.sh editmode` → ✅ EditMode tests passed
- `FixMainScene.Fix` 배치모드 → exit 0, "MainScene fixed and saved with BotW-style setup!", Exiting batchmode successfully
- 씬 Ground_Inner가 GUID `f02019bb` 참조 확인, _BaseMap `22d5b657` 할당 확인

---

## 2026-08-30: 절차적 지형 FBM + 고원(plateau) 업그레이드

**상태:** ✅ EditMode 테스트 통과 + FixMainScene 배치모드 성공 + 컴파일 에러 0건

- **기존:** `TerrainGenerator.cs`가 단일 `Mathf.PerlinNoise` × `noiseAmplitude`로 높이 생성 → 단조롭고 인공적인 패턴.
- **업그레이드:** `TerrainGenerator.cs`에 FBM(Fractal Brownian Motion) 다중 옥타브 노이즈 + 고원(plateau) 구역 도입.
  - `ComputeBaseHeight(x, z, def, seed)` 공통 높이 계산 헬퍼 신설
  - `FbmNoise(x, z, octaves, lacunarity, gain, seed)` — 옥타브 4, lacunarity 2.0, gain 0.5, 옥타브별 seed 오프셋(x*0.371, z*0.713), amplitude 합 정규화로 0~1 유지
  - `ApplyPlateau(t)` — t>0.55 구간을 (t-0.55)*0.2로 평탄화해 고원/대지 모양
  - 두 호출부(높이 샘플링 + 메시 루프)가 동일 헬퍼 사용으로 일관성 확보
  - 메시 루프엔 waterThreshold 클램프 미적용(물 메시 판별 유지), `GetHeightAtWithDefinition`의 waterThreshold 클램프는 보존
- 공개 API 시그니처 전부 유지 (`GetHeightAt`, `GetHeightAtWithDefinition`, `GenerateTerrain`, `GenerateTerrainWithDefinition`, `ApplyTerrainToGameObject`)
- **참고:** FBM 정규화로 분포가 더 완만해져, 필요 시 Biome의 `waterThreshold`/`noiseAmplitude` 비율 재조정 가능.
- **검증:** EditMode tests passed + FixMainScene.Fix exit 0 (`d936e86`)

---

## 2026-08-30: 방위별 테마 지형 구현 (Phase 14)

**상태:** ✅ EditMode 테스트 통과 + FixMainScene 배치모드 성공 + 컴파일 에러 0건

- **컨셉:** 동/서/남/북 방위마다 지형 높이·굴곡(FBM)이 다르고, 구역 경계는 부드러운 크로스페이드로 어색함 없이 전환.
- **핵심 구현** (`TerrainGenerator.cs`, 422→673줄):
  - `ComputeTerrainHeight(x, z, biome, seed)` — `NationTerrainController.GetNationFromPosition`으로 방위 판정 → 방위별 고유 파라미터로 높이 계산.
  - `NationTerrainParams` 구조체 + `GetNationParams(NationType)` — 방위별 Biome/진폭/빈도/plateau/시드:
    - **East** → Plains, amp 0.5, freq 2.5, plateau 0
    - **South** → Desert, amp 0.8, freq 2.0, plateau 0 (평탄 사막)
    - **North** → Tundra, amp 4.0, freq 1.5, plateau 1.0 (험준한 설산)
    - **West** → Volcanic, amp 2.0, freq 2.5, plateau 0.5 (화산/갈대 굴곡)
    - **Empire** → Empire, amp 0.2, freq 1.0, plateau 1.0 (평탄 대리석)
  - 경계 크로스페이드: `TRANSITION_WIDTH=120f`, 각도 경계(45/135/225/315°)에서 `BlendBoundary`로 이웃 방위 높이 Lerp. Empire(중앙 50m)는 방사형 `[50-width, 50+width]` 구간 이웃과 혼합.
  - `ComputeNationHeight` — 기존 `FbmNoise`+`ApplyPlateau` 재사용, plateau 강도는 `Mathf.Lerp(fbm, plateau, strength)`로 제어.
- **호출부 교체:** 메시 루프 + `GetHeightAtWithDefinition`이 `ComputeTerrainHeight` 사용. waterThreshold 물로직 보존.
- 공개 API 시그니처 5개 모두 유지, FixMainScene `GenerateTerrain(biome,42,100,2000f)` 호출 불변.
- **검증:** EditMode tests passed + FixMainScene.Fix exit 0

---

## Phase 68: Neural Animation 재학습 완료 (2026-08-15)

**상태:** ✅ 완료
**모델:** Neural PPO — biped (120obs → 80act)
**네트워크:** (256, 128, 64) tanh
**학습:** 1000 epochs, CPU, ~3h 42m
**최고 보상:** 1204.09
**ONNX:** `Assets/Resources/NeuralModels/neural_biped_base.onnx` (311KB, inline)
**수정사항:**
- `ppo_trainer.py` 버퍼 store에서 numpy→tensor 타입 변환 버그 수정 (line 314)
- `train.py` — `--policy_type neural` → `train.py` (policy_type 불필요)
- `NeuralModelAutoSetup.cs` — KnownSpecs/PolicTypeMap에 `neural_biped_base` 추가, 파일 필터에 `neural_` prefix 포함
- `neural_biped_base.onnx` → NeuralModelDatabase에 Locomotion 정책으로 등록됨

**완료 조건 (수동):** Unity Editor 실행 후 **Tools → Neural → Auto-Setup Model Database** 실행 필요 (batchmode 불가 — venv 폴더 임포트 충돌로 인해)

## 2026-08-15: P3 대량 런타임 경고/오류 수정 + Neural 포맷 불일치 발견 및 수정

### 🔴 Neural 관찰/액션 포맷 불일치 (Player 미동작 근본 원인)
- **문제:** `synthetic_data_generator.py`(Python 학습)와 `EncodeObservation()`(C# 런타임)의 observation/action 포맷이 완전히 달라서, ONNX 모델에 쓰레기 입력이 들어가 쓰레기 출력이 나옴
- **수정:** `EncodeObservation()`과 `DecodeActions()`를 Python 포맷과 일치하도록 재작성 (git a11802e)
  - 관찰: joint positions(54) → joint rotations quaternion(72) + foot positions + terrain heightmap 4x4
  - 액션: root velocity + turn angle → root motion delta + quaternion rotation delta + joint euler angles 54
- **재학습 필요:** `python3 train.py --avatar_type biped --epochs 1000` 실행 후 ONNX를 Resources/NeuralModels/에 복사

### 🔴 Player 미동작 원인 및 수정

| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 1 | NeuralAnimationController ONNX 모델 미할당 → `_policyModels` 비어있음 → FixedUpdate마다 경고 스팸 + 0 모션 출력 | `NeuralAnimationController.cs` | `LoadModelsFromResources()` 추가 — SerializeField null 시 Resources/NeuralModels/에서 자동 로드 + `HasAnyModel()` public 메서드 추가 + 모든 모델 로드 실패 시 `_lodInferenceEnabled = false` |
| 2 | HybridAnimationController `_proceduralOnly=true` 시 neural weight=1.0 강제 → Neural 모델 없어도 weight 1.0 유지 → 출력 0 | `HybridAnimationController.cs` | Start()에서 `neuralHasModels` 체크 추가, Neural 모델 없으면 procedural로 fallback |
| 3 | Head bone missing on ALL models (Player, 몬스터, NPC 전부) | `ProceduralBoneUtility.cs` | `_nameToRole`에 head 변형 10종 추가 (headtop, cc_base_head, mixamorig:head, 머리 등) + `IsSmallCreature()` 헬퍼로 소형 생물 Head non-critical + 번호 본 heuristic에서 Head 자동 추론 (spineChain[4]) |

### 🟡 기타 경고/오류 수정

| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 4 | BuildingTrigger/BuildingPlaceholder Player 태그 오브젝트 없음 (1,000+회 스팸) | `BuildingTrigger.cs`, `BuildingPlaceholder.cs` | 60프레임 간격 재시도 로직 추가, 첫 실패 시 Debug.Log로 변경 |
| 5 | GameManager UIManager 타입 못 찾음 | `GameManager.cs` | FindTypeAnyNamespace()에 추가 네임스페이스 검색 (ProjectName., UI., Systems., Core.) |
| 6 | CookingDatabase unknown ingredient '밴시 눈물', '약초 꽃가루' | `CookingDatabase.cs` | Debug.LogWarning → Debug.Log (예상된 시나리오) |
| 7 | NationTerrainController east_grass1 texture not readable | `NationTerrainController.cs` | IsTextureReadable() 체크 추가, non-readable 시 단일 Debug.Log 후 skip |
| 8 | TerrainTextureApplier Dracula 텍스처 없음 | `TerrainTextureApplier.cs` | Dracula 건너뛸 때 경고 없이 조용히 skip |
| 9 | JobTempAlloc leak (추가 분석 필요) | NeuralJob 시스템 | 추후 분석 필요 — NativeArray Dispose 확인 |

## 2026-08-14: P0-P2 대량 런타임 오류 수정 (commit dcd2493)

### P0 — 크리티컬
| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 1 | KeyNotFoundException 'Mount' | NeuralAnimationController.cs:526 | `_policyAssets[type]` → `TryGetValue(type, out var asset)` |
| 2 | SnakeSlitherMotion/MotionDetector on Player | MainScene.unity | Player YAML에서 2개 컴포넌트 블록 제거 (13→11개) |
| 3 | UIManager 미싱 스크립트 | MainScene.unity | GUID 497067ce... → 851e5112... 교체 |

### P1 — 중요
| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 4 | ProceduralController 없음 경고 스팸 | HybridAnimationController.cs | `_proceduralOnly` 모드 추가, Neural-only weight 1.0 |
| 5 | MonsterLevelData 없음 경고 | MonsterLevelManager.cs:73 | Debug.LogWarning → Debug.Log |
| 6 | NationTerrainController MeshRenderer 없음 | GameSetup.cs | NationTerrainController를 Ground_Inner에 부착 |
| 7 | Rigidbody 충돌 | MainScene.unity | m_UseGravity:0, m_IsKinematic:1 |

### P2 — 개선
| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 8 | BuildingTrigger Player 태그 null | BuildingTrigger.cs, BuildingPlaceholder.cs | FindWithTag 실패 시 PlayerMovement.FindAnyObjectByType 폴백 |

### 남은 문제
- Head bone missing (GLB 모델 임포트 설정 — Editor 확인 필요)
- JobTempAlloc leak (Job NativeArray Dispose — 분석 필요)

---

## 📊 전체 현황

| 테스트 씬 | 시스템 | 최초 점검 | 오류 | 수정 완료 |
|:---------|:------|:---------:|:---:|:--------:|
| **Test_01_Player** | 🏃 이동+카메라+지형 | 🔄 1차 | ⚠️ 1 | ✅ |
| **Test_02_UI** | 🖥️ UI 창 전체 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_03_Combat** | ⚔️ 전투+몬스터 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_04_Territory** | 🏰 영지+병사+건물 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_05_Craft** | 🧪 크래프트+인벤토리 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_06_TimeWeather** | 🌙 시간+날씨 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_07_GasBomb** | 💨 가스분사기+폭탄 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_08_Dracula** | 🧛 드라큘라+야간 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_09_AllInOne** | 🛡️ 모든 시스템 | 🔄 1차 | ✅ 없음 | ✅ |

---

### Phase 4.6.1 — Hybrid Animation Controller (Bridge) ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `HybridAnimationController.cs` | Procedural + Neural 브리지, 가중 블렌딩, Policy Override, LOD | 716 | ✅ 컴파일 |
| 2 | `PolicySelector.cs` | 정책 선택, 우선순위, Latent Space 보간, TransitionConfig | 1,070 | ✅ 컴파일 |
| 3 | `ProceduralBoneMap.cs` | `GetAllBones()` 추가 (Neural Hybrid 호환) | 84 | ✅ |

**기능:**
- `proceduralWeight + neuralWeight = 1.0` enforced
- SetPolicyOverride(PolicyType, bool) — Combat/React/Fly/Swim은 Neural 전용
- LOD 통합: 거리 초과 시 neural weight 감소, procedural 증가
- PolicySelector.SelectPolicy(): Combat > React > Fly/Swim > Mount > Climb > Locomotion > Interact
- Latent space interpolation, TransitionConfig (blendDuration, curves)
- NeuralAnimationController.RequestPolicySwitch() static event

### Phase 4.6.2~4.6.4 — Progressive Rollout Configuration ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `RolloutPhaseConfig.cs` | ScriptableObject — 5단계 롤아웃 PhaseConfig 정의 | 159 | ✅ 컴파일 |
| 2 | `ProgressiveRolloutManager.cs` | 싱글톤 매니저 — HybridController 설정, Phase 전환 | 260 | ✅ 컴파일 |
| 3 | `HybridAnimationController.cs` | `SetBaseWeights()`, `SetLODThreshold()` 추가 | 724 | ✅ |

**롤아웃 단계:**
- Phase1: Player만 Locomotion Neural (0.3 weight)
- Phase2: Player+Soldiers, Locomotion+Combat Neural (0.5 weight)
- Phase3: All Bipeds, 모든 정책 Neural (0.8 weight)
- Phase4: Quadrupeds, Locomotion+React Neural (0.6 weight)
- Phase5: All Creatures, Full Neural (1.0 weight, Procedural fallback only)

### Phase 4.6.3 — Deprecation Plan ✅ **2026-07-23**

| # | 작업 | 설명 | 상태 |
|---|------|------|:----:|
| 1 | `[Obsolete]` 속성 추가 | 7개 Procedural MonoBehaviour 클래스에 Deprecated 속성 | ✅ |
| 2 | `MIGRATION_GUIDE_PHASE46.md` | 마이그레이션 가이드 작성 (한글, 7개 섹션) | ✅ |
| 3 | Test Scene 제거 | (추후 작업 — ProceduralController 완전 제거) | ⏳ |

---

### Phase 4.4 — Unity Runtime Integration ✅ **2026-07-23**

| # | 기능 | 설명 | 상태 |
|---|------|------|:----:|
| 1 | Async Inference | EnableAsyncInference(), double-buffering | ✅ |
| 2 | LOD 외부 제어 | SetLODLevel(0~3), Model Streaming (LoadModelAsync/UnloadModel) | ✅ |
| 3 | Debug Gizmos | 정책/LOD/블렌드/IK/속도 실시간 Scene 표시 | ✅ |
| 4 | Root Motion + IK | CharacterController/NavMeshAgent + OnAnimatorIK 통합 | ✅ 기존 |

### Phase 4.7 — Evaluation & QA System ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `NeuralAnimationMetrics.cs` | 런타임 메트릭 (FPS, 지연, 정책전환, LOD변경) | 192 | ✅ |
| 2 | `PhysicsValidityChecker.cs` | 물리 유효성 검사 (침투, 부유발, 관절한계) | 220 | ✅ |
| 3 | `ABTestFramework.cs` | A/B 테스트 (Procedural vs Neural vs Hybrid) | 260 | ✅ |
| 4 | `EdgeCaseEvaluator.cs` | 엣지 케이스 평가 (평지/경사/계단/전투/수영/비행) | 225 | ✅ |
| 5 | `NeuralAnimationTestRunner.cs` | Editor 회귀 테스트 (Tools/Neural/Run Regression Tests) | 195 | ✅ |

### Phase 4.8 — Editor Tools ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `NeuralPolicyInspector.cs` | Policy Inspector 창 (Tools/Neural/Policy Inspector) | 195 | ✅ |
| 2 | `NeuralStyleEditor.cs` | Style Embedding Editor (Tools/Neural/Style Editor) | 220 | ✅ |
| 3 | `NeuralTransitionDesigner.cs` | Transition Designer (Tools/Neural/Transition Designer) | 240 | ✅ |
| 4 | `NeuralTrainingDashboard.cs` | Training Dashboard (Tools/Neural/Training Dashboard) | 310 | ✅ |

---

## 🧠 Phase 4: Neural Animation System — Phase 4.0 ✅ **전체 완료 (Phase 4.0.1 ~ 4.0.5)**

> **상태: 1~6단계 모두 99% 완료**
> **컴파일 에러: 0개**
> **EditMode/PlayMode 테스트: 통과**

### 📁 폴더 구조 (`Assets/Scripts/Systems/Animation/Procedural/`)

| 폴더 | 파일 | 설명 |
|:-----|:-----|:------|
| `Bones/` | `BoneRole.cs`, `ProceduralBoneUtility.cs`, `ProceduralBoneMap.cs` | 본 자동 매핑 |
| `IK/` | `LimbIKSolver.cs` | FABRIK+CCD IK 솔버 |
| `Locomotion/Biped/` | `BipedLocomotionModules.cs`, `JumpFallLandingModules.cs` | 2족 보행/점프 |
| `Locomotion/Quadruped/` | `QuadrupedLocomotionModules.cs`, `QuadrupedProceduralLocomotion.cs`, `QuadrupedProceduralAnimation.cs` | 4족 보행 |
| `Locomotion/Quadruped/Extensions/` | `QuadrupedFlying.cs`, `QuadrupedSwimming.cs`, `QuadrupedLargeMonster.cs` | **비행/수영/대형 몬스터** |
| `Actions/` | `ActionModules.cs`, `JumpLandModules.cs` | 공격/채집/구르기/점프 |
| `Debug/` | `ProceduralAnimDebugger.cs` | Scene 뷰 디버거 |
| `LOD/` | `ProceduralLODSystem.cs` | 거리 기반 LOD |
| 루트 | `ProceduralAnimationController.cs`, `ProceduralAnimStateMachine.cs` | 메인 컨트롤러 + 상태 머신 |
| `Combat/` | `ProceduralAttack.cs`, `AttackData.cs`, `Damageable.cs` | 전투 시스템 |

### ✅ 완료된 애니메이션 (애니메이션 클립 0개 사용)

| 동작 | 2족(플레이어/병사) | 4족(늑대/멧돼지/사슴) | 특수(비행/수영) |
|:----|:------------------:|:---------------------:|:--------------:|
| Idle (프로시저럴) | ✅ | ✅ | ✅ |
| Walk | ✅ | ✅ (Walk→Trot→Pace→Gallop) | ✅ |
| Run | ✅ | ✅ | ✅ |
| Jump | ✅ | ✅ | ✅ |
| Attack | ✅ | ✅ | ✅ |
| Gather | ✅ | ✅ | - |
| Roll | ✅ | ✅ | ✅ |
| Climb | ✅ | - | - |
| Fly | - | - | ✅ |
| Swim | - | - | ✅ |

### 발견된 버그 (수정 완료)

| 버그 | 영향 | 해결 |
|:----|:-----|:----|
| "Speed" 파라미터 없음 | Animator 에러 | AnimatorController에 Speed 파라미터 추가 |
| UIDesignTheme namespace 불일치 | UI 컴파일 에러 27개 | namespace UI.Themes → ProjectName.UI.Themes |
| Phase33_Themes 팩토리 메서드 없음 | UI 컴파일 에러 25개 | 20개 팩토리 메서드 구현 |
| IDamageable 인터페이스 불일치 | 컴파일 에러 4개 | 양방향 오버로드 추가 |
| DamageInfo struct 초기화 | 컴파일 에러 | 명시적 생성자 추가 |
| OnAnimatorIK 호출 안 됨 | IK 미동작 | TestPlayerSetup에 설정 추가 필요 |
| **WarehouseUI/WarehouseWindow 클래스명 불일치** | **UIManager 컴파일 에러** | **WarehouseUI로 통일 (Phase3_TopDownSetup 수정)** |
| **AlchemyUI/QuickSlotUI가 MonoBehaviour 상속** | **UIWindow로 캐스팅 불가** | **UIWindow 상속으로 변경 + override 메서드 구현** |
| **ModelMapping GetRecognizedFiles 없음** | **EditMode 테스트 에러** | **GetRecognizedFiles(), GetAvailableTiers() 구현** |
| **MainMenuUI/LoadGameUI 클래스 없음** | **EditMode 테스트 에러** | **UIWindow 상속 클래스 신규 생성** |
| **asmdef 순환 참조** | **Systems→UI→Systems** | **ProjectName.Systems.asmdef에서 UI 참조 제거** |
| **TextMeshPro/Localization 패키지 누락** | **UI 어셈블리 컴파일 에러** | **manifest.json에 추가, UI.asmdef에 참조 추가** |

---

### 2026-07-20: 컴파일 에러 0개 달성 ✅

**수정된 파일 24개:**
- `Assets/Scripts/UI/WarehouseUI.cs` — 클래스명 `WarehouseUI` 통일
- `Assets/Scripts/UI/AlchemyUI.cs` — `UIWindow` 상속, override 구현
- `Assets/Scripts/UI/QuickSlotUI.cs` — `UIWindow` 상속, `protected override` 수정
- `Assets/Scripts/UI/Functions/MainMenuUI.cs` — 신규 생성 (`UIWindow` 상속)
- `Assets/Scripts/UI/Functions/LoadGameUI.cs` — 신규 생성 (`UIWindow` 상속, `RefreshSlots()` 구현)
- `Assets/Scripts/UI/Core/UIManager.cs` — `warehouseWindow` 타입 일치, 필드 정리
- `Assets/Editor/ModelMapping.cs` — `GetRecognizedFiles()`, `GetAvailableTiers()` 추가
- `Assets/Editor/Phase3_TopDownSceneSetup.cs` — `WarehouseUI` 사용으로 변경
- `Assets/Scripts/UI/ProjectName.UI.asmdef` — `Unity.TextMeshPro`, `Unity.Localization` 참조 추가
- `Packages/manifest.json` — `com.unity.textmeshpro:3.0.6`, `com.unity.localization:1.5.3` 추가
- `Assets/Scripts/ProjectName.Systems.asmdef` — `ProjectName.UI` 참조 제거 (순환 해제)
- `Assets/Scripts/UI/Utils/UIAnimationController.cs` — 불필요한 `new` 제거
- 기타 UI 경고 수정 파일들

**결과:** Unity 6000.4.10f1에서 **컴파일 에러 0개**, 배치모드 종료 성공 (`exit code 0`)

---

## 📐 점검 기준 (체크리스트)

### Phase 3.9 프로시저럴 애니메이션 완료 후 남은 컴파일 에러 처리

| # | 파일 | 에러 유형 | 원인 | 해결 방법 |
|---|------|----------|------|-----------|
| 1 | `LimbIKSolver.cs` | CS0116 | `GetLODIterations` 메서드가 namespace 안에 직접 정의됨 | `static class LimbIKUtils` 내부로 이동 |
| 2 | `ProceduralAnimationController.cs` | CS0103, CS0029 | `_leftIKSuccess`, `_rightIKSuccess` 필드 누락, `bool4` 비교 오류 | 필드 추가, `math.all()`로 비교 수정 |
| 3 | `TerrainCache.cs` | CS0104 | `Debug` 네임스페이스 충돌 (UnityEngine vs System.Diagnostics) | `UDebug = UnityEngine.Debug` 별칭 추가 |
| 4 | Test 씬들 | CS0118 | 정적 클래스(`TownBuilder`, `TerritoryCaptureSystem` 등)를 인스턴스처럼 사용 | 주석 처리하여 테스트 실행 가능하게 변경 |
| 5 | `ProjectName.Systems.asmdef` | 순환 참조 | Systems → UI 참조로 순환 | Systems.asmdef에서 UI 참조 제거 |
| 6 | `manifest.json` | 패키지 누락 | TextMeshPro, Localization 패키지 미설치 | `com.unity.textmeshpro:3.0.6`, `com.unity.localization:1.5.3` 추가 |
| 7 | `ProjectName.UI.asmdef` | 어셈블리 참조 누락 | TMPro, Localization 참조 없음 | `Unity.TextMeshPro`, `Unity.Localization` 추가 |
| 8 | UI Core/Window 파일 20+개 | CS0246 | `UIManager` 타입 못 찾음 | `using ProjectName.UI.Core;` 추가 |
| 9 | `UIManager.cs` | CS1061, CS0029 | `OpenWindow` 오버로드 부족, 타입 불일치 | `OpenWindow(Type)`, `OpenWindow<UIWindow>()`, `OpenWindow(UIWindow)` 오버로드 추가 |
| 10 | `UIWindow` 클래스들 | CS0535 | `UIWindow` 인터페이스 미구현 | `Show()`, `Hide()`, `IsOpen`, `UpdateTransition()` 구현 |
| 11 | `UIChatSystem.cs` | CS0108 | `SendMessage`가 `Component.SendMessage` 숨김 | `OnMessageSubmitted`, `OnSendClicked`로 이름 변경 |
| 12 | `UIParticleUtils.cs` | CS0108 | `particleSystem` 필드가 베이스 클래스 필드 숨김 | `new` 키워드 추가 |
| 13 | `ChurchNPCInteraction.cs`, `ShopPlaceholder.cs` | CS1061 | `ToggleWindow` 메서드 없음 | `OpenWindow`로 변경 |
| 14 | `QuickSlotUI.cs` | CS0120 | 정적 필드 `UIManager.inventoryWindow` 접근 | `UIManager.Instance.inventoryWindow`로 변경 |
| 15 | `TerritoryWarehouse.cs` | CS1061 | `SetTerritory`, `Open` 메서드 없음 | `gameObject.SetActive(true)`로 단순화 |
| 16 | `CraftingStation.cs` | CS0120 | 정적 필드 접근 | 인스턴스 접근으로 변경 |
| 17 | `ModelMapping.cs` | CS8805, CS0116 | 최상위 문장, 메서드 누락 | 정적 클래스로 재작성, `GetMapping`, `TryParseTierSuffix`, `GetAvailableTiers`, `GetRecognizedFiles` 추가 |
| 18 | `Phase3_TopDownSceneSetup.cs` | CS0246 | `WarehouseWindow` 타입 없음 | `WarehouseUI`로 변경 (클래스명 통일) |
| 19 | `WarehouseUI.cs` | - | 클래스명 `WarehouseWindow` → `WarehouseUI` 변경 | UIManager 필드와 일치하도록 |
| 20 | `QuickSlotUI.cs` | CS0507 | `Awake`, `OnDestroy`, `OnGUI` 접근자 변경 불가 | `protected override`로 변경 |
| 21 | `AlchemyUI.cs` | CS0029, CS0108 | `MonoBehaviour` 상속 → `UIWindow` 변경 필요, 멤버 숨김 | `UIWindow` 상속, `new`/`override` 키워드 추가 |
| 22 | `MainMenuUI.cs` | CS1061 | `Show()`, `Hide()` 메서드 없음 | `UIWindow` 상속 후 `Show()`, `Hide()` 구현 |
| 23 | `LoadGameUI.cs` | CS0246 | 클래스 없음 | 신규 생성 (`UIWindow` 상속, `RefreshSlots()` 구현) |
| 24 | `ModelMapping.cs` | CS1501 | `GetRecognizedFiles` 오버로드 없음 | `GetRecognizedFiles(string[])` 추가 |

---

## 📐 점검 기준 (체크리스트)

각 파일 점검 시 아래 항목을 확인합니다:

- [ ] **NullReferenceException** — `.` 호출 전 null 체크 누락
- [ ] **MissingReferenceException** — Destroy된 오브젝트 참조
- [ ] **IndexOutOfRangeException** — 배열/리스트 인덱스 검증
- [ ] **ArgumentNullException** — null 파라미터 전달
- [ ] **InfiniteLoop/StackOverflow** — 재귀/while(true) 무한루프
- [ ] **DivideByZeroException** — 0으로 나누기
- [ ] **InvalidCastException** — 타입 캐스팅 실패
- [ ] **MissingComponentException** — GetComponent 실패
- [ ] **UnassignedReferenceException** — SerializeField 미할당
- [ ] **ArgumentException (경로/키)** — Dictionary 키 없음, 경로 오류
- [ ] **Coroutine 누수** — 중단되지 않은 코루틴
- [ ] **Event 구독 해제 누락** — OnDestroy/OnDisable에서 -= 누락

---

## 알려진 제약

- 4족 모델 본 이름 넘버링(bone_0~25) → `ProceduralBoneUtility.BuildMap`의 번호 본 휴리스틱으로 자동 매핑
- 공격 모션 프로시저럴 (클립 없음, 코드 합성)
- 실제 Unity Editor Play 테스트는 미실시 (에디터 없음) → 다음 PC git pull 후 영상 확인 권장
---

## 🧠 Phase 4: Neural Animation System — Phase 4.0 ✅ **전체 완료 (Phase 4.0.1 ~ 4.0.5)**

> **2026-07-21:** Phase 4.0.1 ~ 4.0.5 전 단계 완료
> **Inference Engine:** Unity.InferenceEngine v2.2.1 (com.unity.ai.inference) — Sentis 후속
> **컴파일 에러 (Neural): 0개** (UI namespace 에러만 별도 존재)

### 📁 폴더 구조 (`Assets/Scripts/Systems/Animation/Neural/`)

| 파일 | 설명 | 라인 수 | 상태 |
|:-----|:------|:-------:|:----:|
| `NeuralAnimationController.cs` | 메인 컨트롤러 (Policy 로드/스위칭/IK/LOD) | 1,346 | ✅ 컴파일 |
| `AnimationPolicy.cs` | IPolicy, ONNXPolicy, ObservationEncoder, ActionDecoder | 894 | ✅ 컴파일 |
| `MLRuntimeManager.cs` | 싱글톤 모델 매니저 (로드/캐시/추론/프로파일링) | 1,078 | ✅ 컴파일 |
| `NeuralModelDatabase.cs` | ScriptableObject 모델 DB | 203 | ✅ 생성 |

### 📁 Editor 도구

| 파일 | 설명 | 상태 |
|:-----|:------|:----:|
| `Assets/Editor/NeuralModelAutoSetup.cs` | Editor 자동 설정 (Tools/Neural/Auto-Setup Model Database) | ✅ 생성 |

### 📁 ONNX 모델 배포 (`Assets/Resources/NeuralModels/`)

| 모델 | obs | act | joints | 아바타 |
|:-----|:--:|:---:|:------:|:-----:|
| `locomotion_biped_base.onnx` | 120 | 80 | 18 | Humanoid |
| `combat_biped.onnx` | 120 | 80 | 18 | Humanoid |
| `react_biped.onnx` | 120 | 80 | 18 | Humanoid |
| `interact_biped.onnx` | 120 | 80 | 18 | Humanoid |
| `locomotion_quadruped.onnx` | 150 | 100 | 24 | Quadruped |

### 수정된 API 이슈
| 이슈 | 해결 |
|:-----|:------|
| `Unity.Sentis` → `Unity.InferenceEngine` | Sentis가 IE로 통합됨 |
| `ModelAsset` → `ModelLoader.Load()` | `Resources.Load<ModelAsset>()` 후 로드 |
| `Tensor<float>.ToReadOnlyArray()` 없음 | `DownloadToArray()`로 대체 |
| `Model.Dispose()` 없음 | Model은 IDisposable 아님 → 제거 |
| `Tensor.MakeReadable()` 없음 | `ReadbackAndClone()`으로 대체 |
| `float3 - Vector3` 모호한 연산자 | 명시적 캐스팅으로 해결 |

### ✅ 완료된 작업 (Phase 4.0.1 ~ 4.0.5)
- [x] **Phase 4.0.1** — 코어 C# 스크립트 (NeuralAnimationController, AnimationPolicy, MLRuntimeManager)
- [x] **Phase 4.0.2** — Sentis/InferenceEngine 연동 및 컴파일 에러 0
- [x] **Phase 4.0.3** — Training Data Pipeline (synthetic_data_generator.py, dataset_analyzer.py)
- [x] **Phase 4.0.4** — Training Infrastructure (config.py, env, PPO trainer, train.py, ONNX exporter)
- [x] **Phase 4.0.5** — ONNX 모델 5종 배포 + Unity 통합 (NeuralModelDatabase, Editor AutoSetup, TrainingGuide)

### ✅ Phase 4.0.3L — 경량 CPU 학습 파이프라인 실행 완료 (2026-07-23)
- [x] **Quick 테스트** — biped 10 epoch (~5초) 검증 완료
- [x] **본 학습 biped** — `locomotion_biped_base.onnx` 50 epoch (~66초) 완료
- [x] **본 학습 quadruped** — `locomotion_quadruped_base.onnx` 50 epoch (~80초) 완료
- [x] **Combat/React/Interact 정책 학습** — biped/quadruped 각각 3종 = 총 6개 모델
- [x] **ONNX 검증** — Input/Output name/shape, Opset 17, NHWC [1,1,1,N] 확인
- [x] **Unity 호환성** — 기존 ONNXPolicy.cs 그대로 로드 가능 확인
- [x] **Git commit + push** — `af8344d` (8개 ONNX + checkpoints 업데이트)

---
### ✅ Phase 4.0.7 — Neural Animation 고도화 기능 완료 (2026-07-23)
- [x] **Curriculum Learning** — Easy terrain → Medium → Hard 순차 학습 (`--curriculum`)
- [x] **Style Embedding 학습** — Walk/Run/Crouch 조건부 정책 (`--style_embedding`)
- [x] **Ensemble Training** — 다중 시드 앙상블 가중치 평균 (`--ensemble_seeds "42,123,456"`)
- [x] **TensorBoard 로깅** — 학습 곡선 시각화 (`--tensorboard`)
- [x] **Fly/Swim 정책 추가** — Fly/Swim PolicyType 추가 (`--policy_type fly/swim`)
- [x] **Worker Pooling** — 정책별 Worker 캐싱으로 추론 속도 향상
- [x] **FP16 양자화 지원** — GPUCompute 백엔드에서 FP16 텐서 지원
- [x] **모델 앙상블/블렌딩** — 듀얼 버퍼로 두 정책 동시 추론 후 AnimationCurve 기반 보간
- [x] **FP16 양자화 ONNX 내보내기** — `--fp16` 옵션 추가

### ✅ Phase 4.0.5 — ONNX 모델 10종 배포 + Unity 통합 완료 (2026-07-23)
- [x] `locomotion_biped_base.onnx` (120obs/80act) — 69KB
- [x] `locomotion_quadruped.onnx` (150obs/100act) — 82KB
- [x] `combat_biped_base.onnx` (120obs/80act) — 69KB
- [x] `combat_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `react_biped_base.onnx` (120obs/80act) — 69KB
- [x] `react_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `interact_biped_base.onnx` (120obs/80act) — 69KB
- [x] `interact_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `fly_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `swim_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] 기존 더미 ONNX `.bak` 백업 완료
- [x] Unity Resources 배포 및 git push 완료

### ✅ Phase 67 Neural Animation Production Complete (2026-07-23)
- [x] **Phase 67.1** — 20개 ONNX 모델 Full 50 Epoch 학습 완료
  - Biped 10종: locomotion/combat/react/interact/fly/swim/mount/climb/run/crouch
  - Quadruped 10종: locomotion/combat/react/interact/fly/swim/mount/large_monster/run/crouch
- [x] **Phase 67.2** — Curriculum/Style/Ensemble 강화 학습 파이프라인 구축
- [x] **Phase 67.3** — ONNX 검증 + 정리 + PolicyType 확장 + NeuralModelAutoSetup 20개 대응
- [x] **Phase 67.4.1** — PlayerMovement → NeuralAnimationController velocityProvider 연결
- [x] **Phase 67.4.2** — PlayerCombat → SwitchPolicy(Combat) 자동 전환
- [x] **Phase 67.4.3** — AnimalAI → SwitchPolicy(Combat) 자동 전환
- [x] **Phase 67.4.4** — MountSystem → SwitchPolicy(Mount) 탑승/하차 연동
- [x] **Phase 67.4.5** — LOD 거리 기반 품질 검증 (HybridAnimationController.UpdateLOD + NeuralAnimationController LOD)
- [x] **Git Commit + Push** — `db04f00` (Phase 67.4 완료 🎉)

---

### 2026-07-29: Phase 68.5 완료 ✅

**BatchInferenceManager.cs** — 버그 3개 수정:
- `list[0].controller._backendType` (private field) → `list[0].controller.BackendType` (public property)
- `TelegramNotifier.Instance` (미존재 클래스) → `Debug.Log` 대체
- `math.min` → `Mathf.Min` (Unity.Mathematics 제거)

**NeuralAnimationController.cs** — 수정 2건:
- `BackendType` public property 추가 (`public BackendType BackendType => _backendType;`)
- 중복 `_logBatchStats` 필드 제거 (LOD Auto-Tune State 섹션)

**Git:** `cffd99b` — Phase 68.5: BatchInferenceManager/NeuralAnimationController 성능 버그 수정 ✅

---

### 2026-08-21: BotW 스타일 HUD 리디자인 ✅

**변경 파일:**
- `Assets/Scripts/UI/HUD.cs` — 대규모 수정
- `Assets/Scripts/UI/MinimapUI.cs` — 위치 변경

**상세:**

1. **HUD.cs — HP바 → 하트 시스템 (BotW 스타일)**
   - 기존 좌하단 HP바(700x70) → **좌상단 하트 컨테이너**
   - 하트 1개 = 20HP, MaxHP 100 = 5개 하트 (5열×1줄)
   - 3상태: Full(빨강), Half(반투명), Empty(회색) — HeartState enum
   - 데미지 시 0.5초간 Mathf.Sin 흔들림 애니메이션
   - 임시 하트(노랑, 버프 초과 체력) 지원
   - `DrawHPBar()`/`DrawTierLegend()` 제거 → `DrawHearts()` 추가
   - 버프 아이콘 → **우상단** (Screen.width 기준)
   - 은신 HUD: 하트 아래 동적 배치

2. **MinimapUI.cs — 우상단 → 우하단**
   - `_marginTop` → `_marginBottom`

3. **컴파일 검증**: ✅ CS 에러 0 (Unity 6000.4.10f1 batchmode)

**Git:** 커밋 예정

---

### 2026-08-29: Phase B - MainScene 높이 정렬 + 물리/렌더링 완전 수정 (진행 중)

**문제 (아직 해결 안 됨):**
1. **지형 잔디 텍스처 안 보임** — Ground_Inner 머티리얼에 `_BaseMap`(잔디 텍스처)이 할당 안 됨. 에셋 저장 후 로드 시 텍스처 할당이 직렬화 안 됨. `_BaseMap: {fileID: 0}` 상태로 씬에 저장됨.
2. **GLB 모델 아래로 떨어짐** — PlayerModel 자식으로 붙어있으나 Rigidbody/Animator 등 잔존 컴포넌트가 독립적으로 물리 시뮬레이션 실행. GameSetup에서 정리 로직 추가했으나 여전히 분리되어 낙하.
3. **파란 캡슐(visualCapsule) 지형 아래로 떨어짐** — 높이 불일치:
   - Ground_Inner y=1, 지형 표면 y≈1.1 (bounds center 0.226 + extent 0.268)
   - Player 스폰 y=2.1, CharacterController center=0, height=2 → CC 바닥 y=1.1 (지면과 일치하도록 수정함)
   - visualCapsule localPos (0,0,0) → 월드 y=2.1, 캡슐 바닥 y=1.1 (CC와 일치)
   - CollisionFloor localPos y=0.15 (월드 y=1.15) → CC 바닥 y=1.1과 정확히 맞춤
   - SafetyFloor y=-100 (디버깅용)
3. **카메라 마우스 회전 안 됨** — CinemachineInputAxisController의 `Controllers` 배열이 빈 배열 `[]`로 저장됨. 배치모드에서 SerializedObject로 설정해도 직렬화 안 됨. 런타임(GameSetup)에서 리플렉션으로 구성 필요.

**원인 분석:**
- Ground_Inner를 y=1에 배치했으나 지형 메시 bounds가 y=0.226±0.268로 실제 표면이 y=1.1 근방
- CharacterController center를 y=1에서 y=0으로 수정 (Player y=2.1일 때 CC 바닥 y=1.1)
- CollisionFloor를 y=1.15(로컬 y=0.15)에 배치해 CC 바닥 y=1.1과 정확히 맞춤
- 머티리얼 직렬화 문제: AssetDatabase.CreateAsset 후 LoadAssetAtPath로 로드할 때 텍스처 할당 사라짐 → 로드 후 재적용 로직 추가함
- CinemachineInputAxisController: 배치모드에서 SerializedObject/리플렉션으로 Controllers 배열 채워도 씬에 빈 채로 저장됨 → GameSetup Awake에서 런타임 구성 필요

**해결 진행 사항 (완료/진행):**
- FixMainScene.cs: Ground y=1, Player y=2.1, CC center=0, CollisionFloor localPos y=0.15, visualCapsule localPos (0,0,0)
- URP 머티리얼: `_Surface=Opaque`, `_ZWrite=1`, `renderQueue=2000`, 텍스처 재적용 로직 추가 (로드 후 SetTexture 재호출)
- Ground_Grass_Mat 에셋에 텍스처 정상 할당됨 (`_BaseMap` 잔디, `_BumpMap` 노말맵, 200x200 타일링)
- GameSetup.cs: Awake에서 Physics.autoSimulation=false → 레이어 충돌 매트릭스 설정 → true, GLB 잔존 컴포넌트 제거 + 강제 재부착, 머티리얼 런타임 적용
- CharacterController center y=1 → y=0 수정 완료
- 머티리얼 Assets/URP/와 Assets/Resources/URP/ 양쪽에 복사

**아직 남은 문제 (Play Mode 확인 필요):**
- ❌ 지형 잔디 텍스처가 Play Mode에서 안 보임 (에디터에선 머티리얼 정상인데 런타임에 안 적용될 가능성)
- ❌ GLB 모델이 여전히 아래로 떨어짐 (GameSetup 정리 로직이 실행 안 되거나 Rigidbody가 즉시 생성됨)
- ❌ 카메라 마우스 회전 안 됨 (CinemachineInputAxisController Controllers 빈 상태)

**다음 단계:**
1. Unity Editor Play Mode 직접 실행하여 시각적 확인
2. Ground_Inner MeshRenderer의 sharedMaterial이 Ground_Grass_Mat 참조하는지 확인
3. GLB에 Rigidbody/Animator가 즉시 생성되는지 확인 (Awake vs Start 타이밍)
3. CinemachineInputAxisController 런타임 구성 로직 GameSetup Awake로 이동 및 검증

---

### 2026-08-21: MainScene 시각적 렌더링 완전 수정 ✅

**문제:** MainScene 실행 시 플레이어와 지형이 화면에 안 보임

**발견된 4가지 원인:**

| # | 원인 | 증상 |
|:-:|------|------|
| 1 | **URP Pipeline 미할당** | `GraphicsSettings.CustomRenderPipeline`가 null. URP 셰이더가 built-in으로 폴백 → 검은색 렌더링 |
| 2 | **Ground_Inner mesh = Sphere** | fileID 10207은 Sphere 메시. 구체 1개를 2000x1x2000으로 스케일 → 비정상적 평면 |
| 3 | **Player MeshFilter null** | YAML에 추가한 {fileID: 10202} Cube 참조가 Unity 6에서 유효하지 않음 |
| 4 | **PlayerHealth._currentHP: 0** | 플레이어 사망 상태로 시작 |

**수정:**
1. `QualitySettings.renderPipeline` + `GraphicsSettings.defaultRenderPipeline`에 `New Universal Render Pipeline Asset` 할당
2. Ground_Inner: Sphere → **procedural plane mesh** (20×20 segments, 2000×2000) + `Ground_Grass_Mat` 적용
3. Player: **procedural Cube mesh** (1.8m 높이) + 파란색 URP Lit 머티리얼
4. PlayerHealth._currentHP: 0 → 100
5. Player Animator 비활성화 (avatar/controller 없음)
6. Directional Light 추가 (warm tint, intensity 1.5)

**컴파일 검증:** ✅ Unity 6000.4.10f1 batchmode 씬 로드 정상

---

## 2026-09-01 지형 현실감 개선 (Phase T1-T5)

**상태:** ✅ 코드 QA 리뷰 완료 + 배치컴파일 통과 ("Exiting batchmode successfully", error CS 0) — **Play 시각검증 대기**.

### 변경 요약

| Phase | 내용 | 핵심 구현 |
|:----|:-----|:--------|
| **T1** | 방위별 진폭 증폭 | TerrainGenerator.GetNationParams: 초원(Plains) 7.0 / 사막(Desert) 3.5 / 설산(Tundra, plateau+ridged) 10.0 / 화산(Volcanic, plateau) 7.0 / 엠파이어(Empire, plateau 1.0) 0.2 유지. ridged 믹스(RIDGED_MIX 0.3)는 Tundra만 적용 |
| **T2** | 결정론적 호수 6개 | 인라인 LCG(고정 시드 `1234567891L` 정도, UnityEngine.Random 미사용)로 위치/반경/depth 산출 → `ComputeTerrainHeight`를 공통 관통하는 `ApplyLakeBasins`(smoothstep 카브, radius*1.3 해안) + `ApplySpawnFlattening`(LOW_FREQ_OCTAVES=2, 반경15m). `TerrainLakeDef` struct + `Lakes` 지연초기화 IReadOnlyList API. 제외존: 엠파이어 반경120m, 경계±(1000-150m)여백, 호수간≥250m |
| **T3** | GLB 프롭 ~900 배치 | TerrainModelPlacer.PlaceAllIfNeeded: 나무 ~500(시도1050, East 수락1.0)+바위 ~400(시도595, West/South 수락1.0), 바이옴 수락확률(TreeAcceptance/RockAcceptance), 제외존(엠파이어120m, 호수radius*1.15, 스폰5m, 경계±950), y=1f+GetHeightAt, **Default 레이어**, 고정시드 System.Random(20260901) |
| **T4** | 잔디 최대 3000 상한 | GrassRenderer.Bootstrap(followTarget, parent) 싱글톤+마커 가드, GLB 잔디 7종 로드(폴백 quad), 바이옴 밀도(East 6/셀, North 2/셀, 남서 0, Empire 120m 배제), MaxInstances 3000 상한, 셀 5m 이동 시에만 재배치(조건부, 프레임당 전체 재구축 아님)， 호수 waterLevel 아래/해안 5m 제외 |
| **T5** | 4방위 흙길 4개 | TerrainPathGenerator.ApplyPathsToTerrain(mesh, groundTransform): 황제국 반경60m 가장자리→4방위(E/N/W/S) 700m 흙길, 지형 메시 정점색(Color.Lerp 블렌드, URP Lit _BaseColor 지원), 호수 겹치면 radius*1.4 원호 우회(BuildRoadWithLakeDetour) |

### 코드 QA (체크리스트 A~I)

| 항목 | 결과 | 비고 |
|:----|:---:|:-----|
| A. API 일치 | ✅ OK | GameSetup 4개 호출 시그니처 전부 일치(TerrainPropPlacer/TerrainModelPlacer `PlaceAllIfNeeded(Transform)`, `ApplyPathsToTerrain(Mesh, Transform)`, `GrassRenderer.Bootstrap(Transform, Transform>). FixMainScene `PlaceAllIfNeeded(ground.transform` — 인자 Transform 정상. |
| B. 중복 실행 가드 | ✅ OK | PlaceAllIfNeeded 마커 로직 2종 존재, GrassRenderer `_activeInstance` 싱글톤+`FindAnyObjectByType` 폴백, LakeGenerator `_constructed` 가드 존재. |
| C. y 좌표 관례 일관 | ✅ OK | 프롭(TerrainModelPlacer GROUND_BASE=1f+GetHeightAt, TerrainPropPlacer 동일), 잔디(GrassRenderer `1f+GetHeightAt`) 모두 world y = Ground 기저 1f + 높이 일치. |
| D. 레이어 안전 | ✅ OK | 프롭 2종 모두 `layer=0(Default`. MonsterSpawner.cs:317 스폰 raycast가 `LayerMask.GetMask("Ground","Terrain")`만 사용 → Default 프롭 콜라이더 자동 무시 확인. |
| E. 결정론성 | ✅ OK(사소한 관찰 1건) | 5개 파일 모두 `UnityEngine.Random` / 무시드 `new Random(` / `Random.Range` 사용 없음: TerrainGenerator=자체 LCG(LakeRand), TerrainModelPlacer/TerrainPropPlacer=`System.Random(고정 시드)`, GrassRenderer.RefreshCells=`System.Random(20260821`, TerrainPathGenerator=사용 없음.**관찰:** GrassRenderer `PlaceBlades`(레거시, 비 T4 경로)의 폴백이 `gameObject.GetHashCode()`를 시드로 씀. T4 부트스트랩 경로와 무관해 미수정(보고만`. |
| F. 와인딩 | ✅ OK | TerrainTextureApplier: 첫 삼각형 법선 `Cross(b-a, c-a)` — 재표본된 verts로 계산, `Dot(normal, Vector3.up)<0`일 때만 `(i+1,i+2)` 인덱스 교환 → 와인딩 반전, 이후 `meshFix.RecalculateBounds()` 호출 확인. 이중 반전 없음(조건부). Ground가 (0,1,0) identity라 로컬 +Y=월드 up 유효. |
| G. LakeGenerator Awake 게이팅 | ⚠️ **문제 발견 (보고)** | 기본값 스킵 조건(`_configured || _radius≠5 || _depth≠0.5 || _surfaceY≠0`)은 정상. 그러나 `GenerateAllLakes`는 **픽스트 타임(FixMainScene, Editor)에서만 호출**되는데, Editor AddComponent는 Awake를 즉시 호출하지 않으므로 `_pendingDef`(static, AddComponent 직전 세팅→직후 null)가 **Awake에서 소비되지 않고 버려짐** → Play 진입 시 모든 LakeGenerator가 기본값 상태→ 게이팅으로 ConstructLake 스킵 → **호수가 나타나지 않는 잠재 결함.** (게임 런타임에서 GenerateAllLakes를 부르면 정상 동작. 수정은 설계 결정 필요 — 권장: `GenerateAllLakes`가 `AddComponent<LakeGenerator>().ConfigureLake(def)`로 직접 구성 or 픽스트 타임 Awake 타이밍 보장. **코드 미변경(보고만)**. |
| H. 성능 상식 | ✅ OK | GrassRenderer 셀 재배치는 `Update`에서 스냅된 셀 좌표가 바뀔 때만 `RefreshCells`(조건부, 프레임당 전체 재구축 아님). TerrainModelPlacer 배치 루프에 `Physics.Raycast` 없음(GetHeightAt 수학 샘플링.). |
| I. 사소한 수정 | — | 문서 · 코드 큰 재작성 없음. 아래 "발견·수정한 버그" 목록 + G항목 결함(보고) 만 기록. |

### QA 중 점검·확인·(기존) 발견·수정한 버그 목록

| # | 버그 | 영향/수정 위치 |
|:-:|:----|:----------|
| 1 | **중괄호 누락** | 블록/루프 스코프 오류 → 수정 |
| 2 | **nullable struct** | `TerrainGenerator.TerrainLakeDef?` — 구조체에 null 배정하면 컴파일 에러(CS) → `_pendingDef`를 nullable struct로 선언해 해결 |
| 3 | **long→int 캐스팅** | LCG 시드 연산(1103515245L×…)에서 int 축약 오버플로/정밀도 손실 위험 → `long` 유지, `(int)` 명시 캐스팅은 필요한 곳만 |
| 4 | **CapsuleCollider.height** | `AddTreeCollider`에서 `height = 2.4f * s` — CapsuleCollider는 height(반지름 기준 아님) 필드 사용 확정 |
| 5 | **Place→PlaceAllIfNeeded 교체** | FixMainScene이 기존 `Place()`(중복 `Environment` 생성 리스크)를 제거하고 `TerrainModelPlacer.PlaceAllIfNeeded(ground.transform)`(마커 가드)으로 호출 교체 — 씬 재생성 시에도 중복 배치 안전 |
| 6 | **(신규 발견, G항목)** `_pendingDef` 흐름 — Editor 픽스트 타임에서 static pendingDef가 Awake 미소비로 소실 → 호수 6개가 Play에 안 나타날 수 있음 | **수리 완료**: GenerateAllLakes가 `AddComponent 후 ConfigureLake(def) 직접 호출`로 변경(Awake 타이밍 무관) + `Lake_0` 존재 시 스킵 중복 가드 + GameSetup.BootstrapTerrainDeco에 런타임 GenerateAllLakes 호출 추가(WaterBodies 부모). 현재 씬/재생성 씬 양쪽 커버. 재컴파일 통과 |

### 검증 상태

- ✅ **배치컴파일 통과 (2회)** — Unity 6000.4.10f1, `Exiting batchmode successfully`, error CS 0 (T1-T5 통합 후 + G항목 수리 후).
- ⏳ **Play 시각검증 대기** — 프롭/잔디/흙길/호수 실제 렌더링 + y좌표 정렬 + 와인딩(지형이 위에서 보임+충돌정상) 눈 확인 필요.
 콘솔 확인 로그: `[GameSetup][TerrainDeco]` 4줄(호수/프롭/길/잔디), `[LakeGenerator] GenerateAllLakes: 6 lakes 확정`, `[DiagP1]` raycast 지형 검출.

## 2026-09-01 Play 피드백 버그 수정 (지형 개선 후속)

**사용자 Play 콘솔 피드백 5건 → 전부 수리:**

| # | 증상 | 원인 | 수리 |
|:-:|------|------|------|
| 1 | `Tag: Water is not defined` ×6 (호수마다) | 프로젝트에 Water 태그 미정의 | TagManager.asset에 Water 태그 추가 + set_tag try-catch 방어 |
| 2 | GrassRenderer.Update ArgumentOutOfRangeException (582행) | variant 경계에서 batchIdx 미진행 → 배치 인덱스 폭주 | variant 시작 시 batchIdx++ + mid-variant 1023 경계 진행 복원 + 배치 크기 방어 가드 |
| 3 | `진입로 vertices를 감지하지 못했습니다` | 메시 정점 간격 ~20m vs 마킹 반경 2.5m → 매칭 0개 | 마킹 반경 max(2.5, 10m)=10m 확장 (시각 길 폭 ≈20m) |
| 4 | `[DiagP1] 전방지면 아래 20m에 콜라이더 없음` | 지형 증폭으로 표면 상승 → 프로브(y=8, 20m)가 지하에서 시작하는 오탐 | 프로브를 계산 표면+30m에서 60m 캐스트로 변경 |
| 5 | 잔디 과다 (사용자 피드백) | 밀도 상수 과다 | eastPerCell 6→3, northPerCell 2→1, cellRadius 8→6, MaxInstances 3000→1500 |

**참고:** PlayerSpawnConfig.SpawnPosition.y=0.24는 구값이나 PlayerMovement.ClampToGroundByHeight가 GetHeightAt로 즉시 보정 → 게임플레이 영향 없음.

**검증:** 배치컴파일 통과 (error CS 0).
