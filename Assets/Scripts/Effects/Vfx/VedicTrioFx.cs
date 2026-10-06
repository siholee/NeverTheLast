using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Entities;
using Effects.Vfx.Presets;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>베다 3인의 표적 국소 연출. 전투 판정과 자원 변경은 전달받은 코드 콜백만 소유한다.</summary>
    public static class VedicTrioFx
    {
        private sealed class WeaknessPresentation { public int Frame = -1; public int Elements; }
        private static readonly ConditionalWeakTable<Unit, WeaknessPresentation> WeaknessFrames = new();
        public static IEnumerator Single(string key, Unit caster, Unit target, Action onImpact)
        {
            yield return VfxComposer.PlaySingleTarget(key, caster, target, () =>
            {
                if (key == "surya.ultimate" && target != null)
                    VfxComposer.PlayAffectedPart(target, "hit.surya.wheel", caster);
                onImpact?.Invoke();
            });
            AudioManager.PlayCue(key + ".resolve");
        }

        /// <summary>이 입구가 수리야 추공 Cast/Hit/Resolve를 소유한다. 호출 코드에서 같은 큐를 추가하지 않는다.</summary>
        public static IEnumerator SuryaAdditional(Unit caster, Unit target, bool empowered, Action onImpact)
        {
            string key = empowered ? "surya.focused_follow_up" : "surya.follow_up";
            AudioManager.PlayCue(key + ".cast");
            yield return Single(key, caster, target, () =>
            {
                AudioManager.PlayCue(key + ".hit");
                onImpact?.Invoke();
            });
        }

        /// <summary>중심 강조는 실제 강화 티켓의 선택 대상만 따른다. 추가 피해나 불 부착은 생성하지 않는다.</summary>
        public static IEnumerator AgniAdditional(Unit caster, IReadOnlyList<Unit> targets, Unit centralTarget,
            bool empowered, Action onImpact)
        {
            string key = empowered ? "agni.focused_follow_up" : "agni.follow_up";
            AudioManager.PlayCue(key + ".cast");
            yield return LokapalaFx.Area(key, caster, targets, () =>
            {
                if (empowered && centralTarget != null && targets != null && targets.Contains(centralTarget))
                    VfxComposer.PlayAffectedPart(centralTarget, "affected.agni.focused_center", caster);
                AudioManager.PlayCue(key + ".hit");
                onImpact?.Invoke();
            });
        }

        /// <summary>BaseUltimate의 Cast를 재사용한다. 수혜 광은 실제 추가된 상태의 affected 경로가 표시한다.</summary>
        public static IEnumerator AgniUltimate(Unit caster, IReadOnlyList<Unit> beneficiaries, Action onBuff)
        {
            const string key = "agni.ultimate";
            if (caster == null) { onBuff?.Invoke(); yield break; }
            VfxComposer.PlayScreen(key, caster, caster);
            VfxComposer.PlaySlot(key, "cast", 0, caster, caster, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            yield return new WaitForSeconds(0.18f);
            AudioManager.PlayCue(key + ".hit");
            onBuff?.Invoke();
            yield return new WaitForSeconds(0.22f);
            AudioManager.PlayCue(key + ".resolve");
        }

        /// <summary>무피해 준비 행동 한 번. 보류 티켓의 실행·원소 이동은 게임 코드만 수행한다.</summary>
        public static IEnumerator AgniSpecialSetup(Unit caster, Action onSetup)
        {
            const string key = "agni.special_setup";
            if (caster == null) { onSetup?.Invoke(); yield break; }
            VfxComposer.PlayScreen(key, caster, caster);
            VfxComposer.PlaySlot(key, "cast", 0, caster, caster, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            yield return new WaitForSeconds(0.16f);
            AudioManager.PlayCue(key + ".hit");
            onSetup?.Invoke();
            yield return new WaitForSeconds(0.14f);
            AudioManager.PlayCue(key + ".resolve");
        }

        /// <summary>실제 재장전 완료 뒤 호출한다. 잔탄은 게임 코드가 넘긴 값만 표시한다.</summary>
        public static void Reload(Unit caster, int actualAmmo)
        {
            if (caster == null) return;
            Vector3 point = VfxSpace.WorldPoint(caster) + Vector3.down * VfxSpace.CardSize * 0.26f;
            int layer = VfxSpace.SortingLayer(caster);
            for (int i = 0; i < Mathf.Clamp(actualAmmo, 0, 2); i++)
                Mark(VfxArt.Diamond, point + Vector3.right * VfxSpace.CardSize * (0.32f + i * 0.10f),
                    new Vector2(0.06f, 0.13f), new Color(0.98f, 0.79f, 0.47f, 0.72f), 0f, layer);
            CardMotion.ViewOf(caster)?.PulseSupportHud(false, false, new Color(0.94f, 0.76f, 0.44f));
            AudioManager.PlayCue("surya.reload");
        }

        public static bool PlayMotif(VfxPartData part, Unit caster, Unit target, Vector3 point,
            int layer, Vector2 size, float angle)
        {
            if (part.renderer == "trioSunTarget" || part.renderer == "trioSuryaWheel")
            {
                bool wheel = part.renderer == "trioSuryaWheel";
                float card = VfxSpace.CardSize;
                Color rim = new(0.88f, 0.65f, 0.40f, 0.64f);
                float radius = wheel ? 0.37f : 0.24f;
                Vector2 ring = Vector2.one * radius * 2f;
                Mark(VfxSpace.Ring, point, ring, rim, 0f, layer);
                int spokes = wheel ? 7 : 4;
                for (int i = 0; i < spokes; i++)
                {
                    float turn = i * 360f / spokes;
                    Vector3 ray = Quaternion.Euler(0f, 0f, turn) * Vector3.right;
                    Mark(VfxArt.Streak, point + ray * radius * card,
                        new Vector2(wheel ? 0.20f : 0.11f, 0.025f), new Color(0.98f, 0.84f, 0.57f, 0.66f), turn, layer);
                }
                return true;
            }
            if (part.renderer == "trioWeaknessAll")
            {
                string[] all = { "pyro", "hydro", "dendro", "electro", "cryo", "geo", "anemo" };
                foreach (string element in all)
                {
                    var piece = VfxDefinitionCatalog.Part("affected.trio.weakness." + element);
                    if (piece != null) PlayMotif(piece, caster, target, point, layer, size, angle);
                }
                return true;
            }
            if (part.renderer == "trioWeakness")
            {
                // 실제 성공한 원소별 상태가 자기 색 한 조각을 남긴다. 일곱 조각이 같은 정점에서 고리 하나를 이룬다.
                string[] elements = { "pyro", "hydro", "dendro", "electro", "cryo", "geo", "anemo" };
                int element = Array.FindIndex(elements, value => part.id.EndsWith("." + value, StringComparison.OrdinalIgnoreCase));
                element = Mathf.Max(0, element);
                if (target != null)
                {
                    var presentation = WeaknessFrames.GetValue(target, _ => new WeaknessPresentation());
                    if (presentation.Frame != Time.frameCount) { presentation.Frame = Time.frameCount; presentation.Elements = 0; }
                    int bit = 1 << element;
                    if ((presentation.Elements & bit) != 0) return true;
                    presentation.Elements |= bit;
                }
                float turn = 90f - element * 360f / 7f;
                Vector3 center = point + Vector3.down * VfxSpace.CardSize * 0.19f;
                Vector3 ray = Quaternion.Euler(0f, 0f, turn) * Vector3.right;
                Color tint = ColorUtility.TryParseHtmlString("#" + part.bodyColor.TrimStart('#'), out Color parsed) ? parsed : new Color(0.65f, 0.86f, 0.78f);
                Mark(VfxArt.Diamond, center + ray * VfxSpace.CardSize * 0.21f,
                    new Vector2(0.075f, 0.05f), tint, turn, layer);
                return true;
            }
            if (part.renderer == "trioAgniBuff")
            {
                Vector3 center = point + Vector3.down * VfxSpace.CardSize * 0.25f;
                Mark(VfxSpace.Ring, center, new Vector2(0.42f, 0.17f), new Color(0.90f, 0.64f, 0.42f, 0.62f), 0f, layer);
                for (int i = 0; i < 3; i++)
                    Mark(VfxArt.Diamond, center + Vector3.up * VfxSpace.CardSize * (0.04f + i * 0.06f),
                        new Vector2(0.06f, 0.12f), new Color(0.97f, 0.78f, 0.52f, 0.68f), i * 35f, layer);
                CardMotion.ViewOf(target)?.PulseSupportHud(false, false, new Color(0.95f, 0.72f, 0.46f));
                return true;
            }
            if (part.renderer != "trioVayuCast" && part.renderer != "trioAgniCast" && part.renderer != "trioSuryaCast") return false;
            bool wind = part.renderer == "trioVayuCast";
            bool gun = part.renderer == "trioSuryaCast";
            Color body = wind ? new Color(0.53f, 0.82f, 0.73f) : gun ? new Color(0.94f, 0.73f, 0.54f) : new Color(0.91f, 0.62f, 0.52f);
            Color core = wind ? new Color(0.86f, 0.98f, 0.92f) : new Color(1f, 0.91f, 0.73f);
            var sheet = new HandpaintSheet(part.sheet, part.columns, part.rows, part.fps, new Vector2(0.5f, 0.5f));
            HandpaintFx.PlayAt(point, layer, sheet, size, body, core, angle, heldFrame: part.peakFrame,
                peakAlpha: 0.8f, glowAlpha: 0.16f, desaturate: 1f, steadyBody: true, calmLight: true);
            if (!gun) Mark(VfxSpace.Ring, point, new Vector2(0.48f, 0.21f),
                wind ? new Color(0.50f, 0.75f, 0.69f, 0.54f) : new Color(0.89f, 0.65f, 0.46f, 0.58f), angle, layer);
            return true;
        }

        private static void Mark(Sprite art, Vector3 point, Vector2 size, Color color, float angle, int layer)
        {
            size *= VfxSpace.CardSize;
            // 빠른 전투에서도 국소 문양은 약 0.36초 동안 한 번 부드럽게 보인다.
            float life = 0.36f * Mathf.Max(1f, Time.timeScale);
            SpriteFx.Play(art, point, color, false, size, size, life, angle,
                fadeIn: 0.24f, fadeOutFrom: 0.50f, sortingLayer: layer, order: VfxSpace.OverlaySortingOrder + 38);
        }
    }
}
