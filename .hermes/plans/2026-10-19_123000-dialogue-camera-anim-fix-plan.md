# 103차 대화 카메라·주민 애니 수리 계획·스펙 (감독자 작성 — 사용자 승인 완료)

> **승인:** 2026-10-19 사용자 "승인". Play·커밋은 별도 승인 게이트(미수행).
> **정책:** docs/AGENT_DELEGATION_POLICY.md — 구현/QA 서브에이전트 분리 필수.
> **근거(실측):** 마을씬 테스트 13.mp4(64.6s) 프레임 분석 + Editor.log

## 0. 근거 요약 (왜 이 수리인가)

1. **접지 진동(로그 확정)**: `[99차 F-D] 접지(배회) — man1 y=2.019→-1.699 Δ=-3.718` — 주민 10명 전원, 배회 틱 SnapToGroundRay가 루트를 3.3~3.9m 아래로 끌어내림. 발 본은 월드 +2.0 유지 → 루트↔가시 신체 3.7m 괴리.
2. **대화 카메라 지하 조준**: `[99차 F-B] 전신프레임 — 하단기준=Foot본(2.06) 화면상단여유≈-3.57m` → b.center.y≈-0.78(root.y+0.9) = 지하. 영상: 얼굴 화면 밖·상반신만·하체 절단.
3. **플레이어 가림**: `[100차 F-2] 가림 — 3점 Ray 플레이어 히트 2/3(머리/중심 거리 1.38m)` — 카메라(player 측 2.92m)와 NPC 사이에 플레이어 존재. 96~100차 높이가드/측면오프셋 상수는 근본 해결 불가.
4. **애니 문제**: 대화 클로즈업 — 포즈 픽셀 동결 + 팔 분리(d004~008) + 흰 메시 폭발(d009~010). 모든 주민 단일 공유 정적 오버라이드(Walk 0.97s/Idle 16.63s 전원 동일). Player_AC 상태명 실측: `Idle`/`Walk`/`TalkA`/`Talk2` 존재. 믹사모 클립: walkID≠idleID, walkLoop=True idleLoop=True.

## 1. 범위 파일 (화이트리스트)

| 서브에이전트 | 수정 허용 파일 |
|---|---|
| IMPL-A (애니) | `Assets/Scripts/Systems/VillageNpcSpawner.cs`, `Assets/Scripts/Systems/VillagerWanderer.cs`, `Assets/Scripts/Systems/NPCAmbientDialogue.cs`, 신규 `Assets/Scripts/Tests/EditMode/VillagerVariety103Tests.cs` |
| IMPL-B (카메라) | `Assets/Scripts/Systems/DialogueCameraDirector.cs`, 신규 `Assets/Scripts/Tests/EditMode/DialogueCamera103Tests.cs` |
| QA | 코드 수정 금지 — 검증만 |

공통: 지정 파일 외 수정 금지. 불가피하면 즉시 보고. 베이스 컨트롤러/프리팹/에셋 무수정.

## 2. IMPL-A 스펙 (P1 접지 진단 로그 + P3 애니 개별화)

### P3-1 주민별 오버라이드 인스턴스 (VillageNpcSpawner)
- 정적 공유 `_mixamoOverride` 폐기. `_mixamoWalk/_mixamoIdle/클립 해석 1회`는 유지(정적 캐시 — Resolve 로그 계약 유지).
- `GetResolvedMixamoOverride(baseCtrl)` → **주민당 호출마다** `new AnimatorOverrideController(baseCtrl)` + BuildMixamoOverrideController + `name = $"VillagerOverride_{npcName}"` 반환. 시그니처 확장 `(baseCtrl, npcName)` 허용(호출처 2곳: 스폰 배선 / VillagerWanderer F1-3 셀프체크 — F1-3은 **동일 NPC의 인스턴스를 다시 얻는** 동작 유지 = 같은 입력이면 새 인스턴스여도 무방(Rebind 후 재적용이라 참조 동일성 불요 — 주석 근거 명시)).
- 스폰 배선: `anim.runtimeAnimatorController = override(주민별) ?? playerCtrl` 폴백 계약 유지 + 기존 로그 형식 보존.

### P3-2 Idle 클립 다양성 (결정론 패리티)
- BuildMixamoOverrideController에서 idle 슬롯은 `idleClip != null` 가드가 이미 있음 → 시드 패리티(짝수=mixamo Idle 오버라이드 / 홀수=null → Player_AC `Armature|Idle_02` 유지)를 스폰 배선에서 전달. seed = 기존 `SeedBase + GetHash($"{modelKey}_{npcName}")` 재사용(별도 해시 신설 금지).

### P3-3 페이즈 오프셋 (VillagerWanderer)
- 필드 `_walkPhaseApplied`(bool). 첫 Walk 진입 시점(`_walking = true`가 되는 지점 근방, 스트라이드 캘리브레이션 시작 전)에 `animator.Play("Walk", 0, (seed % 1000) / 1000f)` 1회 + try/catch. 이후 재진입 무적용.
- Init에서 Idle 진입 직후 `animator.Play("Idle", 0, (seed % 997) / 997f)` 1회(try/catch).
- **금지**: walkSpeed/animSpeed/스트라이드 산식·클립 선택 로직 일절 무변경([100차 F-3]/[101차 F-3] 계약 보존 — 주석에 계약 유지 명시).

### P3-4 대화 자세 (NPCAmbientDialogue.BeginDialoguePose)
- 현재: PauseForDialogue + `SetTrigger("Talk")`. 수리: 트리거 전 `animator.CrossFade("Idle", 0.15f)` 1회(현재 상태 로그 포함) → Walk 중간 프레임 동결 원천 제거. TalkA 진입은 기존 트리거 체인 그대로.
- 대화 진입 로그: `[103차 P3] 대화자세 — npc=... state=...(진입 전) talk변형=A|B`.
- Talk 변형: `useTalkB = (GetHash(displayName) & 1) == 1` → 변형 B면 `"Talk"` 대신 기존 Talk2 트리거 메서드 활용(이름 충돌/중복 구독 자기점검 — 기존 SetTalkBTrigger 소비처 grep 후 안전 조치).

### P1 접지 근원 진단 (VillageNpcSpawner.SnapToGroundRay)
- RaycastAll 히트 후 `|shift| > 0.5f`일 때만 1회 로그: `[103차 P1] 접지근원 — root=... 히트=N 최근접=best.collider.name layer=N hitY=F2 footY=F2 heightAt=F2 shift=F3`.
- heightAt: TerrainGenerator.GetHeightAt(x, z, Plains, 시드) — VillageNpcSpawner 내 기존 시드 상수/접근 경로 확인 후 사용(부재 시 스폰 시드 상수 참조 — 불가하면 heightAt=기록 생략+주석).
- 동작 변경 금지(순수 로그 — 스냅 로직·반환값 무변경).

### P3 테스트 (VillagerVariety103Tests.cs — 작성만, 실행은 QA)
1. 서로 다른 npcName 2건 → GetResolvedMixamoOverride 2회 → **서로 다른 인스턴스**(ReferenceAssert.AreNotEqual) + 두 컨트롤러의 walk 슬롯 클립 동일성(walk 클립은 전원 동일 계약).
2. 패리티 다양성 — 짝수 시드 → Idle 슬롯 오버라이드=mixamo Idle / 홀수 시드 → 원본 유지. (해석 컨트롤러 GetOverrides로 검증 — 내부 메서드는 internal 노출 허용, asmdef InternalsVisibleTo 기존 선례 준수)
3. 대화 자세 — BeginDialoguePose 후 CrossFade 호출 검증은 상태 평가 불가(EditMode) → 로직 헬퍼(변형 판정/순서)만 분리해 단위판정.
4. 기존 문자열 계약 보존(VillageNpcFKeyTests 대상 소스 스트링 — Begin 오버로드 서명·파일 경로 무변경 확인 선언).

## 3. IMPL-B 스펙 (P2 대화 카메라 3요구)

수정은 **fullBody(주민) 경로 전용**. 병사/영주(fullBody=false) 경로 0변경.

### F-1 center를 가시 발 기준 재정의
- 신규 internal static 헬퍼 `ComputeFullBodyCenter(Transform target)`: `TryMeasureFootBottomY` 성공 → `footY + FullBodyCenterUpM(0.9)` / 실패 → 기존 `target.position + up*0.9` 폴백. 반환은 Vector3(target.position.x, centerY, target.position.z 수평 유지).
- **적용 4곳**: Begin 프레이밍, ReframeCamera, LogFullBodyFrameCheck(b.center 구성), (가림 로그는 기존 발 프로브 유지). 4곳이 반드시 동일 헬퍼 단일소스.
- 산식 불변: dist=(1.8×0.5)/tan(20°)×1.18≈2.92 / FOV 40 / center.y+0.9 계약 재사용.

### F-2 플레이어 렌더러 비노출 (Begin 숨김 → End 복원)
- Begin(fullBody=true): 플레이어 루트(FindGameObjectWithTag Player)의 `GetComponentsInChildren<Renderer>(true)` 전수 — 스냅샷(enabled, shadowCastingMode) 후 `enabled=false`. **콜라이더/Physics 무변경**.
- 정적 스냅샷 컬렉션 + End에서 역순 복원(파괴된 렌더러 null 가드). 재진입(Begin 내 if(_active) End())과 충돌 없게 스냅샷은 Begin 진입 시 비어 있음 보장. restore 중 예외 없어야(try 블록) — fail-safe.
- End 원복 후 스냅샷 클리어 + 멱등. 로그: `[103차 F-2] 플레이어 렌더러 숨김 — n건 / 복원 — n건`.
- CameraFollowSuppressed 게이트([93차])·[97차 F2] 패널 수명 연동·ESC 종료 유지 계약 무변경.

### F-3 완전 정면화
- `FullBodySideOffsetM` 0.45 → **0.0**(주석: [103차 F-2] 플레이어 렌더러 숨김으로 가림 원천 제거 — 완전 정면 계약). `sideDir` 오프셋 곱이 0이면 산식 자연 단순 — 상수+곱 구조 유지(0.0 대입만).
- 높이: `camPos.y = center.y + FullBodyCamHeightUpM(0.6)` 단일식 — `Mathf.Max(..., playerY + FullBodyPlayerHeadClearM)` 가드 **삭제** + `FullBodyPlayerHeadClearM` 상수·주석 폐기(사용처 제거). ComputeFullBodyCamPos 시그니처에서 playerY 인자 제거 가능(호출부 Begin/Reframe 정리).
- 근거 주석은 삭제하지 말고 [103차 F-2] 폐기 사유를 이어 붙여 히스토리 보존.

### F-4 NPC 정면 보장
- Begin의 기존 룩앳 회전(302~310행) 유지 + **LateUpdate 틱에서 재적용**(ReframeCamera의 fullBody 분기 말단 — _paused 주민과 경합 없음, 매 틱 회전은 시선고정 계약).
- Begin 시 1회 판정 로그: `[103차 F-2] 정면각 — 각=F1° (≤5° OK|탈락)` — NPC forward vs (player→NPC) 수평각. 플레이어 부재 시 "플레이어부재" 기록.

### F-5 로그 정합
- LogOcclusionCheck 유지(진단) — 로그문에 `플레이어숨김=ON(콜라이더 유지라 히트 기대 아님)` 주석 추가.
- LogFullBodyFrameCheck는 새 center 기준으로 여유 재계산(산식 동일).

### F-6 테스트 (DialogueCamera103Tests.cs — 작성만)
주입 지점 기존 계약 활용: `MainCameraOverride`(테스트 카메라), `DialoguePanelOpenOverride`(true 반환 — 세션 유지), 종료 후 정적 리셋(End 호출 필수).
1. **Foot 앵커**: 루트 y=-1.68 + 자식 본 `foot.L` 월드 y=2.0 → Begin(fullBody:true) → 카메라 pos.y == 2.0+0.9+0.6(2.9+0.6=3.5, ε 0.01) + 카메라↔center 수평거리 == 2.92(ε 0.05) + LookAt 방향 y 성분 중심 2.9+0.15.
2. **플레이어 숨김/복원**: "Player" 태그 GO + MeshRenderer → Begin 시 enabled=false → End 시 true 복원(재Begin/매크로 재진입 1회 추가 판정).
3. **정면각**: NPC (0,0,0) forward +Z, 플레이어 (0,0,5) → Begin 후 NPC forward·(player−npc) 정규화 각 ≤5°.
4. **병사 경로 무영향**: fullBody=false Begin/End — 렌더러 스냅샷 미동작(enabled 변화 없음) 판정.
5. 수학 헬퍼 직접판정 필요 시 internal 노출 허용(서명 계약 테스트 충돌 없게).

## 4. QA 스펙 (별도 서브에이전트 — 실행 전용)

1. **컴파일**: 기존 배치컴파일 절차(6000.4.10f1, error CS=0) — 로그 경로 제출.
2. **신규 테스트**: VillagerVariety103Tests + DialogueCamera103 필터 런 — 전건 통과(스킵 사유 있으면 명시).
3. **전수 EditMode 회귀**: 기준 = 최신 baseline 11건(editmode-results.xml 대조). 신규 회귀 0 판정 + 결과 xml 경로.
4. **증거 구분 표기**: ①컴파일 ②EditMode ③Play(미실행 — 승인 게이트) — 정책 §5-2 양식.

## 5. 완료 기준

- [ ] IMPL-A/B diff + 컴파일 error CS=0 증거
- [ ] P1 로그 기획 반영(빌드상 존재) / P2 4요소 / P3 4요소 코드 실측
- [ ] QA 3단계 위표본 증거 + baseline 1:1
- [ ] 미커밋 유지(커밋/Play 별도 승인), 무관 더티 파일 무손상