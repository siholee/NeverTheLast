using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Passive;
using Combat;
using Entities;
using Effects.Vfx.Presets;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>실제 추가 발동·보호막 사건만 표시한다. 구독 수명은 해당 행동의 연출 코루틴에 묶인다.</summary>
    public sealed class YamaVarunaFx : IDisposable
    {
        private readonly string _key;
        private bool _disposed;
        private readonly Action<EventContext> _cleanup;
        private readonly Unit _caster;
        private readonly HashSet<Unit> _shieldTargets = new();

        private YamaVarunaFx(string key, Unit caster)
        {
            _key = key;
            _caster = caster;
            DamageOverTimeDetonation.AnyDetonationFinished += OnDetonation;
            Unit.AnyShieldGranted += OnShield;
            VarunaLawLevee.AnyLeveeShield += OnLevee;
            _cleanup = _ => Dispose();
            caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
            caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            ActionScheduler.AnyActionFinished += OnActionFinished;
        }

        public static IDisposable Observe(string key, Unit caster)
            => caster != null && (key.StartsWith("yama.") || key.StartsWith("varuna.")) ? new YamaVarunaFx(key, caster) : null;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DamageOverTimeDetonation.AnyDetonationFinished -= OnDetonation;
            Unit.AnyShieldGranted -= OnShield;
            VarunaLawLevee.AnyLeveeShield -= OnLevee;
            _caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
            _caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            ActionScheduler.AnyActionFinished -= OnActionFinished;
        }

        public static bool PlayMotif(VfxPartData part, Unit caster, Unit target, Vector3 point,
            int layer, Vector2 size, float angle)
        {
            if (part.renderer == "yvShield")
            {
                float cardSize = VfxSpace.CardSize;
                Vector2 rimSize = size * cardSize;
                int rimOrder = VfxSpace.OverlaySortingOrder + 40;
                // 몸체는 얇게 두고 은청 경계만 또렷하게 세워 보호받은 대상을 한눈에 구분한다.
                SpriteFx.Play(VfxSpace.Ring, point, new Color(0.43f, 0.67f, 0.76f, 0.48f), false,
                    rimSize * 1.05f, rimSize * 1.05f, 0.38f, fadeIn: 0.24f, fadeOutFrom: 0.50f,
                    sortingLayer: layer, order: rimOrder)?.Follow(target?.transform);
                SpriteFx.Play(VfxSpace.Ring, point, new Color(0.73f, 0.89f, 0.97f, 0.82f), false,
                    rimSize, rimSize, 0.38f, fadeIn: 0.24f, fadeOutFrom: 0.50f,
                    sortingLayer: layer, order: rimOrder + 1)?.Follow(target?.transform);
                HandpaintFx.PlayAt(point, layer, HandpaintFx.Ball1Blue, size,
                    new Color(0.60f, 0.80f, 0.85f), new Color(0.85f, 0.94f, 0.98f),
                    rotation: 210f, heldFrame: 4, peakAlpha: 0.60f, glowAlpha: 0.22f,
                    peakHoldSeconds: 0.12f, desaturate: 1f, steadyBody: true, calmLight: true);
                return true;
            }
            if (part.renderer == "yvYamaImpact")
            {
                float cardSize = VfxSpace.CardSize;
                int impactOrder = VfxSpace.OverlaySortingOrder + 42;
                // 400% 판결은 같은 낙뢰의 크기가 아니라 짧은 금백 척추와 접점 정점으로 구별한다.
                Vector2 spine = new Vector2(0.62f, 0.045f) * cardSize;
                SpriteFx.Play(VfxArt.Streak, point, new Color(0.72f, 0.58f, 0.85f, 0.74f), false,
                    spine * new Vector2(1f, 2.2f), spine * new Vector2(1f, 2.2f), 0.18f,
                    90f, fadeIn: 0.24f, fadeOutFrom: 0.54f, sortingLayer: layer, order: impactOrder);
                SpriteFx.Play(VfxArt.Streak, point, new Color(1f, 0.94f, 0.75f, 0.90f), false,
                    spine, spine, 0.16f, 90f, fadeIn: 0.24f, fadeOutFrom: 0.48f,
                    sortingLayer: layer, order: impactOrder + 1);
                HandpaintFx.PlayAt(point, layer, HandpaintFx.Impact1Yellow, size,
                    new Color(0.77f, 0.66f, 0.86f), new Color(1f, 0.95f, 0.82f),
                    heldFrame: 3, peakAlpha: 0.70f, glowAlpha: 0.26f, desaturate: 1f, steadyBody: true, calmLight: true);
                return true;
            }
            if (part.renderer != "yvLaw" && part.renderer != "yvLawHit" && part.renderer != "yvVerdict") return false;
            float card = VfxSpace.CardSize;
            bool verdict = part.renderer == "yvVerdict";
            bool hit = part.renderer == "yvLawHit";
            Color gold = new Color(0.91f, 0.81f, 0.59f, 0.70f);
            Color ink = new Color(0.67f, 0.59f, 0.45f, 0.28f);
            point += Vector3.down * card * (hit ? 0.10f : 0.12f);
            Vector2 loop = new Vector2(hit ? 0.78f : 0.60f, hit ? 0.34f : 0.28f) * card;
            int order = VfxSpace.OverlaySortingOrder + 36;
            // 금장 의복의 매듭을 작은 교차 고리로 옮긴다. 얼굴·하단 HUD에 닿는 전신 원은 쓰지 않는다.
            for (int side = -1; side <= 1; side += 2)
            {
                float turn = side * (verdict ? 52f : 28f);
                SpriteFx.Play(VfxSpace.Ring, point, ink, false, loop * 1.12f, loop * 1.12f,
                    0.40f, turn, fadeIn: 0.24f, fadeOutFrom: 0.50f, sortingLayer: layer, order: order);
                SpriteFx.Play(VfxSpace.Ring, point, gold, false, loop, loop,
                    0.40f, turn, fadeIn: 0.24f, fadeOutFrom: 0.50f, sortingLayer: layer, order: order + 1);
            }
            if (!hit)
            {
                Color body = verdict ? new Color(0.72f, 0.59f, 0.84f) : new Color(0.52f, 0.78f, 0.86f);
                Color core = verdict ? new Color(0.92f, 0.82f, 0.97f) : new Color(0.80f, 0.96f, 1f);
                HandpaintFx.PlayAt(point, layer,
                    new HandpaintSheet(part.sheet, part.columns, part.rows, part.fps, new Vector2(0.5f, 0.5f)),
                    size * 0.72f, body, core, heldFrame: part.peakFrame, peakAlpha: 0.65f,
                    glowAlpha: 0.20f, desaturate: 1f, steadyBody: true, calmLight: true);
            }
            return true;
        }

        private void OnActionFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            if (unit == _caster) Dispose();
        }

        private void OnDetonation(Unit trigger, Unit target, int delivered)
        {
            if (trigger != _caster || delivered <= 0 || _key == "yama.special") return;
            // 직접 피격 정점에서 이어지되 첫 몸체가 걷힌 뒤 짧은 잔광 하나로 모든 지속피해의 정산을 묶는다.
            VfxRunner.Run(DetonationTail(target));
        }

        private IEnumerator DetonationTail(Unit target)
        {
            yield return new WaitForSeconds(0.12f);
            if (target == null || !target.isActive) yield break;
            VfxComposer.PlayAffectedSlot(_key, 0, _caster, target);
            AudioManager.PlayCue("yama.detonation");
        }

        private void OnShield(Unit target, Unit source, int amount)
        {
            if (source != _caster || _caster.ID != 66 || amount <= 0 || target == null) return;
            // 초월기의 제방과 파티 보호막은 같은 정점에 누적된다. 양은 실제 HUD가 보여 주고 구체는 대상당 하나다.
            if (_shieldTargets.Add(target))
            {
                VfxComposer.PlayAffectedSlot(_key, 0, _caster, target);
                CardMotion.ViewOf(target)?.PulseSupportHud(true, false, new Color(0.35f, 0.78f, 1f));
            }
        }

        private void OnLevee(Unit varuna, int actionId, int amount)
        {
            if (varuna != _caster || amount <= 0) return;
            AudioManager.PlayAction(varuna, SfxActionSlot.Passive, SfxCuePhase.Resolve);
        }
    }
}
