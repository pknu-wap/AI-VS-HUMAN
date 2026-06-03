using System.Collections;
using UnityEngine;

public class CoreXPhaseTransition : MonoBehaviour
{
    [Header("2페이즈")]
    public float phase2MaxHp = 1000f;
    public Color phase2Color = new Color(0.3f, 0f, 0.5f, 1f);
    public float transitionTime = 2f;
    public float hpFillDuration = 2f;

    public IEnumerator Run(CoreXBoss boss)
    {
        if (boss == null)
            yield break;

        boss.SetInvincible(true);

        float step = Mathf.Max(0.01f, transitionTime / 8f);
        for (int i = 0; i < 8; i++)
        {
            boss.SetSpriteColor(i % 2 == 0 ? Color.white : phase2Color);
            yield return new WaitForSeconds(step);
        }

        boss.SetOriginalColor(phase2Color);
        boss.SetSpriteColor(phase2Color);

        boss.SetMaxHp(phase2MaxHp);
        boss.SetHp(0f);

        bool hasPhase2Servers = boss.ActivatePhase2Servers();
        boss.StartPhase2PressurePatterns();

        if (hasPhase2Servers)
            StartCoroutine(FillHpWhileServersAlive(boss));

        if (hasPhase2Servers)
            yield return new WaitUntil(() => boss.ServersAlive <= 0 || boss.IsDead);

        if (boss.IsDead)
        {
            boss.StopPhase2Patterns();
            yield break;
        }

        boss.SetHp(boss.MaxHp);
        boss.SetInvincible(false);
    }

    private IEnumerator FillHpWhileServersAlive(CoreXBoss boss)
    {
        while (boss.ServersAlive > 0 && !boss.IsDead)
        {
            float fillSpeed = boss.MaxHp / Mathf.Max(0.01f, hpFillDuration);
            boss.SetHp(Mathf.MoveTowards(boss.CurrentHp, boss.MaxHp, fillSpeed * Time.deltaTime));
            yield return null;
        }
    }
}
