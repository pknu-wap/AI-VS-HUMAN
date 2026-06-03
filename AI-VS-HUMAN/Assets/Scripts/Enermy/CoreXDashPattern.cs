using System.Collections;
using UnityEngine;

public class CoreXDashPattern : MonoBehaviour
{
    [Header("Dash")]
    public float windupTime = 0.8f;
    public float moveTime = 0.3f;
    public float speed = 15f;
    public float cooldown = 1.5f;
    public float groggyDuration = 1f;
    public int damage = 1;

    public float Cooldown => cooldown;

    public IEnumerator Run(CoreXBoss boss)
    {
        if (boss == null || boss.Player == null)
            yield break;

        SpriteRenderer spriteRenderer = boss.SpriteRenderer;
        Rigidbody2D rb = boss.Rigidbody;

        if (spriteRenderer != null)
            spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(windupTime);
        if (boss.IsDead)
            yield break;

        if (spriteRenderer != null)
            spriteRenderer.color = Color.red;

        Vector2 dir = ((Vector2)boss.Player.position - (Vector2)boss.transform.position).normalized;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearVelocity = dir * speed;
        }

        float elapsed = 0f;
        bool hitPlayer = false;
        LayerMask playerMask = LayerMask.GetMask("Player");

        while (elapsed < moveTime && !boss.IsDead && !hitPlayer)
        {
            elapsed += Time.deltaTime;

            Collider2D hit = Physics2D.OverlapCircle(boss.transform.position, 1f, playerMask);
            if (hit != null)
            {
                PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(damage);

                hitPlayer = true;
            }

            yield return null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = Color.gray;

        yield return new WaitForSeconds(groggyDuration);

        if (spriteRenderer != null)
            spriteRenderer.color = boss.OriginalColor;
    }
}
