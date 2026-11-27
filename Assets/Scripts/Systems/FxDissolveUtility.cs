using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 30-D4] 절차 디졸브 유틸 — 사망/소멸 연출. 바디 렌더러를 디졸브 머티리얼 인스턴스로
    /// 교체하고 _DissolveAmount를 0→1 램프한 뒤 렌더러 비활성+인스턴스 파괴(누출 금지).
    /// 계약: 셰이더 미발견=false(호출부 기존 사망 연출만 유지 — 폴백 계약), _BaseMap/_BaseColor 승계,
    /// 결정론(Random 0건 — 램프는 선형+엣지는 셰이더 담당), EditMode 가드(21-K1 선례).
    /// </summary>
    public static class FxDissolveUtility
    {
        public const string DissolveShaderName = "ProjectName/FX/Dissolve";

        /// <summary>디졸브 가용 여부(셰이더 존재) — 폴백 판정용.</summary>
        public static bool DissolveSupported => Shader.Find(DissolveShaderName) != null;

        /// <summary>바디 렌더러 디졸브 램프 시작 — 코루틴은 호출부(MonoBehaviour)에서 구동.
        /// false=셰이더 부재/렌더러 부재(기존 사망 연출만 유지).</summary>
        public static bool TryBeginDissolve(GameObject body, Color edgeColor, float duration)
        {
            return TryBeginDissolve(body, edgeColor, duration, 0);
        }

        /// <summary>[무번호 39-T4] 티어 위계 오버로드 — tier≥5(정예)=엣지 강화(×1.3), duration 연장 0.9→1.2s.
        /// 기존 3인자 시그니처 무손상(30-D4 잠금 보존 — 위임 경로).</summary>
        public static bool TryBeginDissolve(GameObject body, Color edgeColor, float duration, int tier)
        {
            if (tier >= 5)
            {
                edgeColor = new Color(Mathf.Clamp01(edgeColor.r * 1.3f), Mathf.Clamp01(edgeColor.g * 1.3f), Mathf.Clamp01(edgeColor.b * 1.3f), edgeColor.a);
                duration = 1.2f;
            }
            if (body == null || duration <= 0.01f) return false;
            if (!Application.isPlaying) return false;   // EditMode 가드 — 인스턴스 스왑 자체를 차단(누출 방지)
            var shader = Shader.Find(DissolveShaderName);
            if (shader == null) return false;   // 폴백 계약 — 기존 사망 연출 유지

            var swapped = new System.Collections.Generic.List<(Renderer rend, Material inst)>();
            foreach (var rend in body.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null || rend is TrailRenderer || rend is ParticleSystemRenderer || rend is LineRenderer) continue;
                if (rend.sharedMaterial == null) continue;
                // [무번호 48-T2] 림 오버레이(29-M2 임시 FX 레이어)는 디졸브 대상 제외 — _MainTex 부재 경고와
                //   불필요한 클론(null _BaseMap 승계)을 차단한다. 시체 본체(GLB SMR)만 디졸브.
                if (rend.sharedMaterial != null && rend.sharedMaterial.name.Contains("FxRimOverlay")) continue;
                var inst = new Material(shader) { name = "FxDissolveMat" };
                // _BaseMap/_BaseColor 승계 — 소멸 중 실루엣 인지 유지
                var srcMat = rend.sharedMaterial;
                if (inst.HasProperty("_BaseMap"))
                {
                    if (srcMat.HasProperty("_BaseMap")) inst.SetTexture("_BaseMap", srcMat.GetTexture("_BaseMap"));
                    else if (srcMat.HasProperty("_MainTex")) inst.SetTexture("_BaseMap", srcMat.GetTexture("_MainTex"));
                    else if (srcMat.mainTexture != null) inst.SetTexture("_BaseMap", srcMat.mainTexture);
                }
                if (inst.HasProperty("_BaseColor"))
                {
                    if (srcMat.HasProperty("_BaseColor")) inst.SetColor("_BaseColor", srcMat.GetColor("_BaseColor"));
                    else if (srcMat.HasProperty("_Color")) inst.SetColor("_BaseColor", srcMat.GetColor("_Color"));
                }
                inst.SetColor("_EdgeColor", edgeColor);
                // [무번호 47-b] 가드 경로 — `.material = inst` 세터 NRE 시 부분 갱신으로 슬롯이 null로 남아
                //   마젠타 시체가 렌더됐다(Monster_slime/input 실측). 슬롯 0 교체 가드는 실패 시 전체 원본 복원
                //   — 시체는 기존 머티리얼(녹색) 유지, 디졸브 연출만 생략(전투 방해 금지 원칙).
                if (!FxMaterialLibrary.TryAssignSlot0(rend, inst, "FxDissolve"))
                {
                    Object.Destroy(inst);   // 미소유 인스턴스 즉시 정리 — 누출 금지
                    continue;
                }
                // [무번호 48-T3] 원본 승계 기록 — 램프 종료 시 파괴 인스턴스 대신 **원본 머티리얼 복원**.
                //   근거: 파괴만 하면 슬롯이 null로 남고, 리스폰 시 렌더러 재활성으로 null 머티리얼(마젠타)이
                //   노출됐다(47차 Play 실측 — 사망 후 리스폰 시점 감지). 복원 후 파괴 = 슬롯 항상 유효.
                _dissolveOriginals[rend] = srcMat;
                _dissolveInsts[rend] = inst;
                swapped.Add((rend, inst));
            }
            return swapped.Count > 0;
        }

        // [무번호 48-T3] 디졸브 원본 머티리얼 승계 레지스트리 — 램프 종료 시 복원용(파괴 전 승계, 누출 없음).
        private static readonly System.Collections.Generic.Dictionary<Renderer, Material> _dissolveOriginals
            = new System.Collections.Generic.Dictionary<Renderer, Material>();

        // [무번호 49-T1] 디졸브 인스턴스 승계 레지스트리 — 램프 종료 파괴를 .material 게터 없이 수행하기 위함.
        private static readonly System.Collections.Generic.Dictionary<Renderer, Material> _dissolveInsts
            = new System.Collections.Generic.Dictionary<Renderer, Material>();

        /// <summary>디졸브 램프 코루틴 — TryBeginDissolve 성공 후 호출부가 구동(Play 전용).
        /// 0→1 선형 램프 → 완료 시 렌더러 비활성 + **원본 머티리얼 복원** 후 인스턴스 파괴(누출 금지).</summary>
        public static System.Collections.IEnumerator FadeRoutine(GameObject body, float duration)
        {
            var rends = body != null ? body.GetComponentsInChildren<Renderer>(true) : null;
            if (rends == null || rends.Length == 0) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / duration);
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    // [무번호 49-T1] .material 게터 금지 — 게터는 공유 머티리얼을 클론 인스턴스화해 슬롯에
                    //   대입하는 부작용이 있고, 종료 시 그 인스턴스를 파괴하면 슬롯이 null로 남는다(리스폰
                    //   재활성 시 마젠타 노출 — 49차 Play 재실측). sharedMaterials 직접 판독(무인스턴스화).
                    try
                    {
                        var mats = r.sharedMaterials;
                        if (mats == null) continue;
                        foreach (var mm in mats)
                        {
                            if (mm != null && mm.HasProperty("_DissolveAmount")) mm.SetFloat("_DissolveAmount", a);
                        }
                    }
                    catch (System.Exception) { }
                }
                yield return null;
            }
            foreach (var r in rends)
            {
                if (r == null) continue;
                r.enabled = false;   // [무번호 48-T2] 소멸 완료 — 머티리얼 파괴 전 비활성(null 슬롯 렌더 창 금지)
                // [무번호 49-T1] 승계된 디졸브 인스턴스 직접 파괴 — .material 게터 사용 금지(위 주석).
                if (_dissolveInsts.TryGetValue(r, out var inst) && inst != null)
                {
                    if (Application.isPlaying) Object.Destroy(inst); else Object.DestroyImmediate(inst);
                    _dissolveInsts.Remove(r);
                }
                // [무번호 48-T3] 원본 복원 우선 — 파괴된 인스턴스가 슬롯에 남는 것을 금지(리스폰 재활성 대비).
                if (_dissolveOriginals.TryGetValue(r, out var original) && original != null)
                {
                    try
                    {
                        var all = r.sharedMaterials;
                        if (all != null && all.Length > 0)
                        {
                            var arr = new Material[all.Length];
                            arr[0] = original;
                            for (int i = 1; i < all.Length; i++) arr[i] = all[i];
                            FxMaterialLibrary.TryAssignSharedMaterials(r, arr, "DissolveRestore");
                        }
                    }
                    catch (System.Exception) { }
                    _dissolveOriginals.Remove(r);
                }
            }
        }
    }
}
