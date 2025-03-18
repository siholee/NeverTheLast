namespace StatusEffects.Base
{
    public class SynergyEffect: StatusEffect, IStackEffect
    {
        public string SynergyName;
        public string SynergyDescription;

        public void SetStack(int count)
        {
            Stack = count;
        }
    }
}