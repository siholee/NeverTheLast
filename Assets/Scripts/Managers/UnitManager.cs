using System;
using UnityEditor.Experimental.GraphView;
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
      Debug.Log("Hero Cooldown Set");
      heroCooldowns[x, y].SetUnit(unit);
    }
    else
    {
      Debug.Log("Enemy Cooldown Set");
      enemyCooldowns[x, y].SetUnit(unit);
    }
  }

  private void Update()
  {
    if (gameManager.gameState == GameManager.GameState.RoundInProgress)
    {
      foreach (var cCon in heroCooldowns)
      {
        ProcessFrame(cCon);
      }
      foreach (var cCon in enemyCooldowns)
      {
        ProcessFrame(cCon);
      }
    }
  }

  private void ProcessFrame(CooldownController cCon)
  {
    if (cCon.unit == null)
    {
      return;
    }
    cCon.UpdateCooldown(Time.deltaTime);
    if (cCon.unit.isControlled)
    {
      cCon.unit.controlDuration -= Time.deltaTime;
      if (cCon.unit.controlDuration <= 0)
      {
        cCon.unit.OnControlEnd();
      }
    }
    else
    {
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