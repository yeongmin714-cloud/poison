using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-09-25 공격 FX 프리미엄 계층] '참 무기 궤적' 3D 스윕 리본 — 젤다(BOTW)식 흰 부채꼴 궤적을
    /// 실제 무기 블레이드 팁의 월드 궤적을 샘플링해 스윕 서피스 메시로 재구성한다.
    /// 기존 WeaponSwingTrail(TrailRenderer 평면 잔상) 대비: 경로를 실제 3D로 따라가고,
    /// 전두엽(최신 샘플)이 굵고 꼬리로 갈수록 얇아지며 0.35s 안에 페이드. URP 안전 발광 흰 재질 재사용.
    /// 기본 OFF(PremiumArcEnabled=false) — 활성화 전까지 기존 공격 연출에 전혀 영향 없음(완전 추가 레이어).
    ///
    /// 구조(SlashFxHost 선례): static 파사드 + 동일 파일 내 top-level 호스트(<see cref="AttackArcHost"/>,
    /// MonoBehaviour — static 클래스는 LateUpdate/메시 갱신을 못 돌리므로 44차 AutoDestroy·SlashFxHost와
    /// 동일하게 컴포넌트 분리). 호스트 GO는 HideAndDontSave로 계층에 노출되지 않고, 활동 없음 시 스스로 파괴된다.
    /// 샘플 소스: WeaponSwingTrail.GetTrailTipAnchor()(무기 팁 트레일 앵커 — 손 본 추적 공짜 동기)의 월드 좌표.
    /// </summary>
    public static class AttackArcVFX
    {
        /// <summary>[무번호 57-P1] 슬래시 텍스처 구조적 최대 알파 — 페더+코어 합(1.85)의 Clamp01 포화가
        ///   "지면에 깔린 불투명 흰 다이아몬드"의 뿌리(공격씬 10 실측). 텍스처 최대치를 0.62로 캡(선단 추가 ×0.85)
        ///   — 53차 핀 계약 진화. FillSlashPixels(파사드 산식) 전용.</summary>
        internal const float AlphaCap = 0.62f;
        /// <summary>[무번호 35-S1] 기본 ON 정식 채택 — 3D 스윕 리본이 기본 공격 연출(사용자 Play 판정 전제).
        ///   F6 토글은 A/B 비교 수단으로 유지(기존 이력 보존).</summary>
        public static bool PremiumArcEnabled = true;

        /// <summary>토글 설정 + 상태 로그 — 인스펙터/디버그에서 켜는 지점.</summary>
        public static void SetPremium(bool on)
        {
            if (PremiumArcEnabled == on) return;
            PremiumArcEnabled = on;
            UnityEngine.Debug.Log("[AttackArcVFX] " + (on ? "활성 (검날 3D 스윕 리본)" : "비활성 (기존 트레일만)"));
        }

        // ── 캡/시간 상수(공개 — 튜닝용) ──
        /// <summary>경로 샘플 링버퍼 용량(프레임 샘플 수) — 정점 예산은 이×2(샘플당 좌/우).</summary>
        public static int SampleCap = 64;
        /// <summary>궤적 유지 시간(s) — 나이가 이 값을 넘은 샘플은 잘리고 알파 0 수렴.</summary>
        public static float TrailSeconds = 0.35f;
        /// <summary>[무번호 60] 속도 비례 알파 기준 팁 속도(m/s) — 느린 스윙 알파 하향·빠른 스윙 상향.
        ///   EmitPass의 speedBoost가 이 값 대비 팁 이동속도를 반영. 상수 트림은 Play 체감 게이트.</summary>
        public static float RefTipSpeed = 14f;
        /// <summary>전두엽(최신 샘플) 반폭(m) — 폭 = 이×pow(1-age/T,1.2), 꼬리로 갈수록 가늘어짐.</summary>
        public static float MaxHalfWidth = 0.35f;
        /// <summary>전체 크기 배율(공격 범위 연동 스케일 예약) — 1.0=기본. 인게임 범위 기반 배율을 여기에 곱한다.</summary>
        public static float ArcScale = 1.0f;
        /// <summary>[Phase3] 사거리 자동 비례(스윙마다 갱신) — MaxHalfWidth×ArcScale(수동)×ArcRangeScale(자동). 장검·단도·창 리치에 맞춤.</summary>
        public static float ArcRangeScale = 1.0f;

        /// <summary>숨은 호스트 — 파괴되면 Unity == null 판정으로 다음 Swing에서 재생성된다.</summary>
        private static AttackArcHost _host;

        /// <summary>스윙 개시 — 콤보 발화(HumanoidClipDriver.FireComboSlash)에서 호출. 호스트 없으면 지연 생성.</summary>
        public static void Swing(WeaponType type, Transform fallbackRoot = null)
        {
            if (!PremiumArcEnabled) return;   // 이중 가드 — 직접 호출 오용 시에도 OFF 상태에선 아무것도 생성하지 않음
            ArcRangeScale = ScaleForRange(type);   // [Phase3] 사거리 자동 비례 — 리치가 긴 무기일수록 궤적 확대
            EnsureHost();
            if (_host != null)
            {
                _host.Swing(type, fallbackRoot);
                // [무번호 57-P2] FX 스폰 레지스트리 — 아크는 수명 0.35s로 역산 시점 전 소멸 → 스폰 좌표 기록.
                var root = fallbackRoot != null ? fallbackRoot : (_host != null ? _host.transform : null);
                MagentaSentinel.RegisterFxSource("AttackArc", root != null ? root.position : Vector3.zero);
            }
        }

        /// <summary>[Phase3] 타입별 공격 사거리에 비례한 크기 배율 — WeaponData.range(공개 필드) 기반.</summary>
        private static float ScaleForRange(WeaponType type)
        {
            float range;
            switch (type)
            {
                case WeaponType.Spear: range = ProjectName.Core.WeaponData.Spear.range; break;   // 4m
                case WeaponType.Bow:   range = ProjectName.Core.WeaponData.Bow.range; break;     // 10m
                case WeaponType.Fist:  range = ProjectName.Core.WeaponData.Fist.range; break;    // 2m
                default:               range = ProjectName.Core.WeaponData.Sword.range; break;   // 2.5m
            }
            return Mathf.Clamp(range / 2.5f, 0.7f, 1.6f);   // 검(2.5)=1.0 중립, 단검·맨손 0.7~, 장검·창 1.6~ 상한
        }

        /// <summary>콤보 종료/인터럽트 — 샘플 동결 후 TrailSeconds 동안 페이드아웃. 호스트 없으면 무시.</summary>
        public static void Settle() { if (_host != null) _host.Settle(); }

        /// <summary>즉시 정리 — 메시/호스트 파괴. 호스트 없으면 무시.</summary>
        public static void Clear() { if (_host != null) _host.ClearNow(); }

        /// <summary>콤보 스테이지 틴트 갱신(1=흰/2=골드/3=주황). 호스트 없으면 무시.</summary>
        public static void SetTint(WeaponType type, int stage) { if (_host != null) _host.Tint(type, stage); }

        // ── [무번호 53-P2] 절차 슬래시 텍스처 — 부채꼴 슬래시 판독용 ──
        //  배경(11-16 사용자 영상 실측): 텍스처 없는 additive 흰 리본이 불투명 폴리곤 덩어리로 판독
        //  (하드엣지·사각 종단). 텍스처 알파가 additive 강도를 조형해 가장자리 페더/꼬리 페이드를 만든다.
        private static Texture2D _slashTexture;   // 정적 캐시 1회 — 재생성 금지(GetStarTexture/GetRingTexture 선례)

        /// <summary>슬래시 텍스처 해상도 계약 — 256(길이 u)×64(폭 v), pow2·mip 없음(파티클 텍스처 선례 준용).</summary>
        internal const int SlashTexWidth = 256;
        internal const int SlashTexHeight = 64;

        /// <summary>
        /// [무번호 53-P2] 절차 슬래시 텍스처(256×64 RGBA) — 리본/트레일 공용.
        ///   v(폭)방향 가우시안 페더(중심 1→가장자리 0) + u(길이)방향 전두엽(u=1) 밝음→꼬리(u=0) 페이드
        ///   + 중심 코어 밴드(검날 판독). 결정론 계약: 순수 수학만(Mathf) — 난수 API 0건(무작위성 배제).
        ///   캐시 1회 — 동일 호출 동일 픽셀(AttackArcTextureContract_53Tests 직접판정).
        /// </summary>
        internal static Texture2D GetSlashTexture()
        {
            if (_slashTexture != null) return _slashTexture;
            var tex = new Texture2D(SlashTexWidth, SlashTexHeight, TextureFormat.RGBA32, false)
            {
                name = "AttackArcSlashTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[SlashTexWidth * SlashTexHeight];
            FillSlashPixels(pixels, SlashTexWidth, SlashTexHeight);
            tex.SetPixels(pixels);
            tex.Apply();
            _slashTexture = tex;
            return tex;
        }

        /// <summary>
        /// 슬래시 픽셀 산식 — 순수 함수(결정론 — 계약테스트가 텍스처 생성과 무관하게 동일 산식 직접판정).
        ///   알파 = 페더(폭 가우시안)×길이 페이드 + 코어 밴드 부스트, RGB = 백(틴트/알파는 정점색 곱 — 계약).
        /// </summary>
        internal static void FillSlashPixels(Color[] pixels, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                float v01 = (y + 0.5f) / h;                        // 0=하단(좌 가장자리) → 1=상단(우 가장자리)
                float d = Mathf.Abs(v01 - 0.5f) * 2f;              // 0=폭 중심 → 1=가장자리
                float feather = Mathf.Exp(-(d * d) / 0.16f);       // [53-P2 핀] 가우시안 시그마²=0.16 — 중심 1→가장자리 소멸
                float core = Mathf.Exp(-(d * d) / 0.02f);          // [53-P2 핀] 중심 코어 밴드(좁은 고알파 — 검기 판독)
                float edgeKill = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - 0.92f) / 0.08f));   // [53-P2 핀] 최외곽 소프트 소멸(가장자리 알파 ≈0 — 하드 사각 엣지 금지)
                for (int x = 0; x < w; x++)
                {
                    float u01 = (x + 0.5f) / w;                    // 0=꼬리(오래된 샘플) → 1=전두엽(최신 샘플)
                    float head = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u01 - 0.82f) / 0.18f));   // [53-P2 핀] 선단 리드온(밴드 끝 하드컷 방지)
                    float len = Mathf.Pow(u01, 1.35f) * (0.85f + 0.15f * head);                    // [53-P2 핀] 꼬리 페이드(1.35)+선단 가강(+0.15)
                    // [무번호 57-P1] 전체 최대 알파 캡 — 공격씬 10 실측: 알파블렌드 전환 후에도 페더+코어 합(1.85)이
                    //   Clamp01에서 포화해 "지면에 깔린 불투명 흰 다이아몬드"로 읽혔다(비전 실측). 구조적 최대치를
                    //   0.62로 캡하고 선단 8%는 추가 15% 감쇠 — 팁이 하드 벽이 아니라 빗살로 빠진다.
                    float baseA = (feather + core * 0.85f) * len * edgeKill;
                    float frontT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u01 - 0.92f) / 0.08f));
                    float a = Mathf.Min(baseA, AlphaCap * Mathf.Lerp(1f, 0.85f, frontT));
                    pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
        }

        /// <summary>
        /// [무번호 53-P2] 슬래시 텍스처 머티리얼 바인딩 — _BaseMap(URP)/_MainTex(폴백) 속성 존재 시에만 대입.
        ///   additive·트랜스페런트·정점색 알파 페이드 계약 무변경 — 텍스처가 additive 강도 분포를 조형할 뿐.
        /// </summary>
        internal static void BindSlashTexture(Material mat)
        {
            if (mat == null) return;
            Texture2D tex = GetSlashTexture();
            if (tex == null) return;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        private static void EnsureHost()
        {
            if (_host != null) return;   // 파괴된 호스트는 Unity == null 판정으로 재생성 경로 진입
            var go = new GameObject("AttackArcVFX") { hideFlags = HideFlags.HideAndDontSave };
            _host = go.AddComponent<AttackArcHost>();
        }

        /// <summary>호스트 자가 파괴(OnDestroy)에서 정적 참조 해제 — 어셈블리 내부 전용.</summary>
        internal static void ResetHost() { _host = null; }
    }

    /// <summary>
    /// AttackArcVFX의 숨은 호스트 — LateUpdate에서 무기 팁 월드 좌표를 링버퍼에 적재하고,
    /// 링버퍼(oldest→newest)로 스윕 리본 메시를 매 프레임 재구성한다(SlashFxHost·TrailPulseRunner와 동일한
    /// 파일 내부 보조 MonoBehaviour 패턴 — 44차 AutoDestroy 선례).
    ///
    /// 설계 노트:
    ///  - 정점 예산 = SampleCap×2(샘플당 좌/우 2정점) — cap 64 기준 최대 128정점, 버퍼는 Awake 1회 할당·재사용
    ///    (핫패스에서 관리 힙 new 없음 — List는 Capacity 선할당 후 Clear/Add 재사용).
    ///  - 정점은 월드좌표 직접 베이크 + 메시 GO는 씬 루트 항등 트랜스폰(플레이어 이동/회전과 무관한 절대 배치).
    ///  - 측면축 = normalize(cross(up, 샘플 진행방향 중앙차분)) — 수직 스윕 등 퇴화 시 마지막 유효축 재사용.
    ///  - 각 쿼드를 양권선(앞/뒤)으로 2중 적재 — 재질 컬링 설정과 무관하게 양면 렌더 보장.
    ///  - 재질 1순위: WeaponSwingTrail.GetTrailMaterial()(검증된 URP 발광 흰 재질 공유 재사용 — 클론 없음),
    ///    미발견 시 경량 폴백 머티리얼, 그래도 null이면 메시 갱신만 건너뛰고 샘플링은 지속(절대 예외 없음).
    ///  - 활동 없음(마지막 Swing/Settle 이후 TrailSeconds+유예 경과) 시 메시 파괴 후 스스로 소멸(씬 오염 없음).
    ///  - 플레이어 루트의 위치/회전은 절대 기록하지 않는다 — 이 호스트가 만지는 것은 자기 메시 GO뿐.
    /// </summary>
    public sealed class AttackArcHost : MonoBehaviour
    {
        // ── 링버퍼(월드 좌표 샘플) — Awake/Swing에서 1회 할당, 핫패스에서 new 없음 ──
        private float[] _px;
        private float[] _py;
        private float[] _pz;
        private float[] _birth;      // 샘플 생성 시각(Time.time)
        private float[] _spd;        // [무번호 60] 샘플 팁 속도(m/s) — WriteSample에서 이전 샘플과의 거리/시간 산출
        private int _cap;            // 실제 할당 용량(AttackArcVFX.SampleCap 스냅샷)
        private int _write;          // 다음 샘플 기록 인덱스
        private int _first;          // 가장 오래된 유효 샘플 인덱스
        private int _count;          // 유효 샘플 수

        // ── 상태 ──
        private WeaponType _type = WeaponType.Sword;
        private int _lastStage;
        private Material _mat;                 // 공유 재질(소유 아님 — 파괴하지 않고 참조만 유지)
        private Transform _fallbackRoot;       // 팁 앵커 부재 시 폴백 샘플 위치(플레이어 루트)
        private GameObject _meshGO;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private bool _settled;                 // Settle(콤보 종료) — 샘플 동결 후 페이드아웃
        private float _settleTime;
        private float _lastSwingTime = -999f;  // 마지막 Swing 시각 — 활동 없음 자가 파괴 기준
        private float _lastActivityTime = -999f;  // [QA_fix] 실제 샘플 활동 시각(스윙 중 자가파괴 방지) — 샘플 쓰기마다 갱신
        private float _lastSampleTime;         // 마지막 샘플 기록 시각(진단/트림 보조)
        private bool _ownsMat;                 // [QA_fix] 폴백 재질은 본인 소유 — 파괴 책임 있음(공유 트레일 재질은 아님)

        // (내부 RebuildRibbon이 프레임당 정확 길이 배열을 신규 할당 — C# arraycopy 부재·프로젝트 관례
        //  mesh.vertices/.colors/.triangles 배열 직접 대입 준수. 2패스 최대 정점 ≤256·삼각 ≤1512, 스윙 중에만 극소량.)

        // 마지막 유효 측면축 — 진행방향 퇴화(수직 직진 등) 시 폴백
        private Vector3 _lastRight = Vector3.right;

        /// <summary>활동 없음 자가 파괴 유예(초) — 페이드(TrailSeconds) 완료 후 추가 대기 프레임 여유.</summary>
        private const float IdleGraceSeconds = 0.4f;
        /// <summary>인접 샘플 최소 구간 길이²(m) — 미세 지터로 생기는 0폭 쿼드 스팸 방지.</summary>
        private const float MinSegmentLengthSq = 0.004f * 0.004f;
        /// <summary>
        /// [무번호 53-P2] 글로우 패스 알파 배율 소스계약 핀 — 0.30의 절반(0.15). 11-16 실측: 텍스처 없던 시절
        /// additive 흰 덩어리 포화의 원인 중 하나가 글로우 과노출 — 절반으로 낮춰 외곽 미광만 남기고, 코어(×1.0)는 유지.
        /// </summary>
        internal const float GlowPassAlphaMul = 0.15f;
        /// <summary>[무번호 57-P1] 코어 패스 알파 배율 상한 — 공격씬 10 실측(8.25s 크롭 "지면에 깔린 불투명 흰
        ///   다이아몬드")으로 1.0→0.6 하향. 알파블렌드에서 0.6+전단 캡 = 밝은 슬래시 질감·불투명 판 해소.</summary>
        internal const float CorePassAlphaMul = 0.6f;
        /// <summary>[무번호 60] 폭 프로파일 지수 — 전두엽(신선 샘플, life01=1) 굵게 유지·꼬리 가늘게 강화.
        ///   47계획 §8 폭 프로파일 항목: 기존 인라인 1.2(완만) → 1.7(가파른 테이퍼). 값 튜닝은 Play 체감 게이트.</summary>
        internal const float WidthProfilePow = 1.7f;

        private void Awake()
        {
            EnsureCapacity(Mathf.Max(8, AttackArcVFX.SampleCap));
        }

        // ================================================================
        // 공개 진입점(AttackArcVFX 파사드 전용)
        // ================================================================

        /// <summary>스윙 개시 — 타입 저장, 세틀 해제, 메시 풀 확보. 샘플 적재는 LateUpdate가 담당.</summary>
        public void Swing(WeaponType type, Transform fallbackRoot)
        {
            _type = type;
            if (fallbackRoot != null) _fallbackRoot = fallbackRoot;
            _settled = false;
            _lastSwingTime = Time.time;
            _lastActivityTime = Time.time;   // [QA_fix] 스윙 개시 = 활동 시작(자가파괴 기준 갱신)
            EnsureCapacity(Mathf.Max(8, AttackArcVFX.SampleCap));   // 튜닝 중 용량 상향 흡수(평시 no-op)
            EnsureMesh();
        }

        /// <summary>콤보 종료 — 샘플 동결 후 TrailSeconds 동안 페이드아웃되고 자가 파괴로 이어짐.</summary>
        public void Settle()
        {
            if (_settled) return;
            _settled = true;
            _settleTime = Time.time;
        }

        /// <summary>즉시 정리 — 메시 파괴 + 자가 파괴(OnDestroy에서 정적 참조 해제). try/finally 후처리 보장.</summary>
        public void ClearNow()
        {
            try
            {
                if (_meshGO != null) Object.Destroy(_meshGO);
                if (_mesh != null) Object.Destroy(_mesh);
            }
            finally
            {
                _meshGO = null;
                _meshFilter = null;
                _meshRenderer = null;
                _mesh = null;
                _first = 0;
                _write = 0;
                _count = 0;
                Destroy(gameObject);   // 자가 파괴 — OnDestroy가 뒤처리(ResetHost 포함)
            }
        }

        /// <summary>콤보 스테이지 틴트 갱신 — 정점 색상 베이스(ResolveTint)에 즉시 반영.</summary>
        public void Tint(WeaponType type, int stage)
        {
            _type = type;
            _lastStage = Mathf.Clamp(stage, 0, 3);
        }

        // ================================================================
        // 프레임 갱신
        // ================================================================

        private void LateUpdate()
        {
            // 자가 가드(44차 선례) — 파괴 진행/비활성 상태에서 조용히 중단
            if (this == null || !gameObject || !gameObject.activeInHierarchy) return;

            float now = Time.time;
            float trailSec = Mathf.Max(0.05f, AttackArcVFX.TrailSeconds);

            // 1) 재질 확보 — 1순위 WeaponSwingTrail 검증 재질 재사용(클론 없이 공유), 미발견 시 경량 폴백,
            //    그래도 null이면 메시 갱신만 건너뛰고 샘플링은 지속(절대 예외 없음).
            if (_mat == null)
            {
                Material shared = WeaponSwingTrail.GetTrailMaterial();   // 공유(소유 아님 — 파괴 금지)
                if (shared != null) { _mat = shared; _ownsMat = false; }
                else { _mat = CreateFallbackMaterial(); _ownsMat = _mat != null; }   // [QA_fix] 폴백은 본인 소유 — 해제 책임
                if (_meshRenderer != null) _meshRenderer.sharedMaterial = _mat;   // null 대입도 안전(렌더러 비활성 유지)
            }

            // 2) 팁 샘플 수집 — Settle 후엔 동결(페이드아웃 진행). 부착 앵커 부재 시 플레이어 루트 폴백.
            if (!_settled)
            {
                GameObject tipGo = WeaponSwingTrail.GetTrailTipAnchor();
                Vector3 p = Vector3.zero;   // [QA_fix] 명시 초기화 — CS0165(used but not definitely assigned) 차단
                bool sampled = false;
                if (tipGo != null && tipGo.transform != null)
                {
                    p = tipGo.transform.position;   // 무기 팁(트레일 앵커) — 손 본 추적을 그대로 승계
                    sampled = true;
                }
                else if (_fallbackRoot != null)
                {
                    p = _fallbackRoot.position + Vector3.up * 1.2f;   // 흉부 높이 근사 폴백(루트는 발높이)
                    sampled = true;
                }
                if (sampled)
                {
                    WriteSample(p, now);
                    _lastSampleTime = now;
                }
            }

            // 3) 만료 샘플 트림(오래된 쪽부터) — 페이드는 수명 비율이 정점 색상 알파로 반영
            while (_count > 0 && now - _birth[_first] > trailSec)
            {
                _first = (_first + 1) % _cap;
                _count--;
            }

            // 4) 리본 메시 재구성 — 재질/샘플 충분할 때만(렌더러 토글은 프레임당 1회 대입)
            int quads = 0;
            if (_mat != null && _mesh != null && _count >= 2)
                quads = RebuildRibbon(now, trailSec);

            if (_meshRenderer != null)
                _meshRenderer.enabled = _mat != null && quads > 0;

            // 5) 활동 없음 자가 파괴 — 마지막 Swing/Settle 이후 페이드+유예 경과 시 스스로 소멸
            //    (씬에 흔적 없음 — 다음 Swing에서 EnsureHost가 재생성). 스케치의 "_lastSampleTime 기준" 대신
            //    스윙/세틀 기준 시각을 쓴다: 샘플링은 대기 중에도 계속되므로 _lastSampleTime은 항상 신선해
            //    페이드 완료 판정 기준으로 쓸 수 없음(설계 스케치 대비 유일 치환 — 상위 보고서 참조).
            float idleRef = _settled ? _settleTime : _lastActivityTime;   // [QA_fix] 미세틀 시 샘플 활동 기준 — 긴 콤보 스윙 중 소멸 방지
            if (now - idleRef > trailSec + IdleGraceSeconds)
            {
                if (_meshGO != null) Object.Destroy(_meshGO);
                if (_mesh != null) Object.Destroy(_mesh);
                _meshGO = null;
                _meshFilter = null;
                _meshRenderer = null;
                _mesh = null;
                Destroy(gameObject);
            }
        }

        // ================================================================
        // 내부: 샘플/메시 구축
        // ================================================================

        /// <summary>링버퍼 기록 — 만석 시 가장 오래된 슬롯을 덮어쓴다.</summary>
        private void WriteSample(Vector3 p, float now)
        {
            if (_cap <= 0 || _px == null) EnsureCapacity(Mathf.Max(8, AttackArcVFX.SampleCap));
            // [무번호 60] 속도 비례 알파 — 직전 샘플과의 거리/시간(결정론, 외부 난수 미사용). 첫 샘플은 0(기본 boost).
            if (_spd == null || _spd.Length != _cap) _spd = new float[Mathf.Max(1, _cap)];   // 수동 버퍼 주입(리플렉션 테스트) 호환 — 지연 할당
            if (_count > 0)
            {
                int prevIdx = (_write - 1 + _cap) % _cap;
                float dt = now - _birth[prevIdx];
                float dist = Vector3.Distance(new Vector3(_px[prevIdx], _py[prevIdx], _pz[prevIdx]), p);
                float speed = dt > 0.0001f ? dist / dt : 0f;
                _spd[_write] = speed;
            }
            else _spd[_write] = 0f;
            _px[_write] = p.x;
            _py[_write] = p.y;
            _pz[_write] = p.z;
            _birth[_write] = now;
            _lastActivityTime = now;   // [QA_fix] 샘플 기록 = 활동 유지(스윙 중 자가파괴 방지)
            _write = (_write + 1) % _cap;
            if (_count < _cap) _count++;
            else _first = (_first + 1) % _cap;   // 만석 — oldest 슬롯 덮어씀
        }

        /// <summary>
        /// 링버퍼(oldest→newest)를 스윕 스트립으로 재구성 — 샘플당 좌/우 2정점, 인접 샘플 간 쿼드(양권선 2중).
        /// 폭 = MaxHalfWidth×ArcScale×pow(1-age/T, WidthProfilePow=1.7)(전두엽 굵고 꼬리 가늘게 — [무번호 60] 강화), 알파 = 1-age/T×속도 boost.
        /// 미세 지터(구간 길이 미달) 쿼드는 스킵 — 0폭 깜빡임 방지.
        /// 반환: 구축된 쿼드 수. (프로젝트 관례 — mesh.vertices/.colors(Color[])/.triangles 배열 필드 직접 대입,
        ///  C# arraycopy 부재로 스윙 중 프레임당 정확 길이 배열 신규 할당 — 정점 극소.)
        ///
        /// [2026-09-25 Phase3] 이중 패스 합성 — ①글로우(넓게·흐리게, α×<see cref="GlowPassAlphaMul"/>) 선행 후 ②코어(꽉·밝게) 후행.
        /// 두 리본을 한 배열 버퍼에 누적해 '가장자리 발광' 톤을 만든다(같은 샘플 루프 EmitPass 재사용).
        /// [무번호 53-P2] 글로우 α 핀 0.30→0.15 절반 완화(additive 포화 완화) + UV 버퍼 할당/메시 uv 대입 추가.
        /// </summary>
        private int RebuildRibbon(float now, float trailSec)
        {
            if (_mesh == null || _cap <= 0) return 0;

            if (_count < 2) return 0;

            Color coreTint = ResolveTint();
            float halfW = Mathf.Max(0.01f, AttackArcVFX.MaxHalfWidth * Mathf.Max(0.01f, AttackArcVFX.ArcScale)
                * Mathf.Max(0.01f, AttackArcVFX.ArcRangeScale));   // [Phase3] 사거리 자동 비례 포함

            // 두 패스 누적 버퍼 — 글로우(전반)+코어(후반). 정점 ≤ 샘플×2×2, 삼각 ≤ (샘플-1)×12×2패스.
            // [QA_fix] tcap은 2패스×4삼각×3idx = 세그먼트당 24 — 이전 ×12는 2패스 때 OOB(치명). 
            // [53-P2] UV 버퍼 — 정점 커서(w[0])를 공유하므로 정점수=UV수 계약이 구조적으로 성립.
            int vcap = _count * 4;
            int tcap = (_count - 1) * 24;
            Vector3[] fArr = new Vector3[vcap];
            Color[] cArr = new Color[vcap];
            Vector2[] uvArr = new Vector2[vcap];
            int[] qArr = new int[tcap];
            int[] w = new int[2];   // [정점커서, 삼각커서]

            // 글로우 패스 — 넓게(×1.65)·흐리게(α×0.15 — [53-P2] 0.30의 절반, additive 포화 완화) — 코어의 외곽 미광.
            // 코어 패스 — 꽉·밝게. [무번호 57-P1] 코어 알파 상한 1.0→0.6 — 공격씬 10 실측(8.25s 크롭): strike
            //   지점 리본이 "지면에 깔린 불투명 흰 다이아몬드"로 렌더(코어 알파 1.0×백 틴트 = 알파블렌드에서도 단색 판).
            //   0.6 상한+57 텍스처 전단 졸업으로 밝은 슬래시 질감 유지·불투명 판 해소.
            // [무번호 60 갱신 79-R20] 글로우=코어→FXPalette.AttackPrimary 35% 보간 — 공격 이펙트 통일색 단일 소스(기존 Edge 폐기).
            Color glowTint = Color.Lerp(coreTint, FXPalette.AttackPrimary, 0.35f);
            EmitPass(now, trailSec, halfW, 1.65f, GlowPassAlphaMul, glowTint, fArr, cArr, qArr, uvArr, w);
            EmitPass(now, trailSec, halfW, 1.0f, CorePassAlphaMul, coreTint, fArr, cArr, qArr, uvArr, w);

            int vw = w[0];
            int tw = w[1];
            if (vw < 4 || tw < 6) return 0;

            // 정확 길이로 슬라이스 후 대입(C# arraycopy 부재 — 수동 복사, 극소량)
            Vector3[] vOut = new Vector3[vw];
            Color[] cOut = new Color[vw];
            Vector2[] uvOut = new Vector2[vw];
            for (int i = 0; i < vw; i++) { vOut[i] = fArr[i]; cOut[i] = cArr[i]; uvOut[i] = uvArr[i]; }
            int[] tOut = new int[tw];
            for (int i = 0; i < tw; i++) tOut[i] = qArr[i];

            // [무번호 45-RIBBON] 메시 배열 대입 순서 계약 — 축소 시(만료 샘플 트림) 정점을 먼저 대입하면
            // 기존 삼각형이 아직 더 큰 인덱스를 참조해 "Mesh.vertices is too small"+"colors out of bounds"가
            // 페이드아웃마다 발화(11-15 사용자 Play 실측 — AttackArcHost:RebuildRibbon 344/345행). 축소 시
            // 삼각형 먼저 → 정점 → 색 순서로 교체(확대 시 정점 먼저). 부분 갱신 순서만 정정 — 데이터 자체 무변경.
            // [53-P2] UV는 정점과 함께 대입(정점수=UV수 계약 — 축소/확대 양분기 동일).
            if (vOut.Length < _mesh.vertexCount)
            {
                _mesh.triangles = tOut;
                _mesh.vertices = vOut;
                _mesh.uv = uvOut;
                _mesh.colors = cOut;
            }
            else
            {
                _mesh.vertices = vOut;
                _mesh.uv = uvOut;
                _mesh.colors = cOut;
                _mesh.triangles = tOut;
            }
            _mesh.RecalculateBounds();
            return tw / 6;
        }

        /// <summary>
        /// 스윕 리본 한 소총 — 같은 샘플 루프로 정점/색/UV/삼각을 배열에 누적. widthMul/alphaMul로 글로우/코어 분기.
        /// 인덱스는 전역 정점 커서(w[0]) 기준 — 두 패스가 한 메시에 합쳐질 수 있도록 baseV 오프셋으로 삼각을 기록.
        /// [무번호 53-P2] UV 대입 — u=샘플 진행도(0=꼬리 → 1=전두엽, 텍스처 전두엽 밝음과 정합), v=좌0/우1.
        ///   UV는 정점 커서(w[0])를 공유 — 정점수=UV수 계약이 구조적으로 성립.
        /// </summary>
        private void EmitPass(float now, float trailSec, float halfW, float widthMul, float alphaMul,
            Color tint, Vector3[] vArr, Color[] cArr, int[] tArr, Vector2[] uvArr, int[] w)
        {
            int v = w[0];   // 이 패스 정점 시작(전역 인덱스) — prevBase/baseIdx가 전역 위치를 그대로 참조
            int t = w[1];
            int prevBase = -1;
            Vector3 prevPos = Vector3.zero;

            for (int k = 0; k < _count; k++)
            {
                int idx = (_first + k) % _cap;
                Vector3 pos = new Vector3(_px[idx], _py[idx], _pz[idx]);
                float life01 = 1f - Mathf.Clamp01((now - _birth[idx]) / trailSec);
                if (life01 <= 0f) continue;

                float width = Mathf.Max(0.001f, halfW * widthMul * Mathf.Pow(life01, WidthProfilePow));   // [무번호 60] 프로파일 지수 상수화

                Vector3 dir = SampleDir(k, idx);
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                if (side.sqrMagnitude > 1e-10f) { side.Normalize(); _lastRight = side; }
                else side = _lastRight;

                int baseIdx = v;
                vArr[v++] = pos - side * width;     // 좌 정점 — v=0(텍스처 하단 가장자리)
                vArr[v++] = pos + side * width;     // 우 정점 — v=1(텍스처 상단 가장자리)
                // [53-P2] UV 대입 — u=샘플 진행도(k=0 꼬리 → k=_count-1 전두엽), v=좌0/우1(계약).
                float u = _count <= 1 ? 1f : (float)k / (_count - 1);
                uvArr[baseIdx] = new Vector2(u, 0f);
                uvArr[baseIdx + 1] = new Vector2(u, 1f);
                // [무번호 60] 속도 비례 알파 — 팁 속도(RefTipSpeed 대비)로 boost. 하한 0.55(과소 노출 방지)·상한 1.15(과노출 방지).
                //   버퍼 부재(_spd 미할당 — 리플렉션 직접 진입) = 미측정 → 중립 boost 1.0(기존 알파 경로 무변경).
                float spd = (_spd != null && idx < _spd.Length) ? _spd[idx] : -1f;
                float speedBoost = spd < 0f ? 1f : Mathf.Clamp(0.55f + 0.45f * (spd / Mathf.Max(0.01f, AttackArcVFX.RefTipSpeed)), 0.55f, 1.15f);
                float a = Mathf.Clamp01(life01 * alphaMul * speedBoost);
                Color col = new Color(tint.r, tint.g, tint.b, a);
                cArr[baseIdx] = col;
                cArr[baseIdx + 1] = col;

                if (prevBase >= 0 && (pos - prevPos).sqrMagnitude >= MinSegmentLengthSq)
                {
                    int l0 = prevBase, r0 = prevBase + 1, l1 = baseIdx, r1 = baseIdx + 1;
                    tArr[t++] = l0; tArr[t++] = r0; tArr[t++] = r1;
                    tArr[t++] = l0; tArr[t++] = r1; tArr[t++] = l1;
                    tArr[t++] = l0; tArr[t++] = r1; tArr[t++] = r0;
                    tArr[t++] = l0; tArr[t++] = l1; tArr[t++] = r0;
                }

                prevBase = baseIdx;
                prevPos = pos;
            }

            w[0] = v;
            w[1] = t;
        }

        /// <summary>샘플 k의 진행방향 — 이웃 샘플 중앙차분(끝점은 한쪽 차분). 링 인덱스 랩 안전.</summary>
        private Vector3 SampleDir(int k, int idx)
        {
            if (k > 0 && k < _count - 1)
            {
                int ip = (_first + k - 1) % _cap;
                int inx = (_first + k + 1) % _cap;
                return new Vector3(_px[inx] - _px[ip], _py[inx] - _py[ip], _pz[inx] - _pz[ip]);
            }
            if (k < _count - 1)
            {
                int inx = (_first + k + 1) % _cap;
                return new Vector3(_px[inx] - _px[idx], _py[inx] - _py[idx], _pz[inx] - _pz[idx]);
            }
            int ip2 = (_first + k - 1 + _cap) % _cap;   // k == _count-1 — k>=1 보장(_count>=2 진입 조건)
            return new Vector3(_px[idx] - _px[ip2], _py[idx] - _py[ip2], _pz[idx] - _pz[ip2]);
        }

        /// <summary>
        /// 정점 색상 베이스 — 타입 기본색(WeaponSwingTrail Phase B 무기별 트레일 색상과 동기화) 후,
        /// 콤보 스테이지 오버라이드(1=흰/2=골드/3=주황 — SlashVFXRunner.ComboStageTint 계열 동기화).
        /// </summary>
        private Color ResolveTint()
        {
            Color c;
            switch (_type)
            {
                case WeaponType.Spear: c = new Color(0.3f, 0.6f, 1f, 1f); break;    // 청(찌르기)
                case WeaponType.Bow: c = new Color(1f, 0.9f, 0.3f, 1f); break;      // 황(원거리)
                case WeaponType.Fist: c = new Color(0.8f, 0.8f, 0.8f, 1f); break;   // 회(잽)
                default: c = Color.white; break;                                     // 검 — 흰(표준)
            }
            if (_lastStage >= 1 && _lastStage <= 3)
            {
                switch (_lastStage)
                {
                    case 1: c = new Color(1f, 1f, 1f, 1f); break;       // Stage 1: 흰
                    case 2: c = new Color(1f, 0.85f, 0.45f, 1f); break; // Stage 2: 골드
                    case 3: c = new Color(1f, 0.5f, 0.2f, 1f); break;   // Stage 3: 주황
                }
            }
            return c;
        }

        // ================================================================
        // 내부: 풀/용량/재질
        // ================================================================

        /// <summary>링버퍼 용량 확보 — Awake 1회 할당 원칙(용량 상정 변경 시에만 재할당, 평시 no-op).</summary>
        private void EnsureCapacity(int cap)
        {
            cap = Mathf.Max(8, cap);
            if (_px != null && _cap >= cap) return;
            _px = new float[cap];
            _py = new float[cap];
            _pz = new float[cap];
            _birth = new float[cap];
            _spd = new float[cap];   // [무번호 60] 속도 버퍼 동시 할당
            _cap = cap;
            _write = 0;
            _first = 0;
            _count = 0;   // 용량 변경은 튜닝 중 이벤트 — 리본은 다음 스윙 샘플부터 재구축
        }

        /// <summary>메시 풀 확보 — 부분 파손 상태면 통째로 재생성(정점은 월드좌표 직접 베이크, GO는 항등 트랜스폰).</summary>
        private void EnsureMesh()
        {
            if (_meshGO != null && _meshFilter != null && _meshRenderer != null && _mesh != null) return;

            if (_meshGO != null) Object.Destroy(_meshGO);   // 부분 파손 정리 후 재생성
            if (_mesh != null) Object.Destroy(_mesh);

            _meshGO = new GameObject("AttackArcRibbon") { hideFlags = HideFlags.HideAndDontSave };
            _meshGO.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _meshGO.transform.localScale = Vector3.one;
            _meshFilter = _meshGO.AddComponent<MeshFilter>();
            _meshRenderer = _meshGO.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            _mesh = new Mesh();
            _meshFilter.sharedMesh = _mesh;
            _mat = null;   // 렌더러 교체 — 다음 LateUpdate에서 재질 재바인딩
        }

        /// <summary>
        /// 경량 폴백 머티리얼 — WeaponSwingTrail 재질 미발견(트레일 미부착 등) 시에만 1회 생성.
        /// WeaponSwingTrail.CreateTrailMaterial과 동일 우선순위(URP Particles/Unlit → Sprites/Default)의
        /// 최소 복제 — 정점 색상 곱셈으로 알파 페이드/틴트가 그대로 표현된다.
        /// </summary>
        private static Material CreateFallbackMaterial()
        {
            // WeaponSwingTrail.CreateTrailMaterial과 동일 사양(완전수식 심볼 + Emission) — 컴파일 안전·발광 지원.
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpUnlit != null)
            {
                var mat = new Material(urpUnlit);
                // [무번호 54-P1] Additive → Alpha 블렌드 계약 진화 — 공격씬 7·8 실측: 밝은 지면(녹색 ~0.7) 위
                //   additive 흰 코어(α1.0)는 지면+흰 > 1.0 클램프로 전 폭 순백 포화 → 53차 슬래시 텍스처의
                //   페더가 시각적으로 마스킹되어 "텍스처 없는 불투명 흰 덩어리"로 읽혔다(비전 판독 일치).
                //   Alpha 블렌드(SrcAlpha/OneMinusSrcAlpha)로 전환해 텍스처 페더·글로우 α계층이 실제로 보이게 한다.
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);           // Transparent
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);               // Alpha (구 Additive=2)
                mat.SetOverrideTag("RenderType", "Transparent");
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.white);
                    mat.EnableKeyword("_EMISSION");
                }
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AttackArcVFX.BindSlashTexture(mat);   // [무번호 53-P2] 슬래시 텍스처 바인딩(_BaseMap/_MainTex) — additive 강도 조형
                return mat;
            }
            Shader spriteDefault = Shader.Find("Sprites/Default");
            var fallbackMat = spriteDefault != null ? new Material(spriteDefault) : null;
            AttackArcVFX.BindSlashTexture(fallbackMat);   // [53-P2] 폴백도 텍스처 바인딩(_MainTex) — 텍스처 없어도 마젠타 아님(계약 유지)
            return fallbackMat;
        }

        // ================================================================
        // 후처리
        // ================================================================

        private void OnDestroy()
        {
            try
            {
                if (_meshGO != null) Object.Destroy(_meshGO);
                if (_mesh != null) Object.Destroy(_mesh);   // 런타임 생성 Mesh는 자동 파괴 대상이 아님 — 명시 해제
                _meshGO = null;
                _meshFilter = null;
                _meshRenderer = null;
                _mesh = null;
                if (_ownsMat && _mat != null) Object.Destroy(_mat);   // [QA_fix] 폴백 재질은 본인 소유 — 해제
                _ownsMat = false;
                _mat = null;                                // 공유 재질 — 소유 아님(파괴하지 않고 참조만 해제)
                _px = null;
                _py = null;
                _pz = null;
                _birth = null;
            }
            finally
            {
                AttackArcVFX.ResetHost();   // 정적 참조 해제 — 다음 Swing에서 재생성
            }
        }
    }
}