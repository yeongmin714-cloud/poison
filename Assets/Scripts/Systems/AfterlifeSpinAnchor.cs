using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [208차] 회전 참격 앵커 — s_spin_slash 궤도 접촉피해(피해원 단일 계약 — 즉발 DamageRadius 대체).
    /// - 발화(8s 쿨) 시 EnsureRunning으로 앵커 1기 유지(기존 앵커 재사용 — 단일 인스턴스)
    /// - 루트 레벨 GO(플레이어 부모 금지 — 스케일/회전 상속 안전), 위치=플레이어+궤도 오프셋(cos/sin×OrbitRadius)
    /// - 각도 진행 = unscaled delta(스킬 시계 일관 — AfterlifeSkillSystem.UnscaledTimeOverride 선례)
    /// - 접촉피해: AliveEnemies 역순(결정론 — 물리 쿼리 0) + 개별 적 쿨다운(HitCooldown)
    ///   판정은 internal static SweepDamage 헬퍼 분리 — EditMode 직접판정(InternalsVisibleTo 선례)
    /// - 정지 게이트: 런 비활성/3택 대기(IsPending) → 회전·피해 스킵(아레나 정지 계약)
    /// - VFX: Resources FX/Afterlife/TrailsVfx/VFX_Trail_Dark(P1 착지 — TrailRenderer 3겹) 부재 시 무음 폴백+1회 경고
    /// </summary>
    public class AfterlifeSpinAnchor : MonoBehaviour
    {
        // ===== [208차] Play 튜닝 대기 상수 =====
        public const float OrbitRadius = 3.5f;  // 궤도 반경 — 기존 참격 반경 계승
        public const float OrbitPeriod = 2.0f;  // 1회전 시간(초)
        public const float HitRadius = 0.9f;    // 접촉 판정 반경(평면거리)
        public const float HitDamage = 15f;     // 접촉 피해 — 기존 s_spin_slash 단발 15 계승
        public const float HitCooldown = 2.0f;  // 개별 적 재타격 쿨(초)
        public const float HeightOffset = 0.6f; // 플레이어 기준 부유 높이

        /// <summary>시간 주입 — AfterlifeSkillSystem.UnscaledTimeOverride 선례(테스트 finally에서 null 복원).</summary>
        internal static System.Func<float> TimeOverride { get; set; }
        private static float Now => TimeOverride != null ? TimeOverride() : Time.unscaledTime;

        private const string TrailVfxResourcePath = "FX/Afterlife/TrailsVfx/VFX_Trail_Dark"; // [P1 착지] 경로 계약

        private static AfterlifeSpinAnchor _live; // 단일 인스턴스 — 기존 앵커 재사용
        private static bool _vfxWarned;           // 경고 1회 세션 가드(앵커 재스폰 로그 스팸 방지)

        private Transform _player;
        private float _angle; // 라디안 누적 — unscaled delta 기반(스킬 시계 일관)
        private float _lastNow;
        private readonly Dictionary<AfterlifeEnemy, float> _cooldowns = new Dictionary<AfterlifeEnemy, float>();

        // ===== 스폰/수명 =====

        /// <summary>발화 시 호출 — isPlaying에서만 스폰(편집 모드는 SweepDamage 직접판정), 루트 레벨 GO(플레이어 부모 금지).</summary>
        public static void EnsureRunning(Transform player)
        {
            if (!Application.isPlaying) return; // 편집 모드 스폰 금지 — Destroy 에디터 에러 유발 함정(126차)
            if (_live != null)
            {
                if (player != null) _live._player = player; // 기존 앵커 재사용 — 플레이어 참조 갱신만
                return;
            }
            var go = new GameObject("AfterlifeSpinAnchor"); // 루트 레벨 — 부모 미지정(스케일/회전 상속 차단)
            _live = go.AddComponent<AfterlifeSpinAnchor>();
            _live._player = player;
            _live._lastNow = Now;
            _live.EnsureVfx();
        }

        /// <summary>런 경계 전멸 — AfterlifeSkillSystem.ResetState에서 호출(BeginRun/EndRun 3곳 경유).</summary>
        public static void DestroyAll()
        {
            if (_live == null) return;
            if (Application.isPlaying) Destroy(_live.gameObject);
            else DestroyImmediate(_live.gameObject); // 편집 모드 게이트 — AfterlifeOrb.ClearAll 선례
            _live = null;
        }

        private void EnsureVfx()
        {
            var prefab = Resources.Load<GameObject>(TrailVfxResourcePath);
            if (prefab == null)
            {
                if (!_vfxWarned)
                {
                    _vfxWarned = true;
                    Debug.LogWarning($"[AfterlifeSpinAnchor] 트레일 VFX 부재 — 무음 폴백({TrailVfxResourcePath})");
                }
                return;
            }
            var child = Instantiate(prefab, transform, false); // worldPositionStays=false — 앵커 로컬 원점 부착
            child.name = "SpinTrailVfx";
        }

        // ===== 프레임 드라이버 =====

        private void Update()
        {
            if (this != _live) return; // 경합 가드 — 구 앵커 잔존 Update 무시
            if (_player == null) { DestroyAll(); return; } // 플레이어 소실 — 자가 파괴(안전)

            float now = Now;
            float dt = Mathf.Max(0f, now - _lastNow);
            _lastNow = now; // 정지 중에도 시계 동기 — 해동 후 시간 점프(회전 월프) 방지

            // 정지 게이트 — 런 비활성/3택 대기: 회전·피해 스킵(아레나 정지 계약)
            if (!AfterlifeSurvivalSystem.IsActive
                || AfterlifeAugmentSystem.IsPending
                || AfterlifeCardSystem.IsPending)
                return;

            _angle += dt * (Mathf.PI * 2f) / OrbitPeriod;
            Vector3 p = _player.position;
            transform.position = new Vector3(
                p.x + Mathf.Cos(_angle) * OrbitRadius,
                p.y + HeightOffset,
                p.z + Mathf.Sin(_angle) * OrbitRadius);

            SweepDamage(transform.position, now, _cooldowns);
        }

        // ===== 접촉 판정 (결정론 — 물리 쿼리 0) =====

        /// <summary>
        /// 앵커 위치 기준 접촉피해 — 컴포넌트 무관 순수 판정(EditMode 직접 호출).
        /// AliveEnemies 역순(Die 중 리스트 수정 대응 — DamageRadius 선례) + 개별 적 쿨다운.
        /// </summary>
        internal static int SweepDamage(Vector3 anchorPos, float now, Dictionary<AfterlifeEnemy, float> cooldowns)
        {
            int hits = 0;
            var enemies = AfterlifeSurvivalSystem.AliveEnemies;
            float r2 = HitRadius * HitRadius;
            for (int i = enemies.Count - 1; i >= 0; i--) // 역순 — Die 중 리스트 수정 대응
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;
                float nextHit;
                if (cooldowns.TryGetValue(e, out nextHit) && now < nextHit) continue; // 개별 적 쿨다운
                Vector3 flat = e.transform.position - anchorPos;
                flat.y = 0f; // 평면거리 판정
                if (flat.sqrMagnitude > r2) continue;
                cooldowns[e] = now + HitCooldown;
                e.TakeDamage(HitDamage, flat.normalized, "afterlife_skill"); // 소스 계약 — 기존 스킬 피해원 동일
                hits++;
            }
            return hits;
        }
    }
}
