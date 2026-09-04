# 🎬 RPG팩 애니 "시각적으로 안 보임" 심층 분석 + 수리 계획 (DD1~DD3)

> 작성: 2026-09-04 / 모드: **PLAN ONLY**
> 근거: Player_AC.controller 전수 파싱 + HumanoidClipDriver/GameSetup 소스 + Editor.log
> 사용자 지적: "부착은 된 것 같은데 시각적으로 애니메이션이 보이지 않는다"

---

## 1. 소스 파싱 결과 — 컨트롤러 자체는 정상

### 1.1 Player_AC.controller 구조 (전수 파싱 완료)
- 상태: **Idle(기본)/Walk/Run/Jump/Roll/Attack/AttackCombo/Hit/Death** 9개 전부 존재
- 클립: Idle=OneHand_Up_Idle, Walk=OneHand_Up_Walk_B, Run=OneHand_Up_Run_B, Jump=OneHand_Up_Jump_B, Attack=OneHand_Up_Attack_1, Hit=Hit_F_1, Roll=믹사모, Death=믹사모 — **RPG팩 6종 정상 참조**
- 전이: Idle↔Walk(Speed>4/<0.35), Walk↔Run(>5.5/<0.55), 트리거들 — 형식 정상
- 파라미터: Speed(float) + 트리거 7종 — HumanoidClipDriver 계약과 일치

**즉 컨트롤러/클립/전이는 완전하다. "안 보이는" 이유는 실행 시점 어딘가에 있다.**

## 2. 남은 가능성 (확률 순) — 그리고 검증법

| # | 가설 | 검증법 | 확률 |
|---|------|--------|------|
| H1 | **Animator가 idle 상태에서 재생 중이지만 모션이 미묘해서 "안 보인다"** (OneHand_Up_Idle = 검 뽑은 idle sway — 탑다운 원거리에서 거의 정지로 보임) | 진단 로그로 normalizedTime 진행 확인 | 중 |
| H2 | **HumanoidClipDriver의 Speed 세팅이 0으로 유지** → 걸어도 Idle만 재생. `_cc.velocity`가 CharacterController 클램프/경사에서 실제로 낮게 나오거나, `UpdatePlayer`가 실행 안 됨 | Speed 파라미터 값 로그 | 중 |
| H3 | **Animator가 다른 GameObject에 있어 보이는 몸과 분리** — HumanoidClipDriver는 bodyF에 붙고 `_anim = GetComponentInChildren<Animator>()` — FBX 프리팹 내부 구조상 Animator가 비주얼 뼈대와 다른 계층이면 재생돼도 안 보임 | Hierarchy 경로 로그 | 하 |
| H4 | **재생되지만 Walk 클립이 정지형/원본 문제** — OneHand_Up_Walk_B가 in-place인데 속도 계수 문제 | normalizedTime 확인 | 하 |
| H5 | **애니메이션 레이어 가중치 0 / culling** | 진단 로그 | 하 |

## 3. 수리 계획

### DD1. 런타임 애니 진단기 (0.3일) — 추측을 수치로 끝낸다

`HumanoidClipDriver`에 진단 로그 추가(처음 12초, 2초 간격, 총 6회):
```
[HumanoidClipDriver][Diag] t=2s state='Idle' normT=1.37 speed=0.03 ccVel=0.02 animEnabled=True culling=CullUpdateTransforms
```
- `GetCurrentAnimatorStateInfo(0).shortNameHash→IsName` 현재 상태
- `normalizedTime` 진행 여부(증가 = 재생 중)
- `GetFloat("Speed")` + cc.velocity
- `Animator.enabled`/`cullingMode`
- 이 로그로 H1~H5 중 정체가 즉시 확정된다 (재생 중이면 normalizedTime 증가, 멈추면 고정)

### DD2. 시각 가시성 보강 (0.3일)

1. **walk/run 클립 속도 정밀화**: 걷기 속도 5m/s인데 OneHand_Up_Walk_B의 기본 속도와 안 맞으면 슬라이딩 — 클립 길이 기반 재생 속도 조정(Animator.speed는 건드리지 않고 state speed만)
2. **탑다운 가시성**: 캐릭터 모션의 시각적 존재감이 약하면 — 애니는 정상이되 보이지 않는 문제이므로, 디테일 타일/노멀맵 대비와 무관하게 **캐릭터 렌더러 경계 확인**(렌더러 1개, bounds 1.4×1.8 정상)
3. 만약 DD1 진단이 "재생 중인데 시각 변화 없음"이면 → 클립 자체가 제자리 미세모션일 가능성 → 팩 내 대안 클립으로 교체(예: Walk_B 대신 전력질주 계열) 후 재판정

### DD2. H3 대비 — 이중 Animator/분리 확인 (병행)

- `ModelAnimatorAssigner`가 playerInstance에 추가한 Animator(루트)와 bodyF의 Animator가 **서로 다른 컨트롤러를 가리키면** 보이는 몸의 애니가 무시될 수 있음
- 진단 로그에 "같은 GameObject 계층의 Animator 수" 포함 — 2개 이상 발견 시 비활성 쪽 정리
- 추가: `DisableGLBRenderers type not found` 경고의 원래 목적(구 GLB 렌더러 정리) 재확인 — 구 GLB 몸이 여전히 렌더 중이면 **보이는 몸이 FBX가 아닐 수도**

### DD3. 검증 순서 (0.5일)

1. DD1 진단 로그로 확정 → 원인별 즉시 수리
2. 스크린샷 58(걷기 2초 시점) — 다리 위치가 달라지는지
3. 남으면 DD2 대안 클립 교체

## 4. 소요: 1.1일 (DD1 진단이 먼저 — 나머지는 진단 결과에 따라)