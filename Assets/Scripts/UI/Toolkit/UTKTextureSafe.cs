// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 618
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [U8 수리] UTK 배경용 텍스처 안전 변환 — 외부 캐시(아이콘/렌더러)가 원본을
    /// 파괴해도 배경이 깨지지 않도록 소유 복사본을 만들어 캐싱한다.
    /// (Play 실측: "Invalid value for image texture" → 노란 경고 아이콘 표시 뿌리)
    ///
    /// [캐시 누수 방지 강화] 아이콘 렌더러가 텍스처를 재생성하면 인스턴스ID가 계속
    /// 늘어 복사본이 무한 누적됨 → FIFO 큐(LinkedList)로 최대 MaxCacheEntries 개만
    /// 유지하고, 오버플로우 시 가장 오래된 복사본을 파괴한다.
    /// </summary>
    public static class UTKTextureSafe
    {
        // [U9 보강] 상한 256→1024 + 축출 조건에 "나이 600초 경과" 추가 — 아이콘 재베이크
        //   체인으로 캐시가 회전해도 "현재 스타일이 참조 중인 젊은 복사본"이 파괴되지 않게
        //   보장(파괴된 복사본 참조 = "Invalid value for image texture" 재발 뿌리).
        private const int MaxCacheEntries = 1024;
        private const float EvictMinAgeSeconds = 600f;

        private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _copies
            = new System.Collections.Generic.Dictionary<int, Texture2D>();
        // FIFO 삽입 순서 큐 — 한계 초과 시 가장 오래된 복사본 제거용.
        private static readonly System.Collections.Generic.LinkedList<Texture2D> _fifo
            = new System.Collections.Generic.LinkedList<Texture2D>();
        // 복사본 instanceID → 원본 instanceID 역매핑. _copies의 키는 '원본' ID인데
        // 큐 노드는 '복사본'이므로 축출 시 이 매핑 없이는 올바른 dict 엔트리를 지울 수 없다.
        private static readonly System.Collections.Generic.Dictionary<int, int> _copySource
            = new System.Collections.Generic.Dictionary<int, int>();
        // 복사본 instanceID → 생성 시각(unscaled). 축출 나이 판정용.
        private static readonly System.Collections.Generic.Dictionary<int, float> _copyBorn
            = new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>원본 텍스처의 소유 복사본 반환 (인스턴스 ID 캐시). 파괴된 원본이면 null.</summary>
        public static Texture2D GetSafe(Texture2D source)
        {
            if (source == null) return null;   // Unity 페이크 null(파괴) 포함

            int id = source.GetInstanceID();
            if (_copies.TryGetValue(id, out var cached))
            {
                if (cached != null) return cached;
                // 원본이 파괴되어 캐시 히트 실패 — 사본도 함께 제거(FIFO 잔여물 정리)
                _copies.Remove(id);
                _fifo.Remove(cached);
                int deadCopyId = cached.GetInstanceID();
                _copySource.Remove(deadCopyId);
                _copyBorn.Remove(deadCopyId);
            }

            try
            {
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "UTKSafe_" + source.name
                };
                copy.SetPixels(source.GetPixels());
                copy.Apply(false, true);   // read/write 비활성화 — GPU 전용 업로드
                _copies[id] = copy;
                _fifo.AddLast(copy);
                int copyId = copy.GetInstanceID();
                _copySource[copyId] = id;
                _copyBorn[copyId] = Time.unscaledTime;
                EvictOverflow();
                return copy;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[UTKTextureSafe] 복사 실패: " + e.Message);
                return null;
            }
        }

        /// <summary>Background 생성 헬퍼 — 무효 텍스처면 StyleKeyword.Null.</summary>
        public static StyleBackground ToBackground(Texture2D source)
        {
            var safe = GetSafe(source);
            return safe != null ? new StyleBackground(Background.FromTexture2D(safe)) : new StyleBackground(StyleKeyword.Null);
        }

        /// <summary>
        /// FIFO 한계 초과 시 가장 오래된 복사본을 파괴(에디터/런타임 안전).
        /// [U9] 단, 600초 미만의 "젊은" 복사본은 살아있는 스타일이 참조 중일 수 있으므로
        ///   축출을 보류한다(상한을 임시 초과 허용 — 메모리 안전 vs 경고 방지 트레이드오프).
        /// </summary>
        private static void EvictOverflow()
        {
            while (_fifo.Count > MaxCacheEntries)
            {
                var oldest = _fifo.First;
                if (oldest == null) return;
                int copyId = oldest.Value.GetInstanceID();
                if (_copyBorn.TryGetValue(copyId, out float born)
                    && Time.unscaledTime - born < EvictMinAgeSeconds)
                    return;   // 가장 오래된 항목이 아직 젊다 — 축출 보류(다음 기회에)
                _fifo.RemoveFirst();
                // _copies 키는 원본 ID — 복사본 ID로 역조회해 정확한 엔트리 제거
                if (_copySource.TryGetValue(copyId, out int srcId))
                {
                    _copies.Remove(srcId);
                    _copySource.Remove(copyId);
                }
                _copyBorn.Remove(copyId);
                if (Application.isPlaying) Object.Destroy(oldest.Value);
                else Object.DestroyImmediate(oldest.Value);
            }
        }
    }
}