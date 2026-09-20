using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using ProjectName.Core;
using ProjectName.Core.Data;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProjectName.Tests.EditMode")]

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O9 (C-O9-02): LLM NPC 대화 어댑터 — 규칙 폴백 이중 경로.
    ///
    /// [이중 경로] LLM 설정됨 + 한도 잔여 + 응답 정상 → LLM 텍스트 / 그 외 모든 경우 → 규칙 폴백 텍스트.
    /// [데일리 한도] PlayerPrefs "llm_daily_calls_v1" + 날짜 키 "llm_daily_date_v1"(UTC 날짜 바뀌면 리셋).
    /// [큐] 동시 요청 1개 제한 — isBusy 중 재요청은 무시(이전 응답 보존).
    /// [폴백] 응답 파싱 실패/빈 content(reasoning형 추정) → backupModel 1회 재시도 → 그래도 비면 규칙 폴백.
    /// [이벤트] DialogueReady(npcKey, llmText) — UnityWebRequest 코루틴이므로 메인스레드 발화 보장.
    ///
    /// GameManager 부트스트랩 1줄(CreateSystemIfMissing("NPCDialogueAdapter")) → Awake에서 DontDestroyOnLoad.
    /// Awake 미실행 대응: Instance getter가 FindAnyObjectByType 폴백 + Bootstrap 멱등 (O7 패턴).
    /// foreach 규약 준수. 네트워크 실호출 판정은 Play 모드로 남긴다(주석 — EditMode 테스트는 순수 메서드만).
    /// </summary>
    public class NPCDialogueAdapter : MonoBehaviour
    {
        public static NPCDialogueAdapter Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<NPCDialogueAdapter>(); // Awake 미실행/부트 순서 폴백 (O7)
                return _instance;
            }
        }
        private static NPCDialogueAdapter _instance;

        /// <summary>(npcKey, llmText) — LLM 응답 또는 규칙 폴백 텍스트 확정 시 발화(메인스레드).</summary>
        public static event Action<string, string> DialogueReady;

        // ── 데일리 한도 저장 키 ──
        private const string DailyCallsKey = "llm_daily_calls_v1";
        private const string DailyDateKey = "llm_daily_date_v1";

        private const float RequestTemperature = 0.9f;

        private LLMConfigData _config;
        private bool _isBusy;

        /// <summary>요청 처리 중(동시 1개 제한) — true 동안 신규 RequestDialogue는 무시.</summary>
        public bool IsBusy => _isBusy;

        // =================== 생명주기 ===================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _config = LLMConfig.Load();
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>부트스트랩 — GameManager 리플렉션 1줄용. 멱등(이미 있으면 무작동).</summary>
        public static void Bootstrap()
        {
            if (Instance != null) return; // getter가 FindAnyObjectByType 폴백까지 수행
            var go = new GameObject("NPCDialogueAdapter");
            go.AddComponent<NPCDialogueAdapter>(); // Awake가 Instance + DontDestroyOnLoad
            Debug.Log("[NPCDialogue] 부트스트랩 생성 완료");
        }

        // =================== 구독 (칭호 패턴 — Subscribe/Unsubscribe 쌍) ===================

        public static void Subscribe(Action<string, string> handler) => DialogueReady += handler;

        public static void Unsubscribe(Action<string, string> handler) => DialogueReady -= handler;

        // =================== 요청 진입점 ===================

        /// <summary>
        /// NPC 대사 요청. LLM 불가 상황(미설정/한도 초과/busy)은 모두 규칙 폴백으로 즉시 이행 — 호출부 대기 없음.
        /// </summary>
        public void RequestDialogue(string npcKey, string systemPrompt, string userMessage)
        {
            // 큐: 동시 요청 1개 — 진행 중 재요청은 이전 응답을 보존하기 위해 무시
            if (_isBusy)
            {
                Debug.Log("[NPCDialogue] busy — 요청 무시: " + npcKey);
                return;
            }

            var cfg = _config ?? LLMConfig.Load();

            // 미설정 → 즉시 규칙 폴백(네트워크 호출 없음)
            if (cfg == null || string.IsNullOrEmpty(cfg.apiKey))
            {
                Emit(npcKey, FallbackReply(systemPrompt));
                return;
            }

            // 데일리 한도 초과 → 즉시 규칙 폴백(호출 안 함)
            int used = GetUsedCallsToday();
            if (!CanCallNow(used, cfg.maxDailyCalls))
            {
                Debug.Log("[NPCDialogue] 데일리 한도 도달(" + used + "/" + cfg.maxDailyCalls + ") — 규칙 폴백: " + npcKey);
                Emit(npcKey, FallbackReply(systemPrompt));
                return;
            }

            StartCoroutine(PostDialogueRoutine(npcKey, systemPrompt, userMessage, cfg));
        }

        private IEnumerator PostDialogueRoutine(string npcKey, string systemPrompt, string userMessage, LLMConfigData cfg)
        {
            _isBusy = true;
            IncrementDailyCalls();

            string content = null;
            // 주 모델 → backupModel 1회 재시도(빈 content = reasoning형 추정)
            string[] modelChain = { cfg.model, cfg.backupModel };
            foreach (string model in modelChain)
            {
                if (string.IsNullOrEmpty(model)) continue;

                string responseJson = null;
                yield return SendOnce(model, systemPrompt, userMessage, cfg, value => responseJson = value);

                if (responseJson == null) continue; // 네트워크/HTTP 실패 — backup으로 폴스루

                content = ExtractContent(responseJson);
                if (!string.IsNullOrEmpty(content)) break;
                Debug.LogWarning("[NPCDialogue] 빈 content(reasoning형 추정) — backup 재시도: " + model);
            }

            // 규칙 폴백 — ExtractContent 실패/빈 응답: 규칙 텍스트를 그대로 DialogueReady로 발화
            if (string.IsNullOrEmpty(content))
                content = FallbackReply(systemPrompt);

            _isBusy = false;
            Emit(npcKey, content);
        }

        private IEnumerator SendOnce(string model, string systemPrompt, string userMessage, LLMConfigData cfg, Action<string> onResult)
        {
            string body = BuildRequestBody(model, systemPrompt, userMessage, cfg);

            using (var request = new UnityWebRequest(cfg.baseUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.disposeUploadHandlerOnDispose = true;
                request.disposeDownloadHandlerOnDispose = true;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + cfg.apiKey);
                request.SetRequestHeader("HTTP-Referer", "https://poison.game"); // OpenRouter 권장 헤더
                request.SetRequestHeader("X-Title", "Poison");
                request.timeout = Mathf.Max(1, cfg.timeoutSeconds);

                yield return request.SendWebRequest();

                // 2xx는 Success로 온다 — HTTP 오류도 규칙 폴백 경로로 통일(null → backup/폴백)
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[NPCDialogue] 요청 실패(" + model + "): " + request.error);
                    onResult?.Invoke(null);
                    yield break;
                }
                onResult?.Invoke(request.downloadHandler.text);
            }
        }

        [Serializable]
        private class LLMMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class LLMRequestBody
        {
            // JsonUtility 필드명 = 와이어 포맷 그대로(max_tokens)
            public string model;
            public LLMMessage[] messages;
            public int max_tokens;
            public float temperature;
        }

        private static string BuildRequestBody(string model, string systemPrompt, string userMessage, LLMConfigData cfg)
        {
            var body = new LLMRequestBody
            {
                model = model,
                messages = new[]
                {
                    new LLMMessage { role = "system", content = systemPrompt },
                    new LLMMessage { role = "user", content = userMessage },
                },
                max_tokens = cfg.maxTokens,
                temperature = RequestTemperature,
            };
            return JsonUtility.ToJson(body);
        }

        // =================== 응답 파싱 (순수 — 네트워크 없음) ===================

        /// <summary>
        /// OpenRouter/챗완성 응답에서 choices[0].message.content 추출 — 순수 메서드(테스트 대상).
        /// JsonUtility 대신 수동 파서: provider마다 응답에 잡필드(error/usage/reasoning)가 섞여도 안전.
        /// 계약: content 키를 찾으면 빈 문자열이라도 반환(→ backup 재시도 트리거), 구조가 없으면 null(폴백 트리거).
        /// </summary>
        public static string ExtractContent(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            int choicesIdx = json.IndexOf("\"choices\"", StringComparison.Ordinal);
            if (choicesIdx < 0) return null;

            int objStart = json.IndexOf('{', choicesIdx);
            if (objStart < 0) return null;

            int objEnd = FindMatchingBrace(json, objStart);
            if (objEnd < 0) return null;

            int messageIdx = json.IndexOf("\"message\"", objStart, objEnd - objStart, StringComparison.Ordinal);
            if (messageIdx < 0) return null;

            int msgStart = json.IndexOf('{', messageIdx);
            if (msgStart < 0 || msgStart >= objEnd) return null;

            int msgEnd = FindMatchingBrace(json, msgStart);
            if (msgEnd < 0) return null;

            int contentIdx = json.IndexOf("\"content\"", msgStart, msgEnd - msgStart, StringComparison.Ordinal);
            if (contentIdx < 0) return null;

            int colon = json.IndexOf(':', contentIdx + "\"content\"".Length);
            if (colon < 0 || colon >= msgEnd) return null;

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return null;
            if (json[i] != '\"') return null; // 문자열 리터럴 외(null/숫자)는 content 없음 취급

            return ReadJsonString(json, i);
        }

        /// <summary>중괄호 짝 찾기 — 문자열 리스코프 처리(content 내 { } 무시).</summary>
        private static int FindMatchingBrace(string text, int openIdx)
        {
            int depth = 0;
            bool inString = false;
            int i = openIdx;
            while (i < text.Length)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\') { i += 2; continue; } // 이스케이프 2글자 건너뜀
                    if (c == '\"') inString = false;
                }
                else if (c == '\"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
                i++;
            }
            return -1;
        }

        /// <summary>JSON 문자열 리터럴 읽기(start는 여는 따옴표) — 이스케이프 언이스케이프.</summary>
        private static string ReadJsonString(string text, int start)
        {
            var sb = new StringBuilder();
            int i = start + 1;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\"') return sb.ToString();
                if (c == '\\')
                {
                    i++;
                    if (i >= text.Length) break;
                    char esc = text[i];
                    switch (esc)
                    {
                        case '\"': sb.Append('\"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < text.Length &&
                                int.TryParse(text.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }
            return sb.ToString(); // 닫는 따옴표 누락(비정상 JSON) — 있는 그대로 반환
        }

        // =================== 데일리 한도 ===================

        /// <summary>한도 판정 순수 함수 — 테스트 가능(PlayerPrefs 비접촉). used &lt; max만 허용.</summary>
        public static bool CanCallNow(int usedCalls, int maxCalls)
        {
            return usedCalls < maxCalls;
        }

        private static string TodayKey()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static int GetUsedCallsToday()
        {
            if (PlayerPrefs.GetString(DailyDateKey, "") != TodayKey()) return 0; // 날짜 바뀌면 리셋
            return PlayerPrefs.GetInt(DailyCallsKey, 0);
        }

        private static void IncrementDailyCalls()
        {
            int used = GetUsedCallsToday();
            PlayerPrefs.SetString(DailyDateKey, TodayKey());
            PlayerPrefs.SetInt(DailyCallsKey, used + 1);
            PlayerPrefs.Save();
        }

        // =================== 규칙 폴백 / 프롬프트 조립 (순수/결정론) ===================

        /// <summary>규칙 폴백 응답 — 네트워크 실패/빈 content/한도 초과 시 규칙 텍스트를 그대로 발화. 결정론.</summary>
        public static string FallbackReply(string systemPrompt)
        {
            // systemPrompt는 향후 확장 여지(말투 변주) — 현재는 고정 규칙 인사.
            return "무슨 일인지 말해보거라. 오늘도 영지는 평화롭구나.";
        }

        /// <summary>DB 미구성 폴백 프롬프트 — 고정 문자열(결정론, NullReference 없음).</summary>
        public static readonly string FallbackSystemPrompt =
            "너는 판타지 왕국의 무명 영주이다. 성격: 보통. 충성도: 50/100. 지병: 없음. 입맛: 특별한 것 없음. " +
            "능력치: STR 10 / DEX 10 / CON 10 / INT 10 / WIS 10 / CHA 10 | GUARD 10 | 합계 60. " +
            "성취사: 변방 기사 출신으로, 이름 없이 조용히 영지를 지켜왔다. " +
            "규칙: ①항상 영주 말투로 답한다 ②2~4문장 이내로 답한다 ③지병/입맛은 직접 물어봐야만 암시(먼저 말하지 않는다) ④거짓 정보 금지";

        /// <summary>
        /// 영주 시스템 프롬프트 조립 — 순수/결정론(네트워크 없음, 동일 입력 동일 출력).
        /// 재료: TerritoryDatabase.lord{이름/선호음식/지병/충성도/성격} + LordStatGenerator 6속성+GUARD + HeroicTaleGenerator 성취사.
        /// DB 미구성(정의 없음/빈 이름) → FallbackSystemPrompt 반환(예외 없음).
        /// </summary>
        public static string BuildLordSystemPrompt(string territoryId)
        {
            string key = string.IsNullOrEmpty(territoryId) ? "unknown" : territoryId;

            var defOpt = TerritoryDatabase.Instance?.GetDefinition(key); // 미등록 키 → 기본 struct(이름 null) + 경고 로그
            TerritoryDefinition def = defOpt.HasValue ? defOpt.Value : default;
            LordInfo lord = def.lord;

            if (string.IsNullOrEmpty(lord.lordName))
                return FallbackSystemPrompt; // DB 미구성(테스트/오타 키) — 폴백 기본 프롬프트

            var stats = LordStatGenerator.GenerateForTerritory(key); // 6속성+GUARD (결정론 — territoryId 시드)
            string tale = HeroicTaleGenerator.GetTale(key, def.nation);

            string disease = string.IsNullOrEmpty(lord.chronicDisease) ? "없음" : lord.chronicDisease;
            string food = string.IsNullOrEmpty(lord.preferredFood) ? "특별한 것 없음" : lord.preferredFood;

            var sb = new StringBuilder();
            sb.Append("너는 ").Append(RingKorean(def.difficulty))
              .Append("의 ").Append(NationKorean(def.nation))
              .Append(" 영주 ").Append(lord.lordName).AppendLine("이다.");
            sb.Append("영지 이름: ").Append(string.IsNullOrEmpty(def.territoryName) ? key : def.territoryName).AppendLine(".");
            sb.Append("성격: ").Append(PersonalityKorean(lord.personality)).AppendLine(".");
            sb.Append("충성도: ").Append(lord.loyalty).AppendLine("/100.");
            sb.Append("지병: ").Append(disease).AppendLine(".");
            sb.Append("입맛: ").Append(food).AppendLine(".");
            sb.Append("능력치: ").Append(stats.ToString()).AppendLine(); // "STR n / DEX n / ... | GUARD n | 합계 n"
            sb.Append("성취사: ").Append(tale).AppendLine();
            sb.AppendLine("규칙: ①항상 영주 말투로 답한다 ②2~4문장 이내로 답한다 ③지병/입맛은 직접 물어봐야만 암시(먼저 말하지 않는다) ④거짓 정보 금지");
            return sb.ToString();
        }

        /// <summary>영주 이름으로 프롬프트 재료 조회 — 대면창이 territoryId 없이 이름만 아는 경로용(성취사 임시 관례와 동일).</summary>
        public static string BuildLordSystemPromptForLord(string lordName)
        {
            if (string.IsNullOrEmpty(lordName)) return FallbackSystemPrompt;

            foreach (TerritoryDefinition def in TerritoryDatabase.Instance.GetAllDefinitions())
            {
                if (def.lord.lordName == lordName)
                    return BuildLordSystemPrompt(def.id.ToString());
            }
            return BuildLordSystemPrompt(lordName); // 이름 매칭 실패 → 이름을 키로(폴백 프롬프트 경로)
        }

        // =================== 한국어 라벨 변환 (결정론) ===================

        private static string NationKorean(NationType nation)
        {
            switch (nation)
            {
                case NationType.East: return "동부";
                case NationType.West: return "서부";
                case NationType.South: return "남부";
                case NationType.North: return "북부";
                case NationType.Empire: return "제국";
                default: return "무소속";
            }
        }

        private static string RingKorean(TerritoryDifficulty ring)
        {
            switch (ring)
            {
                case TerritoryDifficulty.Ring1: return "제1링";
                case TerritoryDifficulty.Ring2: return "제2링";
                case TerritoryDifficulty.Ring3: return "제3링";
                case TerritoryDifficulty.Ring4: return "제4링";
                case TerritoryDifficulty.Empire: return "제국";
                default: return "제2링";
            }
        }

        private static string PersonalityKorean(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Neutral: return "보통";
                case LordPersonality.Greedy: return "탐욕스러움";
                case LordPersonality.Suspicious: return "의심 많음";
                case LordPersonality.Brave: return "용감함";
                case LordPersonality.Cowardly: return "겁 많음";
                case LordPersonality.Wise: return "현명함";
                case LordPersonality.Cruel: return "잔인함";
                default: return "보통";
            }
        }

        // =================== 발화 ===================

        /// <summary>DialogueReady 발화 — 메인스레드 보장(코루틴 재개 지점). 테스트에서도 사용(순수 구독 계약).</summary>
        internal static void Emit(string npcKey, string llmText)
        {
            Debug.Log("[NPCDialogue] 발화: " + npcKey);
            DialogueReady?.Invoke(npcKey, llmText);
        }
    }
}
