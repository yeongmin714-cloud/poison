using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [U9 배경 힐러] "Invalid value for image texture" 노란 경고 완전 차단용 자가 치유 히일러.
    ///
    /// 원인 재구성:
    ///   - Scene 전환 시 Resources.UnloadUnusedAssets류가 런타임 생성 텍스처(아이콘 베이크,
    ///     UTKTextureSafe hideFlags=HideAndDontSave 복사본 포함)를 파괴하는 전례가 있음.
    ///   - 파괴된 텍스처를 쥔 VisualElement가 스타일 재적용될 때마다
    ///     StylePropertyReader.ReadBackground 가 "Invalid value for image texture" 경고를 냄.
    ///   - UTKTextureSafe 경유로 교체해도 Safe 복사본 자체가 같은 요인으로 죽을 수 있음 →
    ///     죽은 배경을 가진 요소를 주기적으로 순회해 스타일 참조를 제거(Null)하면 경고가 소멸한다.
    ///
    /// 구동: MonoBehaviour 없음 — UTKWindowManager.Updater가 0.5s 스로틀 + sceneLoaded 직후 1회 호출.
    /// GC 최소화: 순회용 스택/자식 스크래치 List 재사용, 진단 로그는 요소별 1회만.
    /// </summary>
    public static class UTKBackgroundHealer
    {
        private const int ParentChainDepth = 4;   // 진단 키용 부모 체인 깊이 상한

        // ── GC 최소화용 재사용 스크래치 (Sweep마다 재사용) ──
        private static readonly List<VisualElement> _stack    = new List<VisualElement>();
        private static readonly List<VisualElement> _children = new List<VisualElement>();

        // 진단 로그 1회 제한 — 요소 이름+클래스+부모체인 해시 키.
        // 실측 로그로 진짜 범인 요소를 특정하기 위한 단서 수집용.
        private static readonly HashSet<string> _reported = new HashSet<string>();

        private static int _totalHealed;

        /// <summary>전 트리에서 죽은 배경 텍스처를 검색해 스타일 참조를 제거한다. 호출 스로틀은 호출부 책임.</summary>
        public static void Sweep()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
                return;

            try
            {
                int healed = 0;
                _stack.Clear();
                _stack.Add(root);

                // 스택 기반 DFS — 재귀 없음(스택 오버플로 방지), 자식은 스냅샷 복사 후 재삽입하므로
                // 순회 중 트리 변경(자식 Add 등)에 안전하다. 누락된 부분은 다음 틱에 잡힌다.
                while (_stack.Count > 0)
                {
                    var el = _stack[_stack.Count - 1];
                    _stack.RemoveAt(_stack.Count - 1);
                    if (el == null)
                        continue;

                    if (TryHeal(el))
                        healed += 1;

                    _children.Clear();
                    foreach (var child in el.Children())
                    {
                        if (child != null)
                            _children.Add(child);
                    }
                    for (int i = _children.Count - 1; i >= 0; i--)
                        _stack.Add(_children[i]);
                }

                if (healed > 0)
                    Debug.LogWarning("[UTKBackgroundHealer] 라운드 치유 " + healed + "건 (누적 " + _totalHealed + ")");
            }
            catch (System.Exception e)
            {
                // 전체 순회를 하나의 try/catch로 감싸 다른 업데이트 흐름을 방해하지 않음.
                Debug.LogWarning("[UTKBackgroundHealer] Sweep 예외(다음 틱 재시도): " + e.Message);
            }
        }

        /// <summary>단일 요소의 배경을 검사하고, 죽은 참조면 제거. 개별 실패는 무시하고 true/false 반환.</summary>
        private static bool TryHeal(VisualElement el)
        {
            try
            {
                var bg = el.resolvedStyle.backgroundImage;
                bool dead = false;

                // 1) 텍스처 기반 배경 — Unity fake-null(파괴) 검출
                var tex = bg.texture;
                if (tex != null && IsDeadNative(tex))
                    dead = true;

                // 2) 스프라이트 기반 배경 — 관리상 존재하는 스프라이트라도 그 내부 텍스처가 파괴된 경우
                if (!dead && bg.sprite != null && !ReferenceEquals(bg.sprite, null))
                {
                    var st = bg.sprite.texture;
                    if (st != null && IsDeadNative(st))
                        dead = true;
                }

                if (!dead)
                    return false;

                // 치유: 죽은 참조 제거 → 다음 스타일 재적용에서 경고 소멸
                el.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
                _totalHealed += 1;
                ReportOnce(el);
                return true;
            }
            catch (System.Exception)
            {
                return false;   // 개별 요소 실패는 순회를 멈추지 않음
            }
        }

        /// <summary>
        /// Unity 죽은(파괴·fake-null) 네이티브 텍스처 판정. 널 안전.
        ///   !ReferenceEquals(tex, null) — wrapper 참조가 진짜 null인지(오버로드된 == 를 타지 않음)
        ///   tex == null                 — Unity 네이티브 핸들이 파괴됐는지
        /// 두 조건이 모두 참 → "wrapper는 살아있는데 네이티브는 파괴됨" = fake-null.
        /// </summary>
        private static bool IsDeadNative(Texture2D tex)
        {
            return !ReferenceEquals(tex, null) && tex == null;
        }

        /// <summary>진단 로그 — 요소별 1회만. 키 = 이름+클래스+부모체인.</summary>
        private static void ReportOnce(VisualElement el)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(el.name).Append('|');               // 이름
                foreach (var c in el.GetClasses())            // 클래스 — 콤마 결합
                    sb.Append(c).Append(',');
                sb.Append('|');
                var p = el.parent;                             // 부모 체인
                int depth = 0;
                while (p != null && depth < ParentChainDepth)
                {
                    sb.Append('>').Append(p.name);
                    p = p.parent;
                    depth += 1;
                }
                string key = sb.ToString();
                string parent = el.parent != null ? el.parent.name : "(none)";

                // 이미 보고된 요소 조합은 1회만 출력 — 로그 중복 방지
                if (!_reported.Add(key))
                    return;

                Debug.LogWarning("[UTKBackgroundHealer] 죽은 배경 치유 el=" + el.name
                    + " parent=" + parent
                    + " 총누적=" + _totalHealed);
            }
            catch (System.Exception)
            {
                // 진단 로그 실패가 치유를 막지는 않음
            }
        }
    }
}