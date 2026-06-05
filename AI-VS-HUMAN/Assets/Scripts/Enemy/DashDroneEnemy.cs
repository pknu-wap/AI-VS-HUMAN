using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DashDroneEnemy : EnemyBase
{
    private const float WindupTime = 0.3f;
    private const float ObstacleCheckPadding = 0.08f;
    private const float BounceForce = 7f;
    private const float BounceDuration = 0.18f;
    private static readonly float PlayerKnockbackForce = 6f;
    private const float GroggyDuration = 1f;

    [Header("돌진")]
    public float dashDistance = 8f;
    public float dashSpeed = 10f;
    public float dashCooldown = 2f;

    [Header("피격")]
    public int contactDamage = 1;

    private Rigidbody2D droneRb;
    private Collider2D droneCollider;
    private Coroutine stateCoroutine;
    private Vector2 dashDirection;
    private Vector2 dashStartPosition;
    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[6];
    private readonly Collider2D[] playerOverlapHits = new Collider2D[4];
    private float cooldownTimer;
    private bool isPreparing;
    private bool isDashing;
    private bool isBouncing;
    private bool isGroggy;

    protected override void Start()
    {
        base.Start();

        droneRb = GetComponent<Rigidbody2D>();
        droneCollider = GetComponent<Collider2D>();
        ConfigureRigidbody();
        IgnoreOtherDashEnemyCollisions();

        cooldownTimer = dashCooldown;
    }

    private void Update()
    {
        if (isDead || player == null)
            return;

        KeepUpright();
        FacePlayer();

        if (isDashing)
        {
            float traveledDistance = Vector2.Distance(dashStartPosition, transform.position);
            if (traveledDistance >= dashDistance)
                StopDashWithoutBounce();

            return;
        }

        if (isPreparing || isBouncing || isGroggy)
            return;

        cooldownTimer += Time.deltaTime;

        if (cooldownTimer >= dashCooldown && IsPlayerInDetectionRange())
            stateCoroutine = StartCoroutine(WindupAndDash());
    }

    private void FixedUpdate()
    {
        if (!isDashing || droneRb == null)
            return;

        if (TryHitPlayerByOverlap())
            return;

        if (TryFindObstacleAhead(out Vector2 bounceDirection))
        {
            StopDashWithBounce(bounceDirection, true);
            return;
        }

        droneRb.linearVelocity = dashDirection * dashSpeed;
    }

    private IEnumerator WindupAndDash()
    {
        isPreparing = true;
        SetPositionLock(true);
        SetBodyColor(Color.yellow);

        yield return new WaitForSeconds(Mathf.Max(0f, WindupTime));

        isPreparing = false;

        if (isDead || player == null || !IsPlayerInDetectionRange())
        {
            SetBodyColor(originalColor);
            stateCoroutine = null;
            yield break;
        }

        BeginDash();
        stateCoroutine = null;
    }

    private void BeginDash()
    {
        dashDirection = DirectionToPlayer();
        if (dashDirection == Vector2.zero)
            dashDirection = spriteRenderer != null && spriteRenderer.flipX ? Vector2.left : Vector2.right;

        dashStartPosition = transform.position;
        cooldownTimer = 0f;
        isDashing = true;
        SetPositionLock(false);
        SetBodyColor(Color.red);
    }

    private void StopDashWithoutBounce()
    {
        isDashing = false;

        if (droneRb != null)
            droneRb.linearVelocity = Vector2.zero;

        SetPositionLock(true);
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

        if (IsObstacle(hitCollider))
            StopDashWithBounce(GetCollisionBounceDirection(collision), true);
    }

    private void HitPlayer(PlayerHealth playerHealth)
    {
        playerHealth.TakeDamage(contactDamage);
        ApplyPlayerKnockback(playerHealth);
        StopDashWithBounce(-dashDirection, false);
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
        SetPositionLock(false);
        SetBodyColor(new Color(1f, 0.45f, 0.2f));

        if (droneRb != null)
            droneRb.linearVelocity = bounceDirection * BounceForce;

        yield return new WaitForSeconds(Mathf.Max(0f, BounceDuration));

        if (droneRb != null)
            droneRb.linearVelocity = Vector2.zero;

        isBouncing = false;
        SetPositionLock(true);

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
        SetPositionLock(true);
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

        Vector2 knockbackDirection = ((Vector2)playerHealth.transform.position - (Vector2)transform.position).normalized;
        if (knockbackDirection == Vector2.zero)
            knockbackDirection = dashDirection;

        playerRb.linearVelocity = knockbackDirection * PlayerKnockbackForce;
    }

    private PlayerHealth GetPlayerHealth(Collider2D hitCollider)
    {
        return hitCollider != null ? hitCollider.GetComponentInParent<PlayerHealth>() : null;
    }

    private bool TryHitPlayerByOverlap()
    {
        if (droneCollider == null)
            return false;

        Bounds bounds = droneCollider.bounds;
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

    private Vector2 GetCollisionBounceDirection(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (normal.sqrMagnitude > 0.0001f)
                return normal.normalized;
        }

        return -dashDirection;
    }

    private bool TryFindObstacleAhead(out Vector2 bounceDirection)
    {
        bounceDirection = -dashDirection;

        if (droneCollider == null || dashDirection == Vector2.zero)
            return false;

        Bounds bounds = droneCollider.bounds;
        float castDistance = Mathf.Max(ObstacleCheckPadding, Mathf.Abs(dashSpeed) * Time.fixedDeltaTime + ObstacleCheckPadding);
        int layerMask = obstacleLayer.value != 0 ? obstacleLayer.value : Physics2D.DefaultRaycastLayers;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layerMask);
        filter.useTriggers = false;

        int hitCount = Physics2D.BoxCast(
            bounds.center,
            bounds.size,
            0f,
            dashDirection,
            filter,
            obstacleHits,
            castDistance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = obstacleHits[i];
            if (hit.collider == null || hit.collider == droneCollider || !IsObstacle(hit.collider))
                continue;

            bounceDirection = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : -dashDirection;
            return true;
        }

        return false;
    }

    private Vector2 NormalizedOrFallback(Vector2 direction, Vector2 fallback)
    {
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.left;
    }

    private void ConfigureRigidbody()
    {
        if (droneRb == null)
            return;

        droneRb.bodyType = RigidbodyType2D.Dynamic;
        droneRb.gravityScale = 0f;
        droneRb.linearDamping = 0f;
        droneRb.angularVelocity = 0f;
        droneRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        droneRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        SetPositionLock(true);
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

    private void SetPositionLock(bool locked)
    {
        if (droneRb == null)
            return;

        RigidbodyConstraints2D constraints = RigidbodyConstraints2D.FreezeRotation;
        if (locked)
            constraints |= RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;

        droneRb.angularVelocity = 0f;
        droneRb.constraints = constraints;
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

        if (droneRb != null)
            droneRb.linearVelocity = Vector2.zero;

        SetPositionLock(false);

        base.Die();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, dashDistance);
    }
}
