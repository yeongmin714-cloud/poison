using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Systems
{
    /// <summary>
    /// P20-7 + P26 — 우클릭 명령 지점 표시.
    /// - 이동 명령(비공격): **지속형 고품질 지면 원형 링**(MoveTargetRing 베이크 PNG, 골드).
    ///   [72차 유저 규약] 지속형도 **표시 후 1s → 축소 페이드 0.3s → 소멸** — 도착 소멸 조건 폐기.
    /// - 공격 명령: 기존 페이드형 링(SelectionRing 셰이더 우선, 절차 링 폴백) — [72차] 1s 표시 + 0.3s 축소 페이드로 통일.
    /// Quad 바닥, 지면 위 0.03 (짚단 데칼 위).
    /// </summary>
    public class CommandMarker : MonoBehaviour
    {
        // [72차] 유저 규약 우선 — 표시 1s / 축소 페이드 0.3s(67차 SelectionRingController 규약 동일).
        private const float FadeSeconds = 0.3f;
        private float _life = 1f;
        private float _elapsed;
        private Vector3 _startScale;
        private Renderer _rend;
        private Color _baseColor = CommandRingPresentation.MoveTargetColor;   // 골드 (팀색)
        private bool _persistent;        // 이동 명령 지속형 여부
        private Transform _tickLayer;    // [무번호 37-R2] 내부 회전 눈금 링(지속형 전용 추가 레이어)
        /// <summary>지속형 마커의 소유 병사 — [72차] 소멸은 1s 규약으로 단일화(참조 보존용 public 필드).</summary>
        public GuardPlaceholder owner;

        /// <summary>
        /// 명령 지점 표시. color 미지정 시 골드. owner != null이면 **지속형**(표시 후 1s 소멸 — 72차 유저 규약),
        /// owner == null이면 페이드형(공격 명령용) — [72차] 양쪽 모두 1s 표시 + 0.3s 축소 페이드로 통일.
        /// </summary>
        public static CommandMarker Spawn(Vector3 position, Color? color = null, GuardPlaceholder owner = null, float? scaleOverride = null, System.Collections.Generic.List<GuardPlaceholder> groupOwners = null)
        {
            // [104차 F1] 건설 홀로그램 배치 중 — 우클릭 명령 마커 생성 금지 (배치 클릭과 충돌 방지)
            if (ConstructionHologram.IsPlacementActive) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CommandMarker";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = new Vector3(position.x, position.y + 0.03f, position.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // [2026-10-11 사용자 요구] 이동 포인트 링 = 현행 절반 — scaleOverride로 지속 마커만 축소.
            go.transform.localScale = Vector3.one * (scaleOverride ?? CommandRingPresentation.BaseScale);
            var marker = go.AddComponent<CommandMarker>();
            if (color.HasValue) marker._baseColor = color.Value;
            marker._persistent = owner != null || (groupOwners != null && groupOwners.Count > 0);
            marker.owner = owner;
            marker._groupOwners = groupOwners;   // [무번호 19-F2] 이동 링 도착 진행도 추적용 그룹 보존
            return marker;
        }

        private float _burstElapsed = -1f;   // [15차] 명령 ack 확산 펄스 진행(>=0 활성)
        private System.Collections.Generic.List<GuardPlaceholder> _groupOwners;   // [무번호 19-F2] 이동 링 도착 진행도 추적 그룹
        private float _initDist = -1f;   // [무번호 19-F2] 스폰 시점 그룹 평균 거리(<=0 = 진행도 미계산)
        // [무번호 65-P1] 데칼 경로 폐기로 _proj/_decalMat 제거 — SetMarkerAlpha는 쿼드 머티리얼만 구동.

        private void Start()
        {
            _rend = GetComponent<Renderer>();
            // [2026-10-11 사용자 요구 — 검은 사각형 제거] 명령 링 쿼드도 지면 데칼 — 그림자 방출/수신 금지.
            _rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rend.receiveShadows = false;
            // [무번호 65-P1] 데칼 경로 **완전 폐기** — 항상 쿼드 경로(SetupPersistent/SetupFading).
            //   근거(공격씬 테스트 12 실측 + 58-P2 프로브): ①URP 내장 Decal 셰이더 = Shader.Find NULL(58-P2
            //   스트리핑 실측) → 1순위 항상 실패 ②2순위 매뉴얼 카피 데칼 그래프는 신규 머티리얼
            //   기본값 color(0,0,0,0)+텍스처 대입 무시(슬롯 불일치 — 58-P2 프로브 확정) → DecalProjector 박스
            //   전체가 **불투명 흑 박스**로 렌더 = 전투 중 이동/집결 명령(지속형)마다 화면에 검은 사각.
            //   ③데칼 패스는 [51-SCREEN] 스캔 RT의 수동 렌더 경로에서 스킵(§13 실측) → 센서 사각.
            //   쿼드 경로는 SelectionRing/절차 링 텍스처로 검증 렌더 — B5 부유 지적은 클리핑 트레이드오프로 수용.
            if (_persistent) SetupPersistent();
            else SetupFading();
            _startScale = transform.localScale;
            // [무번호 19-F2] 이동 링 도착 진행도 — 초기 평균 거리 측정(스폰 시점 그룹 병사 ↔ 목적지)
            _initDist = ComputeAvgDist();
            _burstElapsed = 0f;   // [15차] 모든 명령 마커에 1회 ack 확산
            // [무번호 82 보류] TrySpawnMoveChevrons();   // [무번호 35-R1] 이동 명령 경로 체브런(방향 감각) — 사용자 요구(11-20): 이동 경로 표시 제거, 목적지 링만
        }

        /// <summary>버스트/페이 공통 알파 대입 — 셰이더(_TeamColor)/폴백(_Color) 모두.</summary>
        private void SetMarkerAlpha(float a)
        {
            a = Mathf.Clamp01(a);
            // [무번호 65-P1] 데칼 fadeFactor 경로 폐기 — 쿼드 머티리얼 알파만 구동.
            if (_rend == null) return;
            var m = _rend.sharedMaterial;
            if (m == null) return;
            if (m.HasProperty("_TeamColor")) { var c = m.GetColor("_TeamColor"); c.a = a; m.SetColor("_TeamColor", c); }
            else if (m.HasProperty("_Color")) { var c = m.GetColor("_Color"); c.a = a; m.SetColor("_Color", c); }
        }

        /// <summary>지속형 — 베이크 고품질 지면 링 텍스처(MoveTargetRing) + 팀색. 셰이더가 없어도 렌더.</summary>
        private void SetupPersistent()
        {
            _rend = GetComponent<Renderer>();
            CommandRingPresentation.Apply(_rend, _baseColor);
        }

        /// <summary>페이드형 — SelectionRing 셰이더 우선, 없으면 절차 링 텍스처(공격 명령).</summary>
        private void SetupFading()
        {
            var shader = Shader.Find("Custom/SelectionRing");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_TeamColor", _baseColor);
                mat.SetFloat("_RingRadius", 0.8f);
                mat.SetFloat("_RingThickness", 0.12f);
                mat.SetFloat("_PulseSpeed", 0f);
                mat.SetFloat("_ArcSpeed", 1.5f);
            }
            else
            {
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(unlit != null ? unlit : Shader.Find("Sprites/Default"));
                mat.mainTexture = BuildRingTexture(256);
                mat.color = _baseColor;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            if (_rend != null) _rend.sharedMaterial = mat;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            // [무번호 37-R2] 내부 회전 눈금 링 — 지속형(이동/집결) 전용 "명령 수용됨" 회전 피드백.
            //   67차 SelectionRing과 별개(명령 마커 전용), 84차/72차 규약 무손상(자식 레이어 — 본체 무수정).
            if (_persistent && Application.isPlaying)
            {
                if (_tickLayer == null) CreateTickLayer();
                if (_tickLayer != null) _tickLayer.Rotate(0f, -220f * Time.deltaTime, 0f, Space.Self);
            }
            // [15차 입체감] rim 하이라이트 방향 갱신(버스트/페이 공통).
            CommandRingPresentation.UpdateRimDirection(_rend, _rend != null ? _rend.sharedMaterial : null);

            // [15차] 명령 ack 확산 펄스 — 스폰 순간 스케일 1.30→1.0 + 알파 램프인(0.35s, 1회).
            // [84차] 동일 구간에 셰이더 확산 웨이브(_WaveT 0→1) 동기 — 중심→외곽으로 달려나가는 링.
            if (_burstElapsed >= 0f)
            {
                if (_burstElapsed < CommandRingPresentation.AckBurstDuration)
                {
                    _burstElapsed += Time.deltaTime;
                    float burstT = Mathf.Clamp01(_burstElapsed / CommandRingPresentation.AckBurstDuration);
                    transform.localScale = _startScale * Mathf.Lerp(CommandRingPresentation.AckBurstStartScale, 1f, burstT);
                    SetMarkerAlpha(Mathf.Lerp(CommandRingPresentation.AckBurstStartAlpha, 1f, burstT));
                    var burstMat = _rend != null ? _rend.sharedMaterial : null;
                    if (burstMat != null && burstMat.HasProperty("_WaveT"))
                        burstMat.SetFloat("_WaveT", burstT);   // [84차] 확산 웨이브 구동
                    return;
                }
                // 버스트 종료 — 웨이브 off 원복(1회성 보장)
                _burstElapsed = -1f;
                var endMat = _rend != null ? _rend.sharedMaterial : null;
                if (endMat != null && endMat.HasProperty("_WaveT"))
                    endMat.SetFloat("_WaveT", -1f);
            }

            // [72차] 표시 구간(_life=1s) — 지속형은 미세 펄스, 페이드형은 완만한 축소.
            //   도착 소멸 조건 폐기 — 유저가 "표시 후 1초 뒤" 명시(유저 규약 우선).
            if (_elapsed < _life)
            {
                if (_persistent)
                {
                    // [무번호 19-F2 + 20-I3] 도착 진행도 — smoothstep 이징(자연 감쇠) + 임박 가속(≥0.8에서 추가 10% 조임
                    //   = "링이 빨리 조여든다 = 곧 도착" 신호). [72차] 1s 규약·도착 소멸은 미적용 계약 불변.
                    float progress = Mathf.SmoothStep(0f, 1f, ComputeArrivalProgress());
                    float shrink = progress < 0.8f ? 0.25f * progress : 0.25f + 0.10f * ((progress - 0.8f) / 0.2f);
                    transform.localScale = CommandRingPresentation.GetPulseScale(_startScale, _elapsed) * (1f - shrink);
                }
                else
                    transform.localScale = _startScale * (1f - 0.15f * Mathf.Clamp01(_elapsed / _life));
                SetMarkerAlpha(1f);
                return;
            }

            // [72차] 공통 소멸 페이드 — 표시 후 0.3s 축소+페이드 → 소멸(공격 페이드형과 동일 규약 통일).
            float fadeT = Mathf.Clamp01((_elapsed - _life) / FadeSeconds);
            transform.localScale = _startScale * (1f - 0.45f * fadeT);
            SetMarkerAlpha(1f - fadeT);
            if (fadeT >= 1f)
                Destroy(gameObject);
        }

        /// <summary>[무번호 19-F2] 그룹 병사 → 마커(목적지) 평균 수평 거리. 병사 부재/사망 시 -1.</summary>
        private float ComputeAvgDist()
        {
            if (_groupOwners == null || _groupOwners.Count == 0) return -1f;
            float sum = 0f; int n = 0;
            Vector3 dest = transform.position;
            foreach (var g in _groupOwners)
            {
                if (g == null || !g.gameObject.activeInHierarchy) continue;
                Vector3 d = dest - g.transform.position; d.y = 0f;
                sum += d.magnitude; n++;
            }
            return n > 0 ? sum / n : -1f;
        }

        /// <summary>[무번호 19-F2] 도착 진행도(0=시작, 1=도착) — 병사 미추적 시 0.</summary>
        private float ComputeArrivalProgress()
        {
            if (_initDist <= 0.001f) return 0f;
            float dist = ComputeAvgDist();
            if (dist < 0f) return 0f;
            return Mathf.Clamp01(1f - dist / _initDist);
        }

        // ================================================================
        // [무번호 35-R1] 이동 경로 체브런 — 명령 방향 감각(72차 1s 규약 수명 동기)
        // ================================================================

        /// <summary>[35-R1] 지속형(이동/집결) 마커 전용 — 그룹 병사 평균 위치 → 목적지 방향 체브런 3본 스태거 배치.
        ///  페이드형(공격) 미발화. 병사 부재/거리 2m 미만 = 미발화(무의미 방지). 외부 난수 미사용 — 결정론.</summary>
        private void TrySpawnMoveChevrons()
        {
            if (!_persistent || _groupOwners == null || _groupOwners.Count == 0) return;
            try
            {
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (var g in _groupOwners)
                {
                    if (g == null || !g.gameObject.activeInHierarchy) continue;
                    sum += g.transform.position; n++;
                }
                if (n == 0) return;
                Vector3 dir = transform.position - sum / n; dir.y = 0f;
                if (dir.sqrMagnitude < 4f) return;   // 2m 미만 — 방향 무의미
                dir.Normalize();
                var mat = EnsureChevronMaterial();
                if (mat == null) return;   // 셰이더 부재 폴백 — 기존 링만
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                for (int i = 1; i <= 3; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    var col = go.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    go.name = $"CommandChevron_{i}";
                    Vector3 p = transform.position - dir * (1.4f * i);
                    go.transform.position = new Vector3(p.x, transform.position.y, p.z);
                    go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                    go.transform.localScale = Vector3.one * 0.55f;
                    var r = go.GetComponent<MeshRenderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.sharedMaterial = mat;   // 공유 머티리얼 단일 소스 — 등장/소멸은 스케일로(인스턴스 난립 방지)
                    go.AddComponent<ChevronRunner>().Init(0.08f * (i - 1));   // 스태거 팝 인
                }
            }
            catch (System.Exception) { }   // 시각 보조 — 명령 흐름 절대 방해 금지
        }

        private static Material _chevronMat;

        private static Texture2D _tickTex;
        private static Material _tickMat;

        /// <summary>[무번호 37-R2] 내부 회전 눈금 레이어 생성 — 마커 자식 쿼드(반경 62%), 지면+0.02 래프팅.
        /// 생성 실패(머티리얼 부재) 시 레이어 없음(기존 링만 — 폴백 계약). 결정론(Random 0건).</summary>
        private void CreateTickLayer()
        {
            var mat = EnsureTickMaterial();
            if (mat == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "CommandTickRing";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.62f, 0.62f, 1f);   // 부모 스케일(링 지름) 대비 62% — 링 내부
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _tickLayer = go.transform;
        }

        /// <summary>[무번호 37-R2] 눈금 머티리얼 — 절차 방사 대시 텍스처(캐시 1회) + 마커색 틴트(인스턴스).</summary>
        private static Material EnsureTickMaterial()
        {
            if (_tickMat != null) return _tickMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var mat = new Material(shader) { name = "CommandTickRingMat" };
            if (_tickTex == null) _tickTex = BuildTickTexture(128);
            mat.mainTexture = _tickTex;
            mat.color = CommandRingPresentation.MoveTargetColor;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            FxMaterialLibrary.EnsureUnlitTransparent(mat);   // [무번호 39] 표준화(눈금 링 동일 수리)
            _tickMat = mat;
            return mat;
        }

        /// <summary>[무번호 37-R2] 절차 방사 대시 텍스처 — 16눈금(반경 55~72%, 가장자리 AA, 캐시 1회 — 결정론).</summary>
        private static Texture2D BuildTickTexture(int size)
        {
            if (_tickTex != null) return _tickTex;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            int ticks = 16;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0~1
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (ang < 0) ang += 360f;
                    float seg = 360f / ticks;
                    float dSeg = Mathf.Abs(Mathf.Repeat(ang + seg * 0.5f, seg) - seg * 0.5f);
                    bool inTick = dSeg < seg * 0.16f && r > 0.55f && r < 0.72f;
                    byte a = inTick ? (byte)230 : (byte)0;
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>[35-R1] 체브런 공유 머티리얼 — 절차 화살표 텍스처(캐시 1회 — 결정론) + 팀색 틴트(SetupFading 폴백 동일 패턴).</summary>
        private static Material EnsureChevronMaterial()
        {
            if (_chevronMat != null) return _chevronMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var mat = new Material(shader) { name = "CommandChevronMat" };
            mat.mainTexture = BuildChevronTexture(128);
            mat.color = CommandRingPresentation.MoveTargetColor;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            FxMaterialLibrary.EnsureUnlitTransparent(mat);   // [무번호 39] 표준화 — _Surface 누락 불투명 사각 렌더 수리
            _chevronMat = mat;
            return mat;
        }

        /// <summary>[무번호 37-M1] 체브런 공유 머티리얼 접근자 — 예고 수렴 눈금이 동일 텍스처/틴트 재사용(단일 소스).</summary>
        internal static Material SharedChevronMaterial() => EnsureChevronMaterial();

        private static Texture2D _chevronTex;

        /// <summary>[35-R1] 절차 체브런 텍스처 — 위(+V) 향한 두 꺾임 스트로크, 가장자리 AA(캐시 1회 — 결정론).</summary>
        private static Texture2D BuildChevronTexture(int size)
        {
            if (_chevronTex != null) return _chevronTex;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "CommandChevronTex" };
            var px = new Color[size * size];
            float stroke = size * 0.09f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float d1 = SegDist(u, v, 0.28f, 0.30f, 0.50f, 0.62f);
                    float d2 = SegDist(u, v, 0.50f, 0.62f, 0.72f, 0.30f);
                    float d = Mathf.Min(d1, d2);
                    float a = 1f - Mathf.Clamp01((d - stroke * 0.5f) / (stroke * 0.5f));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            _chevronTex = tex;
            return tex;
        }

        /// <summary>[35-R1] 선분 거리 유틸(체브런 스트로크 판정 — 결정론 기하).</summary>
        private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>[35-R1] 체브런 러너 — 스태거 팝 인(0.12s easeOut) → 유지 → 1s 후 0.3s 축소 소멸(72차 규약 동기).
        ///  알파 미사용 — 스케일만으로 등장/소멸(공유 머티리얼 유지).</summary>
        private const float ChevronLifeSeconds = 1f;

        sealed class ChevronRunner : MonoBehaviour
        {
            private const float PopSeconds = 0.12f;
            private const float FadeSeconds = 0.3f;
            private float _delay;
            private float _t;
            private Vector3 _full;

            public void Init(float delay)
            {
                _delay = delay;
                _t = 0f;
                _full = transform.localScale;
                transform.localScale = Vector3.zero;
            }

            private void Update()
            {
                _t += Time.deltaTime;
                float local = _t - _delay;
                if (local < 0f) { transform.localScale = Vector3.zero; return; }
                if (local < PopSeconds)
                {
                    float k = 1f - (1f - local / PopSeconds) * (1f - local / PopSeconds);   // easeOut 팝 인
                    transform.localScale = _full * k;
                }
                else if (local >= ChevronLifeSeconds)
                {
                    float fadeT = Mathf.Clamp01((local - ChevronLifeSeconds) / FadeSeconds);
                    transform.localScale = _full * (1f - 0.45f * fadeT);   // 마커 축소 페이드와 동일 계수
                    if (fadeT >= 1f) Destroy(gameObject);
                }
                else transform.localScale = _full;
            }
        }

        // [무번호 65-P1] TrySetupDecal **삭제** — 데칼 경로 폐기(Start 주석 근거 참조).
        //   구 구현(DecalProjector + 매뉴얼 카피 데칼 그래프)은 58-P2 프로브가 증명한 파손 경로로,
        //   머티리얼 기본값 color(0,0,0,0)+텍스처 대입 무시 → 불투명 흑 박스 렌더(테스트 12 영상 실측:
        //   전투 중 명령마다 ~0.6s 검은 사각, [51-SCREEN] 데칼 패스 사각으로 미보고). 쿼드 경로 단일 소스.

        /// <summary>[무번호 19-F3] 틴트 베이크 지면 링 텍스처 — SelectionRing 룩 평행 이식(밴드+글로우+다크 백킹+2중 링).
        ///   UV 거리 공간(d/반경): 밴드 코어 0.78±0.07, 글로우 외곽 0.04, 백킹 0.54~0.83, 2중 링 0.91 — CommandRingPresentation 상수 평행.</summary>
        public static Texture2D BuildTintedRingTexture(int size, Color tint)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;
            var px = new Color[size * size];
            var backing = CommandRingPresentation.RingBackingColor;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    var col = new Color(0f, 0f, 0f, 0f);
                    // 다크 백킹 — 밴드 기준 내측 패드 0.24/외측 패드 0.05(annulus 0.54~0.83)
                    if (r < CommandRingPresentation.ShaderRingRadius + CommandRingPresentation.RingBackingOuterPad &&
                        r > CommandRingPresentation.ShaderRingRadius - CommandRingPresentation.RingBackingInnerPad)
                        col = backing;
                    // 밴드 코어 — 가장자리 0.015 안티앨리어싱
                    float band = 1f - Mathf.Clamp01((Mathf.Abs(r - CommandRingPresentation.ShaderRingRadius) - 0.055f) / 0.015f);
                    // [무번호 20-I4] 회전 세그먼트 룩 — 6분할 각도 변조(SelectionRing _Segments=6 패리티, 정적 베이크)
                    if (band > 0f)
                    {
                        float ang01 = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;
                        float seg = Mathf.Repeat(ang01 * 6f, 1f);
                        float segMask = seg < 0.72f ? 1f : Mathf.Clamp01(1f - (seg - 0.72f) / 0.12f);
                        band *= 0.35f + 0.65f * segMask;
                        // [무번호 21-K3] 이중 톤 밴드 — 외곽 밝은 코어 에지 라인(0.835 부스트) + 내측 딥 톤 그라디언트
                        float edgeLine = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.835f) / 0.015f);
                        float innerDeep = Mathf.Clamp01((CommandRingPresentation.ShaderRingRadius - r) / 0.07f) * 0.25f;
                        band = Mathf.Clamp01(band * (1f - 0.22f * innerDeep) + edgeLine * 0.5f);
                        var bandCol = new Color(tint.r, tint.g, tint.b, band);
                        col = Color.Lerp(col, bandCol, band);
                    }
                    // [무번호 20-I4] 중앙 그라디언트 필 — C1 ShaderCenterFillAlpha(0.12) 패리티(중심 밝고 가장자리 소멸)
                    if (r < 0.5f)
                    {
                        float fillA = CommandRingPresentation.ShaderCenterFillAlpha * (1f - r / 0.5f);
                        var fillCol = new Color(tint.r, tint.g, tint.b, fillA);
                        col = Color.Lerp(col, fillCol, 0.5f);
                    }
                    // 글로우 — 밴드 외곽 0.04 완화(Fluent 절제 톤)
                    float glow = 1f - Mathf.Clamp01(Mathf.Abs(r - CommandRingPresentation.ShaderRingRadius) / (0.07f + CommandRingPresentation.ShaderGlowWidth));
                    if (glow > 0f && band <= 0f)
                        col = new Color(tint.r, tint.g, tint.b, Mathf.Max(col.a, 0.35f * glow));
                    // 2중 링 — 오프셋 0.13 얇은 링(알파 0.40)
                    float second = 1f - Mathf.Clamp01((Mathf.Abs(r - (CommandRingPresentation.ShaderRingRadius + CommandRingPresentation.ShaderSecondRingOffset)) - 0.01f) / 0.01f);
                    if (second > 0f)
                        col = new Color(tint.r, tint.g, tint.b, Mathf.Max(col.a, CommandRingPresentation.ShaderSecondRingAlpha * second));
                    px[y * size + x] = col;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>절차 환형 링 텍스처 — CommandRingPresentation 셰이더 폴백 경로에서도 재사용(public).</summary>
        public static Texture2D BuildRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;
            float rOuter = c * 0.9f, rInner = c * 0.68f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = 0f;
                    if (d <= rOuter && d >= rInner) a = 1f;
                    else if (d > rOuter && d < rOuter + 4f) a = 1f - (d - rOuter) / 4f;
                    else if (d < rInner && d > rInner - 4f) a = 1f - (rInner - d) / 4f;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}