using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [171차] 패막 성공 보유 에셋 FX — Resources/VFX/Parry/ 복제 프리팹 소비(46차 asset-only FX 도ctrine:
    /// 메인 연출은 에셋, 절차 ShockwaveRingFX는 지면 보조/폴백). 색 = FXPalette.ParryWhite 단일(§6 흰 팔레트).
    /// PERFECT = Guard_01(방어 임팩트)+Holy hit(확산 링) / GOOD = Hit_01(소형).
    /// [176차] Sparks white(방사형 스파크 산란) 철수 — 원형 링/임팩트 계열만 유지(사용자 지시: 원형 링만).
    /// EditMode 무동작, 클립/프리팹 부재 시 조용히 스킵(133차 무음 폴백 정신), 수명 2s 자동 파괴.
    /// [172차 P2] TintWhite 확장 — 노란 원인 제거: ①colorOverLifetime 그라디언트 화이트화(오렌지 그라디언트
    /// key0=(1,0.34,0)~key3=(1,0.83,0) 실측 — 알파 키 원본 보존) ②렌더러 머티리얼 _TintColor/_EmissionColor
    /// 오버라이드(HDR 청발광 차단 — HIE_Guard_01_TrailGlow_01 _EmissionColor=rgb(0,1.63,8.0) 실측).
    /// 에셋 프리팹/머티리얼 파일 비수정(런타임 클론 오버라이드만 — 기존 계약). 화이트화 로직은 순수 함수 분리(EditMode 판정).
    /// </summary>
    public static class ParryAssetFX
    {
        private const string GuardPath = "VFX/Parry/Guard_01";
        private const string HitPath = "VFX/Parry/Hit_01";
        private const string HolyPath = "VFX/Parry/Holy hit";
        private const float LifetimeSeconds = 2f;

        /// <summary>[172차 P2] _EmissionColor 저강도 흰색 — HDR 청발광(HIE_Guard 청) 차단용 댐프 상수.</summary>
        internal static readonly Color EmissionDampedWhite = new Color(0.35f, 0.35f, 0.35f);

        public static void Spawn(bool isPerfect, Vector3 contactPoint)
        {
            if (!Application.isPlaying) return;   // EditMode 무동작
            // [176차] Sparks white 철수 — 방사형 스파크 산란 제거, 메인 임팩트/확산 링 계열만 발화.
            if (isPerfect)
            {
                SpawnOne(GuardPath, contactPoint, 1f);
                SpawnOne(HolyPath, contactPoint, 1f);
            }
            else
            {
                SpawnOne(HitPath, contactPoint, 0.6f);
            }
        }

        private static void SpawnOne(string resourcePath, Vector3 pos, float scale)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return;   // 부재 — 조용히 스킵(무음 폴백)
            var go = Object.Instantiate(prefab, pos, Quaternion.identity);
            go.transform.localScale *= scale;
            TintWhite(go);
            Object.Destroy(go, LifetimeSeconds);
        }

        /// <summary>
        /// [172차 P2] 그라디언트 화이트화 — 순수 함수(입력 그라디언트 무변경, 새 그라디언트 반환).
        /// 각 색 키의 rgb=흰색, 알파 키는 원본 그대로 보존(5키 그라디언트면 5키 전부 rgb(1,1,1)·a 유지).
        /// null/키 0개 입력은 알파 1 흰색 1키 폴백 — EditMode 프리뷰 판정(커버 여부) 가능.
        /// </summary>
        public static Gradient WhitenGradientPreservingAlpha(Gradient source)
        {
            var grad = new Gradient();
            var alphaKeys = (source != null && source.alphaKeys != null && source.alphaKeys.Length > 0)
                ? source.alphaKeys
                : new[] { new GradientAlphaKey(1f, 0f) };
            var colorKeys = (source != null && source.colorKeys != null && source.colorKeys.Length > 0)
                ? source.colorKeys
                : new[] { new GradientColorKey(Color.white, 0f) };
            var whiteColors = new GradientColorKey[colorKeys.Length];
            for (int i = 0; i < colorKeys.Length; i++)
                whiteColors[i] = new GradientColorKey(Color.white, colorKeys[i].time);
            grad.SetKeys(whiteColors, alphaKeys);
            return grad;
        }

        /// <summary>
        /// 파티클 색 오버라이드 — ParryWhite 단일(§6). 개별 시스템 오버라이드 실패는 무해 스킵.
        /// [172차 P2] 커버 채널 3종: ①main.startColor(기존) ②colorOverLifetime(활성 시 — 알파 키 보존 화이트화)
        /// ③렌더러 머티리얼 _TintColor→ParryWhite·_EmissionColor→EmissionDampedWhite.
        /// ②③은 런타임 오버라이드만(에셋 프리팹/머티리얼 파일 비수정 계약 — 머티리얼은 클론 후 교체).
        /// </summary>
        internal static void TintWhite(GameObject go)
        {
            var white = FXPalette.ParryWhite;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                try { main.startColor = white; } catch (System.Exception) { }
                // [172차 P2-①] colorOverLifetime 화이트화 — 활성 모듈만(비활성 강제 활성화 금지 — 기존 룩 무손상)
                try
                {
                    var col = ps.colorOverLifetime;
                    if (col.enabled)
                        col.color = new ParticleSystem.MinMaxGradient(WhitenGradientPreservingAlpha(col.color.gradient));
                }
                catch (System.Exception) { }
            }
            // [172차 P2-②] 렌더러 머티리얼 오버라이드 — shared 머티리얼(에셋) 직접 수정 금지 → 클론 교체
            foreach (var rnd in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                try
                {
                    var shared = rnd.sharedMaterials;
                    var clones = new Material[shared.Length];
                    bool replaced = false;
                    for (int i = 0; i < shared.Length; i++)
                    {
                        var srcMat = shared[i];
                        if (srcMat == null) { clones[i] = null; continue; }
                        var clone = new Material(srcMat) { name = srcMat.name + "_ParryWhiteInst" };
                        if (clone.HasProperty("_TintColor")) clone.SetColor("_TintColor", white);
                        if (clone.HasProperty("_EmissionColor")) clone.SetColor("_EmissionColor", EmissionDampedWhite);
                        clones[i] = clone;
                        replaced = true;
                    }
                    if (replaced) rnd.sharedMaterials = clones;
                }
                catch (System.Exception) { }
            }
        }
    }
}
