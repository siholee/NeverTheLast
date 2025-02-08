using System;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
  public GameManager gameManager;

  public CooldownController[,] heroCooldowns;
  public CooldownController[,] enemyCooldowns;


  public void SetUnitCooldown(Unit unit)
  {
    Cell cell = unit.currentCell;
    int x = unit.isEnemy ? cell.xPos - 1 : cell.xPos + 4;
    int y = cell.yPos - 1;
    if (!unit.isEnemy)
    {
      heroCooldowns[x, y].SetUnit(unit);
    }
    else
    {
      enemyCooldowns[x, y].SetUnit(unit);
    }
  }

  private void Update()
  {
    if (gameManager.gameState == GameManager.GameState.RoundInProgress)
    {
      foreach (CooldownController cCon in heroCooldowns)
      {
        cCon.UpdateCooldown(Time.deltaTime);
        CodeBase.CodeType typeToCast = cCon.CheckCodeTypeToCast();
        switch (typeToCast)
        {
          case CodeBase.CodeType.Passive:
            cCon.unit.ActivatePassiveCode();
            cCon.ResetCooldown(CodeBase.CodeType.Passive);
            break;
          case CodeBase.CodeType.Normal:
            cCon.unit.ActivateNormalCode();
            cCon.ResetCooldown(CodeBase.CodeType.Normal);
            break;
          case CodeBase.CodeType.Ultimate:
            cCon.unit.ActivateUltimateCode();
            cCon.ResetCooldown(CodeBase.CodeType.Ultimate);
            break;
        }
      }
      foreach (CooldownController cCon in enemyCooldowns)
      {
        cCon.UpdateCooldown(Time.deltaTime);
        CodeBase.CodeType typeToCast = cCon.CheckCodeTypeToCast();
        switch (typeToCast)
        {
          case CodeBase.CodeType.Passive:
            cCon.unit.ActivatePassiveCode();
            cCon.ResetCooldown(CodeBase.CodeType.Passive);
            break;
          case CodeBase.CodeType.Normal:
            cCon.unit.ActivateNormalCode();
            cCon.ResetCooldown(CodeBase.CodeType.Normal);
            break;
          case CodeBase.CodeType.Ultimate:
            cCon.unit.ActivateUltimateCode();
            cCon.ResetCooldown(CodeBase.CodeType.Ultimate);
            break;
        }
      }
    }
  }
}