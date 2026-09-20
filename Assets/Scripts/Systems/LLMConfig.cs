using System;
using System.IO;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O9 (C-O9-01): LLM 설정 데이터 — llm_config.json 매핑.
    /// 프로젝트 루트(Assets의 부모)에 두고 git 제외(.gitignore — API 키 포함).
    /// </summary>
    [Serializable]
    public class LLMConfigData
    {
        public string provider;        // "openrouter"
        public string baseUrl;         // https://openrouter.ai/api/v1/chat/completions
        public string apiKey;          // OpenRouter 키 (비어있으면 규칙 폴백)
        public string model;           // 주 모델 (content 반환형 — reasoning 모델 금지)
        public string backupModel;     // 보조 모델 (빈 content 1회 재시도)
        public int maxTokens;          // 응답 최대 토큰
        public int maxDailyCalls;      // 데일리 호출 한도 (초과 시 규칙 폴백)
        public int timeoutSeconds;     // 요청 타임아웃(초)
    }

    /// <summary>
    /// Phase O9 (C-O9-01): LLM 설정 로더 — 정적, 캐시 1회 로드.
    ///
    /// 경로 후보 순회:
    ///   ① Application.dataPath + "/../llm_config.json" — 에디터/개발(= 프로젝트 루트, Assets의 부모)
    ///   ② Application.dataPath + "/llm_config.json"   — 빌드 대응(*_Data 폴더에 복사해 둔 경우)
    ///
    /// 파일 없음/파싱 실패 → null 반환(호출부는 규칙 폴백 경로로 이행 — 예외 던지지 않음).
    /// foreach 규약 준수.
    /// </summary>
    public static class LLMConfig
    {
        private static LLMConfigData _cached;   // static 캐시 — 세션당 1회 로드
        private static bool _loaded;

        /// <summary>llm_config.json 로드 (캐시 1회). 없으면 null — 규칙 폴백 경로.</summary>
        public static LLMConfigData Load()
        {
            if (_loaded) return _cached;
            _loaded = true;

            // ① 에디터/개발: dataPath(=Assets)의 부모 = 프로젝트 루트
            // ② 빌드: dataPath(=*_Data) 바로 아래 — 빌드 시 llm_config.json을 *_Data에 복사하면 읽힌다
            string[] candidates =
            {
                Application.dataPath + "/../llm_config.json",
                Application.dataPath + "/llm_config.json",
            };

            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var data = JsonUtility.FromJson<LLMConfigData>(File.ReadAllText(path));
                    if (data != null)
                    {
                        _cached = data;
                        return _cached;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LLMConfig] 파싱 실패(" + path + "): " + e.Message);
                }
            }

            return null; // 설정 없음 — 규칙 폴백
        }

        /// <summary>LLM 사용 가능 상태 — 설정 파일 존재 + apiKey 비어있지 않음. 미충족이면 규칙 폴백.</summary>
        public static bool IsConfigured()
        {
            var cfg = Load();
            return cfg != null && !string.IsNullOrEmpty(cfg.apiKey);
        }
    }
}
