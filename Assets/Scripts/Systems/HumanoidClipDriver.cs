using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 믹사모 Humanoid Animator 런타임 드라이버 (플레이어 / 병사).
    ///
    /// 각 런타임 AnimatorController가 정의한 공통 파라미터를 구동한다:
    ///   Speed(float) / 트리거 Attack, AttackCombo, Hit, Death
    ///   (Player_AC에만 추가) Roll, Jump
    ///
    /// ■ Player 모드 — CharacterController.velocity(수평)를 Speed로, PlayerCombat.LastAttackTime
    ///   변화를 공격 트리거(2타 내 콤보 감지), PlayerMovement.IsRolling/IsJumping 상승엣지를
    ///   Roll/Jump 트리거로 변환한다.
    /// ■ Soldier 모드 — transform 위치 델타로 Speed를 계산하고, 공격은 GuardCombatAI가
    ///   TriggerAttack()으로 호출한다.
    /// </summary>
    public class HumanoidClipDriver : MonoBehaviour
    {
        public enum DriveMode { Player, Soldier }

        [Header("드라이브 모드")]
        public DriveMode mode = DriveMode.Player;

        // 콤보 창 (초). 마지막 공격 후 이 시간 안에 다음 공격이 들어오면 콤보로 취급.
        private const float ComboWindow = 2f;

        private Animator _anim;
        private CharacterController _cc;
        private PlayerMovement _movement;
        private PlayerCombat _combat;

        private Vector3 _lastPos;
        private float _prevCombatAttack = -999f;   // 직전 프레임의 LastAttackTime
        private int _comboCount;
        private float _lastAttackAt = -999f;       // 마지막 공격 시각 (Time.time)
        private bool _prevRolling, _prevJumping;
        private float _prevSpeedForTransition = -999f;   // T-D3: Run→Walk 전환 연출용 직전 프레임 속도
        private bool _prevBow, _prevSpear, _prevThrow;   // T-D3: 무기 모드 엣지 감지
        private WeaponType _prevWType = WeaponType.Fist; // M2: CurrentType 엣지 감지
        private bool _deathFired;

        /// <summary>T-D3+: 외부 시스템 발화용 퍼블릭 트리거(채집/경직/스턴/다운).</summary>
        public void TriggerHarvest() { if (_anim != null) _anim.SetTrigger("Harvest"); }
        public void TriggerHitLight() { if (_anim != null) _anim.SetTrigger("HitLight"); }
        public void TriggerStun() { if (_anim != null) _anim.SetTrigger("Stun"); }
        public void TriggerKnockdown() { if (_anim != null) _anim.SetTrigger("Knockdown"); }
        public void TriggerSitDown() { if (_anim != null) _anim.SetTrigger("SitDown"); }
        public void TriggerSitUp() { if (_anim != null) _anim.SetTrigger("SitUp"); }
        public void TriggerToss() { if (_anim != null) _anim.SetTrigger("Toss"); }
        public void TriggerDrink() { if (_anim != null) _anim.SetTrigger("Drink"); }
        public void TriggerLadder() { if (_anim != null) _anim.SetTrigger("Ladder"); }
        public void TriggerClimbStairs() { if (_anim != null) _anim.SetTrigger("ClimbStairs"); }
        public void TriggerWallDown() { if (_anim != null) _anim.SetTrigger("WallDown"); }
        public void TriggerThrow() { if (_anim != null) _anim.SetTrigger("Throw"); }
        public void TriggerCarry() { if (_anim != null) _anim.SetTrigger("Carry"); }
        public void TriggerCast() { if (_anim != null) _anim.SetTrigger("Cast"); }
        public void TriggerHarvestObject() { if (_anim != null) _anim.SetTrigger("HarvestObject"); }
        public void TriggerHarvestPickUp() { if (_anim != null) _anim.SetTrigger("HarvestPickUp"); }
        public void TriggerOpenDoor() { if (_anim != null) _anim.SetTrigger("OpenDoor"); }
        public void TriggerTalkA() { if (_anim != null) _anim.SetTrigger("Talk"); }
        public void TriggerTalkB() { if (_anim != null) _anim.SetTrigger("Talk2"); }
        public void TriggerVictory() { if (_anim != null) _anim.SetTrigger("Victory"); }

        // DD1: 애니 상태 진단 타임라인 (최초 600초, 주기 로그 + 상태 전환 즉시 로그)
        private float _diagStart = -1f;
        private float _nextDiagTime = 0f;
        private int _prevStateHash = -1;     // 직전 프레임 상태 hash (전환 감지용)
        private string _prevStateName = "?"; // 직전 상태 이름 (전환 로그 출력용)
        private bool _diagEndLogged;         // 600초 종료 로그 1회 여부
        private float _diagRawSpeed;         // 진단용 raw(스무딩 전) 속도

        // DD2: 뼈 변위 진단 — 2초 주기 스냅샷 비교로 뼈가 실제 움직이는지 수치 확정
        private Vector3 _hipsPosRef;         // 직전 주기의 Hips 위치
        private Quaternion _hipsRotRef;      // 직전 주기의 Hips 회전
        private bool _hipsRefValid;          // 첫 주기는 기준점 저장만 (Δ 계산 스킵)
        // DD3-2: 사지 뼈 변위 — LeftHand/LeftFoot의 Hips 기준 상대벡터(world) 스냅샷 비교
        private Vector3 _lhRelRef;           // 직전 주기 LeftHand의 Hips 기준 상대벡터
        private Vector3 _lfRelRef;           // 직전 주기 LeftFoot의 Hips 기준 상대벡터
        private bool _limbRefValid;          // 첫 주기는 기준 저장만 (Δ 계산 스킵)

        // DD4: 사지 로컬회전 Δ — localRotation 스냅샷 비교. 루트 모션/루트 회전에 완전 면역
        // (DD3-2의 world 상대벡터는 캐릭터가 회전만 해도 Δ가 발생하는 오염이 있음).
        private Quaternion _lfaLocalRef;     // 직전 주기 LeftLowerArm localRotation
        private Quaternion _lulLocalRef;     // 직전 주기 LeftUpperLeg localRotation
        private bool _limbLocalValid;        // 첫 주기는 기준 저장만 (Δ 계산 스킵)

        // Speed 지수 평활 + 멈춤 스냅 (지형/경사 충돌로 속도가 0 근처로 순간 떨어질 때
        // Idle로 떨어졌다 복귀하는 "끊김 + 멈춤 모션"을 방지)
        private float _smoothedSpeed;
        // 2026-09-10: 공격 애니 상태 최소 유지 — 연타 콤보 중 Idle 경유 팝 방지
        private float _attackHoldUntil;   // 이 시각까지 Speed 파라미터를 0으로 고정(공격 애니 보호)
        private float _stallTime;        // 정지 판정 홀드 타이머 — 0.25초 연속 정체 시에만 스냅
        private float _lastTarget;       // 직전 프레임 목표 속도 — 미세 정체 홀드 중 유지값
        // T2B-3(애니 측): 점프→착지 하강 에지 직후 Speed 목표 상승을 잠시 제한 —
        // Jump 종료 후 Walk/Run 복귀 시 클립 팝(급가속) 체감 완화. 블렌드 시간은
        // 애니메이터 컨트롤러(에셋)가 담당하므로 여기서는 속도 파라미터 램프만 완화한다.
        private float _landingSoftTimer;

        private void Start()
        {
            _anim = GetComponentInChildren<Animator>();
            if (_anim == null) _anim = GetComponent<Animator>();

            _cc = GetComponentInParent<CharacterController>();

            switch (mode)
            {
                case DriveMode.Player:
                    _movement = GetComponentInParent<PlayerMovement>();
                    _combat = GetComponentInParent<PlayerCombat>();
                    break;
                case DriveMode.Soldier:
                    _lastPos = transform.position;
                    break;
            }

            if (_anim != null) _anim.SetFloat("Speed", 0f);

            // 진단: 아바타/컨트롤러 상태 (T-pose 원인 판별용)
            var avatarInfo = (_anim != null && _anim.avatar != null)
                ? $"{_anim.avatar.name}:isValid={_anim.avatar.isValid}"
                : "NULL";
            var ctrlInfo = (_anim != null && _anim.runtimeAnimatorController != null)
                ? _anim.runtimeAnimatorController.name
                : "NULL";
            Debug.Log($"[HumanoidClipDriver] anim=OK avatar={avatarInfo} controller={ctrlInfo} mode={mode}");

            // DD1: 이중 Animator 감지 (1회) — 오작동/모션 미표시 원인 후보 파악
            var allAnims = GetComponentsInChildren<Animator>(true);
            if (allAnims != null && allAnims.Length > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < allAnims.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(allAnims[i].gameObject.name);
                }
                Debug.Log($"[HumanoidClipDriver][Diag] 애니메이터 개수={allAnims.Length} → [{sb}]");
            }

            // DD2: 뼈 진단 — 아바타 유효성 / 렌더러(스키닝) 상태 / 루트 컴포넌트 구성
            // (Player_AC 재생 정상 + 뼈 무응답 원인 판별: 스키닝 렌더러 누락, 골격 덮어쓰기 컨트롤러 부착 여부 등)
            if (_anim != null)
            {
                bool avValid = _anim.avatar != null && _anim.avatar.isValid;
                bool avHuman = _anim.avatar != null && _anim.avatar.isHuman;
                Debug.Log($"[HumanoidClipDriver][DD2] avatar={(_anim.avatar != null ? _anim.avatar.name : "NULL")} isValid={avValid} isHuman={avHuman}");

                // PlayerBody(드라이버 자신=bodyF) 하위의 렌더러 수집 — SkinnedMesh가 뼈 스키닝의 최종 표현체
                var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var meshRend = GetComponentsInChildren<MeshRenderer>(true);
                int smEnabled = 0;
                string smDetail = "없음";
                if (skinned != null && skinned.Length > 0)
                {
                    var sm0 = skinned[0];
                    smDetail = $"{sm0.gameObject.name}: rootBone={(sm0.rootBone != null ? sm0.rootBone.name : "NULL")}, bones={(sm0.bones != null ? sm0.bones.Length : 0)}, mesh={(sm0.sharedMesh != null ? sm0.sharedMesh.name : "NULL")}";
                    foreach (var s in skinned)
                        if (s != null && s.enabled) smEnabled++;
                    // DD3-3: SMR 본 동일성(1회) — sm0.bones 중 이 Animator(_anim.transform)의 자손이 아닌 본이
                    // 있으면 스키닝이 다른 골격을 따라가 애니 결과가 렌더에 반영되지 않는 원인이 된다.
                    int foreignBones = 0;
                    var sbF = new System.Text.StringBuilder();
                    if (sm0.bones != null)
                    {
                        for (int i = 0; i < sm0.bones.Length; i++)
                        {
                            var bb = sm0.bones[i];
                            if (bb == null || !bb.IsChildOf(_anim.transform))
                            {
                                foreignBones++;
                                if (foreignBones <= 3)
                                {
                                    if (foreignBones > 1) sbF.Append(", ");
                                    sbF.Append(bb != null ? bb.name : "NULL");
                                }
                            }
                        }
                    }
                    Debug.Log($"[HumanoidClipDriver][DD3-3] SMR본 외부골격={foreignBones}/{(sm0.bones != null ? sm0.bones.Length : 0)} 처음3개=[{sbF}]");
                }
                int mrEnabled = 0;
                foreach (var mr in meshRend)
                    if (mr != null && mr.enabled) mrEnabled++;
                int smCount = skinned != null ? skinned.Length : 0;
                int mrCount = meshRend != null ? meshRend.Length : 0;
                Debug.Log($"[HumanoidClipDriver][DD2] PlayerBody 렌더러: SkinnedMesh={smCount}(enabled={smEnabled}, {smDetail}), Mesh={mrCount}(enabled={mrEnabled})");

                // 루트(Player)에 붙은 골격 개입 컴포넌트 존재 여부 — Hybrid 골격 덮어쓰기 용의 판별
                var root = _anim.transform.root;
                int hasNeural = root.GetComponent<ProjectName.Systems.Animation.Neural.NeuralAnimationController>() != null ? 1 : 0;
                int hasHybrid = root.GetComponent<ProjectName.Systems.Animation.Neural.HybridAnimationController>() != null ? 1 : 0;
                int hasProc = root.GetComponent<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>() != null ? 1 : 0;
                int hasBoneMap = root.GetComponent<ProjectName.Systems.Animation.Procedural.Bones.ProceduralBoneMap>() != null ? 1 : 0;
                int hasRigAnim = root.GetComponent<RigAnimationController>() != null ? 1 : 0;
                var rootAnims = root.GetComponentsInChildren<Animator>(true);
                var sbA = new System.Text.StringBuilder();
                if (rootAnims != null)
                    for (int i = 0; i < rootAnims.Length; i++)
                    {
                        if (i > 0) sbA.Append(", ");
                        sbA.Append(rootAnims[i].gameObject.name);
                    }
                Debug.Log($"[HumanoidClipDriver][DD2] 루트 컴포넌트: Neural={hasNeural} Hybrid={hasHybrid} Procedural={hasProc} BoneMap={hasBoneMap} RigAnim={hasRigAnim} Animator(루트포함전체)=[{sbA}]");
            }
            // DD3-1: 아바타 매핑 덤프(1회) — humanDescription.human 비어있음 가설 검증.
            // 매핑 본수=0이면 휴머노이드 리타깃이 전혀 안 되어 클립 재생(normT 진행)과 무관하게 포즈가 동결한다.
            if (_anim != null && _anim.avatar != null)
            {
                try
                {
                    var humanMap = _anim.avatar.humanDescription.human;
                    var sbM = new System.Text.StringBuilder();
                    for (int i = 0; i < humanMap.Length; i++)
                    {
                        if (i > 0) sbM.Append(", ");
                        sbM.Append($"{humanMap[i].boneName}→{humanMap[i].humanName}");
                    }
                    Debug.Log($"[HumanoidClipDriver][DD3-1] 매핑 본수={humanMap.Length} [{sbM}]");
                }
                catch (System.Exception mapEx)
                {
                    Debug.Log($"[HumanoidClipDriver][DD3-1] humanDescription 접근 실패: {mapEx.GetType().Name}: {mapEx.Message}");
                }
                // 둘 다 확정: HumanBodyBones 0~54를 GetBoneTransform으로 훑어 non-null 개수 확인
                try
                {
                    int mappedCount = 0;
                    for (int b = 0; b < (int)HumanBodyBones.LastBone; b++)
                        if (_anim.GetBoneTransform((HumanBodyBones)b) != null) mappedCount++;
                    Debug.Log($"[HumanoidClipDriver][DD3-1] 실질매핑={mappedCount}/55");
                }
                catch (System.Exception boneEx)
                {
                    Debug.Log($"[HumanoidClipDriver][DD3-1] GetBoneTransform 스캔 실패: {boneEx.GetType().Name}: {boneEx.Message}");
                }
            }
            else
            {
                Debug.Log("[HumanoidClipDriver][DD3-1] avatar=NULL — 매핑 덤프 불가");
            }
        }

        private void Update()
        {
            if (_anim == null) return;

            switch (mode)
            {
                case DriveMode.Player:
                    UpdatePlayer();
                    break;
                case DriveMode.Soldier:
                    UpdateSoldier();
                    break;
            }
        }

        // ───────────────────── Player 모드 ─────────────────────
        private void UpdatePlayer()
        {
            if (_diagStart < 0f) _diagStart = Time.time;
            bool diagActive = Time.time - _diagStart <= 600f;

            // Speed — CharacterController 수평 속도 크기 (지수 평활로 끊김 제거)
            float raw = 0f;
            if (_cc != null)
            {
                var v = _cc.velocity;
                v.y = 0f;
                raw = v.magnitude;
            }
            _diagRawSpeed = raw; // DD1: 스무딩 전 속도 — 스냅 로직 오작동 구분용

            // 측정 속도 추적(실측 확정: 일반 이동 5.0 / 대시 15.0) — raw를 그대로 목표로 사용하면
            // Run 클립이 실제 이동속도와 매칭된다(발 미끄러짐 방지는 Run 상태 speed 스케일링 0.2 담당).
            float target;
            if (raw > 0.05f)
            {
                _stallTime = 0f;
                target = raw;                       // 실측 속도 추적 — Run 클립이 실제 이동속도와 매칭
            }
            else
            {
                _stallTime += Time.deltaTime;
                target = _stallTime >= 0.25f ? 0f : _lastTarget; // 미세 정체 홀드
            }
            _lastTarget = target;
            // T2B-3: 착지 직후 0.22초간 Speed 목표를 "현재 평활값 + 1.5" 이하로 제한 —
            // Walk/Run 클립 복귀 시 파라미터 급등에 의한 클립 팝을 완화 (지수 평활과 합쳐 자연 흡수)
            if (_landingSoftTimer > 0f)
            {
                _landingSoftTimer -= Time.deltaTime;
                target = Mathf.Min(target, _smoothedSpeed + 1.5f);
                _lastTarget = target;
            }
            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, target, k);
            // 2026-09-10: 공격 애니 보호 — 공격 상태 유지 시간(_attackHoldUntil) 내엔 Speed를 0으로 고정해
            // Speed 조건 전이(Idle/Walk)가 공격 애니를 인터럽트하지 않게 한다.
            bool inAttackHold = Time.time < _attackHoldUntil;
            _anim.SetFloat("Speed", inAttackHold ? 0f : _smoothedSpeed);
            // T-D3 이동 파라미터 확장: 로컬 이동 벡터 → MoveX(측면)/MoveY(전후) — 후진/선회 상태 전환용
            if (_movement != null)
            {
                Vector3 lmv = _movement.LocalMoveDirection;
                _anim.SetFloat("MoveX", lmv.x);
                _anim.SetFloat("MoveY", lmv.z);
            }

            // T-D3+: 웅크림/수영 상태 피드(QA 발견 누락 보강) — CrouchF/B/L/R·SwimI/F 진입 조건
            _anim.SetBool("IsCrouch", _movement != null && _movement.IsCrouching);
            _anim.SetBool("IsSwimming", _movement != null && _movement.IsSwimming);

            // 2026-09-09: 은신 피드 추가 — 기존엔 Sneaky 상태로 가는 전이 조건이 전혀 공급되지 않아 은신 애니 미발동
            var stealth = StealthSystem.Instance;
            _anim.SetBool("IsStealthed", stealth != null && stealth.IsStealthed);

            // M2 정식 장착 연동: CurrentType 기반 게이트(핫바 장착이 유일한 전환 경로)
            var wtype = WeaponEquipManager.CurrentType;
            bool throwing = PlayerWeaponModeBridge.ThrowSelected;
            if (wtype != _prevWType)
            {
                if (wtype == WeaponType.Bow) _anim.SetTrigger("BowEnter");
                if (wtype == WeaponType.Spear) _anim.SetTrigger("SpearEnter");
            }
            _prevWType = wtype;
            if (throwing && !_prevThrow) _anim.SetTrigger("ThrowEnter");
            _prevThrow = throwing;
            _anim.SetBool("IsCombat", wtype != WeaponType.Fist);
            _anim.SetBool("IsBow", wtype == WeaponType.Bow);
            _anim.SetBool("IsSpear", wtype == WeaponType.Spear);
            _anim.SetBool("IsThrowing", throwing);

            // DD1: 상태 전환 즉시 로그 — Idle ↔ Walk(걷기) 전환 발생 여부 결정적 증거
            if (diagActive) LogStateTransition();

            // DD1: 600초간 2초 간격 주기 — 실제 재생 상태(state/normT/speed)와 컨트롤러 속도 진단
            if (diagActive && Time.time >= _nextDiagTime)
            {
                _nextDiagTime = Time.time + 2f;
                LogAnimDiagnostic();
            }

            // DD1: 진단 기간 종료 — 스팸 방지를 위해 주기/전환 로그 중단
            if (!diagActive && !_diagEndLogged)
            {
                _diagEndLogged = true;
                Debug.Log($"[HumanoidClipDriver][Diag] 진단 기간 600초 종료 — 주기/전환 로그 중단 (state={ResolveStateName(_anim.GetCurrentAnimatorStateInfo(0))})");
            }

            // 공격 감지 — LastAttackTime 변화 시 트리거 (2타 내 콤보)
            // 2026-09-10: 콤보 트리거는 드라이버 Update에서 1회성 발화하는 구조라 유효 —
            // 문제는 컨트롤러 쪽 Attack* 상태 진입 후 즉시 Idle 복귀(AfterStateExit 판정)였다.
            // Attack* 상태 유지 중 다음 콤보 트리거를 받으면 연타 콤보가 자연스럽게 이어지도록
            // 공격 상태 감시 스위치(_attackHoldUntil)를 두고, 유지 시간 내엔 Speed→Idle 전이 조건이
            // 사실상 불발되게 Speed를 잠시 0 근처로 고정한다(공격 중 이동 애니 인터럽트 방지).
            // 공격 상태 감시 — Attack* 진입 중 Speed 0 고정(Idle/Walk 인터럽트 차단)
            if (_anim != null)
            {
                var stInfo = _anim.GetCurrentAnimatorStateInfo(0);
                // ResolveStateName 미매핑 상태(AttackBase/AttackThrust/AttackCombo2/3)도 감지 — IsName 직접 비교
                bool attackStateHold = stInfo.IsName("Attack") || stInfo.IsName("AttackBase") || stInfo.IsName("AttackThrust")
                    || stInfo.IsName("AttackCombo") || stInfo.IsName("AttackCombo2") || stInfo.IsName("AttackCombo3");
                if (attackStateHold)
                {
                    // 공격 애니 재생 중: Speed 파라미터를 0으로 고정 — Idle/Walk로 가는 Speed 조건 전이 불발
                    _anim.SetFloat("Speed", 0f);
                    // 콤보 창이 열려 있으면(마지막 공격 후 2초 내) 상태 유지 시간 연장
                    if (_lastAttackAt > 0f && Time.time - _lastAttackAt < ComboWindow)
                        _attackHoldUntil = Mathf.Max(_attackHoldUntil, Time.time + 0.35f);
                }
            }
            if (_combat != null)
            {
                float lat = _combat.LastAttackTime;
                if (!Mathf.Approximately(lat, _prevCombatAttack))
                {
                    _prevCombatAttack = lat;

                    float prevAttackAt = _lastAttackAt;
                    _lastAttackAt = Time.time;

                    // 콤보 판정: 마지막 공격 후 콤보 창 내 연속 공격이면 카운트 증가, 아니면 초기화
                    if (Time.time - prevAttackAt <= ComboWindow) _comboCount++;
                    else _comboCount = 1;

                    if (_comboCount >= 4) _anim.SetTrigger("AttackCombo3");
                    else if (_comboCount >= 3) _anim.SetTrigger("AttackCombo2");
                    else if (_comboCount >= 2) _anim.SetTrigger("AttackCombo");
                    else if (WeaponEquipManager.CurrentType == WeaponType.Spear) _anim.SetTrigger("AttackThrust"); // M5: 창=찌르기
                    else
                    {
                        int r = Random.Range(0, 3);
                        _anim.SetTrigger(r == 0 ? "Attack" : r == 1 ? "AttackThrust" : "AttackBase");
                    }
                    // 공격 상태 최소 유지 시작 — 연타 중 Idle 경유 팝 방지
                    _attackHoldUntil = Time.time + 0.45f;
                }
            }

            // 구르기 — 상승엣지 1회
            bool rolling = _movement != null && _movement.IsRolling;
            if (rolling && !_prevRolling) _anim.SetTrigger("Roll");
            _prevRolling = rolling;

            // 점프 — 상승엣지 1회 (후진 입력 중 점프면 Back_Jump)
            bool jumping = _movement != null && _movement.IsJumping;
            if (jumping && !_prevJumping)
            {
                bool backJump = _movement.LocalMoveDirection.z < -0.2f;
                _anim.SetTrigger(backJump ? "JumpBack" : "Jump");
            }

            // M3/M4: 무기 모드 입력 — 활(우클릭 발사: 화살 데미지 연동), 폭탄 선택 중 좌클릭(던지기: Bomb 스폰)
            if (_movement != null && _anim != null)
            {
                if (WeaponEquipManager.CurrentType == WeaponType.Bow && Input.GetMouseButtonDown(1))
                {
                    _anim.SetTrigger("ArcheryShot");
                    var origin = _anim.transform.position + Vector3.up * 1.5f;
                    ArrowProjectile.Spawn(origin, _anim.transform.forward, 22f, WeaponData.Bow.damage, new Color(0.9f, 0.8f, 0.4f));
                }
                if (PlayerWeaponModeBridge.ThrowSelected && Input.GetMouseButtonDown(0))
                {
                    bool pitch = Random.Range(0, 2) == 0;
                    _anim.SetTrigger(pitch ? "ThrowPitch" : "Throw");
                    var bombPrefab = Resources.Load<GameObject>("Bombs/Bomb_Explosive");
                    if (bombPrefab != null)
                    {
                        var go = Object.Instantiate(bombPrefab, _anim.transform.position + Vector3.up * 1.2f + _anim.transform.forward * 0.4f, Quaternion.identity);
                        var rb = go.GetComponent<Rigidbody>();
                        if (rb != null) rb.linearVelocity = _anim.transform.forward * 8f + Vector3.up * 5f;
                    }
                    PlayerWeaponModeBridge.ThrowSelected = false; // 1회 투척 후 해제
                }
            }
            // T2B-3: 착지(하강 에지) 직후 짧은 흡수 창 — Speed 급상승 제한은 아래 목표 계산에서 적용
            if (!jumping && _prevJumping) _landingSoftTimer = 0.22f;
            _prevJumping = jumping;

            // T-D3: Run→Walk 전환 연출 — Speed가 2.0 임계를 아래로 하향 통과할 때 1회
            // 2026-09-10: 공격 애니 보호 — _attackHoldUntil 유지 중엔 RunToWalk 트리거 발화 억제.
            // (RunToWalk는 AnyState 전이라 공격 애니 중에도 Run_to_Walk_Transition으로 인터럽트함 — normT 5% 팝 실증)
            if (_prevSpeedForTransition > 2f && _smoothedSpeed <= 2f && _anim != null
                && Time.time >= _attackHoldUntil)
                _anim.SetTrigger("RunToWalk");
            _prevSpeedForTransition = _smoothedSpeed;
        }

        /// <summary>T-D3: 활 화살/투척물 연출 스폰(전방 포물선). 데미지 연동은 무기 시스템 후속.</summary>
        private void SpawnProjectile(string kind)
        {
            if (_anim == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = kind == "arrow" ? "Arrow_Visual" : "Thrown_Visual";
            go.transform.localScale = Vector3.one * (kind == "arrow" ? 0.15f : 0.3f);
            var rb = go.AddComponent<Rigidbody>();
            var origin = _anim.transform.position + Vector3.up * 1.5f;
            go.transform.position = origin;
            var forward = _anim.transform.forward;
            rb.linearVelocity = forward * 18f + Vector3.up * (kind == "arrow" ? 2f : 5f);
            Object.Destroy(go, 3f);
        }

        /// <summary>DD1: 현재 애니 상태/진행도/속도 로그 (state는 shortNameHash→이름 매핑).</summary>
        private void LogAnimDiagnostic()
        {
            if (_anim == null) return;
            var sinfo = _anim.GetCurrentAnimatorStateInfo(0);
            string stateName = ResolveStateName(sinfo);
            float speed = 0f;
            try { speed = _anim.GetFloat("Speed"); } catch { }
            float ccVel = _cc != null ? _cc.velocity.magnitude : -1f;

            // DD2: 뼈 변위 (Hips 기준) — 2초 전 스냅샷과 비교. 뼈가 실제 움직이는지 수치 확정.
            // 첫 주기는 기준점 저장만 하고 Δ는 생략. Δ=0이 지속되면 뼈가 얼어있음(골격 덮어쓰기/드라이브 실패).
            // 휴머노이드 아바타가 유효할 때만 GetBoneTransform 호출 (non-humanoid InvalidOperationException 예방).
            var av = _anim.avatar;
            bool canHuman = av != null && av.isValid && av.isHuman;
            string hipsDelta = " hips=NULL";
            if (!canHuman)
            {
                hipsDelta = " hips=N/A(nonhumanoid)";
            }
            else
            {
                var hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    if (_hipsRefValid)
                    {
                        float posD = Vector3.Distance(hips.position, _hipsPosRef);
                        float rotD = Quaternion.Angle(hips.rotation, _hipsRotRef);
                        hipsDelta = $" hipsΔ={posD:F4}m rotΔ={rotD:F2}°";
                    }
                    _hipsPosRef = hips.position;
                    _hipsRotRef = hips.rotation;
                    _hipsRefValid = true;
                    // DD3-2: 사지 변위 — LeftHand/LeftFoot의 Hips 기준 상대벡터(world)를 같은 2초 주기로 스냅샷 비교.
                    // Hips는 이동으로 움직여도 사지 포즈가 얼어있으면 상대벡터 불변 → 매핑/드라이브 실패 분리 확정.
                    // 첫 주기는 기준 저장만. 매핑 누락(GetBoneTransform=null) 시 해당 필드 UNMAPPED 표시.
                    var lh = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
                    var lf = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var limbSb = new System.Text.StringBuilder();
                    if (lh != null)
                    {
                        var lhRel = lh.position - hips.position;
                        if (_limbRefValid) limbSb.Append($" LHandΔ={Vector3.Distance(lhRel, _lhRelRef):F3}");
                        _lhRelRef = lhRel;
                    }
                    else
                    {
                        limbSb.Append(" LHand=UNMAPPED");
                    }
                    if (lf != null)
                    {
                        var lfRel = lf.position - hips.position;
                        if (_limbRefValid) limbSb.Append($" LFootΔ={Vector3.Distance(lfRel, _lfRelRef):F3}");
                        _lfRelRef = lfRel;
                    }
                    else
                    {
                        limbSb.Append(" LFoot=UNMAPPED");
                    }
                    if (lh != null || lf != null) _limbRefValid = true;
                    hipsDelta += limbSb.ToString();
                }
            }

            // DD4: 실제 재생 클립명 — 상태(Walk)가 재생 중이어도 클립이 안 붙었으면 뼈에 아무 것도 쓰이지 않는다.
            string clipInfo = "NONE";
            try
            {
                var clips = _anim.GetCurrentAnimatorClipInfo(0);
                clipInfo = clips != null && clips.Length > 0
                    ? clips[0].clip.name + "(human=" + clips[0].clip.humanMotion + ",len=" + clips[0].clip.length.ToString("F2") + "s)"
                    : "NONE";
            }
            catch (System.Exception clipEx)
            {
                clipInfo = "ERR:" + clipEx.GetType().Name;
            }

            // DD4: 사지 로컬회전 Δ — localRotation 스냅샷 비교. 루트 모션/루트 회전에 완전 면역
            // (world 상대벡터 지표와 달리 캐릭터 회전만으로는 Δ가 생기지 않음). Δ=0 지속이면 사지 포즈 동결 확정.
            string limbLocal = "";
            if (canHuman)
            {
                var lfa = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var lul = _anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var llSb = new System.Text.StringBuilder();
                if (lfa != null)
                {
                    if (_limbLocalValid) llSb.Append($" LFArmRotΔ={Quaternion.Angle(lfa.localRotation, _lfaLocalRef):F2}°");
                    _lfaLocalRef = lfa.localRotation;
                }
                else
                {
                    llSb.Append(" LFArmRot=UNMAPPED");
                }
                if (lul != null)
                {
                    if (_limbLocalValid) llSb.Append($" LULegRotΔ={Quaternion.Angle(lul.localRotation, _lulLocalRef):F2}°");
                    _lulLocalRef = lul.localRotation;
                }
                else
                {
                    llSb.Append(" LULegRot=UNMAPPED");
                }
                if (lfa != null || lul != null) _limbLocalValid = true;
                limbLocal = llSb.ToString();
            }

            Debug.Log($"[HumanoidClipDriver][Diag] t={Time.time:F1}s state={stateName} normT={sinfo.normalizedTime:F2} speed={speed:F2} rawSpd={_diagRawSpeed:F2} ccVel={ccVel:F2} animEnabled={_anim.enabled} culling={_anim.cullingMode}{hipsDelta} clip={clipInfo}{limbLocal}");
        }

        /// <summary>DD1: 상태 shortNameHash를 이름으로 매핑 (매핑 실패 시 hex hash 반환).</summary>
        private string ResolveStateName(AnimatorStateInfo sinfo)
        {
            if (sinfo.IsName("Idle")) return "Idle";
            if (sinfo.IsName("Walk")) return "Walk";
            if (sinfo.IsName("Run")) return "Run";
            if (sinfo.IsName("Attack")) return "Attack";
            if (sinfo.IsName("AttackCombo")) return "AttackCombo";
            if (sinfo.IsName("Roll")) return "Roll";
            if (sinfo.IsName("Jump")) return "Jump";
            if (sinfo.IsName("Death")) return "Death";
            return sinfo.shortNameHash.ToString("X8");
        }

        /// <summary>DD1: 현재 상태 hash가 직전 프레임과 다르면 전환 즉시 1회 로그.</summary>
        private void LogStateTransition()
        {
            if (_anim == null) return;
            var sinfo = _anim.GetCurrentAnimatorStateInfo(0);
            int hash = sinfo.fullPathHash;
            if (hash == _prevStateHash) return;
            string curName = ResolveStateName(sinfo);
            float speed = 0f;
            try { speed = _anim.GetFloat("Speed"); } catch { }
            Debug.Log($"[HumanoidClipDriver][State] {_prevStateName} → {curName} (speed={speed:F2} rawSpd={_diagRawSpeed:F2} normT={sinfo.normalizedTime:F2})");
            _prevStateHash = hash;
            _prevStateName = curName;
        }

        // ───────────────────── Soldier 모드 ─────────────────────
        private void UpdateSoldier()
        {
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - _lastPos;
            float speed = dt > 0.0001f ? delta.magnitude / dt : 0f;
            _anim.SetFloat("Speed", speed);
            _lastPos = transform.position;
        }


        // ───────────────────── 머티리얼 유틸 ─────────────────────
        /// <summary>
        /// 원본 GLB(텍스처 정상)의 머티리얼을 Humanoid FBX 본체로 복사한다.
        /// Blender FBX export는 텍스처를 유실하므로 흰색으로 보이는 문제의 해결책.
        /// URP Lit 재생성 + GLB의 _BaseMap 텍스처 이식.
        /// </summary>
        public static void CopyMaterialsFromGlb(GameObject fbxBody, string glbResourcePath)
        {
            var glbPrefab = Resources.Load<GameObject>(glbResourcePath);
            if (glbPrefab == null)
            {
                Debug.LogWarning($"[HumanoidClipDriver] GLB 원본 없음: {glbResourcePath}");
                return;
            }
            var temp = Object.Instantiate(glbPrefab);
            temp.SetActive(false);
            try
            {
                var srcRends = temp.GetComponentsInChildren<Renderer>(true);
                var dstRends = fbxBody.GetComponentsInChildren<Renderer>(true);
                if (srcRends.Length == 0 || dstRends.Length == 0)
                {
                    Debug.LogWarning("[HumanoidClipDriver] 머티리얼 복사 대상 렌더러 없음");
                    return;
                }
                var src = srcRends[0].sharedMaterial;
                Texture baseTex = src != null && src.HasProperty("_BaseMap")
                    ? src.GetTexture("_BaseMap") : (src != null ? src.mainTexture : null);
                Color baseCol = src != null && src.HasProperty("_BaseColor")
                    ? src.GetColor("_BaseColor") : Color.white;

                var urpLit = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(urpLit);
                if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
                mat.SetColor("_BaseColor", baseCol);
                foreach (var r in dstRends)
                    if (r != null) r.sharedMaterial = mat;
                Debug.Log($"[HumanoidClipDriver] 머티리얼 복사 완료: 대상 {dstRends.Length}개, 텍스처={(baseTex != null ? baseTex.name : "없음")}");
            }
            finally
            {
                Object.Destroy(temp);
            }
        }

        // ───────────────────── 외부 트리거 ─────────────────────
        /// <summary>병사 공격 트리거 (GuardCombatAI 등에서 호출). 모드 무관 동작.</summary>
        public void TriggerAttack()
        {
            if (_anim == null) return;
            _anim.SetTrigger("Attack");
        }

        /// <summary>사망 트리거. 최초 1회만 발동 (외부에서 사망 시 호출).</summary>
        public void TriggerDeath()
        {
            if (_anim == null || _deathFired) return;
            _deathFired = true;
            _anim.SetTrigger("Death");
        }
    }
}