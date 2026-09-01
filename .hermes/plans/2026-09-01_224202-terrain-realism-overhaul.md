# 지형 현실감 개선 계획 (Terrain Realism Overhaul)

작성: 2026-09-01 22:42 | 대상: /mnt/c/Unity/code (Unity 6000.4.10f1, URP)
요청: "평지만 있는 지형 → 굴곡 + 물 + 돌 + 나무로 현실감 있게"

---

## 1. 현재 상태 (조사 결과)

### 이미 존재하지만 **전부 미연결(wired 안 됨)**인 시스템
| 시스템 | 파일 | 상태 | 용도 |
|---|---|---|---|
| TerrainPropPlacer | Assets/Scripts/Systems/TerrainPropPlacer.cs | 씬 0개, AddComponent 0건 | GLB 나무/바위/풀 랜덤 배치 (Primitive 폴백 포함) |
| TerrainModelPlacer | Assets/Scripts/Systems/TerrainModelPlacer.cs | 미호출 | GLB 환경모델 **GPU Instancing** 대량 배치 |
| LakeGenerator | Assets/Scripts/Systems/LakeGenerator.cs | 씬 0개 (WaterBodies 빈 부모만 존재) | 노이즈 기반 호수 형태 생성 |
| GrassRenderer | Assets/Scripts/Systems/GrassRenderer.cs | 미연결 | GPU Instancing 잔디 + 바람 애니메이션, 30m 컬링 |
| TerrainPathGenerator | Assets/Scripts/Systems/TerrainPathGenerator.cs | 미연결 (static 유틸) | 영지 진입로 텍스처/색상 계산 |
| TerrainHeightApplier | Assets/Scripts/Systems/TerrainHeightApplier.cs | 씬 0개 | 런타임 지형 메시 재생성 + MeshCollider 갱신 |
| WaterMaterialUpgrader | Assets/Scripts/Systems/WaterMaterialUpgrader.cs | 미연결 | 물 머티리얼 업그레이드 |

### 가용 에셋
- GLB: `Assets/Resources/Models/UserProvided/terrain/` — trees 6종, rocks 5종, grass 7종
- PolyHeven: `Assets/Models/PolyHeven/` — gltf 23종 (바위/절벽/식물 — 재질·스케일 참고용)

### 굴곡이 단조로운 원인
- `TerrainGenerator.GetNationTerrainParams()` (TerrainGenerator.cs:147~157):
  - **East(Plains, 플레이어 시작지) amplitude 0.5 / frequency 2.5** → 2000×2000m 맵에서 0.5m = 사실상 평지
  - Desert 0.8, Volcanic 2.0, Tundra 4.0, Empire 0.2 — 전반적으로 전부 작음
- FbmNoise는 다중 옥타브 구조 (TerrainGenerator.cs:83~) — 증폭만 하면 바로 개선 여지 있음
- 물/프롭/풀/길이 **하나도 생성되고 있지 않아** 단조로움이 가중

### 핵심 아키텍처 (수정 시 유지해야 할 계약)
- **GetHeightAt / ComputeTerrainHeight가 지형 높이의 단일 소스**: 플레이어 ClampToGroundByHeight, 몬스터 y raycast, 건물 배치가 전부 이 수식을 사용 → 높이 파라미터 변경은 자동 전파됨
- 방위 크로스페이드 120m (BlendBoundary 45/135/225/315°) 유지 필수 — 굴곡 증폭 시 이음새 재확인
- Empire 중앙 50m는 평탄(건물 지대) — plateauStrength 1.0 유지
- 지형 메시는 와인딩 반전 픽스가 적용된 상태 (T1=(topLeft,bottomLeft,topRight)) — 절대 되돌리지 말 것

---

## 2. 목표

1. 지형이 방위별 테마에 맞게 눈에 띄게 굴곡져 있을 것 (동쪽 초원 언덕, 남쪽 사막 사구, 북쪽 험한 설산, 서쪽 화산 암석지대)
2. 지도에 호수가 보이고, 물이 표면에 떠 있을 것
3. 나무·바위·풀이 방위/바이옴별로 분포하며 눈에 보일 것
4. 플레이어/몬스터/건물 배치가 새 지형에서도 정상일 것
5. 성능 유지 (GPU Instancing, 2000×2000m 맵)

---

## 3. Phase 계획 (각 Phase = 구현 → 배치컴파일 → Play 시각검증 → QAPROGRESS/메모리/커밋)

### Phase T1 — 지형 굴곡 강화
- [ ] `TerrainGenerator.GetNationTerrainParams()` 증폭 (TerrainGenerator.cs:147~157):
  - East Plains: amplitude 0.5 → **6~8**, frequency 2.5 유지 (완만한 구릉)
  - South Desert: 0.8 → **3~4** + 주파수 낮춰 사구 파형 (또는 desert 전용 파형 스케일)
  - North Tundra: 4.0 → **9~12**, ridge 노이즈 추가 옵션 (험한 능선)
  - West Volcanic: 2.0 → **6~8**
  - Empire: 0.2 유지 (건물 지대, 절대 평탄 유지)
- [ ] FbmNoise 옥타브/게인 미세 조정 — 세부 디테일 옥타브 추가 (gain 0.5 → 0.45~0.5, octaves 4~5)
- [ ] BlendBoundary(120m) 이음새 재검증 — 진폭 차가 커지므로 필요 시 120m → 150~180m 확장
- [ ] 스폰지 (728, -529) 주변 국소 평탄화 옵션: 스폰 반경 20m 내 높이를 로컬 평균으로 완만히 블렌드 (플레이어 착지 안정)
- [ ] TerrainHeightApplier 씬 연결 여부 확인 — 런타임 메시 재생성+MeshCollider 갱신이 확실히 동작하도록 GameSetup/FixMainScene에서 보장
- 검증: Play → 동쪽 시작지에서 언덕 보임, 플레이어 착지/이동 정상, 콘솔 에러 0

### Phase T2 — 호수/물
- [ ] LakeGenerator를 FixMainScene에서 생성: 호수 5~8개 (Empire 중앙 반경 120m 제외, 서로 최소 250m 간격, 지도 경계 150m 내 여백)
- [ ] **지형-호수 통합(중요)**: ComputeTerrainHeight에 호수 분지(basin) 반영 — 호수 중심 반경 내 지형을 부드러운 볼 형태로 파내기 → 물 표면이 지형 아래에 안착. GetHeightAt 단일 소스이므로 플레이어/몬스터/프롭 전파 자동
- [ ] WaterBody 물 표면 머티리얼: WaterMaterialUpgrader 연결 (URP Lit 투명/파도). 물 표면 MeshCollider는 WaterBody.cs:104 로직(명시적 파괴) 유지
- [ ] 호수 해안에 바위 자동 배치 (Phase T3와 연계)
- 검증: Play → 호수 육안 확인, 물에 빠지면 얕은 곳 걷기/깊은 곳 처리 확인, 콜라이더 이상 없음

### Phase T3 — 나무/바위 프롭
- [ ] TerrainModelPlacer(Instancing) + TerrainPropPlacer(개별 오브젝트) 역할 분리:
  - **Instancing 대량 장식**: 나무 400~800그루, 바위 300~600개 (GLB trees 6종/rocks 5종 사용)
  - **개별 프롭(콜라이더/채집 대상)**: ResourceNode 연동 가능한 나무/바위를 스폰지 근처 위주로 20~40개 — 추후 채집 시스템과 연결
- [ ] 바이옴별 분포 규칙 (GetNationFromPosition 활용):
  - East 초원: 활엽수 다수 + 잔디바위 소량
  - South 사막: 바위 위주(건조 틴트), 나무 희소
  - North 설산: 침엽수 + 큰 바위
  - West 화산: 검은 바위 다수, 나무 거의 없음
  - Empire 중앙: 장식수 소량(수동 배치 느낌)
- [ ] 배치 제외 존: Empire 건물지대(중앙 120m), 호수 수면, 길(Path) 위, 스폰지 반경 5m
- [ ] **레이어 규칙(중요)**: 프롭은 Ground/Terrain 레이어 금지 → "Prop" 레이어 생성/사용. 이유: 몬스터 스폰 raycast가 `Ground|Terrain` 마스크로 y=100에서 내려옴(-> ground hit을 프롭이 훔치는 사고 방지). 플레이어 clamp 수식(GetHeightAt)은 프롭 무시
- [ ] 스케일/로테이션 랜덤 + 지면 y = GetHeightAt (프롭이 언덕에 파묻히지 않게, 기저부 보정 0.1~0.3m)
- 검증: Play → 나무/바위가 방위별로 다르게 보임, 몬스터 스폰 raycast 정상(프롭 위에 몬스터 안 뜸)

### Phase T4 — 잔디
- [ ] GrassRenderer 연결: 플레이어 주변 반경 30m(기존 컬링), 바람 애니메이션, 바이옴 색상 변화
- [ ] 밀도: 초원 높음 / 사막·화산 0 / 설산 낮음 (GLB grass 7종)
- 검증: Play → 풀 흔들림, 프레임 드롭 없음

### Phase T5 — 길/트레일
- [ ] TerrainPathGenerator 연결: Empire 중앙 → 4방위 영지 진입로 4개
- [ ] 지형 위 길 텍스처/색상 (흙길), 호수 회피 경로 (직선에서 우회)
- 검증: Play → 길이 지도에서 식별됨

### Phase T6 — 텍스처/디테일 + 종합 QA
- [ ] TerrainTextureApplier: 경사도 기반 절벽 텍스처 블렌드 (slop>30° → 바위 텍스처), 바이옴별 타일링 밀도 차등
- [ ] _BaseMap 자동 배치 반복 소실 문제 재확인 (memory: _BaseMap만 읽음 — 회갈색 나오면 1순위 점검)
- [ ] 종합 QA 체크리스트: 지형 굴곡/호수/프롭/풀/길 렌더 + 플레이어 착지 + 몬스터 스폰 + 프레임레이트(에디터 기준) + 콘솔 에러 0
- [ ] QAPROGRESS.md 매트릭스/변경로그, 메모리(지형 규칙 갱신), git commit+push

---

## 4. 실행 방식 (사용자 규칙 준수)
- 각 Phase: **code agent(delegate_task) 구현 + QA agent(delegate_task) 검증**, 커밋/푸시, ROADMAP.md 기록
- 코드는 FixMainScene/GameSetup 흐름에 연결해 씬 재생성(FixMainScene.Fix())에도 유지되게 할 것
- 배치 컴파일 라이선스 간헐 실패(return code 1)는 코드 오류가 아니므로 중괄호 균형 대체 검증 병행
- Phase 완료 시 에디터 Play로 직접 눈 확인 후 다음 Phase 진행

## 5. 리스크 / 트레이드오프
- 굴곡 증폭 → 기존 건물/영지 배치가 경사면에 매달릴 수 있음 → Empire plateau 유지 + 건물별 y는 GetHeightAt로 이미 계산되므로 대부분 자동 대응, 이상 시 건물 평탄 패치 추가
- 프롭 물리: Instancing 프롭은 콜라이더 없음(장식) → 플레이어가 통과 가능. 채집 가능한 대상만 개별 오브젝트로 (Phase T3 역할 분리 이유)
- 몬스터 지면 raycast가 프롭을 지면으로 오인하는 사고 → 레이어 분리로 원천 차단 (Phase T3)
- 성능: 맵이 2000×2000m로 넓어 오브젝트 개별 배치는 수천 개 한계 → Instancing 필수
- 호수 분지 파내기가 ComputeTerrainHeight에 추가되면 그래프 비용 증가 → 호수 목록을 정적 배열(수 생성 전 결정)로 캐싱해 O(호수수) 선형 검사만 유지

## 6. 미확정 (진행 중 결정)
- 호수 개수/위치: 5~8개 권장 — 사용자 선호 확인 후 고정
- ResourceNode(채집) 시스템과 프롭 연동 시기: Phase T3에서 최소 연결(프롭+콜라이더)만 하고 채집 로직은 별도 작업
