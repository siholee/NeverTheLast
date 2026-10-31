using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>몸체 반동 뒤의 손앞에 시전광을 맞춘다. 정지 첫 프레임에도 상대 위치를 보존한다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PurchasedArcherCastAnchor : MonoBehaviour
    {
        private Unit _caster,_target;

        public static void Bind(FlipbookFx cast,Unit caster,Unit target)
        {
            var anchor=cast.gameObject.AddComponent<PurchasedArcherCastAnchor>();
            anchor._caster=caster;anchor._target=target;
        }

        private void LateUpdate()
        {
            if(_caster==null||!_caster.isActive){Destroy(gameObject);return;}
            // 시간·셀을 진행하지 않는다. UnitCardView와 FlipbookFx의 기본 LateUpdate 뒤에 위치만 맞춘다.
            transform.position=PurchasedArcherFx.EmissionPoint(_caster,_target);
        }
    }
}
