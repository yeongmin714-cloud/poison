using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 72-W2] 몬스터 사망 관절 이완 1단계 — 절차 본 회전(물리엔진/러그돌 컴포넌트 미사용 — 절차 구현).
    ///  - 실측 근거: AnimalAI 절차 눕힘(루트 90° 롤 0.5s) 중 사지는 Idle 포즈 그대로 경직 —
    ///    "눕혔는데 다리는 서 있는" 부자연의 뿌리. 본 유틸은 사지 본만 이완 오프셋으로 Slerp한다.
    ///  - 대상: 자식 SkinnedMeshRenderer의 bones 중 이름 패턴 매치(팔=Arm/ForeArm/Hand, 다리=Leg/Shin/Foot/Paw — 대소문자 무시).
    ///  - 동작: 사망 개시 시각 localRotation 캡처 → duration 동안 사지별 고정 이완 오프셋으로 Slerp.
    ///    이징은 FXMotion.Progress 재사용(무번호 28-U3 표준 — 재구현 금지 규약). 결정론 — 난수 경로 0건.
    ///  - 폴백: 사지 본 0건(명명 미매치) = 조용 스킵 — 기존 절차 눕힘만(현행 무손상). EditMode 무동작 가드.
    ///  - 수명: 유한 수명 러너 + 예산 가드(FxImpactLight OnDestroy 단일 감산 선례 — 누출 금지).
    ///  - 물리엔진 미사용 — 절차 본 회전만("러그돌 관절 이완 1단계"의 절차 구현, 실제 러그돌 전환은 후속 후보).
    /// </summary>
    public static class FxDeathRelax
    {
        // ================================================================
        // 결정론 상수 — 사지별 고정 이완 오프셋(로컬 X축 오일러). 난수 경로 0건.
        // ================================================================
        /// <summary>[무번호 72-W2] 팔 이완 오프셋 — 40° 드롭(이름에 Arm/Hand 포함 본).</summary>
        public const float ArmDropDegrees = 40f;
        /// <summary>[무번호 72-W2] 다리 이완 오프셋 — 20° 굽힘(이름에 Leg/Shin/Foot/Paw 포함 본).</summary>
        public const float LegBendDegrees = 20f;

        /// <summary>[무번호 72-W2] 동시 이완 러너 예산 — 초과 조용 스킵(33-A1 FxImpactLight 예산 선례).</summary>
        public const int MaxConcurrentRelaxes = 8;
        private static int _activeCount;

        /// <summary>
        /// [무번호 72-W2] 사망 관절 이완 개시 — target의 사지 본을 duration 동안 이완 오프셋으로 Slerp.
        /// 호출부는 FX 격리 원칙(사망 후처리 방해 금지)에 따라 try/catch로 감싸야 한다(46-T2 원칙).
        /// 본 0건/무효 인자/예산 초과/편집 모드 = 조용 스킵(기존 절차 눕힘만 — 현행 무손상).
        /// </summary>
        public static void Begin(GameObject target, float duration)
        {
            if (!Application.isPlaying) return;                       // EditMode 무동작 가드
            if (target == null || duration <= 0f) return;             // 무효 인자 조용 스킵
            if (_activeCount >= MaxConcurrentRelaxes) return;         // 예산 가드 — 초과 조용 스킵(선례 승계)

            Transform[] bones;
            Quaternion[] captured;
            Quaternion[] relaxed;
            if (!CollectLimbBones(target, out bones, out captured, out relaxed)) return;   // 본 0건 = 조용 스킵

            try
            {
                var go = new GameObject("FxDeathRelax");
                var runner = go.AddComponent<RelaxRunner>();
                runner.Init(bones, captured, relaxed, duration);
                _activeCount++;
            }
            catch (System.Exception) { }   // 시각 보조 — 사망 흐름 절대 방해 금지(46-T2 원칙)
        }

        private static void Release()
        {
            _activeCount = Mathf.Max(0, _activeCount - 1);
        }

        // ================================================================
        // 사지 본 선별 — SkinnedMeshRenderer.bones 중 이름 패턴 매치(대소문자 무시).
        // 다중 SMR 공유 본은 디듀프. 사지 외 본(척추/머리/꼬리 등)은 미편입.
        // ================================================================
        private static bool CollectLimbBones(GameObject target, out Transform[] bones, out Quaternion[] captured, out Quaternion[] relaxed)
        {
            bones = null;
            captured = null;
            relaxed = null;

            var smrs = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs == null || smrs.Length == 0) return false;

            var seen = new HashSet<Transform>();               // 다중 SMR 공유 본 디듀프
            var boneList = new List<Transform>();
            var capList = new List<Quaternion>();
            var relList = new List<Quaternion>();

            foreach (var smr in smrs)
            {
                if (smr == null || smr.bones == null) continue;
                foreach (var b in smr.bones)
                {
                    if (b == null || !seen.Add(b)) continue;
                    // 팔 = Arm/ForeArm/Hand, 다리 = Leg/Shin/Foot/Paw — 대소문자 무시 이름 매치.
                    // "ForeArm"은 "arm" 부분 일치로 팔에 편입된다(우선 판정 — 이중 편입 방지).
                    bool isArm = NameMatches(b.name, "arm") || NameMatches(b.name, "hand");
                    bool isLeg = !isArm && (NameMatches(b.name, "leg") || NameMatches(b.name, "shin")
                                         || NameMatches(b.name, "foot") || NameMatches(b.name, "paw"));
                    if (!isArm && !isLeg) continue;

                    boneList.Add(b);
                    capList.Add(b.localRotation);              // 사망 개시 시각 포즈 캡처
                    relList.Add(isArm
                        ? b.localRotation * Quaternion.Euler(ArmDropDegrees, 0f, 0f)   // 팔 40° 드롭
                        : b.localRotation * Quaternion.Euler(LegBendDegrees, 0f, 0f)); // 다리 20° 굽힘
                }
            }

            if (boneList.Count == 0) return false;             // 사지 본 0건 = 조용 스킵(폴백 = 기존 절차 눕힘만)
            bones = boneList.ToArray();
            captured = capList.ToArray();
            relaxed = relList.ToArray();
            return true;
        }

        /// <summary>대소문자 무시 부분 일치(본 명명은 대소문자 혼용 — GLB 소스별 상이).</summary>
        private static bool NameMatches(string boneName, string token)
            => !string.IsNullOrEmpty(boneName) && boneName.ToLowerInvariant().Contains(token);

        // ================================================================
        // 이완 러너 — duration 동안 캡처 포즈 → 이완 포즈 Slerp(FXMotion.Progress 이징) → 자가 파괴.
        // 소멸 카운트는 OnDestroy 단일 경로(FxImpactLight 선례 — 이중 감산 방지). 유한 수명 — 누출 금지.
        // ================================================================
        private sealed class RelaxRunner : MonoBehaviour
        {
            private Transform[] _bones;
            private Quaternion[] _captured;
            private Quaternion[] _relaxed;
            private float _duration;
            private float _t;

            public void Init(Transform[] bones, Quaternion[] captured, Quaternion[] relaxed, float duration)
            {
                _bones = bones;
                _captured = captured;
                _relaxed = relaxed;
                _duration = Mathf.Max(0.05f, duration);
                _t = 0f;
            }

            private void Update()
            {
                _t += Time.deltaTime;
                float p = FXMotion.Progress(_t / _duration);   // [무번호 28-U3] 표준 이징 재사용(재구현 금지 규약)
                for (int i = 0; i < _bones.Length; i++)
                {
                    if (_bones[i] == null) continue;           // 시체 정리로 본 소멸 시 해당 본만 스킵
                    _bones[i].localRotation = Quaternion.Slerp(_captured[i], _relaxed[i], p);
                }
                if (_t >= _duration) Destroy(gameObject);      // 유한 수명 — p=1 최종 포즈 적용 후 자가 파괴
            }

            private void OnDestroy()
            {
                FxDeathRelax.Release();   // 유한 수명 종료/조기 소멸 공용 — 카운트 정합(단일 감산 경로)
            }
        }
    }
}
