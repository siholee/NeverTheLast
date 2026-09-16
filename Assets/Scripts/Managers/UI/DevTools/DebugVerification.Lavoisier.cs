#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Core;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Helpers;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class DebugVerification
    {
        private sealed class VerifyOverheal : BaseEffect
        {
            public VerifyOverheal() : base(0) { }
            public override float OverhealShieldConversionModifier(Unit unit) => 1f;
        }

        private IEnumerator LavoisierCoverage()
        {
            Status = "라부아지에 수치·저장·실제 전투";
            var inventory = new ReagentInventory();
            inventory.Resize(100);
            Near("CON 100 capacity", 120, inventory.Capacity);
            for (int i = 0; i < 6; i++) inventory.ResolveNormal();
            Near("unsupported six normals Q", 30, inventory.Batch);
            Near("quarter batch damage", 2550, ReagentInventory.Damage(100, 100, 30));
            Near("half batch damage", 3900, ReagentInventory.Damage(100, 100, 60));
            Near("full batch damage", 6600, ReagentInventory.Damage(100, 100, 120));
            inventory.Consume();
            Equal("ultimate preserves synthesis phase", 6, inventory.NormalActions);
            inventory.Resize(101); inventory.Receive(ReagentKind.Fuel);
            Near("fractional receive preserved", 60.5f, inventory[ReagentKind.Fuel]);
            inventory.Resize(200);
            Near("CON increase does not multiply stock", 60.5f, inventory[ReagentKind.Fuel]);
            inventory.Resize(0);
            Near("CON decrease clamps stock", 20, inventory[ReagentKind.Fuel]);

            // SaveSystem의 디버그 저장소만 시드한다. 실제 PlayerPrefs에는 쓰지 않는다.
            var write = typeof(SaveSystem).GetMethod("WriteString", BindingFlags.Static | BindingFlags.NonPublic);
            write.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });
            Assert("fresh account locked", !SaveSystem.IsStarterUnlocked(8), "locked", "checked");
            SaveSystem.AddTrainedCharacter(21);
            Assert("first completion unlock", SaveSystem.IsStarterUnlocked(8), "unlocked", "checked");
            Assert("unlock is not own training", !SaveSystem.IsCharacterTrained(8), "not trained", "checked");
            Equal("single pending unlock", 1, SaveSystem.LoadTrainedCharacters().pendingCharacterUnlockIds.Count(id => id == 8));
            SaveSystem.AcknowledgeCharacterUnlock(8);
            SaveSystem.AddTrainedCharacter(22);
            Equal("acknowledged unlock does not repeat", 0, SaveSystem.LoadTrainedCharacters().pendingCharacterUnlockIds.Count);
            write.Invoke(null, new object[] { "NTL_TrainedCharacters", "{\"unitIds\":[21]}" });
            Assert("legacy completion migration", SaveSystem.IsStarterUnlocked(8), "unlocked", "checked");
            Assert("migration persisted", ((string)typeof(SaveSystem).GetMethod("ReadString", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "NTL_TrainedCharacters", "" })).Contains("unlockedStarterUnitIds\":[8]"), "saved", "checked");
            SaveSystem.AddTrainedCharacter(8);
            Assert("own completion available for infinite", SaveSystem.IsCharacterTrained(8), "trained", "checked");

            var definition = game.unitDataList.units.Single(d => d.id == 8);
            Assert("identity and gates", definition.element == "Pyro" && definition.tags.Contains("Gaul") &&
                !definition.canStartAsMain && !definition.canStartAsSupport && definition.canUseInInfinite, "Pyro / Gaul / locked", "checked");
            var portrait = SpriteResource.LoadPortrait(definition.portrait);
            Assert("portrait slice loads", portrait != null && portrait.rect.width == portrait.rect.height, "square sprite", portrait?.rect.ToString());
            Assert("standing loads", SpriteResource.LoadStanding(definition.standing) != null, "sprite", "checked");

            Clear();
            var unit = grid.SpawnUnit(-1, 1, false, 8);
            var ally = grid.SpawnUnit(-2, 1, false, 60);
            var ally2 = grid.SpawnUnit(-1, 2, false, 4);
            var enemy = SpawnEnemy(1062, 100);
            var enemy2 = SpawnEnemy(1063, 100, 2, 1);
            grid.OnRoundStart();
            MakeUnavoidable(enemy); MakeUnavoidable(enemy2);
            unit.Chemistry.BeginRound();
            var chemistry = unit.Chemistry;
            float half = chemistry.Reagents.Capacity / 2f;
            unit.RecoverMana(1000); unit.AccrueUltimateResource(1000); unit.FillUltimateResource(true);
            Equal("mana effects cannot prepare reaction", 0, unit.ManaCurr);
            unit.FillUltimateResource(false);
            Assert("explicit refill still needs reagents", !unit.ActiveUltimateCode.HasValidTarget(), "waiting", "checked");
            unit.Chemistry.BeginRound();
            unit.AddStatus(BuffStatus.Create(999981, "verify_overheal", "overheal", unit, unit, new VerifyOverheal()));
            unit.ModifyHp(unit.HpMax + 10, ally);
            Near("overheal gives fuel", half, chemistry.Reagents[ReagentKind.Fuel]);
            Near("overheal shield not stabilizer", 0, chemistry.Reagents[ReagentKind.Stabilizer]);
            unit.ModifyHp(unit.HpMax + 10, ally);
            Near("same source same interval deduplicated", half, chemistry.Reagents[ReagentKind.Fuel]);
            unit.ModifyHp(unit.HpMax + 10, unit);
            unit.ModifyHp(unit.HpMax + 10, enemy);
            Near("self and enemy rejected", half, chemistry.Reagents[ReagentKind.Fuel]);
            unit.AddShield(10, ally);
            Near("different reagent same source allowed", half, chemistry.Reagents[ReagentKind.Stabilizer]);
            unit.AddStatus(BuffStatus.Create(999982, "verify_buff", "buff", ally, unit, new CritChanceBuffEffect(.01f), 2));
            Near("beneficial status creates catalyst", half, chemistry.Reagents[ReagentKind.Catalyst]);
            unit.AddStatus(BuffStatus.Create(999983, "verify_buff2", "buff", ally, unit, new CritChanceBuffEffect(.01f), 2));
            Near("multi buff from same source deduplicated", half, chemistry.Reagents[ReagentKind.Catalyst]);
            unit.ModifyHp(unit.HpMax + 10, ally2);
            Near("different source independent", half * 2, chemistry.Reagents[ReagentKind.Fuel]);
            unit.BeginTurn();
            unit.AddShield(10, ally);
            Near("new owner turn permits support", half * 2, chemistry.Reagents[ReagentKind.Stabilizer]);

            chemistry.BeginRound();
            unit.AddStatus(BuffStatus.Create(999984, "verify_hot", "regen", ally, unit, new PercentHealOverTimeEffect(1), 3, isBeneficial: true));
            Near("HoT application not catalyst", 0, chemistry.Reagents[ReagentKind.Catalyst]);
            unit.BeginTurn();
            Near("HoT tick produces fuel", half, chemistry.Reagents[ReagentKind.Fuel]);
            unit.DebugClearStatuses();
            chemistry.BeginRound();
            for (int i = 0; i < 6; i++)
            {
                unit.BeginTurn(); unit.CastNormalCode();
                yield return Settle(1.8f);
            }
            Equal("six real normals synthesis counter", 6, chemistry.Reagents.NormalActions);
            Near("self synthesis all three", chemistry.Reagents.Capacity / 4f, chemistry.Reagents.Batch);
            Equal("normal readiness capped", 3, unit.ManaCurr);
            Assert("three reagents enable ultimate", unit.ActiveUltimateCode.HasValidTarget(), "ready", "checked");
            float batch = chemistry.Reagents.Batch;
            unit.CastUltimateCode();
            Equal("cast start does not consume readiness", 3, unit.ManaCurr);
            ControlStatuses.ApplyFreeze(unit, enemy, 1);
            yield return Settle(1);
            Near("interruption preserves reagents", batch, chemistry.Reagents.Batch);
            Equal("interruption preserves readiness", 3, unit.ManaCurr);
            unit.DebugClearStatuses();
            MakeUnavoidable(enemy); MakeUnavoidable(enemy2);
            unit.ControlEnds();
            int hp1 = enemy.HpCurr, hp2 = enemy2.HpCurr;
            int activated = 0;
            unit.AddListener<EventContext>(UnitEventType.OnUltimateActivates, _ => activated++);
            unit.CastUltimateCode();
            yield return Settle(1.5f);
            Equal("successful reaction consumes readiness", 0, unit.ManaCurr);
            Near("successful reaction consumes balanced batch", 0, chemistry.Reagents.Batch);
            Equal("ultimate activation emitted once", 1, activated);
            Assert("hits every enemy", enemy.HpCurr < hp1 && enemy2.HpCurr < hp2, "both damaged", $"{hp1-enemy.HpCurr}/{hp2-enemy2.HpCurr}");
            Assert("ultimate attaches fire", enemy.HasCombatElement(UnitElement.Pyro) && enemy2.HasCombatElement(UnitElement.Pyro), "both fire", "checked");
            Equal("reaction preserves normal phase", 6, chemistry.Reagents.NormalActions);

            // 준비도가 가득 차도 시약이 없으면 일반행동을 막거나 헛궁극기를 쓰지 않는다.
            chemistry.BeginRound();
            unit.FillUltimateResource(false);
            Assert("empty reagents wait at full readiness", !unit.ActiveUltimateCode.HasValidTarget(), "waiting", "checked");
            chemistry.Receive(ally, ReagentKind.Fuel);
            chemistry.Receive(ally, ReagentKind.Stabilizer);
            chemistry.Receive(ally, ReagentKind.Catalyst);
            int beforeAutomatic = activated;
            game.ActionScheduler.BeginRound();
            game.ActionScheduler.Tick(.016f);
            yield return Settle(1.5f);
            Equal("scheduler automatically fires supplied reaction", beforeAutomatic + 1, activated);
            game.ActionScheduler.EndRound();
            chemistry.BeginRound();
            unit.BeginTurn();
            game.ActionScheduler.BeginRound();
            game.ActionScheduler.EnqueueAdditional(unit, "verify_lavo_extra", "추가행동", () =>
                unit.Invoke(UnitEventType.OnNormalActionResolved, new EventContext(unit)));
            game.ActionScheduler.Tick(.016f);
            Equal("instant additional cannot synthesize", 0, chemistry.Reagents.NormalActions);
            Equal("instant additional cannot prepare", 0, unit.ManaCurr);
            game.ActionScheduler.EndRound();

            // 버프 연장은 새 시전자의 지원으로 집계한다.
            unit.AddStatus(BuffStatus.Create(999985, "verify_extend", "buff", ally, unit,
                new CritChanceBuffEffect(.01f), 2, StatusStackPolicy.ExtendDuration));
            unit.AddStatus(BuffStatus.Create(999985, "verify_extend", "buff", ally2, unit,
                new CritChanceBuffEffect(.01f), 2, StatusStackPolicy.ExtendDuration));
            Near("different caster refresh credited", chemistry.Reagents.Capacity, chemistry.Reagents[ReagentKind.Catalyst]);
            unit.AddShield(10, ally);
            grid.OnRoundEnd();
            Assert("round end resets chemistry", !chemistry.Active && chemistry.Reagents.NormalActions == 0 && chemistry.Reagents.Batch == 0 && unit.ManaCurr == 0, "cleared", "checked");
            grid.OnRoundStart();
            chemistry.Receive(ally, ReagentKind.Fuel);
            unit.Die(enemy);
            Assert("death resets all reagents", !chemistry.Active && chemistry.Reagents[ReagentKind.Fuel] == 0 && unit.ManaCurr == 0, "cleared", "checked");
            Assert("revive restores playable character", unit.ReviveAfterBattle(), "revived", "checked");
            Assert("revive restores portrait", unit.currentCell.GetComponentsInChildren<SpriteRenderer>()
                .Any(renderer => renderer.sprite != null && renderer.sprite.name == "LAVOISIER_PORTRAIT"), "portrait restored", "checked");
            grid.OnRoundStart();
            Assert("revived next round receiver active", chemistry.Active, "active", "checked");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield return LavoisierScreens(unit, ally);
            Clear();
        }

        private IEnumerator LavoisierScreens(Unit unit, Unit ally)
        {
            Status = "라부아지에 UI 렌더링";
            var write = typeof(SaveSystem).GetMethod("WriteString", BindingFlags.Static | BindingFlags.NonPublic);
            ChemistryForScreenshot(unit, ally);
            yield return CaptureLavoisierScreen("Lavoisier-Combat");
            write.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });
            var selection = new Screens.CharacterSelectScreen();
            selection.Show();
            typeof(Screens.CharacterSelectScreen).GetField("_hovered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(selection, 8);
            typeof(Screens.CharacterSelectScreen).GetMethod("RefreshPreview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selection, null);
            Assert("locked tile visible", GameObject.Find("Unit8") != null, "visible", "checked");
            typeof(Screens.CharacterSelectScreen).GetMethod("OnTileClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selection, new object[] {8});
            Assert("locked tile cannot select", CharacterSelectionManager.Instance.MainUnitId != 8, "rejected", "checked");
            yield return CaptureLavoisierScreen("Lavoisier-Locked");
            selection.Hide();
            SaveSystem.AddTrainedCharacter(21);
            Screens.CharacterUnlockDialog.ShowPending();
            yield return CaptureLavoisierScreen("Lavoisier-Unlocked");
            var dialog = GameObject.Find("CharacterUnlock");
            foreach (var label in dialog.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                label.ForceMeshUpdate();
                Assert("unlock text fits " + label.name, !label.isTextOverflowing, "no overflow", label.text);
            }
            dialog.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();
            Equal("dialog acknowledges unlock", 0, SaveSystem.LoadTrainedCharacters().pendingCharacterUnlockIds.Count);
            yield return null;
            selection.Show();
            typeof(Screens.CharacterSelectScreen).GetMethod("OnTileClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selection, new object[] {8});
            Equal("unlocked tile selects main", 8, CharacterSelectionManager.Instance.MainUnitId);
            selection.Hide();
            var shop = new Screens.ShopScreen();
            shop.Show();
            yield return CaptureLavoisierScreen("Lavoisier-Shop");
            shop.Hide();
        }

        private static void ChemistryForScreenshot(Unit unit, Unit ally)
        {
            unit.Chemistry.Receive(ally, ReagentKind.Fuel);
            unit.Chemistry.Receive(ally, ReagentKind.Stabilizer);
            unit.Chemistry.Receive(ally, ReagentKind.Catalyst);
            unit.FillUltimateResource(false);
        }

        // 원화를 편집하지 않고 실제 게임 카메라/Canvas를 렌더링하여 UI를 검수한다.
        private IEnumerator CaptureLavoisierScreen(string name)
        {
            var camera = Camera.main;
            var texture = new RenderTexture(1920, 1080, 24);
            var originalTarget = camera.targetTexture;
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            camera.targetTexture = texture;
            foreach (var canvas in overlays)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("Logs/" + name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = originalTarget;
            foreach (var canvas in overlays) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(pixels);
            texture.Release(); Object.Destroy(texture);
        }
    }
}
#endif
