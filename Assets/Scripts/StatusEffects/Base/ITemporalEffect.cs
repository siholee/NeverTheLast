using BaseClasses;
using Entities;

namespace StatusEffects.Base
{
    public interface ITemporalEffect
    {
        public void UpdateDuration(float duration);
        public int IsTriggered(float deltaTime);
        public void OnUpdate(EventContext context);
    }
}