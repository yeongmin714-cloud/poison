# Test_10 침하 근본수정 + Free Slash/Impact VFX 연동 + 플레이어 애니메이션 계획

- 날짜: 2026-09-10
- 원칙: **메인씬 공유 코드 무변경(사용자 지시)** — 침하/애니 수정은 Test_10 전용 경로에서만. VFX 연동은 공유 전투 경로에 추가하되 기존 FX를 대체하지 않고 추가(메인에서도 자동 적용되는 것이 사용자 희망).

## 진단 요약 (근거 확보 완료)

1. **침하**: Screenshots/테스트씬 영상.mp4 프레임 분석 + Editor.log 교차 검증
   - 플레이어 루트(캡슐)는 정상: SetupPlayer pos=(0, 0.65, 0) 표면 y=-0.37 정확 착지, CamProbe가 캡슐 상단(y=1.61) 히트, BlobShadow는 지면 추적 중
   - f_16(16초): 플레이어 GLB 모델이 지면 위에 정상 서 있음(Idle 자세) → f_18(18초): 모델만 소실, 블롭 그림자만 지면에 잔존 → **모델(자식)만 침하**
   - 로그: ProceduralBoneMap "Mapped 3 bones" + "Head bone not found — heuristic fallback (spine chain index 4)" + NeuralAnimationController "No policy models → heuristic fallback" = **Procedural/Neural 휴리스틱이 3본 맵으로 골반/루트본을 아래로 밀어 모델만 지형 아래로 침하시키는 것이 유력 원인**
   - 메인씬은 이 경로를 사용하지 않음(PlayerMovement 주석: "Neural/Hybrid 보류 — Player_AC(HumanoidClipDriver) 단일 경로, Phase 67 유산 자동부착 제거"). Test_10은 코드 생성 플레이어라 PlayerPlaceholder.TryLoadGLBModel이 ModelAnimatorAssigner.ForceBiped를 자동 부착 → 레거시 경로가 살아있음
   - 부수 관찰: 영상 17.9초 중 플레이어가 slime에게 11회 피격(CombatLog 4.8×11) — slime 공격 애니 부재(AnimalAI 경고) 확인
2. **애니**: 메인 플레이어 애니 파이프라인 = Player_AC(AnimatorController, Resources/Animation/Controllers/Player_AC) + HumanoidClipDriver(파라미터 Speed/Attack/AttackCombo/Hit/Death/Roll/Jump, Player 모드가 CC.velocity·PlayerCombat.LastAttackTime·PlayerMovement 롤/점프 구동). Test_10 플레이어는 Animator가 없어 애니 0 상태
3. **에셋 확정**
   - Free Slash VFX: `Assets/Free Slash VFX/Prefabs/` — Slash VFX.prefab, Slash Fire/Earth/Eletric/Water VFX.prefab, Multiple Slashes.prefab, Impact.prefab, Multiple Impact.prefab, Projectiles/
   - Free Impact: `Assets/Matthew Guz/Hits Effects FREE/Prefab/` — Basic Hit.prefab, Basic Hit 2/7, Fire Hit, Ice Hit, Lightning Hit Blue, Love Hit 등
4. **연동 지점 확정**
   - 공격 스윙: PlayerCombat.PerformAttack(~168행, attack_swing SFX 재생 지점) — 공격 시작 이펙트는 여기가 단일 관문
   - 피격: CombatFXGate.PlayHitFXInternal(정적 게이트, 초당 12 예산) → 기존 CombatVFXController/HitVFX 파티클 오케스트레이션 — 여기에 Impact 계열 추가 병행

## Phase 1 — Test_10 플레이어 침하 근본수정 + 애니메이션 부착 (한 번에)

- **파일**: 신규 `Assets/Scripts/Systems/TestPlayerAnimatorBoot.cs` (Test_10 전용, 메인씬에 존재하지 않는 코드 → 영향 0) + TestTerritoryCombatSetup.cs에서 부트 호출만 추가
- 동작(지연 부팅 — PlayerPlaceholder.Start 이후 실행 필요):
  1. PlayerModel에서 레거시 애니 제거: ModelAnimatorAssigner, ProceduralAnimationController, NeuralAnimationController, QuadrupedProceduralAnimation, ProceduralBoneMap, 비어있는 Animator 전부 Destroy (휴리스틱 본 조작 제거 = 침하 원인 차단)
  2. 모델 rb 확인: kinematic 유지(기존 수정분) — 재부착되면 다시 관성화
  3. Player_AC 경로 설치(메인과 동일 파이프라인): PlayerModel에 Animator 생성 + `Resources.Load<RuntimeAnimatorController>("Animation/Controllers/Player_AC")` 할당(InventoryWindow:1004 선례) + 재생 정상화(normalizedTime/재생 확인 로그)
  4. 플레이어 루트에 HumanoidClipDriver(mode=Player) 추가 — 내부가 Animator(CC.velocity→Speed, LastAttackTime→Attack 트리거, IsRolling/IsJumping→Roll/Jump)를 자동 구동. 부착 실패 시 1회 경고 후 계속(부트 NRE 격리 — try-catch 필수)
  5. 2초간 플레이어 모델 렌더러 월드 y 감시 로그(1회): 루트 y와 모델 바운드 중심 y 편차 > 0.5m면 침하 경고 로그 — 남은 침하 원인의 증거 수집용
- **타이밍 함정**: PlayerPlaceholder.Start에서 GLB 로드+ForceBiped가 일어나므로, Boot는 `WaitUntil(PlayerModel 존재)` 후 1프레임 대기 뒤 수행. RuntimeInitializeOnLoadMethod 부팅 대신 TestTerritoryCombatSetup.Awake에서 AddComponent(startCoroutine) — 씬 전용 보장
- 검증: Test_10 Play → ①Idle 애니 재생(자연스러운 정지 자세, T포즈 아님) ②좌우 이동 시 Walk 애니 ③좌클릭 시 Attack 애니 ④18초 경과 후에도 모델 지면 위 유지(침하 0) ⑤공유 파일 diff 0

## Phase 2 — Free Slash VFX → 공격 연동

- **연결부**: PlayerCombat.PerformAttack의 스윙 시작점(attack_swing SFX 라인 ~168행 부근) — 조준 방향(카메라 정면/공격 Ray 방향)으로 Slash 프리팹 스폰
- **방식**: 신규 경량 정적 러너 `SlashVFXRunner`(또는 CombatFXGate에 별도 진입점 — 예산 게이트와 분리: 공격 스윙은 유저 발동 1회성이라 초당 12 예산 소모 대상 아님)
  - `Resources/Prefabs/Slash/Slash VFX` 캐싱 후 `Instantiate` → ParticleSystem 자동 재생 → 수명 후 자동 파괴 래퍼(ReusableFX: ParticleSystem.main.duration+startLifetime 기반 Destroy) — 파티클 정지/파괴 누수 방지
  - 방향: 히트 지점 방향이 있으면 그쪽, 없으면 카메라 정면. 위치: 플레이어 앞 1.2m
- **프리팹 선정**: 기본 "Slash VFX.prefab" 단일(국가 색 규칙 불필요 — 무기 공격). Multiple Slashes는 콤보 2타에 후보(1차는 단일 유지)
- **메인씬 적용**: 공유 PlayerCombat 경로이므로 메인에서도 자동 발동(사용자 요구 부합) — Test_10 한정 스위치 없음
- 검증: Test_10 좌클릭 → 슬래시 잔상이 눈에 보임 + 프레임 드랍 없음 + 마젠타(셰이더 미변환) 여부 확인

## Phase 3 — Hits Effects FREE → 피격 연동

- **연결부**: `CombatFXGate.PlayHitFXInternal` — 기존 오케스트(스파크/출혈/숫자/카메라 셰이크) 뒤에 Impact 프리팹 추가 스폰(기존 HitVFX 유지, 교체 아님)
  - position 오버로드/target 오버로드 양쪽에서 발동
  - CombatHitType 매핑: Organic→"Basic Hit.prefab", Construct→"Basic Hit 2.prefab"(금속 타격감), 크리티컬 시 Fire Hit 후보(1차는 기본 2종만)
- **주의**: CombatFXGate는 static 클래스 — 프리팹 캐시는 정적 Dictionary + Resources.Load(Instantiate에 프리팹 직접 사용). 예산 로직 무변경(추가 스폰은 HitFX 내부 1회)
- 검증: Test_10에서 slime/영지/병사 타격 시 각각 임팩트 이펙트 출력 + 기존 이펙트와 겹침 과하지 않은지(파티클 예산 초과 시 수치 조정)

## Phase 4 — 검증/기록

- 배치컴파일 error CS=0 (에디터 락 시 강제종료+락파일 삭제 후 재실행 — 확립 절차)
- QA 에이전트: diff 리뷰 + 공유 파일(PlayerMovement/PlayerPlaceholder/ModelAnimatorAssigner/TerrainGenerator/CombatFXGate) 무변경 검증 — 단, CombatFXGate는 Phase 3에서 의도적으로 수정(1곳만)
- Play 판정: Test_10 영상 재녹화 → 프레임 추출 비교(침하 0 + 애니 동작 + FX 출력)
- 3곳 저장: QAPROGRESS.md + 메모리 + git 커밋/푸시

## 파일 변경 예상

| 파일 | 변경 | 범위 |
|---|---|---|
| Assets/Scripts/Systems/TestPlayerAnimatorBoot.cs | 신규 | Test_10 전용(메인 무관) |
| Assets/Scripts/Systems/TestTerritoryCombatSetup.cs | 부트 호출 +범위 | Test_10 전용 |
| Assets/Scripts/Systems/SlashVFXRunner.cs(신규) or PlayerCombat.cs | 스윙 FX 스포닝 | 공유(공격만, 로직 무변경) |
| Assets/Scripts/Systems/CombatFXGate.cs | PlayHitFXInternal에 Impact 추가 1곳 | 공유(피격 FX는 유지+추가) |
| Assets/Scripts/Systems/HitVFX.cs 또는 신규 ReusableFX.cs | 자동 파괴 래퍼 | 신규/소규모 |

## 리스크 / 비고

1. **URP 셰이더 미호환**: 두 VFX 에셋이 Built-in 셰이더면 URP에서 마젠타 — 발견 시 URP/Particles/Unlit로 머터리얼 교체 필요(별도 소요). 데모 씬의 머터리얼 구성이 URP 호환인지 실행 시 확인
2. **Player_AC가 GLB 리그와 호환되는지**: 메인씬 PlayerModel은 씬에 미리 배치된 동일 GLB(Player_Rigged)를 쓰므로 Player_AC가 본 이름을 공유 — 호환 예상. 호환 실패 시 InventoryWindow 프리뷰 경로(런타임 Player_AC 할당 선례)로 검증
3. **침하 재발 시**: Procedural 제거로도 침하가 남으면 2차 후보 = 피격 넉백(kinematic 넉백이 Test_10 HighSpec에서 모델 y를 밀 가능성) — Phase 1의 y 편차 감시 로그로 판별 후 Test_10 전용으로 차단
4. HumanoidClipDriver가 PlayerCombat.LastAttackTime을 읽음 → Test_10 공격은 PlayerCombat 경로(좌클릭)와 정합 — AttackSystem과 PlayerCombat 중 실제 좌클릭 발화는 로그상 PlayerCombat이므로 유지
