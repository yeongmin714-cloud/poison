using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-09-12 P2 공격 FX 개편] 무기 스윙 트레일 — BOTW식 흰색 궤적 잔상.
    ///
    /// 기존 스윙 슬래시 쿼드(Slash VFX 프리팹)를 대체한다: 예시(젤다 BOTW) 스윙은
    /// "무기 궤적 잔상(흰색, 부채꼴 페이드아웃, 캐릭터 키 수준 스케일)"이며 프리팹 슬래시
    /// 쿼드가 아니다 → TrailRenderer를 무기 bounds 최장축 팁(그립 반대편 끝)에 부착해
    /// 실제 무기 궤적을 그대로 흰 잔상으로 남긴다.
    ///
    /// 구조:
    ///   - 정적 파사드(Static API) + 숨은 호스트 없음 — TrailRenderer는 무기 GLB 인스턴스의
    ///     자식 GO에 부착되므로 손(RightHand) 본 추적이 공짜로 따라온다.
    ///   - WeaponEquipManager.Equip(무기 GLB 장착 + 그립 정렬 완료 직후)가 Attach를 호출해
    ///     (재)부착 — 장착마다 갱신(GLB 교체 시 구 트레일 파괴 후 새 팁에 재부착).
    ///   - HumanoidClipDriver가 콤보 진입(공격 홀드)에 SetEmitting(true), 홀드 종료/
    ///     Idle 크로스(콤보 종료/인터럽트)에 SetEmitting(false) — static 바인딩.
    ///
    /// 트레일 사양(요구):
    ///   - [45차 P4] width 3키 곡선 (0,1.0)/(0.6,0.65)/(1,0) — 뿌리 굵고 끝이 빠지는 부채꼴, time 0.22s
    ///   - 색 그라디언트 흰(알파1) → 흰(알파0)
    ///   - additive(Universal Render Pipeline/Particles/Unlit — _Blend=Additive 세팅,
    ///     셰이더 미발견 시 Sprites/Default 폴백), minVertexDistance 0.05
    ///   - [2026-09-13 맨손 폴백] 무기 미장착 상태 공격에서는 EnsureBareFist가 플레이어
    ///     오른손 본에 폴백 트레일을 부착(팁=손 위치) — 맨손 콤보에서도 궤적 잔상 보장.
    ///   - [2026-09-15 Phase B] 무기별 트레일 색상/폭/이펙트 차별화 — 검(흰/표준), 창(청/얇고 긴),
    ///     활(황/짧음), 맨손(회/짧음). 콤보 스테이지별 색상 오버라이드 지원(1타 흰/2타 골드/3타 주황).
    /// </summary>
    public static class WeaponSwingTrail
    {
        // ── 사양 상수 ──
        /// <summary>트레일 폭(팁 기준 최대, m) — 캐릭터 키(~1.7m) 대비 0.09 = 잘 보이는 궤적(0.06에서 가시성 보강 상향).</summary>
        private const float TipWidth = 0.09f;
        /// <summary>잔상 지속(초) — BOTW식 짧은 흰 궤적. [45차 P4] 0.18 → 0.22 (가독성 소폭 상향).</summary>
        private const float TrailTime = 0.22f;
        /// <summary>정점 최소 간격(m) — 무거운 회전에서도 부드러운 곡선.</summary>
        private const float MinVertexDistance = 0.05f;

        // [Phase B] 무기별 트레일 설정
        private static readonly TrailConfig SwordConfig = new TrailConfig
        {
            startColor = new Color(1f, 1f, 1f, 1f),      // 흰
            endColor = new Color(1f, 1f, 1f, 0f),
            widthMultiplier = TipWidth,
            trailTime = TrailTime,
            minVertexDistance = MinVertexDistance,
            emissionIntensity = 1.0f
        };

        private static readonly TrailConfig SpearConfig = new TrailConfig
        {
            startColor = new Color(0.3f, 0.6f, 1f, 1f),   // 청색 (창 = 찌르기)
            endColor = new Color(0.3f, 0.6f, 1f, 0f),
            widthMultiplier = TipWidth * 0.7f,            // 얇은 찌르기 궤적
            trailTime = TrailTime * 1.2f,                  // 더 오래 남음
            minVertexDistance = MinVertexDistance,
            emissionIntensity = 1.2f
        };

        private static readonly TrailConfig BowConfig = new TrailConfig
        {
            startColor = new Color(1f, 0.9f, 0.3f, 1f),   // 황금빛 (활 = 원거리)
            endColor = new Color(1f, 0.9f, 0.3f, 0f),
            widthMultiplier = TipWidth * 0.5f,            // 짧은 반동 궤적
            trailTime = TrailTime * 0.5f,
            minVertexDistance = MinVertexDistance,
            emissionIntensity = 0.8f
        };

        private static readonly TrailConfig FistConfig = new TrailConfig
        {
            startColor = new Color(0.8f, 0.8f, 0.8f, 1f), // 회색 (맨손 = 잽)
            endColor = new Color(0.8f, 0.8f, 0.8f, 0f),
            widthMultiplier = TipWidth * 0.8f,
            trailTime = TrailTime * 0.8f,
            minVertexDistance = MinVertexDistance,
            emissionIntensity = 0.7f
        };

        // [Phase B] 콤보 스테이지별 색상 오버라이드 (SlashVFXRunner.ComboStageTint와 동기화)
        private static readonly Color[] ComboStageColors =
        {
            new Color(1f, 1f, 1f, 1f),     // Stage 1: 흰 (CoreTint)
            new Color(1f, 0.9f, 0.5f, 1f), // Stage 2: 골드 (AccentTint)
            new Color(1f, 0.55f, 0.2f, 1f) // Stage 3: 주황/붉은 (OuterTint)
        };

        private struct TrailConfig
        {
            public Color startColor;
            public Color endColor;
            public float widthMultiplier;
            public float trailTime;
            public float minVertexDistance;
            public float emissionIntensity;
        }

        /// <summary>현재 부착된 트레일 오브젝트(무기 GLB 자식). null = 부착 없음.</summary>
        private static GameObject _trailGo;
        private static TrailRenderer _trail;
        private static bool _emitting;
        private static bool _shaderWarned;
        private static WeaponType _currentWeaponType = WeaponType.Fist;
        private static int _currentComboStage = 0;

        // ================================================================
        // 정적 API
        // ================================================================

        /// <summary>
        /// 방출 토글 — HumanoidClipDriver가 콤보 진입(공격 홀드)에 true, 홀드 종료/Idle 크로스에 false.
        /// 트레일 미부착(무기 없음/활·창 등)이면 무시. 값 변화 없는 재호출은 조용히 무시(스팸 방지).
        /// </summary>
        public static void SetEmitting(bool on)
        {
            if (_emitting == on) return;   // 동일 값 무시 — 프레임마다 호출해도 로그/세팅 스팸 없음
            _emitting = on;
            if (_trail != null)
                _trail.emitting = on;
        }

        /// <summary>
        /// [Phase B] 콤보 스테이지 설정 — 스테이지별 색상 오버라이드 적용.
        /// HumanoidClipDriver.FireComboSlash(stage) 호출 시 함께 호출.
        /// </summary>
        public static void SetComboStage(int stage)
        {
            _currentComboStage = Mathf.Clamp(stage, 0, 3);
            if (_trail != null && _currentComboStage > 0)
            {
                ApplyComboStageTint();
            }
        }

        /// <summary>
        /// [45차 P4] 히트 순간 트레일 밝기 펄스 — 공격 적중 확정 시점(PlayerCombat.AttackTarget 성공 분기)에서 호출.
        /// startColor 알파 1.0 고정 + 그라디언트 복원 방안은 단순화하고, widthMultiplier를 순간 1.25배로
        /// 뻈다가 0.12s 뒤 원복한다(부채꼴 폭 곡선 전체가 같이 확장되어 밝고 두껍게 읽힘).
        /// static은 코루틴 호스트가 없으므로 트레일 GO에 소형 트리거 컴포넌트(<see cref="TrailPulseRunner"/>)를
        /// 붙여 구현 — 44차 AutoDestroy 선례 패턴. 트레일 미부착(_trail null)이면 무시(호출부 null 가드 불필요).
        /// </summary>
        public static void Pulse()
        {
            if (_trail == null || _trailGo == null) return;   // 미부착/파괴 — 조용히 무시

            var runner = _trailGo.GetComponent<TrailPulseRunner>();
            if (runner == null)
                runner = _trailGo.AddComponent<TrailPulseRunner>();
            runner.Trigger(_trail, GetBaseWidth());
        }

        /// <summary>
        /// 무기 GLB 장착 시 트레일 (재)부착 — WeaponEquipManager가 그립 정렬 완료 직후 호출.
        /// tipWorld: 무기 bounds 최장축 그립 반대편 끝(팁)의 월드 좌표(스케일 보정·오프셋 반영 완료).
        /// weapon 인스턴스의 자식 GO로 부착하므로 손 본을 자동 추적한다. 기존 트레일은 파괴 후 재생성.
        /// </summary>
        public static void Attach(GameObject weapon, Vector3 tipWorld, WeaponType weaponType = WeaponType.Fist)
        {
            Detach();
            if (weapon == null) return;

            _currentWeaponType = weaponType;

            _trailGo = new GameObject("WeaponSwingTrail");
            _trailGo.transform.SetParent(weapon.transform, false);
            // 팁 = 그립 반대편 끝 — 무기 로컬로 고정해 손 본/무기 회전에 완전 동기
            _trailGo.transform.localPosition = weapon.transform.InverseTransformPoint(tipWorld);
            _trailGo.transform.localRotation = Quaternion.identity;

            _trail = _trailGo.AddComponent<TrailRenderer>();
            ApplyWeaponConfig(_trail, GetConfigForWeapon(weaponType));
            _trail.emitting = _emitting;    // 콤보 중 장착 교체 시 현재 방출 상태 유지

            Debug.Log($"[Weapon] 스윙 트레일 부착: 타입={weaponType}, 팁={tipWorld:F2}, width={GetConfigForWeapon(weaponType).widthMultiplier}, time={GetConfigForWeapon(weaponType).trailTime}s, emitting={_emitting}");
        }

        /// <summary>트레일 제거 — 해제/재장착 시 WeaponEquipManager가 호출. 부착물 없으면 무시.</summary>
        public static void Detach()
        {
            if (_trailGo != null)
            {
                Object.Destroy(_trailGo);
                _trailGo = null;
                _trail = null;
            }
            _emitting = false;
            _currentComboStage = 0;
        }

        /// <summary>
        /// [2026-09-13 맨손 폴백] 무기 트레일 미부착(맨손 공격)일 때 플레이어 오른손에 폴백 트레일 부착.
        ///
        /// 배경: 트레일은 WeaponEquipManager.Equip(무기 GLB 장착) 시에만 Attach되므로 맨손 공격에서는
        /// _trail == null → SetEmitting(true)가 전부 무시되어 시각 효과가 전혀 없었다.
        ///
        /// 동작:
        ///   1) RightHand 본 탐색 — Animator 휴머노이드 매핑(GetBoneTransform) → 이름 "RightHand"
        ///      포함 자식 탐색 → 그래도 없으면 player 루트 직접 사용(최후 폴백).
        ///   2) Attach(손 GO, 손 위치) 재사용 — 손 오브젝트 자식으로 트레일 생성(팁 = 손 위치),
        ///      무기 인스턴스 자식 부착 구조를 그대로 활용하므로 손 본 추적이 공짜로 따라온다.
        ///   3) Attach는 기존 트레일을 Detach 후 재생성하므로, 무기 트레일이 이미 있으면
        ///      (_trail != null) 조용히 반환 — 폴백이 무기 트레일을 덮는 사고를 원천 차단.
        /// </summary>
        public static void EnsureBareFist(Transform player)
        {
            if (_trail != null) return;   // 무기 트레일 이미 부착 — 폴백 불필요(덮지 않음, 로그도 재발 안 함)
            if (player == null) return;

            // 1) 휴머노이드 Animator의 RightHand 본
            Transform hand = null;
            Animator animator = player.GetComponentInChildren<Animator>();
            if (animator != null)
                hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            // 2) 이름에 "RightHand" 포함하는 자식 탐색 (휴머노이드 매핑 실패/제네릭 리그 폴백)
            if (hand == null)
            {
                Transform[] children = player.GetComponentsInChildren<Transform>();
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].name.Contains("RightHand"))
                    {
                        hand = children[i];
                        break;
                    }
                }
            }

            // 3) 그래도 없으면 루트에 직접 부착 — 부재(무효과)보다는 나은 최후 폴백
            if (hand == null)
                hand = player;

            // Attach 재사용: weapon 인자에 손 GO, tipWorld=손 위치 → 손 자식으로 부착(팁=손 위치)
            Attach(hand.gameObject, hand.position, WeaponType.Fist);
            Debug.Log($"[Weapon] 맨손 트레일 폴백 부착: hand={GetTransformPath(hand)}");
        }

        // ================================================================
        // 내부: 설정 적용
        // ================================================================

        private static TrailConfig GetConfigForWeapon(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Sword: return SwordConfig;
                case WeaponType.Spear: return SpearConfig;
                case WeaponType.Bow: return BowConfig;
                default: return FistConfig;
            }
        }

        private static void ApplyWeaponConfig(TrailRenderer trail, TrailConfig config)
        {
            trail.time = config.trailTime;
            trail.minVertexDistance = config.minVertexDistance;
            // 부채꼴 페이드아웃 — 폭 곡선 × widthMultiplier
            trail.widthMultiplier = config.widthMultiplier;
            // [45차 P4 폭 곡선 개선] 3키 곡선 — 뿌리 굵게(1.0) → 60% 지점 0.65로 완만히 감쇠 → 끝에서 소실(0).
            // 기존 2키 선형((0,1)→(1,0))은 중간 감쇠가 단조로워 궤적 끝이 밋밋하게 읽히던 것을 개선.
            AnimationCurve width = new AnimationCurve();
            width.AddKey(0f, 1f);
            width.AddKey(0.6f, 0.65f);
            width.AddKey(1f, 0f);
            trail.widthCurve = width;
            // 색 그라디언트
            trail.startColor = config.startColor;
            trail.endColor = config.endColor;
            trail.numCapVertices = 4;      // 팁 마감 둥글림 — 끊긴 궤적 방지
            trail.alignment = LineAlignment.View;   // BOTW식 카메라 정면 빌보드 궤적
            trail.generateLightingData = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.material = CreateTrailMaterial(config.emissionIntensity);
        }

        private static float GetBaseWidth()
        {
            return GetConfigForWeapon(_currentWeaponType).widthMultiplier;
        }

        /// <summary>[Phase B] 콤보 스테이지별 색상 틴트 적용 — SlashVFXRunner.ComboStageTint와 동기화.</summary>
        private static void ApplyComboStageTint()
        {
            if (_trail == null || _currentComboStage <= 0 || _currentComboStage > ComboStageColors.Length)
                return;

            Color stageColor = ComboStageColors[_currentComboStage - 1];
            // 알파는 기존 endColor(투명) 유지하며 색상만 오버라이드
            _trail.startColor = stageColor;
            _trail.endColor = new Color(stageColor.r, stageColor.g, stageColor.b, 0f);
        }

        // ================================================================
        // 내부: 머티리얼 생성
        // ================================================================

        /// <summary>
        /// 트레일 머티리얼 — 1순위 URP Particles/Unlit(transparent+additive 블렌드 세팅),
        /// 미발견 시 Sprites/Default 폴백(알파 블렌드 — 흰 궤적 표현은 유지, 경고 1회).
        /// [Phase B] emissionIntensity로 발광 강도 조절.
        /// </summary>
        private static Material CreateTrailMaterial(float emissionIntensity = 1f)
        {
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpUnlit != null)
            {
                var mat = new Material(urpUnlit);
                // URP Particles Unlit — Surface=Transparent, Blend=Additive(=2) + 실제 블렌드 패스 상태
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
                mat.SetOverrideTag("RenderType", "Transparent");
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                // Emission 설정
                if (mat.HasProperty("_EmissionColor"))
                {
                    Color emission = Color.white * emissionIntensity;
                    mat.SetColor("_EmissionColor", emission);
                    mat.EnableKeyword("_EMISSION");
                }
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return mat;
            }

            Shader spriteDefault = Shader.Find("Sprites/Default");
            if (!_shaderWarned)
            {
                _shaderWarned = true;
                Debug.LogWarning("[Weapon] URP Particles/Unlit 미발견 — 스윙 트레일 Sprites/Default 폴백(알파 블렌드)");
            }
            return spriteDefault != null ? new Material(spriteDefault) : null;
        }

        // ================================================================
        // 내부: 경로 문자열
        // ================================================================

        /// <summary>계층 경로 문자열 생성 — 맨손 폴백 부착 위치 진단 로그용.</summary>
        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "(null)";
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        // ================================================================
        // 내부: 히트 펄스 트리거 컴포넌트 (44차 AutoDestroy 선례 패턴)
        // ================================================================

        /// <summary>
        /// [45차 P4] 스윙 트레일 밝기 펄스 트리거 — static 클래스가 코루틴을 실행할 수 없어 트레일 GO에
        /// 부착하는 소형 호스트 컴포넌트(44차 AutoDestroy 선례 패턴, SlashFxHost와 동일 발상).
        /// widthMultiplier를 1.25배로 순간 확장 후 0.12s 뒤 기본 폭으로 원복. 재트리거 시 원복 시각만 재예약.
        /// </summary>
        private sealed class TrailPulseRunner : MonoBehaviour
        {
            /// <summary>펄스 확장 배율 — 순간 1.25배.</summary>
            private const float PulseScale = 1.25f;
            /// <summary>펄스 지속(초) — 0.12s 뒤 원복.</summary>
            private const float PulseDuration = 0.12f;

            private TrailRenderer _trail;
            private float _baseWidth;
            private float _restoreTime;

            /// <summary>펄스 시작 — 진행 중 재호출 시 원복 시각을 재예약(연속 히트 흡수).</summary>
            public void Trigger(TrailRenderer trail, float baseWidth)
            {
                _trail = trail;
                _baseWidth = baseWidth;
                if (_trail != null)
                    _trail.widthMultiplier = _baseWidth * PulseScale;
                _restoreTime = Time.time + PulseDuration;
                enabled = true;
            }

            private void Update()
            {
                // 트레일 파괴(재장착 등) 시 원복 불필요 — 펄스만 종료
                if (_trail == null)
                {
                    enabled = false;
                    return;
                }
                if (Time.time < _restoreTime) return;

                _trail.widthMultiplier = _baseWidth;   // 원복 — 부착 시 기본 폭
                enabled = false;                        // 펄스 종료 — 다음 트리거는 Trigger()가 재활성화
            }
        }
    }
}