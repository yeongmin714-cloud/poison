using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [169-P6] 공격 범위 호(arc) 텔레그래프 — 몬스터/병사 근접 공격 예고(95차 F5 2페이즈 유지).
    /// 사용자 확정(11-21): 적색 유지(AttackTelegraphIndicator Elite/Normal 2색 패밀리 승계), 원형 링 → 호(부채꼴),
    /// 표시 반경 = 실제 판정 반경(25-A3 링 0.5 축소 배율 미적용 — "표시=판정" 정합이 본 개편의 목적).
    /// 느낌표(AttackTelegraphIndicator.SpawnAlert)는 무수정 — 공격 링만 호로 대체.
    /// 방향은 예고 시작 시점 타겟 방향으로 고정 — 플레이어는 windup 0.5s 내 호 밖 이동(빈 스윙) 또는
    /// 타격 순간 구르기(PlayerHealth C21-02 롤 무적)로 회피한다.
    /// [173차 F1~F4] 시각 고급화(플루언트) — 판정 계약 무변경: ①호 따라 흐르는 광택 스윕(정현파, 결정론)
    /// ②이중 레이어(내부 소프트 글로우 + 외곽 림, 정점알파 5링) ③펄스 완화(사인 smoothstep, 0.88~1.0)
    /// ④가장자리 완화(측면 감쇠 심화 0.08 잔여 — 하드 컷 없음). 반경 1:1·색 패밀리·소멸 타이밍 계약 존속.
    /// </summary>
    public class TelegraphArcIndicator : MonoBehaviour
    {
        /// <summary>전방 중심각 ±60° — 총 120° 호 (Play 판정 후 트림 대상).</summary>
        public const float HalfAngleDeg = 60f;
        /// <summary>예고 시간 — 몬스터 TelegraphWindupSeconds(0.5s)와 정합. 병사 2페이즈도 동일 값 사용.</summary>
        public const float TelegraphSeconds = 0.5f;

        // [169-P6] 적색 유지(사용자 확정) — AttackTelegraphIndicator 위험도 2색 패밀리 동일 값 승계.
        private static readonly Color EliteColor = new Color(1f, 0.30f, 0.16f, 0.45f);
        private static readonly Color NormalColor = new Color(1f, 0.15f, 0.10f, 0.45f);

        private const float FadeSeconds = 0.15f;
        // [172차 T4] 만료 확산+페이드 창 — 0.3s 부드럽게(기존 0.12s 하드 컷 폐기). FlashConclude(조기 타격) 페이드는 FadeSeconds 유지.
        private const float ExpirySeconds = 0.3f;
        // [173차 F1] 광택 스윕 상수 — 정현파 진폭 ±52°(측면 감쇠 구간 안), 각속도 1.7rad/s(주기 ~3.7s, 결정론).
        private const float SweepAmplitudeDeg = 52f;
        private const float SweepOmega = 1.7f;

        // ═══ [무번호 181] 원형(360°) 텔레그래프 — 폭탄 착지 피격 범위 표시(additive — 기존 부채꼴 Spawn/BuildFanMesh/상수 무수정) ═══
        /// <summary>[207차 P4-1] followRoot 추적 모드 y 최종 폴백 값 — 지면 쿼리(레이캐스트/GetHeightAt) 실패 시에만 사용
        /// (구 0.02f 지면 고정 하드코딩 폐기 — MainScene 지형 y≈42에서 원형이 지하에 렌더되던 근원).</summary>
        private const float CircleGroundY = 0.02f;
        // [무번호 181] 추적 모드 — null이면 고정 원형(스폰 위치 유지), !=null이면 Update에서 x/z 추적(폭탄은 공중에서도 이동).
        private Transform _followRoot;

        private Material _mat;
        private float _baseAlpha;
        private float _age;
        private bool _fading;

        // [171차 D1] 수축 임팩트 마커 — "임팩트 예정 지점" 학습 힌트(적 측 경고 계약 유지 — 패막 성공 색(흰)과 무관).
        private Transform _ring;
        private Material _ringMat;
        private Mesh _ringMesh;
        private float _ringStartRadius;
        private float _ringDuration;
        private float _impactAge;
        private bool _impactPlayed;

        // [173차 F1] 광택 스윕 밴드 — 호를 따라 흐르는 밝은 밴드(정현파 스윕 — 시간 기반 위상, 결정론: Random 0건 계약).
        // 자식 메시는 Spawn 시 1회 생성(정적 메시 캐시 구조 유지 — 프레임당 재생성 없음), 구동은 회전+알파만.
        private Transform _band;
        private Material _bandMat;
        private Mesh _bandMesh;

        /// <summary>
        /// 호 텔레그래프 스폰. direction = 예고 시작 시점 타겟 방향(수평) — 이후 고정(회피 가능 설계).
        /// </summary>
        public static GameObject Spawn(Vector3 pos, float radius, float duration, Vector3 direction, bool isElite = false)
        {
            var go = new GameObject("TelegraphArc");
            var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            go.transform.SetPositionAndRotation(
                new Vector3(pos.x, pos.y + 0.02f, pos.z),
                Quaternion.LookRotation(dir, Vector3.up));

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildFanMesh(Mathf.Max(0.4f, radius));

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            var color = isElite ? EliteColor : NormalColor;
            mat.color = color;
            mat.name = "TelegraphArcMat";
            mr.sharedMaterial = mat;

            var ind = go.AddComponent<TelegraphArcIndicator>();
            ind._mat = mat;
            ind._baseAlpha = color.a;
            ind.AttachImpactRing(color, Mathf.Max(0.4f, radius), Mathf.Max(0.1f, duration));   // [171차 D1] 수축 링 자식
            ind.AttachSweepBand(color, Mathf.Max(0.4f, radius));   // [173차 F1] 광택 스윕 밴드 자식(색 패밀리·반경 1:1 승계)
            // [172차 T4] 소멸 예약 — 만료 확산+페이드 창(0.3s) 커버로 연장(기존 duration+Fade+0.05는 0.12s 확산 전제)
            Object.Destroy(go, Mathf.Max(0.1f, duration) + ExpirySeconds + 0.05f);
            return go;
        }

        /// <summary>
        /// [무번호 181] 원형(360°) 텔레그래프 스폰 — 폭탄 착지 피격 범위 표시(사용자 요구 "떨어질 때 피격 범위 표시").
        /// pos는 지면 투영 좌표로 가정([207차 P4-1] Bomb.cs가 QueryGroundY로 산출 — 구 new Vector3(x, 0.02f, z) 하드코딩 폐기).
        /// 기존 Spawn(부채꼴 호)은 무수정 — 원형은 병행 신설.
        /// 시각 언어 완전 승계: EliteColor/NormalColor 동일 패밀리·Pulse 펄스·Fade/만료 타이밍 계약(_ringDuration = duration)·
        /// AttachSweepBand 스윕 밴드(원 주위 회전 하이라이트)·OnDestroy 런타임 자원 해제 정리 계약. 반경은 표시=판정 1:1 유지.
        /// followRoot != null이면 지표가 followRoot.position의 x/z를 따라가고 y는 Update에서 매 프레임 지면 쿼리로 접지
        /// (추적 모드 — 폭탄 낙하 대응, [207차 P4-1]).
        /// 임팩트 수축 링은 원형 모드 미부착(_ring null 가드로 스킵 — 발밑 원형 표시 유실 없음), 소멸 예약 계약 동일 적용.
        /// </summary>
        public static GameObject SpawnCircle(Vector3 pos, float radius, float duration, Transform followRoot = null, bool isElite = false)
        {
            var go = new GameObject("TelegraphCircle");
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildCircleMesh(Mathf.Max(0.4f, radius));

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            var color = isElite ? EliteColor : NormalColor;
            mat.color = color;
            mat.name = "TelegraphCircleMat";
            mr.sharedMaterial = mat;

            var ind = go.AddComponent<TelegraphArcIndicator>();
            ind._mat = mat;
            ind._baseAlpha = color.a;
            // 페이드/만료 타이밍 계약 — 수축 링 미부착 대신 duration 직접 설정(Update의 t/charge/만료 분기가 동일 동작)
            ind._ringDuration = Mathf.Max(0.1f, duration);
            ind._followRoot = followRoot;
            ind.AttachSweepBand(color, Mathf.Max(0.4f, radius));   // [173차 F1] 스윕 밴드 승계 — 원 주위 회전 하이라이트
            // 기존 정리 계약 승계 — 만료 확산+페이드 창(0.3s) 커버로 연장 예약
            Object.Destroy(go, Mathf.Max(0.1f, duration) + ExpirySeconds + 0.05f);
            return go;
        }

        /// <summary>
        /// [207차 P4-1] 착지 텔레그래프 지면 y 쿼리 — 구 y=0.02f 하드코딩 폐기(MainScene 지형 y≈42에서 원형이
        /// 지하 42m 렌더되어 보이지 않던 근원 수리). 3단 폴백:
        /// ①Physics.Raycast 하향(origin에서 -Y, 거리 넉넉히, Default 마스크, 트리거 제외) → 히트 지점 +0.02f
        /// (동평면 z-fight 여유 — 기존 idiom). ②실패 시 TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42)
        /// + 1f(PlayerMovement.ClampToGroundByHeight·VillageBuilder 동일 표면 수식 — 단일 소스) + 0.02f.
        /// ③그마저 실패하면 fallbackY(기존 0.02) 폴백. 예외 재Throw 없이 폴백 — 연출 전용, 로직 무영향 계약.
        /// </summary>
        public static float QueryGroundY(Vector3 origin, float fallbackY)
        {
            const float GroundProbeDistance = 300f;   // 낙하 origin에서 지면까지 넉넉한 탐색 거리
            try
            {
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit,
                        GroundProbeDistance, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                    return groundHit.point.y + 0.02f;   // 지면 위 동평면 여유(기존 0.02 idiom 승계)

                // 레이캐스트 미스 — 수식 지형 표면 단일 소스(1 + GetHeightAt — ClampToGroundByHeight 동일 기준)
                return 1f + TerrainGenerator.GetHeightAt(origin.x, origin.z,
                    ProjectName.Core.Data.BiomeType.Plains, 42) + 0.02f;
            }
            catch (System.Exception)
            {
                return fallbackY;   // ③ 기존 0.02 폴백 — 예외 재Throw 없이 스킵(연출 스킵 계약)
            }
        }

        /// <summary>예고 종료(타격 순간) — 빠른 페이드아웃(몬스터 FlashConclude 선례 계약 유지).</summary>
        public void FlashConclude(Vector3 direction)
        {
            _fading = true;
        }

        /// <summary>[172차 T3] ease-out 쿼드 — 수축/확산 커브 공용(선형 금지 계약).</summary>
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>[173차 F3] 완화 펄스 — 사인을 smoothstep으로 완화(극값 체류 길게 → 기계적 깜빡임 제거),
        /// 진폭 0.88~1.0. 지표·임팩트 링·스윕 밴드 공용 배수(결정론 — Random 0건 계약, 동기 유지).</summary>
        private static float Pulse(float age)
        {
            float s = 0.5f + 0.5f * Mathf.Sin(age * 8f);
            return 0.88f + 0.12f * (s * s * (3f - 2f * s));
        }

        private void Update()
        {
            _age += Time.deltaTime;   // [171차 D1] 마커가 동일 시계 공유 — 증분 지점 단일화(동작 동일)

            // [무번호 181][207차 P4-1] followRoot 추적 모드 — 폭탄 x/z를 따라가고 y는 매 프레임 지면 쿼리로 접지
            // (구 CircleGroundY 0.02f 지면 고정 폐기 — 폭탄이 이동해도 원형이 항상 지면에 붙음).
            // 고정 원형(followRoot null)은 무동작 — 기존 고정 호출처(병사 텔레그래프 등) y 처리 무변경.
            if (_followRoot != null)
                transform.position = new Vector3(_followRoot.position.x,
                    QueryGroundY(_followRoot.position, CircleGroundY), _followRoot.position.z);

            float t = Mathf.Clamp01(_age / Mathf.Max(0.001f, _ringDuration));
            // [172차 T2] 축적 글로우 — 예고 진행에 따라 밝기 상승(임박 강조). easeIn 가속(t²) — 색 계약 유지(알파만).
            float charge = 1f + 0.35f * (t * t);
            UpdateImpactRing(t, charge);

            if (_mat == null) return;
            // [172차 T2] 만료 직전 미세 수축 — 마지막 12% 구간 ease-out으로 0.94배(호가 조여지는 임박 신호).
            //   지표 GO 스케일만(자식 링 localScale은 자체 로직 — 부모 스케일이 함께 적용됨). 결정론.
            float shrink = 1f;
            if (!_fading && t > 0.88f)
                shrink = Mathf.Lerp(1f, 0.94f, EaseOutQuad((t - 0.88f) / 0.12f));
            transform.localScale = Vector3.one * shrink;

            float a;
            if (_fading)
            {
                a = Mathf.Max(0f, _baseAlpha * (1f - _age / FadeSeconds));
                if (a <= 0.001f) { Destroy(gameObject); return; }
            }
            else if (_age >= _ringDuration)
            {
                // [172차 T4] 만료 — 임팩트 링 확산과 동기(0.3s)로 지표 페이드(하드 컷 폐기)
                a = _baseAlpha * charge * Mathf.Max(0f, 1f - (_age - _ringDuration) / ExpirySeconds);
                if (a <= 0.001f) { Destroy(gameObject); return; }
            }
            else
            {
                // [173차 F3] 결정론 펄스(Random 0건 계약) — 완화 사인(극값 체류 길게) 0.88~1.0 + 축적 글로우
                a = _baseAlpha * Pulse(_age) * charge;
            }
            var c = _mat.color; c.a = a; _mat.color = c;
            UpdateSweepBand(a);   // [173차 F1] 스윕 밴드 — 알파는 지표와 동기(페이드 타이밍 계약 동일)
        }

        /// <summary>[171차 D1] 수축 임팩트 마커 부착 — 지표 GO의 자식 평면 링. 색/머티리얼 idiom 그대로
        /// (EliteColor/NormalColor 승계, isElite 분기 값 준용 — 신규 색 신설 없음). Spawn 시그니처/반환 무변경.</summary>
        private void AttachImpactRing(Color color, float radius, float duration)
        {
            var ringGo = new GameObject("TelegraphArcImpactRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = Vector3.zero;

            var mf = ringGo.AddComponent<MeshFilter>();
            _ringMesh = BuildRingMesh();
            mf.sharedMesh = _ringMesh;

            var mr = ringGo.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default");
            _ringMat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            _ringMat.color = color;
            _ringMat.name = "TelegraphArcRingMat";
            mr.sharedMaterial = _ringMat;

            _ring = ringGo.transform;
            _ringStartRadius = radius * 0.9f;   // 시작 반경 = radius×0.9 → 0.15로 duration 동안 수축
            _ringDuration = duration;
        }

        /// <summary>[171차 D1] 마커 구동 — 수축(지표와 동일 Time.deltaTime idiom) 후 duration 만료 시점에
        /// 확산 링 발화. [172차 T3/T4] 수축·확산 전부 ease-out 커브, 확산+페이드 0.3s 부드럽게(기존 0.12s 폐기).
        /// 조기 타격(FlashConclude) 시 지표 페이드에 동기. 부모 파괴/페이드 경로에 자식 동반 파괴,
        /// 머티리얼/메시는 OnDestroy 해제(누출 금지).</summary>
        private void UpdateImpactRing(float progressT, float charge)
        {
            if (_ring == null || _ringMat == null) return;

            if (!_impactPlayed && _age >= _ringDuration)
            {
                _impactPlayed = true;   // duration 만료 — 확산 링 1회 발화
                _impactAge = 0f;
            }

            float a;
            if (_impactPlayed)
            {
                // [172차 T4] 확산 0.3s + ease-out(기존 0.12s 선형 폐기 — 부드러운 임팩트 확산)
                _impactAge += Time.deltaTime;
                float it = Mathf.Clamp01(_impactAge / ExpirySeconds);
                float r = Mathf.Lerp(0.15f, _ringStartRadius * 1.15f, EaseOutQuad(it));
                _ring.localScale = new Vector3(r, 1f, r);
                a = _baseAlpha * (1f - it);   // 확산과 동시 소멸(0.3s — 동일 창 동기)
                if (it >= 1f)
                {
                    Destroy(_ring.gameObject);
                    _ring = null;
                    Destroy(_ringMat);
                    _ringMat = null;
                    return;
                }
            }
            else
            {
                // [172차 T3] 수축 ease-out(선형 금지) + 축적 글로우 동기
                float r = Mathf.Lerp(_ringStartRadius, 0.15f, EaseOutQuad(progressT));
                _ring.localScale = new Vector3(r, 1f, r);
                a = _fading
                    ? Mathf.Max(0f, _baseAlpha * (1f - _age / FadeSeconds))
                    : _baseAlpha * Pulse(_age) * charge;   // 지표 펄스·축적 동기([173차 F3] 공용 Pulse)
            }

            var rc = _ringMat.color; rc.a = a; _ringMat.color = rc;
        }

        /// <summary>[171차 D1] 정리 계약 — 마커 런타임 자원(머티리얼/메시) 해제. 링 GO는 기존 파괴 경로
        /// (Update 페이드 Destroy / Object.Destroy 예약)에서 부모와 함께 소멸 — 별도 경로 불필요.</summary>
        private void OnDestroy()
        {
            if (_ringMat != null) Destroy(_ringMat);
            if (_ringMesh != null) Destroy(_ringMesh);
            if (_bandMat != null) Destroy(_bandMat);   // [173차 F1] 스윕 밴드 런타임 자원 해제(누출 금지)
            if (_bandMesh != null) Destroy(_bandMesh);
        }

        /// <summary>[173차 F1] 광택 스윕 밴드 부착 — 지표 GO의 자식 얇은 부채꼴 스트라이프(호를 따라 흐르는 밝은 밴드).
        /// 색/머티리얼 idiom 그대로(EliteColor/NormalColor 승계 — 신규 색 신설 없음), 반경은 지표와 동일 값(표시=판정 1:1).
        /// 메시는 Spawn 시 1회 생성(캐시 구조 유지), 구동은 localRotation+알파만(프레임당 메시 재생성 없음).
        /// 자식 y +0.005 — 동평면 투명 정렬에서 지표 위 렌더 보장.</summary>
        private void AttachSweepBand(Color color, float radius)
        {
            var bandGo = new GameObject("TelegraphArcSweepBand");
            bandGo.transform.SetParent(transform, false);
            bandGo.transform.localPosition = new Vector3(0f, 0.005f, 0f);

            var mf = bandGo.AddComponent<MeshFilter>();
            _bandMesh = BuildBandMesh(radius);
            mf.sharedMesh = _bandMesh;
            var mr = bandGo.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default");
            _bandMat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            _bandMat.color = color;
            _bandMat.name = "TelegraphArcBandMat";
            mr.sharedMaterial = _bandMat;
            _band = bandGo.transform;
        }

        /// <summary>[173차 F1] 스윕 밴드 구동 — 정현파 각도 스윕(주기 ~3.7s, 결정론) + 지표 알파 동기
        /// (페이드/만료 창 동일 적용 — FlashConclude·소멸 타이밍 계약 준수).</summary>
        private void UpdateSweepBand(float alphaSync)
        {
            if (_band == null || _bandMat == null) return;
            float angle = SweepAmplitudeDeg * Mathf.Sin(_age * SweepOmega);
            _band.localRotation = Quaternion.Euler(0f, angle, 0f);
            var c = _bandMat.color; c.a = alphaSync; _bandMat.color = c;
        }

        /// <summary>[173차 F1] 스윕 밴드 메시 — 얇은 부채꼴 스트라이프(반경 4링 × 각 3열 그리드, 정점알파 프로파일).
        /// 각 단면은 중심 열만 밝은 돔 프로파일(가장자리 0 — 하드 컷 없음), 반경 프로파일은 양끝 페이드
        /// (0.15에서 0으로 올라 0.85에서 피크, 1.0 경계로 소프트 페이드 — 지표 외곽 프로파일 정합).
        /// Sprites/Default 정점색 승수 경로 — 지표 팬과 동일 idiom.</summary>
        private static Mesh BuildBandMesh(float radius)
        {
            var mesh = new Mesh { name = "TelegraphArcSweepBand" };
            // 반경 링 — 비율(0.15 언더페이드 → 0.85 피크 → 1.0 아웃페이드)·알파 계수
            float[] ringR = { 0.15f, 0.5f, 0.85f, 1f };
            float[] ringA = { 0f, 0.9f, 1f, 0.3f };
            const int Cols = 3;              // -w / 0 / +w — 중심 열만 풀 알파(십자 돔 프로파일)
            const float HalfWidthDeg = 5f;   // 밴드 각 폭 ±5° — 얇고 선명

            var verts = new Vector3[ringR.Length * Cols];
            var colors = new Color[verts.Length];
            for (int k = 0; k < ringR.Length; k++)
            {
                for (int cIdx = 0; cIdx < Cols; cIdx++)
                {
                    float colA = (cIdx - 1) * HalfWidthDeg * Mathf.Deg2Rad;   // -w, 0, +w
                    int b = k * Cols + cIdx;
                    verts[b] = new Vector3(Mathf.Sin(colA) * ringR[k] * radius, 0f, Mathf.Cos(colA) * ringR[k] * radius);
                    colors[b] = new Color(1f, 1f, 1f, ringA[k] * (cIdx == 1 ? 1f : 0f));
                }
            }
            var tris = new int[(ringR.Length - 1) * (Cols - 1) * 6];
            int t = 0;
            for (int k = 0; k < ringR.Length - 1; k++)
            {
                for (int cIdx = 0; cIdx < Cols - 1; cIdx++)
                {
                    int inner = k * Cols + cIdx;          // 안쪽 링 현재 열
                    int innerNext = inner + 1;            // 안쪽 링 다음 열
                    int outer = inner + Cols;             // 바깥 링 현재 열
                    int outerNext = outer + 1;            // 바깥 링 다음 열
                    tris[t + 0] = inner; tris[t + 1] = outer; tris[t + 2] = innerNext;
                    tris[t + 3] = innerNext; tris[t + 4] = outer; tris[t + 5] = outerNext;   // +Y 법선(지표 팬 동일 감기)
                    t += 6;
                }
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>[171차 D1] 평면 링 메시 — 단위 외접 반경 1, 내경비 0.85(XZ 평면, 32분할). 크기 구동은 localScale.</summary>
        private static Mesh BuildRingMesh()
        {
            var mesh = new Mesh { name = "TelegraphArcImpactRing" };
            const int Segments = 32;
            const float InnerRatio = 0.85f;
            var verts = new Vector3[Segments * 4];
            var tris = new int[Segments * 6];
            var colors = new Color[verts.Length];
            for (int i = 0; i < Segments; i++)
            {
                float a0 = i / (float)Segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)Segments * Mathf.PI * 2f;
                int b = i * 4;
                verts[b + 0] = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                verts[b + 1] = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                verts[b + 2] = new Vector3(Mathf.Sin(a0) * InnerRatio, 0f, Mathf.Cos(a0) * InnerRatio);
                verts[b + 3] = new Vector3(Mathf.Sin(a1) * InnerRatio, 0f, Mathf.Cos(a1) * InnerRatio);
                colors[b + 0] = Color.white;
                colors[b + 1] = Color.white;
                colors[b + 2] = Color.white;
                colors[b + 3] = Color.white;
                int t = i * 6;
                tris[t + 0] = b + 0; tris[t + 1] = b + 2; tris[t + 2] = b + 1;
                tris[t + 3] = b + 1; tris[t + 4] = b + 2; tris[t + 5] = b + 3;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 부채꼴 판정 — radius 내 + 고정 호 방향 기준 ±HalfAngleDeg 내. 표시=판정 정합의 단일 소스.
        /// </summary>
        public static bool IsInFrontArc(Vector3 attackerPos, Vector3 arcDirection, Vector3 targetPos,
            float radius, float halfAngleDeg = HalfAngleDeg)
        {
            Vector3 to = targetPos - attackerPos; to.y = 0f;
            if (to.sqrMagnitude > radius * radius) return false;
            if (to.sqrMagnitude < 0.0001f) return true;   // 발밑 접촉
            var dir = arcDirection; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return true;
            float dot = Vector3.Dot(dir.normalized, to.normalized);
            return dot >= Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        }
        /// <summary>
        /// 부채꼴 메시 — 로컬 +Z를 호 중심으로, ±HalfAngleDeg 부채꼴.
        /// [172차 T1] 알파 그라디언트 호 — 다중 반경 링 + 정점색 알파 폴백(텍스처 없이).
        /// [173차 F2/F4] 이중 레이어(5링): 내부 소프트 글로우(넓고 연함 — 0→0.78 완만 감쇠) + 외곽 림
        /// (얇고 선명 — 0.94 링 피크 후 1.0 경계로 소프트 페이드, 하드 컷 없음). 측면 가장자리 감쇠 심화
        /// (잔여 0.08 — 중심축 경계 하드 컷 제거). 반경 비율 [0..1] — 표시=판정 1:1 계약 유지.
        /// Sprites/Default 정점색 승수 경로(URP Unlit 폴백은 균일색 — 기존 계약).</summary>
        private static Mesh BuildFanMesh(float radius)
        {
            var mesh = new Mesh { name = "TelegraphArcFan" };
            const int Segments = 24;
            const int Rings = 5;
            // [173차 F2] 반경 링 프로파일 — 글로우(중심 1.0 → 0.78 소프트) + 림(0.94 피크 0.7 → 경계 0.15 소프트 페이드)
            float[] ringR = { 0f, 0.45f, 0.78f, 0.94f, 1f };
            float[] ringA = { 1f, 0.95f, 0.48f, 0.7f, 0.15f };

            var verts = new Vector3[(Rings - 1) * (Segments + 1) + 1];
            var colors = new Color[verts.Length];
            var tris = new int[Segments * 3 + (Rings - 2) * Segments * 6];
            verts[0] = Vector3.zero;
            colors[0] = Color.white;   // 중심(apex) — 풀 알파
            for (int k = 1; k < Rings; k++)
            {
                int baseIdx = 1 + (k - 1) * (Segments + 1);
                for (int i = 0; i <= Segments; i++)
                {
                    float u = i / (float)Segments;             // 0..1 — 호 방향
                    float angle = (-HalfAngleDeg + 2f * HalfAngleDeg * u) * Mathf.Deg2Rad;
                    verts[baseIdx + i] = new Vector3(Mathf.Sin(angle) * ringR[k] * radius, 0f, Mathf.Cos(angle) * ringR[k] * radius);
                    // [173차 F4] 측면 경계 완화 — |u| 0.45→1.0 넓은 구간 감쇠(잔여 0.08) × 반경 링 알파(하드 컷 없음)
                    float lateral = 1f - 0.92f * Mathf.SmoothStep(0.45f, 1f, Mathf.Abs(u * 2f - 1f));
                    colors[baseIdx + i] = new Color(1f, 1f, 1f, ringA[k] * lateral);
                }
            }
            // 중심 팬(정점 0 → 링1)
            int ring1 = 1;
            for (int i = 0; i < Segments; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = ring1 + i;
                tris[i * 3 + 2] = ring1 + i + 1;
            }
            // 링 사이 쿼드(내부 링 → 외곽 링) — 기존 팬 감긴 방향(+Y 법선) 유지
            int tri = Segments * 3;
            for (int k = 1; k < Rings - 1; k++)
            {
                int inner = 1 + (k - 1) * (Segments + 1);
                int outer = inner + Segments + 1;
                for (int i = 0; i < Segments; i++)
                {
                    tris[tri + 0] = inner + i; tris[tri + 1] = outer + i; tris[tri + 2] = inner + i + 1;
                    tris[tri + 3] = inner + i + 1; tris[tri + 4] = outer + i; tris[tri + 5] = outer + i + 1;
                    tri += 6;
                }
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>[무번호 181] 전원 360° 원형 메시 — BuildFanMesh 선례(절차 메시 패밀리 — 벡터메시 금지 계약의 승인 예외 경로)
        /// 팬 트라이앵글 360° 확장. 반경 링 프로파일은 [173차 F2] BuildFanMesh 동일 값(내부 소프트 글로우 + 외곽 림 —
        /// 이중 레이어 승계). 원주에 각 경계가 없으므로 측면 감쇠 미적용(하드 컷 없음 계약 동일). 마지막 분할은 첫 분할로
        /// 폐곡(% Segments — 원형 닫힘). Sprites/Default 정점색 승수 경로 — 기존 지표 팬과 동일 idiom.</summary>
        private static Mesh BuildCircleMesh(float radius)
        {
            var mesh = new Mesh { name = "TelegraphCircleFan" };
            const int Segments = 48;   // 전원 360° — 부드러운 원주(부채꼴 24 분할의 원형 등가 확장)
            const int Rings = 5;
            // [173차 F2] BuildFanMesh 동일 반경 링 프로파일 — 글로우(중심 1.0 → 0.78 소프트) + 림(0.94 피크 0.7 → 경계 0.15 소프트 페이드)
            float[] ringR = { 0f, 0.45f, 0.78f, 0.94f, 1f };
            float[] ringA = { 1f, 0.95f, 0.48f, 0.7f, 0.15f };

            var verts = new Vector3[(Rings - 1) * Segments + 1];
            var colors = new Color[verts.Length];
            var tris = new int[Segments * 3 + (Rings - 2) * Segments * 6];
            verts[0] = Vector3.zero;
            colors[0] = Color.white;   // 중심(apex) — 풀 알파
            for (int k = 1; k < Rings; k++)
            {
                int baseIdx = 1 + (k - 1) * Segments;
                for (int i = 0; i < Segments; i++)
                {
                    float angle = i / (float)Segments * Mathf.PI * 2f;   // 0..360° — 전원 원형
                    verts[baseIdx + i] = new Vector3(Mathf.Sin(angle) * ringR[k] * radius, 0f, Mathf.Cos(angle) * ringR[k] * radius);
                    colors[baseIdx + i] = new Color(1f, 1f, 1f, ringA[k]);   // 원주 경계 없음 — 측면 감쇠 미적용
                }
            }
            // 중심 팬(정점 0 → 링1) — 360° 전체(부채꼴 팬과 동일 감기 방향 — +Y 법선)
            int ring1 = 1;
            for (int i = 0; i < Segments; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = ring1 + i;
                tris[i * 3 + 2] = ring1 + (i + 1) % Segments;   // 마지막 분할 → 첫 분할 폐곡
            }
            // 링 사이 쿼드(내부 링 → 외곽 링) — 기존 팬 감긴 방향(+Y 법선) 유지
            int tri = Segments * 3;
            for (int k = 1; k < Rings - 1; k++)
            {
                int inner = 1 + (k - 1) * Segments;
                int outer = inner + Segments;
                for (int i = 0; i < Segments; i++)
                {
                    int innerNext = (i + 1) % Segments;
                    int outerNext = (i + 1) % Segments;   // 외곽 링도 동일 폐곡
                    tris[tri + 0] = inner + i; tris[tri + 1] = outer + i; tris[tri + 2] = inner + innerNext;
                    tris[tri + 3] = inner + innerNext; tris[tri + 4] = outer + i; tris[tri + 5] = outer + outerNext;
                    tri += 6;
                }
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
