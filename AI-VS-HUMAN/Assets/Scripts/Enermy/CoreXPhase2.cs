using System.Collections;
using UnityEngine;

public class CoreXPhase2 : MonoBehaviour
{
    public IEnumerator Run(CoreXBoss boss)
    {
        if (boss == null)
            yield break;

        while (!boss.IsDead && boss.CurrentHp > 0f)
        {
            yield return StartCoroutine(boss.DashPattern.Run(boss));
            yield return new WaitForSeconds(boss.DashPattern.Cooldown);
        }

        if (!boss.IsDead)
            boss.StartDeath();
    }
}
