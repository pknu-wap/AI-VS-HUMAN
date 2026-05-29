using UnityEngine;
using System.Collections;

/// <summary>
/// 보스 1페이즈 서버 오브젝트
/// - 파괴되면 보스에게 알림
/// </summary>
public class ServerNode : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    public float maxHp        = 30f;
    public float fadeDuration = 0.5f;

    private float          currentHp;
    private bool           isDead = false;
    private SpriteRenderer sr;
    private Coroutine      hitFlashCoroutine;
    private Color          originalColor;

    // 파괴 시 호출할 보스 참조
    private CoreXBoss boss;

    public void Init(CoreXBoss bossRef)
    {
        boss = bossRef;
    }

    void Start()
    {
        currentHp     = maxHp;
        sr            = GetComponent<SpriteRenderer>();
        originalColor = sr != null ? sr.color : Color.white;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;

        if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());

        if (currentHp <= 0f) StartCoroutine(Die());
    }

    IEnumerator HitFlash()
    {
        if (sr == null) yield break;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!isDead) sr.color = originalColor;
        hitFlashCoroutine = null;
    }

    IEnumerator Die()
    {
        isDead = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 보스에게 서버 파괴 알림
        if (boss != null) boss.OnServerDestroyed();

        // 페이드 아웃
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (sr != null)
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b,
                                     Mathf.Lerp(1f, 0f, elapsed / fadeDuration));
            yield return null;
        }

        Destroy(gameObject);
    }
}
