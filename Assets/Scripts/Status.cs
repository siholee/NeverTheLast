using UnityEngine;

// 상태의 범주를 나타내는 열거형
public enum StatusCategory {
    Positive,  // 긍정적인 상태
    Negative,  // 부정적인 상태
    Neutral    // 중립적인 상태
}

public enum StatusType {
    StatBuff,
    DOT,
    Stun,
    Etc
}

// 기본 상태 클래스
public class Status {
    // 상태 식별자
    public int ID;
    // 상태 보유자
    public Unit Owner;
    // 상태 이름
    public string Name;
    // 상태 범주
    public StatusCategory Category;
    public StatusType Type;
    // 상태 제거 가능 여부
    public bool canRemove;
    // 지속 시간 (0일 경우 무제한)
    public float Timer;
    // 횟수 제한 (0일 경우 무제한한)
    public int Counter;

    // 생성자: 기본 상태를 초기화합니다.
    public Status(int id, string name, StatusCategory category, bool canRemove, float timer, int counter) {
        this.ID = id;
        this.Name = name;
        this.Category = category;
        this.canRemove = canRemove;
        this.Timer = timer;
        this.Counter = counter;
    }

    // 상태 생성 시 호출되는 함수
    public virtual void CreateStatus() {
        // 상태 생성에 필요한 로직을 작성합니다.
        Debug.Log($"{Name} 상태가 생성되었습니다.");
    }

    // 상태 제거 시 호출되는 함수
    public virtual void RemoveStatus() {
        // 상태 제거에 필요한 로직을 작성합니다.
        Debug.Log($"{Name} 상태가 제거되었습니다.");
    }
}

