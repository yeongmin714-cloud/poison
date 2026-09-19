using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 칭호 정의 — 서버 부여 표식 1건 (OpenMMO TITLES.md: 칭호는 경제와 비접촉,
    /// 골드/아이템 보상 없음, 표시 전용 명성 지표).
    /// (struct: 생성 후 변경 불가)
    /// </summary>
    public struct TitleDef
    {
        public string id;            // 고유 id (예: "execution_10")
        public string displayName;   // 표시용 칭호명 (예: "십인의 처형자")
        public string sourceType;    // 카운터 소스: executions/assassinations/conquests/set_complete/crafts
        public int threshold;        // 해당 sourceType 누적 카운터 임계값 (이상 도달 시 해금)
        public string description;   // 칭호 설명
    }

    /// <summary>
    /// C-O6-01: 칭호 정의 데이터베이스 (정적, 총 20종).
    /// - 발급은 TitleManager(Systems)가 카운터 도달 시 수행 — Core는 데이터만 제공
    /// - 등록순서 기준 상수 정렬: executions → assassinations → conquests → set_complete → crafts
    ///   (같은 sourceType 내 임계값 오름차순)
    /// </summary>
    public static class TitleData
    {
        public static readonly TitleDef[] All =
        {
            // ===== executions (처형) 5종 — 임계 1/3/5/10/25 =====
            new TitleDef { id = "execution_1",  displayName = "첫 처형을 집행한 자", sourceType = "executions", threshold = 1,
                description = "투항한 영주의 최후를 직접 결정했다. 처형의 길이 열렸다." },
            new TitleDef { id = "execution_3",  displayName = "처형 집행관", sourceType = "executions", threshold = 3,
                description = "세 번의 처형으로 이름이 제국 전역에 퍼졌다." },
            new TitleDef { id = "execution_5",  displayName = "5인의 처형자", sourceType = "executions", threshold = 5,
                description = "다섯 명의 영주가 당신의 명 앞에 무릎 꿇었다." },
            new TitleDef { id = "execution_10", displayName = "십인의 처형자", sourceType = "executions", threshold = 10,
                description = "열 명의 영주를 처형한 냉혹한 통치자." },
            new TitleDef { id = "execution_25", displayName = "처형의 달인", sourceType = "executions", threshold = 25,
                description = "25번의 처형 — 처형대에 전설이 새겨졌다." },

            // ===== assassinations (암살) 4종 — 임계 1/5/10/20 =====
            new TitleDef { id = "assassin_1",  displayName = "그림자의 칼", sourceType = "assassinations", threshold = 1,
                description = "첫 암살을 성공시킨 그림자. 발각되지 않았다." },
            new TitleDef { id = "assassin_5",  displayName = "5개의 그림자", sourceType = "assassinations", threshold = 5,
                description = "다섯 번의 독살 — 흔적도 남기지 않는 손놀림." },
            new TitleDef { id = "assassin_10", displayName = "왕위를 노리는 자", sourceType = "assassinations", threshold = 10,
                description = "열 번의 암살로 왕좌 앞까지 다가섰다." },
            new TitleDef { id = "assassin_20", displayName = "왕좌의 암살자", sourceType = "assassinations", threshold = 20,
                description = "20명의 영주를 침묵으로 보낸 전설의 암살자." },

            // ===== conquests (점령) 5종 — 임계 1/5/10/20/81 =====
            new TitleDef { id = "conquest_1",  displayName = "첫 영지를 손에 넣은 자", sourceType = "conquests", threshold = 1,
                description = "첫 영지를 점령했다. 정복의 서막." },
            new TitleDef { id = "conquest_5",  displayName = "5성의 정복자", sourceType = "conquests", threshold = 5,
                description = "다섯 개의 영지에 당신의 깃발이 꽂혔다." },
            new TitleDef { id = "conquest_10", displayName = "10성 정복자", sourceType = "conquests", threshold = 10,
                description = "열 개의 영지가 당신의 것으로 변했다." },
            new TitleDef { id = "conquest_20", displayName = "20성 대정복자", sourceType = "conquests", threshold = 20,
                description = "스무 개의 영지를 거느린 대정복자." },
            new TitleDef { id = "conquest_81", displayName = "81성의 정복자", sourceType = "conquests", threshold = 81,
                description = "82개 영지 중 81개를 점령 — 황제국만 남았다." },

            // ===== set_complete (세트 완성) 3종 — 임계 1/3/3 (EquipmentTierSet 연동용) =====
            new TitleDef { id = "set_leather", displayName = "가죽을 완성한 자", sourceType = "set_complete", threshold = 1,
                description = "가죽세트(Leather)의 모든 부위를 완성한 사냥꾼." },
            new TitleDef { id = "set_plate",   displayName = "판금을 완성한 자", sourceType = "set_complete", threshold = 3,
                description = "판금세트(Plate)를 완성한 자 — 대장 기술의 정점, 기사의 증표." },
            new TitleDef { id = "set_all3",    displayName = "3세트의 수집가", sourceType = "set_complete", threshold = 3,
                description = "가죽·사슬·판금, 3개 세트를 모두 완성한 수집가." },

            // ===== crafts (제작 수) 3종 — 임계 50/200/500 =====
            new TitleDef { id = "craft_50",  displayName = "장인의 첫걸음", sourceType = "crafts", threshold = 50,
                description = "50번의 제작 — 장인의 길에 들어섰다." },
            new TitleDef { id = "craft_200", displayName = "베테랑 장인", sourceType = "crafts", threshold = 200,
                description = "200번의 제작으로 베테랑 반열에 올랐다." },
            new TitleDef { id = "craft_500", displayName = "대장장이의 전설", sourceType = "crafts", threshold = 500,
                description = "500번의 제작 — 대장장이의 전설이 되었다." }
        };

        /// <summary>등록된 칭호 총수 (20종)</summary>
        public static int Count => All.Length;

        /// <summary>id로 칭호 정의 조회 (실패 시 false)</summary>
        public static bool TryGet(string id, out TitleDef def)
        {
            def = default;
            if (string.IsNullOrEmpty(id))
                return false;

            foreach (var t in All)
            {
                if (t.id == id)
                {
                    def = t;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// sourceType으로 칭호 필터링 (등록순서 유지, 없으면 빈 배열).
        /// TitleManager.RecordEvent → EvaluateUnlocks 양쪽에서 사용.
        /// </summary>
        public static TitleDef[] GetBySource(string sourceType)
        {
            if (string.IsNullOrEmpty(sourceType))
                return new TitleDef[0];

            int matches = 0;
            foreach (var t in All)
            {
                if (t.sourceType == sourceType)
                    matches++;
            }

            if (matches == 0)
                return new TitleDef[0];

            var result = new TitleDef[matches];
            int i = 0;
            foreach (var t in All)
            {
                if (t.sourceType == sourceType)
                    result[i++] = t;
            }
            return result;
        }
    }
}
