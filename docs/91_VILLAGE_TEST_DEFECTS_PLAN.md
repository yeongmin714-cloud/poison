# 91차 스펙 — 마을씬 테스트 결함 수리 5건 (영상+Editor.log 실측 기반)

> 발행: 감독자 (2026-10-16). 근거: Screenshots/마을씬 테스트.mp4(18.1s) 프레임 실측 + Editor.log + 코드 재판독.
> 위임 근거: docs/AGENT_DELEGATION_POLICY.md. Figma 신규 노드 없음 — 결함 수리 차수.

## 실측 확정 (영상/로그/코드)
1. **U키 건설창**: Editor.log에 Toggle 열림/닫힘 로그 존재(2쌍) — 창 로직 자체는 동작. 영상 15.2~16.4s 창 렌더 확인(표시 가능 증명), 이후 재개방 구간 미표시. **GameStatsUTK도 U로 토글**(자기 주석 "U는 GameStatsUTK와 공유(후속 재배치)") — GameStats가 1회라도 열린 뒤 U = 두 창 동시 개방 → GameStats가 위에 떠서 "건설창 안뜸" 체감. 재부착 경로(parent==null 가드)도 stale detach에 취약.
2. **상호작용 패널(상태보기/대화하기/선물주기/퀘스트 2×2) 우상단 고정**: 영상 3.0s 풀프레임 — 패널이 우상단(미니맵 옆), 대상 NPC(Villager_9)와 플레이어는 화면 중앙 하단. PlaceAtTarget이 실패/무효 좌표. 유력 원인: Camera.main이 유저가 보는 카메라와 다른 카메라(멀티 카메라 씬) or 투영 무효값 — z<=0 가드 부재(PlaceAtTarget), 실패 시 대체 없음 → 기본 흐름 위치(우상단) 고정.
3. **NPC 대화창 너무 아래**: 영상 — 대화창이 하단 중앙(NPC 화면 위치)에 걸침. 원인: PlaceAtAnchor가 패널 **중심**을 대상+0.9m에 놓음(패널이 얼굴에 걸리고 하단으로 처짐).
4. **카메라 확대 시 대상 소실**: 로그 — Begin→복원 간격 최소 ~0.3s(3회 중 1회). 원인 = Update의 **전수 키 wasPressedThisFrame 즉시 End**(유예 없음 — 이동 중/직후 대화 시작이면 즉시 복원 → 확대가 "깜빡하고 사라짐"). 대상 정지+플레이어 응시(Begin 시선 회전+PauseForDialogue)는 영상에서 동작 확인(확대 시 NPC 정지·정면).
5. **NPC 애니 부착(girls T-pose)**: 영상 — 남성/노인 모델은 걷기 애니 정상(Player_AC 동작 증명), **girl 계열 3명 T-pose**(확대 마지막 프레임 명확). 원인: SpawnNpc가 `npc.GetComponent<Animator>()`(루트만) → 부재 시 AddComponent(루트) — **아바타 없는 Animator** = 휴머노이드 클립 리타겟 불가 = T-pose. GLB 7종 모두 동일 Rigify 리그(spine.001 체인 — GLB 본 실측: thigh ← spine ← Root, shoulder ← spine.003): Hips=spine, Spine=spine.001, Chest=spine.002, UpperChest=spine.003, Neck=spine.004, Head=spine.005.

## 수정 (화이트리스트)

### F1. U키 단일화 — GameStatsUTK 토글 이동
- Assets/Scripts/UI/Toolkit/GameStatsUTK.cs Updater: `uKey` → `oKey` (주석 [91차] — 건설 관리 창 U키 전용화).
- ConstructionWindowUTK U키 유지. GameStatsUTK 주석/문구("U키로 토글" 로그) 갱신 → "O키".

### F2. 창 재부착 경화 — ConstructionWindowUTK.Open/Show
- `if (_instance.parent == null || _instance.parent.panel == null) root.Add(_instance)` — stale detach 대비(Ensure/Open/Show 3곳 동일 가드).
- 열림 로그에 부착 상태 포함: `[ConstructionUTK] 건설 관리 창 열림 (키: U, attached={parent!=null && parent.panel!=null})` — 다음 Play 진단.

### F3. 대상 앵커 투영 공용화 — 신규 `Assets/Scripts/UI/Toolkit/TargetScreenProjector.cs`
- `public static bool TryProject(Component target, float upOffsetM, out Vector2 screenPx, out string camName)`:
  1. Camera.main → 대상 렌더러 bounds(부재 시 transform.position) + upOffsetM 투영 — 유효 판정: `vp.z > 0` && 화면 내(±64px 마진).
  2. 무효 → Camera.allCameras 순회(최초 유효 카메라 채택).
  3. 전부 무효 → false.
  4. 카메라 결정/무효 전이 시 **1회성 진단 로그**: `[TargetProject] 대상 투영 — target={name} cam={camName} screenPx=({x},{y})` (대상 변경 시 재로그, 스로틀 0.5s).
- **InteractionPanelUTK.PlaceAtTarget** → 유틸 사용: 실패 시(=false) **화면 하단 중앙 폴백 배치**(rootW/2−panelW/2, rootH−panelH−80) — "우상단 고정" 대신 항상 대상 근처 또는 예측 가능한 위치. 기존 FigmaCanvasLayout 스케일 산식 유지.
- **NPCDialoguePanelUTK.PlaceAtAnchor** → 유틸 사용(같은 산식, z 가드 자동).
- **EInteractionPromptUTK.OnGUI** → 유틸 사용(screenPx 직접 — root 변환 불필요).

### F4. NPC 대화창 올림 — PlaceAtAnchor
- 앵커 = 대상 렌더러 bounds `max.y + 0.55m`(머리 위), 패널 **바닥 중앙**이 그 점 → 패널이 머리 위에 온전히. bounds 부재 시 transform+1.9m.
- "살짝 올려주고" 요구 충족 — 패널이 NPC 얼굴을 안 가리고 머리 위에 뜸.

### F5. 대화 카메라 안정화 — DialogueCameraDirector
- **Update 종료 조건 재설계** ([91차]): 0.8s 유예 이후 **ESC 또는 WASD/화살표 이동키** wasPressedThisFrame만 End(전수 키 스캔 폐기 — 타이핑/기타 키 무시). 5s 타이머/대상 파괴·병사 IsDead 감시 유지.
- **매 프레임 재조준**: 활성 동안 `_dialogueCam.transform.LookAt(대상 머리(bounds max.y−12%))` — 대상 살짝 이동/스웨이해도 프레임 유지.
- **카메라 클립 방지**: 위치 = facePos + dir×DialogueDistance — 플레이어까지 거리 < 0.9m가 되면 dir를 90° 회전(측면 사선) — 카메라가 몸통 안에 들어가 대상이 안 보이는 케이스 방지.
- 주석: 73차 "이동·ESC 입력으로 복원" 계약 유지(유예 추가 명시).

### F6. NPC 휴머노이드 아바타 부트스트랩 — VillageNpcSpawner.SpawnNpc 애니 블록 (girls T-pose 근본 수리)
1. `var anim = npc.GetComponentInChildren<Animator>()` — 프리팹 내 Animator(어디든) 재사용(기존 GetComponent 루트 전용 폐기).
2. `anim.avatar != null && avatar.isValid && avatar.isHuman` → 그 Animator에 Player_AC 부착(기존 폴백 SoldierShield_AC 유지).
3. 무효 → 루트 Animator(부재 시 AddComponent) + **`AvatarBuilder.BuildHumanAvatar`** — Rigify 매핑(Hips=spine / Spine=spine.001 / Chest=spine.002 / UpperChest=spine.003 / Neck=spine.004 / Head=spine.005 / Left·RightShoulder=shoulder.L·R / UpperArm=upper_arm / LowerArm=forearm / Hand=hand / UpperLeg=thigh / LowerLeg=shin / Foot=foot / Toes=toe — .L/.R 좌우) + SkeletonBone=전 Transform 현재 로컬 TRS → avatar 부착 + `anim.Rebind()`. 빌드 후 isValid/isHuman 검증, 실패 시 기존 SoldierShield 폴백+경고.
4. **NPC별 진단 로그 1회**: `[VillageNpcSpawner] 애니 부착: {npcName} animator={root|child} avatar={avatarName|신규빌드} isHuman={} controller={Player_AC}` — 다음 Play 로그로 girls 수리 증명.
- 매핑 테이블은 static readonly (string humanSlot, string boneName)[] — GLB 7종 공용(동일 리그 실측).

### F7. 테스트 ([91차] 마커)
- `NpcAnimationAvatarTests.cs` (신규): ①매핑 테이블 — 15슬롯(양팔/양다리/발가락 포함)×Rigify 본명 어서션 ②GetComponentInChildren 재사용+빌드 폴백 순서 소스 어서션 ③진단 로그 문구 ④Player_AC 경로 유지.
- `TargetProjectionTests.cs` (신규): ①TryProject 시그니처/z>0 가드/allCameras 폴백 순서 소스 어서션 ②PlaceAtTarget 폴백(하단 중앙) 소스 어서션 ③PlaceAtAnchor bounds+0.55 어서션 ④EInteractionPrompt 유틸 사용.
- `DialogueCameraGraceTests` — DialogueFeedbackTests에 추가 or 신규: 유예 0.8s/WASD·ESC만 종료/매 프레임 LookAt/클립 방지 90° 회전 소스 어서션. 기존 계약(저장·복원·Begin(talkGuard)) 유지 — 충돌 시 [91차] 갱신.
- `GameStatsKeyTests` — GameStatsUTK oKey/ConstructionWindowUTK uKey 단일소스 어서션(파일 신설 or 기존에 추가).

## 검증
- 컴파일: rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh → error CS 0.
- EditMode(QA): baseline 11 외 0. 90차 기준 1220/1209/11 대비 총계 보고.
- Play 확인(사용자 게이트): ①U키 → 건설창 표시(GameStats 겹침 없음) ②F키 패널이 대상 NPC 위에 ③NPC 대화창이 머리 위 ④대화 확대가 안정(대상 정지·정면·프레임 유지, ESC/이동으로 복원) ⑤girl 주민 걷기 애니(T-pose 해소) — 로그 "애니 부착"으로도 확인.
