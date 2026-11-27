using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 75] 피격 본(상체) 플린치 승격 — transform 전체 젖힘을 상체 척추 본 단위 반응으로 승격.
    ///  - 배경: HitReactionDriver.Apply(Phase C)는 시각 모델(자식) 트랜스폼 전체를 젖힘 — 전신이 판처럼 기움.
    ///    리깅상 피격은 상체 본(Spine 체인)만 꺾이고 하체는 지면에 붙어 있어야 자연스럽다(젤다/엘더스크롤 기준).
    ///  - 본 탐색: 자식 SkinnedMeshRenderer의 bones 중 상체 패턴 매치(Spine/Spine1/Spine2/SpineChest/Chest/Torso —
    ///    대소문자 무시, FxDeathRelax 본 패턴 매칭 선례). Hips(골반/하체 루트)는 제외 리스트 — 스킵.
    ///  - 반응: 맞은 방향 반대로 상체 후방 기움 — 수평 법선 축(Vector3.Cross(Vector3.up, -dir) 패턴 —
    ///    FxSwayRunner/FxDeathRelax 선례) 기준 severity 각도(Light 6°/Heavy 10°/Crit 15°, 0.28s 공통)로 Slerp 후 원복.
    ///  - 체인 배분: 매치된 척추 체인 본이 N개면 각 본에 루트 각도의 1/N 균등 배분(결정론) — 전체 각도 합 = 루트 각도.
    ///  - 이징: FXMotion.Progress 재사용(무번호 28-U3 표준 — 재구현 금지 규약). 로컬 회전 캡처→Slerp→원복,
    ///    OnDestroy 원복 보장(FxSwayRunner 패턴).
    ///  - 러너: target에 AddComponent(중복 방지 — 기존 러너 존재 시 재발화 갱신), 유한 수명 자가 파괴.
    ///  - 결정론: 난수 0건 / 물리엔진(러그돌) 0건 — 절차 본 회전. EditMode 무동작 가드(코루틴·AddComponent 발화 없음 —
    ///    Trigger 자체는 소스 계약 검증 대상).
    ///  - 통합 계약: 본 매치 성공(true) = driver의 transform lean 스킵(대체 — 이중 기움 방지),
    ///    본 0건(false, GLB 비리그 등) = 기존 transform lean 그대로(폴백 — 현행 무손상).
    /// </summary>
    public static class FxHitFlinchBone
    {
        // ================================================================
        // 결정론 상수 — severity 3단계 루트 각도(카이드 루트 본 기준) + 공통 duration. 난수 경로 0건.
        // ================================================================
        /// <summary>[무번호 75] Light 플린치 루트 각도(°).</summary>
        public const float LightFlinchDeg = 6f;
        /// <summary>[무번호 75] Heavy 플린치 루트 각도(°).</summary>
        public const float HeavyFlinchDeg = 10f;
        /// <summary>[무번호 75] Crit 플린치 루트 각도(°).</summary>
        public const float CritFlinchDeg = 15f;
        /// <summary>[무번호 75] 플린치 지속시간(초) — severity 무관 공통.</summary>
        public const float FlinchDurationSec = 0.28f;

        /// <summary>
        /// [무번호 75] 피격 본 플린치 개시 — target의 상체 척추 본을 FlinchDurationSec 동안 기움 후 원복.
        /// 반환: 본 매치 성공 true(driver가 transform lean 스킵), 본 0건/무효 인자/EditMode false(기존 lean 폴백).
        /// </summary>
        public static bool Trigger(GameObject target, Vector3 hitDirection, HitSeverity severity)
        {
            if (!Application.isPlaying) return false;      // EditMode 무동작 가드 — 소스 계약 검증 전용
            if (target == null) return false;

            Transform[] bones;
            if (!CollectSpineBones(target, out bones)) return false;   // 본 0건 = false(폴백 — GLB 비리그 등)

            Vector3 dir = hitDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = -target.transform.forward;
            dir.Normalize();

            // 맞은 방향 반대로 상체 기움 — 수평 법선 축(FxSwayRunner Cross(up, dir) 선례의 반대방향 패턴).
            Vector3 axis = Vector3.Cross(Vector3.up, -dir).normalized;
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;

            float rootDeg = severity == HitSeverity.Crit ? CritFlinchDeg
                          : (severity == HitSeverity.Heavy ? HeavyFlinchDeg : LightFlinchDeg);

            var runner = target.GetComponent<FxHitFlinchBoneRunner>();
            if (runner == null) runner = target.AddComponent<FxHitFlinchBoneRunner>();   // 중복 방지 — 재발화는 갱신
            runner.Play(bones, axis, rootDeg, FlinchDurationSec);
            return true;
        }

        // ================================================================
        // 상체 본 선별 — SkinnedMeshRenderer.bones 중 이름 패턴 매치(대소문자 무시).
        // 다중 SMR 공유 본은 디듀프(FxDeathRelax 선례). Hips는 제외 리스트(하체 루트 — 스킵).
        // ================================================================
        private static bool CollectSpineBones(GameObject target, out Transform[] bones)
        {
            bones = null;
            var smrs = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs == null || smrs.Length == 0) return false;

            var seen = new HashSet<Transform>();               // 다중 SMR 공유 본 디듀프
            var list = new List<Transform>();
            foreach (var smr in smrs)
            {
                if (smr == null || smr.bones == null) continue;
                foreach (var b in smr.bones)
                {
                    if (b == null || !seen.Add(b)) continue;
                    string boneName = b.name;
                    // Hips 제외 리스트 — 골반(하체 루트)은 스킵(상체 체인만 편입, Hips/Spine 구분).
                    if (NameMatches(boneName, "hips")) continue;
                    // 상체 척추 체인 — Spine/Spine1/Spine2/SpineChest/Chest/Torso(포함 판정 — "Spine" 포함 여부).
                    if (NameMatches(boneName, "spine") || NameMatches(boneName, "chest") || NameMatches(boneName, "torso"))
                        list.Add(b);
                }
            }
            if (list.Count == 0) return false;                 // 본 0건 = false(폴백 — 기존 transform lean)
            bones = list.ToArray();
            return true;
        }

        /// <summary>대소문자 무시 부분 일치(본 명명은 대소문자 혼용 — GLB 소스별 상이, FxDeathRelax 선례).</summary>
        private static bool NameMatches(string boneName, string token)
            => !string.IsNullOrEmpty(boneName) && boneName.ToLowerInvariant().Contains(token);
    }

    /// <summary>
    /// [무번호 75] 피격 본 플린치 러너 — 상체 척추 본 로컬 회전 캡처 → 루트 각도의 1/N 체인 배분 Slerp → 원복.
    /// 이징은 FXMotion.Progress 재사용(28-U3 재구현 금지 규약). 유한 수명 — 원복 후 컴포넌트 자가 파괴,
    /// OnDestroy 원복 보장(FxSwayRunner 패턴 — 조기 파괴/씬 전환 대응). 재발화는 base 유지 갱신(연타 대응).
    /// </summary>
    public class FxHitFlinchBoneRunner : MonoBehaviour
    {
        private Transform[] _bones;
        private Quaternion[] _base;      // 최초 발화 시점 휴식 포즈 — 모든 원복의 기준(재발화에도 불변)
        private Quaternion[] _target;    // base * AngleAxis(1/N 배분 각도, 로컬 축)
        private float _duration;
        private float _t;
        private bool _ready;

        /// <summary>플린치 시작 — 기존 러너 존재 시(재발화) base 유지한 채 갱신(연타 대응).</summary>
        public void Play(Transform[] bones, Vector3 axisWorld, float rootAngleDeg, float duration)
        {
            if (bones == null || bones.Length == 0) return;
            if (!_ready || _bones == null || _bones.Length != bones.Length)
            {
                if (_ready && _bones != null) RestoreAll();   // 리깅 변경(본 소멸) 재캡처 전 기존 원복 — 포즈 드리프트 방지
                _bones = bones;
                _base = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    _base[i] = _bones[i] != null ? _bones[i].localRotation : Quaternion.identity;   // 로컬 회전 캡처
                _ready = true;
            }

            // 체인 균등 배분 — 각 본 루트 각도의 1/N(결정론), 전체 각도 합 = 루트 각도.
            float shareDeg = rootAngleDeg / bones.Length;
            _target = new Quaternion[bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                var b = _bones[i];
                if (b == null) { _target[i] = _base[i]; continue; }
                // 수평 법선 축(axisWorld)을 base 포즈 기준 본 로컬 축으로 환산 — 적용 증분의 월드 축 = axisWorld 보장.
                Quaternion baseWorld = b.parent != null ? b.parent.rotation * _base[i] : _base[i];
                Vector3 localAxis = Quaternion.Inverse(baseWorld) * axisWorld;
                if (localAxis.sqrMagnitude < 0.0001f) localAxis = Vector3.right;
                _target[i] = _base[i] * Quaternion.AngleAxis(shareDeg, localAxis.normalized);
            }
            _duration = Mathf.Max(0.05f, duration);
            _t = 0f;
        }

        private void Update()
        {
            if (!_ready) { Destroy(this); return; }
            _t += Time.deltaTime;
            float p = FXMotion.Progress(_t / _duration);   // [무번호 28-U3] 표준 이징 재사용(재구현 금지 규약)
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;           // 사망 정리로 본 소멸 시 해당 본만 스킵
                _bones[i].localRotation = Quaternion.Slerp(_base[i], _target[i], p);
            }
            if (_t >= _duration)
            {
                RestoreAll();      // 종료 원복 보장
                Destroy(this);     // 유한 수명 — 컴포넌트 자가 파괴(누출 금지)
            }
        }

        private void RestoreAll()
        {
            if (_bones == null || _base == null) return;
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i] != null) _bones[i].localRotation = _base[i];
        }

        private void OnDestroy()
        {
            RestoreAll();          // 조기 파괴(사망·씬 전환 등) 원복 보장(FxSwayRunner 패턴)
        }
    }
}
