using System.Collections.Generic;
using UnityEngine;

public class Aura : SFXEffect
{
  public float radius; // 오라의 반경경

  public Aura(Cell target, Vector2 targetOffset, float radius, ParticleSystem ps)
  {
    this.target = target;
    this.targetOffset = targetOffset;
    this.radius = radius;
    particles = ps;
  }

  public override void ApplyEffect()
  {
    
  }
  
  public override void RemoveEffect()
  {
  }
}