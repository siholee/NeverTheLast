using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using UnityEngine;

namespace Effects.Projectiles
{
    /// <summary>
    /// 투사체가 실제로 대상에 닿았는지 알려 주는 표식.
    ///
    /// 공격 코드는 "몇 초쯤 걸리겠지" 하고 기다리는 대신 이 표식이 서기를 기다린다.
    /// 비행 시간과 대기 시간이 어긋나 <b>맞기도 전에 피해가 들어가는</b> 일을 막는다.
    /// </summary>
    public sealed class ProjectileImpactToken
    {
        public bool Impacted { get; private set; }

        /// <summary>투사체가 닿았다. 여러 번 불려도 상관없다.</summary>
        public void MarkImpact() => Impacted = true;
    }

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

        /// <summary>
        /// 연출이 만들어지지 않았을 때(투사체 없이 쏜 경우 등) 무한정 기다리지 않도록 두는 여유.
        /// </summary>
        private const float ImpactWaitGrace = 0.35f;

        /// <summary>
        /// 투사체가 대상에 닿을 때까지 기다린다. 피해는 이 뒤에 넣는다.
        ///
        /// 연출이 아예 만들어지지 않았거나 중간에 걷혔을 수도 있으므로,
        /// 예상 비행 시간 + 여유가 지나면 더 기다리지 않고 넘어간다.
        /// </summary>
        public static IEnumerator WaitForImpact(ProjectileImpactToken token, float expectedFlight)
        {
            if (token == null)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, expectedFlight));
                yield break;
            }

            float waited = 0f;
            float limit = Mathf.Max(0f, expectedFlight) + ImpactWaitGrace;
            while (!token.Impacted && waited < limit)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>궤적에 맞는 파라미터. 직선형은 쓰는 값이 없어 기본값을 준다.</summary>
        public static ProjectilePathData DataFor(ProjectilePathType pathType)
        {
            return pathType == ProjectilePathType.ParabolicArc
                ? ProjectilePathData.CreateParabolic(ArcHeight)
                : new ProjectilePathData();
        }
    }
}
