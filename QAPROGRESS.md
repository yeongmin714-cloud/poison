# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-09-05

---

## 2026-09-05: 애니 7차 — Heat meta 재작성 + 틴트 타입검사 수리 + hips 가드 ✅

**문제1:** 6차에서 손으로 만든 meta의 스키마 오류(animations 블록 안 animationType, human 엔트리 1개, 중복 키)로 Unity가 Heat FBX를 **Generic 아바타로 임포트** → `InvalidOperationException: Avatar is not of type humanoid` (GetBoneTransform).
**수리:** meta 전면 재작성 — human: [] / skeleton: [] 빈 리스트 + animationType: 3 + avatarSetup: 1 → Unity가 히트 본 이름(Hips/Spine/Head/LeftUpperLeg...)을 **자동 매핑** (guid 유지 b7c3d9e2f1a64c5e8d0b2a7c4e6f8a91).

**문제2:** 틴트 경고 스팸 재발 — FindPropertyIndex==-1인데 기본값 Color로 SetColor하는 빈틈(936행). **수리:** idx<0이면 _Custom_Color 세팅을 건너뛰고 _Color→_BaseColor 폴백.

**문제3:** DD2 진단기가 non-humanoid 아바타에서 GetBoneTransform 예외. **수리:** 측정 전 avatar.isValid && isHuman 가드, 아니면 `hips=N/A(nonhumanoid)`.

**QA PASS:** 3파일(+36/−65), 중복키 0, 괄호 균형 완벽. 커밋 48a3a885.

**다음 Play 판정:** ① 에디터 포커스 → Heat FBX 재임포트(자동 아바타 생성) ② 이동 시 몸 동작 + `Idle→Walk→Run` ③ DD2 `avatar isHuman=True` + 예외 소멸 ④ 틴트 경고 스팸 소멸. 여전히 정지면 rigImportWarnings 로그 제출 → Configure 매핑표 수리.

---

## 2026-09-05: 애니 6차(A안 실행) — Blender 리그 변환 Player_Rigged_Heat.fbx 통합 ✅

**근본 원인 최종 확정:** 원본 Player_Rigged의 리그 = Blender Rigify 커스텀(27본: Root/pelvis.L·R 분리/spine.001~.005/head·neck 없음). Unity Humanoid 필수 본(Hips/Spine/Head/좌우 사지 히트명) 미충족 → 아바타 isValid=True에도 **리타겟 대상 뼈가 없어 시각 변화 0**. (DD2 최종판: Neural=0 Hybrid=0에도 뼈 정지 — 재생 시스템 전부 정상, 모델 리그가 원인. DD1 로그 hipsΔ 0.4~7.9m은 Animator 재생 입증)

**A안 실행 (Blender 3.6 headless, roll_make/rerig_heat.py):**
- 본 리네임 매핑 23개: Root→Hips, spine→Spine, spine.001→Chest, spine.002→UpperChest, spine.004→Neck, **spine.005→Head(목-머리 부재 보정)**, shoulder.L/R→Left/RightShoulder, upper_arm→UpperArm, forearm→LowerArm, hand→Hand, thigh→UpperLeg, shin→LowerLeg, foot→Foot, toe→Toes
- breast.L/R, pelvis.L/R은 무매핑 유지(Humanoid 필수 아님)
- 메시 버텍스 그룹 자동 리네임(뼈 이름 추적)
- export: bake_anim=False(모델 전용 — 클립은 Player_AC 담당)
- **검증: 필수 17본 전부 존재, 메시 required_hit 17/17**

**산출물:** `roll_make/Player_Rigged_Heat.fbx` → `Assets/Resources/Models/UserProvided/fbx/` 복사 + Humanoid meta(guid b7c3d9e2f1a64c5e8d0b2a7c4e6f8a91, animationType 3, avatarSetup 1) + GameSetup.cs 로드 경로 `Player_Rigged`→`Player_Rigged_Heat` (머티리얼 원본은 GLB 그대로)

**주의:** .gitignore:43 `models/` 룰이 Assets/Resources/Models/를 무시 → **git add -f로 강제 트래킹** (기존 Player_Rigged.fbx도 미트래킹 상태였음 — 인지할 것)

**검증 수치:** missing_required=[] / mesh vertex_groups=27 hit=17/17 / bones_final 27개

**판정 대기 (Play):** 이동 시 몸이 직접 Idle→Walk→Run. 리타겟은 근육공간이라 팩 클립이 Heat 아바타로 재생됨(본 이름 무관).

---

## 2026-09-05: 애니 5차 수리(정책 반영) — Neural/Hybrid 자동부착 완전 제거, Player_AC 단일 경로 확정 ✅