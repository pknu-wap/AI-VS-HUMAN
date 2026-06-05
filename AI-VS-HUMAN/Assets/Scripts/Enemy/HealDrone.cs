// 蹂댁뒪 ?뚮났 ?⑦꽩?먯꽌 ?붾㈃ 諛뽰뿉???좎븘? 蹂댁뒪?먭쾶 遺숇뒗 誘몃땲 ?쒕줎???대떦?섎뒗 ?ㅽ겕由쏀듃
// 蹂댁뒪?먭쾶 ?욧린 ?꾩뿉 ?뚭눼?섎㈃ ?뚮났?섏? ?딄퀬, 蹂댁뒪?먭쾶 遺숈쑝硫?怨꾩냽 ?뚮났?쒗궎?ㅺ? 泥대젰???ㅽ븯硫??щ씪吏꾨떎.
using UnityEngine;
using System.Collections;

public class HealDrone : MonoBehaviour, IDamageable
{
    private const float FadeDuration = 0.6f;
    private const float HoverAmplitude = 0.25f;
    private const float HoverFrequency = 6f;
    private const float AttachDistance = 0.45f;

    [Header("체력")]
    public float maxHp = 50f;
    public float lifeDrainPerSecond = 10f;

    [Header("이동")]
    public float moveSpeed = 6f;

    private float currentHp;
    private float side = 1f;
    private float hoverTime;
    private bool isDead;
    private bool isAttached;
    private GiantDrone boss;
    private SpriteRenderer spriteRenderer;
    private Collider2D droneCollider;
    private Rigidbody2D rb;
    private Coroutine hitFlashCoroutine;
    private Color originalColor = Color.white;

    public void Init(GiantDrone bossRef, float spawnSide)
    {
        // 蹂댁뒪 湲곗? 醫뚯슦 ?대뒓 履쎌뿉 遺숈쓣吏 ??ν븯怨? ?ъ냼???뚮쭏???고????곹깭瑜?珥덇린?뷀븳??
        boss = bossRef;
        side = Mathf.Sign(spawnSide);
        if (Mathf.Approximately(side, 0f))
            side = 1f;

        currentHp = maxHp;
        hoverTime = 0f;
        isDead = false;
        isAttached = false;

        CacheComponents();
        ConfigurePhysics();
    }

    private void Awake()
    {
        CacheComponents();
        ConfigurePhysics();
        currentHp = maxHp;
    }

    private void Update()
    {
        if (isDead || boss == null)
            return;

        if (isAttached)
        {
            transform.position = boss.GetHealDroneAttachPosition(side);
            boss.HealFromAttachedDrone(Time.deltaTime);
            DrainLifeOverTime();
            return;
        }

        hoverTime += Time.deltaTime;
        Vector3 targetPosition = boss.GetHealDroneAttachPosition(side);
        Vector3 bobOffset = Vector3.up * (Mathf.Sin(hoverTime * HoverFrequency) * HoverAmplitude);
        Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPosition + bobOffset, moveSpeed * Time.deltaTime);
        transform.position = nextPosition;

        if (spriteRenderer != null)
            spriteRenderer.flipX = side < 0f;

        if (Vector2.Distance(transform.position, targetPosition) <= AttachDistance)
            AttachToBoss();
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHp -= damage;

        if (currentHp <= 0f)
        {
            StartCoroutine(Die(true));
            return;
        }

        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());
    }

    private void AttachToBoss()
    {
        // 蹂댁뒪?먭쾶 遺숈? ?ㅼ뿉??肄쒕씪?대뜑瑜??좎??댁꽌 ?뚮젅?댁뼱 珥앹븣濡??뚭눼?????덇쾶 ?쒕떎.
        if (isDead || isAttached)
            return;

        isAttached = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        transform.position = boss.GetHealDroneAttachPosition(side);
    }

    private void DrainLifeOverTime()
    {
        // 蹂댁뒪??遺숈뼱 ?뚮났?섎뒗 ?숈븞 ?쒕줎 ?먯껜??泥대젰???쒖꽌??以꾩뼱, ?뚮젅?댁뼱媛 ?섏? ?딆븘??寃곌뎅 ?щ씪吏꾨떎.
        currentHp -= lifeDrainPerSecond * Time.deltaTime;
        if (currentHp <= 0f)
            StartCoroutine(Die(true));
    }

    private IEnumerator HitFlash()
    {
        if (spriteRenderer == null)
            yield break;

        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);

        if (!isDead && spriteRenderer != null)
            spriteRenderer.color = originalColor;

        hitFlashCoroutine = null;
    }

    private IEnumerator Die(bool notifyBoss)
    {
        isDead = true;

        if (notifyBoss && boss != null)
            boss.OnHealDroneDestroyed();

        if (droneCollider != null)
            droneCollider.enabled = false;

        Color startColor = spriteRenderer != null ? spriteRenderer.color : originalColor;
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / FadeDuration);
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        if (droneCollider == null)
            droneCollider = GetComponent<Collider2D>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }

    private void ConfigurePhysics()
    {
        // Transform 湲곕컲 異붿쟻 ?대룞???곕?濡?臾쇰━ ?붿쭊???쒕줎???꾨옒濡??⑥뼱?⑤━嫄곕굹 踰쎌뿉 諛?대꽔吏 ?딄쾶 怨좎젙?쒕떎.
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (droneCollider != null)
            droneCollider.isTrigger = true;
    }
}
