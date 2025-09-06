namespace StatusEffects.Base
{
    public interface ITemporalEffect
    {
        public void UpdateDuration(float duration);
        public bool IsTriggered(float deltaTime);
    }
}