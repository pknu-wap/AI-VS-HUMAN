// 일반 적들이 공통으로 사용하는 체력, 감지, 피격, 사망 연출을 담당하는 기반 클래스
// SoldierEnemy, ShieldEnemy, DroneEnemy는 이 클래스를 상속해서 개별 공격 패턴만 구현한다.
using UnityEngine;
using System.Collections;

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

    // IDamageable 구현 - AssaultRifle에서 호출한다.
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

    // 사망 처리 - 깜빡임을 중단하고 콜라이더/물리를 끈 뒤 서서히 사라지게 한다.
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

    // 원래 색을 유지한 채 알파만 낮춰서 제거한다.
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

    // 피격 시 잠깐 빨간색으로 깜빡인다.
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
