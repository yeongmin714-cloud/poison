using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 74-F1] 낚시 라인·찌 비주얼 — FishingSystem 훅 위임 전용(낚시 로직 무손상 계약).
    /// 페이즈: Cast(0.6s 포물선) → Floating(찌 부유) → Reel(릴 긴장 시각화 [무번호 74-F2])
    ///         → Catch(물기둥 낚아올리기) / Snap(라인 끊김 반동) [무번호 74-F3].
    /// 결정론 계약: 무작위 호출 0건 — 사인/코사인 상수 파라미터만 사용. 신규 프리펩/에셋 0건(1회성 런타임 생성).
    /// 수명 자관리: 성공/실패/해제 경로에서 스스로 파괴(isPlaying 가드) — FishingSystem은 참조 null 정리만 담당.
    /// </summary>
    public class FishingLineVisual : MonoBehaviour
    {
        private enum Phase { Cast, Floating, Reel, Catch, Snap }

        // ── 결정론 상수(계획서 고정값 — 무작위 요소 없음) ──
        private const int LinePointCount = 8;             // [무번호 74-F1] 라인 분절 8점
        private const float CastDuration = 0.6f;          // [무번호 74-F1] 캐스팅 포물선 시간(s)
        private const float CastArcHeight = 1.2f;         // [무번호 74-F1] 포물선 중간 제어점 상승(m)
        private const float RodTipForward = 0.5f;         // [무번호 74-F1] 낚싯대 끝 전방 오프셋(m)
        private const float RodTipHeight = 1.4f;          // [무번호 74-F1] 낚싯대 끝 높이 오프셋(m)
        private const float FloatJitterAmplitude = 0.02f; // [무번호 74-F1] 찌 미세 요동 진폭(m)
        private const float FloatJitterPeriod = 2f;       // [무번호 74-F1] 찌 미세 요동 주기(s)
        private const float FloatSagHeight = 0.12f;       // [무번호 74-F1] 부유 라인 자연 처짐(m)
        private const float BobberRadius = 0.06f;         // [무번호 74-F1] 찌 원판 반경(m)
        private const float BiteDipDepth = 0.08f;         // [무번호 74-F1] 입질 찌 숨임 깊이(m)
        private const float BiteDipSeconds = 0.5f;        // [무번호 74-F1] 입질 찌 숨임 지속(s)
        private const int BiteSplashParticleCount = 3;    // [무번호 74-F1] 입질 잔물결 입자(본 물튀김 5보다 소)
        private const float LineWidthMin = 0.025f;        // [무번호 74-F2] 라인 폭 하한(m)
        private const float LineWidthMax = 0.085f;        // [무번호 74-F2] 라인 폭 상한(m)
        private const float ReelDriftMax = 0.5f;          // [무번호 74-F2] 찌 플레이어 쪽 최대 드리프트(m)
        private const float ReelTremorHz = 18f;           // [무번호 74-F2] 고장력 고주파 떨림(Hz)
        private const float SnapDuration = 0.25f;         // [무번호 74-F3] 스냅 반동 회수 시간(s)
        private const float CatchDuration = 0.4f;         // [무번호 74-F3] 물기둥 페이드 시간(s)
        private const int CatchStreakCount = 3;           // [무번호 74-F3] 물기둥 스트릭 3본
        private const float CatchStreakAngle = 25f;       // [무번호 74-F3] 스트릭 좌우 전개각(도)
        private const float CatchStreakSpread = 0.07f;    // [무번호 74-F3] 스트릭 수평 산포(m)
        private const float CatchStreakLength = 0.5f;     // [무번호 74-F3] 스트릭 초기 길이(m)
        private const float CatchStreakWidth = 0.045f;    // [무번호 74-F3] 스트릭 폭(m)
        private const float CatchStreakRise = 1.4f;       // [무번호 74-F3] 스트릭 상승 높이(m)
        private const int SplashParticleCount = 5;        // [무번호 74-F3] 물튀김 입자 수
        private const float SplashGravity = 0.35f;        // [무번호 74-F3] 물방울 낙하 중력
        private const float SplashDespawnSeconds = 0.8f;  // [무번호 74-F3] 물튀김 오브젝트 정리 지연(s)

        private static readonly Color LineColorCalm = new Color(0.34f, 0.65f, 1.0f);       // [무번호 74-F2] 평온 파랑
        private static readonly Color LineColorDanger = new Color(0.9f, 0.25f, 0.2f);      // [무번호 74-F2] 경고 적색
        private static readonly Color SplashWaterColor = new Color(0.65f, 0.9f, 1f, 0.8f); // [무번호 74-F3] 물색
        private static readonly Color BobberColor = new Color(0.85f, 0.25f, 0.2f);         // [무번호 74-F1] 찌 적색
        private static readonly Color CatchStreakColor = new Color(0.85f, 0.95f, 1f, 0.9f);// [무번호 74-F3] 물기둥 밝은 색
        private static readonly float FloatJitterOmega = Mathf.PI * 2f / FloatJitterPeriod;// [무번호 74-F1] 요동 각속도

        // ── 상태 ──
        private Phase _phase = Phase.Cast;
        private float _phaseStart;
        private Transform _owner;
        private Vector3 _castPoint;
        private Vector3 _rodTip;
        private Vector3 _bobberPos;
        private Vector3 _snapFrom;
        private float _tension;
        private float _progress;
        private float _bitePulseTime = -99f;
        private LineRenderer _line;
        private LineRenderer _bobber;
        private LineRenderer[] _streaks;
        private Vector3[] _streakDirs;
        private Material _lineMat;
        private bool _selfDestructing;

        /// <summary>[무번호 74-F1] 캐스팅 시작 — 1회성 런타임 오브젝트 생성(프리펩 0건, HideAndDontSave).</summary>
        public static FishingLineVisual BeginCast(Transform owner, Vector3 castPoint)
        {
            var go = new GameObject("FishingLine") { hideFlags = HideFlags.HideAndDontSave };
            var visual = go.AddComponent<FishingLineVisual>();
            visual.Initialize(owner, castPoint);
            return visual;
        }

        private void Initialize(Transform owner, Vector3 castPoint)
        {
            _owner = owner;
            _castPoint = castPoint;
            _bobberPos = castPoint;
            _phaseStart = Time.time;

            _lineMat = CreateLineMaterial();
            _line = BuildLineRenderer("FishingLineRenderer", LineWidthMin, LineColorCalm);
            _bobber = BuildBobberDisc();

            RefreshRodTip();
        }

        /// <summary>[무번호 74-F1] 라인 머티리얼 — FxMaterialLibrary에 라인용 생성 헬퍼 부재 →
        ///   Sprites/Default 폴백(선례 경로). 인스턴스 머티리얼이므로 OnDestroy에서 반드시 파괴.</summary>
        private static Material CreateLineMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;   // 셰이더 부재 환경 — 라인렌더러 기본 머티리얼 폴백
            return new Material(shader) { name = "FishingLineMat" };
        }

        /// <summary>[무번호 74-F1] 자식 라인렌더러 빌더 — 그림자암살자 스트릭 인프라 패턴(정점색 구동).</summary>
        private LineRenderer BuildLineRenderer(string childName, float width, Color color)
        {
            var child = new GameObject(childName) { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(transform, false);
            var lr = child.AddComponent<LineRenderer>();
            lr.positionCount = LinePointCount;
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.material = _lineMat;
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        /// <summary>[무번호 74-F1] 찌 원판 — 수평 원 루프 라인(경량 1회성 FX 리소스, 프리펩 0건).</summary>
        private LineRenderer BuildBobberDisc()
        {
            var lr = BuildLineRenderer("FishingBobber", 0.02f, BobberColor);
            lr.positionCount = 12;   // 원 루프 12분할(결정론)
            lr.loop = true;
            SetDiscPoints(lr, _castPoint, BobberRadius);
            return lr;
        }

        /// <summary>[무번호 74-F1] 찌 원판 정점 — XZ 평면 원 루프(결정론).</summary>
        private static void SetDiscPoints(LineRenderer lr, Vector3 center, float radius)
        {
            const int DiscSegments = 12;
            for (int i = 0; i < DiscSegments; i++)
            {
                float a = i * Mathf.PI * 2f / DiscSegments;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
        }

        /// <summary>[무번호 74-F1] 낚싯대 끝 — owner.position + forward*0.5 + up*1.4(결정론 상수).
        ///   owner 소멸 시 마지막 좌표 유지(널 가드 — 파괴 순서 무관 안전).</summary>
        private void RefreshRodTip()
        {
            if (_owner != null)
                _rodTip = _owner.position + _owner.forward * RodTipForward + Vector3.up * RodTipHeight;
        }

        private void Update()
        {
            RefreshRodTip();
            float t = Time.time - _phaseStart;
            switch (_phase)
            {
                case Phase.Cast: TickCast(t); break;
                case Phase.Floating: TickFloating(t); break;
                case Phase.Reel: TickReel(); break;
                case Phase.Catch: TickCatch(t); break;
                case Phase.Snap: TickSnap(t); break;
            }
        }

        /// <summary>2차 베지어 — B(t)=(1-t)²P0+2(1-t)tC+t²P2(결정론).</summary>
        private static Vector3 QuadraticBezier(Vector3 p0, Vector3 ctrl, Vector3 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * ctrl + t * t * p2;
        }

        /// <summary>[무번호 74-F1] 베지어 곡선 적용 — 라인 8점 재생성 + 정점색 갱신.</summary>
        private static void ApplyCurve(LineRenderer lr, Vector3 p0, Vector3 ctrl, Vector3 p2, Color color)
        {
            for (int i = 0; i < LinePointCount; i++)
            {
                float t = i / (LinePointCount - 1f);
                lr.SetPosition(i, QuadraticBezier(p0, ctrl, p2, t));
            }
            lr.startColor = color;
            lr.endColor = color;
        }

        /// <summary>[무번호 74-F1] 페이즈 전이 — 상태 전환과 페이즈 기준 시각(_phaseStart) 갱신을 한 곳에서.
        ///   Tick* 분기의 t 계산(Time.time - _phaseStart)과 정합 유지.</summary>
        private void BeginPhase(Phase next)
        {
            _phase = next;
            _phaseStart = Time.time;
        }

        /// <summary>[무번호 74-F1] 캐스팅 — rodTip→castPoint 2차 베지어 포물선(제어점 +up*1.2), 0.6s.
        ///   라인은 찌가 지나는 자취(부분 곡선)를 따라 비행.</summary>
        private void TickCast(float t)
        {
            float k = Mathf.Clamp01(t / CastDuration);
            Vector3 ctrl = Vector3.Lerp(_rodTip, _castPoint, 0.5f) + Vector3.up * CastArcHeight;
            Vector3 end = QuadraticBezier(_rodTip, ctrl, _castPoint, k);
            ApplyCurve(_line, _rodTip, ctrl, end, LineColorCalm);
            _bobberPos = end;
            SetDiscPoints(_bobber, end, BobberRadius);
            if (k >= 1f) BeginPhase(Phase.Floating);
        }

        /// <summary>[무번호 74-F1] 부유 — 찌가 castPoint에 정박, 사인 미세 요동(진폭 0.02m·주기 2s).
        ///   입질 직후(OnBite 펄스) 찌 숨임 추가. 라인은 자연 처짐 사곡.</summary>
        private void TickFloating(float t)
        {
            Vector3 bobber = _castPoint
                             + Vector3.up * (Mathf.Sin(t * FloatJitterOmega) * FloatJitterAmplitude)
                             + new Vector3(Mathf.Cos(t * FloatJitterOmega), 0f, 0f) * (FloatJitterAmplitude * 0.5f);
            float biteT = Time.time - _bitePulseTime;
            if (biteT >= 0f && biteT < BiteDipSeconds)
                bobber += Vector3.down * (Mathf.Sin(biteT / BiteDipSeconds * Mathf.PI) * BiteDipDepth);

            Vector3 mid = Vector3.Lerp(_rodTip, bobber, 0.5f) + Vector3.down * FloatSagHeight;
            ApplyCurve(_line, _rodTip, mid, bobber, LineColorCalm);
            _bobberPos = bobber;
            SetDiscPoints(_bobber, bobber, BobberRadius);
        }

        /// <summary>[무번호 74-F1] 입질 — 찌 숨임 펄스 + 소형 잔물결(입자 3).</summary>
        public void OnBite()
        {
            _bitePulseTime = Time.time;
            SpawnSplash(_castPoint, BiteSplashParticleCount);
        }

        /// <summary>[무번호 74-F2] 릴 긴장 시각화 — 미니게임 프레임마다 FishingSystem이 호출.
        ///   라인 폭 0.025→0.085m·색 파랑→경고 적 lerp(tension), 찌가 플레이어 쪽으로 progress*0.5m 드리프트,
        ///   요동 진폭 ×(1+tension*2). Catch/Snap 진행 중이면 무시(소비 경로 보호).</summary>
        public void UpdateTension(float tension, float progress)
        {
            if (_phase == Phase.Catch || _phase == Phase.Snap) return;
            if (_phase == Phase.Cast) BeginPhase(Phase.Floating);   // 캐스팅 중 입질 예외 — 즉시 부유 확정
            if (_phase == Phase.Floating) BeginPhase(Phase.Reel);

            _tension = Mathf.Clamp01(tension);
            _progress = Mathf.Clamp01(progress);
            TickReel();
        }

        /// <summary>[무번호 74-F2] 리일 프레임 갱신 — 긴장 비례 폭/색/요동 + 찌 드리프트 + 고주파 떨림.
        ///   장력이 낮을수록 라인 처짐(자연스러운 느슨함), 높을수록 팽팽.</summary>
        private void TickReel()
        {
            float amp = FloatJitterAmplitude * (1f + _tension * 2f);
            float t = Time.time - _phaseStart;
            Vector3 driftDir = _rodTip - _castPoint;
            driftDir.y = 0f;
            driftDir = driftDir.sqrMagnitude > 0.0001f ? driftDir.normalized : Vector3.forward;

            Vector3 bobber = _castPoint + driftDir * (_progress * ReelDriftMax)
                             + Vector3.up * (Mathf.Sin(t * FloatJitterOmega) * amp)
                             + Vector3.up * (Mathf.Sin(Time.time * ReelTremorHz) * (0.01f * _tension));

            Vector3 mid = Vector3.Lerp(_rodTip, bobber, 0.5f) + Vector3.down * (FloatSagHeight * (1f - _tension));
            _line.widthMultiplier = Mathf.Lerp(LineWidthMin, LineWidthMax, _tension);
            ApplyCurve(_line, _rodTip, mid, bobber, Color.Lerp(LineColorCalm, LineColorDanger, _tension));
            _bobberPos = bobber;
            SetDiscPoints(_bobber, bobber, BobberRadius);
            Color bc = Color.Lerp(BobberColor, LineColorDanger, _tension);
            _bobber.startColor = bc;
            _bobber.endColor = bc;
        }

        /// <summary>[무번호 74-F3] 낚아올리기 성공 — 물기둥 스트릭 3본 상승 + 물튀김 입자 5,
        ///   0.4s 페이드 후 자가 파괴(isPlaying 가드).</summary>
        public void PlayCatch()
        {
            if (_phase == Phase.Catch || _phase == Phase.Snap) return;
            BeginPhase(Phase.Catch);
            SpawnSplash(_castPoint, SplashParticleCount);
            BuildCatchStreaks();
            _bobber.gameObject.SetActive(false);   // 찌 회수 — 물기둥으로 대체
        }

        /// <summary>[무번호 74-F3] 물기둥 스트릭 3본 — castPoint 기준 좌우 소각 오프셋 결정론 배치.</summary>
        private void BuildCatchStreaks()
        {
            _streaks = new LineRenderer[CatchStreakCount];
            _streakDirs = new Vector3[CatchStreakCount];
            for (int k = 0; k < CatchStreakCount; k++)
            {
                float angle = (k - 1) * CatchStreakAngle;   // -1·0·+1 전개(결정론)
                _streakDirs[k] = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var lr = BuildLineRenderer("FishingCatchStreak" + k, CatchStreakWidth, CatchStreakColor);
                lr.positionCount = 2;
                _streaks[k] = lr;
            }
        }

        /// <summary>[무번호 74-F3] 물기둥 틱 — 스트릭 상승·단축 + 전체 알파 페이드, 0.4s 후 자가 파괴.</summary>
        private void TickCatch(float t)
        {
            if (_streaks == null) return;
            float k = Mathf.Clamp01(t / CatchDuration);
            float rise = k * CatchStreakRise;
            for (int i = 0; i < _streaks.Length; i++)
            {
                if (_streaks[i] == null) continue;
                Vector3 basePos = _castPoint + _streakDirs[i] * CatchStreakSpread;
                _streaks[i].SetPosition(0, basePos + Vector3.up * rise);
                _streaks[i].SetPosition(1, basePos + Vector3.up * (rise + CatchStreakLength * (1f - k)));
                Color c = CatchStreakColor;
                c.a = 1f - k;
                _streaks[i].startColor = c;
                _streaks[i].endColor = c;
            }
            Color lc = _line.startColor;
            lc.a = 1f - k;   // 낚아올린 라인 — 물기둥과 함께 페이드
            _line.startColor = lc;
            _line.endColor = lc;
            if (k >= 1f) SelfDestruct(0f);
        }

        /// <summary>[무번호 74-F3] 라인 스냅(끊김) — 끝점(찌)이 낚싯대 쪽으로 순간 회수, 0.25s 페이드 + 물튀김 입자 5.</summary>
        public void PlaySnap()
        {
            if (_phase == Phase.Catch || _phase == Phase.Snap) return;
            BeginPhase(Phase.Snap);
            _snapFrom = _bobberPos;
            SpawnSplash(_snapFrom, SplashParticleCount);
        }

        /// <summary>[무번호 74-F3] 스냅 틱 — 라인 끝점이 rodTip로 t² 가속 회수(반동), 전체 페이드, 0.25s 후 자가 파괴.</summary>
        private void TickSnap(float t)
        {
            float k = Mathf.Clamp01(t / SnapDuration);
            float e = k * k;   // 반동 가속 이징(결정론)
            Vector3 end = Vector3.Lerp(_snapFrom, _rodTip, e);
            Vector3 mid = Vector3.Lerp(_rodTip, end, 0.5f) + Vector3.up * (CastArcHeight * 0.25f * (1f - e));
            Color c = Color.Lerp(LineColorCalm, LineColorDanger, 0.5f);
            c.a = 1f - k;
            ApplyCurve(_line, _rodTip, mid, end, c);
            SetDiscPoints(_bobber, end, BobberRadius);
            Color bc = _bobber.startColor;
            bc.a = 1f - k;
            _bobber.startColor = bc;
            _bobber.endColor = bc;
            if (k >= 1f) SelfDestruct(0f);
        }

        /// <summary>[무번호 74-F1] 즉시 해제 — FishingSystem 종료/리셋 경로에서 호출(isPlaying 가드).</summary>
        public void Release()
        {
            SelfDestruct(0f);
        }

        /// <summary>자가 파괴 — isPlaying 가드(Destroy/DestroyImmediate — ShockwaveRingFX 156차 선례).</summary>
        private void SelfDestruct(float delay)
        {
            if (_selfDestructing) return;
            _selfDestructing = true;
            if (Application.isPlaying) Object.Destroy(gameObject, delay);
            else Object.DestroyImmediate(gameObject);
        }

        /// <summary>[무번호 74-F3] 물튀김 입자 — FxEnvReaction.EnsureSplashTexture(internal 노출 재사용 — 텍스처 단일 소스)
        ///   + ShockwaveRingFX.SpawnMoveTrailPuffCore 선례 패턴(1회 Emit·isPlaying 가드 Destroy/DestroyImmediate).
        ///   물색(0.65,0.9,1,0.8)·수명 0.4~0.5s·중력 0.35.</summary>
        private static void SpawnSplash(Vector3 pos, int count)
        {
            var go = new GameObject("FishingSplash") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.5f);   // 수명 0.4~0.5s(계획 고정)
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = SplashWaterColor;      // 물색(0.65, 0.9, 1, 0.8)
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = SplashGravity;    // 물방울 낙하
            main.maxParticles = SplashParticleCount;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.04f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;   // 1회 Emit만 — 지속 방출 없음

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rnd = go.GetComponent<ParticleSystemRenderer>();
            if (rnd != null)
            {
                rnd.material = FxMaterialLibrary.CreateSoftParticle("FishingSplashMat", SplashWaterColor, FxEnvReaction.EnsureSplashTexture());
                rnd.renderMode = ParticleSystemRenderMode.Billboard;
                rnd.alignment = ParticleSystemRenderSpace.View;
                rnd.sortMode = ParticleSystemSortMode.Distance;
            }
            ps.Emit(count);
            if (Application.isPlaying) Object.Destroy(go, SplashDespawnSeconds);
            else Object.DestroyImmediate(go);   // EditMode — 입자 시뮬 부재 환경 즉시 정리(156차 선례)
        }

        /// <summary>[무번호 74-F1] 머티리얼 인스턴스 파괴 필수 — 런타임 new Material 누수 방지(isPlaying 가드).</summary>
        private void OnDestroy()
        {
            if (_lineMat != null)
            {
                if (Application.isPlaying) Object.Destroy(_lineMat);
                else Object.DestroyImmediate(_lineMat);
                _lineMat = null;
            }
        }
    }
}
