using System.Collections.Generic;
using UnityEngine;

public abstract class SFXEffect : EffectBase
{
  public Vector2 castOffset; // 시전 시작 위치 오프셋(시전 유닛 기준)
  public Cell target; // 대상 셀(들)
  public Vector2 targetOffset; // 시전 대상 위치 오프셋(시전 대상 셀 기준)
  public float duration; // 시각이펙트의 지속시간

  public ParticleSystem particles; // 파티클시스템(임시)
}