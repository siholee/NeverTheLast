#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Combat;
using Core;
using Entities;
using Effects.Vfx;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private bool _trioCaptureActive;
        private bool _trioSparseMode;
        private readonly HashSet<string> _trioSparseCaptureKeys = new();

        private void TrioSparseDamage(Unit target, DamageContext context)
        {
            if (!_trioSparseMode || !_trioCaptureActive || context.Attacker == null || context.CodeType == BaseEnums.CodeType.Effect) return;
            int source = context.Attacker.ID;
            if (source is not (62 or 63 or 64 or 66 or 67)) return;
            string key = GameManager.Instance.ActionScheduler.CurrentActionId + "_" + source + "_" + context.CodeType;
            if (_trioSparseCaptureKeys.Add(key)) StartCoroutine(TrioSparseDamageFrame(key));
        }

        private void TrioSparseAffected(VfxSlotPlaybackEvent value)
        {
            if (!_trioSparseMode || !_trioCaptureActive || value.Phase != "start" || value.SlotName != "affected") return;
            if (value.PartId is not ("affected.agni.ultimate" or "affected.trio.weakness.all" or "affected.varuna.shield" or "affected.agni.focused_center" or "affected.surya.wanted")) return;
            string key = GameManager.Instance.ActionScheduler.CurrentActionId + "_" + value.PartId;
            // 같은 파티·적 집단에 발생한 문양은 한 장에서 함께 확인한다.
            if (_trioSparseCaptureKeys.Add(key)) StartCoroutine(TrioSparseAffectedFrame(key));
        }

        private IEnumerator TrioSparseAffectedFrame(string key)
        {
            yield return WaitUnscaled(.12f);
            yield return EndOfFrame;
            Capture("trio_sparse_affected_" + key);
        }

        private IEnumerator TrioSparseDamageFrame(string key)
        {
            yield return EndOfFrame;
            Capture("trio_sparse_actual_hit_" + key);
        }

        private IEnumerator ScenarioVedicTrioVisual(VerifyScenarioRequest config)
        {
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            _v13PeakDiagnostics = config.captureVfxPeakDiagnostics;
            Unit vayu = AliveAllies().FirstOrDefault(unit => unit.ID == 64);
            Unit agni = AliveAllies().FirstOrDefault(unit => unit.ID == 62);
            Unit surya = AliveAllies().FirstOrDefault(unit => unit.ID == 67);
            if (vayu == null || agni == null || surya == null)
            {
                FailCurrent("베다 연출 검수 편성에 바유·아그니·수리야가 필요하다.");
                GameManager.Instance.enabled = enabled;
                yield break;
            }
            _trioSparseMode = config.name.Contains("sparse");
            if (_trioSparseMode)
            {
                Unit.AnyDamageIncoming += TrioSparseDamage;
                VfxComposer.SlotPlayed += TrioSparseAffected;
            }
            bool done = false;
            Coroutine sequence = StartCoroutine(TrioVisualSequence(vayu, agni, surya, () => done = true));
            float deadline = Time.realtimeSinceStartup + 240f;
            float next = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup < next) { yield return null; continue; }
                yield return EndOfFrame;
                if (_trioCaptureActive && !_trioSparseMode) Capture("trio_visual");
                next = Mathf.Max(next + 1f / 30f, Time.realtimeSinceStartup);
            }
            if (!done) { StopCoroutine(sequence); FailCurrent("베다 연출 검수 제한 시간 초과"); }
            if (_trioSparseMode)
            {
                Unit.AnyDamageIncoming -= TrioSparseDamage;
                VfxComposer.SlotPlayed -= TrioSparseAffected;
            }
            _trioSparseMode = false;
            _trioCaptureActive = false;
            _v13PeakDiagnostics = false;
            GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
            GameManager.Instance.enabled = enabled;
        }

        private IEnumerator TrioVisualSequence(Unit vayu, Unit agni, Unit surya, Action done)
        {
            using var trace = new YvVisualTrace();
            int accepted = 0, firedAgni = 0, firedSurya = 0;
            Action<Unit, Unit, AgniBurnToTheEnd.TicketKind> ticket = (owner, other, kind) =>
            {
                if (owner != agni) return;
                if (kind == AgniBurnToTheEnd.TicketKind.Accepted || kind == AgniBurnToTheEnd.TicketKind.Held) accepted++;
                if (kind == AgniBurnToTheEnd.TicketKind.Fired) firedAgni++;
                VerifyEvents.Raise("trio_agni_ticket_" + kind + "_action=" + GameManager.Instance.ActionScheduler.CurrentActionId);
            };
            // 수리야 리워크는 추공 대신 일반/특수행동의 종언탄이다. 실제 방어 무시 피해로 기록한다.
            Action<Unit, DamageContext> shot = (target, context) =>
            {
                if (context.Attacker==surya && context.DefenseStatMultiplier<1f) firedSurya++;
            };
            AgniBurnToTheEnd.AnyTicket += ticket;
            Unit.AnyDamageIncoming += shot;
            YvAddTestStatus(surya,99969,"verify_trio_mark_accuracy",new Effects.Buffs.StatModifierEffect(BaseEnums.StatType.SpecialAttack,100000f));
            int audioStart = _result.sfxEvents.Count;
            try
            {
                // 실제 일반·궁극기로 패시브를 예약한다. PlayCue를 직접 호출하는 검사는 하지 않는다.
                Unit indra = AliveAllies().FirstOrDefault(unit => unit.ID == IndraId);
                Unit varuna = AliveAllies().FirstOrDefault(unit => unit.ID == VarunaId);
                Unit[] casters = indra == null ? new[] { surya, vayu, agni, vayu, vayu, agni, surya, vayu }
                    : varuna == null ? new[] { surya, vayu, agni, vayu, vayu, agni, surya, vayu, indra }
                    : new[] { surya, vayu, agni, vayu, vayu, agni, surya, vayu, varuna, indra };
                int[] slots = indra == null ? new[] { 0, 0, 0, 1, 2, 1, 1, 0 } : varuna == null ? new[] { 0, 0, 0, 1, 2, 1, 1, 0, 1 }
                    : new[] { 0, 0, 0, 1, 2, 1, 1, 0, 0, 1 };
                for (int i = 0; i < casters.Length; i++)
                {
                    Unit caster = casters[i]; int slot = slots[i];
                    foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
                    yield return WaitUnscaled(8f); // 긴 원음을 임의 절단하지 않고 다음 독립 행동과 분리한다.
                    int beforeAccepted = accepted;
                    var kind = slot == 0 ? ActionScheduler.ActionKind.Normal : slot == 1 ? ActionScheduler.ActionKind.Ultimate : ActionScheduler.ActionKind.Special;
                    _trioSparseCaptureKeys.Clear();
                    VerifyEvents.Raise("trio_begin_" + caster.ID + "_" + kind);
                    _trioCaptureActive = true;
                    using (var recorder = new YvRecorder())
                    {
                        yield return YvRunScheduled(scheduler =>
                        {
                            if (slot == 0) scheduler.AdvanceAction(caster, 1f);
                            else if (slot == 1) caster.FillUltimateResource();
                            else scheduler.EnqueueSpecial(caster, "trio_visual", caster.ActiveSpecialCode.CodeName, caster.CastSpecialCode);
                        }, () => recorder.Finished.Any(item => item.Unit == caster && item.Kind == kind)
                            && (caster != indra || slot != 1 || recorder.Finished.Count(item => item.Kind == ActionScheduler.ActionKind.Special) >= SpecialAction.Participants(indra).Count)
                            && recorder.Finished.Count(item => item.Kind == ActionScheduler.ActionKind.Additional || item.Kind == ActionScheduler.ActionKind.PriorityAdditional)  >= accepted - beforeAccepted, timeout: caster==indra && slot==1 ? 45f : 20f);
                        int expected = (caster == agni || caster == indra || caster == surya) && slot == 1 ? 0 : (caster == vayu && slot != 0 || caster==surya && slot==2) ? AliveEnemies().Count : 1;
                        BaseEnums.CodeType type = caster==surya && slot==2 ? BaseEnums.CodeType.Normal : slot == 0 ? BaseEnums.CodeType.Normal : slot == 1 ? BaseEnums.CodeType.Ultimate : BaseEnums.CodeType.Special;
                        int hits = recorder.IncomingTargets.Count(item => item.Context.Attacker == caster && item.Context.CodeType == type);
                        RecordMechanic("베다 " + caster.ID + " " + kind + " 실제 피해", expected.ToString(), hits.ToString(), hits == expected);
                    }
                    yield return WaitUnscaled(0.9f);
                    _trioCaptureActive = false;
                    Capture("trio_end_" + caster.ID + "_" + kind);
                }
                var sounds = _result.sfxEvents.Skip(audioStart).ToList();
                foreach (string character in new[] { "arlan", "acheron", "jade" })
                {
                    var voices = sounds.Where(item => item.played && item.mixerGroup == "VOICE" && item.clip.Contains(character)).ToList();
                    RecordMechanic("베다 지정 원음 " + character, "실제 재생·pitch 1", voices.Count.ToString(), voices.Count > 0 && voices.All(item => Mathf.Approximately(item.pitch, 1f)));
                }
                int agniHits = sounds.Count(item => item.played && (item.cue == "agni.follow_up.hit" || item.cue == "agni.focused_follow_up.hit"));
                int suryaHits = sounds.Count(item => item.played && item.cue == "surya.finale.hit");
                RecordMechanic("아그니 생산 추공 타격음", firedAgni.ToString(), agniHits.ToString(), firedAgni > 0 && agniHits == firedAgni);
                RecordMechanic("수리야 생산 종언탄 타격음", firedSurya.ToString(), suryaHits.ToString(), firedSurya > 0 && suryaHits == firedSurya);
            }
            finally { AgniBurnToTheEnd.AnyTicket -= ticket; Unit.AnyDamageIncoming -= shot; surya.RemoveStatusByKey("verify_trio_mark_accuracy"); }
            done();
        }
    }
}
#endif
