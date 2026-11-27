# [125차 후속39] 아레나 스테이징 외부 Idyllic 지형 구현

- 날짜: 2026-10-26
- 대상: `Assets/Scripts/UI/ArenaTestSetup.cs` (SetupGround 확장 + SetupStagingDeco 신설)
- 테스트: `Assets/Scripts/Tests/EditMode/ArenaStagingTerrainTests.cs` (신설, 소스계약)
- 승인: 사용자 승인 완료 — 야외 스테이징을 메인씬과 같은 Idyllic 지형으로 교체

## 1. 배경

Test_18_Arena 야외 스테이징은 기존 무지갈색 Plane(60×60m, 색상 0.35/0.32/0.28)으로
메인씬의 Idyllic 지형과 전혀 다른 외관이었다. 사용자 승인하에 메인씬과 동일한
잔디 지면 + Idyllic 데코 프리팹 산포로 교체한다.

## 2. 근거 (실측)

- 메인씬 지면 텍스처 = `Resources/Models/UserProvided/terrain/textures_idyllic/east_grass1_albedo`
  + `Grass_Normal` (TerrainTextureApplier.cs L140-167 실측).
- `Assets/Resources/URP/Ground_Grass_Mat.mat` 존재 실측 — `_BaseMap` = east_grass1.
  → `Resources.Load<Material>("URP/Ground_Grass_Mat")` 가능.
- 데코 프리팹 8종 `Assets/Resources/IdyllicPrefabs/` 하위 ls 실측 완료:
  - Trees: `BlossomTree_01`, `BlossomTree_04`
  - Bushes: `Bush_01_01`, `Bush_02_01`
  - Rocks: `Rock_Big_01`, `Rock_Big_02`
  - Grass: `Grass_01`, `Grass_02`
- 로비 방 실측치 45.6×61.3m(원점 중심) → 데코 배치 금지구역 |x|<27 && |z|<35.
- 후속33 N1 계약: ArenaTestRunner 폴링(0.5s)이 ArenaRoom/ArenaPitRoom 존재 시
  `_ground.SetActive(false)`, 복귀 시 true.

## 3. 구현

### 3.1 SetupGround (기존 메서드 최소 수정)

1. localScale `(6,1,6)` → `(12,1,12)` — Plane 10m × 12배율 = 120×120m.
2. 색상 1줄 → 글래스 블록:
   - `Resources.Load<Material>("URP/Ground_Grass_Mat")` 성공 시
     `new Material(grassMat)` **런타임 인스턴스**를 `sharedMaterial`에 대입하고
     `mainTextureScale = (20,20)` (120m/6m=20타일 — 원본 타일 밀도 유지).
     - 에셋 무손상: T-G5-W2 선례(런타임 인스턴스 머티리얼 사용).
   - 로드 실패(null) 시 기존 갈색 `new Color(0.35f, 0.32f, 0.28f)` 폴백 유지.

### 3.2 SetupStagingDeco (신설 메서드)

- **결정론 seed**: `System.Random(18261)` — 매 실행 동일 배치(회귀 재현).
- **프리팹 로드**: 위 8경로 `Resources.Load<GameObject>`, null이면 스킵(폴백 계약, 예외 없음).
- **배치**: 링 r∈[24,54]m 원점 중심. 금지구역(|x|<27 && |z|<35) 히트 시 재표본(항목당 최대 20회,
  초과 시 스킵 카운트).
- **카운트**: 나무 8(두 종 4+4) / 덤불 10(5+5) / 바위 5(3+2) / 잔디 24(12+12).
- **스케일**: 0.85~1.15 지터. **rotY**: 0~360°. **y=0.0** 접지(평면 톱).
- **부착**: 전부 Ground GameObject 자식(`SetParent(ground.transform, true)` —
  worldPositionStays로 부모 스케일 (12,1,12) 보상). 이름 접두 `ArenaStagingDeco_`.
- **호출**: Awake에서 `SetupGround();` 직후에 호출(순서 계약 — `_ground` 의존).
- 말미 로그: `[ArenaTest] [125차 후속39] 스테이징 Idyllic 데코 배치 — 나무X/덤불X/바위X/잔디X(스킵 Y)`.

## 4. 검증 계약 (ArenaStagingTerrainTests)

| # | 테스트 | 검증 |
|---|--------|------|
| T1 | 잔디 머티리얼 | `URP/Ground_Grass_Mat` 로드 + 20타일 스케일 + 런타임 인스턴스 + 폴백 색상 라인 유지 |
| T2 | 지면 확장 | localScale (12,1,12) + 구 60m 배율 잔존 부정 |
| T3 | 산포 메서드 | SetupStagingDeco 존재 + Awake 호출 순서(SetupGround 직후) + 링/카운트/재표본 상한 |
| T4 | 결정론 | seed 18261 + 결정론 주석 |
| T5 | 프리팹 8종 | 소스 경로 존재 + 실제 .prefab 파일 존재 |
| T6 | N1 토글 계약 | Ground 자식 부착(SetParent) + 이름 접두 + 계약 주석 |
| T7 | 금지구역 | 상수 27/35 + X/Z 이중 판정 |

## 5. 위험 및 대응

- **데코 별도 루트 생성 금지**: N1 토글이 `_ground`만 SetActive하므로 데코가 별도 루트면
  실내에서 잔존(투명 콜라이더 문제 재발). → 전부 Ground 자식 부착으로 해소.
  SetActive(false)면 자식 콜라이더도 함께 비활성.
- **부모 스케일 상속**: Plane (12,1,12) 자식은 스케일이 상속됨 → `SetParent(..., true)`
  (worldPositionStays)로 부모 스케일 보상, 월드 스케일 지터 0.85~1.15 유지.
- **머티리얼 에셋 오염**: `sharedMaterial`에 원본 에셋 직접 대입 시 타일 스케일 변경이
  에셋에 반영될 수 있음 → 런타임 인스턴스(`new Material(grassMat)`)로 차단(T-G5-W2 선례).
- **리소스 부재**: 머티리얼/프리팹 로드 실패 시 예외 없이 폴백(갈색 지면/스킵) — 기존 동작 보존.
- **폴링 토글 빈도**: 데코 47개가 Ground 자식이므로 N1 토글 시 함께 활성/비활성 —
  추가 폴링/루트 관리 코드 불필요.

## 6. QAPROGRESS.md

수정 금지 규약 유지 — 본 문서로만 기록.
