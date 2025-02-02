using System.Collections.Generic;
using UnityEngine;

public class Instant : SFXEffect
{
  public Instant(Cell target, Vector2 targetOffset, ParticleSystem ps)
  {
    this.target = target;
    this.targetOffset = targetOffset;
    particles = ps;
  }

  public override void ApplyEffect()
  {
    
  }
  
  public override void ExpireEffect()
  {
  }
}