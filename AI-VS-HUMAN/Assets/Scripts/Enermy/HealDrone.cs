// 보스 회복 패턴에서 화면 밖에서 날아와 보스에게 붙는 미니 드론을 담당하는 스크립트
// 보스에게 닿기 전에 파괴되면 회복하지 않고, 보스에게 붙으면 계속 회복시키다가 체력이 다하면 사라진다.
using UnityEngine;
using System.Collections;

public class HealDrone : MonoBehaviour, IDamageable
{
    [Header("체력")]
    public float maxHp = 50f;
    public float fadeDuration = 0.6f;
    public float lifeDrainPerSecond = 10f;

    [Header("이동")]
    public float moveSpeed = 6f;
    public float hoverAmplitude = 0.25f;
    public float hoverFrequency = 6f;
    public float attachDistance = 0.45f;

    private float currentHp;
    private float side = 1f;
    private float hoverTime;
    private bool isDead;
    private bool isAttached;
    private BossDrone boss;
    private SpriteRenderer spriteRenderer;
    private Collider2D droneCollider;
    private Rigidbody2D rb;
    private Coroutine hitFlashCoroutine;
    private Color originalColor = Color.white;

    public void Init(BossDrone bossRef, float spawnSide)
    {
        // 보스 기준 좌우 어느 쪽에 붙을지 저장하고, 재소환 때마다 런타임 상태를 초기화한다.
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
        Vector3 bobOffset = Vector3.up * (Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude);
        Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPosition + bobOffset, moveSpeed * Time.deltaTime);
        transform.position = nextPosition;

        if (spriteRenderer != null)
            spriteRenderer.flipX = side < 0f;

        if (Vector2.Distance(transform.position, targetPosition) <= attachDistance)
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
        // 보스에게 붙은 뒤에도 콜라이더를 유지해서 플레이어 총알로 파괴할 수 있게 한다.
        if (isDead || isAttached)
            return;

        isAttached = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        transform.position = boss.GetHealDroneAttachPosition(side);
    }

    private void DrainLifeOverTime()
    {
        // 보스에 붙어 회복하는 동안 드론 자체의 체력이 서서히 줄어, 플레이어가 쏘지 않아도 결국 사라진다.
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
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / fadeDuration);
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
        // Transform 기반 추적 이동을 쓰므로 물리 엔진이 드론을 아래로 떨어뜨리거나 벽에 밀어넣지 않게 고정한다.
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
