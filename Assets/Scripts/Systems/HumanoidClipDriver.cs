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
            // Speed — CharacterController 수평 속도 크기
            float speed = 0f;
            if (_cc != null)
            {
                var v = _cc.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            _anim.SetFloat("Speed", speed);

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