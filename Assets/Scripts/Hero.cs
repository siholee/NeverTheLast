using System.Collections.Generic;

public class Hero : Unit
{
    private float CT_REDUCE; // 쿨타임 감소 값 (최대 50% 제한한)
    public Code[] ModuleSlot = new Code[5];
    public Dictionary<int, float> DamageAmplificationByAttribute = new Dictionary<int, float>(); // 속성별 피해 증가

}
