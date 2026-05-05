using UnityEngine;

/// <summary>
/// 대쉬 중에만 주변 적을 밀어냄
/// - 평소엔 적과 그냥 통과
/// - 대쉬 중일 때 IsDashing = true 로 설정해주면 OverlapCircle로 감지해서 밀어냄
/// 
/// [사용법]
/// 대쉬 스크립트에서:
///   GetComponent<PlayerDashKnockback>().IsDashing = true;  // 대쉬 시작
///   GetComponent<PlayerDashKnockback>().IsDashing = false; // 대쉬 끝
/// </summary>
public class PlayerDashKnockback : MonoBehaviour
{
    [Header("넉백 설정")]
    public float knockbackForce = 10f;      // 밀려나는 힘
    public float knockbackDuration = 0.3f;  // 밀려나는 시간
    public float detectRadius = 1f;         // 감지 반경 (플레이어 크기에 맞게 조절)
    public LayerMask enemyLayer;            // Enemy 레이어

    public bool IsDashing { get; set; } = false;

    private bool knockbackApplied = false;  // 한 번만 밀어내게

    void Update()
    {
        if (!IsDashing)
        {
            knockbackApplied = false;
            return;
        }

        // 대쉬 중 - 주변 적 감지
        if (!knockbackApplied)
            DetectAndKnockback();
    }

    void DetectAndKnockback()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            detectRadius,
            enemyLayer
        );

        if (hits.Length == 0) return;

        knockbackApplied = true;

        foreach (Collider2D hit in hits)
        {
            Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            Vector2 dir = (hit.transform.position - transform.position).normalized;
            StartCoroutine(ApplyKnockback(rb, dir));
        }
    }

    System.Collections.IEnumerator ApplyKnockback(Rigidbody2D rb, Vector2 dir)
    {
        if (rb == null) yield break;

        Vector2 originalVelocity = rb.linearVelocity;
        float originalGravity    = rb.gravityScale;

        rb.gravityScale = 0f;
        rb.linearVelocity     = dir * knockbackForce;

        yield return new WaitForSeconds(knockbackDuration);

        if (rb != null)
        {
            rb.linearVelocity     = Vector2.zero;
            rb.gravityScale = originalGravity;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
