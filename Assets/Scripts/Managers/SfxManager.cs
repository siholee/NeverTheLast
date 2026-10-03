using UnityEngine;

namespace Managers
{
  /// <summary>
  /// 전투 연출 매니저 자리.
  ///
  /// 2026-09 개편에서 옛 투사체 · 베기 연출과 Hovl 프리팹 표를 전부 걷어냈다.
  /// 새 연출은 <see cref="Effects.Vfx"/>가 코드로 그리므로 프리팹을 들고 있을 필요가 없다.
  /// 씬이 이 컴포넌트를 참조하고 있어 클래스만 남겨 둔다.
  /// </summary>
  public class SfxManager : MonoBehaviour
  {
  }
}
