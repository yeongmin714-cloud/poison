# 129 — 플레이어 액션 애니메이션(채집/크래프트)+성공 환호 배선 계획

> 상태: 계획 (구현 전 사용자 승인 대기). 실측 기반 — 2026-10-29 자체 파서/소스 실측.

## 0. 자산 실측 (Assets/플레이어 애니메이션/)

- 폴더에 FBX 72개(전부 meta 존재 = Unity 기본값 임포트 상태). 이번 계획이 사용하는 것은 사용자가 한국어로 넣어둔 **3종뿐** — 나머지 69종은 건드리지 않음(사용자 보관 자산).
- **자체 파서 실측(3종 전부 유효)**:

| 파일 | 본 | 스킨 | 스택 | 실질 커브 | 길이 |
|---|---|---|---|---|---|
| 플레이어 채집.fbx | 23 | 1 | 1 | 61/63 (max 360) | **6.93s** |
| 플레이어 크래프트.fbx | 23 | 1 | 1 | 61/63 (max 359) | **5.0s** |
| 플레이어 환호.fbx | 23 | 1 | 1 | 61/63 (max 359) | **4.7s** |

- 3종 모두 믹사모 표준 휴머노이드 리그(본 23) → Humanoid 임포트로 아바타 자동 매핑 기대(Npc2026과 동일 구조).
- 이관 대상: 3종만 `Assets/Resources/Animations/PlayerActions2026/` + Humanoid 임포트 설정(loopTime=0 원샷 기본 — Npc2026ImportSetup 패턴 재사용).

## 1. 현황 지도 — 액션 시스템과 애니 배선 지점 (실측)

| 액션 | 시작 지점 | 성공 지점 | 기존 애니 |
|---|---|---|---|
| 약초 채집 | HerbPickup.BeginHarvestChannel(L291 — GatherChannelSystem.Begin) | HerbPickup.Harvest(L302 — 완료 콜백) | **완료 시에만** variant 3종(Harvest/HarvestObject/HarvestPickUp = Pull_Radish 등 랜덤) — 채널 중엔 애니 없음 |
| 벌목/채광 | ResourceNode(L111 — E키 채널 Begin) | ResourceNode 완료(L149~ — "벌목 성공!"/"채광 성공!"+피그마 결과창) | **애니 없음**(나무 흔들림 연출만) |
| 크래프트(조합) | CraftingUI.TryCraft(L752) | CraftingUI 성공 분기(L812 — "🟢 제작 성공!") | 없음 |
| 요리 | CookingUI.TryCook(L506) | CookingUI 성공 분기(L637 — "🟢 요리 성공!") | 없음 |
| 건설 | ConstructionManager.placeStructure 성공 리턴(L95~ — 골드 소비 후 시공 등록, 60s 시공) | ConstructionManager.StructureCompleted 이벤트(L72) | 없음 |

- 플레이어 애니 체계: Player_AC(컨트롤러 에셋)+HumanoidClipDriver(mode=Player — TriggerHarvest 등 SetTrigger 경로)+별도 절차형 레이어(_playerRigAnim.SetState(AnimationState.Gather) — HerbPickup이 병행 호출). 신규 배선은 **클립 드라이버 경로 기준**(확립 선례), 절차형 호출은 기존 행위 유지.
- Harvest 상태 소비처 전수: HerbPickup+HumanoidClipDriver뿐 — 병사/NPC 미사용(플레이어 전용 상태 3종).
- Player_AC Run 상태는 MoveX 전투 전용(128-2 R 실측) — 이번 계획과 무관.

## 2. 설계 결정 (근거)

1. **컨트롤러 확장 = 에디터 배치 스크립트(AnimatorController API)** — Player_AC에 상태 3종+트리거 4종+전이 추가(수기 YAML 금지 — 61차 컨트롤러 에셋 편집 선례, batchmode -executeMethod). 125차 애니 패리티 계약: 컨트롤러 에셋 수정은 전 씬 본씬 동일 적용이므로 계약 자체는 유지됨.
2. **신규 상태/파라미터**: 상태 `Gather`(채집 클립)/`Craft`(크래프트)/`Cheer`(환호) — 파라미터 트리거 `Gather`/`Craft`/`Cheer`/`ActionEnd`. 전이: Idle→각 상태(트리거), 각 상태→Idle(ExitTime 0.92 자동 복귀 — TalkA 선례), **Gather→Cheer/Craft→Cheer(트리거 — 채널 완료가 클립 중간에 와도 즉시 환호 전환)**, Gather/Craft→Idle(ActionEnd — 취소/이동 중단 시 즉시 복귀).
3. **기존 variant 단일화**: HumanoidClipDriver.TriggerHarvest/TriggerHarvestObject/TriggerHarvestPickUp 3메서드를 모두 `SetTrigger("Gather")` 위임으로 변경 — **HerbPickup 호출부 무수정**(random variant가 동일 채집 애니로 수렴. 103차 Talk 단일화 선례). 구 Harvest 3상태는 컨트롤러에 잔존(미동작 경로 — 삭제는 Play 검증 후 별도 승인).
4. **벌목/채광**: ResourceNode에 clipDriver 획득 패턴 신설(HerbPickup L309 선례 복사) — 채널 Begin 직후 TriggerGather, 완료("성공!") 직후 TriggerCheer.
5. **크래프트/요리**: UI에서 플레이어 드라이버 획득(HerbPickup 패턴 준용 — PlayerMovement.Instance/기존 참조 경로) — TryCraft/TryCook 진입 직후 TriggerCraft, 성공 분기(L812/L637)에서 TriggerCheer. 실패 분기는 환호 없음(성공률 체계 존중 — CraftSuccessSystem/요리 성공률).
6. **건설**: placeStructure 성공(시공 등록) 직후 TriggerCraft. 완료(StructureCompleted)는 환호 목록에 없어 **미포함**(Q3 — 확장 가능).
7. **트리거 발화 가드**: 이동 중(Walk) 트리ger 잔존 오발 방지 — 드라이버 Trigger* 메서드가 현재 상태가 액션 상태가 아닐 때만 SetTrigger(구현 시 실측 — 액션 중 재트리거는 무시).
8. 임포트 테이크명 불일치(테이크="mixamo.com" 계열) → 컨트롤러 모션 배치는 **파일 경로별 LoadAll/FindAssets 첫 클립**(128-2 파일별 로드 계약 선례).

## 3. Phase 구분

### Phase 0 — 자산 이관+임포트 (에셋, 코드 0건)
- 0.1 3종 FBX 이관: Assets/플레이어 애니메이션/ → Resources/Animations/PlayerActions2026/(파일+.meta 쌍 — 이미 meta 존재하므로 이동).
- 0.2 Humanoid 임포트 설정 배치(Npc2026ImportSetup 패턴 — animationType Human+avatarSetup, loopTime 원샷 기본 유지).
- 완료 기준: 컴파일 error CS 0 + meta 실측(Humanoid 3/3).

### Phase 1 — Player_AC 컨트롤러 확장 (에디터 배치 스크립트)
- PlayerActionsControllerSetup.cs(-executeMethod): 위 2-2 항목대로 상태/파라미터/전이 10건 추가+로그.
- 완료 기준: 배치 실행 로그(상태 3/파라미터 4/전이 10)+컴파일 0+컨트롤러 YAML 실측.

### Phase 2 — 드라이버+배선 (코드)
- 2.1 HumanoidClipDriver: TriggerHarvest/Object/PickUp → Gather 위임 단일화, TriggerGather/TriggerCraft/TriggerCheer/TriggerActionEnd 신설+발화 가드(2-7).
- 2.2 ResourceNode: 채널 Begin 직후 TriggerGather + 완료 성공 직후 TriggerCheer(드라이버 획득 패턴 신설).
- 2.3 CraftingUI: TryCraft 진입 직후 TriggerCraft, 성공 분기(L812) TriggerCheer.
- 2.4 CookingUI: TryCook 진입 직후 TriggerCraft(요리=제작류 — Q1 기본안), 성공 분기(L637) TriggerCheer.
- 2.5 ConstructionManager: placeStructure 성직 등록 리턴 직후 TriggerCraft(Q2/벌목·채광 채널도 TriggerGather 포함).
- 불변 계약: 기존 채널/성공률/결과창/경험치 로직 무수정 — 트리거 1줄 배선만.

### Phase 3 — 검증
- 3.1 계약 테스트: 신규 소스계약(트리거 배선 5지점+단일화)+컨트롤러 YAML 계약(상태 3종/파라미터 4종/전이 ExitTime)+기존 테스트 영향 조사(TriggerHarvest 문자열 계약 전수).
- 3.2 컴파일+EditMode 전수 — baseline 1:1.
- 3.3 Play 승인 게이트(사용자): 약초 채집/벌목/채광 채널 중 채집 애니+성공 시 환호, 조합/요리 크래프트 애니+성공 환호(실패 무환호), 건설 배치 시 크래프트 애니.

## 4. 신규/수정 파일 (예상)
- 수정 5: HumanoidClipDriver.cs / ResourceNode.cs / CraftingUI.cs / CookingUI.cs / ConstructionManager.cs (+테스트 갱신 기존 파일 내)
- 신규: PlayerActionsControllerSetup.cs(에디터) / Npc2026ImportSetup 확장 또는 신규 임포트 스크립트 / 계약 테스트 1파일
- 에셋: 3 FBX 이관+meta(Unity 임포트) / Player_AC.controller(스크립트 경유)

## 5. 셀프감사
- 키 충돌: 신규 입력 바인딩 0건(기존 E키 채널/버튼 클릭 체계 유지).
- 중복 폐기: TriggerHarvest 3종 시그니처 유지(호출부 무수정) — 내부만 Gather 위임. Harvest 상태 3종 잔존(사용처 없음 실측).
- 카운트 계약: 컨트롤러 상태 수 단정 테스트 존재 여부 Phase 3.1 전수 확인(125차 카운트 계약 교훈).
- 용어: 채집(약초)/벌목/채광=채널형, 크래프트(조합)/요리=즉시형, 건설=시공형 — 성공 판정 지점이 각각 다름(위 표).
- 잔존 리스크: 채집 클립 6.93s > 채널 2s — 완료 시 환호 전환은 Gather→Cheer 트리거로 처리(2-2), 취소 시 ActionEnd 즉시 복귀.

## 6. 결정 기록 (사용자 답변 반영 — 2026-10-29)

- **Q1**: 요리·약물제작 = **크래프트 애니 제외**. 약물제작(연금술 계열 — CraftingUI/CraftingWindowUTK/AlchemyBenchUTK/AlchemyStationUTK)은 애니·환호 전부 제외(기본안 명시). 요리는 환호만(원 목록 유지).
- **Q2**: 벌목/채광 = **이번 라운드 보류**(후속 라운드 — ResourceNode 배선 자리만 문서화 유지).
- **Q3**: 건설 완료 환호 없음 ✓.
- **Q4**: 채집 variant 3종 → **Gather 단일화** ✓ (TriggerHarvest/Object/PickUp → SetTrigger("Gather") 위임 — HerbPickup 호출부 무수정).
- **신규 추가**: **전리품 줍기(LootBasket.TakeItem/TakeAll 성공) = 채집 애니 재생**(환호 없음).

### 최종 범위표

| 액션 | 액션 애니 | 환호 |
|---|---|---|
| 약초 채집(HerbPickup — 채널형) | 채집 @채널 시작 | ✓ @완료 |
| 전리품 줍기(LootBasket — TakeItem/TakeAll 성공) | 채집 | ✗ |
| 일반 크래프트(WeaponForgeUTK — 확정 성공) | 크래프트 @제작 클릭 | ✓ @성공(즉시 — Craft→Cheer는 ExitTime 0.92+트리거로 크래프트 애니 마무리 후 환호) |
| 건설(ConstructionManager.placeStructure 성공) | 크래프트 | ✗(Q3) |
| 요리(CookingUI/CookingBenchUTK/CookingWindowUTK) | ✗(Q1) | ✓ @성공 분기 |
| 약물제작(연금술 4종 창) | ✗(Q1) | ✗ |
| 벌목/채광(ResourceNode) | **보류(Q2 — 후속)** | 보류 |

- 전이 설계 정리: Idle→Gather/Craft/Cheer(각 트리거), **Gather→Cheer(트리거, 즉시 — 채널형 성공)**, **Craft→Cheer(트리거+ExitTime 0.92 — 즉시형 성공은 크래프트 애니 마무리 후 환호)**, Gather/Craft→Idle(ActionEnd 트리거 — 취소), 3상태→Idle(ExitTime 0.92 무조건).
