# 영지 스폰 문제 수리 계획 (Territory Spawning Fix)

작성: 2026-09-02 10:06 | 대상: /mnt/c/Unity/code (Unity 6000.4.10f1, URP)
증상: "영지들이 스폰되어야 하는데 영지가 스폰되지 않고 있어"

---

## 1. 조사 결과 — 원인 확정 (복합 원인)

### 코드 흐름 (정상 체인)
TerritoryDatabase.GenerateAllDefinitions() → 82개 TerritoryDefinition (worldPos 포함)
→ GameManager.InitializeSystems()가 TerritoryManager(씬에 이미 존재, Awake에서 Instance 설정) 자동생성 스킵 → **TerritoryBuilder AddComponent는 instance==null일 때만!**
→ TerritoryBuilder.Start() → BuildAllTerritories() → Territory_{nation}_{index} 부모 + 건물/병사

### 발견된 4가지 원인

**[원인 1 — 최중요] 씬에 TerritoryManager가 이미 존재 → TerritoryBuilder가 AddComponent되지 않음**
- 씬에 "TerritoryManager" 오브젝트 존재 (m_Name: TerritoryManager 확인됨)
- GameManager 코드: `if (instance == null && tmType != null)` → instance가 존재하면 AddComponent(TerritoryBuilder) **실행 안 됨**
- TerritoryBuilder는 어디에도 AddComponent 안 됨 → BuildAllTerritories()가 영원히 호출 안 됨 → 영지 0개

**[원인 2] 영지 y=0 — 지형 증폭 후 건물이 지하에 파묻힘**
- TerritoryDatabase.worldPos = (x, 0, z), CreateBuilding은 y 무보정
- 지형 증폭(amp 8~16m) 후 표면이 y=1±6m인데 건물은 y=0에 생성 → 지하 매몰
- 링 거리도 문제: Ring1(가장 가까운 외곽)=1450m? — Ring4=150m, Ring1=1450m로 **명명이 반전**됨(엔진 의도는 Ring1=가까움 추정, 확인 필요)

**[원인 3] 병사(guard) 80영지 × 평균 ~15명 = 1,200개 + 건물 82×6 = ~500개 → 초기 프리징 가능**
- RuntimeModelLoader GLB 실패 시 Primitive Capsule/Cube 폴백이라 부하는 덜하지만, 동기 Start 일괄 생성은 프레임 스파이크 유발

**[원인 4] 병사 y도 지하 배치 (center+offset, y=0)**
- CreateGuard도 y 무보정 — 원인 2와 동일

### 아키텍처 제약 (수리 시 유지)
- TerritoryManager.Instance.Awake 4중방어(싱글톤) 유지
- TerritoryManager.Start에서 FindAndRegisterAll<BuildingPlaceholder> — 빌더가 먼저 끝나야 카운트 정상 (실행 순서: Builder.Start → Manager.Start 필요 → Script Execution Order 또는 Builder가 직접 등록)
- 몬스터 스폰 영지 중심(GetTerritoryCenter)은 건물 위치 기반 → 영지 스폰되면 자연히 연동
- 지형 높이 단일 소스: **월드 지표면 y = 1 + TerrainGenerator.GetHeightAt(x,z,Plains,42)** — 건물/병사 y는 반드시 이 값 사용

---

## 2. 수리 계획 (Phase S1~S4)

### Phase S1 — TerritoryBuilder 연결 (원인 1, 2, 4 수리) [최우선]
- [ ] S1-1 TerritoryBuilder y 배치 교정:
  - BuildSingleTerritory에서 `Vector3 baseY = new Vector3(0, 1f + TerrainGenerator.GetHeightAt(center.x, center.z, BiomeType.Plains, 42), 0)` 계산
  - CreateBuilding/TrySpawnModelOrPlaceholder/CCreateGuard에 y 주입 — parentGo를 baseY에 놓고 자식 offset은 유지하면 가장 간단 (부모 y만 보정)
  - Empire 중앙(Ring4 반경 150m 내)은 평탄(amp 0.2)이라 안전하지만 동일 수식 통일
- [ ] S1-2 링 거리 검증: Ring1=1450m가 "가장 가까운" 링인지 의도 확인 — 명명 반전이면 Ring1=150m 순서로 정상화 (TerritoryDifficulty enum 순서와 GetRingDistance 매핑 확인). 사용자 의사 확인 후 결정
- [ ] S1-3 병사 수 스케일링: 1,200명 → 우선 생성 유지하되 분산 생성(코루틴, 프레임당 N개)으로 초기 프리징 방지. 또는 Ring별 병사 수 임시 축소 옵션
- [ ] S1-4 TerritoryManager 등록 순서: Builder가 건물 생성 후 TerritoryManager.RegisterBuilding 직접 호출 or Script Execution Order 정리 — "건물: N개" 로그가 0이면 재확인
- 검증: Play → 콘솔 "[TerritoryBuilder] 전체 영지 Placeholder 생성 완료! 총 82개" + 씬에 Territory_* 오브젝트 82개 + 눈으로 건물 확인

### Phase S2 — 영지 시각 품질 (건물 GLB 확인)
- [ ] S2-1 RuntimeModelLoader.TryGetModel("hut"/"craft_blend"...) 로드 성공 여부 로그 — 실패 시 Cube Placeholder인지 확인
- [ ] S2-2 GLB 실패 시 Placeholder 품질 향상 여부 판단 (사용자 확인 후)
- 검증: Play → 동쪽 가장 가까운 영지(Ring4 150m 링) 도보 거리에서 건물 육안 확인

### Phase S3 — 영지-시스템 연동 확인
- [ ] S3-1 몬스터 스폰: 영지 중심(GetTerritoryCenter)이 건물 기반으로 갱신되는지 — 플레이어 주변 스폰 로직과 충돌 없는지 (이미 플레이어 중심으로 바꿔놨으므로 영향 없을 것)
- [ ] S3-2 국기/깃발: FlagManager가 영지 건물에 깃발 게양하는지 확인
- [ ] S3-3 BuildingTrigger (E키 상호작용) 동작 확인
- 검증: Play → 상점 접근 → 상호작용 프롬프트 표시

### Phase S4 — 문서/커밋
- [ ] QAPROGRESS.md 기록, 메모리 갱신, 커밋+푸시, ROADMAP 체크

## 3. 실행 방식
- Phase S1: code agent(delegate_task) 구현 → 배치컴파일 → 커밋 → Play 확인
- 원인 1(S1-0)은 최소 수정으로 즉효: TerritoryBuilder를 씬 TerritoryManager에 에디터에서 추가하거나 GameManager 조건 변경(instance != null일 때도 TerritoryBuilder 없으면 AddComponent) — **후자 권장** (씬 재생성 대응)

## 4. 리스크
- Ring 거리 명명 반전 시 몬스터 난이도(RingDifficultyData)와 불일치 가능 → 변경 시 의미 통일 필요
- 82영지 동기 생성 프리징 → S1-3 분산 생성으로 완화
- 건물 y 보정 후 호수 분지 위 영지 존재 가능(호수 반경에 영지가 걸리면 가라앉음) → 호수 제외존 검사 추가 여부 S1에서 확인 (호수 6개 위치와 링 좌표 겹침 사전 계산)
