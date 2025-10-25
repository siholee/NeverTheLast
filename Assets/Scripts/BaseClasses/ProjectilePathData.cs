using UnityEngine;

namespace BaseClasses
{
    /// <summary>
    /// 투사체 경로의 파라미터를 저장하는 클래스
    /// </summary>
    [System.Serializable]
    public class ProjectilePathData
    {
        // ===== Parabolic Arc Parameters =====
        /// <summary>
        /// 포물선의 최고 높이
        /// </summary>
        [Tooltip("포물선의 최고 높이")]
        public float arcHeight = 3f;
        
        // ===== Bezier Curve Parameters =====
        /// <summary>
        /// 베지어 곡선의 제어점 (자동 계산 시 사용)
        /// </summary>
        [Tooltip("베지어 곡선의 수직 오프셋 (자동 제어점 계산용)")]
        public float arcOffset = 4f;
        
        /// <summary>
        /// 베지어 곡선의 수동 제어점 (null이면 자동 계산)
        /// </summary>
        [Tooltip("베지어 곡선의 수동 제어점 (비워두면 자동 계산)")]
        public Vector3? bezierControlPoint = null;
        
        // ===== Multi-Point Parameters =====
        /// <summary>
        /// 경유할 지점들의 배열
        /// </summary>
        [Tooltip("경유할 지점들")]
        public Vector3[] waypoints;
        
        /// <summary>
        /// 각 세그먼트에 적용할 아크 높이
        /// </summary>
        [Tooltip("각 경유 세그먼트의 아크 높이")]
        public float segmentArcHeight = 2f;
        
        // ===== Delayed Drop Parameters =====
        /// <summary>
        /// 상승할 높이
        /// </summary>
        [Tooltip("상승할 높이")]
        public float riseHeight = 10f;
        
        /// <summary>
        /// 정점에서 머무는 시간 비율 (0~1)
        /// </summary>
        [Tooltip("정점에서 머무는 시간 비율 (0~1)")]
        public float hangTime = 0.2f;
        
        // ===== Spiral Parameters =====
        /// <summary>
        /// 나선의 반지름
        /// </summary>
        [Tooltip("나선의 반지름")]
        public float spiralRadius = 1f;
        
        /// <summary>
        /// 나선의 회전 속도 (초당 회전수)
        /// </summary>
        [Tooltip("나선의 회전 속도 (초당 회전수)")]
        public float spiralSpeed = 2f;
        
        /// <summary>
        /// 총 회전 수
        /// </summary>
        [Tooltip("총 회전 수")]
        public float totalRotations = 3f;
        
        // ===== Wave Parameters =====
        /// <summary>
        /// 웨이브의 진폭
        /// </summary>
        [Tooltip("웨이브의 진폭")]
        public float waveAmplitude = 1f;
        
        /// <summary>
        /// 웨이브의 주파수
        /// </summary>
        [Tooltip("웨이브의 주파수")]
        public float waveFrequency = 2f;
        
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public ProjectilePathData()
        {
        }
        
        /// <summary>
        /// 포물선용 생성자
        /// </summary>
        public static ProjectilePathData CreateParabolic(float arcHeight)
        {
            return new ProjectilePathData { arcHeight = arcHeight };
        }
        
        /// <summary>
        /// 베지어용 생성자
        /// </summary>
        public static ProjectilePathData CreateBezier(float arcOffset)
        {
            return new ProjectilePathData { arcOffset = arcOffset };
        }
        
        /// <summary>
        /// 다중 경유용 생성자
        /// </summary>
        public static ProjectilePathData CreateMultiPoint(Vector3[] waypoints, float segmentArcHeight = 2f)
        {
            return new ProjectilePathData { waypoints = waypoints, segmentArcHeight = segmentArcHeight };
        }
        
        /// <summary>
        /// 지연 낙하용 생성자
        /// </summary>
        public static ProjectilePathData CreateDelayedDrop(float riseHeight, float hangTime = 0.2f)
        {
            return new ProjectilePathData { riseHeight = riseHeight, hangTime = hangTime };
        }
        
        /// <summary>
        /// 나선형용 생성자
        /// </summary>
        public static ProjectilePathData CreateSpiral(float radius, float speed, float rotations)
        {
            return new ProjectilePathData { spiralRadius = radius, spiralSpeed = speed, totalRotations = rotations };
        }
        
        /// <summary>
        /// 웨이브용 생성자
        /// </summary>
        public static ProjectilePathData CreateWave(float amplitude, float frequency)
        {
            return new ProjectilePathData { waveAmplitude = amplitude, waveFrequency = frequency };
        }
    }
}

