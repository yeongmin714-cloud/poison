using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P25-06: 용병 Placeholder 모델.
    /// 금색+은색 재질로 일반 병사와 구분됩니다.
    /// 바드 타입은 류트 모양 오브젝트가 추가됩니다.
    /// </summary>
    public class MercenaryPlaceholder : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string _mercenaryId = "";
        [SerializeField] private MercenaryGrade _grade = MercenaryGrade.Normal;
        [SerializeField] private string _jobType = "Soldier";

        private MercenaryData _data;

        /// <summary>용병 ID</summary>
        public string MercenaryId => _mercenaryId;

        /// <summary>등급</summary>
        public MercenaryGrade Grade => _grade;

        /// <summary>직업 타입</summary>
        public string JobType => _jobType;

        /// <summary>
        /// 용병 Placeholder 생성 (정적 팩토리 메서드).
        /// </summary>
        public static GameObject CreateMercenaryPlaceholder(MercenaryData data, Vector3 position, string name = null)
        {
            string goName = name ?? $"Mercenary_{data.mercenaryName}";

            // 기본 캡슐 모델 (일반 병사와 비슷한 크기)
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = goName;
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.5f, 2.2f, 1.5f); // 일반 병사보다 약간 큼

            // 금색+은색 재질 적용 (등급별 색상)
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color matColor = GetGradeColor(data.grade);
                renderer.material = MaterialHelper.CreateLitMaterial(matColor, $"{goName}_Mat");
            }

            // MercenaryPlaceholder 컴포넌트 추가
            var placeholder = go.AddComponent<MercenaryPlaceholder>();
            placeholder._mercenaryId = data.id;
            placeholder._grade = data.grade;
            placeholder._jobType = data.jobType;
            placeholder._data = data;

            // 바드: 류트 모양 오브젝트 추가
            if (data.jobType == "Bard")
            {
                CreateBardInstrument(go);
            }

            // 라벨 (TextMesh)
            var labelGo = new GameObject($"{goName}_Label");
            labelGo.transform.SetParent(go.transform);
            labelGo.transform.localPosition = new Vector3(0, 2.5f, 0);
            var textMesh = labelGo.AddComponent<TextMesh>();
            textMesh.text = $"{data.mercenaryName} {data.GradeStars}";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 0.07f;
            textMesh.color = Color.white;

            return go;
        }

        /// <summary>등급별 색상 반환</summary>
        private static Color GetGradeColor(MercenaryGrade grade)
        {
            return grade switch
            {
                MercenaryGrade.Normal => new Color(0.85f, 0.75f, 0.25f),    // 금색 (Normal)
                MercenaryGrade.High => new Color(0.90f, 0.85f, 0.40f),      // 밝은 금색 (High)
                MercenaryGrade.Elite => new Color(0.70f, 0.85f, 0.95f),     // 은색-백금 (Elite)
                MercenaryGrade.Legendary => new Color(1.0f, 0.85f, 0.0f),   // 황금색 (Legendary)
                _ => new Color(0.85f, 0.75f, 0.25f)
            };
        }

        /// <summary>바드 악기 생성 (르트/하프 모양)</summary>
        private static void CreateBardInstrument(GameObject parent)
        {
            // 류트 몸체 (타원형 Cube)
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "LuteBody";
            body.transform.SetParent(parent.transform);
            body.transform.localPosition = new Vector3(0.6f, 0.3f, 0);
            body.transform.localScale = new Vector3(0.25f, 0.35f, 0.15f);

            var bodyRenderer = body.GetComponent<MeshRenderer>();
            bodyRenderer.material = MaterialHelper.CreateLitMaterial(new Color(0.60f, 0.40f, 0.15f), "LuteBodyMat");

            // 류트 목 (Cylinder-like Cube)
            var neck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neck.name = "LuteNeck";
            neck.transform.SetParent(parent.transform);
            neck.transform.localPosition = new Vector3(0.6f, 0.7f, 0);
            neck.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);

            var neckRenderer = neck.GetComponent<MeshRenderer>();
            neckRenderer.material = MaterialHelper.CreateLitMaterial(new Color(0.40f, 0.25f, 0.10f), "LuteNeckMat");

            // 현 (가는 막대 3개)
            for (int i = 0; i < 3; i++)
            {
                var str = GameObject.CreatePrimitive(PrimitiveType.Cube);
                str.name = $"String_{i}";
                str.transform.SetParent(parent.transform);
                float xOff = (i - 1) * 0.04f;
                str.transform.localPosition = new Vector3(0.6f + xOff, 0.5f, 0.05f);
                str.transform.localScale = new Vector3(0.01f, 0.4f, 0.01f);

                var strRenderer = str.GetComponent<MeshRenderer>();
                strRenderer.material = MaterialHelper.CreateLitMaterial(new Color(0.90f, 0.90f, 0.80f), $"LuteString_{i}");
            }
        }
    }
}