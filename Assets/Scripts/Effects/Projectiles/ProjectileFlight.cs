using System.Collections.Generic;
using BaseClasses;

namespace Effects.Projectiles
{
    /// <summary>
    /// 투사체가 어떤 궤적으로 날아갈지 고른다.
    ///
    /// 궤적은 두 가지만 쓴다.
    ///   · <b>직선형</b>(<see cref="ProjectilePathType.Linear"/>) — 마법·특수 투사체
    ///   · <b>곡선형</b>(<see cref="ProjectilePathType.ParabolicArc"/>) — 화살·투척 등 물리 원거리
    ///
    /// 나머지 경로 타입(Spiral·Wave·MultiPoint 등)은 구현은 남아 있지만 쓰지 않는다.
    /// 이 게임은 XY 평면을 정사영으로 비추므로 <b>경로에 수직인 오프셋이 z축으로 나가 화면에 보이지 않는다</b>.
    /// Wave는 전량, Spiral은 절반이 그 축이라 의도한 모양이 나오지 않는다.
    /// 반면 곡선형은 y를 직접 올리므로 2D에서도 그대로 보인다.
    /// </summary>
    public static class ProjectileFlight
    {
        /// <summary>곡선형이 중간 지점에서 떠오르는 높이(월드 단위). 칸 한 변(10.16)의 절반쯤.</summary>
        public const float ArcHeight = 5f;

        /// <summary>
        /// 피해 태그로 궤적을 고른다. <b>원거리 물리만 곡선형</b>이고 나머지는 직선형이다.
        ///
        /// 접촉(근접) 물리는 제외한다 — 붙어서 때리는 연출에 포물선을 씌우면
        /// 이펙트가 위로 솟았다 내려와 타격감이 어긋난다.
        /// </summary>
        public static ProjectilePathType PathFor(IReadOnlyList<int> damageTags)
        {
            if (damageTags == null) return ProjectilePathType.Linear;

            bool physical = false;
            bool contact = false;
            for (int i = 0; i < damageTags.Count; i++)
            {
                if (damageTags[i] == DamageTag.Physical) physical = true;
                else if (damageTags[i] == DamageTag.ContactAttack) contact = true;
            }
            return physical && !contact ? ProjectilePathType.ParabolicArc : ProjectilePathType.Linear;
        }

        /// <summary><see cref="DamageContext"/>에서 곧바로 궤적을 고른다.</summary>
        public static ProjectilePathType PathFor(DamageContext context) => PathFor(context?.DamageTags);

        /// <summary>궤적에 맞는 파라미터. 직선형은 쓰는 값이 없어 기본값을 준다.</summary>
        public static ProjectilePathData DataFor(ProjectilePathType pathType)
        {
            return pathType == ProjectilePathType.ParabolicArc
                ? ProjectilePathData.CreateParabolic(ArcHeight)
                : new ProjectilePathData();
        }
    }
}
