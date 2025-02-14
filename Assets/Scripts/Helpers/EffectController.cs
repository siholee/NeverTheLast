using System.Collections.Generic;

public class EffectController
{
  public Dictionary<string, EffectBase> effectsDict;

  public EffectController()
  {
    effectsDict = new Dictionary<string, EffectBase>();
  }

  public void AddEffect(EffectBase effect)
  {
    if (!effectsDict.ContainsKey(effect.effectName))
    {
      effectsDict.Add(effect.effectName, effect);
      effect.ApplyEffect();
    }
    else
    {
      effectsDict[effect.effectName] = effect;
    }
  }

  public void RemoveEffect(string codeName)
  {
    if (effectsDict.ContainsKey(codeName))
    {
      effectsDict[codeName].RemoveEffect();
      effectsDict.Remove(codeName);
    }
  }

  public void ModifyStacks(string codeName, int amount)
  {
    if (effectsDict.ContainsKey(codeName))
    {
      effectsDict[codeName].stack += amount;
    }
  }

  public void UpdateEffects(float deltaTime)
  {
    foreach (var dictItem in effectsDict)
    {
      var effect = dictItem.Value;
      if (effect.persistantType == PersistantType.Permanent)
      {
        continue;
      }
      dictItem.Value.timer -= deltaTime;
      if (dictItem.Value.timer <= 0)
      {
        RemoveEffect(dictItem.Key);
      }
    }
  }
}