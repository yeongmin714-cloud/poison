using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-10-14 Phase 4 — 레이아웃 소비 통합용 컨텍스트] 빌드된 성 내부 방에 부착되어
    /// 이 방이 어떤 InteriorLayoutDefinition으로 지어졌는지 기록한다.
    /// - 정적 전역/싱글턴 없음 — 각 방이 자신의 정의를 소유한다(컴포넌트 조회 계약).
    /// - InteriorLayoutDefinition은 직렬화 불가 클래스이므로 런타임 전용 프로퍼티로 보관하고,
    ///   직렬화되는 LayoutId 문자열만 씬에 남긴다. 소비자는 TryGetLayout으로 두 경로 모두 해석.
    /// - 문/영주/깃발/에디터 미리보기 등 소비 통합은 다음 작업(Phase 4b)에서 이 컨텍스트를 읽는다.
    /// </summary>
    public sealed class InteriorLayoutContext : MonoBehaviour
    {
        /// <summary>이 방을 지은 레이아웃의 안정적 식별자(직렬화됨 — 씬 재로드에도 유지).</summary>
        [SerializeField] private string layoutId;

        /// <summary>이 방을 지은 레이아웃의 안정적 식별자.</summary>
        public string LayoutId => layoutId;

        /// <summary>
        /// 이 방을 지은 레이아웃 정의(런타임 전용 — 직렬화되지 않으므로 도메인 리로드 후
        /// 참조가 null일 수 있다. 그 경우 TryGetLayout이 LayoutId로 재해석한다).
        /// </summary>
        public InteriorLayoutDefinition Definition { get; private set; }

        /// <summary>방 생성 직후 빌더가 호출한다(AddComponent 직후 즉시). null이면 기준형 폴백.</summary>
        public void Configure(InteriorLayoutDefinition definition)
        {
            Definition = definition ?? InteriorLayoutDefinition.KeepOctagon;
            layoutId = Definition.LayoutId;
        }

        /// <summary>
        /// 방에서 이 컨텍스트를 찾아 레이아웃 정의로 해석한다. 컨텍스트가 없으면 false.
        /// 해석 순서: 런타임 인스턴스 → 직렬화된 LayoutId 재해석 → 실패 시 false.
        /// </summary>
        public static bool TryGetLayout(GameObject room, out InteriorLayoutDefinition definition)
        {
            definition = null;
            InteriorLayoutContext context = room != null ? room.GetComponent<InteriorLayoutContext>() : null;
            if (context == null) return false;
            definition = context.Definition;
            if (definition != null) return true;
            return InteriorLayoutDefinition.TryGetLayout(context.LayoutId, out definition);
        }
    }
}
