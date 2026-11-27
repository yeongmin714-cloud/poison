using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 29-D2/D3] FX 머티리얼 단일 빌더 — 소프트 파티클(카메라 깊이 페이드)·플립북 연기·프레넬 림.
    /// 계약: URP 파티클 셰이더 부재 환경은 Sprites/Default 폴백(기존 룩 무손상 — 계획 D-2 주의항),
    /// 머티리얼 인스턴스는 호출자/오버레이 소유(누출 금지), Random 0건.
    /// </summary>
    public static class FxMaterialLibrary
    {
        /// <summary>URP 파티클 셰이더(소프트 파티클 내장 — _SoftParticlesEnabled 키워드).</summary>
        public const string SoftShaderName = "Universal Render Pipeline/Particles/Unlit";
        /// <summary>프레넬 림 오버레이 셰이더(29-D3).</summary>
        public const string RimShaderName = "ProjectName/FX/FresnelRim";

        /// <summary>URP 소프트 파티클 셰이더 가용 여부(폴백 판정용 — 검증 로그 대상).</summary>
        public static bool SoftParticleSupported => Shader.Find(SoftShaderName) != null;

        /// <summary>
        /// 소프트 파티클 머티리얼 — URP Particles/Unlit + 깊이 페이드. 셰이더 미발견/속성 부재 시
        /// Sprites/Default 폴백(기존 DustPuffMat/ScorchEmberMat 룩과 동일 경로 — 무손상).
        /// </summary>
        /// <summary>[무번호 47-a] 런타임 렌더러 머티리얼 대입 정규화 경로 — 세션 파손 NRE(기본 머티리얼 접촉)
        /// 방지 단일 관문. 45차 실측: 사용자 세션에서 렌더러 머티리얼 세터가 NRE를 던지며 대입이 실패하고,
        /// 부분 갱신 사례(디졸브 교체 중단)에서 슬롯이 null로 남아 마젠타 시체가 렌더됐다.
        /// 계약: ①배열 대입 사용(단일/인덱서 세터의 내부 접촉 회피) ②실패 시 **원본 배열 복원**(null화 금지
        /// — 마젠타 렌더 원천 차단, 시체는 기존 머티리얼 유지) ③실패 경고 1급 로그(context 동반, 스팸 방지
        /// 스로틀 3s) ④재시도 없음 — 세션 파손은 결정론적이라 재시도는 무의미한 로그만 남긴다(근거 주석).
        /// 반환: 대입 성공 여부 — 호출부는 false 시 자체 폴백(에셋 프리팹/원본 유지)을 진행한다.</summary>
        public static bool TryAssignSharedMaterials(Renderer rend, Material[] mats, string context)
        {
            if (rend == null || mats == null) return false;
            Material[] original = null;
            try { original = rend.sharedMaterials; } catch (System.Exception) { original = null; }
            try
            {
                rend.sharedMaterials = mats;
                return true;
            }
            catch (System.Exception ex)
            {
                if (original != null)
                {
                    try { rend.sharedMaterials = original; } catch (System.Exception) { }
                }
                if (Time.unscaledTime >= _matGuardNextWarn)
                {
                    _matGuardNextWarn = Time.unscaledTime + 3f;
                    Debug.LogWarning($"[47-MATGUARD] 머티리얼 대입 실패·원본 복원 — {context}: {ex.Message}");
                }
                return false;
            }
        }

        private static float _matGuardNextWarn;

        /// <summary>[무번호 47-a] 슬롯 0 교체 편의 오버로드 — 기존 `rend.material = inst` 승계용.
        /// 슬롯 0만 교체하고 나머지 슬롯은 원본 유지(기존 .material 세터 의미론 보존), 실패 시 전체 원본 복원.</summary>
        public static bool TryAssignSlot0(Renderer rend, Material slot0, string context)
        {
            if (rend == null || slot0 == null) return false;
            Material[] all;
            try { all = rend.sharedMaterials; } catch (System.Exception) { all = null; }
            if (all == null || all.Length == 0) return TryAssignSharedMaterials(rend, new Material[] { slot0 }, context);
            var arr = new Material[all.Length];
            arr[0] = slot0;
            for (int i = 1; i < all.Length; i++) arr[i] = all[i];
            return TryAssignSharedMaterials(rend, arr, context);
        }

        public static Material CreateSoftParticle(string name, Color color, Texture2D tex)
        {
            var urp = Shader.Find(SoftShaderName);
            Material mat;
            if (urp != null)
            {
                mat = new Material(urp) { name = name };
                // 투명 블렌드(알파) — Particles/Unlit 표면 옵션
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                // [무번호 79-R17] 프로그램 생성 머티리얼의 블렌드 상태 수리 — new Material()은 셰이더
                //   기본값(_SrcBlend=One, _DstBlend=Zero, _SrcBlendAlpha=One, _DstBlendAlpha=Zero,
                //   _ZWrite=On)으로 남아 Blend[_SrcBlend][_DstBlend]가 불투명 대체가 되고 알파가
                //   블렌딩에 전혀 참여하지 않는다. 증상: 플립북 셀 전체가 불투명 사각(79-R16 마젠타/
                //   알파0/그라디언트 스왑 3관 실측으로 근원 확정 — 알파는 셰이더 입력에서 정상이었다).
                //   URP 에디터 규약(BaseShaderGUI.SetupMaterialBlendModeInternal, Transparent+Alpha)과
                //   동일하게: SrcAlpha, OneMinusSrcAlpha / 알파채널 One, OneMinusSrcAlpha, ZWrite Off.
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                // [무번호 66-P1] 소프트 파티클 키워드 **제거** — 깊이 근접 페이드는 URP 파티클이 요구하는
                //   커스텀 버텍스 스트림+카메라 깊이 텍스처에 의존하고, 세션(에디터 Play)에서 깊이 상태가
                //   다르면 알파가 1로 강제되어 **플립북 전체가 불투명 사각**으로 렌더(테스트 12·13 실측:
                //   웜화이트 계단식 베이지 사각 + 암흑 사각, 배치 프로브에서는 정상 — 세션 의존 렌더).
                //   소프트 페이드는 연출 옵션이지 정합성 필수가 아님 — 알파 블렌드만으로 결정론 렌더 고정.
                mat.renderQueue = 3000;
            }
            else
            {
                mat = new Material(Shader.Find("Sprites/Default")) { name = name };   // 폴백 — 기존 경로
            }
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.mainTexture = tex;
            mat.color = color;
            return mat;
        }

        /// <summary>플립북 연기 머티리얼 — 소프트 파티클 + D1 절차 연기 아틀라스(시트 애니는 파티클 시스템 모듈이 담당).</summary>
        public static Material CreateFlipbookSmoke()
        {
            return CreateSoftParticle("FlipbookSmokeMat", new Color(1f, 1f, 1f, 1f), FxFlipbookGenerator.GetSmokeFlipbook());
        }

        /// <summary>[무번호 69-P2] URP 파티클 버텍스 스트림 정합 — 구조적 결함 제거(증상 트림 아님).
        ///   ①URP Particles/Unlit은 Position,Normal,Color,UV 순서의 표준 레이아웃을 요구한다. 빌트인
        ///   파티클 셰이더용 커스텀 스트림([Position,UV,Color] — Normal 없음, MagicHit.prefab YAML
        ///   14675/19579 실측)은 어트리뷰트 슬롯을 어긋나게 해 TEXCOORD0가 미바인딩 → UV 샘플링 상수화 →
        ///   **솔리드 블랙 사각** 렌더(69 바이섹션 실측: MagicHit Shockwave 억제 시 결함 10,653→67px).
        ///   ②Texture Sheet Animation 활성 파티클은 **AnimFrame** 스트림이 필요하다(프레임 인덱스
        ///   미바인딩 → 가비지 타일 샘플링 = 블랙/베이지 박스 — SlashArcFlash 바이섹션 1,344→61px 실측).
        ///   인스턴스 전용(에셋 무변경), 멱등 — 이미 표준이면 무접촉. 반환: 재정합 수행 여부.</summary>
        public static bool AlignParticleVertexStreams(ParticleSystemRenderer pr)
        {
            if (pr == null) return false;
            try
            {
                var ps = pr.GetComponent<ParticleSystem>();
                var streams = new System.Collections.Generic.List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                };
                if (ps != null && ps.textureSheetAnimation.enabled)
                    streams.Add(ParticleSystemVertexStream.AnimFrame);   // 플립북 프레임 인덱스 바인딩
                var cur = new System.Collections.Generic.List<ParticleSystemVertexStream>();
                pr.GetActiveVertexStreams(cur);
                if (cur.Count == streams.Count)
                {
                    bool same = true;
                    for (int i = 0; i < streams.Count; i++)
                        if (cur[i] != streams[i]) { same = false; break; }
                    if (same) return false;   // 멱등 — 이미 표준
                }
                pr.SetActiveVertexStreams(streams);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[69-P2] 버텍스 스트림 정합 실패(시각 폴백 무영향): {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static Texture2D _fanBand;

        /// <summary>[무번호 64-P2] 팬/스윙 스트릭 밴드 텍스처(128×16 — u방향 머리/꼬리 페더 + v방향 가우시안 소프트닝,
        ///   결정론 Random 0건·정적 캐시 1회). FXBandingRing의 _MainTex 셰이더 기본값은 "white"라 텍스처
        ///   미바인딩 시 하드엣지 불투명 스트릭(57차 역산 "텍스처 없는 쿼드" 정합)으로 렌더된다 — 슬롯 직접 바인딩으로 정규화.</summary>
        public static Texture2D GetFanBandTexture()
        {
            if (_fanBand != null) return _fanBand;
            const int w = 128, h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y / (float)(h - 1) - 0.5f) * 2f;   // -1..1
                float soft = Mathf.Exp(-v * v * 4.5f);        // v방향 가우시안 — 하드엣지 제거
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float head = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.22f));      // 머리 램프업
                    float tail = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((u - 0.55f) / 0.45f)); // 꼬리 페이드
                    float a = head * tail * soft;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _fanBand = tex;
            return _fanBand;
        }

        /// <summary>[무번호 39] URP/Unlit 투명화 표준화 — _Surface=1+_Blend=0+키워드+큐(누락 시 불투명 사각
        ///   렌더 실측: 체브런/눈금/크랙 흰·검 사각형 — 15:56 영상). 기존 SetInt 경로 호출부 무손상(보강만).</summary>
        public static void EnsureUnlitTransparent(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
        }

        private static Texture2D _magicCircle;

        /// <summary>[무번호 40] 스무스 매직 서클 텍스처 — MagicHit 에셋의 픽셀화 보라 서클 교체용(부드러운 AA 원환:
        ///   외곽 링+내부 링+6글리프+방사 그라디언트 — 캐시 1회, 결정론 Random 0건). 흑백 생성 후 틴트는 머티리얼 담당.</summary>
        public static Texture2D GetSmoothMagicCircle()
        {
            if (_magicCircle != null) return _magicCircle;
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0~1
                    float a = 0f;
                    // 외곽 링(0.92±0.035 — smoothstep AA) + 내부 링(0.62±0.025)
                    a = Mathf.Max(a, 1f - Mathf.Abs(r - 0.92f) / 0.035f);
                    a = Mathf.Max(a, 0.7f * (1f - Mathf.Abs(r - 0.62f) / 0.025f));
                    // 6글리프(40° 간격 소형 다이아 — 0.77 위치)
                    if (r > 0.70f && r < 0.84f)
                    {
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (ang < 0) ang += 360f;
                        float seg = 360f / 6f;
                        float dSeg = Mathf.Abs(Mathf.Repeat(ang + seg * 0.5f, seg) - seg * 0.5f);
                        if (dSeg < seg * 0.10f) a = Mathf.Max(a, 0.85f);
                    }
                    // 방사 그라디언트(중심 0.12 → 외곽 0)
                    a = Mathf.Max(a, 0.18f * Mathf.Clamp01(1f - r / 0.5f));
                    byte alpha = (byte)(Mathf.Clamp01(a) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _magicCircle = tex;
            return tex;
        }

        /// <summary>프레넬 림 머티리얼(29-D3) — 셰이더 미발견 시 null(호출부 기존 emissive 경로 유지 — 폴백 계약).</summary>
        public static Material CreateRimMaterial(string name, Color rimColor, float rimPower, float intensity)
        {
            var sh = Shader.Find(RimShaderName);
            if (sh == null) return null;   // 폴백 계약: 미지원 환경 = 기존 emissive만
            var mat = new Material(sh) { name = name };
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", rimColor);
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", rimPower);
            if (mat.HasProperty("_RimIntensity")) mat.SetFloat("_RimIntensity", intensity);
            return mat;
        }
    }
}
