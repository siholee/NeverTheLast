#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using Effects.Vfx;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    /// <summary>13차 로카팔라 연출 검수. 한 캐릭터를 한 연속 1배속 캡처로 남긴다.</summary>
    public sealed partial class VerifyHarnessRunner
    {
        private Material _v13MaskMaterial;
        private bool _v13Diagnostics;
        private bool _v13PeakDiagnostics;
        private float _v13DiagnosticUntil;

        private void V13Damage(Unit target, DamageContext context)
        {
            if (_currentScenario?.StartsWith("v13", StringComparison.Ordinal) != true) return;
            VerifyEvents.Raise($"v13_damage_{context.Attacker?.UnitName}_{target.UnitName}_{context.CodeType}");
        }

        private void V13Heal(Unit target, Unit source, int effective, int overflow)
        {
            if (_currentScenario?.StartsWith("v13", StringComparison.Ordinal) != true) return;
            VerifyEvents.Raise($"v13_heal_{source?.UnitName}_{target.UnitName}_{effective}_{overflow}");
        }

        private void V13Death(Unit target, Unit attacker)
        {
            if (_currentScenario?.StartsWith("v13", StringComparison.Ordinal) != true) return;
            VerifyEvents.Raise($"v13_death_{target.UnitName}");
        }

        private IEnumerator ScenarioV13A(VerifyScenarioRequest config)
        {
            _v13Diagnostics = config.captureVfxDiagnostics;
            _v13PeakDiagnostics = config.captureVfxPeakDiagnostics;
            _v13DiagnosticUntil = -1f;
            yield return PrepareStage(config, 6, true, false, false);
            FreezeBattleForManualCapture();
            GameManager game = GameManager.Instance;
            bool wasEnabled = game.enabled;
            // 자동 Tick만 멈추고 실제 전투 HUD·특수행동 슬롯은 보여 준다. 게임 실행 경로는 수정하지 않는다.
            game.enabled = false;
            game.gameState = BaseEnums.GameState.RoundInProgress;
            game.uiManager?.HidePreparationPhasePanel();
            game.uiManager?.UpdateGameStatusWithEnemyCount(BaseEnums.GameState.RoundInProgress, 0, AliveEnemies().Count);
            // 실제 피해·회복은 살리고 적은 검수용 보호막으로 생존시킨다. 게임의 피해 공식은 그대로다.
            foreach (Unit enemy in AliveEnemies()) enemy.AddShield(enemy.HpMax * 200);
            foreach (Unit ally in AliveAllies())
            {
                ally.ClearUltimateResource();
                ally.ModifyHp(Mathf.Max(1, ally.HpMax / 3));
            }
            // 최초 시트 분할·GPU/보이스 로딩이 캡처 프레임 간격을 벌리지 않게 표시 자산만 예열한다.
            // 피해·상태·자원이나 오디오 재생을 호출하지 않으며 예열 객체는 촬영 전에 끝난다.
            foreach (var sheet in new[] {
                Effects.Vfx.Presets.HandpaintFx.Lightning2Purple, Effects.Vfx.Presets.HandpaintFx.Impact1Yellow,
                Effects.Vfx.Presets.HandpaintFx.Impact1Green, Effects.Vfx.Presets.HandpaintFx.Impact1White,
                Effects.Vfx.Presets.HandpaintFx.Explosion2Blue, Effects.Vfx.Presets.HandpaintFx.Explosion3Yellow,
                Effects.Vfx.Presets.HandpaintFx.Smoke1Blue,
                new Effects.Vfx.Presets.HandpaintSheet("Slash4_Green", 3, 2, 30f, new Vector2(.5f, .5f)),
                new Effects.Vfx.Presets.HandpaintSheet("Slash5_Green", 3, 3, 36f, new Vector2(.5f, .5f)),
                new Effects.Vfx.Presets.HandpaintSheet("Fire3_Yellow", 3, 3, 30f, new Vector2(.5f, .5f)) })
                Effects.Vfx.Presets.HandpaintFx.PlayAt(VfxSpace.WorldPoint(AliveAllies().First()),
                    VfxSpace.SortingLayer(AliveAllies().First()), sheet, new Vector2(.01f, .01f),
                    Color.clear, Color.clear, peakAlpha: 0f, glowAlpha: 0f);
            Unit warmCaster = AliveAllies().First();
            Unit warmTarget = AliveEnemies().First();
            foreach (string key in new[] { "kubera.coin_toss", "kubera.gold_rush", "kubera.treasury",
                "indra.vajra", "indra.gathering" })
                VfxComposer.PlaySlot(key, "cast", 0, warmCaster, warmTarget,
                    VfxSpace.WorldPoint(warmCaster), VfxSpace.SortingLayer(warmCaster));
            foreach (string clip in new[] {
                "Hsr/chandra/nishakara_hit_01",
                "Hsr/chandra/nishakara_hit_march7_01",
                "Hsr/chandra/soma_cast_march7_01",
                "Hsr/chandra/soma_hit_march7_01",
                "Hsr/chandra/soma_screen_01",
                "Hsr/chandra/soma_screen_march7_01",
                "Hsr/sabah/azrael_hit_castorice_01",
                "Hsr/sabah/lightning_slash_hit_castorice_01",
                "Hsr/sei/bigbang_cast_01",
                "Hsr/sei/bigbang_hit_01",
                "Hsr/sei/bigbang_resolve_01",
                "Hsr/sei/genesis_cast_01",
                "Hsr/sei/genesis_hit_01",
                "Hsr/sei/genesis_screen_01",
                "Hsr/sei/ground_cast_01",
                "Hsr/sei/ground_hit_01",
                "Hsr/sei/ground_projectile_01",
                "Hsr/shi/ghost_slash_projectile_01" })
                Resources.Load<AudioClip>("Audio/Sfx/" + clip)?.LoadAudioData();
            yield return WaitUnscaled(1.3f);
            Text label = CreateStatusPreviewLabel(out GameObject labelRoot);
            label.font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 32);
            bool done = false;
            Coroutine sequence = StartCoroutine(V13Sequence(config.name, label, () => done = true));
            float deadline = Time.realtimeSinceStartup + 55f;
            float nextCapture = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup < deadline)
            {
                while (Time.realtimeSinceStartup < nextCapture) yield return null;
                yield return EndOfFrame;
                Capture("v13a");
                nextCapture = Mathf.Max(nextCapture + 1f / 30f, Time.realtimeSinceStartup);
            }
            if (!done) { StopCoroutine(sequence); FailCurrent("13차 연속 캡처가 제한 시간을 넘겼다."); }
            Destroy(labelRoot);
            GameManager.Instance.ActionScheduler.EndRound();
            Combat.SpecialAction.Reset();
            if (config.name == "v13a_gate")
                RecordMechanic("특수행동 실제 HUD 표시", "특수", string.Join(" / ", _result.uiRects
                        .Where(r => r.scenario == _currentScenario && r.element == "Kind").Select(r => r.text).Distinct()),
                    _result.uiRects.Any(r => r.scenario == _currentScenario && r.element == "Kind" && r.text.Contains("특수")));
            game.gameState = BaseEnums.GameState.Preparation;
            game.enabled = wasEnabled;
        }

        private IEnumerator V13Sequence(string scenario, Text label, Action onComplete)
        {
            if (scenario.StartsWith("v13b", StringComparison.Ordinal))
            {
                yield return V13BSequence(scenario, label);
                yield return WaitUnscaled(.7f);
                onComplete();
                yield break;
            }
            Unit Find(int id) => AliveAllies().FirstOrDefault(unit => unit.ID == id);
            Unit indra = Find(IndraId), vayu = Find(VayuId), kubera = Find(KuberaId), chandra = Find(ChandraId);
            if (scenario == "v13a_gate")
            {
                yield return V13Segment(label, "로카팔라 — 팔방의 소집 → 바유 → 쿠베라 → 인드라 → 찬드라",
                    "lokapala_gate", () => V13Gate(indra));
            }
            else if (scenario == "v13a_indra")
            {
                yield return V13Segment(label, "일반 — 뇌정", "indra.thunderbolt", () => V13Cast(indra, 0));
                yield return V13Segment(label, "특수행동 — 바즈라", "indra.vajra", () => V13Cast(indra, 1));
                yield return V13Segment(label, "초월기 — 팔방의 소집", "indra.gathering", () => V13Cast(indra, 2));
                // 소집을 여는 코드는 그대로 실행하지만 이 영상에서는 예약된 후속을 다음 구간에서 한 번만 검수한다.
                GameManager.Instance.ActionScheduler.EndRound();
                Combat.SpecialAction.Reset();
                yield return V13Segment(label, "특성 — 데바의 왕 · 소집 순환", "indra.deva_king",
                    () => V13SingleScheduledSpecial(vayu));
            }
            else if (scenario == "v13a_vayu")
            {
                yield return V13Segment(label, "일반 — 돌풍 · 바람의 침식", "vayu.gust", () => V13Cast(vayu, 0));
                yield return V13Segment(label, "특수행동 — 태풍의 눈", "vayu.typhoon_eye", () => V13Cast(vayu, 1));
                yield return V13Segment(label, "초월기 — 폭풍", "vayu.storm", () => V13Cast(vayu, 2));
            }
            else if (scenario == "v13a_kubera")
            {
                kubera?.ClearUltimateResource();
                yield return V13Segment(label, "보물 — 0 / 8 · 상시 표시", "kubera.treasure_zero", () => WaitUnscaled(0.4f));
                yield return V13Segment(label, "일반 — 금화 투척", "kubera.coin_toss", () => V13Cast(kubera, 0));
                GameManager.Instance.inventoryManager.RestoreGold(0);
                yield return V13Segment(label, "특수행동 — 골드 러쉬 · 금력 ×1", "kubera.gold_rush.low", () => V13Cast(kubera, 1));
                GameManager.Instance.inventoryManager.RestoreGold(6 * 100 * 40);
                yield return V13Segment(label, "특수행동 — 골드 러쉬 · 금력 ×4", "kubera.gold_rush.high", () => V13Cast(kubera, 1));
                yield return V13Segment(label, "특성 — 재보천의 후원 · 보물 +1", "kubera.patronage", () => V13UchiSummon(Find(UchiId)));
                yield return V13Segment(label, "초월기 — 보고 개방", "kubera.treasury", () => V13Cast(kubera, 2));
            }
            else if (scenario == "v13a_chandra")
            {
                yield return V13Segment(label, "특수행동 — 창야 · 공격 후 아군 전원 회복", "chandra.changya", () => V13Cast(chandra, 1));
                yield return V13Segment(label, "특성 — 달빛 순환 · 특수행동 종료 회복", "chandra.moon_cycle", () => V13SingleScheduledSpecial(vayu));
                yield return V13Segment(label, "특성 — 니샤카라 · 위기 회복", "chandra.nishakara", () => V13MoonGuard(chandra));
            }
            yield return WaitUnscaled(0.7f);
            onComplete();
        }

        private IEnumerator V13Segment(Text label, string title, string key, Func<IEnumerator> action)
        {
            label.text = title;
            VerifyEvents.Raise("v13_begin_" + key);
            yield return WaitUnscaled(0.5f);
            VerifyEvents.Raise("v13_action_" + key);
            yield return action();
            VerifyEvents.Raise("v13_end_" + key);
            yield return WaitUnscaled(0.9f);
            label.text = string.Empty;
        }

        private IEnumerator V13Cast(Unit unit, int slot)
        {
            if (unit == null) { FailCurrent("연출 시전자가 편성에 없다."); yield break; }
            if (slot == 0) unit.CastNormalCode();
            else if (slot == 1) unit.CastSpecialCode();
            else { unit.FillUltimateResource(); unit.CastUltimateCode(); }
            yield return WaitForCasting(unit, 8f);
            // 지원 피격의 HUD 도착까지 같은 연속 구간에 남긴다. 판정 대기는 늘리지 않는다.
            yield return WaitUnscaled(0.5f);
        }

        private IEnumerator V13UchiSummon(Unit uchi)
        {
            if (uchi == null) { FailCurrent("후원 검수에 우치가 필요하다."); yield break; }
            uchi.CastNormalCode();
            yield return WaitUnscaled(0.6f);
        }

        private IEnumerator V13MoonGuard(Unit chandra)
        {
            var passive = chandra?.ActivePassiveCodes.OfType<Codes.Passive.ChandraNishakara>().FirstOrDefault();
            if (passive == null) yield break;
            passive.GainMoonlight(playVfx: false);
            chandra.ModifyHp(chandra.HpMax / 2);
            chandra.TakeDamage(new DamageContext(AliveEnemies().First(), chandra.HpMax / 4,
                BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage, DamageTag.NormalAttack }));
            yield return WaitUnscaled(1.2f);
        }

        private IEnumerator V13SingleScheduledSpecial(Unit unit)
        {
            if (unit == null) yield break;
            ActionScheduler scheduler = GameManager.Instance.ActionScheduler;
            scheduler.BeginRound();
            scheduler.EnqueueSpecial(unit, "v13a", unit.ActiveSpecialCode.CodeName, unit.CastSpecialCode);
            scheduler.Tick(0f);
            yield return WaitForCasting(unit, 8f);
            // 실제 AnyActionFinished를 쓰고 자동 일반행동은 캡처 준비 상태에서 정리한다.
            void Finished(Unit finished, ActionScheduler.ActionKind kind)
            {
                if (finished == unit && kind == ActionScheduler.ActionKind.Special) scheduler.EndRound();
            }
            ActionScheduler.AnyActionFinished += Finished;
            scheduler.Tick(0f);
            ActionScheduler.AnyActionFinished -= Finished;
            scheduler.EndRound();
            foreach (Unit ally in AliveAllies()) { ally.StopAllCoroutines(); ally.isCasting = false; }
            yield return WaitUnscaled(1.0f);
        }

        private IEnumerator V13Gate(Unit indra)
        {
            ActionScheduler scheduler = GameManager.Instance.ActionScheduler;
            scheduler.BeginRound();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            indra.FillUltimateResource();
            var order = new List<int>();
            int ended = 0;
            void Started(Unit unit, ActionScheduler.ActionKind kind, string name)
            {
                if (kind == ActionScheduler.ActionKind.Special && !unit.IsEnemy) order.Add(unit.ID);
            }
            void Finished(Unit unit, ActionScheduler.ActionKind kind)
            {
                if (kind == ActionScheduler.ActionKind.Special && !unit.IsEnemy)
                {
                    ended++;
                    if (ended == 4) scheduler.EndRound();
                }
            }
            ActionScheduler.AnyActionStarted += Started;
            ActionScheduler.AnyActionFinished += Finished;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (ended < 4 && Time.realtimeSinceStartup < deadline)
            {
                scheduler.Tick(Time.deltaTime);
                yield return null;
            }
            ActionScheduler.AnyActionStarted -= Started;
            ActionScheduler.AnyActionFinished -= Finished;
            RecordMechanic("로카팔라 실제 예약 순서", "64→65→63→60", string.Join("→", order),
                order.SequenceEqual(new[] { VayuId, KuberaId, IndraId, ChandraId }));
            scheduler.EndRound();
            foreach (Unit ally in AliveAllies()) { ally.StopAllCoroutines(); ally.isCasting = false; }
            yield return WaitUnscaled(0.8f);
        }

        /// <summary>같은 렌더 프레임의 VFX 없는 배경과 비가산 몸체 알파를 분리해 P18을 실제 픽셀로 잰다.</summary>
        private void V13CaptureDiagnostics(string imagePath)
        {
            if (!_currentScenario.StartsWith("v13", StringComparison.Ordinal) ||
                (!_v13Diagnostics && (!_v13PeakDiagnostics || Time.realtimeSinceStartup > _v13DiagnosticUntil))) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            var sprites = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && r.sortingLayerName == "CombatVfx" && r.sprite != null &&
                    !r.gameObject.name.StartsWith("CombatFocus", StringComparison.Ordinal)).ToList();
            var states = sprites.Select(r => (Renderer: r, Material: r.sharedMaterial, Layer: r.gameObject.layer)).ToList();
            RenderTexture priorTarget = camera.targetTexture;
            RenderTexture priorActive = RenderTexture.active;
            int priorMask = camera.cullingMask;
            CameraClearFlags priorClear = camera.clearFlags;
            Color priorColor = camera.backgroundColor;
            RenderTexture rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = rt;
                foreach (var state in states) state.Renderer.enabled = false;
                camera.Render();
                SaveTarget(rt, imagePath + ".background.rgba");
                if (_v13MaskMaterial == null)
                    #if UNITY_EDITOR
                    _v13MaskMaterial = new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/Verify/VerifyVfxAlpha.shader"));
#else
                    return;
#endif
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                foreach (var state in states)
                {
                    bool body = state.Material != null && !state.Material.shader.name.Contains("Glow") &&
                        !state.Material.shader.name.Contains("Additive");
                    state.Renderer.enabled = body;
                    state.Renderer.gameObject.layer = 31;
                    state.Renderer.sharedMaterial = _v13MaskMaterial;
                }
                camera.Render();
                SaveTarget(rt, imagePath + ".alpha.rgba");
                var boxes = states.Where(state => state.Renderer.enabled).Select(state =>
                {
                    Bounds b = state.Renderer.bounds;
                    Vector3 lo = camera.WorldToScreenPoint(b.min), hi = camera.WorldToScreenPoint(b.max);
                    return $"{{\"name\":\"{state.Renderer.transform.parent?.name}\",\"sheet\":\"{state.Renderer.sprite.texture.name}\",\"xMin\":{lo.x},\"yMin\":{lo.y},\"xMax\":{hi.x},\"yMax\":{hi.y}}}";
                });
                File.WriteAllText(imagePath + ".bounds.json", "[" + string.Join(",", boxes) + "]");
            }
            finally
            {
                foreach (var state in states)
                {
                    if (state.Renderer == null) continue;
                    state.Renderer.sharedMaterial = state.Material;
                    state.Renderer.gameObject.layer = state.Layer;
                    state.Renderer.enabled = true;
                }
                camera.targetTexture = priorTarget;
                camera.cullingMask = priorMask;
                camera.clearFlags = priorClear;
                camera.backgroundColor = priorColor;
                RenderTexture.active = priorActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // PNG 압축은 외부 산출 단계에서 한다. 실제 1배속 연출이 캡처 I/O 때문에 건너뛰지 않게 한다.
        private static void V13CaptureFrame(string path)
        {
            RenderTexture full = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            // 1920 전용 HUD/희소 검수만 원래 해상도를 보존한다. 기존 연속 캡처 크기는 유지한다.
            bool native = path.IndexOf("_1920_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (path.Contains("v13surya_hud_") || path.Contains("v13trio_visual"));
            RenderTexture rt = RenderTexture.GetTemporary(native ? Screen.width : 960, native ? Screen.height : 540, 0, RenderTextureFormat.ARGB32);
            RenderTexture prior = RenderTexture.active;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                Graphics.Blit(full, rt);
                SaveTarget(rt, path);
                if (native)
                    File.WriteAllText(path + ".dimensions.json",
                        $"{{\"width\":{rt.width},\"height\":{rt.height},\"format\":\"RGBA32\",\"bytes\":{(long)rt.width * rt.height * 4},\"native\":true}}", System.Text.Encoding.UTF8);
            }
            finally
            {
                RenderTexture.active = prior;
                RenderTexture.ReleaseTemporary(rt);
                RenderTexture.ReleaseTemporary(full);
            }
        }

        private static void SaveTarget(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.GetRawTextureData<byte>().ToArray());
            Destroy(image);
        }
    }
}
#endif
