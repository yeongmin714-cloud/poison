# 138차 계획 — 동굴 실내 개편(승인 설계 구현) + NPC 정면 수리(증거 기반 클립 교정)

작성: 2026-10-30 (138차). 선행: 137차(동굴 테스트 씬), 136차(NPC 180° 렌더 확정). 사용자 "승인"으로 §2(동굴 P-A~C)·§3(NPC 수리) 실행. Play·커밋 별도 승인.

## §1 확정 전제 (136차 실측 재확인용 요약)
- NPC 메시 = 트랜스폼 forward 180° 반대 렌더(front컷=등/rear컷=얼굴). dot(player→NPC) −1 = 정면(주석 부호 반대).
- 대화(TalkA): hips−root +0.1°(135차 패치 반영) — 수치 정면, 렌더 등. M(메시 오프셋) = face ≈ hips+180 가설.
- 런타임 idle = MeshyUser/Idle_02.fbx(guid 39a29092f4f554844ada1e013b987a50), hips−root −165.1°. **Idle_02는 Player_AC.controller가 참조(플레이어 공유)** — 무차별 패치 금지.

## §2 동굴 실내 개편 (P-A~C — 137차 §3 승인 설계의 좌표판)

### P-A CaveInteriorBuilder 개편 (좌표 박제 — 결정론)
- 시그니처 유지: `BuildCaveInterior(territoryId, tier)`. 크기: base 폭24×깊18×벽높이3.2, tier 스케일 ×(1+0.25×(tier−1)). Random 0건 규약 — 요터는 InteriorRandomizer.CreateRandom(territoryId+"_cave")만.
- **천장 생성 제거**(탑다운 천장금지 계약 — CaveCeiling 큐브 폐기. IndoorSceneTransition 실게임 경로 동일 적용).
- 박스 벽 4면 = 백플레인(틈 메움 + 콜라이더 유지) — 현행 유지하되 머티리얼 암색 (0.22,0.20,0.18).
- **암석 GLB 벽링**: NaturalResourceSpawner.LoadRockVisuals()로 외곽 둘레 따라 배치 — 간격 1.6m, 요터 ±0.4m, 남측 입구(중앙 폭3m)와 갱도 입구(서/동/북 각 폭2.5m)는 공백. 스케일 1.6~2.2 랜덤(시드). 프리팹 부재 시 박스 폴백(기존 벽).
- **구역(Room 로컬, 원점=중심)**:
  - 입구 사면: z=+8.0, 횟불1 @(0,1.6,+7.4)
  - 중앙 광장: 화로(횟불 대형 — range 9, TorchFlicker) @(0,0) + 보조 횟불 2 @(−1.8,0)/(+1.8,0)
  - 서쪽 채굴 갱도: ResourceNode ×3 @(−9.5,0,−3.0)/(−10.5,0,0)/(−9.5,0,+3.0)(Stone×2/IronOre×1 — SetupMiningNodes 선례) + 횟불 2 @(−10.6,1.6,∓1.5)
  - 동쪽 저장고: GemChest ×3 @(9.5,0,−2.5)/(10.0,0,+0.5)/(9.0,0,+3.0) + 횟불 1 @(10.4,1.6,0)
  - 북쪽 심부 갱도: 숨은 GemChest 1 @(0,0,−8.0) + 횟불 1 @(0,1.6,−7.4)
  - 횟불 총계 = 8(≤8 상한). 전부 TorchFlicker. 그림자: 중앙 화로+입구만 Soft, 나머지 None.
- 바닥: 구역 톤 — 광장 밝은 암반(0.30,0.27,0.24)/갱도·통로 어두운 흙(0.20,0.18,0.16) 스트립 2종 추가.
- 보석상자: 랜덤 배치 폐기 → 고정 4개(저장고3+심부1), GemType 결정론(시드).
- 이동 가능 보장: 입구→광장→각 갱도 경로 폭 ≥3m(노드/상자는 벽에서 ≥1.2m 이격).

### P-B CaveInteriorTopologyTests 신설(EditMode — CastleInteriorTopology 선례, DestroyImmediate/LogAssert 규약 준수)
- BuildCaveInterior("test",1) 호출 후: ①TorchFlicker 부착 Light 수 == 8 ≤ 상한 ②CaveCeiling 부재 ③GemChest 수 == 4 ④ResourceNode 수 ≥ 3 ⑤구역 오브젝트 존재(화로/갱도 이름 규약) ⑥결정론 — 동일 territoryId 재빌드 시 좌표 동일(요터 포함 비교 허용오차 0.01).
- 계약 충돌 사전 grep: CaveInterior 관련 기존 테스트(GemChest/CaveInteriorSceneTests) 소유 목록 확인 — CaveInteriorSceneTests는 Setup 레벨이라 원칙 무수정(천장 숨김은 null 가드로 무해화).

### P-C CaveInteriorTestSetup 최소 정합
- 천장 숨김 null 가드(CaveCeiling 부재 시 로그 1회+스킵). 타 로직 무수정.

## §3 NPC 정면 수리 (증거 기반 — 확정 전 수리 금지 원칙 유지, 증거 수집→패치→재검증)

### Phase-A 감사 프로브 확장 [138차]
- NpcFacing136Probe에 RunClipAudit 추가: ①런타임 Animator 컨트롤러/오버라이드의 상태→클립 매핑(idle/walk/talk) 덤프(클립명+소스 FBX+guid) ②각 상태 hips−root 실측 ③walk/idle 렌더 컷(front=루트 forward측) 추가 캡처 ④Idle_02·Walk류 공유 클립의 컨트롤러 참조 전수(guid grep — Player_AC 확인됨, 추가 참조자 조사).
- 산출: TestOutput/npc138_clip_audit.txt + npc138_walk_*.png 등.

### Phase-B 판정 규칙(감사 수치로 자동 분기 — 지시서에 규칙 박제)
- face ≈ hips+M (M=180 가설). 목표: 각 상태에서 face == root 방향(플레이어 쪽).
- NPC 전용 클립(per-character Npc2026 FBX) → orientationOffsetY 자유 패치: 목표 Δ = 180 − (현재 hips−root).
- **공유 클립(MeshyUser — Player_AC 참조) → 패치 금지**, 대신 ①Δ가 플레이어에도 대칭 이득(±15° 내 정렬 개선)이면 예외적으로 허용(Idle_02 Δ≈−15 후보 — 플레이어 idle face R_p+15→R_p 개선) ②아니면 NPC 오버라이드 슬롯을 NPC 전용 클립으로 교체+그 클립 패치.
- walk 렌더가 후방 보행이면 반드시 교정(사용자 신고 "NPC들 등 돌림"이 배회 전반일 수 있음).

### Phase-C 적용+재검증
- FBX meta orientationOffsetY 패치(135차 3패스 방법론 — gain 부호 반전 주의, 메타 YAML 직접 편집) → 재임포트 → 프로브 재실행으로 대화/걷기/idle front컷 = 얼굴 확인 → TestOutput/npc138_facing_after.txt.
- 플레이어/병사 무영향 증거: 공유 클립 패치 시 플레이어 hips 예측값 산술 로그.

## §4 검증/QA
- 컴파일 error CS 0 + 전체 EditMode baseline 1:1(QA 위임 — TestOutput/editmode-results.xml 판독).
- 최종 Play(사용자): Test_21 동굴(암흑+횟불 8개+채굴/상자), Test_20 4채널, NPC 대화/배회 정면, 피부.

## §5 소유권
- Impl-A(동굴): CaveInteriorBuilder.cs, CaveInteriorTestSetup.cs, CaveInteriorTopologyTests.cs(신규)
- Impl-B(NPC): NpcFacing136Probe.cs 확장, FBX meta(Npc2026/MeshyUser 해당 클립), 필요 시 VillageNpcSpawner(오버라이드 슬롯 교체 분기만)
- QA: EditMode 전수 + 프로브 재실행 판독
