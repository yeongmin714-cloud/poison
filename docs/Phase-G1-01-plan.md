# Phase G1-01: 🌄 지형 높이맵 시스템 (Terrain Height)

## 목표
현재 완전 평평한 Plane(y=0)을 Perlin noise 기반 높이맵으로 교체하여 언덕/계곡/산이 있는 지형 생성.

## 기존 자산
- **TerrainGenerator.cs** (Systems) — Perlin noise 50×50 그리드 → Mesh 변환 (static class, GenerateTerrain(biome, seed, resolution, size))
- **BiomeType** enum, **BiomeData** definitions, **BiomeDefinition** struct
- **NationTerrainController.cs** — 국가별 텍스처 적용, GetNationFromPosition()
- **Phase36_TerrainSetup.cs** (Editor) — 지형 설정 에디터 메뉴
- **MainScene.unity** — 1000m×1000m Ground Plane, 245개 3D 모델
- **WaterBody.cs / LakeGenerator.cs** — 절차적 물 시스템
- **CharacterController** — PlayerMovement + AnimalAI (몬스터)

## 요구사항

### 1. 지형 메시 생성
- TerrainGenerator.GenerateTerrain() 활용
- 50×50 그리드, 1000m×1000m 크기
- 3개 높이 레벨:
  - Gentle hills: 0~5m (초원/동부/서부 대부분)
  - Medium slopes: 5~15m (남부/북부 언덕)
  - Steep mountains: 15~40m (황제국 주변, 북부 산맥)
- BiomeType 기반 높이 파라미터 차별화

### 2. 텍스처 블렌딩
- 저지대(0~2m): 잔디/진흙 텍스처 (기존 NationTerrainController 텍스처)
- 중간지대(2~8m): 바위/흙 텍스처
- 고지대(8~40m): 눈/돌 텍스처
- UV 기반 텍스처 블렌딩 또는 높이맵 기반 vertex color

### 3. 오브젝트 재배치
- 3D 모델 245개 Y축 위치를 지형 높이에 맞게 조정
- Raycast로 지형 표면 높이 측정 → 오브젝트 Y = terrainHeight
- 나무/바위/식물 각각 terrainHeight에 약간의 랜덤 Y offset (+0.1~0.5m)

### 4. 물 높이 기준
- LakeGenerator: y=-0.5m (지형 저지대에 물 생성)
- WaterBody: y=0 기준 유지

### 5. CharacterController 호환
- PlayerMovement: 지형 경사면에서 미끄러짐 방지 (Slope Limit)
- AnimalAI: 지형 높이에 맞게 이동

### 6. Editor 메뉴
- Tools/Phase G1/Apply Terrain Heightmap — 지형 생성+오브젝트 재배치+텍스처 적용
- Tools/Phase G1/Reset Flat Terrain — 원래 평지로 복구

## 파일 생성/수정
- Assets/Editor/PhaseG1_TerrainHeightSetup.cs — Editor 메뉴 스크립트
- Assets/Scripts/Systems/TerrainHeightApplier.cs — 런타임 지형 높이 적용기
- Assets/Tests/EditMode/PhaseG1_TerrainHeightTests.cs — 10개+ 테스트

## 제약사항
- Namespace: ProjectName.Systems (시스템), ProjectName.Tests.EditMode (테스트)
- URP Lit materials (MaterialHelper.CreateLitMaterial)
- GameObject.CreatePrimitive for procedural meshes (TerrainGenerator 활용)
- No external assets needed