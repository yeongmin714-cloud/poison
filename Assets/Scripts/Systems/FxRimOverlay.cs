using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 29-D3] 프레넬 림 오버레이 유틸 — 소스 SMR과 동일 mesh/bones를 공유하는 제2 SMR에
    /// 림 머티리얼을 얹는 표준 기법. 계약: 오버레이가 머티리얼 인스턴스를 소유하고 해제 시 함께 파괴
    /// (누출 금지), 림 셰이더 미지원 환경=AttachRim null 반환(기존 emissive 경로 유지 — 폴백 계약),
    /// Random 0건, 가림막/그림자 비활성(오버레이 순수 발광).
    /// </summary>
    public static class FxRimOverlay
    {
        /// <summary>림 오버레이 생성 — 소스 SMR 본 계열 공유. 셰이더 부재 시 null(호출부 폴백).</summary>
        public static GameObject AttachRim(SkinnedMeshRenderer source, Color rimColor, float rimPower, float intensity)
        {
            if (source == null || source.sharedMesh == null) return null;
            var mat = FxMaterialLibrary.CreateRimMaterial("FxRimOverlayMat", rimColor, rimPower, intensity);
            if (mat == null) return null;   // 폴백 계약 — 미지원 환경 기존 경로 유지
            var go = new GameObject("FxRimOverlay");
            go.transform.SetParent(source.transform, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = source.sharedMesh;
            smr.bones = source.bones;
            smr.rootBone = source.rootBone;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;
            smr.sharedMaterial = mat;   // 인스턴스 소유 — Release에서 함께 파괴(누출 금지)
            return go;
        }

        /// <summary>[무번호 29-P1-2] 정적 메시 림 오버로드 — 무기(장착 GLB MeshRenderer)용.
        /// 소스 MeshFilter의 sharedMesh를 공유하는 자식 오버레이(로컬 항등 변환). 셰이더 부재=null 폴백.</summary>
        public static GameObject AttachRimStatic(GameObject sourceVisual, Color rimColor, float rimPower, float intensity)
        {
            if (sourceVisual == null) return null;
            var mf = sourceVisual.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;
            var mat = FxMaterialLibrary.CreateRimMaterial("FxRimOverlayMat", rimColor, rimPower, intensity);
            if (mat == null) return null;   // 폴백 계약 — 기존 emissive 경로 유지
            var go = new GameObject("FxRimOverlay");
            go.transform.SetParent(mf.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;   // 인스턴스 소유 — Release에서 함께 파괴(누출 금지)
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>림 색 갱신 — 오버레이 소유 머티리얼 인스턴스에 직접(공유 머티리얼 오염 금지 경로).
        ///  [무번호 36-PW3] 정적 오버로드(AttachRimStatic — MeshRenderer)도 지원(방어 림 골드→청색 전환 경로).</summary>
        public static void SetRimColor(GameObject overlay, Color rimColor)
        {
            if (overlay == null) return;
            var smr = overlay.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMaterial == null)
            {
                var mr = overlay.GetComponent<MeshRenderer>();   // [무번호 36-PW3] 정적 림 오버로드 대응
                if (mr == null || mr.sharedMaterial == null) return;
                if (mr.sharedMaterial.HasProperty("_RimColor")) mr.sharedMaterial.SetColor("_RimColor", rimColor);
                return;
            }
            if (smr.sharedMaterial.HasProperty("_RimColor")) smr.sharedMaterial.SetColor("_RimColor", rimColor);
        }

        /// <summary>림 강도 갱신(충전 임박 강화 등 — M1-2). [무번호 35-S2] 정적 오버로드(AttachRimStatic — MeshRenderer)도 지원.</summary>
        public static void SetRimIntensity(GameObject overlay, float intensity)
        {
            if (overlay == null) return;
            var smr = overlay.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMaterial == null)
            {
                var mr = overlay.GetComponent<MeshRenderer>();   // [무번호 35-S2] 정적 림 오버로드 대응(무기/정적 메시)
                if (mr == null || mr.sharedMaterial == null) return;
                if (mr.sharedMaterial.HasProperty("_RimIntensity")) mr.sharedMaterial.SetFloat("_RimIntensity", intensity);
                return;
            }
            if (smr.sharedMaterial.HasProperty("_RimIntensity")) smr.sharedMaterial.SetFloat("_RimIntensity", intensity);
        }

        /// <summary>오버레이 해제 — 머티리얼 인스턴스 + GO 파괴(EditMode 가드 — 21-K1 선례).
        ///  [무번호 35-S2] 정적 오버로드(AttachRimStatic — MeshRenderer) 머티리얼도 함께 파괴(누출 금지).</summary>
        public static void Release(GameObject overlay)
        {
            if (overlay == null) return;
            var smr = overlay.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMaterial != null)
            {
                if (Application.isPlaying) Object.Destroy(smr.sharedMaterial);
                else Object.DestroyImmediate(smr.sharedMaterial);
            }
            var mr = overlay.GetComponent<MeshRenderer>();
            if (smr == null && mr != null && mr.sharedMaterial != null)
            {
                if (Application.isPlaying) Object.Destroy(mr.sharedMaterial);
                else Object.DestroyImmediate(mr.sharedMaterial);
            }
            if (Application.isPlaying) Object.Destroy(overlay);
            else Object.DestroyImmediate(overlay);
        }
    }
}
