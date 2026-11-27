# 128-2 — 마을 NPC 전면 교체 + 캐릭터별 애니메이션 배선 계획

> 상태: 계획 (구현 전 사용자 승인 대기). 실측 기반 — 모든 수치는 2026-10-29 조사 시점 자체 파서/소스 실측.

## 0. 자산 실측 결과 (Assets/새로운 glb/Npc 애니메이션/)

- **파일 형식 정정**: 폴더명은 "새로운 glb"지만 실제 파일은 **FBX 119개 전부**(GLB 0건).
- **구성**: 24캐릭터 × (idle/run/walk/talk + rigged) — Boy 5 / Girl 6 / Man 5 / Old Man 3 / Old Woman 5.
- **특이점 3건**: ①`Npc_Old Woman_3` **talk.fbx 누락**(4파일뿐) ②`Npc_Old Woman_3/새 폴더`(빈 폴더 — 제거 대상) ③`Npc_Girl_1-rigged.fbx` 하이픈(타 캐릭터는 언더스코어).
- **파일명 공백**: "Npc_Old Man_1" 등 공백 포함 — 경로 키 매핑 리스크.
- **크기**: 파일당 19~20MB, 합계 2.2G(텍스처 2장 임베드 포함 추정).

### FBX 자체 파서 실측 (Npc_Boy_1 4파일 — 127차 검증 파서 재사용)
| 파일 | 본 | 스킨메시 | 스택 | 클립 실질 커브 | spread p50/max |
|---|---|---|---|---|---|
| rigged | 23 | 1(+22클러스터, 텍스처 2) | **0(무애니 — T포즈 베이스)** | — | — |
| idle | 23 | 1 | 1 | 61/63 | 3.68 / 31.55 |
| walk | 23 | 1 | 1 | 58/60 | 9.11 / 358.24 |
| talk | 23 | 1 | 1 | 60/63 | 2.62 / 359.94 |

- **해석**: 모든 FBX가 메시+스킨+텍스처를 자체 포함한 자족형 믹사모식 풀 익스포트. rigged=베이스, 나머지 4종=클립(실질 커브 전부 유효 — 정지 모델 아님).
- **리그**: 본 23개 = 플레이어/병사와 동일 휴머노이드 계열(127차 드워프 FBX와 동일 구조) → **휴머노이드 임포트 시 아바타 자동 매핑 기대**(기존 GLB의 Rigify 27본+AvatarBuilder 부트스트랩 계약이 불필요해지는 것이 본 교체의 구조적 이득).
- 잔여: 24캐릭터 전수 파서 검증(본 수/클립 실질 커브 일관성)은 Phase 0 첫 작업.

## 1. 현황 지도 — 현재 마을 NPC 파이프라인 (교체 대상)

- **스폰**: `Systems/VillageNpcSpawner.cs`(838행) — `VillagerKeys` 7종(`man1/man2/girl1/girl2/girl3/oldman1/oldman2`) → `RuntimeModelLoader` alias(L92~98) → `Resources/Models/UserProvided/NPC_*_Rigged.glb` Instantiate. 마을당 6~10명(일반)/3~5명(촌락), 24마을.
- **애니**: `EnsureVillageNpcAnimation`(L222) — Player_AC 컨트롤러 + 아바타 무효 시 `AvatarBuilder.BuildHumanAvatar`(Rigify 매핑 L515~) + AlwaysAnimate + `VillagerWanderer`의 믹사모 오버라이드(`Resources/Animations/Villagers_Mixamo` walk/idle — 주민별 인스턴스 [103차]).
- **이동**: `VillagerWanderer` — 트랜스폼 구동 배회, Speed 파라미터 >0.55 Walk / <0.35 Idle 전환, 발슬립 진단([99차 F-C]).
- **대화**: `NPCAmbientDialogue.BeginDialoguePose` — Idle CrossFade + **트리거 "Talk"/"Talk2"**(Player_AC TalkA/TalkB 상태 — 표시명 홀짝 결정론 변형) + DialogueEnded 복원.
- **키 계약**: `EnsureVillageNpcHeight`(L194) — MeasureStandingHeight → 1.8m 균등 스케일 → bounds.min.y 재접지(전 캐릭터키 계약).
- **소비처(별도 수정 불요 — alias 교체로 자동 반영)**: CoreSystemsBootstrap / NPCAmbientDialogue / RestaurantSystem / VillageEconomySystem / VillagerDepartureSystem / TestTerritoryCombatSetup / ConstructionHologram.
- **범위 외 유지**: lord/king/shop_npc/dracula 등 비주민 NPC GLB는 교체하지 않음(마을 주민만).

## 2. 설계 결정 (근거)

1. **임포트 = Humanoid**: 기존 병사 FBX .meta 실측 — `animationType: 3`(Humanoid)+`avatarSetup: 1` 프로젝트 확립 패턴(Assets/Resources/Models/UserProvided/fbx/soldier_lv1-20_rigged.fbx.meta L427~429). 믹사모 리그는 Unity 휴머노이드 표준이라 수동 매핑 불필요.
2. **클립 로드 = 파일별 LoadAll**: 각 FBX 내부 테이크명은 "Take 001"/"mixamo.com" 계열로 파일명과 불일치(92차 walk/idle 동일객체 함정의 근원). 파일 경로별 `Resources.LoadAll<AnimationClip>(".../Npc_Boy_1/Npc_Boy_1_idle")`로 파일→클립 1:1 확정(기존 VillagerWanderer L403~406 파일별 재해석 선례).
3. **교체 지점 = alias 매핑 1곳**: RuntimeModelLoader alias가 단일 진실원 — 여기만 새 FBX로 교체하면 스폰/레스토랑/경제/이탈 전 소비처 자동 반영.
4. **애니 = Player_AC + 주민별 오버라이드 계약 승계**: [103차] 주민별 인스턴스 체계 유지, 클립 소스만 교체. 토크 변형(Talk/Talk2 2종)은 새 talk 클립이 캐릭터당 1종이라 **Talk 단일화** 필요(홀짝 변형 폐기 — 트리거 Talk만).
5. **토크 파일의 중복 메시**: 클립 파일도 메시 포함(자족형) — Resources 전량 포함 시 빌드/메모리 중복(캐릭터당 ~95MB). 수용 여부는 사용자 결정 사항(§6-Q3).

## 3. Phase 구분

### Phase 0 — 자산 수용·검증 (에셋 인프라, 코드 0건)
- **0.1 전수 파서 검증**: 24 rigged + 95 클립 파일 — 본 수(기대 23)/스킨/스택/실질 커브 spread 전수 실측 → 불일치 캐릭터 목록화(rigged 무애니 아님/클립 평평함 등).
- **0.2 정리·리네임**: 공백 제거(`Npc_Old Man_1`→`Npc_OldMan_1`), Girl_1 하이픈 통일, 빈 "새 폴더" 삭제 — .meta 동반 이동(리네임은 git mv 아닌 파일+meta 쌍 이동).
- **0.3 이관**: `Assets/새로운 glb/Npc 애니메이션/` → `Assets/Resources/Models/UserProvided/Npc2026/` + .meta 생성(Unity 임포트 — 수기 meta 폐기 [113차 계약]).
- **0.4 임포트 설정**: 119 .meta의 ModelImporter 블록에 animationType(Humanoid)+avatarSetup 기입 — 기존 병사 .meta L427~429 패턴 복제. YAML 수기 vs 배치모드 에디터 스크립트(AssetImporter)는 Phase 0에서 실측 후 결정.
- **완료 기준**: 컴파일 error CS 0(임포트 검증 겸사) + 파서 전수 리포트(불일치 0 또는 목록화).

### Phase 1 — 스폰 경로 교체 (2파일)
- **1.1 `RuntimeModelLoader.cs`**: villager alias 7종 → 새 rigged FBX 키 매핑 교체(24종 풀 확장은 Q1 결정).
- **1.2 `VillageNpcSpawner.cs`**: `VillagerKeys` 갱신 + `EnsureVillageNpcAnimation` 개편 — ①아바타: Humanoid 임포트 유효성 검증 후 기존 부트스트랩 계약은 폴백으로 유지 ②오버라이드: 새 캐릭터 파일별 idle/walk 클립 소스.
- **불변 계약**: EnsureVillageNpcHeight(1.8m)/AlwaysAnimate/멱등 스폰/시드 체계 유지.

### Phase 2 — 애니 상태 배선 (2파일)
- **2.1 `VillagerWanderer.cs`**: 믹사모 오버라이드 소스를 `Animations/Villagers_Mixamo` → 캐릭터별 새 클립으로 교체(파일별 LoadAll). run은 Q2 결정(미배선 시 walk만).
- **2.2 `NPCAmbientDialogue.cs`**: Talk/Talk2 이중 트리거 → Talk 단일화(새 talk 클립 1종 — Q4는 talk 누락 캐릭터 폴백).
- **불변 계약**: 트랜스폼 구동 보행(Speed 전환 임계)/DialogueEnded 복원/발슬립 진단 유지.

### Phase 3 — 검증
- **3.1 계약 테스트 영향 조사**: VillagerVariety103Tests/DialogueGaze104Tests 등 — 키 풀·오버라이드·트리거 문자열 의존 전수 grep → 갱신 목록화(94차 소멸 예정 문자열 계약).
- **3.2 컴파일 + EditMode 전수** — baseline 1:1(유지/해소/신규 3분류).
- **3.3 Play 승인 게이트**(사용자): 마을 진입 → 주민 24캐릭터 시각/1.8m/배회 walk·idle/대화 talk/이탈·레스토랑 연동 무회귀.

## 4. 신규/수정 파일 (예상)
- 수정 4종: RuntimeModelLoader.cs / VillageNpcSpawner.cs / VillagerWanderer.cs / NPCAmbientDialogue.cs
- 신규: NPCVisualKeys 매핑(필요 시) + 테스트 갱신(기존 파일 내)
- 에셋: 119 FBX + meta 이관(코드 아님)

## 5. 셀프감사 (계획 단계)
- 키 충돌: 신규 입력 바인딩 0건(기존 F키/좌클릭 체계 유지).
- 중복 폐기: 아바타 부트스트랩(Rigify 매핑)은 폴백으로만 유지 — Humanoid 임포트 성공 시 미동작 경로(삭제는 Play 검증 후 별도 승인).
- 카운트 계약: VillagerKeys 7종 단정 테스트 존재 여부 Phase 3.1에서 전수 확인(124차 카운트 계약 교훈).
- 용어: 마을 주민(주민/마을NPC)만 교체 — 영지 관리인/아레나 관리인/병사/영주/딸은 범위 외.
- 대형 에셋: 2.2G는 기존 UserProvided 관례상 Resources 포함이나 클립 파일 메시 중복(§2-5) — Q3 결정.

## 6. 열린 질문 (구현 전 결정 필요)
- **Q1 풀 구성**: 24종 전부 사용(마을마다 다양화↑) vs 기존 7키 역할 유지(7캐릭터만 매핑)?
- **Q2 run 배선**: 마을 NPC에 run 상태 필요?(배회 속도 2단 = 느린 walk/빠른 run) — 없으면 run 파일은 미사용.
- **Q3 용량**: 클립 파일 메시 중복 포함 2.2G Resources 수용?(수용 시 빌드 크기 증가 명시) / rigid만 Resources+클립 별도 경로 대안.
- **Q4 Old Woman_3 talk 누락**: talk 미보유 캐릭터의 대화 포즈 = idle 재생 폴백(기본안) — 재요청 여부.
