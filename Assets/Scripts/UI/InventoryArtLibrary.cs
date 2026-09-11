using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 인벤토리 AAA 아트 라이브러리 — 절차 텍스처 정적 캐시.
    /// 모든 텍스처는 최초 요청 시 1회 생성 후 static 필드에 캐시(프레임마다 생성 금지).
    /// 노이즈는 고정시드 정수 해시(LCG) 기반 결정론 value noise/fBm — UnityEngine.Random 미사용.
    /// 텍스처 파기 금지(static 캐시 — 도메인 리로드 시 자동 정리).
    /// 소비 전제: Backplate=border 24 등소비 / MetalFrame=border(16,16,16,16) / SlotCell=순백 테두리 tint.
    /// </summary>
    public static class InventoryArtLibrary
    {
        // ===================================================================
        // 희귀도 색상 (인덱스 0~4 = Common/Uncommon/Rare/Epic/Legendary — 계약 고정)
        // ===================================================================
        /// <summary>희귀도 틴트 팔레트 — RarityColors[(int)ItemRarity] 로 소비.</summary>
        public static readonly Color[] RarityColors =
        {
            new Color(0.72f, 0.74f, 0.76f, 1f),   // [0] Common    — 회백
            new Color(0.35f, 0.78f, 0.42f, 1f),   // [1] Uncommon  — 녹
            new Color(0.30f, 0.55f, 0.95f, 1f),   // [2] Rare      — 청
            new Color(0.68f, 0.38f, 0.92f, 1f),   // [3] Epic      — 보라
            new Color(0.95f, 0.78f, 0.30f, 1f),   // [4] Legendary — 골드
            new Color(0.40f, 0.92f, 0.95f, 1f),   // [5] Unique — 계약 외 안전분 (ItemRarity.Unique=5 인덱스 초과 방지)
        };

        // ===== 2026-09-11: Flat 모드 (인벤토리 예시 2 — 플랫 다크 네이비) =====
        /// <summary>아트 스타일 모드. Flat = 플랫 모던(다크 네이비 + 얇은 회백 보더 + 스카이블루 강조). 기본 Flat.</summary>
        public enum ArtStyleMode { Medieval, Flat }
        /// <summary>현재 스타일 모드 — 최초 Get* 호출 전에만 변경 가능(캐시 파기 금지 관례).</summary>
        public static ArtStyleMode Style = ArtStyleMode.Flat;

        // ===== Flat 모드 지연 생성 static 캐시 (파기 금지) =====
        private static Texture2D _flatBackplate;
        private static Texture2D _flatFrame;
        private static Texture2D _flatTitleStrip;
        private static Texture2D _flatSlotCell;
        private static Texture2D _flatSlotBorder;
        private static Texture2D _flatSlotHighlight;
        private static Texture2D _flatEmpty;    // corner ornament 대체 (완전 투명)

        // ===== 지연 생성 static 캐시 (파기 금지) =====
        private static Texture2D _backplate;
        private static Texture2D _metalFrame;
        private static Texture2D _cornerOrnament;
        private static Texture2D _titleBanner;
        private static Texture2D _slotCell;
        private static Texture2D _slotGlow;
        private static Texture2D _slotHighlight;
        private static Texture2D _dropShadow;

        // ===================================================================
        // 공개 API — 시그니처 절대 변경 금지 (다른 에이전트가 동일 시그니처 사용)
        // ===================================================================

        /// <summary>256×256 스톤 백플레이트 — SDF 라운드 코너(18px) + 다크 스톤 + 세로 그라디언트 + fBm 질감 + 중앙 마법진. 9-Slice border 24 전제.</summary>
        public static Texture2D GetBackplate()
        {
            if (Style == ArtStyleMode.Flat)
            {
                if (_flatBackplate == null) _flatBackplate = BuildFlatPanel();
                return _flatBackplate;
            }
            if (_backplate == null) _backplate = BuildBackplate();
            return _backplate;
        }

        /// <summary>256×256 금속 프레임 — 두께 14px, 외곽 하이라이트→내측 섀도우 그라디언트 + 스크래치 노이즈. border(16,16,16,16) 전제.</summary>
        public static Texture2D GetMetalFrame()
        {
            if (Style == ArtStyleMode.Flat)
            {
                if (_flatFrame == null) _flatFrame = BuildFlatFrame();
                return _flatFrame;
            }
            if (_metalFrame == null) _metalFrame = BuildMetalFrame();
            return _metalFrame;
        }

        /// <summary>96×96 좌상단 기준 로터스/필리그리 코너 장식 — 소비자가 GUI.matrix로 회전 배치.</summary>
        public static Texture2D GetCornerOrnament()
        {
            if (Style == ArtStyleMode.Flat)
            {
                if (_flatEmpty == null) _flatEmpty = MakeCanvas(96, 96, "InvArt_FlatEmpty");
                return _flatEmpty;
            }
            if (_cornerOrnament == null) _cornerOrnament = BuildCornerOrnament();
            return _cornerOrnament;
        }

        /// <summary>512×96 금속 타이틀 배너 — 중앙 볼록 곡률 + 좌우 테이퍼 + 양끝 리벳 + 세로 스트릭 노이즈.</summary>
        public static Texture2D GetTitleBanner()
        {
            if (Style == ArtStyleMode.Flat)
            {
                if (_flatTitleStrip == null) _flatTitleStrip = BuildFlatTitleStrip();
                return _flatTitleStrip;
            }
            if (_titleBanner == null) _titleBanner = BuildTitleBanner();
            return _titleBanner;
        }

        /// <summary>72×72 엠보싱 인너섀도우 슬롯 셀 — 내부 어둡고 위/왼 어두움·아래/오른 밝음(움푹 파임), 순백 2px 테두리(tint 전제), 라운드 10px.</summary>
        public static Texture2D GetSlotCell()
        {
            if (Style == ArtStyleMode.Flat)
            {
                if (_flatSlotCell == null) _flatSlotCell = BuildFlatSlotCell();
                return _flatSlotCell;
            }
            if (_slotCell == null) _slotCell = BuildSlotCell();
            return _slotCell;
        }

        /// <summary>72×72 방사형 글로우 — 중심 알파 0.55→가장자리 0, 순백(희귀도 색 tint 전용).</summary>
        public static Texture2D GetSlotGlow()
        {
            if (Style == ArtStyleMode.Flat)
            {
                if (_flatSlotBorder == null) _flatSlotBorder = BuildFlatSlotBorder();
                return _flatSlotBorder;
            }
            if (_slotGlow == null) _slotGlow = BuildSlotGlow();
            return _slotGlow;
        }

        /// <summary>72×72 라운드 테두리 하이라이트 — 내부 투명, 순백 3px + 외곽 소프트(호버/선택 tint 전용).</summary>
        public static Texture2D GetSlotHighlight()
        {
            if (Style == ArtStyleMode.Flat)
            {
                if (_flatSlotHighlight == null) _flatSlotHighlight = BuildFlatSlotHighlight();
                return _flatSlotHighlight;
            }
            if (_slotHighlight == null) _slotHighlight = BuildSlotHighlight();
            return _slotHighlight;
        }

        /// <summary>128×128 사각 소프트 드롭섀도우 — 중앙 알파 ~0.5→가장자리 0(가우시안 유사), 창 뒤 그림자 tint용.</summary>
        public static Texture2D GetWindowDropShadow()
        {
            if (_dropShadow == null) _dropShadow = BuildWindowDropShadow();
            return _dropShadow;
        }

        // ===================================================================
        // 텍스처 빌더 (RGBA32 2D — 최초 1회만 실행)
        // ===================================================================

        /// <summary>스톤 백플레이트 생성 — 코너 페더 2px, 그라디언트 ±8%, fBm ±4%, 마법진 ≤±4%, 내측 다크 아웃라인.</summary>
        private static Texture2D BuildBackplate()
        {
            const int S = 256;
            const float half = S * 0.5f;
            const float corner = 18f;
            var tex = MakeCanvas(S, S, "InvArt_Backplate");
            var buf = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;

                    // ① SDF 라운드 사각(반경 18px) 밖은 페더 2px로 알파 소멸
                    float d = SdfRoundRect(px, py, half, half, corner);
                    float alpha = Mathf.Clamp01(0.5f - d * 0.5f);
                    if (alpha <= 0f) { buf[i] = Color.clear; continue; }

                    // ② 다크 스톤 베이스 + 상→하 세로 그라디언트 (위 +8% 밝게 / 아래 −8%)
                    //    (텍스처 y=S-1 = GUI 상단)
                    float grad = Mathf.Lerp(0.92f, 1.08f, y / (float)(S - 1));
                    var c = new Color(0.12f * grad, 0.11f * grad, 0.10f * grad, 0.95f);

                    // ③ fBm 3옥타브 스톤 노이즈 (대비 ±4%, 낮은 주파수 — 9-Slice 중앙 늘어남 대비)
                    float n = (Fbm3(x / 56f, y / 56f) - 0.5f) * 2f;   // -1..1 근사
                    float tone = 1f + n * 0.04f;
                    c.r *= tone; c.g *= tone; c.b *= tone;

                    // ④ 중앙부 은은한 동심원 마법진 — 2겹 원 + 8방위 룬 틱 (대비 ±4% 이하)
                    float r = Mathf.Sqrt(px * px + py * py);
                    float ring = Mathf.Max(RingMask(r, 58f, 4f), RingMask(r, 86f, 3f));
                    float ang = Mathf.Atan2(py, px);
                    float tick = TickMask(ang) * BandMask(r, 66f, 82f);
                    float magic = Mathf.Clamp01(ring * 0.8f + tick * 0.6f) * 0.04f;
                    c.r += magic; c.g += magic * 0.92f; c.b += magic * 0.75f;

                    // ⑤ 모서리 안쪽 1px 다크 아웃라인
                    float dIn = SdfRoundRect(px, py, half - 1f, half - 1f, corner - 1f);
                    if (Mathf.Abs(dIn) <= 0.7f)
                    {
                        c.r *= 0.5f; c.g *= 0.5f; c.b *= 0.5f;
                    }

                    c.a *= alpha;
                    buf[i] = c;
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>금속 프레임 생성 — 두께 14px, 코너 볼록(내측 라운드 축소), 수평 스트로크 노이즈 ±6%, 내측 1px 진한 아웃라인.</summary>
        private static Texture2D BuildMetalFrame()
        {
            const int S = 256;
            const float half = S * 0.5f;
            const float thick = 14f;
            var tex = MakeCanvas(S, S, "InvArt_MetalFrame");
            var buf = new Color[S * S];
            var hi = new Color(1.00f, 0.86f, 0.52f);   // 외곽 하이라이트
            var mid = new Color(0.72f, 0.58f, 0.24f);  // 베이스 골드
            var lo = new Color(0.42f, 0.32f, 0.14f);   // 내측 섀도우

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;

                    // 프레임 밖은 완전 투명 (1px 페더 AA)
                    float dOut = SdfRoundRect(px, py, half, half, 14f);
                    if (dOut > -0.5f) { buf[i] = Color.clear; continue; }

                    // 내측은 투명 (0.5px 페더 AA — 내측 경계 부드럽게)
                    float dIn = SdfRoundRect(px, py, half - thick, half - thick, 4f);
                    if (dIn < -0.5f) { buf[i] = Color.clear; continue; }

                    // 금속 그라디언트: 외곽 하이라이트 → 베이스 → 내측 섀도우
                    float t = Mathf.Clamp01(dIn / thick);   // 0=내측, 1=외곽
                    Color c = t < 0.5f ? Color.Lerp(lo, mid, t * 2f)
                                       : Color.Lerp(mid, hi, (t - 0.5f) * 2f);

                    // 노이즈 스크래치 — 수평 스트로크 노이즈 ±6% (행 해시 + 가로로 긴 fBm)
                    float stroke = (Hash01(7, y) - 0.5f) * 0.8f + (Fbm3(x / 34f, y / 5f + 13f) - 0.5f);
                    float tone = 1f + stroke * 0.075f;
                    c.r *= tone; c.g *= tone; c.b *= tone;

                    // 내측 1px 진한 아웃라인
                    if (dIn < 1.2f)
                    {
                        c.r *= 0.42f; c.g *= 0.40f; c.b *= 0.35f;
                    }

                    c.a = Mathf.Clamp01(-dOut + 0.5f) * Mathf.Clamp01(dIn + 0.5f);   // 외곽+내측 AA
                    buf[i] = c;
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>코너 장식 생성 — 중심(좌상단)에서 뻗는 2개 호 + 대칭 필리그리 곡선 + 끝점 원형 스터드 + 금색 그라디언트/하이라이트.</summary>
        private static Texture2D BuildCornerOrnament()
        {
            const int S = 96;
            var tex = MakeCanvas(S, S, "InvArt_CornerOrnament");
            var buf = new Color[S * S];

            var goldNear = new Color(1.00f, 0.88f, 0.55f);   // 중심(밝은 금)
            var goldFar = new Color(0.62f, 0.45f, 0.16f);    // 끝(깊은 금)
            const float a0 = 0.28f, a1 = 1.29f;              // 대각선(↘) 부채꼴 각도 창 (rad, y-down)

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    // 좌상단 코너를 원점으로 (px: 오른쪽+, py: 아래+)
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float r = Mathf.Sqrt(px * px + py * py);

                    // ① 중심에서 뻗는 2개 호 (반경 30 / 44)
                    float m = ArcMask(px, py, 0f, 0f, 30f, 1.6f, a0, a1);
                    m = Mathf.Max(m, ArcMask(px, py, 0f, 0f, 44f, 1.3f, a0 + 0.10f, a1 - 0.10f));

                    // ② 대칭 필리그리 곡선 — 대각선에 평행하게 볼록한 호 (중심 62,62 / 반경 46)
                    m = Mathf.Max(m, ArcMask(px, py, 62f, 62f, 46f, 1.1f, Mathf.PI * 1.08f, Mathf.PI * 1.42f));

                    // ③ 끝점 원형 스터드 + 코너 뿌리 스터드
                    m = Mathf.Max(m, DiscMask(px, py, 30f * Mathf.Cos(a0), 30f * Mathf.Sin(a0), 3.6f));
                    m = Mathf.Max(m, DiscMask(px, py, 30f * Mathf.Cos(a1), 30f * Mathf.Sin(a1), 3.6f));
                    m = Mathf.Max(m, DiscMask(px, py, 44f * Mathf.Cos(a0 + 0.10f), 44f * Mathf.Sin(a0 + 0.10f), 3.0f));
                    m = Mathf.Max(m, DiscMask(px, py, 44f * Mathf.Cos(a1 - 0.10f), 44f * Mathf.Sin(a1 - 0.10f), 3.0f));
                    m = Mathf.Max(m, DiscMask(px, py, 5f, 5f, 5.2f));

                    if (m <= 0f) { buf[i] = Color.clear; continue; }

                    // 금색 그라디언트 (중심 밝게 → 끝 깊게)
                    var c = Color.Lerp(goldNear, goldFar, Mathf.Clamp01(r / 52f));

                    // 미세 하이라이트 — 호 라인 중심(|r - R| < 1.2px)
                    float hl = Mathf.Max(RimHighlight(px, py, 30f), RimHighlight(px, py, 44f));
                    c = Color.Lerp(c, new Color(1f, 0.97f, 0.86f), hl * 0.45f);

                    c.a = m;   // 프리멀티 없이 소프트 알파 엣지
                    buf[i] = c;
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>타이틀 배너 생성 — 중앙 볼록(상하 곡률) + 좌우 테이퍼 + 양끝 리벳(반경 6px + 스페큘러 점) + 세로 스트릭 노이즈.</summary>
        private static Texture2D BuildTitleBanner()
        {
            const int W = 512, H = 96;
            var tex = MakeCanvas(W, H, "InvArt_TitleBanner");
            var buf = new Color[W * H];
            var hi = new Color(1.00f, 0.86f, 0.52f);
            var mid = new Color(0.72f, 0.58f, 0.24f);
            var lo = new Color(0.40f, 0.30f, 0.13f);

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float u = x / (float)(W - 1);
                    float px = x + 0.5f, py = y + 0.5f;

                    // 좌우 테이퍼(끝 52% 두께) + 상하 곡률(중앙이 위로 볼록)
                    float taper = 0.52f + 0.48f * Mathf.Sin(Mathf.PI * u);
                    float bow = 5f * Mathf.Sin(Mathf.PI * u);
                    float midY = H * 0.55f - bow;
                    float halfH = (H * 0.5f - 5f) * taper;
                    float top = midY - halfH;
                    float bottom = midY + halfH;

                    // 배너 커버리지 (1px 소프트 엣지)
                    float cov = Mathf.Clamp01(Mathf.Min(py - top, bottom - py) + 0.5f);
                    if (cov <= 0f) { buf[i] = Color.clear; continue; }

                    // 금속 세로 그라디언트 (텍스처 y=H-1 = GUI 상단 → 하이라이트)
                    float v = y / (float)(H - 1);
                    Color c;
                    if (v > 0.6f) c = Color.Lerp(mid, hi, (v - 0.6f) / 0.4f);
                    else if (v > 0.2f) c = Color.Lerp(lo, mid, (v - 0.2f) / 0.4f);
                    else c = lo;

                    // 세로 스트릭 노이즈 (x 고주파 / y 저주파) ±6%
                    float streak = (Hash01(x, 3) - 0.5f) * 0.6f + (Fbm3(x / 7f, y / 46f + 5f) - 0.5f);
                    float tone = 1f + streak * 0.09f;
                    c.r *= tone; c.g *= tone; c.b *= tone;

                    // 양끝 리벳 — 반경 6px 원 + 스페큘러 하이라이트 점(좌상단)
                    float rv = Mathf.Max(DiscMask(px, py, 22f, midY, 6f),
                                         DiscMask(px, py, W - 22f, midY, 6f));
                    if (rv > 0f)
                    {
                        var rc = Color.Lerp(new Color(0.90f, 0.76f, 0.42f), new Color(0.45f, 0.34f, 0.15f), 1f - rv);
                        float spec = DiscMask(px, py, 20f, midY + 2f, 1.8f);   // GUI 좌상단 하이라이트(텍스처 y+)
                        rc = Color.Lerp(rc, new Color(1f, 0.98f, 0.9f), spec * 0.85f);
                        c = Color.Lerp(c, rc, Mathf.Clamp01(rv + 0.3f));
                    }

                    c.a = cov;
                    buf[i] = c;
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>슬롯 셀 생성 — 내부 다크 + 엠보싱(위/왼 어둡게·아래/오른 밝게) + 순백 2px 테두리 + 라운드 10px.</summary>
        private static Texture2D BuildSlotCell()
        {
            const int S = 72;
            const float half = S * 0.5f;
            const float corner = 10f;
            var tex = MakeCanvas(S, S, "InvArt_SlotCell");
            var buf = new Color[S * S];
            var inner = new Color(0.08f, 0.08f, 0.10f, 0.85f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float d = SdfRoundRect(px, py, half, half, corner);

                    if (d > 0.5f) { buf[i] = Color.clear; continue; }

                    // 순백 2px 테두리 (tint 소비 전제 — 외곽 1px AA)
                    if (d > -2.5f)
                    {
                        buf[i] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                        continue;
                    }

                    // 내부 — 엠보싱 인너섀도우: GUI 위/왼쪽 에지 어둡게, 아래/오른쪽 에지 밝게 (움푹 파인 느낌)
                    // (텍스처 y=S-1 = GUI 상단)
                    float ex = x, ey = y;
                    float darkDist = Mathf.Min(ex, (S - 1) - ey);       // GUI 위/왼쪽까지 거리
                    float lightDist = Mathf.Min((S - 1) - ex, ey);      // GUI 아래/오른쪽까지 거리
                    float tone = 1f - Mathf.Clamp01(1f - darkDist / 9f) * 0.45f
                               + Mathf.Clamp01(1f - lightDist / 9f) * 0.35f;
                    var c = new Color(inner.r * tone, inner.g * tone, inner.b * tone, inner.a);

                    // 라운드 코너 안쪽 살짝 어둡게(섀도우 라인)
                    float dSoft = Mathf.Clamp01(-d / 4f);
                    c = Color.Lerp(new Color(0f, 0f, 0f, inner.a), c, dSoft);

                    buf[i] = c;
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>슬롯 글로우 생성 — 중심 알파 0.55 → 가장자리 0 방사형 falloff, 순백(희귀도 tint 전용).</summary>
        private static Texture2D BuildSlotGlow()
        {
            const int S = 72;
            const float half = S * 0.5f;
            var tex = MakeCanvas(S, S, "InvArt_SlotGlow");
            var buf = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float a = 0.55f * Mathf.Pow(Mathf.Clamp01(1f - r), 1.7f);
                    buf[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>슬롯 하이라이트 생성 — 내부 투명 라운드 테두리(순백 3px) + 외곽 소프트 폴백.</summary>
        private static Texture2D BuildSlotHighlight()
        {
            const int S = 72;
            const float half = S * 0.5f;
            const float corner = 10f;
            var tex = MakeCanvas(S, S, "InvArt_SlotHighlight");
            var buf = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float d = SdfRoundRect(px, py, half, half, corner);

                    float a;
                    if (d <= -3.5f) a = 0f;                          // 내부 완전 투명
                    else if (d <= -2.5f) a = (d + 3.5f);             // 내측 소프트 램프업
                    else if (d <= 0f) a = 1f;                        // 순백 3px 코어
                    else if (d <= 2f) a = 1f - d / 2f;               // 외곽 소프트
                    else a = 0f;

                    buf[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>창 드롭섀도우 생성 — 에지 거리 기반 가우시안 유사 falloff (중심 알파 ~0.5 → 테두리 0).</summary>
        private static Texture2D BuildWindowDropShadow()
        {
            const int S = 128;
            const float half = S * 0.5f;
            var tex = MakeCanvas(S, S, "InvArt_DropShadow");
            var buf = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    // 가장 가까운 테두리까지 거리 (0=테두리, half=중앙)
                    float dEdge = Mathf.Min(Mathf.Min(px + half, half - px), Mathf.Min(py + half, half - py));
                    float u = dEdge / half;
                    // 가우시안 유사 falloff
                    float a = 0.5f * Mathf.Exp(-3.5f * (1f - u) * (1f - u));
                    buf[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        // ===================================================================
        // Flat 모드 텍스처 빌더 (2026-09-11 — 인벤토리 예시 2 스타일 토큰)
        //   배경 다크 네이비 반투명 RGBA(16,22,34,0.88) / 테두리 밝은 회백 2px 라운드
        //   강조 스카이블루 (0.35,0.65,0.90) / 슬롯 짙은 반투명 정사각 + 등급색 보더
        //   결정론(SDF)만 사용 — Random 미사용 관례 유지. static 캐시 파기 금지.
        // ===================================================================

        /// <summary>Flat 패널 — 다크 네이비 반투명 + 2px 회백 보더, 라운드 12px. 9-Slice border 24 전제.</summary>
        private static Texture2D BuildFlatPanel()
        {
            const int S = 256;
            const float half = S * 0.5f;
            const float corner = 12f;
            var tex = MakeCanvas(S, S, "InvArt_FlatPanel");
            var buf = new Color[S * S];
            var fill = new Color(16f / 255f, 22f / 255f, 34f / 255f, 0.88f);
            var border = new Color(0.62f, 0.70f, 0.78f, 0.92f);   // 밝은 회백

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float d = SdfRoundRect(px, py, half, half, corner);
                    if (d > 1f) { buf[i] = Color.clear; continue; }

                    float aa = Mathf.Clamp01(0.5f - d);
                    // 보더: 외곽 2px (9-Slice 코너/엣지에서 두께 보존)
                    if (d > -2.5f)
                        buf[i] = new Color(border.r, border.g, border.b, border.a * aa);
                    else
                        buf[i] = new Color(fill.r, fill.g, fill.b, fill.a * aa);
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>Flat 프레임 — 중앙 투명 + 2px 회백 라운드 보더. border(16,16,16,16) 9-Slice 전제.</summary>
        private static Texture2D BuildFlatFrame()
        {
            const int S = 256;
            const float half = S * 0.5f;
            const float corner = 12f;
            var tex = MakeCanvas(S, S, "InvArt_FlatFrame");
            var buf = new Color[S * S];
            var border = new Color(0.62f, 0.70f, 0.78f, 0.95f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float d = SdfRoundRect(px, py, half, half, corner);
                    // 중앙 투명, 외곽 2px 보더만
                    if (d <= -2.5f || d > 1f) { buf[i] = Color.clear; continue; }
                    float aa = Mathf.Clamp01(0.5f - d);
                    buf[i] = new Color(border.r, border.g, border.b, border.a * aa);
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>Flat 타이틀 스트립 — 얇은 상단 스트립(짙은 네이비 반투명 + 하단 2px 스카이블루 라인). 9-Slice 미사용(Stretch).</summary>
        private static Texture2D BuildFlatTitleStrip()
        {
            const int W = 512, H = 96;
            var tex = MakeCanvas(W, H, "InvArt_FlatTitleStrip");
            var buf = new Color[W * H];
            var strip = new Color(0.055f, 0.078f, 0.125f, 0.72f);   // 패널보다 살짝 어두운 네이비
            var accent = new Color(0.35f, 0.65f, 0.90f, 0.95f);     // 하단 스카이블루 액센트 라인

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    // 텍스처 y=0이 GUI 하단 → 하단 2px = y 0..1 에 액센트
                    if (y < 2)
                        buf[i] = accent;
                    else
                        buf[i] = strip;
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>Flat 슬롯 셀 — 짙은 반투명 다크 네이비 정사각, 라운드 8px, 보더 없음(등급색 보더는 GetSlotGlow tint).</summary>
        private static Texture2D BuildFlatSlotCell()
        {
            const int S = 72;
            const float half = S * 0.5f;
            const float corner = 8f;
            var tex = MakeCanvas(S, S, "InvArt_FlatSlotCell");
            var buf = new Color[S * S];
            var fill = new Color(0.045f, 0.075f, 0.125f, 0.66f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int i = y * S + x;
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float d = SdfRoundRect(px, py, half, half, corner);
                    if (d > 0.5f) { buf[i] = Color.clear; continue; }
                    buf[i] = new Color(fill.r, fill.g, fill.b, fill.a * Mathf.Clamp01(0.5f - d));
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>Flat 슬롯 보더 — 2px 라운드 링 순백(등급색 tint 전용 → RarityColors 소비).</summary>
        private static Texture2D BuildFlatSlotBorder()
        {
            const int S = 72;
            const float half = S * 0.5f;
            const float corner = 8f;
            var tex = MakeCanvas(S, S, "InvArt_FlatSlotBorder");
            var buf = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float d = SdfRoundRect(px, py, half, half, corner);
                    float a;
                    if (d <= -2.5f) a = 0f;                  // 내부 투명
                    else if (d <= -1.5f) a = d + 2.5f;       // 내측 램프업
                    else if (d <= 0f) a = 1f;                // 2px 코어
                    else if (d <= 1f) a = 1f - d;            // 외곽 소프트
                    else a = 0f;
                    buf[(y * S) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        /// <summary>Flat 슬롯 하이라이트 — 내부 투명 2px 라운드 링(호버/선택 tint 전용, 스카이블루 소비 전제).</summary>
        private static Texture2D BuildFlatSlotHighlight()
        {
            const int S = 72;
            const float half = S * 0.5f;
            const float corner = 8f;
            var tex = MakeCanvas(S, S, "InvArt_FlatSlotHighlight");
            var buf = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float d = SdfRoundRect(px, py, half, half, corner);
                    float a;
                    if (d <= -2.5f) a = 0f;
                    else if (d <= -1.5f) a = d + 2.5f;
                    else if (d <= 0f) a = 1f;
                    else if (d <= 2f) a = 1f - d / 2f;
                    else a = 0f;
                    buf[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        // ===================================================================
        // private 헬퍼 — 캔버스/SDF/결정론 노이즈
        // ===================================================================

        /// <summary>RGBA32 2D 캔버스 생성 (Bilinear + Clamp 공통 설정).</summary>
        private static Texture2D MakeCanvas(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return tex;
        }

        /// <summary>텍스처 확정 적용 (mipmap 없음).</summary>
        private static void Apply(Texture2D tex)
        {
            tex.Apply(false, false);
        }

        /// <summary>
        /// SDF 라운드 사각 거리 함수 — 중심 원점 좌표(px, py)에서
        /// 사각형 경계까지의 부호 거리(음수=내부). 라운드 코너/페더 계산 공용.
        /// </summary>
        private static float SdfRoundRect(float px, float py, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(px) - (halfW - radius);
            float qy = Mathf.Abs(py) - (halfH - radius);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>
        /// 결정론 정수 해시 — 고정시드 LCG 스타일 (UnityEngine.Random 미사용).
        /// x*374761393 + y*668265263 믹싱 → 0..1 반환.
        /// </summary>
        private static float Hash01(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }

        /// <summary>결정론 value noise — 격자 해시의 smoothstep 보간.</summary>
        private static float ValueNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3f - 2f * fx);   // smoothstep
            float sy = fy * fy * (3f - 2f * fy);
            float n00 = Hash01(ix, iy);
            float n10 = Hash01(ix + 1, iy);
            float n01 = Hash01(ix, iy + 1);
            float n11 = Hash01(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        /// <summary>결정론 fBm — 3옥타브 합성 (0..1 정규화).</summary>
        private static float Fbm3(float x, float y)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int o = 0; o < 3; o++)
            {
                sum += ValueNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        /// <summary>동심원 링 마스크 (반경 radius, 반경 폭 halfW, 1px 소프트).</summary>
        private static float RingMask(float r, float radius, float halfW)
        {
            return Mathf.Clamp01(1f - Mathf.Abs(r - radius) / halfW);
        }

        /// <summary>방사형 밴드 마스크 (r0~r1 사이, 3px 소프트 엣지).</summary>
        private static float BandMask(float r, float r0, float r1)
        {
            return Mathf.Clamp01(Mathf.Min(r - r0, r1 - r) / 3f + 0.5f);
        }

        /// <summary>8방위 룬 틱 마스크 — 45°마다 가느다란 방사 틱.</summary>
        private static float TickMask(float ang)
        {
            const float step = Mathf.PI / 4f;
            float phase = Mathf.Repeat(ang, step) / step;          // 섹터 내 위상 0..1
            float centerDist = Mathf.Abs(phase - 0.5f) * 2f;       // 0=섹터 중심
            return Mathf.Clamp01(1f - centerDist * 6f);
        }

        /// <summary>원판 마스크 — 중심(cx,cy) 반경 radius, 1px 소프트 엣지.</summary>
        private static float DiscMask(float px, float py, float cx, float cy, float radius)
        {
            float dx = px - cx, dy = py - cy;
            return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
        }

        /// <summary>호 라인 림 하이라이트 — 반경 radius에서 |r-R| < 1.2px 만 1에 수렴.</summary>
        private static float RimHighlight(float px, float py, float radius)
        {
            float r = Mathf.Sqrt(px * px + py * py);
            return Mathf.Clamp01(1f - Mathf.Abs(r - radius) / 1.2f);
        }

        /// <summary>호 마스크 — 중심(cx,cy) 반경 radius·두께 2*halfW, 각도 창 [a0,a1](rad) 내부만. 랩 안전.</summary>
        private static float ArcMask(float px, float py, float cx, float cy, float radius, float halfW, float a0, float a1)
        {
            float dx = px - cx, dy = py - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float radial = Mathf.Min(r - (radius - halfW), (radius + halfW) - r);
            if (radial <= -0.5f) return 0f;

            float ang = Mathf.Atan2(dy, dx);
            float d0 = AngDelta(ang, a0);   // 창 시작으로부터의 각거리 (내부 양수)
            float d1 = AngDelta(a1, ang);   // 창 끝까지의 각거리 (내부 양수)
            float edge = Mathf.Min(d0, d1) / 0.05f;
            if (edge <= -0.5f) return 0f;

            return Mathf.Clamp01(radial + 0.5f) * Mathf.Clamp01(edge + 0.5f);
        }

        /// <summary>각도 차 랩 (-π..π) — 각도 창 경계 판정용.</summary>
        private static float AngDelta(float a, float b)
        {
            float d = a - b;
            while (d > Mathf.PI) d -= 2f * Mathf.PI;
            while (d < -Mathf.PI) d += 2f * Mathf.PI;
            return d;
        }
    }
}
