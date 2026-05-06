// 플레이어가 대시 중일 때 주변 적을 감지해서 밀어내는 스크립트
// PlayerMove가 대시 시작/종료 시 IsDashing 값을 바꾸면 이 스크립트가 넉백을 한 번 적용한다.
using UnityEngine;

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
        // 대시 한 번에 여러 번 밀리지 않도록 첫 감지 후 knockbackApplied를 잠근다.
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

        // 넉백 중에는 중력을 잠시 끄고, 시간이 지나면 원래 중력값으로 돌린다.
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
