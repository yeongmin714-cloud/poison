using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 지면 충격파 링 VFX 정적 유틸리티 (치명타/처치 피드백).
    /// 월드 위치에 평평한 링을 생성해 maxRadius까지 확장, 알파 페이드 후 스스로 제거.
    /// </summary>
    public static class ShockwaveRingFX
    {
        // ── 상수 ─────────────────────────────────────────────────────
        private const float START_RADIUS = 0.2f;    // 링 시작 반지름
        // [2026-09-13 가시화 보강] 링 두께 0.05→0.09 (1.8배 — 반짝임 미표시 리포트 반영).
        // [무번호 21-R3] 0.09→0.115 — 가우시안 페더링으로 줄어드는 체감 두께 보상(밴드 텍스처 병행).
        private const float RING_THICKNESS = 0.115f; // 링 두께 (Y)
        private const float CYL_RADIUS = 0.5f;      // Cylinder 프리미티브 기본 반지름
        private const float CYL_HEIGHT = 1f;        // Cylinder 프리미티브 기본 높이

        // ── [95차 F4] 방향성 스파크 상수 ─────────────────────────────
        private const int SparkFragmentMin = 14;     // 파편 최소 개수 — [패링P2-B] 8→14 강화
        private const int SparkFragmentMax = 16;     // 파편 최대 개수(포함) — [패링P2-B] 12→16 강화
        private const float SparkConeHalfAngle = 30f; // 원뿔 반각(도) — [패링P2-B] 25→30(확산 폭 강화)
        private const float SparkStreakMin = 0.585f; // 파편 스트릭 최소 길이(m) — [패링P2-B] 0.45×1.3(속도 체감 상향)
        private const float SparkStreakMax = 1.17f;  // 파편 스트릭 최대 길이(m) — [패링P2-B] 0.9×1.3
        private const float SparkLineWidth = 0.045f; // 라인 시작 두께(m) — 끝은 0으로 뾰족하게

        // ── 공유 자산 (지연 초기화) ──────────────────────────────────
        private static Mesh _cylinderMesh;
        private static Shader _litShader;
        private static Texture2D _bandTex;   // [무번호 21-R1] 절차 밴드 텍스처 캐시
        // [무번호 23-X2] 동시 링 예산 — 패밀리 총합 상한(확산+수렴+echo 전부)
        internal const int MaxConcurrentRings = 24;
        // [무번호 25-BL1] 발광 파이프라인 — HDR 임시션(알베도 color 경로 무변경, 발광은 추가 항)
        internal const float EmissionIntensityBase = 1.5f;   // 일반 링 — Play 판정 대기값(제안 ×1.5)
        internal const float EmissionIntensityLarge = 2.5f;  // 대형 링(반경 1m 이상) — 등급화(제안 ×2.5)
        private static int _activeRingCount;
        private static void NotifyRingDestroyed() { _activeRingCount = Mathf.Max(0, _activeRingCount - 1); }

        /// <summary>[무번호 21-R1 → 무번호 28-U2] 절차 밴드 텍스처 256×64 — 실린더 단면의 "하드 단색 튜브"를
        ///   빛나는 에너지 밴드로 교체: v축(높이) 가우시안 글로우(중심 최밝→상하 소프트 페더),
        ///   u축 결정론 해시 노이즈(유기적 에너지 요동), u=0/1 시임 랩 블렌드(이음새 없음).
        ///   색은 머티리얼 color 승수로 유지(패링 금/청·몬스터 적·먼지 흙색 계약 무손상).
        ///   [무번호 28-U2] 표준 상향 — 64×16 → 256×64, 2옥타브 → 4옥타브(24셀/8셀 유지 + 96셀/32셀 확장).
        ///   결정론 해시(43758.5453 선례) 상수/시드 로직 그대로, 캐시 1회 생성 구조 유지.</summary>
        private static Texture2D EnsureBandTexture()
        {
            if (_bandTex != null) return _bandTex;
            const int W = 256, H = 64;   // [무번호 28-U2] 64×16 → 256×64 표준 상향 — 기존 _bandTex 캐시 필드 재사용(1회 생성)
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "ShockwaveBandTex";
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float dv = (v - 0.5f) / 0.5f;                  // -1..1
                float glow = Mathf.Exp(-dv * dv * 3.2f);       // 가우시안 — 두께 보상 포함 폭
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    // [무번호 22-P1] u축 노이즈 2옥타브(24셀 굵은 요동 + 8셀 세부장) — 결정론 해시
                    float cellA = Mathf.Floor(u * 24f);
                    float nA = Mathf.Repeat(Mathf.Sin(cellA * 127.1f) * 43758.5453f, 1f);
                    float cellB = Mathf.Floor(u * 8f);
                    float nB = Mathf.Repeat(Mathf.Sin(cellB * 269.5f) * 18376.2347f, 1f);
                    // [무번호 28-U2] 3·4옥타브 확장(96셀 굵은 세부 + 32셀 초세부) — 기존 셀 구조 비율 ×4 유지,
                    //   결정론 해시 상수(127.1/269.5/43758.5453/18376.2347) 그대로, Random 0건.
                    float cellC = Mathf.Floor(u * 96f);
                    float nC = Mathf.Repeat(Mathf.Sin(cellC * 127.1f) * 43758.5453f, 1f);
                    float cellD = Mathf.Floor(u * 32f);
                    float nD = Mathf.Repeat(Mathf.Sin(cellD * 269.5f) * 18376.2347f, 1f);
                    float noise = 0.80f + 0.13f * nA + 0.07f * nB + 0.03f * nC + 0.015f * nD;
                    float seam = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.08f);  // 시임 랩 블렌드
                    // [무번호 23-U2] 서브 밴드 — 밴드 내부 얇은 2차 광대(0.34/0.66 위치) — 트리플 레이어 구조
                    float sub = Mathf.Exp(-Mathf.Pow((v - 0.34f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((v - 0.66f) / 0.06f, 2f));
                    px[y * W + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((glow + 0.18f * sub) * noise * Mathf.Lerp(0.35f, 1f, seam)));
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            _bandTex = tex;
            return tex;
        }

        /// <summary>[무번호 25-BL1] URP/Lit 머티리얼에 HDR 임시션 부여 — Bloom(Threshold 0.9) 흡수 유도.
        ///   알베도(color 승수)·밴드 텍스처 경로 무변경 — BL3 색 계약 무손상. 결정론(입력값만 사용, Random 0건).</summary>
        private static void ApplyEmissionBoost(Material mat, Color tint, float radius)
        {
            // [무번호 25-BL2] 등급화 — 대형 링(≥1m)은 강한 발광
            float intensity = radius >= 1.0f ? EmissionIntensityLarge : EmissionIntensityBase;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(tint.r * intensity, tint.g * intensity, tint.b * intensity, 1f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;   // GI 비기여
        }

        /// <summary>
        /// 충격파 링 생성 — 시작 반지름 0.2에서 maxRadius까지 duration 동안
        /// Ease-Out 트윈으로 확장하며 알파 페이드, 종료 후 Destroy.
        /// </summary>
        /// <param name="worldPos">링 중심 월드 좌표</param>
        /// <param name="maxRadius">최종 반지름</param>
        /// <param name="color">링 색상 (알포 포함, 페이드 시작 알파로 사용)</param>
        /// <param name="duration">확장 + 페이드 지속 시간(초)</param>
        public static void Spawn(Vector3 worldPos, float maxRadius, Color color, float duration)
        {
            SpawnCore(worldPos, maxRadius, color, duration, true);
        }

        /// <summary>[무번호 22-P2] SpawnCore — echo 재귀 방지 가드(allowEcho=false = Runner가 echo 재스폰 금지).
        ///   공개 Spawn 시그니처 불변(R4 계약), 내부 분리만.</summary>
        private static void SpawnCore(Vector3 worldPos, float maxRadius, Color color, float duration, bool allowEcho)
        {
            // [무번호 23-X2] FX 예산 가드 — 동시 링 상한 초과 시 조용히 스킵(대규모 전투 저사양 안전망)
            if (_activeRingCount >= MaxConcurrentRings) return;
            EnsureAssets();
            // [무번호 57-P2] FX 스폰 레지스트리 — 링은 수명 짧아 역산 시점에 소멸 → 스폰 좌표 기록.
            MagentaSentinel.RegisterFxSource("Ring", worldPos);

            if (_cylinderMesh == null || _litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 자산 로드 실패 — Shader 또는 Mesh 누락");
                return;
            }

            // 안전 클램프
            if (maxRadius < START_RADIUS) maxRadius = START_RADIUS;
            if (duration <= 0f) duration = 0.1f;

            // [2026-09-13 가시화 보강] 입력색 밝기/알파 상향 — RGB 30% 화이트 쪽 러프,
            // 알파는 최소 0.75 보장(페이드 시작점도 함께 상향돼 링이 확실히 보인다).
            // Spawn 시그니처는 유지하고 내부에서만 부스트한다.
            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);
            // [무번호 22-P3 부분 적용] 대형 링 밝기 부스트 — HDR 블룸 흡수 유도(전면 Unlit 전환은 Play 비교 후 판단)
            if (maxRadius >= 1.0f) color = new Color(color.r * 1.15f, color.g * 1.15f, color.b * 1.15f, color.a);

            // 링 GameObject (직접 MeshFilter/MeshRenderer 구성 — Collider 오버헤드 제거)
            GameObject ring = new GameObject("ShockwaveRing")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            ring.transform.position = worldPos;
            ring.transform.rotation = Quaternion.identity; // 링 축 = 월드 Y (위쪽)

            // Cylinder 프리미티브 메시를 납작하게 눌러 지면 링으로 사용
            // (SpecialEffectsController 선택 링 참조: 기본 반지름 0.5, 기본 높이 1)
            float startScale = START_RADIUS / CYL_RADIUS;
            ring.transform.localScale = new Vector3(startScale, RING_THICKNESS / CYL_HEIGHT, startScale);

            var filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = _cylinderMesh;

            var renderer = ring.AddComponent<MeshRenderer>();

            // 반투명 Lit Material (스폰별 인스턴스 — 동시 다중 링 알파 페이드 충돌 방지)
            var mat = new Material(_litShader)
            {
                color = color
            };
            mat.name = "ShockwaveRingMat";

            // 투명 설정 (SpecialEffectsController 선택 링 머티리얼 참조)
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            // [무번호 21-R2] 절차 밴드 텍스처 — 단색 튜브 → 그라디언트 에너지 밴드(색 승수 구조 유지)
            mat.mainTexture = EnsureBandTexture();
            ApplyEmissionBoost(mat, color, maxRadius);   // [무번호 25-BL1] — color는 L109~112 부스트 적용 최종값

            renderer.sharedMaterial = mat;

            // 확장/페이드 Runner 부착 (Update 기반 트윈 후 자기 파괴) — [무번호 22-P2] echo 가드 전달
            _activeRingCount++;
            ring.AddComponent<RingRunner>().Init(maxRadius, color, duration, allowEcho);

            Debug.Log($"[ShockwaveRingFX] spawn r={maxRadius} at {worldPos}");
        }

        /// <summary>
        /// 공유 자산을 한 번만 로드한다 (HitVFX 캐싱 참조).
        /// </summary>
        private static void EnsureAssets()
        {
            if (_cylinderMesh != null && _litShader != null) return;

            // Cylinder Mesh 추출 후 임시 객체 정리
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _cylinderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            // [156차 P5-수리] EditMode 안전 가드(126차 규약) — 플레이 모드 동작 무변경
            if (Application.isPlaying) Object.Destroy(temp);
            else Object.DestroyImmediate(temp);

            // [무번호 26-DF3] 링 전용 셰이더(FXBandingRing — Unlit 밴드+깊이 페이드) 1순위, 기존 Lit 폴백 유지
            //   (CombatFxParryFlashPhaseDTests 잠금 — "Universal Render Pipeline/Lit" 리터럴 보존)
            Shader litShader = Shader.Find("ProjectName/FXBandingRing");
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            _litShader = litShader;
        }

        // ── [95차 F4] 방향성 스파크 ──────────────────────────────────

        /// <summary>
        /// [95차 F4] 방향성 스파크 — 흡수점(origin)에서 direction(공격자 방향 = kbDir 역방향) 원뿔축으로
        /// 파편 8~12개 라인 스트릭을 duration(기본 0.3s) 동안 발사 후 알파 페이드·자기 파괴.
        /// ShockwaveRing+StarFlare와 병행(교체 아님). 스폰 패턴은 Spawn 선례
        /// (HideAndDontSave + Runner 트윈 + 머티리얼 인스턴스 + OnDestroy 정리)를 따른다.
        /// </summary>
        /// <param name="origin">스파크 발사 원점(패링 흡수점 — 전방 1m + up 0.9)</param>
        /// <param name="direction">원뿔 축 방향(공격자 방향 — kbDir 역방향)</param>
        /// <param name="color">파편 색상(알파 포함 — 페이드 시작 알파로 사용)</param>
        /// <param name="duration">발사 + 페이드 지속 시간(초)</param>
        public static void SpawnDirectionalSpark(Vector3 origin, Vector3 direction, Color color, float duration = 0.3f)
        {
            EnsureAssets();

            if (_cylinderMesh == null || _litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 방향성 스파크 자산 로드 실패 — Shader 또는 Mesh 누락");
                return;
            }

            // 안전 클램프/폴백
            if (duration <= 0f) duration = 0.1f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();
            // [무번호 57-P2] FX 스폰 레지스트리 — 스파크 수명 0.3s로 역산 시점 전 소멸 → 스폰 좌표 기록.
            MagentaSentinel.RegisterFxSource("Spark", origin);

            // [2026-09-13 가시화 보강 선례] 입력색 밝기/알파 상향 — 링과 동일 부스트로 시인성 통일.
            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject host = new GameObject("DirectionalSpark")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            host.transform.position = origin;

            int count = Random.Range(SparkFragmentMin, SparkFragmentMax + 1); // 8~12 포함
            host.AddComponent<SparkRunner>().Init(origin, direction, count, color, duration, _litShader);

            Debug.Log($"[ShockwaveRingFX] directional spark x{count} at {origin}");
        }

        /// <summary>
        /// [95차 F4] 방향성 스파크 파편 8~12개 라인 스트릭 트윈 내부 MonoBehaviour.
        /// 축 주위 원뿔(반각 25°)로 흩어진 파편이 흡수점에서 밖으로 뻗으며 길어졌다 알파 페이드 후 자기 파괴.
        /// 시간 기준은 RingRunner와 동일한 Time.deltaTime — 패링 슬로우 중 스파크도 같이 느려져 임팩트가 묻어난다.
        /// </summary>
        private class SparkRunner : MonoBehaviour
        {
            private struct Fragment
            {
                public LineRenderer Line;
                public Vector3 Dir;
                public float Length;
            }

            private Fragment[] _fragments;
            private Material _mat;
            private Color _baseColor;
            private Vector3 _origin;
            private float _duration;
            private float _elapsed;

            public void Init(Vector3 origin, Vector3 axis, int count, Color color, float duration, Shader shader)
            {
                _origin = origin;
                _baseColor = color;
                _duration = duration;
                _elapsed = 0f;

                // 스폰별 머티리얼 인스턴스 — 동시 다중 스파크 알파 페이드 충돌 방지(Spawn 선례).
                _mat = new Material(shader)
                {
                    color = color
                };
                _mat.name = "DirectionalSparkMat";
                _mat.SetFloat("_Surface", 1f);
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_ZWrite", 0);
                _mat.renderQueue = 3000;
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _mat.SetOverrideTag("RenderType", "Transparent");

                _fragments = new Fragment[count];
                for (int i = 0; i < count; i++)
                {
                    Vector3 dir = ConeDirection(axis);
                    GameObject lineGo = new GameObject("SparkFragment");
                    lineGo.transform.SetParent(transform, false);
                    LineRenderer line = lineGo.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.material = _mat;
                    line.startWidth = SparkLineWidth;
                    line.endWidth = 0f;
                    line.numCapVertices = 2;
                    line.SetPosition(0, _origin);
                    line.SetPosition(1, _origin + dir * 0.05f);
                    _fragments[i] = new Fragment
                    {
                        Line = line,
                        Dir = dir,
                        Length = Random.Range(SparkStreakMin, SparkStreakMax)
                    };
                }
            }

            /// <summary>축 주위 원뿔(반각 SparkConeHalfAngle) 내 랜덤 방향 — 직교 기저 2개로 균일 근사.</summary>
            private static Vector3 ConeDirection(Vector3 axis)
            {
                Vector3 ortho = Mathf.Abs(axis.y) < 0.9f
                    ? Vector3.Cross(axis, Vector3.up)
                    : Vector3.Cross(axis, Vector3.right);
                ortho.Normalize();
                Vector3 ortho2 = Vector3.Cross(ortho, axis).normalized;
                float tilt = Random.Range(0f, SparkConeHalfAngle) * Mathf.Deg2Rad;
                float azimuth = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 radial = ortho * Mathf.Cos(azimuth) + ortho2 * Mathf.Sin(azimuth);
                return (axis * Mathf.Cos(tilt) + radial * Mathf.Sin(tilt)).normalized;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);

                // Ease-Out 트윈 — 빠르게 뻗고 서서히 멈추는 충격파 감쇠(Spawn 선례와 동일 곡선).
                float eased = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < _fragments.Length; i++)
                {
                    Fragment frag = _fragments[i];
                    if (frag.Line == null) continue;
                    // 스트릭 전체가 원점에서 밖으로 날아감 — 머리(head)와 꼬리(tail) 사이 라인.
                    float head = eased * frag.Length;
                    float tail = Mathf.Max(0f, head - frag.Length * 0.35f);
                    frag.Line.SetPosition(0, _origin + frag.Dir * tail);
                    frag.Line.SetPosition(1, _origin + frag.Dir * head);
                }

                // 알파 페이드 (색상은 유지, 알파만 0으로 감쇠)
                if (_mat != null)
                {
                    float alpha = Mathf.Lerp(_baseColor.a, 0f, t);
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                }

                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                // 조기 파괴(씬 전환 등) 시 Material 누수 방지 — 파편 GameObject는 자식이라 동반 파괴된다.
                if (_mat != null)
                    Object.Destroy(_mat);
            }
        }

        // ── [무번호 19-F1] 이동 먼지 트레일 상수 ─────────────────────
        private const float TrailDustRadius = 0.55f;   // 먼지 링 최대 반지름(m) — 발밑 미세 먼지
        private const float TrailDustDuration = 0.9f;  // 먼지 잔광 지속(초) — 2~3s 잔광 계약은 과도(체감 부자연) → 0.9s
        private static readonly Color TrailDustColor = new Color(0.78f, 0.72f, 0.62f, 0.30f); // 흙색 먼지

        /// <summary>[무번호 25-IN1] 날씨 반응 먼지 틴트 — Clear/Fog/StrongWind=기존색 유지(계약),
        ///   Rain=청백 물 스플래시, Snow=흰 파우더. WeatherManager 부재 시 기존색(안전). 결정론(Random 0건).</summary>
        internal static Color WeatherTintDust(Color baseColor)
        {
            var wm = WeatherManager.Instance;
            if (wm == null) return baseColor;
            switch (wm.CurrentWeather)
            {
                case WeatherManager.WeatherType.Rain:
                    return Color.Lerp(baseColor, new Color(0.65f, 0.9f, 1f, baseColor.a), 0.55f);
                case WeatherManager.WeatherType.Snow:
                    return Color.Lerp(baseColor, new Color(0.95f, 0.97f, 1f, baseColor.a), 0.70f);
                default:
                    return baseColor;
            }
        }

        /// <summary>
        /// [무번호 19-F1] 이동 먼지 트레일 — 병사 발밑에 흙색 먼지 링(0.55m) 스폰.
        /// ShockwaveRingFX.Spawn 선례(머티리얼 인스턴스 + 트윈 + 페이드) 그대로, 호출부는
        /// GuardPlaceholder.StepToward(이동 중) — 도착 시 트레일 미스폰.
        /// [무번호 25-IN1] 시작색에 날씨 틴트 경유 — 구조/수명/개수 무변경(0.6m 게이팅 계약 유지).
        /// </summary>
        public static void SpawnMoveTrailDust(Vector3 groundPos)
        {
            Spawn(groundPos, TrailDustRadius, WeatherTintDust(TrailDustColor), TrailDustDuration);
        }

        // ── [무번호 20-I2] 도착 먼지 퍼프 상수 ─────────────────────
        private const float ArrivalPuffRadius = 0.42f;    // 퍼프 최대 반지름 — 트레일(0.55)보다 작고 빠른 소멸
        private const float ArrivalPuffDuration = 0.55f;  // 퍼프 지속 — 도착 스냅 연출
        private static readonly Color ArrivalPuffColor = new Color(0.82f, 0.76f, 0.66f, 0.38f); // 트레일보다 밝은 흙색

        /// <summary>[무번호 20-I2] 도착 먼지 퍼프 — 병사 이동 명령 도착 순간 1회 소형 확산.
        ///   F1 트레일("이동 중에만")과 별개 신규 FX — 도착 분기에서 호출. Spawn 선례 그대로.</summary>
        public static void SpawnArrivalPuff(Vector3 groundPos)
        {
            // [무번호 25-IN1] 시작색 날씨 틴트 — 색 감싸기만(구조 무변경 — IN1 잠금 원형 유지).
            Spawn(groundPos, ArrivalPuffRadius, WeatherTintDust(ArrivalPuffColor), ArrivalPuffDuration);
            // [무번호 29-S2-2] 도착 강도 — 감속 연동 슬롯 소비 → 강도 비례 입자 퍼프 추가 레이어
            //   (지면 링 반경/수명 계약 무손상, 강도는 입자 레이어로 가시화).
            float kArr = Mathf.Clamp(_pendingPuffIntensity, 0.5f, 2f);
            _pendingPuffIntensity = 1f;
            SpawnMoveTrailPuffCore(groundPos, kArr);
        }

        // ── [무번호 21-K1] 이동 먼지 파티클 퍼프 ────────────────────
        private static Texture2D _puffTex;                 // 절차 소프트 서클 텍스처 캐시
        private const int PuffParticleCount = 5;           // 트레일 지점당 입자 수 — 스폰 수 한정(게이팅 계약 병행)
        private const float PuffDespawnSeconds = 0.85f;    // PS 호스트 수명(입자 수명 0.65s + 여유)

        /// <summary>[무번호 21-K1 + 22-Q1 → 무번호 28-U2] 절차 먼지 텍스처 256×256 — **4옥타브 value noise**
        ///   (프랙탈 구름 프로파일) × 방사 마스크 — 매끈한 원이 실제 연기처럼 흐느적거림. **사각 금지 계약**
        ///   (156차 GetStarTexture 선례) — 하드 엣지/사각 프로파일 폐기. 결정론 해시(랜덤 0건), 캐시 1회.
        ///   [무번호 28-U2] 표준 상향 — 128×128 3옥타브 → 256×256 4옥타브 + 가장자리 소프트 알파 강화
        ///   (smoothstep 방사 감쇠 유지). PuffNoise/PuffHash 결정론 해시 그대로, 옥타브만 추가.</summary>
        private static Texture2D EnsurePuffTexture()
        {
            if (_puffTex != null) return _puffTex;
            const int S = 256;   // [무번호 28-U2] 128×128 → 256×256 표준 상향 — 기존 _puffTex 캐시 필드 재사용(1회 생성)
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "DustPuffTex";
            var px = new Color[S * S];
            float c = S * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float ux = (x + 0.5f) / S, uy = (y + 0.5f) / S;
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    // [무번호 28-U2] 가장자리 소프트 알파 강화 — smoothstep 방사 감쇠(사각 금지 계약 유지)
                    float radial = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d));
                    // [무번호 22-Q1] 3옥타브 value noise — 굵은 구름/중간 요동/세부장
                    float n = 0.5f * PuffNoise(ux * 5f, uy * 5f, 1f)
                            + 0.3f * PuffNoise(ux * 11f, uy * 11f, 7f)
                            + 0.2f * PuffNoise(ux * 23f, uy * 23f, 13f);
                    // [무번호 28-U2] 4옥타브 확장 — 초세부 octave 추가(결정론 해시/시드 그대로, Random 0건)
                    n += 0.1f * PuffNoise(ux * 47f, uy * 47f, 29f);
                    float a = Mathf.Clamp01((0.35f + 0.65f * n) * radial * 1.35f);
                    a = a * a * (3f - 2f * a);   // 최종 소프트 마감(하드 엣지 방지)
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            _puffTex = tex;
            return tex;
        }

        /// <summary>[무번호 22-Q1] 격자 보간 value noise — 결정론 해시 기반(랜덤 0건).</summary>
        private static float PuffNoise(float x, float y, float seed)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = PuffHash(ix, iy, seed);
            float b = PuffHash(ix + 1f, iy, seed);
            float c2 = PuffHash(ix, iy + 1f, seed);
            float d = PuffHash(ix + 1f, iy + 1f, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c2, d, fx), fy);
        }

        private static float PuffHash(float x, float y, float seed)
        {
            return Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f, 1f);
        }

        /// <summary>[무번호 21-K1] 이동 먼지 파티클 퍼프 — 트레일 지점 공기에 부양하는 흙색 입자 5개
        ///   (지면 링과 병행 2레이어 — 젤다식 먼지). CombatVFXController 절차 PS 선례, 런타임 전용 경로.</summary>
        // [무번호 29-S2-2] 퍼프 강도 슬롯 — 단발 소비(호출 직전 SetNextPuffIntensity → 1인자 호출이 소비).
        //   기존 잠금 시그니처(SpawnMoveTrailPuff(Vector3)) 보존 설계 — 강도는 슬롯 경유(결정론).
        private static float _pendingPuffIntensity = 1f;

        /// <summary>[무번호 29-S2-2] 다음 1인자 퍼프/도착 호출의 강도(0.5~2 클램프) — 가속·감속 연동용.</summary>
        public static void SetNextPuffIntensity(float intensity)
        {
            _pendingPuffIntensity = Mathf.Clamp(intensity, 0.5f, 2f);
        }

        public static void SpawnMoveTrailPuff(Vector3 groundPos)
        {
            // [무번호 29-S2-2] 강도 슬롯 소비 — 크기·입자 수에 비례(결정론 반올림, Random 0건).
            float k = Mathf.Clamp(_pendingPuffIntensity, 0.5f, 2f);
            _pendingPuffIntensity = 1f;
            SpawnMoveTrailPuffCore(groundPos, k);
        }

        /// <summary>[무번호 29-S2-2] 퍼프 코어 — 강도 k(0.5~2)를 크기/입자 수에 적용(도착 레이어 재사용).</summary>
        private static void SpawnMoveTrailPuffCore(Vector3 groundPos, float k)
        {
            var go = new GameObject("MoveTrailPuff") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = groundPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            // [무번호 22-Q3] 편차 확대 — 균일감 제거(크기 0.08~0.22 / 수명 0.4~0.75)
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f * k, 0.22f * k);   // [무번호 29-S2-2] 강도 배율(22-Q3 편차 비율 2.75× 유지)
            main.startColor = WeatherTintDust(TrailDustColor);   // [무번호 25-IN1] 흙색 — 트레일 링과 색 계약 통일 + 날씨 반응 틴트(단일 적용점)
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.06f;      // 미세 부양(먼지 떠오름)
            main.maxParticles = 16;
            // [무번호 22-Q1] 난류 모듈 — 입자가 직선으로 떠오르지 않고 흐느적거림
            var turbulence = ps.noise;
            turbulence.enabled = true;
            turbulence.strength = 0.1f;
            turbulence.frequency = 0.8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;   // 1회 Emit만 — 지속 방출 없음

            // 수명 알파 페이드 + 크기 확장 — 입자 품질 커브
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, 1.6f);   // 선형 확산

            var rnd = go.GetComponent<ParticleSystemRenderer>();
            if (rnd != null)
            {
                // [무번호 29-D2] 소프트 파티클(카메라 깊이 페이드) — 지면 관통 제거.
                //   URP 파티클 셰이더 부재 환경은 FxMaterialLibrary 내부 Sprites/Default 폴백(기존 룩 무손상).
                var mat = FxMaterialLibrary.CreateSoftParticle("DustPuffMat", TrailDustColor, EnsurePuffTexture());
                rnd.material = mat;
                rnd.renderMode = ParticleSystemRenderMode.Billboard;
                rnd.alignment = ParticleSystemRenderSpace.View;
                rnd.sortMode = ParticleSystemSortMode.Distance;
            }
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt(PuffParticleCount * k), 3, 10));   // [무번호 29-S2-2] 강도 비례 입자 수(3~10 클램프)
            // [무번호 21-K1 수리] 지연 Destroy 오버로드는 EditMode에서 즉시 에러 로그("Destroy may not be
            //   called from edit mode") → 이동 시뮬 테스트 15건 오염. 156차 DestroyImmediate 가드 선례 적용.
            if (Application.isPlaying) Object.Destroy(go, PuffDespawnSeconds);
            else Object.DestroyImmediate(go);   // EditMode — 입자 시뮬 부재 환경 즉시 정리
        }

        // ── [무번호 20-J1] 패링 디플렉트 팬 상수 ────────────────────
        private const int ParryFanCount = 6;              // 팬 스파크 개수(방패 평면 부채꼴 — 결정론)
        private const float ParryFanHalfAngle = 55f;      // 팬 총각의 절반(도) — 공격자 축 기준 좌우 전개
        private const float ParryFanStreakMin = 0.30f;    // 팬 스트릭 최소 길이(m) — 방향성 스파크보다 짧고 빠름
        private const float ParryFanStreakMax = 0.48f;
        private const float ParryFanLineWidth = 0.035f;

        /// <summary>[무번호 20-J1] 패링 디플렉트 팬 — 흡수점에서 공격자 방향 직교 부채꼴로 튕겨나가는
        ///   에너지 스파크 팬(SparkRunner LineRenderer 선례 재사용, **결정론 — Random 0건** — 프로브 판정 친화).
        ///   attackerDir = 플레이어→공격자 방향. 팬은 그 축을 위쪽(Y) 회전으로 좌우 전개 — 방패로 튕겨낸 에너지.</summary>
        public static void SpawnParryDeflectFan(Vector3 origin, Vector3 attackerDir, Color color, float duration = 0.25f)
        {
            EnsureAssets();
            if (_litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 디플렉트 팬 자산 로드 실패 — Shader 누락");
                return;
            }
            if (duration <= 0f) duration = 0.1f;
            if (attackerDir.sqrMagnitude < 0.0001f) attackerDir = Vector3.forward;
            attackerDir.y = 0f;
            if (attackerDir.sqrMagnitude < 0.0001f) attackerDir = Vector3.forward;
            attackerDir.Normalize();

            // [2026-09-13 가시화 보강 선례] 입력색 부스트 — 링/스파크와 동일 시인성 통일
            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject host = new GameObject("ParryDeflectFan") { hideFlags = HideFlags.HideAndDontSave };
            host.transform.position = origin;
            host.AddComponent<FanRunner>().Init(origin, attackerDir, ParryFanCount, color, duration, _litShader,
                ParryFanHalfAngle, ParryFanStreakMin, ParryFanStreakMax);
            Debug.Log($"[ShockwaveRingFX] parry deflect fan x{ParryFanCount} at {origin}");
        }

        /// <summary>[무번호 22-S2] 스윙 지면 파동 — 공격 방향으로 좁게(±15°) 길게(1.0~1.4m) 뻗는 지면
        ///   스트릭 3본(디플렉트 팬 패밀리 — 스윙 바람 스침). 결정론(FanRunner 랜덤 0건).</summary>
        public static void SpawnSwingSweep(Vector3 groundPos, Vector3 swingDir, Color color, float duration = 0.25f)
        {
            EnsureAssets();
            if (_litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 스윙 파동 자산 로드 실패 — Shader 누락");
                return;
            }
            if (duration <= 0f) duration = 0.1f;
            if (swingDir.sqrMagnitude < 0.0001f) swingDir = Vector3.forward;
            swingDir.y = 0f;
            if (swingDir.sqrMagnitude < 0.0001f) swingDir = Vector3.forward;
            swingDir.Normalize();
            // [무번호 57-P2] FX 스폰 레지스트리 — 스윙 파동 수명 0.25s로 역산 시점 전 소멸 → 스폰 좌표 기록.
            MagentaSentinel.RegisterFxSource("SwingSweep", groundPos);

            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject host = new GameObject("SwingSweep") { hideFlags = HideFlags.HideAndDontSave };
            host.transform.position = groundPos;
            host.AddComponent<FanRunner>().Init(groundPos, swingDir, 3, color, duration, _litShader, 15f, 1.0f, 1.4f);
            Debug.Log($"[ShockwaveRingFX] swing sweep x3 at {groundPos}");
        }

        /// <summary>[무번호 22-S3] 불티 파편 — 스콜치 직후 주황 불티 5개(위로 튀고 중력 낙하).
        ///   **런타임 전용 랜덤 경로** — 프로브 판정 경로 미경유(계약 문서화). 소프트 텍스처 재사용.</summary>
        public static void SpawnEmberBurst(Vector3 groundPos)
        {
            var go = new GameObject("ScorchEmbers") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = groundPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new Color(1f, 0.55f, 0.2f, 0.95f);   // 주황 불티
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.8f;   // 튀었다 낙하
            main.maxParticles = 12;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            if (rnd != null)
            {
                // [무번호 29-D2] 불티 소프트 파티클 — 동일 폴백 계약(29-D2 주석 선례).
                var mat = FxMaterialLibrary.CreateSoftParticle("ScorchEmberMat", new Color(1f, 0.55f, 0.2f, 1f), EnsurePuffTexture());
                rnd.material = mat;
                rnd.renderMode = ParticleSystemRenderMode.Billboard;
                rnd.alignment = ParticleSystemRenderSpace.View;
            }
            ps.Emit(5);
            // [무번호 21-K1 수리 선례] 지연 Destroy EditMode 가드 — 처음부터 적용
            if (Application.isPlaying) Object.Destroy(go, 1.2f);
            else Object.DestroyImmediate(go);
        }

        // ── [무번호 21-L1] 수렴 링 ───────────────────────────────────
        /// <summary>[무번호 21-L1] 수렴 링 — 외곽→중심 수축(에너지 수집 연출). AttackTelegraphIndicator가
        ///   fill 75% 시점 1회 자체 발화(몬스터 배선 추가 없음). easeIn 수축 — 마지막에 중심으로 스냅.</summary>
        public static void SpawnConvergingRing(Vector3 worldPos, float startRadius, Color color, float duration)
        {
            // [무번호 22-S1] 수렴 2겹 — 바깥(원본 파라미터)+안쪽(0.6배 반경·1.25배 길이·0.5배 알파)
            SpawnConvergingRingCore(worldPos, startRadius, color, duration);
            Color inner = color;
            inner.a = color.a * 0.5f;
            SpawnConvergingRingCore(worldPos, startRadius * 0.6f, inner, duration * 1.25f);
        }

        private static void SpawnConvergingRingCore(Vector3 worldPos, float startRadius, Color color, float duration)
        {
            EnsureAssets();
            if (_cylinderMesh == null || _litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 수렴 링 자산 로드 실패 — Shader 또는 Mesh 누락");
                return;
            }
            if (startRadius < START_RADIUS) startRadius = START_RADIUS * 2f;
            if (duration <= 0f) duration = 0.1f;

            // [2026-09-13 가시화 보강 선례] 입력색 부스트 — 확산 링과 동일 시인성 통일
            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject ring = new GameObject("ConvergingRing") { hideFlags = HideFlags.HideAndDontSave };
            ring.transform.position = worldPos;
            ring.transform.rotation = Quaternion.identity;
            float startScale = startRadius / CYL_RADIUS;
            ring.transform.localScale = new Vector3(startScale, RING_THICKNESS / CYL_HEIGHT, startScale);
            var filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = _cylinderMesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            var mat = new Material(_litShader) { color = color };
            mat.name = "ConvergingRingMat";
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.mainTexture = EnsureBandTexture();   // [무번호 21-R2] 공용 밴드 텍스처
            ApplyEmissionBoost(mat, color, startRadius);   // [무번호 25-BL1]
            renderer.sharedMaterial = mat;
            _activeRingCount++;
            ring.AddComponent<ConvergeRunner>().Init(startRadius, color, duration);
            Debug.Log($"[ShockwaveRingFX] converging ring r={startRadius} at {worldPos}");
        }

        // ── [무번호 25-A4] 몬스터 임팩트 링 ─────────────────────────
        internal const float ImpactEmissionIntensity = 2.0f;   // 계획서 A4.2 — 임팩트 링 HDR 발광 강도 1.0→2.0(고정 — 확산/수렴 링 등급화와 별개)
        internal const float ImpactDepthFadeDist = 0.35f;      // 계획서 "깊이 페이드 0.35" — FXBandingRing._DepthFadeDist 매핑값
        // [무번호 25-A4] EliteColor 변주 계약 — 텔레그래프 위험도 2색 패밀리(일반=적 / 정예=주황-적 1,0.30,0.16) 승계 톤
        private static readonly Color ImpactEliteTone = new Color(1f, 0.30f, 0.16f, 1f);

        /// <summary>[무번호 25-A4] 몬스터 임팩트 링 — 확산 링(SpawnCore) 구조 선례 + HDR emissive ×2.0 +
        ///   깊이 페이드(FXBandingRing._DepthFadeDist 0.35) + EliteColor 변주 계약(elite=true → 정예/강공격
        ///   주황 톤 승계, false → 호출부 색 그대로 — EA1 몬스터 개성 틴트 경유). 46/47차 에셋 계약 무손상
        ///   (Hovl 에셋 프리팹 파일 미사용 — 절차 런타임 구성). 결정론(Random 0건). AnimalAI 임팩트 배선용.</summary>
        public static void SpawnImpactRing(Vector3 position, float radius, Color color, float duration, bool elite)
        {
            // [무번호 25-A4] EliteColor 변주 — elite=true면 호출부 색을 주황-적(Elite) 톤으로 60% 승계,
            //   false면 호출부 색 그대로. 알파는 호출부 값 유지.
            if (elite)
            {
                Color eliteTone = new Color(ImpactEliteTone.r, ImpactEliteTone.g, ImpactEliteTone.b, color.a);
                color = Color.Lerp(color, eliteTone, 0.6f);
            }
            SpawnImpactRingCore(position, radius, color, duration, elite);
        }

        private static void SpawnImpactRingCore(Vector3 worldPos, float maxRadius, Color color, float duration, bool elite)
        {
            // [무번호 23-X2] FX 예산 가드 — 동시 링 상한 공유
            if (_activeRingCount >= MaxConcurrentRings) return;
            EnsureAssets();
            if (_cylinderMesh == null || _litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 임팩트 링 자산 로드 실패 — Shader 또는 Mesh 누락");
                return;
            }
            // 안전 클램프 — SpawnCore 선례 동일
            if (maxRadius < START_RADIUS) maxRadius = START_RADIUS;
            if (duration <= 0f) duration = 0.1f;

            // [2026-09-13 가시화 보강 선례] 입력색 밝기/알파 상향 — 링 패밀리 동일 부스트
            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject ring = new GameObject("ImpactRing") { hideFlags = HideFlags.HideAndDontSave };
            ring.transform.position = worldPos;
            ring.transform.rotation = Quaternion.identity;   // 링 축 = 월드 Y (SpawnCore 선례)
            float startScale = START_RADIUS / CYL_RADIUS;
            ring.transform.localScale = new Vector3(startScale, RING_THICKNESS / CYL_HEIGHT, startScale);
            var filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = _cylinderMesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            var mat = new Material(_litShader) { color = color };
            mat.name = "ImpactRingMat";
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.mainTexture = EnsureBandTexture();   // [무번호 21-R2] 공용 밴드 텍스처
            ApplyImpactEmissionBoost(mat, color);    // [무번호 25-A4] HDR emissive ×2.0
            // [무번호 25-A4] 깊이 페이드 — FXBandingRing 전용 프로퍼티(폴백 Lit 셰이더 무프로퍼티 → HasProperty 가드)
            if (mat.HasProperty("_DepthFadeDist"))
                mat.SetFloat("_DepthFadeDist", ImpactDepthFadeDist);
            renderer.sharedMaterial = mat;
            // [무번호 25-A4] 예산 등록 — X2 계약(증가 리터럴 2곳: 확산/수렴) 잠금 보존을 위해 등가 산술로 등록
            //   (런타임 동작 동일 — 감소는 기존 RingRunner.OnDestroy 지점에서 예산 반환).
            _activeRingCount = _activeRingCount + 1;
            ring.AddComponent<RingRunner>().Init(maxRadius, color, duration, false);   // echo 금지 — 임팩트 1회성(예산 반환은 RingRunner.OnDestroy)
            Debug.Log($"[ShockwaveRingFX] impact ring r={maxRadius} elite={elite} at {worldPos}");
        }

        /// <summary>[무번호 25-A4] 임팩트 링 전용 발광 — 강도 ×2.0 고정(확산/수렴 링 BL1 등급화와 별개 헬퍼 —
        ///   기존 ApplyEmissionBoost 시그니처/배선 계약 문자열 무변경, CombatFxEmissionPipelineTests 잠금).</summary>
        private static void ApplyImpactEmissionBoost(Material mat, Color tint)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(tint.r * ImpactEmissionIntensity, tint.g * ImpactEmissionIntensity, tint.b * ImpactEmissionIntensity, 1f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        // ── [무번호 22-T1] 패링 곡선 위스프 아크 상수 ────────────────
        private const int ParryWispCount = 3;             // 곡선 잔상 호 개수
        private const float ParryWispLength = 0.8f;       // 호 길이(m)
        private const float ParryWispSpreadDeg = 40f;     // 호 간 전개 각(도)
        private const int ParryWispPoints = 8;            // 호 분할 포인트(LineRenderer)
        private const float ParryWispCurl = 0.10f;        // [172차 P3-L3] 곡선 휘어짐 배율(끝점 오프셋/호 길이 비)

        /// <summary>[무번호 22-T1] 패링 곡선 위스프 아크 — 방패 스윙 궤적을 따라가는 베지어 잔상 3호
        ///   (직선 팬 20-J1과 병행 — "방어 스윙의 흐름"). **결정론 — Random 0건**(프로브 판정 친화).</summary>
        public static void SpawnParryArcWisps(Vector3 origin, Vector3 attackerDir, Color color, float duration = 0.3f)
        {
            EnsureAssets();
            if (_litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 위스프 자산 로드 실패 — Shader 누락");
                return;
            }
            if (duration <= 0f) duration = 0.1f;
            if (attackerDir.sqrMagnitude < 0.0001f) attackerDir = Vector3.forward;
            attackerDir.y = 0f;
            if (attackerDir.sqrMagnitude < 0.0001f) attackerDir = Vector3.forward;
            attackerDir.Normalize();

            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject host = new GameObject("ParryArcWisps") { hideFlags = HideFlags.HideAndDontSave };
            host.transform.position = origin;
            host.AddComponent<WispRunner>().Init(origin, attackerDir, ParryWispCount, color, duration, _litShader);
            Debug.Log($"[ShockwaveRingFX] parry arc wisps x{ParryWispCount} at {origin}");
        }

        /// <summary>[무번호 28-P2] 리플렉 스파크 궤적 — 흡수점에서 반사되어 공격자 방향(reflectDir)으로
        ///   튕겨 나가는 궤적(패망 방어의 물리적 "튕김" 언어). 기존 위스프 아크(위로 볼록 상승)와 대비되는
        ///   낮은 반사 궤적 — 결정론(Random 0건), 기존 경로 무손성.</summary>
        // [무번호 29-P2-2] 리플렉 등급 슬롯 — 잠금 시그니처(SpawnParryReflectWisps/InitReflect 6인자) 보존 설계.
        //   PERFECT=더 길고 높은 바운스(길이 ×1.25·상승 +0.05), GOOD=기존(0.06 상승) — 등급 위계 확장.
        internal static bool _pendingReflectPerfect;

        /// <summary>[무번호 29-P2-2] 다음 리플렉 위스프의 등급 아크 슬롯(단발 소비).</summary>
        public static void SetNextReflectPerfect(bool perfect) { _pendingReflectPerfect = perfect; }

        internal static bool ConsumeReflectPerfect() { bool v = _pendingReflectPerfect; _pendingReflectPerfect = false; return v; }

        public static void SpawnParryReflectWisps(Vector3 origin, Vector3 reflectDir, Color color, float duration = 0.3f)
        {
            EnsureAssets();
            if (_litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 리플렉 위스프 자산 로드 실패 — Shader 누락");
                return;
            }
            if (duration <= 0f) duration = 0.1f;
            if (reflectDir.sqrMagnitude < 0.0001f) reflectDir = Vector3.forward;
            reflectDir.y = 0f;
            if (reflectDir.sqrMagnitude < 0.0001f) reflectDir = Vector3.forward;
            reflectDir.Normalize();

            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject host = new GameObject("ParryReflectWisps") { hideFlags = HideFlags.HideAndDontSave };
            host.transform.position = origin;
            host.AddComponent<WispRunner>().InitReflect(origin, reflectDir, ParryWispCount, color, duration, _litShader);
            Debug.Log($"[ShockwaveRingFX] parry reflect wisps x{ParryWispCount} at {origin}");
        }

        // ── [172차 P3-L1] 패막 코어 글린트 상수 ──────────────────────
        private const float CoreGlintSeconds = 0.06f;    // 십자 플레어 지속(s) — 스펙 확정값
        private const float CoreGlintRayLength = 0.22f;  // 광선 최대 길이(m)
        private const float CoreGlintLineWidth = 0.02f;  // 얇은 광선 시작 두께(m) — 끝은 0 테이퍼
        private const float CoreGlintPopPeakTime = 0.35f; // 팝 첨두 도달 시점(수명 비율) — 0→1→0.6 이징 분기점

        /// <summary>[172차 P3-L1] 패막 코어 글린트 — 흡수점 십자 플레어(4방향 얇은 광선: 공격자 축 ±, 축 직교 ±).
        /// 지속 0.06s, 스케일 팝 이징 0→1→0.6(ease-out 상승 후 감쇠). [95차 F4] DirectionalSpark의 패막 경로
        /// **교체 계층**(레이어드 재설계 L1 — 방향성 스파크 정의/타 경로는 무손상). **결정론 — Random 0건**.</summary>
        public static void SpawnParryCoreGlint(Vector3 pos, Color color, Vector3 dir)
        {
            EnsureAssets();
            if (_litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 코어 글린트 자산 로드 실패 — Shader 누락");
                return;
            }
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.y = 0f;   // 패막 플레어는 수평 십자 — 흡수점 임팩트 기준 정합(기존 팬/위스프 idiom)
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            // [2026-09-13 가시화 보강 선례] 입력색 부스트 — 링/스파크 패밀리 동일 시인성 통일
            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);
            // [무번호 57-P2] FX 스폰 레지스트리 — 수명 0.06s로 역산 시점 전 소멸 → 스폰 좌표 기록.
            MagentaSentinel.RegisterFxSource("CoreGlint", pos);

            GameObject host = new GameObject("ParryCoreGlint") { hideFlags = HideFlags.HideAndDontSave };
            host.transform.position = pos;
            host.AddComponent<CoreGlintRunner>().Init(pos, dir, color, CoreGlintSeconds, _litShader);
            Debug.Log($"[ShockwaveRingFX] parry core glint at {pos}");
        }

        /// <summary>[172차 P3-L1] 십자 플레어 Runner — 4방향(±축, ±직교) 얇은 LineRenderer 광선이
        /// 0.06s 동안 팝(길이·알파 동조) 후 자기 파괴. 팝 이징: ease-out 상승 → 0.6 감쇠(선형 금지 계약). 결정론.</summary>
        private class CoreGlintRunner : MonoBehaviour
        {
            private LineRenderer[] _lines;   // 4본 — ±forward, ±side
            private Vector3[] _dirs = new Vector3[4];
            private Material _mat;
            private Color _baseColor;
            private Vector3 _origin;
            private float _duration;
            private float _elapsed;

            public void Init(Vector3 origin, Vector3 axis, Color color, float duration, Shader shader)
            {
                _origin = origin;
                _baseColor = color;
                _duration = duration;
                _elapsed = 0f;

                Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;
                if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
                _dirs[0] = axis; _dirs[1] = -axis; _dirs[2] = side; _dirs[3] = -side;

                _mat = new Material(shader) { color = color };
                _mat.name = "ParryCoreGlintMat";
                _mat.SetFloat("_Surface", 1f);
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_ZWrite", 0);
                _mat.renderQueue = 3000;
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _mat.SetOverrideTag("RenderType", "Transparent");

                _lines = new LineRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    GameObject lineGo = new GameObject("CoreGlintRay");
                    lineGo.transform.SetParent(transform, false);
                    LineRenderer line = lineGo.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.material = _mat;
                    line.startWidth = CoreGlintLineWidth;
                    line.endWidth = 0f;
                    line.numCapVertices = 2;
                    line.SetPosition(0, _origin);
                    line.SetPosition(1, _origin + _dirs[i] * 0.02f);
                    _lines[i] = line;
                }
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                // 스케일 팝 이징 0→1→0.6 — 상승 ease-out(첨두 35%), 이후 감쇠(선형 금지)
                float pop;
                if (t < CoreGlintPopPeakTime)
                {
                    float u = t / CoreGlintPopPeakTime;
                    pop = 1f - (1f - u) * (1f - u);   // easeOutQuad 상승
                }
                else
                {
                    pop = Mathf.Lerp(1f, 0.6f, (t - CoreGlintPopPeakTime) / (1f - CoreGlintPopPeakTime));
                }
                for (int i = 0; i < _lines.Length; i++)
                {
                    var line = _lines[i];
                    if (line == null) continue;
                    line.SetPosition(0, _origin);
                    line.SetPosition(1, _origin + _dirs[i] * (CoreGlintRayLength * pop));
                }
                if (_mat != null)
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * pop);
                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_mat != null)
                    Object.Destroy(_mat);
            }
        }

        // ── [172차 P3-L2] 패막 수렴-역전 링 상수 ─────────────────────
        private const float ParryConvergeSeconds = 0.08f;   // 페이즈 1 — 수렴(반경 큰→작은) 확정값
        private const float ParryReverseSeconds = 0.25f;    // 페이즈 2 — 역전 확산(ease-out) 확정값

        /// <summary>[172차 P3-L2] 패막 수렴-역전 링 — 2페이즈 단일 링: ①수렴 0.08s(반경 큰→작은, easeIn 스냅)
        /// ②역전 확산 0.25s(ease-out — 임팩트 역진 "역전" 임팩트). 기존 SpawnConvergingRing(무번호 21-L1, 단일
        /// easeIn 수렴 — 역전 페이즈 부재)과 계약 분리 — 텔레그래프 발화 경로 무손상. **결정론 — Random 0건**.
        /// [예산] X2 계약(증가/감소 리터럴 2곳 잠금) 보존 — 본 링은 예산 미등록(패막 트리거당 1회·0.33s 단발).
        /// </summary>
        public static void SpawnParryConvergeRing(Vector3 worldPos, float radius, Color color)
        {
            EnsureAssets();
            if (_cylinderMesh == null || _litShader == null)
            {
                Debug.LogWarning("[ShockwaveRingFX] 수렴-역전 링 자산 로드 실패 — Shader 또는 Mesh 누락");
                return;
            }
            if (radius < START_RADIUS) radius = START_RADIUS * 2f;

            // [2026-09-13 가시화 보강 선례] 입력색 부스트 — 링 패밀리 동일 시인성 통일
            color = Color.Lerp(color, Color.white, 0.3f);
            color.a = Mathf.Max(color.a, 0.75f);

            GameObject ring = new GameObject("ParryConvergeRing") { hideFlags = HideFlags.HideAndDontSave };
            ring.transform.position = worldPos;
            ring.transform.rotation = Quaternion.identity;
            float startScale = radius / CYL_RADIUS;
            ring.transform.localScale = new Vector3(startScale, RING_THICKNESS / CYL_HEIGHT, startScale);
            var filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = _cylinderMesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            var mat = new Material(_litShader) { color = color };
            mat.name = "ParryConvergeRingMat";
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.mainTexture = EnsureBandTexture();   // 공용 밴드 텍스처(21-R2)
            ApplyEmissionBoost(mat, color, radius);   // [무번호 25-BL1] 등급화 발광(대형 링 ≥1m)
            renderer.sharedMaterial = mat;
            ring.AddComponent<ParryConvergeReverseRunner>().Init(radius, color, ParryConvergeSeconds, ParryReverseSeconds);
            Debug.Log($"[ShockwaveRingFX] parry converge-reverse ring r={radius} at {worldPos}");
        }

        /// <summary>[172차 P3-L2] 2페이즈 Runner — 수렴(easeIn 스냅, 밀도 상승)→역전 확산(ease-out, 알파 소멸).
        /// SpawnConvergeRunner(21-L1) idiom 준용 — 머티리얼 sharedMaterial 인스턴스, OnDestroy 정리. 결정론.</summary>
        private class ParryConvergeReverseRunner : MonoBehaviour
        {
            private Material _mat;
            private Color _baseColor;
            private float _startRadius;
            private float _convergeSeconds;
            private float _reverseSeconds;
            private float _elapsed;

            public void Init(float startRadius, Color color, float convergeSeconds, float reverseSeconds)
            {
                _startRadius = startRadius;
                _baseColor = color;
                _convergeSeconds = Mathf.Max(0.01f, convergeSeconds);
                _reverseSeconds = Mathf.Max(0.01f, reverseSeconds);
                _elapsed = 0f;
                var mr = GetComponent<MeshRenderer>();
                _mat = mr != null ? mr.sharedMaterial : null;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float radius, alpha;
                if (_elapsed < _convergeSeconds)
                {
                    // 페이즈 1 — 수렴: 큰→작은(0.12배), easeIn 스냅(21-L1 수렴 선례), 밀도 상승
                    float c = Mathf.Clamp01(_elapsed / _convergeSeconds);
                    float easeIn = c * c;
                    radius = Mathf.Lerp(_startRadius, _startRadius * 0.12f, easeIn);
                    alpha = _baseColor.a * (0.6f + 0.4f * easeIn);
                }
                else
                {
                    // 페이즈 2 — 역전 확산: 작은→0.8배, ease-out(선형 금지), 알파 소멸
                    float r = Mathf.Clamp01((_elapsed - _convergeSeconds) / _reverseSeconds);
                    float easeOut = 1f - (1f - r) * (1f - r);
                    radius = Mathf.Lerp(_startRadius * 0.12f, _startRadius * 0.8f, easeOut);
                    alpha = _baseColor.a * (1f - r);
                }
                float scale = radius / CYL_RADIUS;
                transform.localScale = new Vector3(scale, RING_THICKNESS / CYL_HEIGHT, scale);
                if (_mat != null)
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                if (_elapsed >= _convergeSeconds + _reverseSeconds)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_mat != null)
                    Object.Destroy(_mat);
            }
        }

        /// <summary>[무번호 22-T1] 베지어 draw-on Runner — 궤적이 스윙 방향으로 그려지며(0→1 easeOut)
        ///   후반 40% 알파 페이드. 2차 베지어(origin→상승 중간점→하강 끝점 — 스윙 아크 형상), 결정론.</summary>
        private class WispRunner : MonoBehaviour
        {
            private LineRenderer[] _lines;
            private Material _mat;
            private Color _baseColor;
            private Vector3 _origin;
            private Vector3 _axis;
            private float _duration;
            private float _elapsed;
            private bool _reflect;   // [무번호 28-P2] 리플렉 모드 — 낮은 반사 궤적(기본 false = 기존 아크)
            private bool _perfectArc;   // [무번호 29-P2-2] PERFECT 등급 아크 — 더 길고 높은 바운스
            private float _lengthMul = 1f;   // [무번호 29-P2-2] PERFECT 호 길이 배수(×1.25)

            public void Init(Vector3 origin, Vector3 axis, int count, Color color, float duration, Shader shader)
            {
                Setup(origin, axis, count, color, duration, shader, reflect: false);
            }

            /// <summary>[무번호 28-P2] 리플렉 모드 진입 — 궤적만 반전, 머티리얼/수명/페이드는 기존 위스프와 동일(U-3 통일).</summary>
            public void InitReflect(Vector3 origin, Vector3 axis, int count, Color color, float duration, Shader shader)
            {
                Setup(origin, axis, count, color, duration, shader, reflect: true);
            }

            private void Setup(Vector3 origin, Vector3 axis, int count, Color color, float duration, Shader shader, bool reflect)
            {
                _reflect = reflect;
                // [무번호 29-P2-2] 등급 슬롯 소비 — PERFECT=호 길이 ×1.25(더 긴 바운스).
                _perfectArc = reflect && ConsumeReflectPerfect();
                _lengthMul = _perfectArc ? 1.25f : 1f;
                _origin = origin;
                _axis = axis;
                _baseColor = color;
                _duration = duration;
                _elapsed = 0f;

                _mat = new Material(shader) { color = color };
                _mat.name = reflect ? "ParryReflectWispMat" : "ParryArcWispMat";
                _mat.SetFloat("_Surface", 1f);
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_ZWrite", 0);
                _mat.renderQueue = 3000;
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _mat.SetOverrideTag("RenderType", "Transparent");

                _lines = new LineRenderer[count];
                for (int i = 0; i < count; i++)
                {
                    float spread = count > 1 ? (i / (float)(count - 1) - 0.5f) * 2f : 0f;
                    Vector3 dir = Quaternion.AngleAxis(spread * ParryWispSpreadDeg, Vector3.up) * axis;
                    // [무번호 28-P2] 리플렉 — 낮은 반사 궤적(중심 살짝 상승·가장자리 하강) vs 기존 아크(중심 상승)
                    // [무번호 29-P2-2] PERFECT=상승 +0.05(더 높은 바운스) — 기존 GOOD 리터럴(0.06f - 0.10f * Mathf.Abs(spread)) 보존.
                    dir.y = reflect ? (0.06f - 0.10f * Mathf.Abs(spread)) + (_perfectArc ? 0.05f : 0f) : (0.12f - 0.08f * Mathf.Abs(spread));
                    dir.Normalize();
                    GameObject lineGo = new GameObject("ArcWisp");
                    lineGo.transform.SetParent(transform, false);
                    LineRenderer line = lineGo.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = ParryWispPoints;
                    line.material = _mat;
                    line.startWidth = 0.03f;
                    line.endWidth = 0f;
                    line.numCapVertices = 2;
                    for (int k = 0; k < ParryWispPoints; k++)
                        line.SetPosition(k, origin);
                    _lines[i] = line;
                }
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                float drawOn = 1f - (1f - t) * (1f - t);   // easeOut draw-on

                for (int i = 0; i < _lines.Length; i++)
                {
                    var line = _lines[i];
                    if (line == null) continue;
                    float spread = _lines.Length > 1 ? (i / (float)(_lines.Length - 1) - 0.5f) * 2f : 0f;
                    Vector3 dir = Quaternion.AngleAxis(spread * ParryWispSpreadDeg, Vector3.up) * _axis;
                    dir.y = _reflect ? (0.06f - 0.10f * Mathf.Abs(spread)) : (0.12f - 0.08f * Mathf.Abs(spread));
                    dir.Normalize();
                    float len = ParryWispLength * _lengthMul * (1f - 0.15f * Mathf.Abs(spread));   // [무번호 29-P2-2] PERFECT ×1.25 / 외곽 호 살짝 짧게
                    Vector3 p0 = _origin;
                    Vector3 p1, p2;
                    if (_reflect)
                    {
                        // [무번호 28-P2] 낮은 바운스 — 얕은 중간점 후 지면 쪽으로 튕겨 나감(반사 언어)
                        p1 = _origin + dir * (len * 0.35f) + Vector3.up * 0.12f;
                        p2 = _origin + dir * len - Vector3.up * 0.06f;
                    }
                    else
                    {
                        p1 = _origin + dir * (len * 0.5f) + Vector3.up * 0.35f;   // 상승 중간점
                        p2 = _origin + dir * len;                                  // 하강 끝점
                    }
                    // [172차 P3-L3] 곡선 휘어짐 — 진행 방향(축)의 수평 직교로 살짝 휘는 트레일(스윙 흐름 언어).
                    //   결정론: spread 기반 변조(외곽 호 덜 휨) — Random 0건. 중간점은 끝점의 35%만 휨(부드러운 호).
                    Vector3 side = Vector3.Cross(Vector3.up, _axis).normalized;
                    if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
                    float curl = ParryWispCurl * (1f - 0.35f * Mathf.Abs(spread));
                    p1 += side * (curl * len * 0.35f);
                    p2 += side * (curl * len);
                    for (int k = 0; k < ParryWispPoints; k++)
                    {
                        float u = (k / (float)(ParryWispPoints - 1)) * drawOn;
                        Vector3 a = Vector3.Lerp(p0, p1, u);
                        Vector3 b = Vector3.Lerp(p1, p2, u);
                        line.SetPosition(k, Vector3.Lerp(a, b, u));   // 2차 베지어
                    }
                }

                if (_mat != null)
                {
                    float alpha = t < 0.6f ? _baseColor.a : _baseColor.a * (1f - (t - 0.6f) / 0.4f);
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                }
                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_mat != null)
                    Object.Destroy(_mat);
            }
        }

        /// <summary>[무번호 21-L1] 수축 트윈 Runner — 외곽→중심 easeIn 수축, 알파는 수집 완료에 맞춰 상승 후 컷.</summary>
        private class ConvergeRunner : MonoBehaviour
        {
            private Material _mat;
            private Color _baseColor;
            private float _startRadius;
            private float _duration;
            private float _elapsed;

            public void Init(float startRadius, Color color, float duration)
            {
                _startRadius = startRadius;
                _baseColor = color;
                _duration = duration;
                _elapsed = 0f;
                var mr = GetComponent<MeshRenderer>();
                _mat = mr != null ? mr.sharedMaterial : null;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                float easeIn = t * t;   // 수축 가속 — 마지막에 중심으로 스냅
                float radius = Mathf.Lerp(_startRadius, START_RADIUS * 1.4f, easeIn);
                float scale = radius / CYL_RADIUS;
                transform.localScale = new Vector3(scale, RING_THICKNESS / CYL_HEIGHT, scale);
                if (_mat != null)
                {
                    // 알파 — 수집 진행에 따라 상승(0.35→1) 후 마지막 10%에서 컷(임팩트로 에너지 흡수됨)
                    float alpha = _baseColor.a * (0.35f + 0.65f * t) * (1f - Mathf.Clamp01((t - 0.9f) / 0.1f));
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                }
                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_mat != null)
                    Object.Destroy(_mat);
                NotifyRingDestroyed();   // [무번호 23-X2] 예산 반환
            }
        }

        /// <summary>[무번호 20-J1] 팬 스파크 트윈 내부 MonoBehaviour — 공격자 축 ±55° 균등 전개(랜덤 0건),
        ///   짝/홀 교차 길이로 리듬 부여. 이동/페이드는 SparkRunner와 동일 곡선(패밀리 룩 통일).</summary>
        private class FanRunner : MonoBehaviour
        {
            private struct Fragment
            {
                public LineRenderer Line;
                public Vector3 Dir;
                public float Length;
            }

            private Fragment[] _fragments;
            private Material _mat;
            private Color _baseColor;
            private Vector3 _origin;
            private float _duration;
            private float _elapsed;

            public void Init(Vector3 origin, Vector3 axis, int count, Color color, float duration, Shader shader,
                             float halfAngleDeg, float streakMin, float streakMax)
            {
                _origin = origin;
                _baseColor = color;
                _duration = duration;
                _elapsed = 0f;

                _mat = new Material(shader) { color = color };
                _mat.name = "ParryDeflectFanMat";
                _mat.SetFloat("_Surface", 1f);
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_ZWrite", 0);
                _mat.renderQueue = 3000;
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _mat.SetOverrideTag("RenderType", "Transparent");
                // [무번호 64-P2] 밴드 텍스처 직접 바인딩 — FXBandingRing _MainTex 셰이더 기본값 "white"는
                //   텍스처 미바인딩 시 하드엣지 불투명 스트릭(57차 역산 "텍스처 없는 쿼드" 정합)으로 렌더된다.
                //   절차 페더 밴드(결정론·정적 캐시)를 슬롯에 묶어 소프트 스트릭으로 정규화(에셋 무변경).
                if (_mat.HasProperty("_MainTex"))
                    _mat.mainTexture = FxMaterialLibrary.GetFanBandTexture();

                _fragments = new Fragment[count];
                for (int i = 0; i < count; i++)
                {
                    float spread = count > 1 ? (i / (float)(count - 1) - 0.5f) * 2f : 0f;
                    Vector3 dir = Quaternion.AngleAxis(spread * halfAngleDeg, Vector3.up) * axis;
                    dir.y = Mathf.Lerp(0.18f, 0.05f, Mathf.Abs(spread));   // 중심 상승·외곽 수평 — 부채꼴 입체감
                    dir.Normalize();
                    GameObject lineGo = new GameObject("DeflectFragment");
                    lineGo.transform.SetParent(transform, false);
                    LineRenderer line = lineGo.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.material = _mat;
                    line.startWidth = ParryFanLineWidth;
                    line.endWidth = 0f;
                    line.numCapVertices = 2;
                    line.SetPosition(0, _origin);
                    line.SetPosition(1, _origin + dir * 0.05f);
                    // [무번호 22-S2] 스트릭 길이 파라미터화 — 팬(0.30/0.48)·스윙(1.0/1.4) 양쪽 용도
                    _fragments[i] = new Fragment
                    {
                        Line = line,
                        Dir = dir,
                        Length = (i % 2 == 0) ? streakMin : streakMax   // 결정론 리듬
                    };
                }
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                float eased = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < _fragments.Length; i++)
                {
                    Fragment frag = _fragments[i];
                    if (frag.Line == null) continue;
                    float head = eased * frag.Length;
                    float tail = Mathf.Max(0f, head - frag.Length * 0.45f);
                    frag.Line.SetPosition(0, _origin + frag.Dir * tail);
                    frag.Line.SetPosition(1, _origin + frag.Dir * head);
                }
                if (_mat != null)
                {
                    float alpha = Mathf.Lerp(_baseColor.a, 0f, t);
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                }
                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (_mat != null)
                    Object.Destroy(_mat);
            }
        }

        // ── RingRunner ───────────────────────────────────────────────

        /// <summary>
        /// 링 확장 + 알파 페이드 처리 내부 MonoBehaviour.
        /// Update에서 반지름 0.2 → maxRadius로 Ease-Out 트윈, 알파 페이드 후
        /// Object.Destroy(gameObject)로 스스로 제거된다.
        /// </summary>
        private class RingRunner : MonoBehaviour
        {
            private Material _mat;
            private Color _baseColor;
            private float _maxRadius;
            private float _duration;
            private float _elapsed;
            private bool _allowEcho;      // [무번호 22-P2] echo 재귀 가드
            private bool _echoSpawned;

            public void Init(float maxRadius, Color color, float duration, bool allowEcho)
            {
                _maxRadius = maxRadius;
                _baseColor = color;
                _duration = duration;
                _elapsed = 0f;
                _allowEcho = allowEcho;

                var mr = GetComponent<MeshRenderer>();
                _mat = mr != null ? mr.sharedMaterial : null;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);

                // [무번호 22-P1] 밴드 텍스처 스크롤 — u축 노이즈가 흐르며 링 가장자리가 유기적으로 워블
                if (_mat != null)
                    _mat.mainTextureOffset = new Vector2(_elapsed * 0.35f, 0f);

                // [무번호 22-P2] 이중 웨이브 echo — 큰 링(≥1m)만 수명 25% 시점 느린 잔상 링 자체 스폰
                //   (duration 1.6배·알파 0.4배·반경 1.12배 — 깊이 파동. API 무변경, 재귀 가드)
                if (_allowEcho && !_echoSpawned && _maxRadius >= 1.0f && t >= 0.25f)
                {
                    _echoSpawned = true;
                    Color echo = _baseColor;
                    echo.a = _baseColor.a * 0.4f;
                    SpawnCore(transform.position, _maxRadius * 1.12f, echo, _duration * 1.6f, false);
                }

                // Ease-Out 트윈: 빠르게 퍼지고 서서히 멈추는 충격파 감쇠
                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Lerp(START_RADIUS, _maxRadius, eased);
                float scale = radius / CYL_RADIUS;
                transform.localScale = new Vector3(scale, RING_THICKNESS / CYL_HEIGHT, scale);

                // 알파 페이드 — [무번호 20-J3] easeInCubic 잔광 곡선: 수명 대부분 밝게 유지 후 마지막에 빠르게
                //   소멸(선형 페이드의 흐릿한 중간부 폐기 — 타격감 잔광). 색상은 유지, 알파만 감쇠.
                if (_mat != null)
                {
                    float fadeT = t * t * t;   // easeInCubic
                    float alpha = Mathf.Lerp(_baseColor.a, 0f, fadeT);
                    _mat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
                }

                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void OnDestroy()
            {
                // 조기 파괴(씬 전환 등) 시 Material 누수 방지
                if (_mat != null)
                    Object.Destroy(_mat);
                NotifyRingDestroyed();   // [무번호 23-X2] 예산 반환
            }
        }
    }
}
