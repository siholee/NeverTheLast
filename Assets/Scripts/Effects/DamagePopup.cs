using System.Collections.Generic;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;

namespace Effects
{
    public enum CombatNumberKind { Damage, Shielded, Heal, Miss, Nullified, EffectResisted, HealthCost }

    /// <summary>화면 크기를 기준으로 찍히는 전투 숫자. 배속과 무관하게 머물며 짧게 튀고 떠오른다.</summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopup : MonoBehaviour
    {
        private const int MaxConcurrent = 24;
        private const float Lifetime = 1.12f;
        private static readonly List<DamagePopup> Live = new();
        private static Material _numberMaterial;
        private Unit _target;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _lastAnchor, _offset;
        private float _age, _scale;
        private int _slot;

        public static void Show(Unit target, int amount, bool isCrit, Color accent,
            CombatNumberKind kind = CombatNumberKind.Damage, float weight = 0f, bool counter = false)
        {
            if (target == null) return;
            if (amount <= 0 && kind is CombatNumberKind.Damage or CombatNumberKind.Heal or CombatNumberKind.Shielded) return;
            bool critical = isCrit && kind == CombatNumberKind.Damage;
            if (critical) Managers.AudioManager.PlayCue("common.crit");
            Live.RemoveAll(item => item == null);
            while (Live.Count >= MaxConcurrent)
            { var first = Live[0]; Live.RemoveAt(0); if(first != null) Destroy(first.gameObject); }
            var used = new HashSet<int>();
            foreach (DamagePopup other in Live) if (other._target == target) used.Add(other._slot);
            if (used.Count >= 4)
            {
                DamagePopup oldest = Live.Find(item => item._target == target);
                if (oldest != null)
                { used.Remove(oldest._slot); Live.Remove(oldest); oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
            }
            int stack = 0; while (used.Contains(stack)) stack++;
            var rect = UIBuild.Container("DamagePopup", CombatFeedbackSurface.Root);
            var popup = rect.gameObject.AddComponent<DamagePopup>();
            popup.Build(target, amount, critical, accent, kind, Mathf.Clamp01(weight), stack % 4,counter);
            Live.Add(popup);
        }
        private void Build(Unit target, int amount, bool critical, Color accent, CombatNumberKind kind, float weight, int stack,bool counter)
        {
            _target = target; _slot = stack;
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(.5f,.5f);
            _rect.sizeDelta = new Vector2(186,82);
            _group = gameObject.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false;
            _lastAnchor = CombatFeedbackSurface.Position(target, true);
            // 같은 표적의 머리 위 두 열을 사용한다. 세 번째 타격을 다른 카드 쪽으로 밀지 않는다.
            float lift = Mathf.Min(68f, Mathf.Max(0f, CombatFeedbackSurface.Root.rect.yMax - 198f - _lastAnchor.y));
            _offset = new Vector2((stack % 2 == 0 ? -1 : 1) * 52f, 34f + (stack / 2) * lift);
            if (lift < 50f) _offset.y = -36f - (stack / 2) * 50f;
            // 아랫행 적의 숫자가 윗행 HP판에 붙지 않도록 표적 자신의 상단 안쪽으로 두 행을 내린다.
            if(target.IsEnemy&&CombatFeedbackSurface.DamageHasHudAbove(target))
                _offset.y=-36f-(stack/2)*50f;
            // 아군 쪽은 기존 검수된 오른쪽 머리 여백을 유지한다. 위로 밀면 앞줄 HUD에 겹친다.
            if(!target.IsEnemy)
            {
                float allyLift=Mathf.Min(68f,Mathf.Max(0f,CombatFeedbackSurface.Root.rect.yMax-162f-_lastAnchor.y));
                _offset=new Vector2(42+(stack/2)*102,(stack%2)*allyLift);
                if (allyLift < 50f) _offset.y = -(stack % 2) * 68f;
            }
            _scale = Mathf.Lerp(.95f, 1.10f, weight);
            bool numeric = kind is CombatNumberKind.Damage or CombatNumberKind.Heal or CombatNumberKind.Shielded or CombatNumberKind.HealthCost;
            string text = kind switch
            {
                CombatNumberKind.Heal => "+" + amount.ToString("N0"),
                CombatNumberKind.HealthCost => "−" + amount.ToString("N0"),
                CombatNumberKind.Miss => "회피", CombatNumberKind.Nullified => "무효",
                CombatNumberKind.EffectResisted => "효과 저항", _ => amount.ToString("N0")
            };
            Color healInk=BattlefieldBackdrop.CurrentBackgroundLuminance>=.50f
                ? new Color(.20f,.58f,.46f) : new Color(.56f,1,.84f);
            Color ink = critical ? new Color(1,.86f,.44f) : kind == CombatNumberKind.Heal ? healInk :
                kind == CombatNumberKind.HealthCost ? new Color(1,.70f,.64f) :
                kind == CombatNumberKind.Shielded ? new Color(.70f,.88f,1) : new Color(.98f,.99f,1);
            TextMeshProUGUI label = UIBuild.Text("Number", transform, text, numeric ? critical ? 54 : counter ? 52 : 42 : 28,
                ink, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(label.rectTransform, new Vector2(0,.22f), new Vector2(1,.88f));
            if (_numberMaterial == null)
            {
                _numberMaterial = new Material(label.fontSharedMaterial) { name="Combat Number SDF", hideFlags=HideFlags.HideAndDontSave };
                _numberMaterial.EnableKeyword("OUTLINE_ON"); _numberMaterial.EnableKeyword("UNDERLAY_ON");
                _numberMaterial.SetFloat("_OutlineWidth", .16f);
                _numberMaterial.SetColor("_OutlineColor", new Color(.09f,.16f,.25f,.95f));
                _numberMaterial.SetColor("_UnderlayColor", new Color(.03f,.08f,.14f,.65f));
                _numberMaterial.SetFloat("_UnderlayOffsetX", .7f); _numberMaterial.SetFloat("_UnderlayOffsetY", -.8f);
                _numberMaterial.SetFloat("_UnderlaySoftness", .12f);
            }
            label.fontSharedMaterial = _numberMaterial; label.UpdateMeshPadding();
            if (numeric)
            {
                string caption = counter ? critical ? "치명타 · 반격" : "반격" : critical ? "치명타" : kind == CombatNumberKind.Heal ? "회복" : kind == CombatNumberKind.Shielded ? "보호막" : kind == CombatNumberKind.HealthCost ? "체력 소모" : "";
                if (caption.Length > 0)
                {
                    Color captionInk = counter && BattlefieldBackdrop.CurrentBackgroundLuminance >= .50f
                        ? new Color(.13f,.23f,.36f) : ink;
                    TextMeshProUGUI tag = UIBuild.Text("Kind", transform, caption, counter ? 22 : 18, captionInk, TextAlignmentOptions.Center);
                    if (counter) tag.fontStyle = FontStyles.Bold;
                    else tag.fontSharedMaterial = _numberMaterial;
                    UIBuild.Anchor(tag.rectTransform, new Vector2(0,0), new Vector2(1,.27f));
                }
                var stroke = UIBuild.Solid("ImpactStroke", transform, critical ? new Color(1,.76f,.30f,.85f) : Color.Lerp(accent,Color.white,.30f));
                stroke.raycastTarget=false;
                UIBuild.Pin(stroke.rectTransform,new Vector2(.5f,.30f),new Vector2(critical ? 74 : 48,2),Vector2.zero);
                stroke.rectTransform.pivot=new Vector2(.5f,.5f);
                stroke.rectTransform.localRotation=Quaternion.Euler(0,0,-8);
            }
            Frame();
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0) return;
            _age += Vfx.VfxTiming.ReadableDeltaTime;
            if (_age >= Lifetime) { Destroy(gameObject); return; }
            if (_target != null && _target.isActive) _lastAnchor = CombatFeedbackSurface.Position(_target, true);
            Frame();
        }
        private void Frame()
        {
            _rect.anchoredPosition = CombatFeedbackSurface.Clamp(_lastAnchor + _offset + Vector2.up * (32 * _age / Lifetime),new Vector2(100,48));
            float pop = 1 + .18f * Mathf.Pow(1 - Mathf.Clamp01(_age / .13f),2);
            _rect.localScale = Vector3.one * (_scale * pop);
            _group.alpha = _age < .72f ? 1 : Mathf.Clamp01((Lifetime-_age)/.40f);
        }
        /// <summary>상태 알림은 숫자의 남은 상승 경로까지 비워 두어 다음 프레임에도 겹치지 않는다.</summary>
        internal static bool HasRoom(Vector2 position, Vector2 halfSize)
        {
            Rect box = new(position - halfSize, halfSize * 2);
            foreach (var item in Live)
            {
                if (item == null || !item.gameObject.activeInHierarchy || item._age >= Lifetime) continue;
                float rise = 32f * Mathf.Clamp01((Lifetime-item._age)/Lifetime);
                Vector2 half = new Vector2(100,48) * item._rect.localScale.x;
                Vector2 center = item._rect.anchoredPosition;
                Rect occupied = Rect.MinMaxRect(center.x-half.x,center.y-half.y,
                    center.x+half.x,center.y+half.y+rise);
                if (box.Overlaps(occupied)) return false;
            }
            return true;
        }
        private void OnDestroy() => Live.Remove(this);
    }
}
