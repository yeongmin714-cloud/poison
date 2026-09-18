using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [U8 수리] UTK 배경용 텍스처 안전 변환 — 외부 캐시(아이콘/렌더러)가 원본을
    /// 파괴해도 배경이 깨지지 않도록 소유 복사본을 만들어 캐싱한다.
    /// (Play 실측: "Invalid value for image texture" → 노란 경고 아이콘 표시 뿌리)
    /// </summary>
    public static class UTKTextureSafe
    {
        private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _copies
            = new System.Collections.Generic.Dictionary<int, Texture2D>();

        /// <summary>원본 텍스처의 소유 복사본 반환 (인스턴스 ID 캐시). 파괴된 원본이면 null.</summary>
        public static Texture2D GetSafe(Texture2D source)
        {
            if (source == null) return null;   // Unity 페이크 null(파괴) 포함

            int id = source.GetInstanceID();
            if (_copies.TryGetValue(id, out var cached))
            {
                if (cached != null) return cached;
                _copies.Remove(id);
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
    }
}
