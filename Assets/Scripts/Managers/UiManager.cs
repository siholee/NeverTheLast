using UnityEngine;
using UnityEngine.UI;

public class UiManager : MonoBehaviour
{
  public GameManager gameManager;
  [SerializeField]
  private Button _nextRoundBtn;
  public void OnNextRoundBtnClick()
  {
    GameManager.Instance.NextGameState(false);
    _nextRoundBtn.gameObject.SetActive(false);
  }
}