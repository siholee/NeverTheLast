using UnityEngine;
using System.Collections;

public class Status : MonoBehaviour
{
    public string NAME;
    public string TYPE;
    public float DURATION;
    public int MAX_STACK;
    public int CURRENT_STACK;
    public float STACK_EFFECT;
    public float STACK_TIMER;
    public bool IsInfinite;

    private Unit OWNER;
    private float timer;
    private bool isStackEffectActive;

    void Start()
    {
        if (!IsInfinite)
        {
            StartCoroutine(StatusTimer());
        }
    }

    public void InitializeStatus(Unit owner, string name, string type, float duration, int maxStack, float stackEffect, float stackTimer, bool isInfinite)
    {
        OWNER = owner;
        NAME = name;
        TYPE = type;
        DURATION = duration;
        MAX_STACK = maxStack;
        STACK_EFFECT = stackEffect;
        STACK_TIMER = stackTimer;
        IsInfinite = isInfinite;
        timer = duration;
        CURRENT_STACK = 0;
    }

    IEnumerator StatusTimer()
    {
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        // Remove status effect when the duration ends
        RemoveStatusEffect();
    }

    public void AddStack()
    {
        if (TYPE == "stack")
        {
            if (CURRENT_STACK < MAX_STACK)
            {
                CURRENT_STACK++;
                OWNER.ATK_MULBUFF += STACK_EFFECT; // Increase ATK multiplier
                isStackEffectActive = true;
                StartCoroutine(StackTimer());
            }
        }
    }

    IEnumerator StackTimer()
    {
        float stackTimer = STACK_TIMER;
        while (stackTimer > 0)
        {
            stackTimer -= Time.deltaTime;
            yield return null;
        }

        CURRENT_STACK--;
        OWNER.ATK_MULBUFF -= STACK_EFFECT; // Decrease ATK multiplier
        if (CURRENT_STACK > 0)
        {
            StartCoroutine(StackTimer());
        }
        else
        {
            isStackEffectActive = false;
        }
    }

    public void DestroyStatus()
    {
        // // Remove the status effect from the owner
        // if (TYPE == "normal" || (TYPE == "stack" && CURRENT_STACK > 0))
        // {
        //     OWNER.ATK_MULBUFF -= CURRENT_STACK * STACK_EFFECT; // Reset ATK multiplier
        // }

        // // Remove the status from the owner's STATUS_MANAGER list
        // OWNER.STATUS_MANAGER.Remove(this);

        // // Destroy the status object
        // Destroy(gameObject);
    }

    void RemoveStatusEffect()
    {
        DestroyStatus(); // Use DestroyStatus to clean up and destroy itself
    }
}