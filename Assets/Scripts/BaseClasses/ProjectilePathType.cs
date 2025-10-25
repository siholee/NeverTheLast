namespace BaseClasses
{
    /// <summary>
    /// 투사체 경로 타입을 정의하는 Enum
    /// </summary>
    public enum ProjectilePathType
    {
        /// <summary>
        /// 직선 경로 - 가장 빠른 경로
        /// </summary>
        Linear,
        
        /// <summary>
        /// 포물선 경로 - 중력의 영향을 받는 자연스러운 궤적
        /// </summary>
        ParabolicArc,
        
        /// <summary>
        /// 베지어 곡선 경로 - 부드러운 곡선 경로
        /// </summary>
        BezierCurve,
        
        /// <summary>
        /// 다중 포인트 경유 - 여러 지점을 경유하는 경로
        /// </summary>
        MultiPoint,
        
        /// <summary>
        /// 지연 낙하 - 목표 지점 위로 올라간 후 수직 낙하
        /// </summary>
        DelayedDrop,
        
        /// <summary>
        /// 나선형 - 회전하면서 전진하는 경로
        /// </summary>
        Spiral,
        
        /// <summary>
        /// 웨이브 - 부드러운 파동 형태로 이동
        /// </summary>
        Wave
    }
}

