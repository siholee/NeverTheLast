using Helpers;
using UnityEngine;

namespace Entities.View
{
    /// <summary>
    /// 칸을 차지하지 않는 소환수의 카드 자리.
    ///
    /// 일반 유닛의 카드는 <see cref="Cell"/>이 만들어 준다(<c>Cell.EnsureCard</c>).
    /// 소환수는 칸이 없으므로 <b>소환자 카드 바깥쪽에 붙는 소형 카드</b>로 세운다.
    /// 카드는 프리팹이 아니라 코드로 조립되므로(<see cref="UnitCardView.Attach"/>)
    /// 칸이 아닌 트랜스폼에도 같은 카드를 그대로 얹을 수 있다.
    ///
    /// <b>크기는 소환자 카드 한 변의 42%다.</b> 1/3이면 체력 바가 몇 픽셀로 뭉개져 "곧 죽는다"를
    /// 눈으로 알 수 없고, 1/2이면 열 사이 틈에 들어가지 않는다.
    ///
    /// 서는 자리는 <b>소환자 카드의 바깥쪽(적에게서 먼 쪽) 위</b>다. 예전에는 소환자 카드의
    /// 모서리에 겹쳐 세웠는데, 어느 모서리든 카드 위에는 초상화 · 이름 · 게이지가 있어 5인 편성에서
    /// 라이트의 소환수가 라이트의 정보를 가렸다(QA). 전장의 열 간격을 넓혀(<see cref="Managers.GridManager"/>)
    /// 카드 사이에 소형 카드가 들어갈 틈을 만들었다. 둘째 소환수는 그 아래에 선다.
    /// </summary>
    public sealed class SummonCardView : MonoBehaviour
    {
        /// <summary>소환자 카드 한 변 대비 비율.</summary>
        public const float ScaleRatio = 0.42f;

        /// <summary>소환자 카드와 소형 카드 사이의 틈(월드 단위).</summary>
        private const float Gap = 0.2f;

        /// <summary>소환자 카드(order+6까지 씀) 위에 얹는 깊이 오프셋.</summary>
        private const int DepthOffset = 7;

        private UnitCardView _card;
        private SpriteRenderer _portrait;
        private Unit _unit;
        private Cell _ownerCell;
        private int _appliedOrder = int.MinValue;
        private int _appliedLayer;

        /// <summary>
        /// 소환자 카드 바깥쪽에 소환수 카드를 세운다.
        /// 소환자가 칸에 없으면 붙일 자리가 없으므로 만들지 않는다.
        /// </summary>
        public static SummonCardView Attach(Unit summon, Unit owner)
        {
            if (summon == null || owner == null || owner.currentCell == null) return null;

            float cardSize = Cell.CardSize * ScaleRatio;

            // 이미 붙어 있는 소환수 수. 둘째부터는 첫째 아래로 내려 선다.
            int stacked = owner.currentCell.GetComponentsInChildren<SummonCardView>().Length;

            var go = new GameObject($"SummonCard_{summon.UnitName}");
            go.transform.SetParent(owner.currentCell.transform, false);

            // 바깥쪽 = 적에게서 먼 쪽. 아군은 왼쪽, 적은 오른쪽이다.
            float outward = owner.IsEnemy ? 1f : -1f;
            float x = outward * (Cell.CardSize * 0.5f + Gap + cardSize * 0.5f);
            float step = cardSize + UnitCardView.HudBelow(cardSize) + 0.3f;
            float y = Cell.CardSize * 0.5f - cardSize * 0.5f - stacked * step;
            go.transform.localPosition = new Vector3(x, y, 0f);

            var host = go.AddComponent<SummonCardView>();
            host._unit = summon;
            host._card = UnitCardView.Attach(go.transform, Cell.CellSize * ScaleRatio);
            host.BuildPortrait(summon, cardSize);
            host._card.Bind(summon, combatHud: true);
            host._ownerCell = owner.currentCell;
            host.ApplyDepthFrom(host._ownerCell);

            summon.SummonView = host;
            return host;
        }

        /// <summary>
        /// 소환수 초상화를 카드 안에 세운다.
        ///
        /// 칸의 <c>portraitRenderer</c>는 프리팹이 들고 있어 소환수에게는 없다.
        /// 같은 규칙(카드 안에 contain)으로 직접 만들어 카드에 물려 준다.
        /// </summary>
        private void BuildPortrait(Unit summon, float cardSize)
        {
            if (string.IsNullOrWhiteSpace(summon.PortraitPath)) return;

            Sprite sprite = SpriteResource.LoadPortrait(summon.PortraitPath);
            if (sprite == null) return;

            var portraitObject = new GameObject("Portrait", typeof(SpriteRenderer));
            portraitObject.transform.SetParent(transform, false);

            _portrait = portraitObject.GetComponent<SpriteRenderer>();
            _portrait.sprite = sprite;

            float fit = cardSize * 1.03f;   // 일반 유닛처럼 카드 면 없이 원화를 조금 크게 세운다.
            float scale = Mathf.Min(
                fit / Mathf.Max(0.0001f, sprite.bounds.size.y),
                fit / Mathf.Max(0.0001f, sprite.bounds.size.x));
            portraitObject.transform.localScale = new Vector3(scale, scale, 1f);

            _card?.BindPortrait(_portrait);
        }

        /// <summary>소환자 칸의 깊이 위에 얹는다. 같은 줄 안에서 소환자보다 앞에 그려진다.</summary>
        private void ApplyDepthFrom(Cell cell)
        {
            if (cell == null) return;

            int order = cell.DepthOrder + DepthOffset;
            if (order == _appliedOrder && cell.DepthLayerId == _appliedLayer) return;

            _appliedOrder = order;
            _appliedLayer = cell.DepthLayerId;

            _card?.ApplyDepth(order, _appliedLayer);
            if (_portrait != null)
            {
                _portrait.sortingLayerID = _appliedLayer;
                _portrait.sortingOrder = order + 1;
            }
        }

        /// <summary>소환수 상태가 바뀌었을 때 카드를 다시 그린다.</summary>
        public void Tick() => _card?.Tick();

        /// <summary>투사체 착탄·근접 타격 시점의 카드 반응. 칸의 같은 이름 진입점과 짝이다.</summary>
        public void PlayAttackReaction(float strength = 1f) => _card?.PlayAttackReaction(strength);

        /// <summary>피해가 들어간 순간의 카드 반응. 칸의 같은 이름 진입점과 짝이다.</summary>
        public void PlayHitReaction(float strength, bool physical, bool crit)
            => _card?.PlayHitReaction(strength, physical, crit);

        private void Update()
        {
            // 칸이 매 프레임 카드를 따라가듯(Cell.Update), 소환수 카드도 스스로 따라간다.
            if (_unit == null || !_unit.isActive) return;

            _card?.Tick();
            // 줄 배치가 다시 잡히면 칸의 깊이가 바뀐다. 값이 달라졌을 때만 다시 얹는다.
            ApplyDepthFrom(_ownerCell);
        }

        /// <summary>소환수가 사라질 때 카드를 걷는다.</summary>
        public void Dismiss()
        {
            if (_unit != null && _unit.SummonView == this) _unit.SummonView = null;
            _card?.Bind(null, false);
            _unit = null;
            if (this != null) Destroy(gameObject);
        }
    }
}
