using UnityEngine;
using System.Collections;

/// <summary>
/// 모든 적의 공통 기반 클래스
/// - 피격 시 빨간색 깜빡임
/// - 사망 시 원래 색 그대로 서서히 투명하게 사라짐
/// </summary>
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("기본 스탯")]
    public float maxHp = 30f;
    protected float currentHp;

    [Header("감지")]
    public float detectionRange = 8f;
    public float attackRange = 6f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("사망 연출")]
    public float fadeDuration = 1.5f;

    protected Transform player;
    protected bool isDead = false;

    protected SpriteRenderer spriteRenderer;
    private Coroutine hitFlashCoroutine;

    protected virtual void Start()
    {
        currentHp = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        // 물리 충돌로 밀리지 않게 Kinematic 설정
        // 단, 중력이 0인 드론 계열은 제외
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null && rb.gravityScale > 0f)
            rb.bodyType = RigidbodyType2D.Kinematic;
    }

    protected bool IsPlayerInDetectionRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) <= detectionRange;
    }

    protected bool IsPlayerInAttackRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) <= attackRange;
    }

    protected Vector2 DirectionToPlayer()
    {
        if (player == null) return Vector2.zero;
        return (player.position - transform.position).normalized;
    }

    /// <summary>IDamageable 구현 - AssaultRifle에서 호출</summary>
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;

        // 살아있을 때만 피격 깜빡임
        if (currentHp > 0)
        {
            // 이전 깜빡임 코루틴 중단 후 다시 시작 (중첩 방지)
            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlash());
        }
        else
        {
            Die();
        }
    }

    /// <summary>
    /// 사망 - 깜빡임 즉시 중단 후 원래 색으로 복귀, 서서히 사라짐
    /// </summary>
    protected virtual void Die()
    {
        isDead = true;

        // 진행 중인 피격 깜빡임 즉시 중단
        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        // 색을 원래 흰색으로 복귀 (빨간 채로 사라지지 않게)
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        // 콜라이더 끄기 (죽은 후 충돌 없게)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Rigidbody2D 완전 정지 (중력/속도 제거)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        StartCoroutine(FadeOutAndDestroy());
    }

    /// <summary>원래 색 그대로 서서히 투명해지며 제거</summary>
    IEnumerator FadeOutAndDestroy()
    {
        if (spriteRenderer == null)
        {
            Destroy(gameObject);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            // r, g, b는 그대로 두고 알파만 낮춤
            Color c = spriteRenderer.color;
            spriteRenderer.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>피격 시 빨간 깜빡임 (살아있을 때만)</summary>
    IEnumerator HitFlash()
    {
        if (spriteRenderer == null) yield break;

        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;

        hitFlashCoroutine = null;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
