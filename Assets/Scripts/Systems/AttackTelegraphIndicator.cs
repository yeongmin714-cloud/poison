using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [95차 F5→프리미엄] 적 공격 텔레그래프 — 발밑 경고 인디케이터 프리미엄화(사용자 지시: "적색링이 저품질처럼 보인다").
    /// - [텔레그래프P1] 64px 절차 링 텍스처(블러 원인) 폐기 → 커스텀 URP 셰이더(TelegraphRing.shader) 절차 렌더:
    ///   ①외곽 경계 링(날카로운 에지+밝은 에지 라인) ②방사형 채움 _Fill01(예고 진행 0→1 — 중심→외곽 위험영역 확장)
    ///   ③채움 선행 에지 하이라이트 ④에지 발광 펄스(블룸 흡수).
    /// - [텔레그래프P1-E] 순수 적색 → 경고 주황-적 톤(팔레트 조화).
    /// - [텔레그래프P1-D] 예고 종료 순간 화이트 플래시+1.15× 확산 소멸(0.12s) 후 자가 파괴 — 자체 포함(호출부 무수정).
    /// - 폴백: 셰이더 부재 시 기존 URP Unlit/Unlit-Color 균일 컬러 쿼드(기존 계약 유지 — 시각 저하, 로직 무영향).
    /// - 스폰 시그니처 불변(AnimalAI/AfterlifeEnemy 무수정), 상태 전부 인스턴스 필드, 결정론(Random 0건).
    /// - [무번호 16-B1] 채움형 부채꼴 게이지 링: 오버로드 Spawn(..., attackDirection) — 공격 방향을 중심으로 각도 방향 채움
    ///   (플레이어가 "언제 맞는지"를 읽게). 기존 시그니처는 원형 그대로(AnimalAI/AfterlifeEnemy 무수정).
    /// - [무번호 16-B2] 셰이더 효과: 에지 글로우·내부 스캔라인 흐름·시작 확산→수축 펄스 이징·임팩트 순간 플래시 링.
    /// - [무번호 16-B3] 위험도 색 계약: 일반 공격=적색 · 정예/강공격=주황(기존 주황-적 톤 승계).
    /// - [무번호 16-B4] 경고 SFX(저음 웅성) 동기 — 시각+청각 이중 예고(SFX_Telegraph_Warning, Application.isPlaying 가드).
    /// </summary>
    public class AttackTelegraphIndicator : MonoBehaviour
    {
        const string ShaderName = "ProjectName/TelegraphRing";
        const float ALPHA = 0.45f;                 // 붉은 반투명 기저 알파
        const float GROUND_LIFT = 0.06f;           // 지면 쿼드 래프팅 (BlobShadow 0.05 선례 준용)
        const float CONCLUDE_FLASH_SECONDS = 0.12f; // [텔레그래프P1-D] 종료 플래시 길이
        const float CONCLUDE_EXPAND = 1.15f;        // [텔레그래프P1-D] 종료 확산 배율
        const float START_PULSE_SECONDS = 0.2f;    // [무번호 16-B2] 시작 펄스 길이
        const float START_PULSE_EXPAND = 0.12f;    // [무번호 16-B2] 시작 펄스 확산 배율
        // [무번호 25-A3] 적색링 절반 축소 기본 계수 — 시각 링(쿼드)만 축소, 게임플레이 판정 사정거리(_attackRange)는 무변경
        internal const float RadiusVisualScaleDefault = 0.5f;

        // [무번호 16-B3] 위험도 색 계약 — 일반 공격=적색 · 정예/강공격=주황(기존 주황-적 톤 승계)
        // [텔레그래프P1-E] 순수 적색 폐기(환경 톤 조화) → 주황-적(1, 0.30, 0.16) — 정예/강공격 색 승계
        static readonly Color EliteColor = new Color(1f, 0.30f, 0.16f, ALPHA);
        // [무번호 16-B3] 일반 공격 = 적색(1, 0.15, 0.10) — 순수 적색(톤 조화는 알파로 유지)
        static readonly Color NormalColor = new Color(1f, 0.15f, 0.10f, ALPHA);

        static Material _sharedRingMat;            // 공유 셰이더 머티리얼 캐시(인스턴스 복제 사용)

        MeshRenderer _renderer;
        Material _instanceMat;                     // 인스턴스 머티리얼(셰이더 경로) 또는 폴백 균일 컬러
        bool _shaderPath;                          // 셰이더 절차 렌더 사용 여부(폴백 분기)
        float _duration;
        float _elapsed;
        bool _concluding;                          // 종료 플래시 페이즈 진입 여부
        float _baseScale;
        Color _ringColor;                          // [무번호 16-B3] 위험도 색 계약 — 인스턴스별 색
        bool _useWedge;                            // [무번호 16-B1] 부채꼴 채움 사용 여부(오버로드)
        float _attackAngle;                        // [무번호 16-B1] 공격 방향 각도
        float _startPulse;                         // [무번호 16-B2] 시작 펄스 진행(0→1)
        float _startPulseElapsed;                  // [무번호 16-B2] 시작 펄스 경과
        bool _convergeSpawned;                     // [무번호 21-L1] 수렴 링 1회 발화 가드
        bool _concludeFlashSpawned;                // [무번호 22-S1] 응축 플래시 1회 가드
        Transform _convergeLayer;                  // [무번호 37-M1] 예고 수렴 체브런 레이어(추가 계층 — 기존 링 무수정)

        /// <summary>
        /// 월드 스페이스 발밑 링 스폰 — duration 경과 후 종료 플래시(0.12s) 후 자가 파괴.
        /// [무번호 16-B3] 위험도 색 계약 — 기존 시그니처는 일반 공격(적색).
        /// </summary>
        public static GameObject Spawn(Vector3 groundPosition, float radius, float duration)
        {
            return Spawn(groundPosition, radius, duration, false);
        }

        /// <summary>
        /// [무번호 16-B3] 위험도 색 계약 오버로드 — elite=true면 정예/강공격(주황), false면 일반 공격(적색).
        /// </summary>
        public static GameObject Spawn(Vector3 groundPosition, float radius, float duration, bool elite)
        {
            return Spawn(groundPosition, radius, duration, elite, false, Vector3.forward);
        }

        /// <summary>
        /// [무번호 19-E1] 발견(!) 시각 시그널 — 몬스터 Alert 진입 시 발밑 적색 ! 링.
        /// Alert 지속시간(3s) 동안 _Fill01 0→1 진행 = Alert→Combat 전이 게이지.
        /// 기존 Spawn 경로(공격 텔레그래프)와 무관 — 부채꼴 미사용, 전이 게이지 전용.
        /// </summary>
        public static GameObject SpawnAlert(Vector3 groundPosition, float radius, float duration)
        {
            return Spawn(groundPosition, radius, duration, false, false, Vector3.forward);
        }

        /// <summary>
        /// [무번호 16-B1] 채움형 부채꼴 게이지 링 오버로드 — attackDirection이 있으면 부채꼴 채움(공격 방향 각도).
        /// [무번호 25-A3] 시그니처 불변(CombatFxMonsterAlertPhaseETests 리플렉션 잠금) — 본문은 신규 7인자
        ///   오버로드로 위임하며 기본 계수 radiusVisualScale 0.5(적색링 절반 축소)를 Spawn 체인 경유로 전달한다.
        /// </summary>
        public static GameObject Spawn(Vector3 groundPosition, float radius, float duration, bool elite, bool useWedge, Vector3 attackDirection)
        {
            return Spawn(groundPosition, radius, duration, elite, useWedge, attackDirection, RadiusVisualScaleDefault);
        }

        /// <summary>
        /// [무번호 25-A3] 적색링 절반 축소 오버로드 — 쿼드 localScale = radius*2*radiusVisualScale(기본 0.5).
        ///   호출부(AnimalAI/AfterlifeEnemy) 3인자 시그니처는 무수정(CombatFxTelegraphPhaseBTests.T6 계약 잠금) —
        ///   Spawn 체인(3인자 오버로드→전체 오버로드 기본 계수) 경유로 0.5가 적용된다. 판정 사정거리 무변경.
        /// </summary>
        public static GameObject Spawn(Vector3 groundPosition, float radius, float duration, bool elite, bool useWedge, Vector3 attackDirection, float radiusVisualScale = 0.5f)
        {
            if (radius <= 0f || duration <= 0f) return null;
            try
            {
                var go = new GameObject("AttackTelegraphIndicator");
                go.transform.position = groundPosition + Vector3.up * GROUND_LIFT;
                go.transform.rotation = Quaternion.identity;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); // 기본 세로(법선 +Z)
                quad.name = "AttackTelegraph_RingQuad";
                Object.Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // [무번호 25-A3] 시각 링 절반 축소 — 쿼드 localScale = radius*2*radiusVisualScale(기본 0.5)
                float quadSize = radius * 2f * radiusVisualScale;
                quad.transform.localScale = new Vector3(quadSize, quadSize, 1f);

                var indicator = go.AddComponent<AttackTelegraphIndicator>();
                indicator._duration = duration;
                indicator._ringColor = elite ? EliteColor : NormalColor;
                indicator._useWedge = useWedge;
                indicator._attackAngle = Mathf.Atan2(attackDirection.x, attackDirection.z) + Mathf.PI;

                // 1순위: [텔레그래프P1] 절차 셰이더 링(텍스처 불요 — 전부 셰이더 수학)
                var shader = Shader.Find(ShaderName);
                if (shader != null)
                {
                    if (_sharedRingMat == null)
                    {
                        _sharedRingMat = new Material(shader) { name = "AttackTelegraph_ShaderMat" };
                        _sharedRingMat.hideFlags = HideFlags.HideAndDontSave;
                    }
                    indicator._instanceMat = new Material(_sharedRingMat);
                    indicator._instanceMat.name = "AttackTelegraph_Mat_Inst";
                    indicator._instanceMat.SetColor("_BaseColor", indicator._ringColor);
                    indicator._instanceMat.SetFloat("_Fill01", 0f);
                    indicator._instanceMat.SetFloat("_EdgePulse", 0.5f);
                    indicator._instanceMat.SetFloat("_Wedge01", 0f);
                    indicator._instanceMat.SetFloat("_AttackAngle", indicator._attackAngle);
                    indicator._instanceMat.SetFloat("_Scan", 0f);
                    indicator._instanceMat.SetFloat("_StartPulse", 0f);
                    indicator._instanceMat.SetFloat("_Flash", 0f);
                    indicator._renderer.sharedMaterial = indicator._instanceMat;
                    indicator._shaderPath = true;
                }
                else
                {
                    var uniform = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    if (uniform == null) uniform = new Material(Shader.Find("Unlit/Color"));
                    uniform.name = "AttackTelegraph_Uniform";
                    uniform.color = indicator._ringColor;
                    indicator._renderer.sharedMaterial = uniform;
                }

                // [무번호 16-B4] 경고 SFX(저음 웅성) 동기 — 시각+청각 이중 예고
                if (Application.isPlaying)
                {
                    var sfx = SoundEffectManager.Instance;
                    if (sfx != null) sfx.PlaySFXAtPoint(SoundEffectManager.SFXType.Telegraph_Warning, groundPosition);
                }
                return go;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AttackTelegraphIndicator] 스폰 실패(시각 보조 무영향): {e.Message}");
                return null;
            }
        }

        /// <summary>[무번호 20-H3] 발동 순간 공격 방향 부채꼴 플래시 — ConcludeMeleeTelegraph에서 실제 스윙 방향 전달.
        ///   _Wedge01 풀 채움+공격 각도+플래시 동시 반영(기존 자체 종료 플래시와 병행 — 교체 아님).</summary>
        public void FlashConclude(Vector3 attackDirection)
        {
            if (_instanceMat == null || !_shaderPath) return;
            float ang = Mathf.Atan2(attackDirection.x, attackDirection.z) + Mathf.PI;
            if (_instanceMat.HasProperty("_AttackAngle")) _instanceMat.SetFloat("_AttackAngle", ang);
            if (_instanceMat.HasProperty("_Wedge01")) _instanceMat.SetFloat("_Wedge01", 1f);
            if (_instanceMat.HasProperty("_Flash")) _instanceMat.SetFloat("_Flash", 1f);
        }

        /// <summary>[무번호 37-M1] 예고 수렴 체브런 — 링 위 3본 체브런이 중심을 향해 회전·수렴(남은 시간 비례).
        /// 기존 링 쿼드/셰이더 계약 무수정 — 자식 레이어(수렴 시작 후 0.15s 남은 시점 제거 — 종료 플래시와 겹침 방지).
        /// 절차 체브런 텍스처는 CommandMarker 공유(37-S3 선례 — 단일 소스). 결정론(Random 0건).</summary>
        void UpdateConvergeChevrons()
        {
            if (_convergeLayer == null)
            {
                var mat = CommandMarker.SharedChevronMaterial();
                if (mat == null) return;   // 폴백 — 텍스처/머티리얼 부재 시 레이어 미생성(기존 링만)
                var layerGo = new GameObject("TelegraphConvergeChevrons");
                layerGo.transform.SetParent(transform, false);
                layerGo.transform.localPosition = Vector3.zero;
                float qs = _baseScale * 0.18f;
                for (int i = 0; i < 3; i++)
                {
                    float ang = i * 120f;
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(q.GetComponent<Collider>());
                    q.name = "ConvergeChevron";
                    q.transform.SetParent(layerGo.transform, false);
                    q.transform.localPosition = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0f, Mathf.Cos(ang * Mathf.Deg2Rad)) * (_baseScale * 0.38f);
                    q.transform.localRotation = Quaternion.Euler(90f, 180f + ang, 0f);   // 중심 향해(안쪽)
                    q.transform.localScale = new Vector3(qs, qs * 0.6f, 1f);
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                _convergeLayer = layerGo.transform;
            }
            float remain = Mathf.Max(0f, _duration - _elapsed);
            if (remain <= 0.15f)
            {
                Object.Destroy(_convergeLayer.gameObject);   // 종료 플래시(0.12s)와 겹침 방지 — 레이어 먼저 정리
                _convergeLayer = null;
                return;
            }
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.001f, _duration));
            // 수렴: 반경 0.38→0.22 (남은 시간 비례), 회전: 임박할수록 가속(3회전/초 → 6회전/초)
            float radius = Mathf.Lerp(_baseScale * 0.38f, _baseScale * 0.22f, t);
            _convergeLayer.localScale = Vector3.one * Mathf.Lerp(1f, 0.58f, t);
            _convergeLayer.localPosition = Vector3.zero;
            _convergeLayer.Rotate(0f, -(120f + 240f * t) * Time.deltaTime, 0f, Space.Self);
            for (int i = 0; i < _convergeLayer.childCount; i++)
            {
                var ch = _convergeLayer.GetChild(i);
                float baseAng = i * 120f;
                ch.localPosition = new Vector3(Mathf.Sin(baseAng * Mathf.Deg2Rad), 0f, Mathf.Cos(baseAng * Mathf.Deg2Rad)) * radius;
                ch.localRotation = Quaternion.Euler(90f, 180f + baseAng, 0f);
            }
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            UpdateConvergeChevrons();   // [무번호 37-M1] 수렴 눈금(남은 시간 비례 회전·수렴 — "언제 맞는지" 가독)

            // [무번호 16-B2] 시작 펄스 이징 — 스폰 시점 링 확산→수축
            if (_startPulseElapsed < START_PULSE_SECONDS)
            {
                _startPulseElapsed += Time.deltaTime;
                _startPulse = Mathf.Clamp01(_startPulseElapsed / START_PULSE_SECONDS);
                if (_startPulseElapsed >= START_PULSE_SECONDS) _startPulse = 1f;
            }

            if (_concluding)
            {
                // [텔레그래프P1-D] 종료 플래시 — 화이트 밝기 상승+1.15× 확산+알파 소멸
                float fade = 1f - Mathf.Clamp01(((_elapsed - _duration) / CONCLUDE_FLASH_SECONDS));
                _baseScale *= 1f + (CONCLUDE_EXPAND - 1f) * Mathf.Clamp01(Time.deltaTime * 12f);
                if (_renderer != null) _renderer.transform.localScale = new Vector3(_baseScale, _baseScale, 1f);
                if (_shaderPath && _instanceMat != null)
                {
                    Color c = Color.Lerp(_ringColor, new Color(1f, 0.95f, 0.9f, 1f), 0.8f);
                    c.a = _ringColor.a * fade;
                    if (_instanceMat.HasProperty("_BaseColor")) _instanceMat.SetColor("_BaseColor", c);
                    if (_instanceMat.HasProperty("_Fill01")) _instanceMat.SetFloat("_Fill01", 1f);
                    if (_instanceMat.HasProperty("_Flash")) _instanceMat.SetFloat("_Flash", 1f);
                }
                else if (_instanceMat != null)
                {
                    Color c = Color.Lerp(_ringColor, new Color(1f, 0.95f, 0.9f, 1f), 0.8f);
                    c.a = _ringColor.a * fade;
                    if (_instanceMat.HasProperty("_BaseColor")) _instanceMat.SetColor("_BaseColor", c);
                    if (_instanceMat.HasProperty("_Color")) _instanceMat.SetColor("_Color", c);
                }
                if (fade <= 0.01f) Destroy(gameObject); // 플래시 소멸 후 자가 파괴
                return;
            }

            if (_elapsed >= _duration)
            {
                _concluding = true;
                // [무번호 22-S1] 응축 플래시 — 임팩트 직전 중심 소형 플래시 링 1회(에너지 응축 완료 클라이맥스)
                if (!_concludeFlashSpawned)
                {
                    _concludeFlashSpawned = true;
                    try
                    {
                        Color fc = _ringColor;
                        fc.a = Mathf.Min(1f, fc.a + 0.35f);
                        ShockwaveRingFX.Spawn(transform.position, 0.25f, fc, 0.15f);
                    }
                    catch (System.Exception) { }
                }
                return;
            }

            if (_shaderPath && _instanceMat != null)
            {
                // [텔레그래프P1] 채움 진행(0→1) + 에지 발광 펄스(예고 0.5s 기준 2회 맥동 — 기존 리듬 유지)
                float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, _duration));
                float pulse = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 4f);
                _instanceMat.SetFloat("_Fill01", t);
                _instanceMat.SetFloat("_EdgePulse", pulse);
                // [무번호 16-B1] 부채꼴 채움 진행(0→1) — 공격 방향을 중심으로 각도 방향 채움
                if (_useWedge) _instanceMat.SetFloat("_Wedge01", t);
                // [무번호 16-B2] 스캔라인 흐름 — 예고 진행에 따라 흐름
                _instanceMat.SetFloat("_Scan", t);
                // [무번호 21-L1] 수렴 링 — 채움 75% 시점 1회 자체 발화(외곽→중심 수축 — "공격이 모이는" 연출).
                //   수축 시간 = 남은 windup — 임팩트 순간 중심 수렴 완료 동기. 색 = 링색 밝은 변조.
                if (!_convergeSpawned && t >= 0.75f)
                {
                    _convergeSpawned = true;
                    try
                    {
                        float convRadius = _baseScale * 0.5f;   // 쿼드 스케일(지름) → 반경
                        Color cc = _ringColor; cc.a = Mathf.Min(1f, cc.a + 0.25f);
                        ShockwaveRingFX.SpawnConvergingRing(transform.position, convRadius, cc, Mathf.Max(0.25f, _duration - _elapsed));
                    }
                    catch (System.Exception) { }
                }
                // [무번호 16-B2] 시작 펄스 이징 — 스폰 시점 링 확산→수축
                _instanceMat.SetFloat("_StartPulse", _startPulse);
                // [무번호 16-B2] 시작 펄스 확산 배율 — _baseScale *= 1 + 0.12 * sin(π * pulse)
                float pulseScale = 1f + START_PULSE_EXPAND * Mathf.Sin(Mathf.PI * (1f - _startPulse));
                if (_renderer != null) _renderer.transform.localScale = new Vector3(_baseScale * pulseScale, _baseScale * pulseScale, 1f);
                // [텔레그래프P1-C] 알파 기저도 펄스에 미세 연동(기존 알파 리듬 계승 — 에지 선명 유지)
                Color c = _ringColor;
                c.a = Mathf.Lerp(0.55f, 1.0f, pulse) * ALPHA / 0.45f * 0.8f + ALPHA * 0.2f;
                if (_instanceMat.HasProperty("_BaseColor")) _instanceMat.SetColor("_BaseColor", c);
            }
        }

        void Awake()
        {
            _renderer = GetComponentInChildren<MeshRenderer>();
            _baseScale = _renderer != null ? _renderer.transform.localScale.x : 1f;
        }
    }
}
