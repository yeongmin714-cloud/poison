#pragma warning disable 0414
﻿using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Core
{
    /// <summary>
    /// 몬스터 티어 (초반/중반/후반)
    /// </summary>
    public enum MonsterTier
    {
        Beginner,   // 초반 🟢
        Intermediate, // 중반 🟡
        Advanced    // 후반 🔴
    }

    /// <summary>
    /// C18-01: 주야간 출현 시간
    /// </summary>
    public enum ActiveTime
    {
        Day,
        Night,
        Both
    }

    /// <summary>
    /// 단일 몬스터 정의
    /// </summary>
    [System.Serializable]
    public class MonsterDef
    {
        public string id;
        public string displayName;
        public MonsterTier tier;
        public float baseHP;
        public int baseDamage;
        public float baseSpeed;
        public Color gizmoColor;
        public string description;

        /// <summary>C18-01: 주야 출현 시간</summary>
        public ActiveTime activeTime = ActiveTime.Both;

        public GameObject hitEffectPrefab;
        public GameObject deathEffectPrefab;
        public AudioClip hitSound;
        public AudioClip deathSound;

        public MonsterDef(string id, string name, MonsterTier tier,
            float hp, int damage, float speed, Color color, string desc = "", ActiveTime active = ActiveTime.Both)
        {
            this.id = id;
            this.displayName = name;
            this.tier = tier;
            this.baseHP = hp;
            this.baseDamage = damage;
            this.baseSpeed = speed;
            this.gizmoColor = color;
            this.description = desc;
            this.activeTime = active;
            LoadEffects(id);
        }

        private void LoadEffects(string monsterId)
        {
            hitEffectPrefab = Resources.Load<GameObject>($"Effects/{monsterId}_hit");
            deathEffectPrefab = Resources.Load<GameObject>($"Effects/{monsterId}_death");
            hitSound = Resources.Load<AudioClip>($"Effects/{monsterId}_hit");
            deathSound = Resources.Load<AudioClip>($"Effects/{monsterId}_death");
        }
    }

    /// <summary>
    /// GAME_DATA.md v2.0 기반 몬스터 22종 데이터 저장소
    /// </summary>
    public static class MonsterDatabase
    {
        private static Dictionary<string, MonsterDef> _all = null;

        public static IReadOnlyDictionary<string, MonsterDef> All
        {
            get
            {
                if (_all == null) Init();
                return _all;
            }
        }

        public static MonsterDef Get(string id)
        {
            if (_all == null) Init();
            return _all.TryGetValue(id, out var def) ? def : null;
        }

        public static List<MonsterDef> GetByTier(MonsterTier tier)
        {
            if (_all == null) Init();
            var result = new List<MonsterDef>();
            foreach (var kv in _all)
                if (kv.Value.tier == tier)
                    result.Add(kv.Value);
            return result;
        }

        /// <summary>
        /// C18-01: ActiveTime으로 몬스터 필터링 (Both는 주야간 모두 포함)
        /// </summary>
        public static List<MonsterDef> GetByActiveTime(ActiveTime time)
        {
            if (_all == null) Init();
            var result = new List<MonsterDef>();
            foreach (var kv in _all)
            {
                if (kv.Value.activeTime == ActiveTime.Both || kv.Value.activeTime == time)
                    result.Add(kv.Value);
            }
            return result;
        }

        public static void Init()
        {
            _all = new Dictionary<string, MonsterDef>();

            // ===== 초반 몬스터 (Beginner) 🟢 =====
            _all["rabbit"]     = new MonsterDef("rabbit",     "토끼",     MonsterTier.Beginner, 10f,  3, 4f,  new Color(0.6f, 1f, 0.6f), "기본 사냥감. 도망만 다닌다.", ActiveTime.Day);
            _all["wolf"]       = new MonsterDef("wolf",       "늑대",     MonsterTier.Beginner, 20f,  5, 5f,  new Color(0.7f, 0.9f, 0.7f), "무리지어 추격한다. 초반 위협.", ActiveTime.Day);
            _all["boar"]       = new MonsterDef("boar",       "멧돼지",   MonsterTier.Beginner, 25f,  7, 4f,  new Color(0.8f, 1f, 0.6f), "돌진 공격. 방심하면 위험.", ActiveTime.Day);
            _all["deer"]       = new MonsterDef("deer",       "사슴",     MonsterTier.Beginner, 15f,  2, 6f,  new Color(0.5f, 1f, 0.5f), "도망치는 사슴. 고급 고기 제공.", ActiveTime.Day);
            _all["poison_snake"] = new MonsterDef("poison_snake", "독뱀", MonsterTier.Beginner, 18f,  8, 3f,  new Color(0.4f, 0.9f, 0.4f), "독 공격. 방치하면 위험.", ActiveTime.Day);
            _all["bat"]        = new MonsterDef("bat",        "박쥐",     MonsterTier.Beginner, 12f,  4, 5f,  new Color(0.6f, 0.8f, 0.6f), "밤에 더 활발. 날개 재료.", ActiveTime.Night);
            _all["giant_rat"]  = new MonsterDef("giant_rat",  "거대쥐",   MonsterTier.Beginner, 15f,  5, 4f,  new Color(0.5f, 0.7f, 0.5f), "오염된 지역에서 서식.");
            _all["crow"]       = new MonsterDef("crow",       "까마귀",   MonsterTier.Beginner, 10f,  3, 6f,  new Color(0.3f, 0.6f, 0.3f), "멀리서 관찰. 깃털 재료.", ActiveTime.Day);

            // ===== 중반 몬스터 (Intermediate) 🟡 =====
            _all["slime"]      = new MonsterDef("slime",      "점액괴물(슬라임)", MonsterTier.Intermediate, 50f,  8, 3f,  new Color(1f, 0.9f, 0.4f), "끈적한 점액. 접근하면 산성 공격.", ActiveTime.Night);
            _all["stone_golem"]= new MonsterDef("stone_golem","돌골렘",   MonsterTier.Intermediate, 80f, 12, 2f,  new Color(1f, 0.8f, 0.3f), "단단한 방어력. 천천히 다가온다.", ActiveTime.Night);
            _all["fire_lizard"]= new MonsterDef("fire_lizard","화염도마뱀", MonsterTier.Intermediate, 55f, 15, 4f, new Color(1f, 0.6f, 0.2f), "화염 공격. 불꽃 재료.");
            _all["electric_porcupine"] = new MonsterDef("electric_porcupine", "전기가시고슴도치", MonsterTier.Intermediate, 60f, 10, 3f, new Color(1f, 0.9f, 0.1f), "전기 가시. 접근하면 감전.");
            _all["swamp_croc"] = new MonsterDef("swamp_croc", "늪지악어", MonsterTier.Intermediate, 90f, 18, 3f, new Color(0.7f, 0.9f, 0.3f), "늪지에 숨어 있다가 덤벼든다.");
            _all["forest_spirit"] = new MonsterDef("forest_spirit", "숲정령", MonsterTier.Intermediate, 70f,  9, 5f, new Color(0.3f, 1f, 0.6f), "빛나는 이끼. 치료 재료 드랍.");
            _all["wild_troll"]= new MonsterDef("wild_troll", "야생트롤", MonsterTier.Intermediate, 100f, 20, 3f, new Color(0.8f, 0.7f, 0.2f), "강력한 재생력. 오래 싸워야 한다.");

            // ===== 후반 몬스터 (Advanced) 🔴 =====
            _all["ogre"]       = new MonsterDef("ogre",       "오우거",   MonsterTier.Advanced, 200f, 25, 3f,  new Color(1f, 0.3f, 0.3f), "거대한 덩치. 강력한 타격.");
            _all["banshee"]    = new MonsterDef("banshee",    "밴시",     MonsterTier.Advanced, 150f, 20, 5f,  new Color(0.8f, 0.2f, 0.8f), "영혼 공격. 정신력에 타격.", ActiveTime.Night);
            _all["griffin"]    = new MonsterDef("griffin",    "그리폰",   MonsterTier.Advanced, 180f, 22, 7f,  new Color(1f, 0.8f, 0.2f), "하늘에서 덤벼든다. 빠르다.");
            _all["minotaur"]   = new MonsterDef("minotaur",   "미노타우로스", MonsterTier.Advanced, 250f, 30, 3f, new Color(0.9f, 0.3f, 0.1f), "미궁의 수호자. 강력한 돌진.");
            _all["manticore"]  = new MonsterDef("manticore",  "만티코어", MonsterTier.Advanced, 220f, 28, 5f, new Color(1f, 0.4f, 0.0f), "독침 공격. 즉사급 위험.");
            _all["salamander"] = new MonsterDef("salamander", "샐러맨더", MonsterTier.Advanced, 170f, 24, 4f, new Color(1f, 0.5f, 0.0f), "용의 혈통. 화염 저항.");
            _all["shadow_assassin"] = new MonsterDef("shadow_assassin", "그림자암살자", MonsterTier.Advanced, 160f, 35, 8f, new Color(0.1f, 0.1f, 0.1f), "암살자. 은신 후 기습.", ActiveTime.Night);
        }

        /// <summary>
        /// 티어에 따른 HP 범위 문자열 (디버그/UI용)
        /// </summary>
        public static string GetTierLabel(MonsterTier tier)
        {
            return tier switch
            {
                MonsterTier.Beginner     => "🟢 초반",
                MonsterTier.Intermediate => "🟡 중반",
                MonsterTier.Advanced     => "🔴 후반",
                _ => "❓"
            };
        }
    }
}
