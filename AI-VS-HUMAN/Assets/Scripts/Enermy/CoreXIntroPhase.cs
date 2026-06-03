using System.Collections;
using UnityEngine;

public class CoreXIntroPhase : MonoBehaviour
{
    [Header("인트로")]
    public float startDelay = 1f;

    public IEnumerator Run(CoreXBoss boss)
    {
        if (boss == null)
            yield break;

        boss.SetInvincible(true);

        yield return new WaitForSeconds(Mathf.Max(0f, startDelay));

        if (!boss.ActivatePhase1Servers())
            yield break;

        yield return new WaitUntil(() => boss.ServersAlive <= 0 || boss.IsDead);
    }
}
