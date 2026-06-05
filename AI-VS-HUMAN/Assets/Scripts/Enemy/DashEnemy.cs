using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DashEnemy : EnemyBase
{
    private const float WindupTime = 0.25f;
    private const float GravityScale = 1f;
    private const float DelayedDashWindupTime = 0.75f;
    private const float DelayedDashSpeedMultiplier = 0.55f;
    private const float BounceForce = 8f;
    private const float BounceDuration = 0.18f;
    private static readonly float PlayerKnockbackForce = 5f;
    private const float GroggyDuration = 1.2f;
    private const float WallNormalThreshold = 0.35f;
    private const float WallCheckPadding = 0.08f;

    [Header("돌진")]
    public float dashDistance = 10f;
    public float dashSpeed = 12f;
    public float dashCooldown = 1.5f;

    [Header("시간차 돌진")]
    [Range(0f, 1f)] public float delayedDashChance = 0.3f;

    [Header("피격")]
    public int contactDamage = 1;

    private Rigidbody2D dashRb;
    private Collider2D dashCollider;
    private Coroutine stateCoroutine;
    private Vector2 dashDirection;
    private Vector2 dashStartPosition;
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[6];
    private readonly Collider2D[] playerOverlapHits = new Collider2D[4];
    private float cooldownTimer;
    private float currentDashSpeed;
    private bool isPreparing;
    private bool isDashing;
    private bool isBouncing;
    private bool isGroggy;

    protected override void Start()
    {
        base.Start();

        dashRb = GetComponent<Rigidbody2D>();
        dashCollider = GetComponent<Collider2D>();
        ConfigureRigidbody();
        IgnoreOtherDashEnemyCollisions();

        cooldownTimer = dashCooldown;
    }

    private void Update()
    {
        if (isDead || player == null)
            return;

        FacePlayer();

        if (isDashing)
        {
            float traveledDistance = Mathf.Abs(transform.position.x - dashStartPosition.x);
            if (traveledDistance >= dashDistance)
                StopDashWithoutBounce();

            return;
        }

        if (isPreparing || isBouncing || isGroggy)
            return;

        cooldownTimer += Time.deltaTime;

        if (cooldownTimer >= dashCooldown && IsPlayerInDetectionRange())
            stateCoroutine = StartCoroutine(WindupAndDash(Random.value < delayedDashChance));
    }

    private void FixedUpdate()
    {
        if (!isDashing || dashRb == null)
            return;

        if (TryHitPlayerByOverlap())
            return;

        if (TryFindWallAhead(out Vector2 bounceDirection))
        {
            StopDashWithBounce(bounceDirection, true);
            return;
        }

        dashRb.linearVelocity = new Vector2(dashDirection.x * currentDashSpeed, dashRb.linearVelocity.y);
    }

    private IEnumerator WindupAndDash(bool useDelayedDash)
    {
        isPreparing = true;
        SetBodyColor(useDelayedDash ? new Color(1f, 0.65f, 0.1f) : Color.yellow);

        float prepareTime = useDelayedDash ? DelayedDashWindupTime : WindupTime;
        yield return new WaitForSeconds(Mathf.Max(0f, prepareTime));

        isPreparing = false;

        if (isDead || player == null || !IsPlayerInDetectionRange())
        {
            SetBodyColor(originalColor);
            stateCoroutine = null;
            yield break;
        }

        BeginDash(useDelayedDash);
        stateCoroutine = null;
    }

    private void BeginDash(bool useDelayedDash)
    {
        dashDirection = HorizontalDirectionToPlayer();
        float speedMultiplier = useDelayedDash ? Mathf.Max(0.05f, DelayedDashSpeedMultiplier) : 1f;
        currentDashSpeed = dashSpeed * speedMultiplier;

        dashStartPosition = transform.position;
        cooldownTimer = 0f;
        isDashing = true;
        SetHorizontalLock(false);
        SetBodyColor(Color.red);
    }

    private void StopDashWithoutBounce()
    {
        isDashing = false;

        if (dashRb != null)
            dashRb.linearVelocity = new Vector2(0f, dashRb.linearVelocity.y);

        SetHorizontalLock(true);
        SetBodyColor(originalColor);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleDashCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleDashCollision(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDashing || other == null)
            return;

        PlayerHealth playerHealth = GetPlayerHealth(other);

        if (playerHealth != null)
        {
            HitPlayer(playerHealth);
            return;
        }

        if (IsObstacle(other))
            StopDashWithBounce(-dashDirection, true);
    }

    private void HitPlayer(PlayerHealth playerHealth)
    {
        playerHealth.TakeDamage(contactDamage);
        ApplyPlayerKnockback(playerHealth);

        Vector2 bounceDirection = -dashDirection;
        StopDashWithBounce(bounceDirection, false);
    }

    private void HandleDashCollision(Collision2D collision)
    {
        if (!isDashing || collision == null)
            return;

        Collider2D hitCollider = collision.collider;
        PlayerHealth playerHealth = GetPlayerHealth(hitCollider);

        if (playerHealth != null)
        {
            HitPlayer(playerHealth);
            return;
        }

        if (IsObstacle(hitCollider) && TryGetWallBounceDirection(collision, out Vector2 bounceDirection))
            StopDashWithBounce(bounceDirection, true);
    }

    private void StopDashWithBounce(Vector2 bounceDirection, bool groggyAfterBounce)
    {
        isDashing = false;

        if (stateCoroutine != null)
            StopCoroutine(stateCoroutine);

        stateCoroutine = StartCoroutine(BounceRoutine(NormalizedOrFallback(bounceDirection, -dashDirection), groggyAfterBounce));
    }

    private IEnumerator BounceRoutine(Vector2 bounceDirection, bool groggyAfterBounce)
    {
        isBouncing = true;
        SetHorizontalLock(false);
        SetBodyColor(new Color(1f, 0.45f, 0.2f));

        if (dashRb != null)
            dashRb.linearVelocity = new Vector2(bounceDirection.x * BounceForce, dashRb.linearVelocity.y);

        yield return new WaitForSeconds(Mathf.Max(0f, BounceDuration));

        if (dashRb != null)
            dashRb.linearVelocity = new Vector2(0f, dashRb.linearVelocity.y);

        isBouncing = false;
        SetHorizontalLock(true);

        if (groggyAfterBounce)
            yield return GroggyRoutine();
        else
            SetBodyColor(originalColor);

        cooldownTimer = 0f;
        stateCoroutine = null;
    }

    private IEnumerator GroggyRoutine()
    {
        isGroggy = true;
        SetHorizontalLock(true);
        SetBodyColor(Color.gray);

        yield return new WaitForSeconds(Mathf.Max(0f, GroggyDuration));

        isGroggy = false;
        SetBodyColor(originalColor);
    }

    private void ApplyPlayerKnockback(PlayerHealth playerHealth)
    {
        if (PlayerKnockbackForce <= 0f)
            return;

        Rigidbody2D playerRb = playerHealth.GetComponent<Rigidbody2D>();
        if (playerRb == null)
            return;

        float knockbackX = playerHealth.transform.position.x >= transform.position.x ? 1f : -1f;
        playerRb.linearVelocity = new Vector2(knockbackX * PlayerKnockbackForce, playerRb.linearVelocity.y);
    }

    private PlayerHealth GetPlayerHealth(Collider2D hitCollider)
    {
        return hitCollider != null ? hitCollider.GetComponentInParent<PlayerHealth>() : null;
    }

    private bool TryHitPlayerByOverlap()
    {
        if (dashCollider == null)
            return false;

        Bounds bounds = dashCollider.bounds;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapBox(
            bounds.center,
            bounds.size,
            0f,
            filter,
            playerOverlapHits);

        for (int i = 0; i < hitCount; i++)
        {
            PlayerHealth playerHealth = GetPlayerHealth(playerOverlapHits[i]);
            if (playerHealth == null)
                continue;

            HitPlayer(playerHealth);
            return true;
        }

        return false;
    }

    private bool IsObstacle(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        if (GetPlayerHealth(hitCollider) != null || hitCollider.GetComponentInParent<EnemyBase>() != null)
            return false;

        if (obstacleLayer.value == 0)
            return !hitCollider.isTrigger;

        return (obstacleLayer.value & (1 << hitCollider.gameObject.layer)) != 0;
    }

    private Vector2 NormalizedOrFallback(Vector2 direction, Vector2 fallback)
    {
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.left;
    }

    private Vector2 HorizontalDirectionToPlayer()
    {
        if (player == null)
            return spriteRenderer != null && spriteRenderer.flipX ? Vector2.left : Vector2.right;

        float directionX = player.position.x >= transform.position.x ? 1f : -1f;
        return new Vector2(directionX, 0f);
    }

    private bool TryGetWallBounceDirection(Collision2D collision, out Vector2 bounceDirection)
    {
        bounceDirection = -dashDirection;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) < WallNormalThreshold)
                continue;

            bounceDirection = normal.x >= 0f ? Vector2.right : Vector2.left;
            return true;
        }

        return false;
    }

    private bool TryFindWallAhead(out Vector2 bounceDirection)
    {
        bounceDirection = -dashDirection;

        if (dashCollider == null || dashDirection == Vector2.zero)
            return false;

        Bounds bounds = dashCollider.bounds;
        Vector2 direction = dashDirection.x >= 0f ? Vector2.right : Vector2.left;
        float castDistance = Mathf.Max(WallCheckPadding, Mathf.Abs(currentDashSpeed) * Time.fixedDeltaTime + WallCheckPadding);
        int layerMask = obstacleLayer.value != 0 ? obstacleLayer.value : Physics2D.DefaultRaycastLayers;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layerMask);
        filter.useTriggers = false;

        int hitCount = Physics2D.BoxCast(
            bounds.center,
            bounds.size,
            0f,
            direction,
            filter,
            wallHits,
            castDistance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = wallHits[i];
            if (hit.collider == null || hit.collider == dashCollider || !IsObstacle(hit.collider))
                continue;

            if (Mathf.Abs(hit.normal.x) < WallNormalThreshold)
                continue;

            bounceDirection = hit.normal.x >= 0f ? Vector2.right : Vector2.left;

            return true;
        }

        return false;
    }

    private void ConfigureRigidbody()
    {
        if (dashRb == null)
            return;

        dashRb.bodyType = RigidbodyType2D.Dynamic;
        dashRb.gravityScale = GravityScale;
        dashRb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        dashRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        SetHorizontalLock(true);
    }

    private void IgnoreOtherDashEnemyCollisions()
    {
        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] allColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);

        foreach (Collider2D otherCollider in allColliders)
        {
            if (otherCollider == null || otherCollider.transform.IsChildOf(transform))
                continue;

            if (otherCollider.GetComponentInParent<DashEnemy>() == null
                && otherCollider.GetComponentInParent<DashDroneEnemy>() == null)
                continue;

            foreach (Collider2D ownCollider in ownColliders)
            {
                if (ownCollider != null)
                    Physics2D.IgnoreCollision(ownCollider, otherCollider, true);
            }
        }
    }

    private void SetHorizontalLock(bool locked)
    {
        if (dashRb == null)
            return;

        RigidbodyConstraints2D constraints = RigidbodyConstraints2D.FreezeRotation;
        if (locked)
            constraints |= RigidbodyConstraints2D.FreezePositionX;

        dashRb.constraints = constraints;
    }

    private void FacePlayer()
    {
        if (spriteRenderer == null || player == null)
            return;

        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    private void SetBodyColor(Color color)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }

    protected override void Die()
    {
        if (stateCoroutine != null)
        {
            StopCoroutine(stateCoroutine);
            stateCoroutine = null;
        }

        if (dashRb != null)
            dashRb.linearVelocity = Vector2.zero;

        SetHorizontalLock(false);

        base.Die();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + transform.right * dashDistance);
    }
}
