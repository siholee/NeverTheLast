using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;

public class Projectile : SFXEffect
{
  public Projectile(Unit caster, Vector2 offset, Cell target, Vector2 targetOffset, float duration, ParticleSystem ps)
  {
    this.caster = caster;
    castOffset = offset;
    this.target = target;
    this.targetOffset = targetOffset;
    this.duration = duration;
    particles = ps;
  }

  public override void ApplyEffect()
  {
    
  }

  public override void ExpireEffect()
  {
  }
}