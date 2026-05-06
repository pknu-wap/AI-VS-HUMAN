using UnityEngine;
using System.Collections;

public class HealDrone : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    public float maxHp        = 50f;
    public float fadeDuration = 1f;

    [Header("이동")]
    public float hoverAmplitude = 0.4f;  // 상하 부유 크기
    public float hoverFrequency = 1.5f;  // 상하 부유 속도
    public float swayAmplitude  = 1.5f;  // 좌우 흔들기 크기
    public float swayFrequency  = 0.8f;  // 좌우 흔들기 속도
    public float swaySpeed      = 2f;    // 좌우 이동 속도

    private float      currentHp;
    private bool       isDead    = false;
    private BossDrone  boss;
    private Vector3    basePos;
    private float      hoverTime = 0f;
    private float      swayTime  = 0f;
    private SpriteRenderer     sr;
    private Coroutine  hitFlashCoroutine;

    private Color originalColor;

    public void Init(BossDrone bossRef)
    {
        boss          = bossRef;
        basePos       = transform.position;
        currentHp     = maxHp;
        sr            = GetComponent<SpriteRenderer>();
        originalColor = sr != null ? sr.color : Color.white;
    }

    void Awake()
    {
        currentHp = maxHp;
        sr        = GetComponent<SpriteRenderer>();
        originalColor = sr != null ? sr.color : Color.white;
    }

    void Start() { }

    void Update()
    {
        if (isDead) return;

        hoverTime += Time.deltaTime;
        swayTime  += Time.deltaTime;

        // 상하 sin 부유
        float bob  = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;

        // 좌우 sin 흔들기 (swayAmplitude 범위 안에서 왔다갔다)
        float swayX = Mathf.Sin(swayTime * swayFrequency) * swayAmplitude;

        transform.position = new Vector3(
            basePos.x + swayX,
            basePos.y + bob,
            0f
        );
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;

        if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());

        if (currentHp <= 0f)
            StartCoroutine(Die(false));
    }

    public void OnTimerExpired()
    {
        if (isDead) return;
        StartCoroutine(Die(true));
    }

    IEnumerator HitFlash()
    {
        if (sr == null) yield break;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!isDead) sr.color = originalColor;
        hitFlashCoroutine = null;
    }

    IEnumerator Die(bool healBoss)
    {
        isDead = true;

        if (boss != null)
            boss.OnHealDroneDestroyed(healBoss);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }

        Destroy(gameObject);
    }
}
