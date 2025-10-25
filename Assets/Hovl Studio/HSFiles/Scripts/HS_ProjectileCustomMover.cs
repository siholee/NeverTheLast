using System.Collections;
using System.Collections.Generic;
using Effects;
using Entities;
using BaseClasses;
using UnityEngine;

public class HS_ProjectileCustomMover : MonoBehaviour
{
    [SerializeField] protected Unit unitFrom;
    [SerializeField] protected Unit unitTo;
    [SerializeField] protected float duration;
    [SerializeField] protected float timer;
    [SerializeField] protected Vector3 rotationOffset = new Vector3(0, 0, 0);
    [SerializeField] protected GameObject hit;
    [SerializeField] protected ParticleSystem hitPS;
    [SerializeField] protected GameObject flash;
    [SerializeField] protected Light lightSourse;
    [SerializeField] protected GameObject[] Detached;
    [SerializeField] protected ParticleSystem projectilePS;
    private bool startChecker = false;
    [SerializeField] protected bool notDestroy = false;
    protected bool isAlive = true;
    
    // 새로운 경로 시스템 필드
    [Header("Projectile Path Settings")]
    [SerializeField] protected ProjectilePathType pathType = ProjectilePathType.Linear;
    [SerializeField] protected ProjectilePathData pathData = new ProjectilePathData();
    
    // 경로 계산용 캐시 변수
    protected Vector3 startPosition;
    protected Vector3 endPosition;
    protected Vector3 previousPosition;
    protected Vector3 bezierControlPoint;
    protected Vector3 pathDirection;
    protected Vector3 pathPerpendicular;

    public void SetProjectileInfo(Unit from, Unit to, float duration)
    {
        unitFrom = from;
        unitTo = to;
        this.duration = duration;
        
        // 경로 계산을 위한 초기 위치 저장
        InitializePathData();
    }
    
    public void SetProjectileInfo(Unit from, Unit to, float duration, ProjectilePathType pathType, ProjectilePathData pathData = null)
    {
        unitFrom = from;
        unitTo = to;
        this.duration = duration;
        this.pathType = pathType;
        if (pathData != null)
            this.pathData = pathData;
        
        // 경로 계산을 위한 초기 위치 저장
        InitializePathData();
    }
    
    protected void InitializePathData()
    {
        if (unitFrom == null || unitTo == null) return;
        
        startPosition = unitFrom.transform.position;
        endPosition = unitTo.transform.position;
        previousPosition = startPosition;
        
        // 경로 방향 및 수직 벡터 계산
        pathDirection = (endPosition - startPosition).normalized;
        pathPerpendicular = Vector3.Cross(pathDirection, Vector3.up).normalized;
        if (pathPerpendicular == Vector3.zero)
            pathPerpendicular = Vector3.Cross(pathDirection, Vector3.forward).normalized;
        
        // 베지어 제어점 계산
        if (pathType == ProjectilePathType.BezierCurve)
        {
            if (pathData.bezierControlPoint.HasValue)
            {
                bezierControlPoint = pathData.bezierControlPoint.Value;
            }
            else
            {
                Vector3 midPoint = (startPosition + endPosition) / 2f;
                bezierControlPoint = midPoint + Vector3.up * pathData.arcOffset;
            }
        }
    }

    protected virtual void Start()
    {
        if (!startChecker)
        {
            /*lightSourse = GetComponent<Light>();
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            if (hit != null)
                hitPS = hit.GetComponent<ParticleSystem>();*/
            if (flash != null)
            {
                flash.transform.parent = null;
            }
        }
        // if (notDestroy)
        //     StartCoroutine(DisableTimer(5));
        // else
        //     Destroy(gameObject, 5);
        startChecker = true;
        isAlive = true;
    }

    protected virtual IEnumerator DisableTimer(float time)
    {
        yield return new WaitForSeconds(time);
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
        yield break;
    }

    protected virtual void OnEnable()
    {
        if (startChecker)
        {
            if (flash != null)
            {
                flash.transform.parent = null;
            }
            if (lightSourse != null)
                lightSourse.enabled = true;
            // col.enabled = true;
            // rb.constraints = RigidbodyConstraints.None;
            isAlive = true;
            timer = 0f;
            
            // 경로 데이터 재초기화
            InitializePathData();
            
            if (projectilePS != null)
                projectilePS.Play();
        }
    }

    protected virtual void Update()
    {
        if (unitFrom == null || unitTo == null)
        {
            return;
        }
        if (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Min(timer / duration, 1f);
            
            // 이전 위치 저장 (회전 계산용)
            previousPosition = transform.position;
            
            // 경로 타입에 따른 위치 계산
            Vector3 newPosition = CalculatePositionByPathType(t);
            transform.position = newPosition;
            
            // 투사체 회전 업데이트
            UpdateProjectileRotation();
        }
        if (timer >= duration && isAlive)
        {
            isAlive = false;
            OnHitTarget();
        }
    }
    
    /// <summary>
    /// 경로 타입에 따라 현재 위치 계산
    /// </summary>
    protected Vector3 CalculatePositionByPathType(float t)
    {
        switch (pathType)
        {
            case ProjectilePathType.Linear:
                return CalculateLinearPoint(startPosition, endPosition, t);
                
            case ProjectilePathType.ParabolicArc:
                return CalculateParabolicPoint(startPosition, endPosition, t, pathData.arcHeight);
                
            case ProjectilePathType.BezierCurve:
                return CalculateBezierPoint(startPosition, bezierControlPoint, endPosition, t);
                
            case ProjectilePathType.MultiPoint:
                return CalculateMultiPointPath(pathData.waypoints, t);
                
            case ProjectilePathType.DelayedDrop:
                return CalculateDelayedDropPoint(startPosition, endPosition, t, pathData.riseHeight, pathData.hangTime);
                
            case ProjectilePathType.Spiral:
                return CalculateSpiralPoint(startPosition, endPosition, t, pathData.spiralRadius, pathData.spiralSpeed, pathData.totalRotations);
                
            case ProjectilePathType.Wave:
                return CalculateWavePoint(startPosition, endPosition, t, pathData.waveAmplitude, pathData.waveFrequency);
                
            default:
                return CalculateLinearPoint(startPosition, endPosition, t);
        }
    }
    
    // ===== 경로 계산 메서드들 =====
    
    /// <summary>
    /// 직선 경로 계산
    /// </summary>
    protected Vector3 CalculateLinearPoint(Vector3 start, Vector3 end, float t)
    {
        return Vector3.Lerp(start, end, t);
    }
    
    /// <summary>
    /// 포물선 경로 계산
    /// </summary>
    protected Vector3 CalculateParabolicPoint(Vector3 start, Vector3 end, float t, float arcHeight)
    {
        Vector3 linearPoint = Vector3.Lerp(start, end, t);
        // 포물선 방정식: 0에서 시작해서 중간에 최고점, 끝에서 0
        float parabola = 4 * arcHeight * t * (1 - t);
        linearPoint.y += parabola;
        return linearPoint;
    }
    
    /// <summary>
    /// 2차 베지어 곡선 계산 (3개 점)
    /// </summary>
    protected Vector3 CalculateBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        
        return uu * p0 + 2 * u * t * p1 + tt * p2;
    }
    
    /// <summary>
    /// 다중 포인트 경유 경로 계산
    /// </summary>
    protected Vector3 CalculateMultiPointPath(Vector3[] waypoints, float t)
    {
        if (waypoints == null || waypoints.Length < 2)
            return CalculateLinearPoint(startPosition, endPosition, t);
        
        // 전체 경로를 세그먼트로 나눔
        float totalProgress = t * (waypoints.Length - 1);
        int currentSegment = Mathf.FloorToInt(totalProgress);
        
        if (currentSegment >= waypoints.Length - 1)
            return waypoints[waypoints.Length - 1];
        
        float segmentProgress = totalProgress - currentSegment;
        
        // 각 세그먼트는 작은 포물선으로 연결
        return CalculateParabolicPoint(
            waypoints[currentSegment], 
            waypoints[currentSegment + 1], 
            segmentProgress,
            pathData.segmentArcHeight
        );
    }
    
    /// <summary>
    /// 지연 낙하 경로 계산
    /// </summary>
    protected Vector3 CalculateDelayedDropPoint(Vector3 start, Vector3 end, float t, float riseHeight, float hangTime)
    {
        // 3단계: 상승 -> 정지 -> 낙하
        float risePhase = 0.3f;  // 전체 시간의 30%
        float hangPhase = Mathf.Clamp01(hangTime);
        float dropPhase = 1f - risePhase - hangPhase;
        
        if (dropPhase < 0.1f)
        {
            risePhase = 0.3f;
            hangPhase = 0.1f;
            dropPhase = 0.6f;
        }
        
        Vector3 peakPosition = new Vector3(
            Mathf.Lerp(start.x, end.x, 0.5f),
            end.y + riseHeight,
            Mathf.Lerp(start.z, end.z, 0.5f)
        );
        
        if (t < risePhase)
        {
            // 상승 단계
            float riseT = t / risePhase;
            return Vector3.Lerp(start, peakPosition, riseT);
        }
        else if (t < risePhase + hangPhase)
        {
            // 정지 단계
            return peakPosition;
        }
        else
        {
            // 낙하 단계 (가속)
            float dropT = (t - risePhase - hangPhase) / dropPhase;
            dropT = dropT * dropT;  // 제곱으로 가속감 표현
            return Vector3.Lerp(peakPosition, end, dropT);
        }
    }
    
    /// <summary>
    /// 나선형 경로 계산
    /// </summary>
    protected Vector3 CalculateSpiralPoint(Vector3 start, Vector3 end, float t, float radius, float speed, float rotations)
    {
        Vector3 linearPoint = Vector3.Lerp(start, end, t);
        
        // 나선 회전 각도 계산
        float angle = t * rotations * 2f * Mathf.PI;
        
        // 나선 반지름 (시작점에서 점점 작아짐)
        float currentRadius = radius * (1f - t * 0.5f);
        
        // 경로에 수직인 평면에서 원운동
        Vector3 offset = pathPerpendicular * Mathf.Cos(angle) * currentRadius +
                        Vector3.up * Mathf.Sin(angle) * currentRadius;
        
        return linearPoint + offset;
    }
    
    /// <summary>
    /// 웨이브 경로 계산
    /// </summary>
    protected Vector3 CalculateWavePoint(Vector3 start, Vector3 end, float t, float amplitude, float frequency)
    {
        Vector3 linearPoint = Vector3.Lerp(start, end, t);
        
        // 사인파 계산
        float wave = Mathf.Sin(t * Mathf.PI * frequency) * amplitude;
        
        // 경로에 수직 방향으로 웨이브 적용
        return linearPoint + pathPerpendicular * wave;
    }
    
    /// <summary>
    /// 투사체 회전 업데이트 (이동 방향을 바라보도록)
    /// </summary>
    protected void UpdateProjectileRotation()
    {
        Vector3 direction = (transform.position - previousPosition).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    protected virtual void OnHitTarget()
    {
        if (lightSourse != null)
            lightSourse.enabled = false;
        projectilePS.Stop();
        projectilePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Quaternion rot = Quaternion.LookRotation(unitTo.transform.position - unitFrom.transform.position);

        //Spawn hit effect on collision
        if (hit != null)
        {
            hit.transform.rotation = rot;
            hit.transform.position = unitTo.transform.position;
            hitPS.Play();
        }

        //Removing trail from the projectile on cillision enter or smooth removing. Detached elements must have "AutoDestroying script"
        foreach (var detachedPrefab in Detached)
        {
            if (detachedPrefab != null)
            {
                ParticleSystem detachedPS = detachedPrefab.GetComponent<ParticleSystem>();
                detachedPS.Stop();
            }
        }
        if (notDestroy)
            StartCoroutine(DisableTimer(hitPS.main.duration));
        else
        {
            if (hitPS != null)
            {
                Destroy(gameObject, hitPS.main.duration);
            }
            else
                Destroy(gameObject, 1);
        }
    }
}