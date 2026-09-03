using UnityEngine;

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
        private bool _deathFired;

        // Speed 지수 평활 + 멈춤 스냅 (지형/경사 충돌로 속도가 0 근처로 순간 떨어질 때
        // Idle로 떨어졌다 복귀하는 "끊김 + 멈춤 모션"을 방지)
        private float _smoothedSpeed;
        private int _stopFrames;

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
            // Speed — CharacterController 수평 속도 크기 (지수 평활로 끊김 제거)
            float raw = 0f;
            if (_cc != null)
            {
                var v = _cc.velocity;
                v.y = 0f;
                raw = v.magnitude;
            }
            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, raw, k);
            if (raw < 0.05f)
            {
                // 연속 3프레임 거의 정지면 즉시 0으로 스냅 → Idle 진입 지연 방지
                if (++_stopFrames >= 3) _smoothedSpeed = 0f;
            }
            else
            {
                _stopFrames = 0;
            }
            _anim.SetFloat("Speed", _smoothedSpeed);

            // 공격 감지 — LastAttackTime 변화 시 트리거 (2타 내 콤보)
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

                    if (_comboCount >= 2) _anim.SetTrigger("AttackCombo");
                    else _anim.SetTrigger("Attack");
                }
            }

            // 구르기 — 상승엣지 1회
            bool rolling = _movement != null && _movement.IsRolling;
            if (rolling && !_prevRolling) _anim.SetTrigger("Roll");
            _prevRolling = rolling;

            // 점프 — 상승엣지 1회
            bool jumping = _movement != null && _movement.IsJumping;
            if (jumping && !_prevJumping) _anim.SetTrigger("Jump");
            _prevJumping = jumping;
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