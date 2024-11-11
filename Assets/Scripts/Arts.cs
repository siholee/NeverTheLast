using UnityEngine;
using System.Collections;

public class Arts : MonoBehaviour
{
    public string NAME;
    public string TYPE;
    public Unit OWNER;
    public int COUNTER;
    public int MAX_COUNTER;
    public float TIMER;
    public float CT; 
    public float BASE_CT;
    public float MAX_CT;

    public string[] CONDITIONS;
    public string[] EFFECTS;

    private bool isTimerActive = false;

    void Start()
    {
        StartTimer();
    }

    public void StartTimer()
    {
        if (TYPE == "NORMAL" && !isTimerActive)
        {
            StartCoroutine(NormalTypeTimerCoroutine());
        }
    }

    // NORMAL TYPE의 타이머 코루틴
    private IEnumerator NormalTypeTimerCoroutine()
    {
        isTimerActive = true;

        while (TIMER > 0)
        {
            TIMER -= 0.1f;  // 0.1초마다 0.1씩 감소
            yield return new WaitForSeconds(0.1f);
        }

        isTimerActive = false;  // 타이머가 중지됨
        EffectReader();  // EffectReader 실행
    }

    public void EffectReader()
    {
        foreach (string effect in EFFECTS)
        {
            if (string.IsNullOrEmpty(effect) || !effect.StartsWith("!"))
            {
                Debug.LogError("Invalid effect format: " + effect);
                continue;
            }

            int firstDashIndex = effect.IndexOf('-');
            if (firstDashIndex == -1)
            {
                Debug.LogError("Invalid effect format: " + effect);
                continue;
            }

            string attackType = effect.Substring(1, firstDashIndex - 1);

            int secondDashIndex = effect.IndexOf('-', firstDashIndex + 1);
            if (secondDashIndex == -1)
            {
                Debug.LogError("Invalid effect format: " + effect);
                continue;
            }

            string attribute = effect.Substring(firstDashIndex + 1, secondDashIndex - firstDashIndex - 1);
            string damageStr = effect.Substring(secondDashIndex + 1);

            int openParenIndex = damageStr.IndexOf('(');
            int closeParenIndex = damageStr.IndexOf(')');

            if (openParenIndex == -1 || closeParenIndex == -1 || closeParenIndex <= openParenIndex)
            {
                Debug.LogError("Invalid damage format: " + damageStr);
                continue;
            }

            string damageValueStr = damageStr.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1);
            if (!float.TryParse(damageValueStr, out float damageValue))
            {
                Debug.LogError("Invalid damage value: " + damageValueStr);
                continue;
            }

            float damage = 0;
            switch (attribute)
            {
                case "HP":
                    damage = OWNER.HP_MAX * (damageValue / 100);
                    break;
                case "ATK":
                    damage = OWNER.ATK * (damageValue / 100);
                    break;
                case "DEF":
                    damage = OWNER.DEF * (damageValue / 100);
                    break;
                default:
                    Debug.LogError("Unknown attribute: " + attribute);
                    continue;
            }

            string[] tags = damageStr.Substring(closeParenIndex + 1).Split('#', System.StringSplitOptions.RemoveEmptyEntries);
            OWNER.EffectHandler(damage, tags);
        }
    }
}
