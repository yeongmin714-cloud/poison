using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 공격 데이터 정의 (ScriptableObject로 관리 가능).
    /// 콤보, 데미지, 넉백, 히트박스, 타이밍 등 모든 공격 파라미터 포함.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackData", menuName = "ProjectName/Combat/Attack Data")]
    public class AttackData : ScriptableObject
    {
        [Header("Identity")]
        public string attackName;
        public int attackID;

        [Header("Combo")]
        public int comboIndex = 0;              // 0 = 스타터, 1+ = 연계
        public AttackData nextCombo;            // 다음 콤보 공격
        public float comboWindow = 0.6f;        // 다음 입력 허용 시간(초)
        public bool canChainFromAny = false;    // 다른 공격에서 연계 가능

        [Header("Timing (seconds)")]
        public float startupTime = 0.1f;        // 발동 전 준비
        public float activeTime = 0.2f;         // 판정 활성
        public float recoveryTime = 0.3f;       // 후딜레이
        public float hitPauseDuration = 0.05f;  // 히트퍼즈(타격감)

        [Header("Damage & Force")]
        public float baseDamage = 10f;
        public float knockbackForce = 5f;
        public float knockbackUpward = 2f;
        public float hitStunDuration = 0.3f;

        [Header("Hitbox")]
        public Vector3 hitboxCenter = new Vector3(0, 1f, 1f);  // 손 기준 로컬
        public Vector3 hitboxSize = new Vector3(0.5f, 0.5f, 1f);
        public LayerMask hittableLayers = ~0;
        public int maxHits = 1;                 // 멀티히트용

        [Header("Camera & Effects")]
        public float cameraShakeIntensity = 0.3f;
        public float cameraShakeDuration = 0.15f;
        public GameObject hitEffectPrefab;
        public AudioClip hitSound;

        [Header("Advanced")]
        public bool useRootMotion = false;
        public Vector3 rootMotionVelocity;
        public bool guardBreak = false;
        public float staminaCost = 10f;
    }

    /// <summary>
    /// 런타임 공격 인스턴스 상태.
    /// </summary>
    public class AttackInstance
    {
        public AttackData data;
        public float timer;
        public int hitCount;
        public List<Collider> hitTargets = new List<Collider>();
        public bool isActive => timer > 0 && timer < data.startupTime + data.activeTime;
        public bool inStartup => timer < data.startupTime;
        public bool inActive => timer >= data.startupTime && timer < data.startupTime + data.activeTime;
        public bool inRecovery => timer >= data.startupTime + data.activeTime;
    }
}