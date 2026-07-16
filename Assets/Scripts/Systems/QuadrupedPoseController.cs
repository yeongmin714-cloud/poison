using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 4족보행 몬스터(Wolf/Boar/Deer 등) 전용 프로시저럴 포즈 컨트롤러.
    /// 본 이름이 넘버링(bone_0~bone_25)인 모델에서 위치/계층 기반으로 본을 추론하여
    /// 애니메이션 클립 없이 순수 코드로 보행 동작을 합성합니다.
    /// 2족용 ProceduralPoseController와 독립적으로 동작합니다.
    /// </summary>
    public class QuadrupedPoseController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Public tuning parameters (SerializeField)
        // ──────────────────────────────────────────────

        [Header("Gait Settings")]
        [SerializeField, Range(0f, 5f)] private float _gaitFrequency = 2.5f;
        [SerializeField, Range(0f, 30f)] private float _gaitAmplitude = 15f;
        [SerializeField, Range(0f, 30f)] private float _legSwingAmount = 10f;
        [SerializeField, Range(0f, 0.5f)] private float _spineBobAmount = 0.05f;
        [SerializeField, Range(0f, 3f)] private float _stepPhaseOffset = 0.5f;

        [Header("Speed Threshold")]
        [SerializeField, Range(0f, 5f)] private float _speedThreshold = 1.5f;

        // ──────────────────────────────────────────────
        //  Private fields
        // ──────────────────────────────────────────────

        private Animator _animator;
        private Transform _modelRoot;
        private Vector3 _lastPosition;
        private float _currentSpeed;
        private float _prevY;
        private float _verticalVelocity;
        private bool _initialized;

        // Leg chain data
        private LegChain _legFL; // Front-Left
        private LegChain _legFR; // Front-Right
        private LegChain _legHL; // Hind-Left
        private LegChain _legHR; // Hind-Right

        // Spine chain (for body bob)
        private Transform[] _spineChain;

        // Initial rotation cache (to prevent accumulation)
        private Quaternion[][] _initialLegRotations;
        private Quaternion[] _initialSpineRotations;

        // ──────────────────────────────────────────────
        //  Structs
        // ──────────────────────────────────────────────

        private struct LegChain
        {
            public Transform UpperLeg;
            public Transform LowerLeg;
            public Transform Paw;
            public string Label;
            public float PhaseOffset;
        }

        // ──────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            try
            {
                InitializeBones();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[QuadrupedPoseController] Bone initialization failed: {ex.Message}");
                enabled = false;
            }
        }

        private void Start()
        {
            if (_initialized)
            {
                _lastPosition = transform.position;
                _prevY = transform.position.y;
            }
        }

        private void Update()
        {
            if (!_initialized) return;

            try
            {
                UpdateSpeed();
                UpdateVerticalVelocity();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[QuadrupedPoseController] Update error: {ex.Message}");
            }
        }

        private void LateUpdate()
        {
            if (!_initialized) return;

            try
            {
                // Safety: Animator가 실제 클립 재생 중이면 보행 합성 스킵
                if (ShouldSkipGaitSynthesis())
                {
                    RestoreInitialPose();
                    return;
                }

                // 걸음 속도가 너무 느리면 스킵 (정지 상태)
                if (_currentSpeed < 0.01f)
                {
                    RestoreInitialPose();
                    return;
                }

                SynthesizeGait();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[QuadrupedPoseController] LateUpdate error: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        //  Bone Discovery (Awake)
        // ──────────────────────────────────────────────

        private void InitializeBones()
        {
            // Step 1: Find model root
            _animator = GetComponentInChildren<Animator>();
            _modelRoot = _animator != null ? _animator.transform : transform;

            // Step 2: Find Root bone (bone_0 etc.)
            Transform rootBone = FindRootBone();
            if (rootBone == null)
            {
                Debug.LogWarning("[QuadrupedPoseController] Root bone not found. Disabling.");
                enabled = false;
                return;
            }

            // Step 3: Get all SkinnedMeshRenderer bone sets for validation
            SkinnedMeshRenderer[] smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs.Length == 0)
            {
                Debug.LogWarning("[QuadrupedPoseController] No SkinnedMeshRenderer found. Disabling.");
                enabled = false;
                return;
            }

            // Collect valid bone set from SMRs
            System.Collections.Generic.HashSet<Transform> smrBoneSet = new System.Collections.Generic.HashSet<Transform>();
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                foreach (Transform bone in smr.bones)
                {
                    if (bone != null) smrBoneSet.Add(bone);
                }
            }

            // Step 4: Find leg chains from Root's direct children
            System.Collections.Generic.List<LegCandidate> legCandidates = FindLegCandidates(rootBone, smrBoneSet);
            if (legCandidates.Count < 4)
            {
                Debug.LogWarning($"[QuadrupedPoseController] Found {legCandidates.Count} leg chains, need 4. Disabling.");
                enabled = false;
                return;
            }

            // Step 5: Classify legs by position (FL/FR/HL/HR)
            ClassifyAndAssignLegs(legCandidates);

            // Step 6: Cache initial rotations
            CacheInitialRotations();

            // Step 7: Find spine chain (longest non-leg chain from root)
            _spineChain = FindSpineChain(rootBone, legCandidates, smrBoneSet);
            if (_spineChain != null && _spineChain.Length > 0)
            {
                _initialSpineRotations = new Quaternion[_spineChain.Length];
                for (int i = 0; i < _spineChain.Length; i++)
                {
                    _initialSpineRotations[i] = _spineChain[i].localRotation;
                }
            }

            _initialized = true;
            Debug.Log($"[QuadrupedPoseController] Initialized. Root={rootBone.name}, " +
                      $"FL={_legFL.UpperLeg?.name}, FR={_legFR.UpperLeg?.name}, " +
                      $"HL={_legHL.UpperLeg?.name}, HR={_legHR.UpperLeg?.name}, " +
                      $"Spine bones={(_spineChain != null ? _spineChain.Length : 0)}");
        }

        /// <summary>
        /// Root 본 찾기: 계층 최상위 본 중 로컬Y가 가장 낮고 자식이 많은 본을 Root로推定.
        /// </summary>
        private Transform FindRootBone()
        {
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            Transform bestRoot = null;
            float lowestY = float.MaxValue;
            int maxChildren = 0;

            // 1) modelRoot 바로 아래 자식 중 본 역할(자식 있음)인 것
            foreach (Transform t in allTransforms)
            {
                if (t == _modelRoot) continue;
                if (t.parent != _modelRoot) continue;
                if (t.childCount == 0) continue;

                float localY = t.localPosition.y;
                if (localY < lowestY - 0.001f || (Mathf.Abs(localY - lowestY) < 0.001f && t.childCount > maxChildren))
                {
                    lowestY = localY;
                    maxChildren = t.childCount;
                    bestRoot = t;
                }
            }

            // 2) 없으면 SkeletonBindArmature / Armature 컨테이너의 자식 중 탐색
            if (bestRoot == null)
            {
                foreach (Transform t in allTransforms)
                {
                    if (t == _modelRoot || t.parent == null) continue;
                    if (t.childCount == 0) continue;

                    string parentName = t.parent.name.ToLowerInvariant();
                    if (parentName.Contains("skeleton") || parentName.Contains("armature") || parentName.Contains("bind"))
                    {
                        float localY = t.localPosition.y;
                        if (localY < lowestY || (Mathf.Approximately(localY, lowestY) && t.childCount > maxChildren))
                        {
                            lowestY = localY;
                            maxChildren = t.childCount;
                            bestRoot = t;
                        }
                    }
                }
            }

            // 3) 최후의 수단: 자식이 가장 많은 본
            if (bestRoot == null)
            {
                foreach (Transform t in allTransforms)
                {
                    if (t == _modelRoot || t.parent == null) continue;
                    if (t.childCount > maxChildren)
                    {
                        maxChildren = t.childCount;
                        bestRoot = t;
                    }
                }
            }

            return bestRoot;
        }

        /// <summary>
        /// Root 본의 직계 자식 중 SMR 본 리스트에 포함되고 깊이 3 이상 체인을 가진 것들을 찾는다.
        /// </summary>
        private System.Collections.Generic.List<LegCandidate> FindLegCandidates(
            Transform rootBone, System.Collections.Generic.HashSet<Transform> smrBoneSet)
        {
            var candidates = new System.Collections.Generic.List<LegCandidate>();

            foreach (Transform child in rootBone)
            {
                if (child == null) continue;
                if (!smrBoneSet.Contains(child)) continue;

                // 깊이 3 이상 체인 탐색
                System.Collections.Generic.List<Transform> chain = GetChainDown(child, smrBoneSet);
                if (chain.Count >= 3) // 허벅지 + 종아리 + 발
                {
                    candidates.Add(new LegCandidate
                    {
                        Root = child,
                        Chain = chain,
                        WorldPosition = child.position
                    });
                }
            }

            return candidates;
        }

        /// <summary>
        /// 시작 본부터 SMR 본만 따라가며 체인을 구성한다.
        /// </summary>
        private System.Collections.Generic.List<Transform> GetChainDown(
            Transform start, System.Collections.Generic.HashSet<Transform> validBones)
        {
            var chain = new System.Collections.Generic.List<Transform>();
            Transform current = start;
            int safety = 20;

            while (current != null && safety > 0)
            {
                chain.Add(current);
                safety--;

                // 첫 번째 valid 자식으로 이동
                Transform next = null;
                foreach (Transform child in current)
                {
                    if (validBones.Contains(child))
                    {
                        next = child;
                        break;
                    }
                }
                current = next;
            }

            return chain;
        }

        /// <summary>
        /// 4개 다리 위치(x,z) 기준 분류:
        /// - z값(모델 로컬)이 양수 → 앞다리(FL/FR)
        /// - z값이 음수 → 뒷다리(HL/HR)
        /// - x값 부호로 L/R 결정
        /// </summary>
        private void ClassifyAndAssignLegs(System.Collections.Generic.List<LegCandidate> candidates)
        {
            // 남은 후보가 4개보다 많으면 가장 긴 체인 4개만 선택
            while (candidates.Count > 4)
            {
                // 가장 짧은 체인 제거
                int shortestIdx = 0;
                int shortestLen = candidates[0].Chain.Count;
                for (int i = 1; i < candidates.Count; i++)
                {
                    if (candidates[i].Chain.Count < shortestLen)
                    {
                        shortestLen = candidates[i].Chain.Count;
                        shortestIdx = i;
                    }
                }
                candidates.RemoveAt(shortestIdx);
            }

            // 모델 로컬 좌표계로 변환 후 분류
            foreach (LegCandidate c in candidates)
            {
                c.LocalPosition = _modelRoot.InverseTransformPoint(c.WorldPosition);
            }

            // z값 기준 앞/뒤 분류
            float zThreshold = 0f;
            foreach (LegCandidate c in candidates)
            {
                zThreshold += c.LocalPosition.z;
            }
            zThreshold /= candidates.Count; // 중앙값 기준

            bool usePositiveZ = true; // z가 양수면 앞
            if (zThreshold < 0)
            {
                // 대부분이 음수면 음수=앞
                usePositiveZ = false;
            }

            foreach (LegCandidate c in candidates)
            {
                bool isFront;
                if (usePositiveZ)
                    isFront = c.LocalPosition.z >= 0;
                else
                    isFront = c.LocalPosition.z < 0;

                bool isRight = c.LocalPosition.x >= 0;
                c.IsFront = isFront;
                c.IsRight = isRight;
            }

            // FL (Front-Left):  phase 0
            // HR (Hind-Right): phase 0
            // FR (Front-Right): phase π
            // HL (Hind-Left):  phase π
            // (대각 보행 — trot)
            foreach (LegCandidate c in candidates)
            {
                float phase;
                string label;

                if (c.IsFront && !c.IsRight)
                {
                    label = "FL"; phase = 0f;
                    _legFL = MakeLegChain(c, label, phase);
                }
                else if (c.IsFront && c.IsRight)
                {
                    label = "FR"; phase = Mathf.PI;
                    _legFR = MakeLegChain(c, label, phase);
                }
                else if (!c.IsFront && !c.IsRight)
                {
                    label = "HL"; phase = Mathf.PI;
                    _legHL = MakeLegChain(c, label, phase);
                }
                else
                {
                    label = "HR"; phase = 0f;
                    _legHR = MakeLegChain(c, label, phase);
                }
            }
        }

        private LegChain MakeLegChain(LegCandidate c, string label, float phase)
        {
            LegChain chain = new LegChain
            {
                UpperLeg = c.Chain.Count > 0 ? c.Chain[0] : null,
                LowerLeg = c.Chain.Count > 1 ? c.Chain[1] : null,
                Paw = c.Chain.Count > 0 ? c.Chain[c.Chain.Count - 1] : null,
                Label = label,
                PhaseOffset = phase
            };
            return chain;
        }

        private void CacheInitialRotations()
        {
            LegChain[] legs = new LegChain[] { _legFL, _legFR, _legHL, _legHR };
            _initialLegRotations = new Quaternion[4][];

            for (int i = 0; i < 4; i++)
            {
                int count = 0;
                if (legs[i].UpperLeg != null) count++;
                if (legs[i].LowerLeg != null) count++;
                if (legs[i].Paw != null && legs[i].Paw != legs[i].LowerLeg) count++;

                _initialLegRotations[i] = new Quaternion[count];
                int idx = 0;
                if (legs[i].UpperLeg != null)
                    _initialLegRotations[i][idx++] = legs[i].UpperLeg.localRotation;
                if (legs[i].LowerLeg != null)
                    _initialLegRotations[i][idx++] = legs[i].LowerLeg.localRotation;
                if (legs[i].Paw != null && legs[i].Paw != legs[i].LowerLeg && idx < count)
                    _initialLegRotations[i][idx] = legs[i].Paw.localRotation;
            }
        }

        /// <summary>
        /// 척추: Root에서 가장 긴 자식 체인(다리 아닌 쪽)을 spine 체인으로 캐시.
        /// </summary>
        private Transform[] FindSpineChain(
            Transform rootBone,
            System.Collections.Generic.List<LegCandidate> legCandidates,
            System.Collections.Generic.HashSet<Transform> smrBoneSet)
        {
            // Collect leg root transforms
            System.Collections.Generic.HashSet<Transform> legRoots = new System.Collections.Generic.HashSet<Transform>();
            foreach (LegCandidate leg in legCandidates)
            {
                legRoots.Add(leg.Root);
            }

            // Root의 직계 자식 중 다리가 아닌 것 중 가장 긴 체인
            Transform bestSpineStart = null;
            int maxDepth = 0;

            foreach (Transform child in rootBone)
            {
                if (child == null || legRoots.Contains(child)) continue;
                if (!smrBoneSet.Contains(child)) continue;

                System.Collections.Generic.List<Transform> chain = GetChainDown(child, smrBoneSet);
                if (chain.Count > maxDepth)
                {
                    maxDepth = chain.Count;
                    bestSpineStart = child;
                }
            }

            if (bestSpineStart == null)
                return null;

            System.Collections.Generic.List<Transform> spineChain = GetChainDown(bestSpineStart, smrBoneSet);
            return spineChain.ToArray();
        }

        // ──────────────────────────────────────────────
        //  Speed & Detection
        // ──────────────────────────────────────────────

        private void UpdateSpeed()
        {
            // Animator Speed 파라미터 우선
            if (_animator != null && _animator.isActiveAndEnabled && _animator.parameters != null)
            {
                foreach (AnimatorControllerParameter param in _animator.parameters)
                {
                    if (param.name == "Speed" || param.name == "speed")
                    {
                        _currentSpeed = _animator.GetFloat(param.nameHash);
                        return;
                    }
                }
            }

            // Fallback: Transform 이동 속도 기반
            Vector3 delta = transform.position - _lastPosition;
            _currentSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPosition = transform.position;
        }

        private void UpdateVerticalVelocity()
        {
            float currentY = transform.position.y;
            _verticalVelocity = (currentY - _prevY) / Mathf.Max(Time.deltaTime, 0.0001f);
            _prevY = currentY;
        }

        /// <summary>
        /// Animator가 실제 클립을 재생 중이면 보행 합성 스킵.
        /// </summary>
        private bool ShouldSkipGaitSynthesis()
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
                return false;
            if (_animator.runtimeAnimatorController == null)
                return false;
            if (_animator.avatar == null)
                return false;

            // 현재 상태가 비어있지 않으면 클립 재생 중으로 간주
            for (int i = 0; i < _animator.layerCount; i++)
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(i);
                if (stateInfo.length > 0.01f && stateInfo.normalizedTime >= 0f)
                {
                    // 클립 재생 중 → 보정 약하게 (스킵)
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 초기 포즈로 복원 (Animator 클립 재생 시 또는 정지 시).
        /// </summary>
        private void RestoreInitialPose()
        {
            LegChain[] legs = new LegChain[] { _legFL, _legFR, _legHL, _legHR };

            for (int i = 0; i < 4; i++)
            {
                if (_initialLegRotations[i] == null) continue;
                int idx = 0;

                if (legs[i].UpperLeg != null && idx < _initialLegRotations[i].Length)
                {
                    legs[i].UpperLeg.localRotation = _initialLegRotations[i][idx++];
                }
                if (legs[i].LowerLeg != null && idx < _initialLegRotations[i].Length)
                {
                    legs[i].LowerLeg.localRotation = _initialLegRotations[i][idx++];
                }
                if (legs[i].Paw != null && legs[i].Paw != legs[i].LowerLeg && idx < _initialLegRotations[i].Length)
                {
                    legs[i].Paw.localRotation = _initialLegRotations[i][idx];
                }
            }

            if (_spineChain != null && _initialSpineRotations != null)
            {
                for (int i = 0; i < _spineChain.Length && i < _initialSpineRotations.Length; i++)
                {
                    _spineChain[i].localRotation = _initialSpineRotations[i];
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Gait Synthesis (LateUpdate)
        // ──────────────────────────────────────────────

        private void SynthesizeGait()
        {
            float time = Time.time;
            bool isGrounded = Mathf.Abs(_verticalVelocity) < 0.5f;
            float freq = _gaitFrequency * Mathf.Max(0.1f, _currentSpeed);
            float amp = _currentSpeed > _speedThreshold ? _gaitAmplitude * 1.5f : _gaitAmplitude;
            float swing = _currentSpeed > _speedThreshold ? _legSwingAmount * 1.5f : _legSwingAmount;

            ApplyLegGait(time, freq, amp, swing, isGrounded);
            ApplySpineBob(time, freq);
        }

        /// <summary>
        /// 각 다리 관절 각도 = base + sin(time*freq + phase) * amp
        /// FL=0, HR=0, FR=π, HL=π (대각 보행 trot)
        /// 허벅지: X축 회전, 종아리: X축 회전 (반대 위상)
        /// 점프/공중 시 4다리 모두 굽힘
        /// </summary>
        private void ApplyLegGait(float time, float freq, float amp, float swing, bool isGrounded)
        {
            LegChain[] legs = new LegChain[] { _legFL, _legFR, _legHL, _legHR };

            for (int i = 0; i < 4; i++)
            {
                LegChain leg = legs[i];
                if (leg.UpperLeg == null || leg.LowerLeg == null) continue;

                // 사인파 기반 관절 각도
                float sinVal = Mathf.Sin(time * freq + leg.PhaseOffset);
                float thighAngle = sinVal * amp;
                float lowerLegAngle = -sinVal * amp * 0.7f; // 반대 위상, 약간 작은 진폭
                float pawSwing = sinVal * swing * 0.3f;

                // 점프/공중: 4다리 모두 굽힘
                if (!isGrounded)
                {
                    float jumpFactor = Mathf.Clamp01(Mathf.Abs(_verticalVelocity) * 0.5f);
                    thighAngle = Mathf.Lerp(thighAngle, -20f, jumpFactor);
                    lowerLegAngle = Mathf.Lerp(lowerLegAngle, 30f, jumpFactor);
                }

                // 초기 회전값 캐시 후 오프셋 적용 (누적 방지)
                Quaternion initThigh = (_initialLegRotations[i] != null && _initialLegRotations[i].Length > 0)
                    ? _initialLegRotations[i][0] : Quaternion.identity;
                Quaternion initLowerLeg = (_initialLegRotations[i] != null && _initialLegRotations[i].Length > 1)
                    ? _initialLegRotations[i][1] : Quaternion.identity;

                leg.UpperLeg.localRotation = initThigh * Quaternion.Euler(thighAngle, 0f, pawSwing);
                leg.LowerLeg.localRotation = initLowerLeg * Quaternion.Euler(lowerLegAngle, 0f, 0f);
            }
        }

        /// <summary>
        /// 척추 Y 위치 bob: sin(time*freq*0.5) * amp, 속도 비례.
        /// </summary>
        private void ApplySpineBob(float time, float freq)
        {
            if (_spineChain == null || _spineChain.Length == 0 || _initialSpineRotations == null)
                return;

            float bobAmount = _spineBobAmount * Mathf.Max(0.1f, _currentSpeed);
            float bob = Mathf.Sin(time * freq * 0.5f) * bobAmount;

            for (int i = 0; i < _spineChain.Length && i < _initialSpineRotations.Length; i++)
            {
                float t = (float)i / _spineChain.Length;
                float iBob = bob * (1f - t * 0.5f); // 끝으로 갈수록 감쇠
                _spineChain[i].localRotation = _initialSpineRotations[i] * Quaternion.Euler(iBob * 5f, 0f, 0f);
            }
        }

        // ──────────────────────────────────────────────
        //  Helper class
        // ──────────────────────────────────────────────

        private class LegCandidate
        {
            public Transform Root;
            public System.Collections.Generic.List<Transform> Chain;
            public Vector3 WorldPosition;
            public Vector3 LocalPosition;
            public bool IsFront;
            public bool IsRight;
        }
    }
}