using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// [139차 P3] 피부 얼룩 원인 A/B 프로브 — 셀프섀도우(그림자 아크네) 가설 검증(수리 금지, 캡처 전용).
/// 근거: 139차 근접컷 판독에서 얼룩이 폴리곤 굴곡/면 겹침을 따라 발생(텍스처 소스 오염 패턴과 상이).
/// 시퀀스: NPC girl1 실경로 스폰 → 얼굴 1.2m 컷 3종 캡처:
///   ①A on  — 현행(SMR receiveShadows 기본+선향 그림자 on)
///   ②B off — SMR.receiveShadows=false(원복)
///   ③C lgt — 메인 방향광 shadows=None(원복, SMR on 유지)
/// 산출: TestOutput/npc139ab_{on,smroff,lgtoff}_girl1.png + npc139ab.txt
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Npc139ShadowABProbe.RunAll -quit -logFile TestOutput/npc139ab_unity.log
/// </summary>
public static class Npc139ShadowABProbe
{
    public static void RunAll()
    {
        var log = new StringBuilder();
        log.AppendLine("[139차 P3] 섀도우 A/B 프로브 시작 — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Directory.CreateDirectory("TestOutput");

        // NPC 스폰(실경로 조립 — 에디터 어셈블리는 Systems 직접 참조 가능)
        if (!RuntimeModelLoader.TryGetModel("girl1", out GameObject npcPrefab) || npcPrefab == null)
        {
            log.AppendLine("[139차 P3] FAIL — girl1 프리팹 로드 실패");
            File.WriteAllText("TestOutput/npc139ab.txt", log.ToString());
            return;
        }
        GameObject npc = Object.Instantiate(npcPrefab);
        npc.name = "P3AB_girl1";
        npc.transform.position = Vector3.zero;

        // 텍스처/키/애니 실경로 공용화(스폰 순서 = VillageNpcSpawner.SpawnNpc 선례).
        // EnsureNpcTexture는 private — 리플렉션 호출(NpcFacing136Probe 선례의 리플렉션 패턴 준용).
        {
            var spawnerType = typeof(VillageNpcSpawner);
            var miTex = spawnerType.GetMethod("EnsureNpcTexture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (miTex != null) miTex.Invoke(null, new object[] { npc, "girl1" });
            else Debug.LogWarning("[139차 P3] EnsureNpcTexture 리플렉션 실패 — 텍스처 자가부착 스킵");
        }
        VillageNpcSpawner.EnsureVillageNpcHeight(npc, 0f, "P3AB_girl1");
        VillageNpcSpawner.EnsureVillageNpcAnimation(npc, "girl1", "P3AB_girl1");

        // 얼굴 정면 1.2m 카메라(루트 forward측 — 근평면 침입 없는 거리)
        var camGo = new GameObject("P3AB_Cam");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 1.45f, 1.2f);
        cam.transform.LookAt(new Vector3(0f, 1.35f, 0f));
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 40f;

        // [139차 P3 보강] 배치 씬엔 태양광이 없다(139차 1차 실행 실측: sun=없음 → 3컷 동일) —
        //   인게임 방향광 등가 조명을 프로브 씬에 생성(그림자 Soft)해 A/B가 성립하게 한다.
        var sunGo = new GameObject("P3AB_Sun");
        var sunLight = sunGo.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.shadows = LightShadows.Soft;
        sunLight.intensity = 1.0f;
        sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);   // 대각 사광(자기 그림자 발생 각도)

        // [139차 P3 추가] 정점 노멀 통계 감사 — 메시 노멀 파손 가설 수치 확정(측정 전용).
        {
            var smr0 = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh0 = smr0 != null ? smr0.sharedMesh : null;
            if (mesh0 != null)
            {
                var norms = mesh0.normals;
                int zeroish = 0, nan = 0, total = norms.Length;
                Vector3 sum = Vector3.zero;
                var verts = mesh0.vertices;
                for (int i = 0; i < total; i++)
                {
                    Vector3 nn = norms[i];
                    float mag = nn.magnitude;
                    if (float.IsNaN(nn.x) || float.IsNaN(nn.y) || float.IsNaN(nn.z)) nan++;
                    else if (mag < 0.5f) zeroish++;
                    sum += nn;
                }
                // [139차 P3 확장] 방향성 통계 — 안쪽 지향 노멀 비율(메시 뒤집힘 판정).
                Bounds mb = mesh0.bounds;
                Vector3 center = mb.center;
                int inward = 0, outward = 0;
                for (int i = 0; i < total; i++)
                {
                    Vector3 rel = verts[i] - center;
                    float d = Vector3.Dot(norms[i], rel);
                    if (d < -0.01f) inward++;
                    else outward++;
                }
                log.AppendLine($"[139차 P3] 노멀 방향성 — inward={inward}({100f * inward / Mathf.Max(1, total):F1}%) " +
                               $"outward={outward} — inward>30%면 뒤집힘(안쪽 노멀) 의심");

                // [140차 확장] 부위별 inward 비율 — 얼굴/상체/하체 아일랜드 단위 뒤집힘 특정(얼굴에만 얼룩 증상과 대조).
                int headIn = 0, headOut = 0, torsoIn = 0, torsoOut = 0, legsIn = 0, legsOut = 0;
                for (int i = 0; i < total; i++)
                {
                    Vector3 rel = verts[i] - center;
                    float d = Vector3.Dot(norms[i], rel);
                    bool isIn = d < -0.01f;
                    // 로컬 공간 상대 3분할(루트본 피벗이 발 근처가 아님 — localBounds.y=[-0.61~0.57] 실측):
                    //   상단 1/3=머리, 중간 1/3=몸통, 하단 1/3=하체.
                    float relY = (verts[i].y - mb.min.y) / Mathf.Max(0.001f, mb.size.y);
                    if (relY > 0.667f) { if (isIn) headIn++; else headOut++; }
                    else if (relY > 0.333f) { if (isIn) torsoIn++; else torsoOut++; }
                    else { if (isIn) legsIn++; else legsOut++; }
                }
                log.AppendLine($"[140차] 부위별 inward — 머리 {headIn}/{headIn + headOut}({100f * headIn / Mathf.Max(1, headIn + headOut):F1}%) " +
                               $"몸통 {torsoIn}/{torsoIn + torsoOut}({100f * torsoIn / Mathf.Max(1, torsoIn + torsoOut):F1}%) " +
                               $"하체 {legsIn}/{legsIn + legsOut}({100f * legsIn / Mathf.Max(1, legsIn + legsOut):F1}%)");
                // UV 아일랜드 이웃 정합성 — 같은 아일랜드(근접 UV) 내 노멀 일관성은 생략(부위별 비율로 1차 판정).

                // [140차 추가] 중복 지오메트리 감사 — Unlit에선 안 보이고 Lit에서만 얼룩인 대체 설명:
                //   같은 텍스처를 입은 겹친 지오메트리(Unlit=동일 픽셀 무증상 / Lit=셰이딩 차이로 얼룩).
                //   렌더러 전수(타입/이름/메시 참조 동일성/겹침 bounds) 덤프.
                var allRends = npc.GetComponentsInChildren<Renderer>(true);
                log.AppendLine($"[140차] 렌더러 전수={allRends.Length}");
                var meshRefs = new System.Collections.Generic.Dictionary<Mesh, int>();
                foreach (var r in allRends)
                {
                    Mesh mref = null;
                    if (r is SkinnedMeshRenderer s) mref = s.sharedMesh;
                    else if (r is MeshRenderer mr)
                    {
                        var mfilter = mr.GetComponent<MeshFilter>();
                        mref = mfilter != null ? mfilter.sharedMesh : null;
                    }
                    string mname = mref != null ? mref.name : "없음";
                    if (mref != null)
                    {
                        if (!meshRefs.ContainsKey(mref)) meshRefs[mref] = 0;
                        meshRefs[mref]++;
                    }
                    Bounds rb = r.bounds;
                    log.AppendLine($"    [140차] {r.GetType().Name} '{r.name}' mesh='{mname}' " +
                                   $"bounds=({rb.min.x:F2},{rb.min.y:F2},{rb.min.z:F2})~({rb.max.x:F2},{rb.max.y:F2},{rb.max.z:F2})");
                }
                foreach (var kvp in meshRefs)
                    if (kvp.Value > 1)
                        log.AppendLine($"    [140차] ⚠️ 동일 메시 다중 렌더러(중복 지오메트리): '{kvp.Key.name}' ×{kvp.Value} — Z-fighting/Lit 셰이딩 차이 유력");
            }
        }

        var rt = new RenderTexture(1240, 828, 24);
        cam.targetTexture = rt;
        cam.Render();
        Capture(cam, rt, "TestOutput/npc139ab_on_girl1.png");
        log.AppendLine("[139차 P3] ①A on — npc139ab_on_girl1.png (현행)");

        // ②B — SMR receiveShadows off
        var smrs = npc.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        bool[] prevReceive = new bool[smrs.Length];
        for (int i = 0; i < smrs.Length; i++)
        {
            prevReceive[i] = smrs[i].receiveShadows;
            smrs[i].receiveShadows = false;
        }
        cam.Render();
        Capture(cam, rt, "TestOutput/npc139ab_smroff_girl1.png");
        log.AppendLine($"[139차 P3] ②B SMR receiveShadows off — npc139ab_smroff_girl1.png (smr={smrs.Length})");
        for (int i = 0; i < smrs.Length; i++) smrs[i].receiveShadows = prevReceive[i];   // 원복

        // ③C — 방향광 shadows off(프로브 태양광)
        LightShadows prevSunShadows = sunLight.shadows;
        sunLight.shadows = LightShadows.None;
        cam.Render();
        Capture(cam, rt, "TestOutput/npc139ab_lgtoff_girl1.png");
        log.AppendLine("[139차 P3] ③C 방향광 shadows=None — npc139ab_lgtoff_girl1.png (sun=P3AB_Sun)");
        sunLight.shadows = prevSunShadows;   // 원복

        // ④D — 셰이딩 배제(URP/Unlit + base_color 직결): 얼룩 잔존=UV/메시-텍스처 정합, 소멸=셰이딩 상호작용.
        //   [139차 1차 A/B 판정] on==smroff(수신 그림자 무효과), lgtoff에서도 얼룩 잔존 → 그림자 아님 확정 후속 감별.
        {
            var smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMaterial != null)
            {
                var prevMat = smr.sharedMaterial;
                var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                var baseTex = prevMat.mainTexture != null ? prevMat.mainTexture : prevMat.GetTexture("_BaseMap");
                unlit.mainTexture = baseTex;
                smr.material = unlit;   // 인스턴스 교체 — 원본 무손상
                cam.Render();
                Capture(cam, rt, "TestOutput/npc139ab_unlit_girl1.png");
                log.AppendLine($"[139차 P3] ④D Unlit(base_color 직결) — npc139ab_unlit_girl1.png (tex={(baseTex != null ? baseTex.name : "null")})");
                smr.material = prevMat;   // 원복
            }
            else log.AppendLine("[139차 P3] ④D SKIP — SMR/머티리얼 부재");
        }

        // ⑤E — Lit 유지 + 노멀 슬롯 클리어: 청결=노멀맵 바인딩(탄젠트 부재 포함)이 원인 확정.
        {
            var smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMaterial != null)
            {
                var prevMat = smr.sharedMaterial;
                var litNoNormal = new Material(prevMat);   // 인스턴스 복제
                var bumpProp = litNoNormal.HasProperty("_BumpMap") ? "_BumpMap" : (litNoNormal.HasProperty("_NormalMap") ? "_NormalMap" : null);
                Texture prevBump = bumpProp != null ? litNoNormal.GetTexture(bumpProp) : null;
                if (bumpProp != null) litNoNormal.SetTexture(bumpProp, null);
                if (litNoNormal.HasProperty("_BumpScale")) litNoNormal.SetFloat("_BumpScale", 0f);
                // [139차 P3 추가] 전체 텍스처 바인딩 덤프 — 추가 맵(AO/마스크/EM 등) 오염원 특정 데이터.
                var so = new UnityEditor.SerializedObject(litNoNormal);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType == UnityEditor.SerializedPropertyType.ObjectReference
                        && it.objectReferenceValue is Texture boundTex)
                        log.AppendLine($"    [139차 P3] 바인딩 덤프 — {it.name} = {boundTex.name}");
                }

                smr.material = litNoNormal;
                cam.Render();
                Capture(cam, rt, "TestOutput/npc139ab_litnonormal_girl1.png");
                log.AppendLine($"[139차 P3] ⑤E Lit+노멀클리어 — npc139ab_litnonormal_girl1.png (bumpPrev={(prevBump != null ? prevBump.name : "무바인딩")})");
                smr.material = prevMat;   // 원복

                // ⑥F — AO(_OcclusionMap) 클리어 추가: E에서도 잔존한 얼룩의 다음 용의자(A4 이후 미검증 슬롯).
                if (smr.sharedMaterial != null && smr.sharedMaterial.HasProperty("_OcclusionMap") && smr.sharedMaterial.GetTexture("_OcclusionMap") != null)
                {
                    var prevMat2 = smr.sharedMaterial;
                    var litNoAO = new Material(prevMat2);
                    litNoAO.SetTexture("_OcclusionMap", null);
                    litNoAO.SetFloat("_OcclusionStrength", 0f);
                    if (litNoAO.HasProperty("_BumpMap")) litNoAO.SetTexture("_BumpMap", null);
                    if (litNoAO.HasProperty("_BumpScale")) litNoAO.SetFloat("_BumpScale", 0f);
                    smr.material = litNoAO;
                    cam.Render();
                    Capture(cam, rt, "TestOutput/npc139ab_litnoao_girl1.png");
                    log.AppendLine("[139차 P3] ⑥F Lit+노멀+AO클리어 — npc139ab_litnoao_girl1.png");
                    smr.material = prevMat2;   // 원복
                }
                else log.AppendLine("[139차 P3] ⑥F SKIP — AO 바인딩 없음");
            }
        }

        // ⑦G — 메탈릭/스무스니스 0: 금속 표면이 반사 환경 없는 씬에서 검은 얼룩처럼 보이는 경우 감별.
        {
            var smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMaterial != null)
            {
                var prevMat = smr.sharedMaterial;
                var litMatte = new Material(prevMat);
                if (litMatte.HasProperty("_Metallic")) litMatte.SetFloat("_Metallic", 0f);
                if (litMatte.HasProperty("_Smoothness")) litMatte.SetFloat("_Smoothness", 0.2f);
                if (litMatte.HasProperty("_Glossiness")) litMatte.SetFloat("_Glossiness", 0.2f);
                smr.material = litMatte;
                cam.Render();
                Capture(cam, rt, "TestOutput/npc139ab_matte_girl1.png");
                log.AppendLine("[139차 P3] ⑦G Lit+메탈릭0 — npc139ab_matte_girl1.png");
                smr.material = prevMat;   // 원복
            }
        }

        // ⑧H — 라이트프로브/반사프로브 OFF: Lit 전용 잔차의 다음 용의자(프로브 샘플링 가비지 → 얼룩).
        {
            var smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                var prevLP = smr.lightProbeUsage;
                var prevRP = smr.reflectionProbeUsage;
                smr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                smr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                cam.Render();
                Capture(cam, rt, "TestOutput/npc139ab_noprobe_girl1.png");
                log.AppendLine("[139차 P3] ⑧H 라이트/반사프로브 OFF — npc139ab_noprobe_girl1.png");
                smr.lightProbeUsage = prevLP;   // 원복
                smr.reflectionProbeUsage = prevRP;
            }
        }

        log.AppendLine("[139차 P3] DONE — 원복 완료(광원/SMR/머티리얼). 판정은 감독자 vision.");
        Debug.Log(log.ToString());
        File.WriteAllText("TestOutput/npc139ab.txt", log.ToString());

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(sunGo);   // 프로브 조명 정리(씬 오염 없음)
        Object.DestroyImmediate(npc);
    }

    private static void Capture(Camera cam, RenderTexture rt, string path)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        RenderTexture.active = null;
    }
}
