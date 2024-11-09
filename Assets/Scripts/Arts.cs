using UnityEngine;

public class Arts : MonoBehaviour
{
    public string NAME;
    public string TYPE;
    public Unit OWNER; // 스킬 소유자
    public int COUNTER;
    public float TIMER;
    public float CT;
    public float CURRENT_CT;
    public float MAX_CT;

    public string[] CONDITIONS;
    public string[] EFFECTS;

    public void EffectReader()
    {
        foreach (string effect in EFFECTS)
        {
            // 문자열이 null이거나 느낌표로 시작하지 않으면 에러 로그 출력
            if (string.IsNullOrEmpty(effect) || !effect.StartsWith("!"))
            {
                Debug.LogError("Invalid effect format: " + effect);
                continue;
            }

            // 느낌표와 '-' 사이의 문자열 추출
            int firstDashIndex = effect.IndexOf('-');
            if (firstDashIndex == -1)
            {
                Debug.LogError("Invalid effect format: " + effect);
                continue;
            }

            string attackType = effect.Substring(1, firstDashIndex - 1);

            // '-'를 기준으로 나머지 문자열 파싱
            int secondDashIndex = effect.IndexOf('-', firstDashIndex + 1);
            if (secondDashIndex == -1)
            {
                Debug.LogError("Invalid effect format: " + effect);
                continue;
            }

            string attribute = effect.Substring(firstDashIndex + 1, secondDashIndex - firstDashIndex - 1);
            string damageStr = effect.Substring(secondDashIndex + 1);

            // 괄호 안의 수치 추출
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

            // 데미지를 계산 (능력치에 따라)
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

            // '#'로 태그를 분리
            string[] tags = damageStr.Substring(closeParenIndex + 1).Split('#', System.StringSplitOptions.RemoveEmptyEntries);

            // EffectHandler 호출
            OWNER.EffectHandler(damage, tags);
        }
    }
}
