#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Core;
using Effects.Base;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class DebugVerification
    {
        private IEnumerator NewEquipmentItems()
        {
            Status = "신규 장비/조건부 드랍 검증";

            int[] itemIds = { 4510, 4328, 4329, 4330, 4331, 4332, 4411, 4511, 4333, 4512 };
            int[] codeIds = { 445, 446, 447, 448, 449, 450, 451, 452, 453, 454 };
            var items = game.itemDataList.items.Where(item => itemIds.Contains(item.id)).ToList();
            Equal("new equipment count", itemIds.Length, items.Count);
            Assert("new equipment IDs", new HashSet<int>(itemIds).SetEquals(items.Select(item => item.id)),
                string.Join(",", itemIds), string.Join(",", items.Select(item => item.id)));
            Assert("new equipment code grants", new HashSet<int>(codeIds).SetEquals(
                    items.SelectMany(item => item.codeGrants ?? new List<EquipmentCodeGrant>()).Select(grant => grant.codeId)),
                string.Join(",", codeIds),
                string.Join(",", items.SelectMany(item => item.codeGrants ?? new List<EquipmentCodeGrant>()).Select(grant => grant.codeId)));
            Assert("new equipment is in common reward pool",
                itemIds.All(id => game.dataManager.FetchRewardDataList().commonDropItemIds.Contains(id)),
                "all 10", "missing reward ID");

            ItemData Spec(int id) => items.First(item => item.id == id);
            bool HasSecondary(int id, EquipmentSecondaryStat stat, int amount) =>
                Spec(id).statBonuses?.Any(bonus => bonus.Secondary == stat && bonus.amount == amount) == true;
            bool HasPrimary(int id, PrimaryStat stat, int amount) =>
                Spec(id).statBonuses?.Any(bonus => bonus.TryGetPrimary(out PrimaryStat primary) &&
                                                  primary == stat && bonus.amount == amount) == true;
            Assert("Saint Catherine sword metadata",
                Spec(4510).rarity == 5 && Spec(4510).category == "Longsword" &&
                HasSecondary(4510, EquipmentSecondaryStat.CritDamage, 18),
                "T5 Longsword CritDamage+18", "mismatch");
            Assert("Salvation banner metadata",
                Spec(4328).rarity == 3 && Spec(4328).category == "Spear" && Spec(4328).twoHanded &&
                HasPrimary(4328, PrimaryStat.INT, 5),
                "T3 Spear INT+5", "mismatch");
            Assert("consumable necklace metadata",
                new[] { 4329, 4330 }.All(id => Spec(id).rarity == 3 && Spec(id).category == "Necklace") &&
                HasPrimary(4329, PrimaryStat.INT, 5) &&
                HasSecondary(4330, EquipmentSecondaryStat.Durability, 10),
                "T3 necklaces INT+5/Durability+10", "mismatch");
            Assert("all-stat equipment metadata",
                new[] { 4331, 4333 }.All(id => Enum.GetValues(typeof(PrimaryStat)).Cast<PrimaryStat>()
                    .All(stat => HasPrimary(id, stat, 5))),
                "five stats +5", "mismatch");
            Assert("conditional ring and necklace metadata",
                Spec(4332).rarity == 3 && Spec(4332).category == "Ring" && HasPrimary(4332, PrimaryStat.CON, 5) &&
                Spec(4411).rarity == 4 && Spec(4411).category == "Necklace" &&
                HasSecondary(4411, EquipmentSecondaryStat.CritDamage, 14),
                "Kitty Hawk/alternator specs", "mismatch");
            Assert("winter triangle metadata",
                Spec(4511).rarity == 5 && Spec(4511).category == "Necklace" &&
                HasSecondary(4511, EquipmentSecondaryStat.CritDamage, 18),
                "T5 Necklace CritDamage+18", "mismatch");
            Assert("Golden Fleece metadata",
                Spec(4512).rarity == 5 && Spec(4512).category == "MediumArmor" &&
                Spec(4512).durability == 65 && HasPrimary(4512, PrimaryStat.LUK, 9),
                "T5 MediumArmor Durability65 LUK+9", "mismatch");

            foreach (int codeId in codeIds)
            {
                PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext());
                Assert($"new equipment code {codeId}", code != null, "created", code?.GetType().Name ?? "null");
            }

            // 조건부 공용 풀: 덱 전용, 해금 전용, 덱 또는 해금의 세 경우를 분리한다.
            var write = typeof(SaveSystem).GetMethod("WriteString", BindingFlags.Static | BindingFlags.NonPublic);
            write?.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });
            Clear();
            ItemData sword = items.First(item => item.id == 4510);
            ItemData banner = items.First(item => item.id == 4328);
            ItemData kitty = items.First(item => item.id == 4332);
            ItemData alternator = items.First(item => item.id == 4411);
            Assert("conditional gear starts hidden",
                !RewardManager.IsAvailableForRoster(sword) &&
                !RewardManager.IsAvailableForRoster(banner) &&
                !RewardManager.IsAvailableForRoster(kitty) &&
                !RewardManager.IsAvailableForRoster(alternator),
                "all hidden", "leaked");

            Unit jean = grid.SpawnUnit(-1, 1, false, 7);
            Assert("Saint Catherine sword opens for Jean roster", RewardManager.IsAvailableForRoster(sword),
                "available", "hidden");
            Assert("Salvation banner still needs Jean unlock", !RewardManager.IsAvailableForRoster(banner),
                "hidden", "available");
            Clear();
            SaveSystem.AddStarterUnlock(7);
            Assert("Salvation banner opens for Jean unlock", RewardManager.IsAvailableForRoster(banner),
                "available", "hidden");
            Unit light = grid.SpawnUnit(-1, 1, false, 5);
            Assert("Kitty Hawk opens for Light roster", RewardManager.IsAvailableForRoster(kitty),
                "available", "hidden");
            Clear();
            SaveSystem.AddStarterUnlock(6);
            Assert("alternator opens for Nicole unlock", RewardManager.IsAvailableForRoster(alternator),
                "available", "hidden");
            write?.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });

            // 정복 — 실제 처치 이벤트 뒤 물리 배율이 10% 올라간다.
            Clear();
            Unit theseus = grid.SpawnUnit(-1, 1, false, 23);
            Assert("equip Saint Catherine sword", theseus != null && theseus.TryEquipItem(4510, out _),
                "equipped", "failed");
            RoundStart(theseus);
            KillStackTaggedDamageEffect conquest = EffectOf<KillStackTaggedDamageEffect>(theseus);
            var physical = new DamageContext(theseus, 100, CodeType.Normal,
                new List<int> { DamageTag.Physical });
            Near("Conquest starts neutral", 1f, conquest?.OutgoingDamageModifier(theseus, null, physical) ?? 0f);
            theseus.Invoke(UnitEventType.OnKill, new EventContext(theseus));
            Near("Conquest gains 10% per kill", 1.10f,
                conquest?.OutgoingDamageModifier(theseus, null, physical) ?? 0f);

            // 포격 — 시전당 체력 비용은 한 번만 지불한다.
            Clear();
            Unit freya = grid.SpawnUnit(-1, 1, false, 81);
            Assert("equip Salvation banner", freya != null && freya.TryEquipItem(4328, out _),
                "equipped", "failed");
            RoundStart(freya);
            BombardMeEffect bombard = EffectOf<BombardMeEffect>(freya);
            int hpBeforeBombard = freya.HpCurr;
            Near("Bombard Me ultimate multiplier", 1.25f,
                bombard?.PrepareUltimateDamageMultiplier(freya) ?? 0f);
            Equal("Bombard Me spends 5% max HP",
                Mathf.Max(1, Mathf.RoundToInt(freya.HpMax * 0.05f)), hpBeforeBombard - freya.HpCurr);

            // 수호부 — 장비가 사라져도 시작된 2턴 부활은 유지된다.
            Clear();
            Unit guardian = SpawnHero();
            Unit attacker = SpawnEnemy(1062, 90);
            Assert("equip guardian talisman", guardian.TryEquipItem(4329, out _), "equipped", "failed");
            RoundStart(guardian);
            guardian.TakeDamage(new DamageContext(attacker, guardian.HpMax * 100, CodeType.Normal,
                new List<int> { DamageTag.Physical }));
            Assert("guardian talisman prevents lethal hit", guardian.isActive && guardian.HpCurr >= 1,
                "alive", "dead");
            yield return null;
            Assert("guardian talisman is destroyed", !guardian.IsEquipped(4329), "destroyed", "equipped");
            guardian.BeginTurn();
            guardian.EndTurn();
            guardian.BeginTurn();
            guardian.EndTurn();
            Equal("guardian talisman completes delayed full revive", guardian.HpMax, guardian.HpCurr);

            // 천상의 과일 — 임계선 진입 후 50% 회복하고 소모된다.
            Clear();
            Unit fruit = SpawnHero();
            Assert("equip heavenly fruit", fruit.TryEquipItem(4330, out _), "equipped", "failed");
            RoundStart(fruit);
            int fruitLowHp = Mathf.FloorToInt(fruit.HpMax * 0.20f);
            fruit.ModifyHp(fruitLowHp);
            Assert("heavenly fruit heals at least 50% max HP",
                fruit.HpCurr >= fruitLowHp + Mathf.RoundToInt(fruit.HpMax * 0.50f),
                $">={fruitLowHp + Mathf.RoundToInt(fruit.HpMax * 0.50f)}", fruit.HpCurr.ToString());
            yield return null;
            Assert("heavenly fruit is destroyed", !fruit.IsEquipped(4330), "destroyed", "equipped");

            // 턴 회복·우선도·조건부 피해·반격 피해.
            Clear();
            Unit ring = SpawnHero();
            ring.DebugSetLevel(1);
            Assert("equip spirit blessing ring", ring.TryEquipItem(4331, out _), "equipped", "failed");
            RoundStart(ring);
            ring.ModifyHp(ring.HpMax / 2);
            int ringBefore = ring.HpCurr;
            ring.BeginTurn();
            ring.EndTurn();
            int ringRawHeal = Mathf.RoundToInt(ring.HpMax / 16f);
            int ringExpectedHeal = Mathf.RoundToInt(ringRawHeal * (1f + ring.HealingBonusCurr));
            Equal("spirit blessing heals 1/16 max HP",
                Mathf.Min(ring.HpMax, ringBefore + ringExpectedHeal), ring.HpCurr);

            Clear();
            Unit kittyHolder = SpawnHero();
            int priorityBefore = kittyHolder.Priority;
            Assert("equip Kitty Hawk", kittyHolder.TryEquipItem(4332, out _), "equipped", "failed");
            RoundStart(kittyHolder);
            Equal("Kitty Hawk target priority -1", priorityBefore - 1, kittyHolder.Priority);

            Clear();
            Unit nicole = grid.SpawnUnit(-1, 1, false, 6);
            Assert("equip alternating current device", nicole != null && nicole.TryEquipItem(4411, out _),
                "equipped", "failed");
            RoundStart(nicole);
            ElementalOwnerDamageEffect electric = EffectOf<ElementalOwnerDamageEffect>(nicole);
            Near("alternating current Electro damage +7%", 1.07f,
                electric?.OutgoingDamageModifier(nicole, null, physical) ?? 0f);

            Clear();
            Unit winter = SpawnHero();
            Assert("equip winter triangle", winter.TryEquipItem(4511, out _), "equipped", "failed");
            RoundStart(winter);
            TaggedDamageMultiplierEffect counter = EffectOf<TaggedDamageMultiplierEffect>(winter);
            var counterContext = new DamageContext(winter, 100, CodeType.Passive,
                new List<int> { DamageTag.CounterAttack });
            Near("winter triangle counter damage +25%", 1.25f,
                counter?.OutgoingDamageModifier(winter, null, counterContext) ?? 0f);

            // 일리아스 — 성장분은 그대로 두고 Greek 태생 스탯만 배율을 받는다.
            Clear();
            Unit pygmalion = grid.SpawnUnit(-1, 1, false, 20);
            pygmalion?.DebugSetLevel(50);
            int growthBefore = pygmalion?.GetGrowthStatValue(PrimaryStat.STR) ?? -1;
            int strBefore = pygmalion?.GetBaseStr() ?? -1;
            Assert("equip Iliad", pygmalion != null && pygmalion.TryEquipItem(4333, out _),
                "equipped", "failed");
            RoundStart(pygmalion);
            GreekInitialStatEffect iliad = EffectOf<GreekInitialStatEffect>(pygmalion);
            Near("Iliad Greek initial stat multiplier", 1.5f,
                iliad?.InitialPrimaryStatMultiplierModifier(pygmalion, PrimaryStat.STR) ?? 0f);
            Equal("Iliad leaves growth stat unchanged", growthBefore, pygmalion.GetGrowthStatValue(PrimaryStat.STR));
            Assert("Iliad raises final STR", pygmalion.GetBaseStr() > strBefore,
                $">{strBefore}", pygmalion.GetBaseStr().ToString());

            // 황금양모 — 장비의 기본 내구에 착용자의 현재 LUK가 그대로 더해진다.
            Clear();
            Unit jasonArmor = grid.SpawnUnit(-1, 1, false, 103);
            Assert("equip Golden Fleece", jasonArmor != null && jasonArmor.TryEquipItem(4512, out _),
                "equipped", "failed");
            RoundStart(jasonArmor);
            LuckDurabilityEffect fleece = EffectOf<LuckDurabilityEffect>(jasonArmor);
            Equal("Golden Fleece grants LUK as durability", jasonArmor.GetBaseLuk(),
                fleece?.DurabilityAdditiveModifier(jasonArmor) ?? -1);

            // 라이트 — 기존 완주 해금 서포터와 같은 데이터·선택 규칙을 사용한다.
            UnitData lightData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 5);
            Assert("Light support-clear starter unlock flag", lightData?.unlocksAsStarterOnClear == true,
                "flagged", "missing");
            write?.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });
            Assert("Light starts support-only", lightData != null && CharacterSelectionManager.IsSupportOnly(lightData),
                "support only", "main eligible");
            Clear();
            Unit runMain = grid.SpawnUnit(-1, 1, false, 80);
            Unit runLight = grid.SpawnUnit(-1, 2, false, 5);
            Assert("Light completion fixture", runMain != null && runLight != null,
                "Surtr main and Light support", $"{runMain?.ID}/{runLight?.ID}");
            MethodInfo grantSupportUnlocks = typeof(RunManager).GetMethod(
                "GrantSupportStarterUnlocks", BindingFlags.Static | BindingFlags.NonPublic);
            grantSupportUnlocks?.Invoke(null, new object[] { 80 });
            Assert("Light support completion records starter unlock", SaveSystem.IsStarterUnlocked(5),
                "unlocked", "locked");
            Assert("Light support completion queues unlock notice",
                SaveSystem.LoadTrainedCharacters().pendingCharacterUnlockIds.Contains(5),
                "queued", "missing");
            Assert("Light completion unlock opens main eligibility",
                lightData != null && !CharacterSelectionManager.IsSupportOnly(lightData) && CanBeMain(lightData),
                "main eligible", "still support-only");
            write?.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });

            // 이아손 — 자신이 아니라 연결된 메인 옥타비아를 완주 보상으로 해금한다.
            UnitData jasonData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 103);
            UnitData octaviaData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 101);
            Assert("Jason has Octavia support-clear unlock target",
                jasonData?.unlocksUnitIdsOnClear?.SequenceEqual(new[] { 101 }) == true,
                "103 -> 101", "missing");
            Assert("Octavia has explicit unlock path",
                octaviaData != null && !CharacterSelectionManager.HasNoUnlockPath(octaviaData),
                "explicit path", "treated as pathless");
            Clear();
            Unit octaviaRunMain = grid.SpawnUnit(-2, 1, false, 80);
            Unit runJason = grid.SpawnUnit(-1, 1, false, 103);
            Assert("Jason completion fixture", octaviaRunMain != null && runJason != null,
                "main and Jason support", $"{octaviaRunMain?.ID}/{runJason?.ID}");
            grantSupportUnlocks?.Invoke(null, new object[] { 80 });
            Assert("Jason support completion unlocks Octavia", SaveSystem.IsStarterUnlocked(101),
                "unlocked", "locked");
            Assert("Octavia unlock opens main eligibility", octaviaData != null && CanBeMain(octaviaData),
                "main eligible", "still locked");
            write?.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });

            // 야마 — 승패·모드에 관계없이 런을 한 번 끝내면 영구 해금된다.
            UnitData yamaData = game.unitDataList.units.FirstOrDefault(unit => unit.id == RunManager.YamaUnitId);
            Assert("Yama starts locked", yamaData?.characterType == "Locked" &&
                    yamaData.canStartAsMain == false && yamaData.canUseInInfinite == false,
                "locked until a run ends", yamaData?.characterType ?? "missing");
            Assert("Yama has explicit run-completion unlock path",
                yamaData != null && !CharacterSelectionManager.HasNoUnlockPath(yamaData),
                "explicit path", "treated as pathless");
            RunManager.GrantAnyRunCompletionUnlocks();
            Assert("Yama any-run completion records starter unlock", SaveSystem.IsStarterUnlocked(RunManager.YamaUnitId),
                "unlocked", "locked");
            Assert("Yama any-run completion queues unlock notice",
                SaveSystem.LoadTrainedCharacters().pendingCharacterUnlockIds.Contains(RunManager.YamaUnitId),
                "queued", "missing");
            Assert("Yama unlock opens main eligibility", yamaData != null && CanBeMain(yamaData),
                "main eligible", "still locked");

            UnitData surtrData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 80);
            UnitData freyaData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 81);
            Assert("Surtr Indomitable archetype tag", surtrData?.archetypeTags?.Contains("Indomitable") == true,
                "Indomitable", string.Join(",", surtrData?.archetypeTags ?? new List<string>()));
            Assert("Freya Indomitable archetype tag", freyaData?.archetypeTags?.Contains("Indomitable") == true,
                "Indomitable", string.Join(",", freyaData?.archetypeTags ?? new List<string>()));
            Managers.UI.Core.UnitTagCatalog.Definition indomitable =
                Managers.UI.Core.UnitTagCatalog.Resolve(new[] { "Indomitable" }).SingleOrDefault();
            Assert("Indomitable tag catalog", indomitable.Name == "불굴" &&
                    indomitable.Description.Contains("체력을 자원"),
                "불굴/체력 자원", $"{indomitable.Name}/{indomitable.Description}");
            Assert("Indomitable tag icon", Managers.UI.Core.UnitTagCatalog.Icon(indomitable) != null,
                "loaded", "missing");
            write?.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });

            Clear();
        }

        private static T EffectOf<T>(Unit unit) where T : BaseEffect
            => unit?.ActiveStatuses.SelectMany(status => status.Effects)
                .Select(instance => instance.EffectObject).OfType<T>().FirstOrDefault();
    }
}
#endif
