using Helpers;
using UnityEngine;

namespace Entities.View
{
    /// <summary>
    /// 칸을 차지하지 않는 소환수의 카드 자리.
    ///
    /// 일반 유닛의 카드는 <see cref="Cell"/>이 만들어 준다(<c>Cell.EnsureCard</c>).
    /// 소환수는 칸이 없으므로 <b>소환자 카드의 아래 모서리에 겹치는 소형 카드</b>로 세운다.
    /// 카드는 프리팹이 아니라 코드로 조립되므로(<see cref="UnitCardView.Attach"/>)
    /// 칸이 아닌 트랜스폼에도 같은 카드를 그대로 얹을 수 있다.
    ///
    /// <b>크기는 소환자 카드의 1/4(한 변 1/2)이다.</b> 1/9(한 변 1/3)도 후보였지만,
    /// 소환수는 체력을 가지고 격파당하므로 <b>체력 바를 읽을 수 있어야 한다.</b>
    /// 한 변이 1/3이면 체력 바가 몇 픽셀로 뭉개져 "곧 죽는다"를 눈으로 알 수 없다.
    /// 1/2은 종속 관계가 분명히 보이면서 바가 살아 있는 가장 작은 크기다.
    ///
    /// 서는 쪽은 <b>소환자의 등 뒤</b>다 — 아군은 왼쪽 아래, 적은 오른쪽 아래.
    /// 카드가 공격할 때 적 방향으로 전진하므로, 반대편에 두어야 연출과 겹치지 않는다.
    /// </summary>
    public sealed class SummonCardView : MonoBehaviour
    {
        /// <summary>소환자 카드 한 변 대비 비율. 면적으로는 1/4이다.</summary>
        public const float ScaleRatio = 0.5f;

        /// <summary>
        /// 모서리에서 얼마나 밖으로 걸치는가(소형 카드 한 변 기준).
        /// 0이면 소환자 카드의 1/4을 정확히 덮는다. 조금 흘려 두면 초상화를 덜 가리면서
        /// '붙어 있는 작은 카드'로 읽힌다.
        /// </summary>
        private const float OverhangRatio = 0.22f;

        /// <summary>소환자 카드(order+6까지 씀) 위에 얹는 깊이 오프셋.</summary>
        private const int DepthOffset = 7;

        private UnitCardView _card;
        private SpriteRenderer _portrait;
        private Unit _unit;
        private Cell _ownerCell;
        private int _appliedOrder = int.MinValue;
        private int _appliedLayer;

        /// <summary>
        /// 소환자 카드의 아래 모서리에 소환수 카드를 세운다.
        /// 소환자가 칸에 없으면 붙일 자리가 없으므로 만들지 않는다.
        /// </summary>
        public static SummonCardView Attach(Unit summon, Unit owner)
        {
            if (summon == null || owner == null || owner.currentCell == null) return null;

            float cardSize = Cell.CardSize * ScaleRatio;

            var go = new GameObject($"SummonCard_{summon.UnitName}");
            go.transform.SetParent(owner.currentCell.transform, false);

            // 아군 소환수는 왼쪽 아래, 적 소환수는 오른쪽 아래. 소환자의 등 뒤에 선다.
            float side = summon.IsEnemy ? 1f : -1f;
            float inset = Cell.CardSize * 0.5f - cardSize * (0.5f - OverhangRatio);
            go.transform.localPosition = new Vector3(side * inset, -inset, 0f);

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

            float fit = cardSize * 0.86f;   // Cell.PortraitFitSize와 같은 여백 비율
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
