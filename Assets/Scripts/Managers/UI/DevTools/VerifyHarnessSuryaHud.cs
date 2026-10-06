#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Text;
using BaseClasses;
using Codes.Passive;
using Core;
using Effects.Vfx;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static void SuryaHudObservation(VerifyScenarioRequest config, string phase,
            Entities.View.UnitCardView.ResourceHudDiagnostic value)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Logs/VedicTrio20261005/vsfx/surya-hud/runtime-diagnostics"));
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, config.name + ".jsonl"),
                "{\"phase\":\"" + phase + "\",\"state\":" + JsonUtility.ToJson(value) + "}\n", new UTF8Encoding(false));
        }

        private IEnumerator ScenarioSuryaHud(VerifyScenarioRequest config)
        {
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            Unit surya = AliveAllies().FirstOrDefault(unit => unit.ID == 67);
            if (surya == null) { FailCurrent("수리야 HUD 편성 없음"); yield break; }
            // 준비 HUD만 격리한다. 생산 U 효과를 직접 실행하며 추공 연쇄 검증은 다른 core/visual 시나리오가 맡는다.
            foreach (Unit ally in AliveAllies())
                foreach (var passive in ally.ActivePassiveCodes.OfType<AgniBurnToTheEnd>()) passive.StopCode();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            using (var recorder = new YvRecorder())
                yield return YvRunScheduled(scheduler => surya.FillUltimateResource(),
                    () => recorder.Finished.Any(item => item.Unit == surya && item.Kind == ActionScheduler.ActionKind.Ultimate));
            var view = CardMotion.ViewOf(surya);
            yield return WaitUnscaled(0.35f);
            yield return EndOfFrame;
            Capture("surya_hud_actual_u_production_before_probe");
            var prepared = view.VerificationResourceHud();
            RecordMechanic("실제 수리야 U 준비 자원", "탄2·강탄1", JsonUtility.ToJson(prepared), prepared.ammo == 2 && prepared.strong == 1);
            view.VerificationLegacyResourcePlacement(true);
            yield return EndOfFrame;
            var legacy = view.VerificationResourceHud();
            SuryaHudObservation(config, "legacy_placement", legacy);
            Capture("surya_hud_legacy_prepared");
            view.Tick(); view.VerificationLegacyResourcePlacement(false);
            yield return EndOfFrame;
            Capture("surya_hud_actual_u_prepared");

            // 같은 Unit 자원 API를 통해 0~2/강탄 켜짐·꺼짐과 실제 렌더의 대응을 검사한다.
            foreach (bool strong in new[] { false, true }) foreach (int ammo in new[] { 2, 1, 0 })
            {
                surya.AddCombatResource(SuryaBeforeSunset.BulletResourceId, ammo - surya.GetCombatResource(SuryaBeforeSunset.BulletResourceId));
                surya.AddCombatResource(SuryaBeforeSunset.EmpoweredResourceId, (strong ? 1 : 0) - surya.GetCombatResource(SuryaBeforeSunset.EmpoweredResourceId));
                yield return WaitUnscaled(0.12f);
                yield return EndOfFrame;
                Capture("surya_hud_ammo_" + ammo + "_strong_" + strong + "_production_before_probe");
                var state = view.VerificationResourceHud();
                bool correct = state.combatHud && state.maximum == 2 && state.ammo == ammo && state.strong == (strong ? 1 : 0)
                    && state.ammoEnabled && state.ammoText == "탄 " + ammo + "/2" && state.ammoMeshVisible
                    && state.strongEnabled == strong && (!strong || state.strongMeshVisible)
                    && state.nameText == surya.UnitName && state.unitName == "수리야";
                RecordMechanic("잔탄" + ammo + " 강탄" + strong + " 실제 HUD", "자원·메시·이름 일치", JsonUtility.ToJson(state), correct);
                SuryaHudObservation(config, "ammo_" + ammo + "_strong_" + strong, state);
            }
            GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
            GameManager.Instance.enabled = enabled;
        }
    }
}
#endif
