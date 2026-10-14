using System;
using System.Collections;
using System.Linq;
using Combat;
using Effects.Vfx;
using Helpers;
using UnityEngine;

namespace Entities.View
{
    /// <summary>칸 없는 소환수의 표시만 소유한다. 소환자 카드의 실제 모션을 따라가고 전장 배치에는 참가하지 않는다.</summary>
    public sealed class SummonCardView : MonoBehaviour
    {
        public Unit BoundUnit => _unit;
        public SpriteRenderer PortraitRenderer => _portrait;
        private Unit _unit;
        private SpriteRenderer _portrait;
        private SpriteRenderer _healthTrack, _healthFill;
        public bool HasHealthDisplay => _healthTrack != null && _healthFill != null;
        public float DisplayedHealthRatio { get; private set; }
        private Vector3 _advance;
        private float _opacity = 0.68f;
        private bool IsCompanion => _unit != null && Summons.StyleOf(_unit.SummonOwner) is SummonStyle.Companion or SummonStyle.Cohort;
        private float BaseOpacity => IsCompanion ? .94f : .68f;

        public void Bind(Unit unit)
        {
            _unit = unit;
            _opacity = BaseOpacity;
            _portrait = new GameObject("ClonePortrait").AddComponent<SpriteRenderer>();
            _portrait.transform.SetParent(transform, false);
            _portrait.sprite = unit.SummonPortraitOverride != null ? unit.SummonPortraitOverride
                : UnitCardView.PortraitWithBottomFade(SpriteResource.LoadPortrait(unit.PortraitPath, out _));
            _portrait.color = new Color(0.74f, 0.95f, 0.91f, _opacity);
            if (!unit.IsUntargetable)
            {
                _healthTrack = new GameObject("SummonHealthTrack").AddComponent<SpriteRenderer>();
                _healthFill = new GameObject("SummonHealthFill").AddComponent<SpriteRenderer>();
                _healthTrack.transform.SetParent(transform, false);
                _healthFill.transform.SetParent(transform, false);
                _healthTrack.sprite = _healthFill.sprite = Managers.UI.Theme.UIShapes.Solid(Color.white);
                _healthTrack.color = new Color(.13f,.21f,.31f,.92f);
                _healthFill.color = new Color(.25f,.79f,.73f,.98f);
            }
            FollowOwner();
        }

        private void LateUpdate() => FollowOwner();

        private void FollowOwner()
        {
            if (_unit == null || _unit.SummonOwner == null || _portrait == null) return;
            Unit owner = _unit.SummonOwner;
            var view = CardMotion.ViewOf(owner);
            if (view == null || view.Body == null)
            {
                _portrait.enabled = false;
                if (HasHealthDisplay) _healthTrack.enabled = _healthFill.enabled = false;
                return;
            }
            _portrait.enabled = _unit.isActive && owner.isActive && owner.IsOnField;
            int index = Mathf.Clamp(Summons.Of(owner).IndexOf(_unit), 0, 2);
            float facing = owner.IsEnemy ? -1f : 1f;
            float card = view.CardSize;
            // 오래된 분신이 가장 가까운 앞쪽에 있고 새 분신은 위·뒤로 한 장씩 펼쳐진다.
            transform.position = view.Body.position + new Vector3(-facing * (.42f + index * .10f), .28f + index * .10f) * card + _advance;
            _portrait.sortingLayerID = view.PortraitRenderer.sortingLayerID;
            _portrait.sortingOrder = view.PortraitRenderer.sortingOrder - 3 - index;
            float width = _portrait.sprite != null ? _portrait.sprite.bounds.size.x : 1f;
            _portrait.transform.localScale = Vector3.one * (card * .64f / Mathf.Max(.01f, width));
            _portrait.color = IsCompanion ? new Color(1f, 1f, 1f, _opacity)
                : new Color(.74f, .95f, .91f, _opacity - index * .07f);
            if (Summons.StyleOf(owner) == SummonStyle.Cohort)
            {
                // 수호 문양 세 개가 서로와 주인 HUD를 덮지 않게 한 열로 펼친다.
                transform.position = view.Body.position + new Vector3(-facing * .64f, .68f - index * .34f) * card + _advance;
                _portrait.sortingOrder = view.PortraitRenderer.sortingOrder + 2;
                _portrait.transform.localScale = Vector3.one * (card * .28f / Mathf.Max(.01f,width));
                _portrait.color = new Color(.94f,.83f,.62f,.86f);
            }
            if (HasHealthDisplay)
            {
                // 피격 가능한 동반자의 실제 체력을 원화 위에 짧게 표시한다. 주인의 HUD를 복제하지 않는다.
                DisplayedHealthRatio = Mathf.Clamp01(_unit.HpCurr/(float)Mathf.Max(1,_unit.HpMax));
                float barWidth=card*.52f,barHeight=card*.018f;
                float top=_portrait.sprite.bounds.max.y*_portrait.transform.localScale.y+card*.035f;
                _healthTrack.transform.localPosition=new Vector3(0,top,0);
                _healthFill.transform.localPosition=new Vector3(-barWidth*(1-DisplayedHealthRatio)*.5f,top,0);
                float spriteWidth=_healthTrack.sprite.bounds.size.x,spriteHeight=_healthTrack.sprite.bounds.size.y;
                _healthTrack.transform.localScale=new Vector3(barWidth/spriteWidth,barHeight/spriteHeight,1);
                _healthFill.transform.localScale=new Vector3(barWidth*DisplayedHealthRatio/spriteWidth,barHeight/spriteHeight,1);
                _healthTrack.sortingLayerID=_healthFill.sortingLayerID=_portrait.sortingLayerID;
                _healthTrack.sortingOrder=_portrait.sortingOrder+1;
                _healthFill.sortingOrder=_portrait.sortingOrder+2;
                _healthTrack.enabled=_portrait.enabled;
                _healthFill.enabled=_portrait.enabled&&DisplayedHealthRatio>0;
            }
        }

        public IEnumerator Advance(Unit target, float scale = 1f)
        {
            Vector3 direction = VfxSpace.AimFrom(_unit, target).Direction;
            float card = VfxSpace.CardSize;
            _opacity = BaseOpacity * scale;
            Trace("card_motion_clone_start");
            for (float t = 0f; t < .24f; t += Time.deltaTime)
            {
                _advance = direction * (card * .18f * Mathf.SmoothStep(0f, 1f, t / .24f));
                yield return null;
            }
            _advance = direction * card * .18f;
            Trace("card_motion_clone_peak");
        }

        public void Guard(Unit attacker)
        {
            Unit owner = _unit.SummonOwner;
            _advance = VfxSpace.AimFrom(owner, attacker).Direction * VfxSpace.CardSize * .46f;
            FollowOwner();
            Trace("clone_guard");
        }

        public void Overflow(Unit target)
        {
            _opacity = .38f;
            _advance = VfxSpace.AimFrom(_unit, target).Direction * VfxSpace.CardSize * .12f;
            FollowOwner();
            Trace("card_motion_clone_overflow_peak");
        }

        public void Depart()
        {
            if (_portrait == null) return;
            if (!IsCompanion)
                UchiFx.PaperDeparture(transform.position, _portrait.sprite, _portrait.transform.lossyScale,
                    _portrait.color, _unit);
            _portrait.enabled = false;
            if (HasHealthDisplay) _healthTrack.enabled = _healthFill.enabled = false;
        }

        private void Trace(string tag)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise(FormattableString.Invariant($"{tag}|{_unit.GetInstanceID()}|{transform.position.x:F5}|{transform.position.y:F5}"));
#endif
        }
    }
}
