using System.Collections.Generic;

namespace Managers
{
    /// <summary>
    /// 사건(이벤트)을 페이즈 사이 어디서든 발생시키기 위한 스케줄러.
    /// <para>
    /// 기존에는 사건이 테마의 5번째 스테이지(<c>StageInRound == 5</c>)에만 고정되어 있었다.
    /// 이 스케줄러는 그 고정 슬롯과 별개로, 런타임에 어떤 페이즈 전환 지점에서든 발생시킬 수 있는
    /// 동적 사건 큐를 제공한다. 포켓로그/붕괴: 스타레일의 '사건'처럼 준비/보상/라운드 종료 등
    /// 임의의 페이즈 경계에서 사건을 끼워 넣는 데 사용한다.
    /// </para>
    /// <para>
    /// GameManager는 페이즈 전환 지점에서 <see cref="TryDequeue"/>를 호출해 대기 중인 사건이 있으면
    /// 먼저 처리하고, 사건 종료 후 원래 흐름을 이어서 진행한다.
    /// </para>
    /// </summary>
    public class EventScheduler
    {
        private readonly Queue<StageEventData> _pending = new Queue<StageEventData>();

        /// <summary>대기 중인 사건이 하나라도 있으면 true.</summary>
        public bool HasPending => _pending.Count > 0;

        /// <summary>대기 중인 사건 수.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// 사건을 큐 맨 뒤에 예약한다. 다음 페이즈 체크포인트에서 순서대로 발생한다.
        /// </summary>
        public void Enqueue(StageEventData stageEvent)
        {
            if (stageEvent != null)
            {
                _pending.Enqueue(stageEvent);
            }
        }

        /// <summary>
        /// 가장 먼저 예약된 사건을 꺼낸다. 없으면 null.
        /// </summary>
        public StageEventData TryDequeue()
        {
            return _pending.Count > 0 ? _pending.Dequeue() : null;
        }

        /// <summary>
        /// 예약된 모든 사건을 비운다. 런 종료/게임 오버 시 잔여 사건을 정리할 때 사용한다.
        /// </summary>
        public void Clear()
        {
            _pending.Clear();
        }
    }
}
