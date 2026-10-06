#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Entities;
using Effects.Vfx;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private bool _yvCaptureActive;

        private IEnumerator ScenarioYvVisual(VerifyScenarioRequest config)
        {
            bool wasEnabled = true;
            yield return YvPrepareScene(config, value => wasEnabled = value);
            _v13PeakDiagnostics = config.captureVfxPeakDiagnostics;
            Unit yama = AliveAllies().First(unit => unit.ID == YamaId);
            Unit varuna = AliveAllies().First(unit => unit.ID == VarunaId);
            Unit pyg = AliveAllies().First(unit => unit.ID == PygmalionId);
            if (config.name.Contains("watchdog"))
            {
                yield return YvWatchdog(yama, AliveEnemies().First());
                yield return EndOfFrame;
                Capture("yv_watchdog_complete");
                GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
                GameManager.Instance.enabled = wasEnabled;
                yield break;
            }
            Text label = CreateStatusPreviewLabel(out GameObject labelRoot);
            label.font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 32);
            bool done = false;
            Coroutine sequence = StartCoroutine(YvVisualSequence(yama, varuna, pyg, label, config.name.Contains("voice") ? AliveAllies().First(unit => unit.ID == IndraId) : null, () => done = true));
            float deadline = Time.realtimeSinceStartup + 90f;
            float next = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup < deadline)
            {
                while (Time.realtimeSinceStartup < next) yield return null;
                yield return EndOfFrame;
                if (_yvCaptureActive) Capture("yv_visual");
                next = Mathf.Max(next + 1f / 30f, Time.realtimeSinceStartup);
            }
            if (!done) { StopCoroutine(sequence); FailCurrent("야마·바루나 연출 캡처 시간 초과"); }
            Destroy(labelRoot);
            _v13PeakDiagnostics = false;
            GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
            GameManager.Instance.enabled = wasEnabled;
        }

        private IEnumerator YvWatchdog(Unit caster, Unit target)
        {
            var hit = VfxComposer.Slot("yama.normal", "hit", 0);
            float priorOffset = hit.timeOffset;
            int calls = 0;
            try
            {
                yield return VfxComposer.PlaySingleTarget("yama.normal", caster, target, () => calls++);
                RecordMechanic("정상 시트 정점 콜백", "1", calls.ToString(), calls == 1);
                calls = 0;
                hit.timeOffset = 20f;
                yield return VfxComposer.PlaySingleTarget("yama.normal", caster, target, () => calls++);
                RecordMechanic("감시 시간 초과 콜백", "1", calls.ToString(), calls == 1);
                yield return WaitUnscaled(3.0f);
                int peaks = _result.vfxSlots.Count(item => item.scenario == _currentScenario && item.skillKey == "yama.normal" && item.slot == "hit" && item.phase == "peak");
                RecordMechanic("늦은 정점 실제 도착", "정상+늦은 정점 2", peaks.ToString(), peaks == 2);
                RecordMechanic("늦은 정점 중복 억제", "1 유지", calls.ToString(), calls == 1);
            }
            finally { hit.timeOffset = priorOffset; }
        }

        private sealed class YvVisualTrace : IDisposable
        {
            public YvVisualTrace()
            {
                Unit.AnyDamageIncoming += Damage;
                DamageOverTimeDetonation.AnyDetonationFinished += Detonation;
                ActionScheduler.AnyActionStarted += Started;
                AudioManager.SfxEventRecorded += Sound;
            }
            private static string Execution()
            {
                var scheduler = GameManager.Instance.ActionScheduler;
                return $"action={scheduler.CurrentActionId}_executing={scheduler.ExecutingUnit?.ID}";
            }
            private static void Damage(Unit target, DamageContext context)
                => VerifyEvents.Raise($"yv_trace_damage_{Execution()}_source={context.Attacker?.ID}_type={context.CodeType}_target={target?.ID}");
            private static void Detonation(Unit trigger, Unit target, int count)
                => VerifyEvents.Raise($"yv_trace_detonation_{Execution()}_source={trigger?.ID}_target={target?.ID}_count={count}");
            private static void Started(Unit unit, ActionScheduler.ActionKind kind, string label)
                => VerifyEvents.Raise($"yv_trace_start_{Execution()}_unit={unit?.ID}_kind={kind}");
            private static void Sound(SfxPlaybackEvent value)
            {
                if (value.mixerGroup == "VOICE") VerifyEvents.Raise($"yv_trace_voice_{Execution()}_cue={value.cue}_played={value.played}_reason={value.reason}");
            }
            public void Dispose()
            {
                Unit.AnyDamageIncoming -= Damage;
                DamageOverTimeDetonation.AnyDetonationFinished -= Detonation;
                ActionScheduler.AnyActionStarted -= Started;
                AudioManager.SfxEventRecorded -= Sound;
            }
        }

        private IEnumerator YvVisualSequence(Unit yama, Unit varuna, Unit pyg, Text label, Unit indra, Action done)
        {
            using (var trace = new YvVisualTrace())
            foreach (Unit caster in indra != null ? new[] { indra } : new[] { yama, varuna })
            for (int slot = 0; slot < 3; slot++)
            {
                foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
                foreach (Unit enemy in AliveEnemies())
                {
                    YvResetEnemy(enemy);
                    if (caster == yama)
                    {
                        YvPoison(pyg, enemy);
                        YvBurn(pyg, enemy);
                        YvWeathering(pyg, enemy);
                        YvShock(varuna, enemy);
                    }
                }
                yield return WaitUnscaled(1.3f);
                string action = slot == 0 ? "normal" : slot == 1 ? "ultimate" : "special";
                string key = (caster == yama ? "yama." : caster == varuna ? "varuna." : slot == 0 ? "indra.thunderbolt." : slot == 1 ? "indra.gathering." : "indra.vajra.") + (caster == indra ? "" : action);
                label.text = (caster == yama ? "야마 " : caster == varuna ? "바루나 " : "인드라 ") + action;
                _yvCaptureActive = true;
                int soundStart = _result.sfxEvents.Count;
                VerifyEvents.Raise("yv_begin_" + key);
                int expectedTargets = caster == indra && slot == 1 ? 0 : slot == 0 ? 1 : AliveEnemies().Count;
                using (var recorder = new YvRecorder())
                {
                    var kind = slot == 0 ? ActionScheduler.ActionKind.Normal : slot == 1 ? ActionScheduler.ActionKind.Ultimate : ActionScheduler.ActionKind.Special;
                    yield return YvRunScheduled(scheduler =>
                    {
                        if (slot == 0) scheduler.AdvanceAction(caster, 1f);
                        else if (slot == 1) caster.FillUltimateResource();
                        else scheduler.EnqueueSpecial(caster, "yv_visual", caster.ActiveSpecialCode.CodeName, caster.CastSpecialCode);
                    }, () => recorder.Finished.Any(item => item.Unit == caster && item.Kind == kind) &&
                        (caster != indra || slot != 1 || recorder.Finished.Count(item => item.Kind == ActionScheduler.ActionKind.Special) >= SpecialAction.Participants(indra).Count));
                    var type = slot == 0 ? BaseEnums.CodeType.Normal : slot == 1 ? BaseEnums.CodeType.Ultimate : BaseEnums.CodeType.Special;
                    int hits = recorder.IncomingTargets.Count(item => item.Context.Attacker == caster && item.Context.CodeType == type);
                    RecordMechanic(key + " 직접 피해 한 번씩", expectedTargets.ToString(), hits.ToString(), hits == expectedTargets);
                    int dots = recorder.DetonationEnds.Count(item => item.Trigger == caster && item.Delivered > 0);
                    int shields = recorder.Shields.Count(item => item.Source == caster);
                    if (caster == yama)
                        RecordMechanic(key + " 실제 정산", slot == 2 ? "0" : expectedTargets.ToString(), dots.ToString(), dots == (slot == 2 ? 0 : expectedTargets));
                    else if (caster == varuna)
                    {
                        int expectedShieldEvents = slot == 0 ? 1 : AliveAllies().Count + (slot == 1 ? 1 : 0);
                        RecordMechanic(key + " 실제 보호막", expectedShieldEvents.ToString(), shields.ToString(), shields == expectedShieldEvents);
                        int waters = recorder.Elements.Count(item => item.Source == caster);
                        RecordMechanic(key + " 물 부착", slot == 2 ? "0" : expectedTargets.ToString(), waters.ToString(), waters == (slot == 2 ? 0 : expectedTargets));
                    }
                }
                if (caster != indra)
                {
                int vfxHits = _result.vfxSlots.Count(item => item.scenario == _currentScenario && item.skillKey == key && item.slot == "hit" && item.slotIndex == 0 && item.phase == "peak");
                int hitSounds = _result.sfxEvents.Count(item => item.scenario == _currentScenario && item.cue == key + ".hit" && item.played);
                int affected = _result.vfxSlots.Count(item => item.scenario == _currentScenario && item.skillKey == key && item.slot == "affected" && item.phase == "start");
                int expectedAffected = caster == yama ? (slot == 2 ? 0 : expectedTargets) : (slot == 0 ? 1 : AliveAllies().Count);
                RecordMechanic(key + " 피격 정점", expectedTargets.ToString(), vfxHits.ToString(), vfxHits == expectedTargets);
                RecordMechanic(key + " 타격음 한 번", "1", hitSounds.ToString(), hitSounds == 1);
                RecordMechanic(key + " 실제 효과 연출", expectedAffected.ToString(), affected.ToString(), affected == expectedAffected);
                }
                var voiceEvents = _result.sfxEvents.Skip(soundStart).Where(item => item.played && item.mixerGroup == "VOICE").ToList();
                string characterClip = caster == yama ? "castorice" : caster == varuna ? "welt" : "ratio";
                bool ownVoice = voiceEvents.Count == 1 && voiceEvents.All(item => item.clip.Contains(characterClip) && Mathf.Approximately(item.pitch, 1f));
                RecordMechanic(key + " 지정 원음 보이스", "지정 캐릭터 1회·pitch 1", voiceEvents.Count.ToString(), ownVoice);
                if (caster == indra && slot == 1)
                {
                    bool blocked = _result.sfxEvents.Skip(soundStart).Any(item => !item.played && item.reason == "voice_group_busy");
                    RecordMechanic("팔방소집 보이스 중첩 제한", "가지 행동 보이스 제한", blocked.ToString(), blocked);
                }
                VerifyEvents.Raise("yv_end_" + key);
                yield return WaitUnscaled(0.50f);
                _yvCaptureActive = false;
                // 원음 길이는 게임 속도와 무관하다. 다음 독립 행동의 보이스 검수를 위해 기다린다.
                yield return WaitUnscaled(6.7f);
            }
            done();
        }
    }
}
#endif
