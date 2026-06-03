using System.Collections;
using UnityEngine;

public class CoreXDashPattern : MonoBehaviour
{
    private const float GroggyDuration = 1f;
    private const float OffscreenPadding = 2f;
    private const float HorizontalDashHeightOffset = 0f;
    private const float DashPause = 0.25f;
    private const float HitRadius = 1f;

    [Header("돌진")]
    public float windupTime = 0.8f;
    public float speed = 18f;
    public float cooldown = 1.5f;
    public int damage = 1;

    [Header("화면 돌진")]
    public int horizontalDashCount = 3;
    public float curveTowardPlayerStrength = 2.5f;

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

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        Bounds dashBounds = GetDashBounds(boss);
        Vector3 firstExitPosition = GetInitialExitPosition(boss, dashBounds);
        yield return StartCoroutine(MoveBossAlongDash(boss, firstExitPosition));

        int dashCount = Mathf.Max(1, horizontalDashCount);
        for (int i = 0; i < dashCount && !boss.IsDead; i++)
        {
            float startSide = i % 2 == 0 ? -1f : 1f;
            Vector3 startPosition = GetHorizontalDashStartPosition(boss, dashBounds, startSide);
            Vector3 endPosition = GetHorizontalDashStartPosition(boss, dashBounds, -startSide);

            boss.transform.position = startPosition;
            Physics2D.SyncTransforms();

            yield return new WaitForSeconds(Mathf.Max(0f, DashPause));
            yield return StartCoroutine(MoveBossAlongDash(boss, endPosition, true, dashBounds));
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = Color.gray;

            yield return new WaitForSeconds(GroggyDuration);

        if (spriteRenderer != null)
            spriteRenderer.color = boss.OriginalColor;
    }

    private IEnumerator MoveBossAlongDash(CoreXBoss boss, Vector3 targetPosition, bool curveTowardPlayer = false, Bounds dashBounds = default)
    {
        LayerMask playerMask = LayerMask.GetMask("Player");
        bool hitPlayer = false;

        while (!boss.IsDead && Vector2.Distance(boss.transform.position, targetPosition) > 0.05f)
        {
            if (curveTowardPlayer && boss.Player != null)
            {
                float wantedY = Mathf.Clamp(
                    boss.Player.position.y + HorizontalDashHeightOffset,
                    dashBounds.min.y + OffscreenPadding,
                    dashBounds.max.y - OffscreenPadding);

                targetPosition.y = Mathf.MoveTowards(
                    targetPosition.y,
                    wantedY,
                    Mathf.Max(0f, curveTowardPlayerStrength) * Time.deltaTime);
            }

            boss.transform.position = Vector3.MoveTowards(
                boss.transform.position,
                targetPosition,
                Mathf.Max(0.1f, speed) * Time.deltaTime);

            Physics2D.SyncTransforms();

            if (!hitPlayer)
            {
                Collider2D hit = Physics2D.OverlapCircle(boss.transform.position, HitRadius, playerMask);
                PlayerHealth playerHealth = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                    hitPlayer = true;
                }
            }

            yield return null;
        }

        boss.transform.position = targetPosition;
        Physics2D.SyncTransforms();
    }

    private Bounds GetDashBounds(CoreXBoss boss)
    {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 center = cam.transform.position;
            return new Bounds(
                new Vector3(center.x, center.y, boss.transform.position.z),
                new Vector3(halfWidth * 2f, halfHeight * 2f, 0f));
        }

        if (boss.BossRoom != null)
            return boss.BossRoom.GetBounds();

        return new Bounds(boss.transform.position, new Vector3(32f, 18f, 0f));
    }

    private Vector3 GetInitialExitPosition(CoreXBoss boss, Bounds bounds)
    {
        Vector2 toPlayer = ((Vector2)boss.Player.position - (Vector2)boss.transform.position).normalized;
        if (toPlayer.sqrMagnitude < 0.001f)
            toPlayer = Vector2.right;

        float targetX = toPlayer.x >= 0f ? bounds.max.x + OffscreenPadding : bounds.min.x - OffscreenPadding;
        float targetY = toPlayer.y >= 0f ? bounds.max.y + OffscreenPadding : bounds.min.y - OffscreenPadding;

        return new Vector3(targetX, targetY, boss.transform.position.z);
    }

    private Vector3 GetHorizontalDashStartPosition(CoreXBoss boss, Bounds bounds, float side)
    {
        float x = side < 0f ? bounds.min.x - OffscreenPadding : bounds.max.x + OffscreenPadding;
        float y = Mathf.Clamp(
            boss.Player.position.y + HorizontalDashHeightOffset,
            bounds.min.y + OffscreenPadding,
            bounds.max.y - OffscreenPadding);

        return new Vector3(x, y, boss.transform.position.z);
    }
}
