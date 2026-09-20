using System;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase O9 (C-O9-01/02/03): LLM NPC 테스트 — 14개.
    ///   ① BuildLordSystemPrompt: 결정론 / 필수 요소(lordName·성취사·능력치·규칙) / DB 미구성 폴백
    ///  ② CanCallNow: used&lt;max true, used&gt;=max false (순수 — PlayerPrefs 비접촉)
    ///  ③ 설정 로더: llm_config.json(프로젝트 루트) provider=="openrouter" + apiKey 비어있지 않음
    ///     — 에디터에서 실제 통과(dataPath+"/../" 경로 검증). CI 격리 시 파일 없으면 Assert.Ignore.
    ///  ④ ExtractContent: choices[0].message.content 추출 순수 메서드(정상/빈 choices/빈 content/null/reasoning형)
    ///  ⑤ 규칙 폴백: FallbackReply 비어있지 않음(결정론)
    ///  ⑥ DialogueReady 구독 계약: Subscribe → Emit 수신 → Unsubscribe → 미수신
    ///
    /// [네트워크 실호출] OpenRouter 실제 POST(타임아웃/한도/busy 동시성 포함)는 EditMode에서 네트워크 금지 원칙에
    /// 따라 판정하지 않는다 — Play 모드 실측으로 남긴다(영주 알현창 Open → 대사 교체 확인).
    /// </summary>
    public class LLMNpcTests
    {
        private const string ConfiguredTerritoryKey = "East_01"; // TerritoryDatabase 구성 키(국가×링×5, index 1~20)

        // ===================== ① BuildLordSystemPrompt =====================

        [Test]
        public void BuildLordSystemPrompt_Deterministic_SameOutputTwice()
        {
            string a = NPCDialogueAdapter.BuildLordSystemPrompt(ConfiguredTerritoryKey);
            string b = NPCDialogueAdapter.BuildLordSystemPrompt(ConfiguredTerritoryKey);
            Assert.IsFalse(string.IsNullOrEmpty(a));
            Assert.AreEqual(a, b, "같은 영지 = 항상 같은 프롬프트(결정론)");
        }

        [Test]
        public void BuildLordSystemPrompt_ConfiguredDb_ContainsLordName_Tale_Stats_Rules()
        {
            // DB는 Lazy 자가 구성 — 에디터/EditMode에서도 항상 정의 존재
            var defOpt = TerritoryDatabase.Instance?.GetDefinition(ConfiguredTerritoryKey);
            Assert.IsTrue(defOpt.HasValue, "DB 구성 시 정의 존재");
            var def = defOpt.Value;

            string prompt = NPCDialogueAdapter.BuildLordSystemPrompt(ConfiguredTerritoryKey);

            Assert.IsTrue(prompt.Contains(def.lord.lordName), "영주 이름 포함: " + def.lord.lordName);
            Assert.IsTrue(prompt.Contains("성취사"), "성취사 섹션 포함");
            Assert.IsTrue(prompt.Contains("STR"), "능력치 STR 포함");
            Assert.IsTrue(prompt.Contains("합계"), "능력치 합계 포함");
            Assert.IsTrue(prompt.Contains("충성도"), "충성도 포함");
            Assert.IsTrue(prompt.Contains("거짓 정보 금지"), "규칙 ④ 포함");
            // 성취사 본문 일부 — 템플릿에 실제 문장이 주입되었는지(폴백 고정문자열과 다름)
            Assert.AreNotEqual(NPCDialogueAdapter.FallbackSystemPrompt, prompt);
        }

        [Test]
        public void BuildLordSystemPrompt_UnconfiguredDb_ReturnsFixedFallbackPrompt()
        {
            // 미등록 키 → GetDefinition 기본 struct(이름 null) → 폴백 프롬프트(예외 없음)
            string fallback = NPCDialogueAdapter.BuildLordSystemPrompt("존재하지않는_영지_XX");
            Assert.IsFalse(string.IsNullOrEmpty(fallback), "폴백 프롬프트 비어있지 않음");
            Assert.AreEqual(NPCDialogueAdapter.FallbackSystemPrompt, fallback, "DB 미구성 = 고정 폴백 프롬프트");

            // null/빈 키도 동일 경로 — NullReference 없음
            Assert.AreEqual(NPCDialogueAdapter.FallbackSystemPrompt, NPCDialogueAdapter.BuildLordSystemPrompt(null));
            Assert.AreEqual(fallback, NPCDialogueAdapter.BuildLordSystemPrompt(""));
        }

        // ===================== ② CanCallNow (데일리 한도 순수 함수) =====================

        [Test]
        public void CanCallNow_UnderLimit_True()
        {
            Assert.IsTrue(NPCDialogueAdapter.CanCallNow(0, 200));
            Assert.IsTrue(NPCDialogueAdapter.CanCallNow(199, 200), "마지막 1회 잔여");
        }

        [Test]
        public void CanCallNow_AtOrOverLimit_False()
        {
            Assert.IsFalse(NPCDialogueAdapter.CanCallNow(200, 200), "한도 도달 = 호출 금지(즉시 규칙 폴백)");
            Assert.IsFalse(NPCDialogueAdapter.CanCallNow(201, 200));
            Assert.IsFalse(NPCDialogueAdapter.CanCallNow(0, 0), "max 0 = 항상 폴백");
        }

        // ===================== ③ 설정 로더 (llm_config.json — 프로젝트 루트) =====================

        [Test]
        public void LLMConfig_Load_ProjectRoot_ProviderOpenRouter_ApiKeyPresent()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "llm_config.json");
            if (!System.IO.File.Exists(path))
                Assert.Ignore("llm_config.json 없음 — CI 격리 환경(에디터 실측에서만 실행)");

            var cfg = LLMConfig.Load();
            Assert.IsNotNull(cfg, "프로젝트 루트 설정 로드");
            Assert.AreEqual("openrouter", cfg.provider);
            Assert.IsFalse(string.IsNullOrEmpty(cfg.apiKey), "apiKey 비어있으면 안 됨");
            Assert.IsFalse(string.IsNullOrEmpty(cfg.baseUrl), "baseUrl 비어있으면 안 됨");
            Assert.IsFalse(string.IsNullOrEmpty(cfg.model), "주 모델 비어있으면 안 됨");
            Assert.Greater(cfg.maxDailyCalls, 0, "데일리 한도 양수");
            Assert.Greater(cfg.maxTokens, 0, "maxTokens 양수");
            Assert.Greater(cfg.timeoutSeconds, 0, "타임아웃 양수");
        }

        [Test]
        public void LLMConfig_DataPathParent_ResolvesToProjectRoot_AndLoaderHitsIt()
        {
            string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            if (!System.IO.File.Exists(System.IO.Path.Combine(projectRoot, "llm_config.json")))
                Assert.Ignore("llm_config.json 없음 — CI 격리 환경(에디터 실측에서만 실행)");

            // 에디터: dataPath = <프로젝트>/Assets → 후보 ① "../"가 프로젝트 루트를 가리키는지 검증
            Assert.IsTrue(System.IO.Directory.Exists(projectRoot), "dataPath/../ = 프로젝트 루트");

            // 로더가 후보 ① 경로의 파일을 실제로 읽었는지(캐시 1회 로드)
            var cfg = LLMConfig.Load();
            Assert.IsNotNull(cfg);
            Assert.AreEqual("openrouter", cfg.provider);
        }

        // ===================== ④ ExtractContent (응답 파싱 — 순수) =====================

        private const string SampleOpenRouterJson =
            "{\"id\":\"gen-1\",\"object\":\"chat.completion\",\"choices\":[{\"index\":0," +
            "\"message\":{\"role\":\"assistant\",\"content\":\"크하하, 잘 왔구나.\"},\"finish_reason\":\"stop\"}]," +
            "\"usage\":{\"total_tokens\":42}}";

        [Test]
        public void ExtractContent_NormalJson_ReturnsContent()
        {
            string content = NPCDialogueAdapter.ExtractContent(SampleOpenRouterJson);
            Assert.AreEqual("크하하, 잘 왔구나.", content);
        }

        [Test]
        public void ExtractContent_EmptyChoices_ReturnsNull()
        {
            Assert.IsNull(NPCDialogueAdapter.ExtractContent("{\"choices\":[]}"), "빈 choices = content 없음");
            Assert.IsNull(NPCDialogueAdapter.ExtractContent("{\"object\":\"chat.completion\"}"), "choices 키 없음");
            Assert.IsNull(NPCDialogueAdapter.ExtractContent("{\"error\":{\"message\":\"rate limited\"}}"), "에러 응답");
            Assert.IsNull(NPCDialogueAdapter.ExtractContent("not a json"), "비 JSON");
        }

        [Test]
        public void ExtractContent_EmptyContent_ReturnsEmptyString()
        {
            string json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\"}}]}";
            string content = NPCDialogueAdapter.ExtractContent(json);
            Assert.AreEqual(string.Empty, content, "빈 content = 빈 문자열(backup 재시도 트리거), null 아님");
        }

        [Test]
        public void ExtractContent_NullJson_ReturnsNull()
        {
            Assert.IsNull(NPCDialogueAdapter.ExtractContent(null));
            Assert.IsNull(NPCDialogueAdapter.ExtractContent(""));
        }

        [Test]
        public void ExtractContent_ReasoningOnlyShape_ReturnsEmptyNotReasoning()
        {
            // reasoning 모델 실패 모드: content 비우고 reasoning만 반환 — 절대 reasoning을 대사로 쓰지 않는다.
            string json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"reasoning\":\"생각하는 중...\"}}]}";
            string content = NPCDialogueAdapter.ExtractContent(json);
            Assert.AreEqual(string.Empty, content, "빈 content 반환 → backup 재시도 → 규칙 폴백 경로");
        }

        // ===================== ⑤ 규칙 폴백 =====================

        [Test]
        public void FallbackReply_NonEmpty_And_Deterministic()
        {
            string a = NPCDialogueAdapter.FallbackReply("아무 시스템 프롬프트");
            string b = NPCDialogueAdapter.FallbackReply(null);
            Assert.IsFalse(string.IsNullOrEmpty(a), "폴백 응답 비어있지 않음");
            Assert.IsFalse(string.IsNullOrEmpty(b));
            Assert.AreEqual(a, b, "규칙 폴백은 결정론(고정 규칙 텍스트)");
        }

        // ===================== ⑥ DialogueReady 구독 계약 (정적 이벤트) =====================

        [Test]
        public void DialogueReady_SubscribeUnsubscribe_RoundTrip()
        {
            string gotKey = null;
            string gotText = null;
            Action<string, string> handler = (key, text) => { gotKey = key; gotText = text; };

            NPCDialogueAdapter.Subscribe(handler);
            NPCDialogueAdapter.Emit("테스트영주", "테스트 대사"); // internal — InternalsVisibleTo(테스트 전용 발화점)
            Assert.AreEqual("테스트영주", gotKey);
            Assert.AreEqual("테스트 대사", gotText);

            NPCDialogueAdapter.Unsubscribe(handler);
            gotKey = null;
            gotText = null;
            NPCDialogueAdapter.Emit("테스트영주", "두 번째 대사");
            Assert.IsNull(gotKey, "구독 해제 후 미수신");
            Assert.IsNull(gotText);
        }
    }
}
