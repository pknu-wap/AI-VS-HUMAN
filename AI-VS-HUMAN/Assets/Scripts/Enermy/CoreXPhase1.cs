using System.Collections;
using UnityEngine;

public class CoreXPhase1 : MonoBehaviour
{
    [Header("Wake Up")]
    public float wakeUpDuration = 1.5f;
    public float flashInterval = 0.15f;

    public IEnumerator Run(CoreXBoss boss)
    {
        if (boss == null)
            yield break;

        yield return StartCoroutine(WakeUpEffect(boss));

        if (boss.HpBar != null)
            boss.HpBar.Show();

        boss.SetInvincible(false);

        BossSafeZonePattern safeZone = boss.GetComponent<BossSafeZonePattern>();
        if (safeZone != null)
            safeZone.enabled = true;

        yield return new WaitUntil(() => boss.CurrentHp <= 0f || boss.IsDead);

        if (safeZone != null)
            safeZone.enabled = false;
    }

    private IEnumerator WakeUpEffect(CoreXBoss boss)
    {
        boss.SetInvincible(true);

        float interval = Mathf.Max(0.01f, flashInterval);
        for (float t = 0f; t < wakeUpDuration; t += interval)
        {
            Color color = t % (interval * 2f) < interval ? Color.white : boss.OriginalColor;
            boss.SetSpriteColor(color);
            yield return new WaitForSeconds(interval);
        }

        boss.SetSpriteColor(boss.OriginalColor);
    }
}
