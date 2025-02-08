using System.Collections;
using System.Collections.Generic;
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

    public void SetProjectileInfo(Unit from, Unit to, float duration)
    {
        unitFrom = from;
        unitTo = to;
        this.duration = duration;
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
            if (unitFrom != null && unitTo != null)
            {
                transform.position = Vector3.Lerp(unitFrom.transform.position, unitTo.transform.position, Mathf.Min(timer / duration, 1f));
            }
        }
        if (timer >= duration && isAlive)
        {
            isAlive = false;
            OnHitTarget();
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