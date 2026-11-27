using NUnit.Framework;
using System.IO;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-11-25] GblItemIconRenderer 전 아이템 동기 베이크 소스계약.
    /// 배경: 베이크 큐는 "마운트 프레임 → 다음 프레임 ReadPixels" 2프레임 갭이라 N번째 아이템
    /// 아이콘이 ≈2N프레임 후에야 표출 — '전 아이템 아이콘 즉시·균일 표출' 요구 위반.
    /// 물고기 전용 동기 베이크([34차] SyncBakeFromGlbPath)가 동일 프레임 완결을 증명했으므로
    /// 이를 전 아이템으로 확장(SyncBakeItem 진입 → SyncBakeFromPrefab 공용 본). 계약 축:
    /// ① GetOrCreateIcon은 에디터에서 큐 등록 전에 동기 베이크를 선행 호출(즉시 표출)
    /// ② 캐시 상한(MaxCacheSize) 검사가 동기 베이크보다 선행(상한 계약 균일 적용)
    /// ③ 동기 베이크 본은 마운트→cam.Render→ReadPixels→캐시 저장을 1메서드 안에서 순차 완결
    /// ④ 프리팹 해석 — GLB 직결(AssetDatabase) 후 Resources/UserProvided 폴백(TryMount 동일 규약)
    /// ⑤ 방향·프레이밍 공용 헬퍼 + 거치대 은닉 + 요리 틴트 배선(TryMount 규약 통일)
    /// ⑥ 큐 경로 잔존(에디터 동기 실패 폴백 + 비에디터 빌드) + 예외 시 임시 자원 정리.
    /// 런타임 실렌더 판정은 Play 게이트(부모 몫) — 본 테스트는 소스 어서션 원칙
    /// (Cave146P1ContractTests/GblItemIconOrientationTests 선례).
    /// </summary>
    public class GblItemIconSyncBakeContractTests
    {
        private const string SrcPath = "Assets/Scripts/UI/GblItemIconRenderer.cs";

        /// <summary>동기 베이크 섹션(#if UNITY_EDITOR 블록) 바로 다음 선언 — 공용 본 슬라이스 종료 마커.</summary>
        private const string SyncBodyEndMarker = "/// <summary>id에서 알려진 접두사를 제거합니다.</summary>";

        // ── ①② 진입 순서 — 상한 검사 → 동기 베이크(#if UNITY_EDITOR) → 큐 등록 ──

        [Test]
        public void GetOrCreateIcon_CacheCap_ThenSyncBake_ThenQueueEnrollment()
        {
            string src = ReadSource(SrcPath);
            string body = Body(src, "public static Texture2D GetOrCreateIcon(PlayerInventory.ItemData item)",
                "private void Awake()");

            int cap = body.IndexOf("if (_iconCache.Count >= MaxCacheSize)", System.StringComparison.Ordinal);
            int ifdef = body.IndexOf("#if UNITY_EDITOR", System.StringComparison.Ordinal);
            int sync = body.IndexOf("SyncBakeItem(item.id, modelKey, glbPath)", System.StringComparison.Ordinal);
            int queue = body.IndexOf("_bakeQueue.Add(new BakeJob", System.StringComparison.Ordinal);

            Assert.That(cap, Is.GreaterThanOrEqualTo(0), "캐시 상한 검사 존재(MaxCacheSize 계약).");
            Assert.That(sync, Is.GreaterThanOrEqualTo(0),
                "전 아이템 동기 베이크 진입(SyncBakeItem) 존재 — 물고기 전용([34차]) 경유의 전 아이템 승격.");
            Assert.That(ifdef, Is.GreaterThan(cap).And.LessThan(sync),
                "동기 베이크 진입은 #if UNITY_EDITOR 게이트 뒤 — 에디터 Play 전용(비에디터는 큐 경로 유지).");
            Assert.That(queue, Is.GreaterThan(sync),
                "큐 등록은 동기 베이크 실패 시의 폴백 — 큐가 먼저면 2프레임 갭(≈2N프레임 지연) 회귀.");

            Assert.That(src, Does.Not.Contain("private static Texture2D SyncBakeFromGlbPath("),
                "물고기 전용 진입점(SyncBakeFromGlbPath) 잔존 금지 — 전 아이템 진입점으로 일원화.");
        }

        // ── ④ 진입점 — 에디터 게이트 + 프리팹 해석 규약(GLB 직결 → UserProvided) ──

        [Test]
        public void SyncBakeEntry_EditorGated_And_ResolvesPrefab_GlbPathThenUserProvided()
        {
            string src = ReadSource(SrcPath);

            int def = src.IndexOf("private static Texture2D SyncBakeItem(", System.StringComparison.Ordinal);
            Assert.That(def, Is.GreaterThanOrEqualTo(0), "SyncBakeItem 진입점 존재.");
            int openIfdef = src.LastIndexOf("#if UNITY_EDITOR", def, System.StringComparison.Ordinal);
            int closeIfdef = src.IndexOf("#endif", def, System.StringComparison.Ordinal);
            Assert.That(openIfdef, Is.GreaterThanOrEqualTo(0),
                "동기 베이크 섹션은 #if UNITY_EDITOR로 개시(에디터 전용 — [34차] 승계).");
            Assert.That(closeIfdef, Is.GreaterThan(def),
                "동기 베이크 섹션은 #endif로 폐쇄 — AssetDatabase는 에디터 전용 API.");

            string body = Body(src, "private static Texture2D SyncBakeItem(",
                "private static Texture2D SyncBakeFromPrefab(");
            int glb = body.IndexOf("AssetDatabase.LoadAssetAtPath<GameObject>(glbPath)", System.StringComparison.Ordinal);
            int res = body.IndexOf("Resources.Load<GameObject>(\"Models/UserProvided/\" + modelKey)", System.StringComparison.Ordinal);
            Assert.That(glb, Is.GreaterThanOrEqualTo(0),
                "GLB 직결 경로(물고기 등 UserProvided 밖)는 에디터 AssetDatabase 로드. [34차] 승계.");
            Assert.That(res, Is.GreaterThan(glb),
                "UserProvided Resources 로드가 GLB 직결 뒤 폴백 — TryMount와 동일 규약·순서(전 아이템 커버).");
            Assert.That(body, Does.Contain("return SyncBakeFromPrefab(itemId, modelKey, prefab);"),
                "프리팹 해석 성공 시 공용 본 1회 위임 — 베이크 로직 중복 구현 금지.");
        }

        // ── ③ 동일 프레임 완결 — 마운트→Render→ReadPixels→캐시가 1메서드 안에 순차 배치 ──

        [Test]
        public void SyncBakeBody_SameFrameCompletes_Mount_Render_ReadPixels_Cache()
        {
            string src = ReadSource(SrcPath);
            string body = Body(src, "private static Texture2D SyncBakeFromPrefab(", SyncBodyEndMarker);

            int mount = body.IndexOf("Object.Instantiate(prefab)", System.StringComparison.Ordinal);
            int render = body.IndexOf("cam.Render();", System.StringComparison.Ordinal);
            // [197차] ReadPixels는 공용 슈퍼샘플 헬퍼(BakeFromRTDownsampled)로 위임 — 동일 프레임 완결 유지.
            int read = body.IndexOf("BakeFromRTDownsampled(rt,", System.StringComparison.Ordinal);
            int cache = body.IndexOf("_iconCache[itemId] = copy;", System.StringComparison.Ordinal);

            Assert.That(mount, Is.GreaterThanOrEqualTo(0), "동기 베이크 본이 마운트(Instantiate)를 소유.");
            Assert.That(render, Is.GreaterThan(mount), "cam.Render()가 마운트 이후 — 오프스크린 렌더 즉시 수행.");
            Assert.That(read, Is.GreaterThan(render),
                "ReadPixels(슈퍼샘플 헬퍼)가 Render 직후 — 다음 프레임 대기 없음(2프레임 갭 제거 계약). [197차 갱신]");
            Assert.That(cache, Is.GreaterThan(read),
                "캐시 저장까지 1회 호출에 완결 — 반환값이 곧 최종 아이콘(즉시·균일 표출).");
        }

        // ── ⑤ TryMount 규약 통일 — 방향/프레이밍 공용 헬퍼 + 거치대 은닉 + 요리 틴트 ──

        [Test]
        public void SyncBakeBody_TryMountParity_Orientation_Framing_StandHiding_DishTint()
        {
            string src = ReadSource(SrcPath);
            string body = Body(src, "private static Texture2D SyncBakeFromPrefab(", SyncBodyEndMarker);

            Assert.That(body.IndexOf("ApplyIconOrientation(modelKey", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0),
                "방향 정규화는 공용 헬퍼(per-key 오버라이드 우선) — TryMount와 동일 키 규약. [166차 E]");
            Assert.That(body.IndexOf("FrameIconCamera(cam, b);", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0),
                "프레이밍은 공용 헬퍼(정면 고정 전체핏) — TryMount와 동일. [142차 H][166차 E]");
            Assert.That(body.IndexOf("ItemHoldManager.GetHideNodeNames(modelKey)", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0),
                "거치대 노드 은닉([165b차]) — 전 아이템 확장 시 동기 경로에도 TryMount 동일 적용.");
            Assert.That(body.IndexOf("DishGlbCatalog.ApplyTint", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0),
                "왕실 요리 hue 틴트([172차]) — 동기 경로 유지(전 아이템 확장 시 누락 금지).");
        }

        // ── ⑥ 큐 경로 잔존(폴백/디바이스) + 동기 베이크 임시 자원 정리 ──

        [Test]
        public void QueuePath_RemainsFallback_And_SyncBakeCleansTempResources()
        {
            string src = ReadSource(SrcPath);

            string update = Body(src, "private void Update()", "싱글턴 보조");
            Assert.That(update, Does.Contain("프레임당 1개 처리"),
                "큐는 프레임당 1개 순차 처리 유지 — 동기 실패 폴백 + 비에디터(디바이스) 빌드 본경로.");
            Assert.That(src, Does.Contain("_bakeQueue.Add(new BakeJob"),
                "큐 경로 잔존 계약 — 에디터 동기 베이크 실패 시 낙하 지점.");

            string sync = Body(src, "private static Texture2D SyncBakeFromPrefab(", SyncBodyEndMarker);
            //  [187차 수리] 동기베이크는 같은 프레임 연속 호출 — Object.Destroy(지연)로 두면 직전 아이템 노드가
            //    프레임 끝까지 같은 원격 위치에 남아 뒤 아이템 카메라가 겹쳐 찍는다("아이템 겹침" 사용자 실측).
            //    → DestroyImmediate 계약(성공 경로 + 예외 정리 경로 ×2).
            Assert.That(System.Text.RegularExpressions.Regex.Matches(sync, @"Object\.DestroyImmediate\(camGo\)").Count,
                Is.GreaterThanOrEqualTo(2), "카메라 즉시 파괴 — 성공 경로 + 예외 정리 경로(크래시/누수 금지).");
            Assert.That(System.Text.RegularExpressions.Regex.Matches(sync, @"Object\.DestroyImmediate\(node\)").Count,
                Is.GreaterThanOrEqualTo(2), "노드 즉시 파괴 — 성공 경로 + 예외 정리 경로.");
            Assert.That(System.Text.RegularExpressions.Regex.Matches(sync, @"rt\.Release\(\)").Count,
                Is.GreaterThanOrEqualTo(2), "RT Release — 성공 경로 + 예외 정리 경로.");
            // 지연 파괴 재발 방지 — 동기 섹션 내 Object.Destroy(node/camGo) 직접 호출 금지.
            Assert.That(System.Text.RegularExpressions.Regex.Matches(sync, @"Object\.Destroy\((camGo|node)\)").Count,
                Is.EqualTo(0), "동기 섹션에 지연 Object.Destroy(node/camGo) 재발 금지 — 같은 프레임 연속 베이크 겹침 원인.");
        }

        // ── [209차 B] 베이크 품질 — 조명 릭 튜닝 + 4× 다운샘플 후처리(비네트/콘트라스트) ──

        /// <summary>
        /// [209차 B] 아이콘 품질 계약 핀.
        /// ① 조명 릭 명암 상향(완만 — 흰 재질 백색클리핑 실측 가드): 키 0.60/각도(35,25,0)·필 0.10·림 0.45.
        /// ② 4× 다운샘플 출력 픽셀 후처리: 비네트/콘트라스트/채도 부스트 상수(public const) + 적용 함수
        ///    + 루프 배선 + 배경 알파 보존 가드(임계 미만 무처리). 기존 동기 베이크/프레이밍 계약과 무충돌.
        /// </summary>
        [Test]
        public void BakeQuality209B_LightRigTuning_And_DownsamplePostFX()
        {
            string src = ReadSource(SrcPath);

            // ① 조명 릭 튜닝 — AttachIconLightRig 본 한정 매치(타 파일 0.45f 오탐 방지).
            string rig = Body(src, "private static void AttachIconLightRig(Camera cam)",
                "private static void CreateRigLight(");
            Assert.That(rig, Does.Contain("new Vector3(35f, 25f, 0f)"),
                "[209차 B] 키 각도 (30,40,0)→(35,25,0) 측면성 강화 — 림 각도 (0,25,180) 유지.");
            Assert.That(rig, Does.Contain("0.60f"), "[209차 B] 키 강도 0.45→0.60(완만 상향).");
            Assert.That(rig, Does.Contain("0.10f"), "[209차 B] 필 강도 0.15→0.10(명암 극대화).");
            Assert.That(rig, Does.Contain("0.45f"), "[209차 B] 림 강도 0.30→0.45(에지 발광 팝).");

            // ② 후처리 상수(public const — 튜닝 단일 수정점) + 적용 함수 + 다운샘플 배선.
            Assert.That(src, Does.Contain("public const float VignetteStrength = 0.15f"),
                "[209차 B] 비네트 강도 상수(코너 최대 15% 감광).");
            Assert.That(src, Does.Contain("public const float ContrastBoost = 1.05f"),
                "[209차 B] 콘트라스트 부스트 상수.");
            Assert.That(src, Does.Contain("public const float SaturationBoost = 1.05f"),
                "[209차 B] 채도 부스트 상수.");
            Assert.That(src, Does.Contain("public const float ItemAlphaThreshold = 0.05f"),
                "[209차 B] 아이템 픽셀 알파 임계(배경 보존 가드) 상수.");
            Assert.That(src, Does.Contain("public static void ApplyIconPixelPostFX("),
                "[209차 B] 픽셀 후처리 순수 함수 존재.");
            Assert.That(src, Does.Contain("ApplyIconPixelPostFX(nx, ny, ba / 255f, ref fr, ref fg, ref fb)"),
                "[209차 B] 4× 박스다운샘플 출력 픽셀마다 후처리 적용 배선.");
            Assert.That(src, Does.Contain("if (alpha < ItemAlphaThreshold)"),
                "[209차 B] 배경 픽셀 알파/색 보존 가드(조기 반환) — RT 배경 불투명 실측(SolidColor alpha=1)에서도 설계 대응.");
        }

        // ── [210차] 모루 아이콘 밑동 제외 — 은닉 레지스트리 등록 + bounds 수집 순서 핀 ──

        /// <summary>
        /// [210차] 모루(anvil-1286cb) 밑동(stump) 은닉 소스핀. 근거: 아이콘에서 밑동 제외 — 사용자 지시.
        /// ① ItemHoldManager._holdConfigs에 "anvil-1286cb" → HideNodeNames {"stump"} 등록(GLB 원형 유지,
        ///    아이콘 렌더에서만 은닉 — HoldConfig 나머지 기본값이라 Hold() 무효, 월드 무영향).
        /// ② GblItemIconRenderer 동기 베이크 본은 은닉(SetActive false)이 렌더러 수집(GetComponentsInChildren)
        ///    에 선행 — 기본 includeInactive=false로 stump 렌더러가 bounds에서 제외(프레이밍 정합).
        /// </summary>
        [Test]
        public void AnvilStumpHide210_RegistryPin_And_BoundsCollectOrder()
        {
            // ① 은닉 레지스트리 등록 핀 — 키는 프로브/ResolveModelKey 계열 파일명 기반 "anvil-1286cb".
            string hold = ReadSource("Assets/Scripts/Systems/ItemHoldManager.cs");
            Assert.That(hold, Does.Contain("{ \"anvil-1286cb\", new HoldConfig { HideNodeNames = new[]{ \"stump\" } } }"),
                "[210차] 모루 은닉 레지스트리 등록(anvil-1286cb → stump) 존재 — 아이콘 밑동 제외 계약.");
            Assert.That(hold, Does.Contain("[210차] 모루 아이콘 밑동 제외"),
                "[210차] 등록 근거 주석 존재(사용자 지시, GLB 원형 유지 — 월드 무영향).");

            // ② bounds 수집 순서 핀 — 은닉 루프가 렌더러 수집보다 선행(뒤집히면 stump가 bounds에 재포함).
            string src = ReadSource(SrcPath);
            string body = Body(src, "private static Texture2D SyncBakeFromPrefab(", SyncBodyEndMarker);
            int hide = body.IndexOf("ItemHoldManager.GetHideNodeNames(modelKey)", System.StringComparison.Ordinal);
            int collect = body.IndexOf("node.GetComponentsInChildren<Renderer>()", System.StringComparison.Ordinal);
            Assert.That(hide, Is.GreaterThanOrEqualTo(0), "[210차] 동기 베이크 본 은닉 루프 존재.");
            Assert.That(collect, Is.GreaterThan(hide),
                "[210차] 렌더러 수집은 은닉 이후 — GetComponentsInChildren 기본(비활성 미수집)으로 stump bounds 제외.");
        }

        // ── 공용 헬퍼 ──

        private static string ReadSource(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"소스 파일 존재: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>소스에서 startMarker~endMarker 구간(메서드 블록) 추출 — 미발견 시 빈 문자열.</summary>
        private static string Body(string src, string startMarker, string endMarker)
        {
            int start = src.IndexOf(startMarker, System.StringComparison.Ordinal);
            int end = start >= 0 ? src.IndexOf(endMarker, start, System.StringComparison.Ordinal) : -1;
            return start >= 0 && end > start ? src.Substring(start, end - start) : string.Empty;
        }
    }
}
