using UnityEngine;

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
    ///   - width 0.06 → 0 곡선(부채꼴 페이드아웃), time 0.18s
    ///   - 색 그라디언트 흰(알파1) → 흰(알파0)
    ///   - additive(Universal Render Pipeline/Particles/Unlit — _Blend=Additive 세팅,
    ///     셰이더 미발견 시 Sprites/Default 폴백), minVertexDistance 0.05
    /// </summary>
    public static class WeaponSwingTrail
    {
        // ── 사양 상수 ──
        /// <summary>트레일 폭(팁 기준 최대, m) — 캐릭터 키(~1.7m) 대비 0.06 = 가늘고 날카로운 궤적.</summary>
        private const float TipWidth = 0.06f;
        /// <summary>잔상 지속(초) — BOTW식 짧은 흰 궤적.</summary>
        private const float TrailTime = 0.18f;
        /// <summary>정점 최소 간격(m) — 무거운 회전에서도 부드러운 곡선.</summary>
        private const float MinVertexDistance = 0.05f;

        /// <summary>현재 부착된 트레일 오브젝트(무기 GLB 자식). null = 부착 없음.</summary>
        private static GameObject _trailGo;
        private static TrailRenderer _trail;
        private static bool _emitting;
        private static bool _shaderWarned;

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
        /// 무기 GLB 장착 시 트레일 (재)부착 — WeaponEquipManager가 그립 정렬 완료 직후 호출.
        /// tipWorld: 무기 bounds 최장축 그립 반대편 끝(팁)의 월드 좌표(스케일 보정·오프셋 반영 완료).
        /// weapon 인스턴스의 자식 GO로 부착하므로 손 본을 자동 추적한다. 기존 트레일은 파괴 후 재생성.
        /// </summary>
        public static void Attach(GameObject weapon, Vector3 tipWorld)
        {
            Detach();
            if (weapon == null) return;

            _trailGo = new GameObject("WeaponSwingTrail");
            _trailGo.transform.SetParent(weapon.transform, false);
            // 팁 = 그립 반대편 끝 — 무기 로컬로 고정해 손 본/무기 회전에 완전 동기
            _trailGo.transform.localPosition = weapon.transform.InverseTransformPoint(tipWorld);
            _trailGo.transform.localRotation = Quaternion.identity;

            _trail = _trailGo.AddComponent<TrailRenderer>();
            _trail.time = TrailTime;
            _trail.minVertexDistance = MinVertexDistance;
            // 부채꼴 페이드아웃 — 폭 0.06 → 0 선형 감소 곡선 × widthMultiplier
            _trail.widthMultiplier = TipWidth;
            AnimationCurve width = new AnimationCurve();
            width.AddKey(0f, 1f);
            width.AddKey(1f, 0f);
            _trail.widthCurve = width;
            // 색 그라디언트 — 흰(불투명) → 흰(투명)
            _trail.startColor = new Color(1f, 1f, 1f, 1f);
            _trail.endColor = new Color(1f, 1f, 1f, 0f);
            _trail.numCapVertices = 4;      // 팁 마감 둥글림 — 끊긴 궤적 방지
            _trail.alignment = LineAlignment.View;   // BOTW식 카메라 정면 빌보드 궤적
            _trail.generateLightingData = false;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.material = CreateTrailMaterial();
            _trail.emitting = _emitting;    // 콤보 중 장착 교체 시 현재 방출 상태 유지

            Debug.Log($"[Weapon] 스윙 트레일 부착: 팁={tipWorld:F2}, width={TipWidth}→0, time={TrailTime}s, emitting={_emitting}");
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
        }

        // ================================================================
        // 내부: additive 머티리얼
        // ================================================================

        /// <summary>
        /// 트레일 머티리얼 — 1순위 URP Particles/Unlit(transparent+additive 블렌드 세팅),
        /// 미발견 시 Sprites/Default 폴백(알파 블렌드 — 흰 궤적 표현은 유지, 경고 1회).
        /// </summary>
        private static Material CreateTrailMaterial()
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
    }
}
