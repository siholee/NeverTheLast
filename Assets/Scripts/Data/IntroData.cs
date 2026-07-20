using System;
using System.Collections.Generic;

namespace Managers
{
    /// <summary>
    /// 인트로 시퀀스 목록(`00_intro.yaml`).
    /// 각 항목은 사건과 동일한 StageEventData 구조라 비주얼 노벨 화면이 그대로 재생한다.
    /// </summary>
    [Serializable]
    public class IntroDataList
    {
        public List<StageEventData> intros;
    }
}
