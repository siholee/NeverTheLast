#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
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

            int[] commonItemIds = { 4510, 4328, 4329, 4330, 4331, 4332, 4411, 4511, 4333, 4512, 4513, 4514, 4412, 4413, 4334, 4515 };
            int[] shopOnlyItemIds = { 4335, 4336, 4337 };
            int[] itemIds = commonItemIds.Concat(shopOnlyItemIds).ToArray();
            int[] codeIds = { 445, 446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456, 457, 458, 459, 460, 461, 462, 463 };
            var items = game.itemDataList.items.Where(item => itemIds.Contains(item.id)).ToList();
            Equal("new equipment count", itemIds.Length, items.Count);
            Assert("new equipment IDs", new HashSet<int>(itemIds).SetEquals(items.Select(item => item.id)),
                string.Join(",", itemIds), string.Join(",", items.Select(item => item.id)));
            Assert("new equipment code grants", new HashSet<int>(codeIds).SetEquals(
                    items.SelectMany(item => item.codeGrants ?? new List<EquipmentCodeGrant>()).Select(grant => grant.codeId)),
                string.Join(",", codeIds),
                string.Join(",", items.SelectMany(item => item.codeGrants ?? new List<EquipmentCodeGrant>()).Select(grant => grant.codeId)));
            Assert("common equipment is in reward pool",
                commonItemIds.All(id => game.dataManager.FetchRewardDataList().commonDropItemIds.Contains(id)),
                $"all {commonItemIds.Length}", "missing reward ID");
            Assert("shop-only equipment stays out of reward pool",
                shopOnlyItemIds.All(id => !game.dataManager.FetchRewardDataList().commonDropItemIds.Contains(id)),
                "all shop-only", "reward pool leak");

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
            Assert("Hugo equipment metadata",
                Spec(4514).rarity == 5 && Spec(4514).category == "Clothing" &&
                Spec(4514).durability == 30 && HasPrimary(4514, PrimaryStat.CON, 9) &&
                Spec(4412).rarity == 4 && Spec(4412).category == "Orb" &&
                HasPrimary(4412, PrimaryStat.INT, 7),
                "Notre-Dame T5 Clothing CON+9 / Les Miserables T4 Orb INT+7", "mismatch");
            Assert("mercenary shield/cursed crown metadata",
                Spec(4413).rarity == 4 && Spec(4413).category == "Shield" &&
                Spec(4413).durability == 29 && Spec(4413).shopPrice == 320 &&
                HasSecondary(4413, EquipmentSecondaryStat.Durability, 14) &&
                Spec(4334).rarity == 3 && Spec(4334).category == "Helmet" &&
                Spec(4334).shopPrice == 180 && HasSecondary(4334, EquipmentSecondaryStat.CritRate, 10),
                "T4 shield/T3 helmet shop equipment", "mismatch");
            Assert("replication equipment metadata",
                Spec(4515).rarity == 5 && Spec(4515).category == "Helmet" &&
                HasPrimary(4515, PrimaryStat.INT, 9) && Spec(4515).shopPrice == 0 &&
                Spec(4335).rarity == 3 && Spec(4335).category == "LightArmor" &&
                Spec(4335).shopPrice == 180 && HasPrimary(4335, PrimaryStat.INT, 5),
                "T5 crown INT+9 common / T3 cloak INT+5 shop", "mismatch");
            Assert("gold equipment metadata",
                Spec(4336).rarity == 3 && Spec(4336).category == "HeavyArmor" &&
                Spec(4336).durability == 52 && Spec(4336).shopPrice == 180 &&
                HasPrimary(4336, PrimaryStat.CON, 5) &&
                Spec(4337).rarity == 3 && Spec(4337).category == "Necklace" &&
                Spec(4337).shopPrice == 180 && HasPrimary(4337, PrimaryStat.LUK, 5),
                "T3 heavy CON+5 / T3 necklace LUK+5 shop", "mismatch");
            Assert("new equipment appears in shop",
                game.rewardManager.BuildShopEquipment().Any(reward => reward.itemId == 4413) &&
                game.rewardManager.BuildShopEquipment().Any(reward => reward.itemId == 4334) &&
                shopOnlyItemIds.All(id => game.rewardManager.BuildShopEquipment().Any(reward => reward.itemId == id)),
                "all shop entries", "missing");

            foreach (int codeId in codeIds)
            {
                PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext());
                Assert($"new equipment code {codeId}", code != null, "created", code?.GetType().Name ?? "null");
            }

            Clear();
            Unit shieldBearer = grid.SpawnUnit(-1, 1, false, 5);
            Unit shieldAttacker = SpawnEnemy(1020, 1);
            Assert("equip nameless mercenary shield",
                shieldBearer != null && shieldBearer.TryEquipItem(4413, out _), "equipped", "failed");
            RoundStart(shieldBearer);
            int hpBeforeShield = shieldBearer?.HpCurr ?? 0;
            shieldBearer?.TakeDamage(new DamageContext(shieldAttacker, 10, CodeType.Normal,
                new List<int> { DamageTag.Physical, DamageTag.ContactAttack }));
            Equal("durability can fully absorb damage", hpBeforeShield, shieldBearer?.HpCurr ?? -1);
            Equal("mercenary shield gains persistent stack", 1,
                shieldBearer?.GetPersistentEquipmentStack(4413) ?? -1);
            shieldBearer?.TryUnequip(4413, storeToCarried: true);
            Equal("mercenary shield resets on unequip", 0,
                shieldBearer?.GetPersistentEquipmentStack(4413) ?? -1);

            Unit crownBearer = grid.SpawnUnit(-1, 2, false, 7);
            Unit crownTarget = grid.SpawnUnit(-1, 3, false, 5);
            Assert("equip cursed crown", crownBearer != null && crownBearer.TryEquipItem(4334, out _),
                "equipped", "failed");
            Assert("cursed crown target has durability",
                crownTarget != null && crownTarget.TryEquipItem(4413, out _) && crownTarget.DurabilityCurr > 0,
                "durability target", "missing durability");
            RoundStart(crownBearer);
            CursedCrownEffect crown = EffectOf<CursedCrownEffect>(crownBearer);
            var critical = new DamageContext(crownBearer, 100, CodeType.Normal,
                new List<int> { DamageTag.Physical }, isCrit: true);
            Equal("cursed crown ignores half durability",
                Mathf.CeilToInt((crownTarget?.DurabilityCurr ?? 0) * 0.5f),
                crown?.DurabilityPenetrationModifier(crownBearer, crownTarget, critical) ?? -1);

            // 주문 복제 계열은 행동을 새로 예약하지 않고 피해만 한 번 반복한다.
            Clear();
            Unit replicator = grid.SpawnUnit(-1, 1, false, 101);
            Unit replicationTarget = SpawnEnemy(1020, 100);
            Assert("equip replicator cloak", replicator != null && replicator.TryEquipItem(4335, out _),
                "equipped", "failed");
            RoundStart(replicator);
            int cloakCopies = 0;
            int cloakPower = 0;
            replicationTarget.AddListener<EventContext>(UnitEventType.OnAfterDamageTaken, context =>
            {
                if (context?.DmgCtx?.DamageTags?.Contains(DamageTag.ReplicatedAttack) != true) return;
                cloakCopies++;
                cloakPower += context.DmgCtx.Damage;
            });
            replicationTarget.TakeDamage(new DamageContext(replicator, 100, CodeType.Normal,
                new List<int> { DamageTag.Special, DamageTag.NonContactAttack }));
            Equal("spell replication repeats once", 1, cloakCopies);
            Assert("spell replication has 20 percent power", cloakPower > 0 && cloakPower <= 20,
                "1..20 processed damage", cloakPower.ToString());

            Clear();
            Unit echoBearer = grid.SpawnUnit(-1, 1, false, 101);
            replicationTarget = SpawnEnemy(1020, 100);
            Assert("equip cloak and Justinian crown",
                echoBearer != null && echoBearer.TryEquipItem(4335, out _) && echoBearer.TryEquipItem(4515, out _),
                "both equipped", "failed");
            RoundStart(echoBearer);
            int echoCopies = 0;
            int echoPower = 0;
            replicationTarget.AddListener<EventContext>(UnitEventType.OnAfterDamageTaken, context =>
            {
                if (context?.DmgCtx?.DamageTags?.Contains(DamageTag.ReplicatedAttack) != true) return;
                echoCopies++;
                echoPower += context.DmgCtx.Damage;
            });
            replicationTarget.TakeDamage(new DamageContext(echoBearer, 100, CodeType.Normal,
                new List<int> { DamageTag.Special, DamageTag.NonContactAttack }));
            Equal("Imperial Echo supersedes replication", 1, echoCopies);
            Assert("Imperial Echo has stronger copy", echoPower > cloakPower,
                $"> {cloakPower}", echoPower.ToString());

            int originalGold = game.inventoryManager.Gold;
            Clear();
            Unit goldBearer = grid.SpawnUnit(-1, 1, false, 7);
            Assert("equip golden armor", goldBearer != null && goldBearer.TryEquipItem(4336, out _),
                "equipped", "failed");
            game.inventoryManager.RestoreGold(1000);
            RoundStart(goldBearer);
            GoldScaledDamageEffect goldEffect = EffectOf<GoldScaledDamageEffect>(goldBearer);
            Assert("golden armor scales at 0.1 percent per gold",
                Mathf.Approximately(goldEffect?.OutgoingDamageModifier(goldBearer, replicationTarget, critical) ?? 0f, 2f),
                "2.0x at 1000 gold",
                (goldEffect?.OutgoingDamageModifier(goldBearer, replicationTarget, critical) ?? 0f).ToString("0.000"));

            Clear();
            Unit luckyBearer = grid.SpawnUnit(-1, 1, false, 101);
            Assert("equip maneki-neko", luckyBearer != null && luckyBearer.TryEquipItem(4337, out _),
                "equipped", "failed");
            RoundStart(luckyBearer);
            Assert("maneki-neko raises battle gold by 20 percent",
                Mathf.Approximately(RewardModifiers.GoldMultiplier(), 1.2f),
                "1.2x", RewardModifiers.GoldMultiplier().ToString("0.000"));
            game.inventoryManager.RestoreGold(originalGold);

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

            // 옥타비아는 초기 스타터로 승격했고 이아손의 연결 해금은 제거했다.
            UnitData jasonData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 103);
            UnitData octaviaData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 101);
            Assert("Jason no longer unlocks Octavia",
                jasonData?.unlocksUnitIdsOnClear == null || jasonData.unlocksUnitIdsOnClear.Count == 0,
                "no linked unlock", string.Join(",", jasonData?.unlocksUnitIdsOnClear ?? new List<int>()));
            Assert("Octavia is initial Starter",
                octaviaData?.characterType == "Starter" && octaviaData.canStartAsMain &&
                octaviaData.canUseInInfinite && CanBeMain(octaviaData),
                "Starter/main/infinite", octaviaData?.characterType ?? "missing");
            UnitData shakespeareData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 10);
            UnitData theseusData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 23);
            UnitData anubisData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 121);
            Assert("Shakespeare is initial Starter",
                shakespeareData?.characterType == "Starter" && shakespeareData.canStartAsMain &&
                shakespeareData.canUseInInfinite && !shakespeareData.canStartAsSupport,
                "Starter/main/infinite", shakespeareData?.characterType ?? "missing");
            Assert("Theseus and Anubis are locked",
                new[] { theseusData, anubisData }.All(unit => unit != null &&
                    unit.characterType == "Locked" && !unit.canStartAsMain && !unit.canStartAsSupport &&
                    !CharacterSelectionManager.HasNoUnlockPath(unit)),
                "locked with closed path", $"{theseusData?.characterType}/{anubisData?.characterType}");

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

            // 위고 — 소환 100회 계정 해금과 코드 팩토리 연결.
            UnitData hugoData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 9);
            Assert("Hugo support/unlock metadata",
                hugoData?.characterType == "Support" && hugoData.canStartAsSupport &&
                hugoData.unlockAfterSummonCount == 100,
                "Support / 100 summons", hugoData == null ? "missing" :
                    $"{hugoData.characterType}/{hugoData.unlockAfterSummonCount}");
            for (int i = 0; i < 99; i++) SaveSystem.RecordCombatSummon();
            Assert("Hugo remains main-locked at 99 summons", !SaveSystem.IsStarterUnlocked(9),
                "locked", "unlocked early");
            SaveSystem.RecordCombatSummon();
            Assert("Hugo unlocks at 100 summons", SaveSystem.IsStarterUnlocked(9),
                "unlocked", "locked");
            Assert("Hugo and Pensee codes are registered",
                CodeFactory.CreatePassiveCode(209, new PassiveCodeContext()) is HugoEnsemble &&
                CodeFactory.CreateNormalCode(9, new NormalCodeContext()) is Codes.Normal.HugoPartyHeal &&
                CodeFactory.CreateUltimateCode(9, new UltimateCodeContext()) is Codes.Ultimate.HugoBraveAdvocate &&
                CodeFactory.CreateNormalCode(503, new NormalCodeContext()) is Codes.Normal.PenseeHeal &&
                CodeFactory.CreateUltimateCode(503, new UltimateCodeContext()) is Codes.Ultimate.PenseeProtection,
                "all registered", "missing factory branch");

            Clear();
            Unit hugo = grid.SpawnUnit(-2, 1, false, 9);
            Unit hugoAlly = grid.SpawnUnit(-1, 1, false, 1);
            Unit hugoEnemy = SpawnEnemy(1062, 30);
            hugo?.DebugSetLevel(100);
            Assert("Hugo mechanics fixture", hugo != null && hugoAlly != null && hugoEnemy != null,
                "Hugo/ally/enemy", $"{hugo != null}/{hugoAlly != null}/{hugoEnemy != null}");
            if (hugo != null && hugoAlly != null && hugoEnemy != null)
            {
                grid.OnRoundStart();
                Near("Hugo Ensemble starts at summon damage +1%", 1.01f, Summons.DamageMultiplier(hugo));
                Unit firstPensee = grid.SpawnSummon(hugo, SummonCatalog.Pensee(hugo));
                Near("Hugo Ensemble gains +1% per allied summon", 1.02f, Summons.DamageMultiplier(hugo));

                int hurtHp = Mathf.Max(1, hugoAlly.HpMax / 3);
                hugoAlly.ModifyHp(hurtHp);
                hugo.CastNormalCode();
                yield return Settle(2f);
                Assert("Hugo normal heals the party", hugoAlly.HpCurr > hurtHp,
                    $">{hurtHp}", hugoAlly.HpCurr.ToString());

                int summonsBeforeUltimate = hugo.ActiveSummons.Count(summon => summon != null && summon.isActive);
                hugo.FillUltimateResource(false);
                hugo.CastUltimateCode();
                yield return Settle(2f);
                Assert("Hugo ultimate summons Pensee for three turns",
                    hugo.ActiveSummons.Count(summon => summon != null && summon.isActive && summon.UnitName == "팡세")
                    > summonsBeforeUltimate,
                    "additional Pensee", string.Join(",", hugo.ActiveSummons.Where(s => s != null).Select(s => s.UnitName)));

                if (firstPensee != null && firstPensee.isActive)
                {
                    int lowerHp = Mathf.Max(1, hugoAlly.HpMax / 4);
                    hugoAlly.ModifyHp(lowerHp);
                    firstPensee.CastNormalCode();
                    yield return Settle(2f);
                    Assert("Pensee normal heals the lowest-HP ally", hugoAlly.HpCurr > lowerHp,
                        $">{lowerHp}", hugoAlly.HpCurr.ToString());

                    firstPensee.FillUltimateResource(false);
                    firstPensee.CastUltimateCode();
                    yield return Settle(2f);
                    Assert("Pensee ultimate grants party damage reduction",
                        hugoAlly.ActiveStatuses.Any(status => status.Key.StartsWith("pensee_defense_")),
                        "damage reduction status", "missing");
                }

                Assert("equip Les Miserables", hugo.TryEquipItem(4412, out _), "equipped", "failed");
                RoundStart(hugo);
                LesMiserablesEffect lesMiserables = EffectOf<LesMiserablesEffect>(hugo);
                int ownedSummons = hugo.ActiveSummons.Count(summon => summon != null && summon.isActive);
                Near("Les Miserables grants +10% per owned summon", 1f + ownedSummons * 0.10f,
                    lesMiserables?.SummonDamageMultiplierModifier(hugo) ?? 0f);
            }
            write?.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });

            // 셰익스피어·피그말리온 — 소환 판정, 언어 확정 치명타, 결계 부활.
            Clear();
            Unit shakespeare = grid.SpawnUnit(-2, 1, false, 10);
            Unit summonPygmalion = grid.SpawnUnit(-1, 1, false, 20);
            Unit uchiSupport = grid.SpawnUnit(-2, 2, false, 180);
            Unit summonEnemy = SpawnEnemy(1062, 50);
            UnitData pygmalionData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 20);
            Assert("Pygmalion summon/domination tags",
                pygmalionData?.archetypeTags?.Contains("Summon") == true &&
                pygmalionData.archetypeTags.Contains("Domination"),
                "Summon/Domination", string.Join(",", pygmalionData?.archetypeTags ?? new List<string>()));
            Assert("Shakespeare/Pygmalion fixture",
                shakespeare != null && summonPygmalion != null && uchiSupport != null && summonEnemy != null,
                "four units", $"{shakespeare != null}/{summonPygmalion != null}/{uchiSupport != null}/{summonEnemy != null}");
            if (shakespeare != null && summonPygmalion != null && uchiSupport != null && summonEnemy != null)
            {
                grid.OnRoundStart();
                Assert("Pygmalion counts as summon", Summons.IsSummonLike(summonPygmalion),
                    "summon-like", "not summon-like");
                Near("Pygmalion aura grants allied summon damage +30%", 1.30f,
                    Summons.DamageMultiplier(summonPygmalion));

                summonPygmalion.FillUltimateResource(false);
                summonPygmalion.CastUltimateCode();
                yield return Settle(2f);
                Entities.Status.UnitStatus thorns = summonPygmalion.ActiveStatuses
                    .FirstOrDefault(status => status.Key == "pygmalion_rose_thorns");
                Assert("Pygmalion ultimate lasts three turns and blocks normal action",
                    thorns?.RemainingTurns == 3 && summonPygmalion.IsNormalAttackBlocked &&
                    summonPygmalion.Priority >= 1,
                    "3 turns/blocked/priority +1",
                    $"{thorns?.RemainingTurns}/{summonPygmalion.IsNormalAttackBlocked}/{summonPygmalion.Priority}");
                RoseThornsEffect roseThorns = EffectOf<RoseThornsEffect>(summonPygmalion);
                Near("Pygmalion ultimate reduces incoming damage by half", 0.5f,
                    roseThorns?.ReceivingDamageModifier(summonPygmalion) ?? 0f);
                int pygmalionManaBeforeHit = summonPygmalion.ManaCurr;
                summonPygmalion.TakeDamage(new DamageContext(summonEnemy, 20, CodeType.Normal,
                    new List<int> { DamageTag.Physical, DamageTag.ContactAttack, DamageTag.TrueDamage }));
                Equal("Pygmalion ultimate grants doubled hit mana", 2,
                    summonPygmalion.ManaCurr - pygmalionManaBeforeHit);

                Unit clone = grid.SpawnSummon(uchiSupport, SummonCatalog.Clone(uchiSupport));
                Equal("Shakespeare gains language on allied summon creation", 1,
                    shakespeare.GetCombatResource(ShakespeareCombat.LanguageResource));
                bool cloneCrit = false;
                Action<DamageResolvedContext> critWatcher = context =>
                {
                    if (context?.Attacker == clone) cloneCrit = context.DamageContext?.IsCrit == true;
                };
                Unit.AnyDamageDealt += critWatcher;
                clone?.CastNormalCode();
                yield return Settle(2f);
                Unit.AnyDamageDealt -= critWatcher;
                Assert("Shakespeare language guarantees summon critical", cloneCrit,
                    "critical", "not critical");
                Equal("summon action gains then attack consumes one language", 1,
                    shakespeare.GetCombatResource(ShakespeareCombat.LanguageResource));

                shakespeare.FillUltimateResource(false);
                shakespeare.CastUltimateCode();
                yield return Settle(2f);
                Near("Shakespeare field stacks +25% with Pygmalion +30%", 1.55f,
                    Summons.DamageMultiplier(summonPygmalion));
                Assert("Shakespeare field wards summon-like Pygmalion",
                    summonPygmalion.ActiveStatuses.Any(status => status.Key.StartsWith("shakespeare_summon_revival_")),
                    "revival ward", "missing");
                summonPygmalion.TakeDamage(new DamageContext(summonEnemy, summonPygmalion.HpMax * 100,
                    CodeType.Normal, new List<int>
                    {
                        DamageTag.Physical, DamageTag.TrueDamage, DamageTag.DurabilityPenetration,
                    }));
                Equal("Shakespeare field revives Pygmalion once", summonPygmalion.HpMax, summonPygmalion.HpCurr);
            }

            // 우치 — 분신 한 기가 치명상뿐 아니라 적중 한 번 자체를 0으로 만든다.
            Clear();
            Unit usa = grid.SpawnUnit(-1, 1, false, 180);
            Unit usaAttacker = SpawnEnemy(1062, 20);
            RoundStart(usa);
            Assert("Uchi clone fixture", UsaTaoistNature.SummonClones(usa, 1) == 1,
                "one clone", "summon failed");
            int usaHp = usa.HpCurr;
            int clonesBefore = UsaTaoistNature.CloneCount(usa);
            usa.TakeDamage(new DamageContext(usaAttacker, Mathf.Max(10, usa.HpMax / 4), CodeType.Normal,
                new List<int> { DamageTag.Physical }));
            Equal("Uchi clone nullifies hit before HP loss", usaHp, usa.HpCurr);
            Equal("Uchi clone is consumed", clonesBefore - 1, UsaTaoistNature.CloneCount(usa));

            // 아누비스 — 처치되지 않고 흩어진 소환수도 영혼 수확에 포함된다.
            Clear();
            Unit anubis = grid.SpawnUnit(-1, 1, false, 121);
            Unit summoner = grid.SpawnUnit(-2, 1, false, 180);
            RoundStart(anubis);
            int anubisStr = anubis.GetBaseStr();
            for (int i = 0; i < 4; i++)
                grid.SpawnSummon(summoner, Combat.SummonCatalog.Clone(summoner))?.Withdraw();
            Equal("Anubis gains STR from four summon disappearances", anubisStr + 1, anubis.GetBaseStr());

            Clear();
        }

        /// <summary>세이메이·아스클레피아 리워크의 데이터와 핵심 전투 판정을 실제 Unit 파이프라인으로 검증한다.</summary>
        private IEnumerator ReworkedHealers()
        {
            Status = "세이메이/아스클레피아 리워크 검증";

            UnitData seimeiData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 43);
            UnitData asclepiusData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 24);
            UnitData uchiData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 180);
            UnitData bastetData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 122);

            Assert("Seimei archetypes", seimeiData?.archetypeTags != null &&
                new HashSet<string>(seimeiData.archetypeTags).SetEquals(new[] { "Domination", "Healing" }),
                "Domination,Healing", string.Join(",", seimeiData?.archetypeTags ?? new List<string>()));
            Assert("Seimei unlock ladder", seimeiData?.levelPassives != null &&
                seimeiData.levelPassives.Select(entry => (entry.codeId, entry.unlockLevel)).SequenceEqual(new[]
                {
                    (56, 3), (70, 10), (146, 15), (147, 23), (116, 30), (148, 45), (55, 52),
                }), "56@3,70@10,146@15,147@23,116@30,148@45,55@52",
                string.Join(",", seimeiData?.levelPassives?.Select(entry => $"{entry.codeId}@{entry.unlockLevel}") ?? Array.Empty<string>()));
            Assert("removed Seimei passives stay removed",
                CodeFactory.CreatePassiveCode(115, new PassiveCodeContext()) == null &&
                CodeFactory.CreatePassiveCode(117, new PassiveCodeContext()) == null &&
                CodeFactory.CreatePassiveCode(118, new PassiveCodeContext()) == null,
                "all null", "legacy code created");
            Assert("new Seimei factories",
                CodeFactory.CreatePassiveCode(243, new PassiveCodeContext()) is SeimeiTripleSeal &&
                CodeFactory.CreatePassiveCode(146, new PassiveCodeContext()) is SeimeiSealFormation &&
                CodeFactory.CreatePassiveCode(147, new PassiveCodeContext()) is SeimeiEmergencyTreatment &&
                CodeFactory.CreatePassiveCode(148, new PassiveCodeContext()) is SeimeiEmergencyRoom,
                "all created", "factory mismatch");

            Assert("Asclepius identity", asclepiusData != null &&
                asclepiusData.mainStat == "LUK" && asclepiusData.subStats.SequenceEqual(new[] { "STR", "CON" }) &&
                asclepiusData.startingProficiencies.SequenceEqual(new[] { "Crossbow" }) &&
                new HashSet<string>(asclepiusData.tags).SetEquals(new[] { "Akasha", "Greek", "Human" }),
                "LUK/STR,CON Crossbow Akasha,Greek,Human", "data mismatch");
            Assert("Asclepius unlock ladder", asclepiusData?.levelPassives != null &&
                asclepiusData.levelPassives.Select(entry => (entry.codeId, entry.unlockLevel)).SequenceEqual(new[]
                {
                    (55, 2), (5, 15), (30, 40), (37, 44), (32, 50),
                }), "55@2,5@15,30@40,37@44,32@50",
                string.Join(",", asclepiusData?.levelPassives?.Select(entry => $"{entry.codeId}@{entry.unlockLevel}") ?? Array.Empty<string>()));
            Assert("growth coefficient rebalance",
                uchiData?.intIncrementLvl == 3 && uchiData?.lukIncrementLvl == 3 &&
                Mathf.Approximately(asclepiusData?.strLevelGrowthScale ?? 0f, 1.55f) &&
                bastetData?.dexIncrementLvl == 2 &&
                Mathf.Approximately(bastetData?.dexLevelGrowthScale ?? 0f, 0.90f) &&
                bastetData?.lukIncrementLvl == 2,
                "Uchi INT/LUK 3; Asclepius STR 1.55; Bastet DEX 1.8/LUK 2",
                $"Uchi {uchiData?.intIncrementLvl}/{uchiData?.lukIncrementLvl}; Bastet {bastetData?.dexIncrementLvl}/{bastetData?.lukIncrementLvl}");

            // 봉인부 세 장은 모두 소비되고 속박은 대상의 다음 턴 시작에 풀린다.
            Clear();
            Unit seimei = grid.SpawnUnit(-1, 1, false, 43);
            Unit sealTarget = SpawnEnemy(1020, 30);
            Assert("Seimei seal fixture", seimei != null && sealTarget != null, "spawned", "spawn failed");
            SeimeiIds.ApplySeal(seimei, sealTarget);
            SeimeiIds.ApplySeal(seimei, sealTarget);
            Assert("two seals do not bind", !sealTarget.isControlled &&
                sealTarget.GetAllStatuses().Count(status => status.Key == SeimeiIds.SealKey) == 2,
                "2 seals, active", "wrong pre-trigger state");
            SeimeiIds.ApplySeal(seimei, sealTarget);
            Assert("third seal binds and consumes marks", sealTarget.isControlled &&
                sealTarget.GetAllStatuses().All(status => status.Key != SeimeiIds.SealKey),
                "bound, 0 seals", "trigger failed");
            sealTarget.BeginTurn();
            Assert("bind releases before target action", !sealTarget.isControlled,
                "can act on next turn", "still controlled");

            // 응급치료는 실제 회복이 생겼을 때만 CON 보호막을 만든다.
            Unit patient = grid.SpawnUnit(-1, 2, false, 80);
            PassiveCode emergency = CodeFactory.CreatePassiveCode(147, new PassiveCodeContext { Caster = seimei });
            emergency.CastCode();
            patient.ModifyHp(Mathf.Max(1, patient.HpMax / 2));
            int shieldBefore = patient.ShieldCurr;
            patient.ModifyHp(patient.HpCurr + 10, seimei);
            Assert("Seimei effective heal grants CON shield", patient.ShieldCurr > shieldBefore,
                $"> {shieldBefore}", patient.ShieldCurr.ToString());
            emergency.StopCode();

            // 생명의 잔은 체력이 부족한 아군의 행동 시작에 반응하고 행동당 자원은 한 번만 준다.
            Clear();
            Unit asclepius = grid.SpawnUnit(-1, 1, false, 24);
            Unit actingAlly = grid.SpawnUnit(-1, 2, false, 80);
            PassiveCode chalice = CodeFactory.CreatePassiveCode(224, new PassiveCodeContext { Caster = asclepius });
            chalice.CastCode();
            actingAlly.ModifyHp(Mathf.Max(1, actingAlly.HpMax / 2));
            int hpBefore = actingAlly.HpCurr;
            int manaBefore = asclepius.ManaCurr;
            var actionEvent = typeof(ActionScheduler).GetField("AnyActionStarted",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as
                Action<Unit, ActionScheduler.ActionKind, string>;
            actionEvent?.Invoke(actingAlly, ActionScheduler.ActionKind.Normal, "verification");
            Assert("Chalice heals injured acting ally", actingAlly.HpCurr > hpBefore,
                $"> {hpBefore}", actingAlly.HpCurr.ToString());
            Equal("Chalice grants five percent resource once", manaBefore + Mathf.CeilToInt(asclepius.ManaMax * 0.05f),
                asclepius.ManaCurr);
            actionEvent?.Invoke(actingAlly, ActionScheduler.ActionKind.Normal, "same verification action");
            Equal("Chalice resource is capped once per action", manaBefore + Mathf.CeilToInt(asclepius.ManaMax * 0.05f),
                asclepius.ManaCurr);
            chalice.StopCode();

            // 두 궁극기는 실제 코루틴을 통과시켜 광역 치유와 버프 적용을 확인한다.
            Clear();
            seimei = grid.SpawnUnit(-1, 1, false, 43);
            patient = grid.SpawnUnit(-1, 2, false, 80);
            SpawnEnemy(1020, 30);
            UltimateCode seimeiUltimate = CodeFactory.CreateUltimateCode(43, new UltimateCodeContext { Caster = seimei });
            seimeiUltimate.CastCode();
            yield return new WaitForSeconds(0.7f);
            Assert("Taizan ritual grants regeneration and attack seal",
                patient.GetAllStatuses().Any(status => status.Key.StartsWith("seimei_regeneration_")) &&
                patient.GetAllStatuses().Any(status => status.Key.StartsWith("seimei_attack_seal_")),
                "both buffs", string.Join(",", patient.GetAllStatuses().Select(status => status.Key)));

            Clear();
            asclepius = grid.SpawnUnit(-1, 1, false, 24);
            actingAlly = grid.SpawnUnit(-1, 2, false, 80);
            Unit ultimateTarget = SpawnEnemy(1020, 30);
            actingAlly.ModifyHp(Mathf.Max(1, actingAlly.HpMax / 2));
            hpBefore = actingAlly.HpCurr;
            int enemyHpBefore = ultimateTarget.HpCurr;
            UltimateCode lifeLine = CodeFactory.CreateUltimateCode(24, new UltimateCodeContext { Caster = asclepius });
            Assert("Asclepius lifeline has a valid target", lifeLine.HasValidTarget(), "true", "false");
            lifeLine.CastCode();
            yield return new WaitForSeconds(2f);
            Assert("Asclepius lifeline heals all allies", actingAlly.HpCurr > hpBefore,
                $"> {hpBefore}", actingAlly.HpCurr.ToString());
            Assert("Asclepius lifeline damages enemies", ultimateTarget.HpCurr < enemyHpBefore,
                $"< {enemyHpBefore}", ultimateTarget.HpCurr.ToString());

            Clear();
        }

        /// <summary>수르트·프레이아·옥타비아 최신 리워크의 핵심 수치와 라운드 수명을 검증한다.</summary>
        private IEnumerator LatestBalanceMechanics()
        {
            Status = "수르트/프레이아/옥타비아 최신 리워크";

            // 수르트의 체력 연소는 고정 배율이 아니라 CON 비례 추가 위력으로 바뀌었다.
            Clear();
            Unit surtr = grid.SpawnUnit(-1, 1, false, 80);
            Unit surtrTarget = SpawnEnemy(1020, 100);
            surtr.DebugSetLevel(100);
            int hpBeforeBurn = surtr.HpCurr;
            int expectedBonus = Mathf.RoundToInt(surtr.GetBaseCon() * SurtrBurn.ConPowerCoefficient);
            int bonusPower = SurtrBurn.PayForBonusPower(surtr);
            Equal("Surtr burn grants CON-scaled power", expectedBonus, bonusPower);
            Assert("Surtr burn consumes 15 percent max HP", surtr.HpCurr < hpBeforeBurn,
                $"< {hpBeforeBurn}", surtr.HpCurr.ToString());

            PassiveCode twilight = CodeFactory.CreatePassiveCode(280, new PassiveCodeContext { Caster = surtr });
            twilight.CastCode();
            for (int i = 0; i < 5; i++)
            {
                surtr.Invoke(UnitEventType.OnNormalActivates, new EventContext(surtr));
                surtrTarget.TakeDamage(new DamageContext(surtr, 1, CodeType.Normal,
                    new List<int> { DamageTag.Physical, DamageTag.ContactAttack }));
                surtrTarget.ModifyHp(surtrTarget.HpMax);
            }
            Assert("Surtr twilight shield has no 40 percent HP cap",
                surtr.ShieldCurr > surtr.HpMax * 0.4f,
                $"> {surtr.HpMax * 0.4f:0}", surtr.ShieldCurr.ToString());
            twilight.StopCode();

            // 피톤치드는 적용 전 프레이아 CON을 스냅샷해 전 아군에게 75%를 나눈다.
            Clear();
            Unit freya = grid.SpawnUnit(-1, 1, false, 81);
            Unit ally = grid.SpawnUnit(-1, 2, false, 80);
            Unit harvestTarget = SpawnEnemy(1020, 100);
            freya.DebugSetLevel(100);
            ally.DebugSetLevel(100);
            // Spawn/레벨 갱신 과정에서 준비 상태 패시브가 한 차례 켜질 수 있다. 실제 전투와 같은
            // 라운드 경계를 통과시켜 이전 스냅샷을 지우고 Lv.100 수치로 다시 계산한다.
            grid.OnRoundEnd();
            int freyaConBefore = freya.GetBaseCon();
            int allyConBefore = ally.GetBaseCon();
            int auraBonus = Mathf.RoundToInt(freyaConBefore * FreyaPhytoncide.ConShareRatio);
            grid.OnRoundStart();
            float AllyAuraAmount(Unit unit) => unit.ActiveStatuses
                .Where(status => status.Key.StartsWith("freya_phytoncide_"))
                .SelectMany(status => status.Effects)
                .Select(instance => instance.EffectObject)
                .OfType<PrimaryStatBonusBuffEffect>()
                .Select(effect => effect.Coefficient)
                .FirstOrDefault();
            Assert("Phytoncide grants 75 percent Freya CON to ally",
                Mathf.Approximately(AllyAuraAmount(ally), auraBonus) && ally.GetBaseCon() > allyConBefore,
                $"flat +{auraBonus}", $"effect={AllyAuraAmount(ally):0}/CON={allyConBefore}->{ally.GetBaseCon()}");
            Assert("Phytoncide self-bonus uses pre-aura snapshot",
                Mathf.Approximately(AllyAuraAmount(freya), auraBonus) && freya.GetBaseCon() > freyaConBefore,
                $"flat +{auraBonus}", $"effect={AllyAuraAmount(freya):0}/CON={freyaConBefore}->{freya.GetBaseCon()}");

            ally.ModifyHp(Mathf.Max(1, ally.HpMax / 2));
            ally.ModifyHp(ally.HpMax, freya);
            Assert("Freya records effective healing for Harvest", freya.RoundEffectiveHealingDone > 0,
                ">0", freya.RoundEffectiveHealingDone.ToString());
            UltimateCode harvest = CodeFactory.CreateUltimateCode(81, new UltimateCodeContext { Caster = freya });
            int harvestHpBefore = harvestTarget.HpCurr;
            harvest.CastCode();
            yield return Settle(0.75f);
            Assert("Harvest damages an enemy", harvestTarget.HpCurr < harvestHpBefore,
                $"< {harvestHpBefore}", harvestTarget.HpCurr.ToString());
            Equal("Harvest settles and clears effective healing", 0, freya.RoundEffectiveHealingDone);
            grid.OnRoundEnd();

            // 옥타비아의 가상 마나는 실제 마나와 합산해 임계점에서만 실제 게이지로 바뀐다.
            Clear();
            Unit octavia = grid.SpawnUnit(-1, 1, false, 101);
            Unit ultimateAlly = grid.SpawnUnit(-1, 2, false, 103);
            PassiveCode festina = CodeFactory.CreatePassiveCode(301, new PassiveCodeContext { Caster = octavia });
            festina.CastCode();
            var ultimateEvent = typeof(Unit).GetField("AnyActiveUltimateActivated",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Action<Unit>;
            ultimateEvent?.Invoke(ultimateAlly);
            Equal("Festina Lente gains five virtual mana", 5,
                octavia.GetCombatResource(FestinaLenteEffect.VirtualManaResource));
            octavia.AddUltimateResource(Mathf.CeilToInt(octavia.ManaMax * 0.95f));
            Equal("Festina Lente converts immediately on natural mana gain", octavia.ManaMax, octavia.ManaCurr);
            Equal("Festina Lente clears virtual mana after conversion", 0,
                octavia.GetCombatResource(FestinaLenteEffect.VirtualManaResource));
            octavia.Invoke(UnitEventType.OnRoundEnd, new EventContext(octavia));
            Equal("Festina Lente virtual mana clears after battle", 0,
                octavia.GetCombatResource(FestinaLenteEffect.VirtualManaResource));
            festina.StopCode();

            Clear();
        }

        private static T EffectOf<T>(Unit unit) where T : BaseEffect
            => unit?.ActiveStatuses.SelectMany(status => status.Effects)
                .Select(instance => instance.EffectObject).OfType<T>().FirstOrDefault();
    }
}
#endif
