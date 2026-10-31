using System.Collections;
using UnityEngine;

namespace Combat
{
    /// <summary>절대 Time.time의 float 크기와 무관하게 양 호스트가 같은 상대 판정 대기를 진행한다.</summary>
    public static class CombatPresentationWait
    {
        public static IEnumerator Delay(float seconds)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                yield return null;
                remaining -= Time.deltaTime;
            }
        }
    }
}
