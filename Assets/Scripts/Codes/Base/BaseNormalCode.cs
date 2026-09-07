using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Base
{
    /// <summary>
    /// 모든 일반공격의 기본 클래스
    /// 공통 로직과 기본 구현을 제공하고, 상속 클래스에서 필요한 부분을 오버라이드
    /// </summary>
    public abstract class BaseNormalCode : NormalCode
    {
        protected readonly HS_Poolable _prefab;

        protected BaseNormalCode(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            Caster = context.Caster;
            Cooldown = 1;
            CastingDelay = 0.5f;
            CodeTags = new List<int> { BaseClasses.DamageTag.Physical };
            Power = 50;   // 기본 일반공격 위력
            _prefab = GameManager.Instance.sfxManager.ProjectilePrefabs["FireBlast"];
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}{Caster.FieldPositionLabel()}이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // 캐스팅
            float elapsedTime = 0f;
            while (elapsedTime < CastingDelay)
            {
                if (Caster.isControlled || !Caster.isActive)
                {
                    Debug.Log($"{Caster.UnitName}{Caster.FieldPositionLabel()}의 {CodeName} 시전이 방해됨");
                    StopCode();
                    yield break;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 타겟 선택 로직
            TargetUnits = SelectTarget();
            
            if (TargetUnits.Count == 0)
            {
                StopCode();
                yield break;
            }

            // 크리티컬 계산
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            
            // 접촉/비접촉 태그 결정
            List<int> damageTags = GetDamageTags();
            if (Caster.HasEquippedBow() &&
                damageTags.Contains(BaseClasses.DamageTag.NonContactAttack) &&
                !damageTags.Contains(BaseClasses.DamageTag.Arrow))
            {
                damageTags.Add(BaseClasses.DamageTag.Arrow);
            }
            
            // 데미지 계산 (하위 클래스에서 오버라이드 가능)
            int damage = CalculateDamage(critMultiplier);
            
            // 디버그 로그
            string contactType = damageTags.Contains(BaseClasses.DamageTag.ContactAttack) ? "접촉" : "비접촉";
            Debug.Log($"{Caster.UnitName}이 {TargetUnits[0].UnitName}에게 {contactType} 일반공격을 시전했습니다.");
            
            DamageContext context = CreateDamageContext(damage, damageTags, isCrit);
            Caster.StartCoroutine(FireProjectile(TargetUnits, 0.5f, context));
            
            // 추가 효과 처리 (하위 클래스에서 오버라이드 가능)
            yield return ApplyAdditionalEffects(TargetUnits[0], context);
            
            NotifyActionResolved();

            // 궁극기 자원은 전투 시간으로 차오른다(Unit.AccrueUltimateResource).
            // 여기서 또 주면 행동이 잦은 유닛이 이중으로 이득을 본다.
            StopCode();
        }

        /// <summary>
        /// 일반행동이 실제로 끝났음을 알린다. 공격하지 않는 일반행동도 여기를 지난다.
        /// 시전만 하고 방해받은 경우에는 발행하지 않는다 — '철벽'처럼 행동 횟수를 세는 코드가 듣는다.
        /// </summary>
        protected void NotifyActionResolved()
            => Caster?.Invoke(
                BaseClasses.BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));

        public override void StopCode()
        {
            Caster.normalCooldown = Cooldown;
            Caster.isCasting = false;
        }

        /// <summary>
        /// 데미지 계산 로직 (하위 클래스에서 오버라이드)
        /// </summary>
        protected virtual int CalculateDamage(float critMultiplier)
        {
            return RollDamage(critMultiplier);
        }

        /// <summary>캐릭터별 방어 무시·관통 같은 공격 단위 보정을 붙일 수 있는 생성 훅.</summary>
        protected virtual DamageContext CreateDamageContext(int damage, List<int> damageTags, bool isCrit)
            => new(Caster, damage, BaseEnums.CodeType.Normal, damageTags, isCrit);

        /// <summary>
        /// 추가 효과 적용 (하위 클래스에서 오버라이드)
        /// </summary>
        protected virtual IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            yield return null; // 기본적으로는 추가 효과 없음
        }

        protected virtual IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            foreach (var target in targets)
            {
                SfxManager sfx = GameManager.Instance?.sfxManager;
                bool melee = sfx != null && sfx.TryPlayMeleeAttack(Caster, target, context);
                if (melee)
                {
                    yield return new WaitForSeconds(SfxManager.MeleeImpactDelay);
                }
                else
                {
                    // 물리(화살·투척)는 곡선형, 마법·특수는 직선형으로 날아간다.
                    // 피해는 투사체가 실제로 닿은 뒤에 들어간다. 미리 재 둔 시간으로 기다리면
                    // 대상이 먼저 쓰러져 카드가 사라진 자리로 투사체만 날아가는 꼴이 된다.
                    ProjectilePathType path = ProjectileFlight.PathFor(context);
                    var token = new ProjectileImpactToken();
                    sfx?.FireElementalProjectile(
                        Caster, target, delay, path, ProjectileFlight.DataFor(path), _prefab,
                        token.MarkImpact, context);
                    yield return ProjectileFlight.WaitForImpact(token, delay);
                }
                target.TakeDamage(context);
                Caster.Invoke(BaseEnums.UnitEventType.OnNormalAttackHit, new EventContext(Caster, target, context));
                OnAttackResolved(target, context);
            }
        }

        /// <summary>투사체가 실제로 적중한 직후의 캐릭터별 후처리 훅.</summary>
        protected virtual void OnAttackResolved(Unit target, DamageContext context) { }

        public override bool HasValidTarget()
        {
            return GetAvailableEnemies().Count > 0;
        }
        
        /// <summary>
        /// 타겟 선택 로직 - 기존 타겟을 우선시하되, 더 높은 우선도의 적이 있으면 타겟 변경
        /// </summary>
        protected virtual List<Unit> SelectTarget()
        {
            List<Unit> availableEnemies = GetAvailableEnemies();
            
            if (availableEnemies.Count == 0)
                return new List<Unit>();

            int maxPriority = availableEnemies.Max(enemy => enemy.Priority);
            List<Unit> highestPriorityEnemies = availableEnemies
                .Where(enemy => enemy.Priority == maxPriority)
                .ToList();

            if (Caster.currentNormalTarget != null &&
                Caster.currentNormalTarget.isActive &&
                highestPriorityEnemies.Contains(Caster.currentNormalTarget))
            {
                return new List<Unit> { Caster.currentNormalTarget };
            }

            Caster.currentNormalTarget = highestPriorityEnemies[Random.Range(0, highestPriorityEnemies.Count)];
            
            return Caster.currentNormalTarget != null ? 
                new List<Unit> { Caster.currentNormalTarget } : 
                new List<Unit>();
        }
        
        /// <summary>
        /// 공격 가능한 적 목록 가져오기
        /// </summary>
        protected List<Unit> GetAvailableEnemies()
        {
            GridManager gridManager = GameObject.FindAnyObjectByType<GridManager>();
            if (gridManager == null)
                return new List<Unit>();
            
            bool casterIsAlly = gridManager.heroList.Contains(Caster);
            List<Unit> targetList = casterIsAlly ? gridManager.enemyList : gridManager.heroList;
            
            // 전장에 서 있는 유닛만 남긴다. 칸이 없는 소환수도 여기서 함께 걸러진다.
            return targetList.Where(unit => unit && unit.IsOnField && !unit.IsUntargetable).ToList();
        }
        
        /// <summary>
        /// 기본 일반공격 데미지 태그
        /// </summary>
        protected virtual List<int> GetDamageTags()
        {
            return new List<int> { BaseClasses.DamageTag.SingleTarget, BaseClasses.DamageTag.NormalAttack, BaseClasses.DamageTag.Physical, BaseClasses.DamageTag.NonContactAttack };
        }
    }
}
