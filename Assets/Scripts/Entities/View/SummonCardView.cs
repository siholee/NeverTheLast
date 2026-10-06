using System;
using System.Collections;
using System.Linq;
using Combat;
using Effects.Vfx;
using Helpers;
using Managers.UI.Theme;
using UnityEngine;

namespace Entities.View
{
    /// <summary>칸 없는 분신의 표시만 소유한다. 소환자 카드의 실제 모션을 따라가고 전장 배치에는 참가하지 않는다.</summary>
    public sealed class SummonCardView : MonoBehaviour
    {
        public Unit BoundUnit => _unit;
        public SpriteRenderer PortraitRenderer => _portrait;
        private Unit _unit;
        private SpriteRenderer _portrait;
        private SpriteRenderer _frame;
        private Vector3 _advance;
        private float _opacity = 0.68f;

        public void Bind(Unit unit)
        {
            _unit = unit;
            _frame = new GameObject("CloneCardFrame").AddComponent<SpriteRenderer>();
            _frame.transform.SetParent(transform, false);
            _frame.sprite = UIShapes.CutCorner(5, new Color(.14f, .24f, .28f, .30f),
                UIShapes.Corner.Diagonal, new Color(.43f, .76f, .73f, .84f), 2);
            _frame.drawMode = SpriteDrawMode.Sliced;
            _portrait = new GameObject("ClonePortrait").AddComponent<SpriteRenderer>();
            _portrait.transform.SetParent(transform, false);
            _portrait.sprite = SpriteResource.LoadPortrait(unit.PortraitPath, out _);
            _portrait.color = new Color(0.74f, 0.95f, 0.91f, _opacity);
            FollowOwner();
        }

        private void LateUpdate() => FollowOwner();

        private void FollowOwner()
        {
            if (_unit == null || _unit.SummonOwner == null || _portrait == null) return;
            Unit owner = _unit.SummonOwner;
            var view = CardMotion.ViewOf(owner);
            if (view == null || view.Body == null) { _portrait.enabled = _frame.enabled = false; return; }
            _portrait.enabled = owner.isActive && owner.IsOnField;
            int index = Mathf.Clamp(Summons.Of(owner).IndexOf(_unit), 0, 2);
            float facing = owner.IsEnemy ? -1f : 1f;
            float card = view.CardSize;
            // 오래된 분신이 가장 가까운 앞쪽에 있고 새 분신은 위·뒤로 한 장씩 펼쳐진다.
            transform.position = view.Body.position + new Vector3(-facing * (.42f + index * .10f), .28f + index * .10f) * card + _advance;
            _portrait.sortingLayerID = view.PortraitRenderer.sortingLayerID;
            _portrait.sortingOrder = view.PortraitRenderer.sortingOrder - 3 - index;
            _frame.enabled = _portrait.enabled;
            _frame.sortingLayerID = _portrait.sortingLayerID;
            _frame.sortingOrder = _portrait.sortingOrder - 1;
            _frame.size = new Vector2(.61f, .66f) * card;
            _frame.color = new Color(1f, 1f, 1f, _opacity);
            float width = _portrait.sprite != null ? _portrait.sprite.bounds.size.x : 1f;
            _portrait.transform.localScale = Vector3.one * (card * .64f / Mathf.Max(.01f, width));
            _portrait.color = new Color(.74f, .95f, .91f, _opacity - index * .07f);
        }

        public IEnumerator Advance(Unit target, float scale = 1f)
        {
            Vector3 direction = VfxSpace.AimFrom(_unit, target).Direction;
            float card = VfxSpace.CardSize;
            _opacity = .68f * scale;
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
            UchiFx.PaperDeparture(transform.position, _portrait.sprite, _portrait.transform.lossyScale,
                _portrait.color, _unit);
            _portrait.enabled = false;
            _frame.enabled = false;
        }

        private void Trace(string tag)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise(FormattableString.Invariant($"{tag}|{_unit.GetInstanceID()}|{transform.position.x:F5}|{transform.position.y:F5}"));
#endif
        }
    }
}
