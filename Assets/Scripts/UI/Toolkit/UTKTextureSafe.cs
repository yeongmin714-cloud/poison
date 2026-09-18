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
        private const int MaxCacheEntries = 256;

        private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _copies
            = new System.Collections.Generic.Dictionary<int, Texture2D>();
        // FIFO 삽입 순서 큐 — 한계 초과 시 가장 오래된 복사본 제거용.
        private static readonly System.Collections.Generic.LinkedList<Texture2D> _fifo
            = new System.Collections.Generic.LinkedList<Texture2D>();
        // 복사본 instanceID → 원본 instanceID 역매핑. _copies의 키는 '원본' ID인데
        // 큐 노드는 '복사본'이므로 축출 시 이 매핑 없이는 올바른 dict 엔트리를 지울 수 없다.
        private static readonly System.Collections.Generic.Dictionary<int, int> _copySource
            = new System.Collections.Generic.Dictionary<int, int>();

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
                _copySource.Remove(cached.GetInstanceID());
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
                _copySource[copy.GetInstanceID()] = id;
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

        /// <summary>FIFO 한계 초과 시 가장 오래된 복사본을 파괴(에디터/런타임 안전).</summary>
        private static void EvictOverflow()
        {
            while (_fifo.Count > MaxCacheEntries)
            {
                var oldest = _fifo.First;
                if (oldest == null) return;
                _fifo.RemoveFirst();
                // _copies 키는 원본 ID — 복사본 ID로 역조회해 정확한 엔트리 제거
                int copyId = oldest.Value.GetInstanceID();
                if (_copySource.TryGetValue(copyId, out int srcId))
                {
                    _copies.Remove(srcId);
                    _copySource.Remove(copyId);
                }
                if (Application.isPlaying) Object.Destroy(oldest.Value);
                else Object.DestroyImmediate(oldest.Value);
            }
        }
    }
}