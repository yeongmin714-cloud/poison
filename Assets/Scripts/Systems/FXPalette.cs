using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-09-13(45차 P1): FX 전역 색 팔레트 + 런타임 파티클 머티리얼 정규화.
    /// BOTW 팔레트 통일 — 흰/골드/주황(+블러드) 상수만 사용한다.
    /// 근거: 테스트 7 영상 히트 프레임 픽셀 실측 — 보라 파티클 평균 RGB (219,19,219)
    /// = Unity 셰이더 에러 마젠타. 원인은 런타임 생성 파티클(CombatVFXController 6종)이
    /// 머티리얼 미지정 → 기본 Particles/Standard Unlit → URP 미지원이었으므로,
    /// 모든 런타임 파티클 렌더러는 FXPalette.ApplyTo()로 정규화 머티리얼을 강제한다.
    /// </summary>
    public static class FXPalette
    {
        // ================================================================
        // BOTW 팔레트 상수 (흰/골드/주황 3색 + 블러드)
        // ================================================================
        /// <summary>코어 흰색 — 플래시/일반 데미지 숫자.</summary>
        public static readonly Color Core = new Color(1f, 1f, 1f);
        /// <summary>골드 — 크리/강타 데미지 숫자, 히트 스파크(44차 골드화이트).</summary>
        public static readonly Color Accent = new Color(1f, 0.9f, 0.5f);
        /// <summary>옅은 주황 — 외곽 스파크/기타 강조.</summary>
        public static readonly Color Edge = new Color(1f, 0.55f, 0.2f);
        /// <summary>블러드 붉은색 — 출혈/암살 계열(순수 Color.red 대체).</summary>
        public static readonly Color Blood = new Color(0.75f, 0.08f, 0.08f);
        /// <summary>[79-R20] 공격 이펙트 통일색 — 웜 골드화이트. 공격 VFX 전 계층(슬래시 아크·스파크·데브리·
        /// 라이트·실루엣·디스토션·무기글로우·스파클 버스트) 단일 소스. 강도 구분은 색이 아닌 크기/앰프 위계
        /// (38→39-T3 선례). 데미지 숫자(Accent)·패리(ParryGold)·출혈(Blood)·사망 불티(Edge)·명령(MoveCommand)은 계층별 유지.</summary>
        public static readonly Color AttackPrimary = new Color(1f, 0.92f, 0.72f);

        // ================================================================
        // 정규화 파티클 머티리얼 (지연 캐시)
        // ================================================================
        private static Material _particleMaterial;
        private static bool _shaderResolved; // 해상 로그 1회 스팸 가드

        /// <summary>
        /// 정규화된 파티클 머티리얼 — 최초 접근 시 1회 생성 후 캐시.
        /// 셰이더 폴백: Sprites/Default(빌트인, URP 확실 렌더) → URP Particles/Unlit
        /// → Particles/Standard Unlit. 기본색 흰색 세팅.
        /// </summary>
        public static Material ParticleMaterial
        {
            get
            {
                if (_particleMaterial == null)
                    _particleMaterial = CreateParticleMaterial();
                return _particleMaterial;
            }
        }

        private static Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit");

            if (shader == null)
            {
                // 폴백 전부 실패 — 마젠타 위험 경고 1회(스팸 가드), null 반환 시 ApplyTo가 무시한다.
                if (!_shaderResolved)
                {
                    _shaderResolved = true;
                    Debug.LogWarning("[FXPalette] 파티클 셰이더 폴백 전부 실패(마젠타 위험) — 머티리얼 미적용");
                }
                return null;
            }

            var mat = new Material(shader);
            mat.name = "FXPalette_ParticleMaterial";
            // 기본색 흰색 — Sprites/Default는 _Color, URP Particles/Unlit은 _BaseColor(둘 다 기록).
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            if (!_shaderResolved)
            {
                _shaderResolved = true;
                Debug.Log($"[FXPalette] 파티클 머티리얼 정규화 — shader={shader.name}");
            }
            return mat;
        }

        /// <summary>
        /// 파티클 렌더러에 정규화 머티리얼 적용. rnd가 null이면 무시(안전 반환).
        /// sharedMaterial 사용 — 머티리얼 인스턴스 난립 방지(단일 공유 캐시).
        /// </summary>
        public static void ApplyTo(ParticleSystemRenderer rnd)
        {
            if (rnd == null) return;
            var mat = ParticleMaterial;
            if (mat == null) return;
            // [무번호 47-b] 정규화 가드 경로 — 단일 세터 NRE(사용자 세션 실측) 시 무음 삼켜져 마젠타 빌보드가
            //   됐던 경로(FxEmberRise 실측)를 가드로 전환: 실패 시 원본 복원+1급 로그, 호출부 무음 삼킴 제거.
            FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, "FXPalette.ApplyTo");
        }

        // ================================================================
        // [무번호 28-U1] 명령/전투 팔레트 진입점 — 28 계획 U-1: 신규 FX(M/S/P)의 단일 참조 소스.
        //   기존 단일 소스가 있는 색은 프로퍼티 위임(값 복제 금지 — 소스 이원화 방지),
        //   단일 소스가 없는 색만 리터럴 선언(출처 좌표 주석 필수).
        //   기존 잠긴 상수(BOTW 4색·CombatFx 계약색)는 28 계획 U-1에 따라 이 라운드 이관 금지(계약 보호).
        // ================================================================

        /// <summary>[무번호 19-C2] 이동 명령 청록 — 위임: CommandRingPresentation 단일 소스.</summary>
        public static Color MoveCommand => CommandRingPresentation.MoveTargetColor;

        /// <summary>[무번호 19-C2] 선택 링 청록 — 위임: CommandRingPresentation 단일 소스.</summary>
        public static Color SelectedGuard => CommandRingPresentation.SelectedGuardColor;

        /// <summary>[무번호 19-C2] 공격 명령 주황 — 위임: CommandRingPresentation 단일 소스.</summary>
        public static Color AttackCommand => CommandRingPresentation.ExplicitTargetColor;

        /// <summary>[무번호 19-C2] 집결 금(발령 미구현 — 상수만 준비, 위조 배선 금지) — 위임: CommandRingPresentation.</summary>
        public static Color RallyCommand => CommandRingPresentation.RallyColor;

        /// <summary>[무번호 18-B3] 몬스터 위험 적색 — 텔레그래프 계약 리터럴(AttackTelegraphIndicator NormalColor).</summary>
        public static readonly Color MonsterDanger = new Color(1f, 0.30f, 0.16f, 0.45f);

        /// <summary>[무번호 19-F1] 이동 먼지 흙색 — ShockwaveRingFX.cs:367 TrailDustColor 리터럴 미러.</summary>
        public static readonly Color Dust = new Color(0.78f, 0.72f, 0.62f, 0.30f);

        /// <summary>[무번호 20-J1] 패망 PERFECT 금 지면 링 — PlayerCombat.cs:770 리터럴 미러.</summary>
        public static readonly Color ParryGold = new Color(1f, 0.95f, 0.55f, 0.70f);

        /// <summary>[171차 6-A] 패막 흰 팔레트 — 사용자 확정(171차 §6: 패막 전 채널 흰색 단일, 알파는 용도별 조정 전제). 리터럴 신설.</summary>
        public static readonly Color ParryWhite = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>[무번호 20-J1] 패망 청 큐 링 — PlayerCombat.cs:443 리터럴 미러.</summary>
        public static readonly Color ParryCyan = new Color(0.55f, 0.8f, 1f, 0.50f);

        /// <summary>[무번호 26-ST1] 병사 스윙 리본 청백 — SoldierSwingTrail.cs:34 StartColorPaleBlue 리터럴 미러(알파 1 고정).</summary>
        public static readonly Color SoldierRibbon = new Color(0.75f, 0.92f, 1f, 1f);

        /// <summary>[무번호 25-IN] 몬스터 명중 액센트(적색 우세) — AnimalAI.cs:974 리터럴 미러.</summary>
        public static readonly Color MonsterAccent = new Color(1f, 0.25f, 0.2f, 0.8f);

        /// <summary>[무번호 28-M4] 정예 리듬 오라 — [무번호 18-B3 EliteColor 미러 — AttackTelegraphIndicator.cs:34] (ALPHA 0.45 실측 미러). 기존 멤버 무손상.</summary>
        public static readonly Color ElitAura = new Color(1f, 0.30f, 0.16f, 0.45f);
    }
}
