using System.Collections.Generic;
using Entities;

/// <summary>논리 효과음 큐 목록. 실제 클립 경로와 재생 제한은 95_sfx_cues.yaml이 가진다.</summary>
public sealed class SfxCueDataList
{
    public int poolSize = 18;
    public float speedMergeFloor = 0.04f;
    public float speedMergeAt8x = 0.12f;
    public List<SfxCueData> cues = new();
}

/// <summary>한 논리 효과음이 무작위로 고를 후보와 재생 정책.</summary>
public sealed class SfxCueData
{
    public string name;
    public List<string> clips = new();
    public float volumeMin = 1f;
    public float volumeMax = 1f;
    public float pitchMin = 1f;
    public float pitchMax = 1f;
    public string mixerGroup = "SFX";
    public string voiceGroup;
    public int maxVoices = 2;
    public float minInterval = 0.04f;
    public bool speedMerge = true;
    /// <summary>후보가 둘 이상이면 직전에 실제 재생한 클립을 한 번 건너뛴다.</summary>
    public bool avoidImmediateRepeat;
    public float duckBgm = 1f;
    public float duckSeconds;
}

namespace Managers
{
    public enum SfxActionSlot
    {
        Normal,
        Passive,
        Ultimate,
        Special,
    }

    public enum SfxCuePhase
    {
        Cast,
        Draw,
        Projectile,
        Hit,
        Screen,
        Resolve,
    }

    /// <summary>
    /// 전투 코드는 숫자 ID를 유지하고, 소리는 사람이 읽는 논리 큐 이름으로 분리한다.
    /// 새 유닛을 만들 때 이 표와 YAML을 함께 늘리면 코드별 연출 타이밍은 공용 훅을 그대로 쓴다.
    /// </summary>
    public static class SfxCueRoutes
    {
        public static string Resolve(Unit unit, SfxActionSlot slot, SfxCuePhase phase, string codeName = null)
        {
            string prefix = Prefix(unit, slot, codeName);
            return string.IsNullOrWhiteSpace(prefix) ? null : $"{prefix}.{PhaseName(phase)}";
        }

        private static string Prefix(Unit unit, SfxActionSlot slot, string codeName)
        {
            if (unit == null) return null;
            if (unit.IsEnemy) return EnemyPrefix(unit, slot, codeName);

            return (unit.ID, slot) switch
            {
                (25, SfxActionSlot.Normal) => "orpheus.melody",
                (25, SfxActionSlot.Ultimate) => "orpheus.lament",
                (1, SfxActionSlot.Normal) => "sei.ground",
                (1, SfxActionSlot.Passive) => "sei.bigbang",
                (1, SfxActionSlot.Ultimate) => "sei.genesis",
                (2, SfxActionSlot.Normal) => "shi.destruction_slash",
                (2, SfxActionSlot.Passive) => "shi.song_of_ruin",
                (2, SfxActionSlot.Special) => "shi.song_of_ruin",
                (2, SfxActionSlot.Ultimate) => "shi.final_bell",
                (3, SfxActionSlot.Normal) => "sabah.lightning_slash",
                (3, SfxActionSlot.Passive) => "sabah.debuff_hunter",
                (3, SfxActionSlot.Ultimate) => "sabah.azrael",
                (7, SfxActionSlot.Normal) => "jean.holy_strike",
                (7, SfxActionSlot.Passive) => "jean.iron_sanction",
                (7, SfxActionSlot.Ultimate) => "jean.tension_rising",
                (20, SfxActionSlot.Normal) => "pygmalion.ember",
                (20, SfxActionSlot.Passive) => "pygmalion.galatea",
                (20, SfxActionSlot.Ultimate) => "pygmalion.sharp_thorns",
                (44, SfxActionSlot.Normal) => "himiko.ember",
                (44, SfxActionSlot.Passive) => "himiko.blaze",
                (44, SfxActionSlot.Ultimate) => "himiko.blaze_festival",
                (60, SfxActionSlot.Normal) => "chandra.sachi",
                (60, SfxActionSlot.Passive) => "chandra.nishakara",
                (60, SfxActionSlot.Ultimate) => "chandra.soma",
                (60, SfxActionSlot.Special) => "chandra.changya",
                (61, SfxActionSlot.Normal) => "yama.normal",
                (61, SfxActionSlot.Ultimate) => "yama.ultimate",
                (61, SfxActionSlot.Special) => "yama.special",
                (61, SfxActionSlot.Passive) => "yama.passive",
                (66, SfxActionSlot.Normal) => "varuna.normal",
                (66, SfxActionSlot.Ultimate) => "varuna.ultimate",
                (66, SfxActionSlot.Special) => "varuna.special",
                (66, SfxActionSlot.Passive) => "varuna.passive",
                (62, SfxActionSlot.Normal) => "agni.normal",
                (62, SfxActionSlot.Passive) => "agni.follow_up",
                (62, SfxActionSlot.Ultimate) => "agni.ultimate",
                (62, SfxActionSlot.Special) => "agni.special_setup",
                (160, SfxActionSlot.Normal) => "marie.normal",
                (160, SfxActionSlot.Ultimate) => "marie.ultimate",
                (161, SfxActionSlot.Normal) => "napoleon.normal",
                (161, SfxActionSlot.Special) => "napoleon.volley",
                (161, SfxActionSlot.Ultimate) => "napoleon.ultimate",
                (81, SfxActionSlot.Normal) => "surtr.normal",
                (81, SfxActionSlot.Ultimate) => "surtr.ultimate",
                (82, SfxActionSlot.Normal) => "freya.normal",
                (80, SfxActionSlot.Normal) => "loki.normal",
                (80, SfxActionSlot.Passive) => "loki.fenrir",
                (80, SfxActionSlot.Ultimate) => "loki.ultimate",
                (902, SfxActionSlot.Normal) => "loki.fenrir_claw",
                (45, SfxActionSlot.Normal) => "tsukuyomi.normal",
                (45, SfxActionSlot.Ultimate) => "tsukuyomi.ultimate",
                (67, SfxActionSlot.Special) => "surya.special",
                (67, SfxActionSlot.Normal) => "surya.normal",
                (67, SfxActionSlot.Passive) => "surya.follow_up",
                (67, SfxActionSlot.Ultimate) => "surya.ultimate",
                (63, SfxActionSlot.Normal) => "indra.thunderbolt",
                (63, SfxActionSlot.Passive) => "indra.deva_king",
                (63, SfxActionSlot.Special) => "indra.vajra",
                (63, SfxActionSlot.Ultimate) => "indra.gathering",
                (64, SfxActionSlot.Normal) => "vayu.gust",
                (64, SfxActionSlot.Passive) => "vayu.tailwind",
                (64, SfxActionSlot.Special) => "vayu.typhoon_eye",
                (64, SfxActionSlot.Ultimate) => "vayu.storm",
                (200, SfxActionSlot.Normal) => "mansa.normal",
                (200, SfxActionSlot.Ultimate) => "mansa.ultimate",
                (220, SfxActionSlot.Normal) => "ishtar.normal",
                (220, SfxActionSlot.Ultimate) => "ishtar.ultimate",
                (65, SfxActionSlot.Normal) => "kubera.coin_toss",
                (65, SfxActionSlot.Passive) => "kubera.patronage",
                (65, SfxActionSlot.Special) => "kubera.gold_rush",
                (65, SfxActionSlot.Ultimate) => "kubera.treasury",
                (121, SfxActionSlot.Normal) => "anubis.heart_scale",
                (121, SfxActionSlot.Passive) => "anubis.soul",
                (121, SfxActionSlot.Ultimate) => "anubis.book",
                (180, SfxActionSlot.Normal) => "uchi.clone",
                (180, SfxActionSlot.Passive) => "uchi.guard",
                (180, SfxActionSlot.Ultimate) => "uchi.manchunhwau",
                (901, SfxActionSlot.Normal) => "uchi.strike",
                (122, SfxActionSlot.Normal) => "bastet.cat_punch",
                (122, SfxActionSlot.Passive) => "bastet.guard",
                (122, SfxActionSlot.Ultimate) => "bastet.one_more_hit",
                _ => null,
            };
        }

        private static string EnemyPrefix(Unit unit, SfxActionSlot slot, string codeName)
        {
            if (unit.HasUnitTag("RestoredTheme"))
            {
                var kit = Codes.Restored.RestoredEnemyKit.Find(unit);
                string mode = slot == SfxActionSlot.Ultimate ? kit?.ultimateMode : kit?.normalMode;
                if (Codes.Restored.RestoredEnemyKit.Support(mode)) return "orpheus.lament";
                return unit.VfxClass == "mage" ? "enemy.void_boss" : unit.VfxClass == "archer"
                    ? "enemy.void_marksman.shot" : "enemy.void_wolf.charge";
            }
            if (slot == SfxActionSlot.Normal)
            {
                if (codeName == "돌격" && (unit.HasUnitTag("Giant") || unit.HasUnitTag("Monstrous")))
                    return "enemy.void_beast.strike";
                if (codeName == "돌격") return "enemy.void_wolf.charge";
                if (codeName == "사격" && string.Equals(unit.UnitTier, "boss", System.StringComparison.OrdinalIgnoreCase))
                    return "enemy.void_boss";
                if (codeName == "사격") return "enemy.void_marksman.shot";
            }
            if (slot == SfxActionSlot.Ultimate)
            {
                if (codeName == "연사") return "enemy.void_marksman.shot";
                if ((codeName == "강타" || codeName == "대지 분쇄") &&
                    (unit.HasUnitTag("Giant") || unit.HasUnitTag("Monstrous")))
                    return "enemy.void_beast.strike";
                if (codeName == "강타" || codeName == "대지 분쇄") return "enemy.void_wolf.charge";
                if (codeName == "공허의 숨결") return "enemy.void_boss";
            }
            return null;
        }

        private static string PhaseName(SfxCuePhase phase) => phase switch
        {
            SfxCuePhase.Cast => "cast",
            SfxCuePhase.Draw => "draw",
            SfxCuePhase.Projectile => "projectile",
            SfxCuePhase.Hit => "hit",
            SfxCuePhase.Screen => "screen",
            SfxCuePhase.Resolve => "resolve",
            _ => "hit",
        };
    }
}
