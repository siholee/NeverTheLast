using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Entities;
using Managers;

namespace Codes.Base
{
  public class NormalCode : Code
  {
    /// <summary>이번 일반행동 슬롯이 조건형 대체행동으로 바뀌는가.</summary>
    public virtual bool IsSubstituteAction => false;

    /// <summary>행동 시작 배너에 표시할 현재 행동 이름.</summary>
    public virtual string AnnouncementName => CodeName;

    public NormalCode(NormalCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Active;
    }

    // ══════════════════════════════════════════════════════════
    // 지휘 사격 계약 — 다른 유닛의 특수행동이 이 일반 공격을 한 번 빌린다(나폴레옹, Detail_08 §20.3)
    //
    // 클래스 종류가 아니라 "이 일반행동이 실제로 공격하는가"로 참여를 정한다. BaseNormalCode는 자기 전달을,
    // 사라지며 치는 분신 일격처럼 다른 뼈대의 공격은 각자 CommandedDeliver를 구현한다.
    // 일반행동 시작 · 완료 · 행동치 · 마나 +20 · 턴은 생기지 않는다. 최종 피해 배율과 늦은 판정 차단은
    // 피해 컨텍스트에 붙는 CommandedAttackScope가 맡는다.
    // ══════════════════════════════════════════════════════════

    private CommandedAttack _command;
    private List<Unit> _savedTargets;

    /// <summary>지휘 사격에 응하는 실제 공격형 일반행동인가. 공격하지 않는 일반행동은 거짓이다.</summary>
    public virtual bool CanJoinCommand => false;

    /// <summary>지금 이 코드가 다른 유닛의 지휘로 공격 중인가(종료되지 않은 지휘가 열려 있다).</summary>
    public bool IsCommanded => _command != null && !_command.Ended;

    /// <summary>지금 열린 지휘의 표지. 피해 컨텍스트를 직접 만드는 코드가 <see cref="StampCommand"/>로 붙인다.</summary>
    protected CommandedAttackScope CurrentCommand => _command != null && !_command.Ended ? _command.Scope : null;

    /// <summary>지휘 중이면 컨텍스트에 표지와 최종 배율을 붙인다. 지휘 밖이면 아무것도 하지 않는다.</summary>
    protected void StampCommand(DamageContext context) => CurrentCommand?.Stamp(context);

    /// <summary>외부에서 끊겨 남은 지휘 범위를 닫는다. 실제 일반행동 시작 · 새 지휘 시작에서 부른다.</summary>
    protected void EndStaleCommand() => _command?.End();

    public override void StopCode()
    {
      EndStaleCommand();
      base.StopCode();
    }

    /// <summary>지휘 공격의 대상 교체 · 선제 개입 판정에 쓸 피해 태그.</summary>
    protected virtual List<int> CommandDamageTags()
      => new() { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack };

    /// <summary>지휘 범위가 열릴 때 일반행동 전용 상태(대체행동 박자 등)를 단일 공격 모양으로 잠시 바꾼다.</summary>
    protected virtual void OnCommandBegin() { }

    /// <summary>지휘 범위가 닫힐 때 <see cref="OnCommandBegin"/>이 바꾼 상태를 되돌린다.</summary>
    protected virtual void OnCommandEnd() { }

    /// <summary>지휘 공격 한 번의 실제 전달. 원래 공격의 적중 후처리를 그대로 연다.</summary>
    protected virtual IEnumerator CommandedDeliver(Unit target) { yield break; }

    /// <summary>지휘 공격 한 번이 대상에게 들어갈 결정적 추정 피해(치명 없음, 최종 배율 포함). 대상 선정이 읽는다.</summary>
    public virtual int EstimateCommandedDamage(Unit target) => 0;

    /// <summary>
    /// 지휘로 이 일반 공격을 한 번 빌린다. 반환된 지휘는 <see cref="CommandedAttack.End"/>로 반드시 닫는다.
    /// 지휘 특수행동은 한 번에 하나뿐이므로 남아 있는 범위는 외부에서 끊긴 이전 지휘다 — 닫고 새로 연다.
    /// </summary>
    public CommandedAttack BeginCommandedAttack(Unit lockedTarget, Unit commander = null,
      float ratio = CommandedAttackScope.FinalDamageRatio)
    {
      if (Caster == null || !Caster.isActive || !CanJoinCommand) return null;
      if (lockedTarget == null || !lockedTarget.isActive) return null;
      EndStaleCommand();
      var attack = new CommandedAttack(this, new CommandedAttackScope(commander, Caster, lockedTarget, ratio));
      _command = attack;
      _savedTargets = TargetUnits;
      OnCommandBegin();
      return attack;
    }

    /// <summary>지휘 한 번을 순서대로 끝까지 실행한다(대상 교체 · 선제 개입 → 공지 → 전달 → 정리).</summary>
    public IEnumerator PerformCommandedAttack(Unit lockedTarget, Unit commander = null)
    {
      CommandedAttack attack = BeginCommandedAttack(lockedTarget, commander);
      if (attack == null) yield break;
      try
      {
        yield return attack.Prepare();
        yield return attack.Deliver();
      }
      finally
      {
        attack.End();
      }
    }

    /// <summary>
    /// 지휘 한 건. 사전 단계(<see cref="Prepare"/>)와 전달(<see cref="Deliver"/>)이 나뉘어 지휘자가 사전 단계를 차례로,
    /// 전달을 동시에 돌린다. 취소 · 종료는 표지에 남아 이미 만든 피해 컨텍스트의 늦은 판정도 막는다.
    /// </summary>
    public sealed class CommandedAttack
    {
      private readonly NormalCode _source;

      internal CommandedAttack(NormalCode source, CommandedAttackScope scope)
      {
        _source = source;
        Scope = scope;
      }

      public NormalCode Source => _source;
      public CommandedAttackScope Scope { get; }
      public Unit Attacker => Scope.Attacker;
      public Unit LockedTarget => Scope.LockedTarget;
      /// <summary>대상 교체를 거친 실제 수신자. 준비가 끝나기 전에는 null.</summary>
      public Unit FinalTarget { get; private set; }
      public bool Ready { get; private set; }
      public bool Delivered { get; private set; }
      public bool Cancelled => Scope.Cancelled;
      public bool Ended => Scope.Ended;
      public int HitsLanded => Scope.HitsLanded;

      /// <summary>대상 교체 → 선제 개입 → 공지. 공격자가 막히거나 대상이 사라지면 취소로 끝난다.</summary>
      public IEnumerator Prepare()
      {
        if (!Scope.Active) yield break;
        Unit attacker = Attacker;
        AttackTargetResolution routing = AttackTargetRouter.Prepare(attacker, new[] { LockedTarget },
          BaseEnums.CodeType.Normal, _source.CommandDamageTags(), $"{_source.CodeName}#command");
        yield return routing.ResolvePreAttack();
        Unit target = routing.Targets.FirstOrDefault(unit => unit != null && unit.isActive);
        if (!Scope.Active || !routing.CanContinue || target == null || !Targeting.Announce(attacker, routing.Targets))
        {
          Cancel();
          yield break;
        }
        FinalTarget = target;
        Ready = true;
      }

      /// <summary>원래 공격의 전달을 한 번 실행한다. 일반행동 해결 알림은 내지 않는다.</summary>
      public IEnumerator Deliver()
      {
        if (!Ready || !Scope.Active || FinalTarget == null || !FinalTarget.isActive) yield break;
        _source.TargetUnits = new List<Unit> { FinalTarget };
        AudioManager.PlayAction(Attacker, SfxActionSlot.Normal, SfxCuePhase.Cast, _source.CodeName);
        yield return _source.CommandedDeliver(FinalTarget);
        Delivered = true;
      }

      public void Cancel() => Scope.Cancel();

      /// <summary>지휘 범위를 닫고 바꿔 둔 상태를 되돌린다. 표지는 종료로 남아 늦은 판정을 계속 막는다.</summary>
      public void End()
      {
        if (Scope.Ended) return;
        Scope.End();
        if (_source._command != this) return;
        _source._command = null;
        _source.TargetUnits = _source._savedTargets;
        _source._savedTargets = null;
        _source.OnCommandEnd();
      }
    }
  }
}
