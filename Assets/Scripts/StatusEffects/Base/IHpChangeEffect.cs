namespace StatusEffects.Base
{
    public interface IHpChangeEffect
    {
        public int HpFlatChange();
        public float HpPercentageChange();
    }
}