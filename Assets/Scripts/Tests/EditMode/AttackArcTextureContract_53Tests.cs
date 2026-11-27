using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 53-P2] AttackArcVFX 리본 아크 고품질 계약테스트 — 절차 슬래시 텍스처+UV 매핑+알파 계층.
    /// 배경(11-16 사용자 영상 실측): 텍스처 없는 additive 흰 리본이 불투명 폴리곤 덩어리로 판독.
    ///  ①텍스처 결정론(동일 호출 동일 픽셀 — Random 0건) ②UV 정합(정점수=UV수·v 좌0/우1)
    ///  ③머티리얼 바인딩(_BaseMap/_MainTex 존재) ④틴트 무손상(스테이지1=흰) ⑤글로우 알파 핀(0.30→0.15 절반).
    /// Unity 배치 실행 없음 — EditMode 검증만(컴파일/Play는 부모 단일 실행·별도 승인).
    /// </summary>
    public class AttackArcTextureContract53Tests
    {
        private GameObject _hostGo;

        [TearDown]
        public void TearDown()
        {
            if (_hostGo != null)
            {
                Object.DestroyImmediate(_hostGo);
                _hostGo = null;
            }
        }

        // ================================================================
        // ① 텍스처 결정론 — 동일 산식 동일 픽셀 / 캐시 1회 / 스펙(256×64 RGBA, Clamp, Bilinear, mip 없음)
        // ================================================================

        [Test]
        public void SlashTexture_DeterministicPixels()
        {
            var a = new Color[AttackArcVFX.SlashTexWidth * AttackArcVFX.SlashTexHeight];
            var b = new Color[AttackArcVFX.SlashTexWidth * AttackArcVFX.SlashTexHeight];
            AttackArcVFX.FillSlashPixels(a, AttackArcVFX.SlashTexWidth, AttackArcVFX.SlashTexHeight);
            AttackArcVFX.FillSlashPixels(b, AttackArcVFX.SlashTexWidth, AttackArcVFX.SlashTexHeight);
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a)
                    Assert.Fail($"[53-P2] 결정론 위반 — 픽셀 {i} 불일치 ({a[i]} vs {b[i]}). Random/상태 의존 산식 금지.");
            }
        }

        [Test]
        public void SlashTexture_CacheOnce_AndSpec()
        {
            Texture2D t1 = AttackArcVFX.GetSlashTexture();
            Texture2D t2 = AttackArcVFX.GetSlashTexture();
            Assert.That(t2, Is.SameAs(t1), "[53-P2] 정적 캐시 1회 — 재호출 재생성 금지(GetStarTexture 선례).");
            Assert.That(t1.width, Is.EqualTo(256), "[53-P2] 길이(u) 256.");
            Assert.That(t1.height, Is.EqualTo(64), "[53-P2] 폭(v) 64.");
            Assert.That(t1.format, Is.EqualTo(TextureFormat.RGBA32), "[53-P2] RGBA32.");
            Assert.That(t1.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), "[53-P2] Clamp(종단 사각 번짐 방지).");
            Assert.That(t1.filterMode, Is.EqualTo(FilterMode.Bilinear), "[53-P2] Bilinear(페더 부드러움).");
            Assert.That(t1.mipmapCount, Is.EqualTo(1), "[53-P2] mip 없음(파티클 텍스처 선례).");
        }

        [Test]
        public void SlashTexture_Profiles_FeatherTailCore()
        {
            const int w = AttackArcVFX.SlashTexWidth;
            const int h = AttackArcVFX.SlashTexHeight;
            var px = new Color[w * h];
            AttackArcVFX.FillSlashPixels(px, w, h);
            int centerRow = h / 2;

            // v(폭)방향 — 중심(코어)이 최대, 가장자리는 소멸(≈0 — additive 잔존 무의미 수준), 중심→가장자리 단조 감쇠.
            float center = px[centerRow * w + (w - 1)].a;
            float quarter = px[(h / 4) * w + (w - 1)].a;
            float edge = px[0 * w + (w - 1)].a;
            Assert.That(center, Is.GreaterThan(quarter), "[53-P2] 폭방향 페더 — 중심 > 1/4 높이.");
            Assert.That(quarter, Is.GreaterThan(edge), "[53-P2] 폭방향 페더 — 1/4 높이 > 가장자리.");
            Assert.That(edge, Is.LessThan(0.01f), "[53-P2] 가장자리 소멸(하드 사각 엣지 금지 — 11-16 실측 원인).");

            // u(길이)방향 — 중심 행에서 전두엽(u=1)이 꼬리(u≈0.1)보다 밝고, +x 단조 증가(꼬리 페이드).
            // [무번호 57-P1 계약 진화] 단조 구간을 u<0.92로 한정 — 선단 8%는 부채꼴 끝 빠짐(소프트 감쇠)이
            //   계약("지면에 깔린 불투명 흰 다이아몬드" 해소 — 공격씬 10 실측). 구 단조 전체 핀 폐기.
            float tail = px[centerRow * w + (w / 10)].a;
            float head = px[centerRow * w + (w - 1)].a;
            Assert.That(head, Is.GreaterThan(tail), "[53-P2/57-P1] 길이방향 — 전두엽 > 꼬리(페이드 유지).");
            int monotonicEnd = (int)(w * 0.92f);
            for (int x = 1; x < monotonicEnd; x++)
            {
                float prev = px[centerRow * w + (x - 1)].a;
                float curr = px[centerRow * w + x].a;
                Assert.That(curr, Is.GreaterThanOrEqualTo(prev),
                    $"[53-P2/57-P1] 길이방향 단조 증가 위반(u<0.92) — x={x} ({prev} → {curr}).");
            }
            // 선단 감쇠 구간 — u≥0.92에서 알파가 캡(0.62) 이하로 유지되며 급격한 상승 없음(하드 벽 금지).
            float frontMax = 0f;
            for (int x = monotonicEnd; x < w; x++)
                frontMax = Mathf.Max(frontMax, px[centerRow * w + x].a);
            Assert.That(frontMax, Is.LessThanOrEqualTo(AttackArcVFX.AlphaCap + 0.01f),
                "[57-P1] 선단 구간 알파 ≤ 캡 — 불투명 다이아몬드 모서리 금지.");

            // 중심 코어 밴드 — 같은 u에서 중심 행이 가우시안 꼬리부보다 유의하게 밝음(검날 판독).
            float coreBand = px[centerRow * w + (w / 2)].a;
            float offBand = px[(h * 3 / 8) * w + (w / 2)].a;
            Assert.That(coreBand, Is.GreaterThan(offBand), "[53-P2] 중심 코어 밴드 존재.");

            // 텍스처 실측 = 순수 산식 재현(RGBA32 양자화 허용오차) — 생성 경로 단일 소스.
            Texture2D tex = AttackArcVFX.GetSlashTexture();
            var texPixels = tex.GetPixels();
            Assert.That(texPixels.Length, Is.EqualTo(px.Length), "[53-P2] 텍스처 픽셀 수 일치.");
            for (int i = 0; i < px.Length; i++)
            {
                Assert.That(texPixels[i].a, Is.EqualTo(px[i].a).Within(2f / 255f),
                    $"[53-P2] 텍스처 알파가 FillSlashPixels 산식과 불일치 — 픽셀 {i}.");
            }
        }

        [Test]
        public void SlashTexture_Source_NoRandom()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackArcVFX.cs");
            int start = src.IndexOf("[무번호 53-P2] 절차 슬래시 텍스처", System.StringComparison.Ordinal);
            int end = src.IndexOf("private static void EnsureHost()", System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "[53-P2] 슬래시 텍스처 블록 마커 존재.");
            Assert.That(end, Is.GreaterThan(start), "[53-P2] 블록 범위 판정 가능.");
            string block = src.Substring(start, end - start);
            Assert.That(block, Does.Not.Contain("UnityEngine.Random"), "[53-P2] Random 직접 사용 금지(결정론 계약).");
            Assert.That(block, Does.Not.Contain("Random.Range"), "[53-P2] Random.Range 금지(결정론 계약).");
            Assert.That(block, Does.Contain("TextureFormat.RGBA32"), "[53-P2] RGBA32 생성.");
            Assert.That(src, Does.Contain("internal const int SlashTexWidth = 256;"), "[53-P2] 폭 256 핀.");
            Assert.That(src, Does.Contain("internal const int SlashTexHeight = 64;"), "[53-P2] 높이 64 핀.");
        }

        // ================================================================
        // ② UV 정합 — 정점수=UV수(공유 커서)·v 좌0/우1·u=샘플 진행도
        // ================================================================

        [Test]
        public void EmitPass_UvCountMatchesVertices_AndVIs01()
        {
            _hostGo = new GameObject("AttackArcHost53_UV");
            var host = _hostGo.AddComponent<AttackArcHost>();

            var ty = typeof(AttackArcHost);
            SetInstanceField(ty, host, "_px", new float[] { 0f, 0.3f, 0.6f, 9f });
            SetInstanceField(ty, host, "_py", new float[] { 1f, 1f, 1f, 9f });
            SetInstanceField(ty, host, "_pz", new float[] { 0f, 0f, 0f, 9f });
            SetInstanceField(ty, host, "_birth", new float[] { 0.5f, 0.5f, 0.5f, 0f });
            SetInstanceField(ty, host, "_cap", 4);
            SetInstanceField(ty, host, "_first", 0);
            SetInstanceField(ty, host, "_count", 3);
            SetInstanceField(ty, host, "_lastRight", Vector3.right);

            var emit = ty.GetMethod("EmitPass", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(emit, Is.Not.Null, "[53-P2] EmitPass 존재(리플렉션 진입점 — Cave156FallSafety 선례).");

            const int vcap = 3 * 4;         // 샘플×2정점×2패스 상한 중 1패스 호출
            const int tcap = (3 - 1) * 24;
            var vArr = new Vector3[vcap];
            var cArr = new Color[vcap];
            var tArr = new int[tcap];
            var uvArr = new Vector2[vcap];
            var w = new[] { 0, 0 };
            var args = new object[] { 1.0f, 1.0f, 0.1f, 1.0f, 1.0f, Color.white, vArr, cArr, tArr, uvArr, w };
            emit.Invoke(host, args);

            Assert.That(w[0], Is.EqualTo(6), "[53-P2] 생존 샘플 3×2정점 — 정점 커서.");
            Assert.That(w[1], Is.EqualTo(24), "[53-P2] 인접 2쿼드×양권선 12idx — 삼각 커서.");
            // 정점수=UV수 — UV가 정점 커서를 공유(계약의 구조적 보장).
            for (int i = 0; i < w[0]; i++)
            {
                bool vertWritten = vArr[i].sqrMagnitude > 0f;
                Assert.That(vertWritten, Is.True, $"[53-P2] 정점 {i} 미기록 — 커서 정합 붕괴.");
                float expectedV = (i % 2 == 0) ? 0f : 1f;   // 좌(짝)=0 / 우(홀)=1
                Assert.That(uvArr[i].y, Is.EqualTo(expectedV), $"[53-P2] 정점 {i}의 v — 좌0/우1 계약.");
                Assert.That(uvArr[i].x, Is.InRange(0f, 1f), $"[53-P2] 정점 {i}의 u 범위.");
            }
            // u=샘플 진행도 — k=0(꼬리)0 → k=2(전두엽)1.
            Assert.That(uvArr[0].x, Is.EqualTo(0f).Within(1e-5f), "[53-P2] 첫 샘플 u=0(꼬리).");
            Assert.That(uvArr[1].x, Is.EqualTo(0f).Within(1e-5f), "[53-P2] 첫 샘플 우정점 동일 u.");
            Assert.That(uvArr[2].x, Is.EqualTo(0.5f).Within(1e-5f), "[53-P2] 중간 샘플 u=0.5.");
            Assert.That(uvArr[4].x, Is.EqualTo(1f).Within(1e-5f), "[53-P2] 마지막 샘플 u=1(전두엽).");
            // 색 경로 무변경 — life01=0.5×alphaMul=1.
            for (int i = 0; i < w[0]; i++)
                Assert.That(cArr[i].a, Is.EqualTo(0.5f).Within(1e-4f), $"[53-P2] 정점 {i} 알파(정점색 페이드 계약 유지).");
        }

        [Test]
        public void Source_MeshUvAssignedWithVertices()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackArcVFX.cs");
            Assert.That(src, Does.Contain("Vector2[] uvArr = new Vector2[vcap];"),
                "[53-P2] RebuildRibbon UV 버퍼 할당.");
            Assert.That(src, Does.Contain("Vector2[] uvOut = new Vector2[vw];"),
                "[53-P2] UV 슬라이스 할당(정점 슬라이스와 동일 길이).");
            Assert.That(System.Text.RegularExpressions.Regex.Matches(src, "_mesh\\.uv = uvOut;").Count,
                Is.EqualTo(2), "[53-P2] 메시 uv 대입 — 축소/확대 양분기.");

            // [45차] 대입 순서 계약 유지 — 축소 분기: triangles → vertices → uv(정점과 함께) → colors.
            int shrinkIdx = src.IndexOf("if (vOut.Length < _mesh.vertexCount)", System.StringComparison.Ordinal);
            Assert.That(shrinkIdx, Is.GreaterThan(0), "[53-P2] 축소 분기 존재([45-RIBBON] 계약).");
            int triIdx = src.IndexOf("_mesh.triangles = tOut;", shrinkIdx, System.StringComparison.Ordinal);
            int vertIdx = src.IndexOf("_mesh.vertices = vOut;", shrinkIdx, System.StringComparison.Ordinal);
            int uvIdx = src.IndexOf("_mesh.uv = uvOut;", shrinkIdx, System.StringComparison.Ordinal);
            int colIdx = src.IndexOf("_mesh.colors = cOut;", shrinkIdx, System.StringComparison.Ordinal);
            Assert.That(triIdx, Is.LessThan(vertIdx), "[45] 축소 시 삼각형 먼저(기존 계약 무손상).");
            Assert.That(vertIdx, Is.LessThan(uvIdx), "[53-P2] UV는 정점과 함께(정점 직후).");
            Assert.That(uvIdx, Is.LessThan(colIdx), "[53-P2] UV는 색보다 먼저(정점-색 사이).");

            // EmitPass UV 산식 — u=샘플 진행도, v=좌0/우1.
            Assert.That(src, Does.Contain("float u = _count <= 1 ? 1f : (float)k / (_count - 1);"),
                "[53-P2] u=샘플 진행도 산식.");
            Assert.That(src, Does.Contain("uvArr[baseIdx] = new Vector2(u, 0f);"),
                "[53-P2] 좌 정점 v=0.");
            Assert.That(src, Does.Contain("uvArr[baseIdx + 1] = new Vector2(u, 1f);"),
                "[53-P2] 우 정점 v=1.");
            int callIdx = src.IndexOf("EmitPass(now, trailSec, halfW, 1.65f, GlowPassAlphaMul", System.StringComparison.Ordinal);
            Assert.That(callIdx, Is.GreaterThan(0), "[53-P2] 글로우 패스 호출 존재.");
            Assert.That(src.IndexOf("fArr, cArr, qArr, uvArr, w);", callIdx, System.StringComparison.Ordinal),
                Is.GreaterThan(callIdx), "[53-P2] EmitPass에 uvArr 전달(tArr/uvArr 순서 계약).");
        }

        // ================================================================
        // ③ 머티리얼 바인딩 — _BaseMap/_MainTex = 슬래시 텍스처, additive 계약 유지
        // ================================================================

        [Test]
        public void Material_BindsSlashTexture_Runtime()
        {
            Texture2D slash = AttackArcVFX.GetSlashTexture();
            Material mat = WeaponSwingTrail.CreateTrailMaterialShared(1f);   // 공개 팩토리 — [26-ST2] 위임자 실측 경로
            Assert.That(mat, Is.Not.Null, "[53-P2] 트레일 머티리얼 생성.");
            bool bound = (mat.HasProperty("_BaseMap") && ReferenceEquals(mat.GetTexture("_BaseMap"), slash))
                      || (mat.HasProperty("_MainTex") && ReferenceEquals(mat.GetTexture("_MainTex"), slash));
            Assert.That(bound, Is.True, "[53-P2] 머티리얼에 슬래시 텍스처 바인딩(_BaseMap/_MainTex).");
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void Material_Source_BindingAndBlendContractPreserved()
        {
            string arcSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackArcVFX.cs");
            string wsSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/WeaponSwingTrail.cs");

            // 바인딩 배선 — 폴백 머티리얼(AttackArcVFX)과 트레일 머티리얼(WeaponSwingTrail) 모두.
            Assert.That(arcSrc, Does.Contain("AttackArcVFX.BindSlashTexture(mat);"),
                "[53-P2] 아크 폴백 머티리얼 바인딩 호출.");
            Assert.That(arcSrc, Does.Contain("AttackArcVFX.BindSlashTexture(fallbackMat);"),
                "[53-P2] 폴백(Sprites/Default) 경로도 바인딩 — 마젠타 아님 계약 유지.");
            Assert.That(wsSrc, Does.Contain("AttackArcVFX.BindSlashTexture(mat);"),
                "[53-P2] WeaponSwingTrail 트레일 머티리얼 바인딩 호출(구조·width 곡선 무변경 — 바인딩만 추가).");
            Assert.That(wsSrc, Does.Contain("width.AddKey(0.6f, 0.65f);"),
                "[53-P2] [45차 P4] 3키 폭 곡선 무손상.");

            // [무번호 54-P1 계약 진화] additive → Alpha 블렌드 — 양 파일 모두 URP Particles/Unlit + _Surface=1
            //   + _Blend=0 + SrcAlpha/OneMinusSrcAlpha + ZWrite 0. 구 Additive 핀(53차)은 밝은 지면 위 순백 포화
            //   실측(공격씬 7·8)으로 폐기 — Alpha 블렌드에서 텍스처 페더·글로우 α계층이 실가시된다.
            foreach (var kv in new[] { ("AttackArcVFX", arcSrc), ("WeaponSwingTrail", wsSrc) })
            {
                string src = kv.Item2;
                Assert.That(src, Does.Contain("\"Universal Render Pipeline/Particles/Unlit\""),
                    $"[53-P2/54-P1] {kv.Item1} URP 셰이더 1순위 계약.");
                Assert.That(src, Does.Contain("Shader.Find(\"Sprites/Default\")"),
                    $"[53-P2/54-P1] {kv.Item1} 폴백 계약.");
                Assert.That(src, Does.Contain("mat.SetFloat(\"_Surface\", 1f)"),
                    $"[53-P2/54-P1] {kv.Item1} Transparent.");
                Assert.That(src, Does.Contain("mat.SetFloat(\"_Blend\", 0f)"),
                    $"[53-P2/54-P1] {kv.Item1} Alpha 블렌드(54차 진화 — 구 Additive=2 폐기).");
                Assert.That(src, Does.Contain("BlendMode.SrcAlpha"), $"[53-P2/54-P1] {kv.Item1} SrcAlpha.");
                Assert.That(src, Does.Contain("BlendMode.OneMinusSrcAlpha"), $"[53-P2/54-P1] {kv.Item1} OneMinusSrcAlpha(54차 진화).");
                Assert.That(src, Does.Contain("mat.SetFloat(\"_ZWrite\", 0f)"), $"[53-P2/54-P1] {kv.Item1} ZWrite off.");
            }
        }

        // ================================================================
        // ④ 틴트 무손상 — ResolveTint(무기 기본색 + 콤보 스테이지 오버라이드) 계약
        // ================================================================

        [Test]
        public void Tint_ResolveTint_ContractUnchanged()
        {
            _hostGo = new GameObject("AttackArcHost53_Tint");
            var host = _hostGo.AddComponent<AttackArcHost>();
            var resolve = typeof(AttackArcHost).GetMethod("ResolveTint", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null, "[53-P2] ResolveTint 존재(리플렉션 진입점).");

            host.Tint(WeaponType.Spear, 1);
            AssertColor((Color)resolve.Invoke(host, null), 1f, 1f, 1f, 1f,
                "[53-P2] 콤보 스테이지1=흰 — 틴트 계약 무손상.");

            host.Tint(WeaponType.Spear, 0);
            AssertColor((Color)resolve.Invoke(host, null), 0.3f, 0.6f, 1f, 1f,
                "[53-P2] 무기 기본색(창=청) 계약 무손상.");

            host.Tint(WeaponType.Spear, 2);
            AssertColor((Color)resolve.Invoke(host, null), 1f, 0.85f, 0.45f, 1f,
                "[53-P2] 스테이지2=골드 무손상.");

            host.Tint(WeaponType.Spear, 3);
            AssertColor((Color)resolve.Invoke(host, null), 1f, 0.5f, 0.2f, 1f,
                "[53-P2] 스테이지3=주황 무손상.");

            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackArcVFX.cs");
            Assert.That(src, Does.Contain("Stage 1: 흰"), "[53-P2] ResolveTint 스테이지1 흰 계약 문자열 잠금.");
        }

        /// <summary>색상 성분별 판정 — Color 구조체에 Within 멤버별 허용오차가 적용되지 않는 NUnit 특성 회피.</summary>
        private static void AssertColor(Color actual, float r, float g, float b, float a, string message)
        {
            Assert.That(actual.r, Is.EqualTo(r).Within(1e-5f), message + " (r)");
            Assert.That(actual.g, Is.EqualTo(g).Within(1e-5f), message + " (g)");
            Assert.That(actual.b, Is.EqualTo(b).Within(1e-5f), message + " (b)");
            Assert.That(actual.a, Is.EqualTo(a).Within(1e-5f), message + " (a)");
        }

        // ================================================================
        // ⑤ 알파 계층 — 글로우 패스 절반 핀(코어 유지)
        // ================================================================

        [Test]
        public void GlowAlphaMul_PinnedHalf_CoreUntouched()
        {
            Assert.That(AttackArcHost.GlowPassAlphaMul, Is.EqualTo(0.15f).Within(1e-6f),
                "[53-P2] 글로우 alphaMul 소스계약 핀 — 0.30의 절반(additive 포화 완화).");
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackArcVFX.cs");
            Assert.That(src, Does.Contain("EmitPass(now, trailSec, halfW, 1.65f, GlowPassAlphaMul"),
                "[53-P2] 글로우 패스가 핀 상수 사용.");
            // [무번호 57-P1 계약 진화] 코어 α 상한 0.6 — 공격씬 10 실측(strike 지점 불투명 흰 다이아몬드)으로
            //   53차의 α×1.0 유지 핀 폐기. GlowPassAlphaMul(0.15) 핀은 유지.
            Assert.That(src, Does.Contain("EmitPass(now, trailSec, halfW, 1.0f, CorePassAlphaMul, coreTint"),
                "[53-P2/57-P1] 코어 패스 상한 상수 경유(α 1.0→0.6 진화).");
        }

        private static void SetInstanceField(System.Type type, object instance, string name, object value)
        {
            var f = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, $"[53-P2] 필드 {name} 존재(리플렉션 진입점).");
            f.SetValue(instance, value);
        }
    }
}
