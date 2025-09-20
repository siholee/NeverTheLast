namespace StatusEffects.Base
{
    public class SynergyEffect: StatusEffect, IStackEffect
    {
        public string SynergyName;
        public string SynergyDescription;
        
        public SynergyEffect(string identifier, string name, string description) : base(null, identifier)
        {
            SynergyName = name;
            SynergyDescription = description;
            Stack = 1;
            Duration = 0f;
        }
        
        public void SetStack(int count)
        {
            Stack = count;
        }
    }
}