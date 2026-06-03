using UnityEngine;

public class PetalBullet : MonoBehaviour
{
    private const float DefaultDamage = 1f;
    private const float HitRadius = 0.2f;
    private const float MaxDistance = 15.5f;
    private const float WaveCount = 1f;
    private const bool PassThroughPlatforms = true;

    private float damage = DefaultDamage;
    private LayerMask playerMask;
    private LayerMask groundMask;
    private Vector2 startPos;
    private Vector2 direction;
    private Vector2 perpendicular;
    private float speed;
    private float curvature;
    private float maxLifetime;
    private float age;
    private bool initialized;

    private void Awake()
    {
        playerMask = LayerMask.GetMask("Player");
        groundMask = LayerMask.GetMask("Ground");

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void Init(Vector2 direction, float speed, float curvature, float maxLifetime, float spawnOffset = 1.5f)
    {
        this.direction = direction.normalized;
        this.perpendicular = new Vector2(-this.direction.y, this.direction.x);
        this.speed = speed;
        this.curvature = curvature;
        this.maxLifetime = maxLifetime;

        age = 0f;
        initialized = true;

        startPos = (Vector2)transform.position + this.direction * spawnOffset;
        transform.position = startPos;
        RotateToMoveDirection(this.direction);

        Destroy(gameObject, maxLifetime);
    }

    private void Update()
    {
        if (!initialized)
            return;

        age += Time.deltaTime;

        float progress = age * speed;
        float t = progress / MaxDistance;
        if (age >= maxLifetime || t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float wave = Mathf.Sin(t * Mathf.PI * WaveCount);
        Vector2 currentPos = transform.position;
        Vector2 nextPos = startPos + direction * progress + perpendicular * wave * curvature;
        Vector2 moveDir = nextPos - currentPos;

        transform.position = nextPos;

        if (moveDir.sqrMagnitude > 0.001f)
            RotateToMoveDirection(moveDir.normalized);

        CheckHit(nextPos);
    }

    private void CheckHit(Vector2 position)
    {
        Collider2D playerHit = Physics2D.OverlapCircle(position, HitRadius, playerMask);
        if (playerHit != null)
        {
            DamagePlayer(playerHit);
            Destroy(gameObject);
            return;
        }

        Collider2D groundHit = Physics2D.OverlapCircle(position, HitRadius, groundMask);
        if (groundHit != null && !ShouldPassThrough(groundHit))
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized)
            return;

        if (((1 << other.gameObject.layer) & playerMask) != 0)
        {
            DamagePlayer(other);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & groundMask) != 0 && !ShouldPassThrough(other))
            Destroy(gameObject);
    }

    private void DamagePlayer(Collider2D playerHit)
    {
        PlayerHealth playerHealth = playerHit.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = playerHit.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.TakeDamage(Mathf.RoundToInt(damage));
    }

    private bool ShouldPassThrough(Collider2D other)
    {
        return PassThroughPlatforms && IsPlatform(other);
    }

    private bool IsPlatform(Collider2D other)
    {
        return other.gameObject.layer == LayerMask.NameToLayer("Platform")
            || other.GetComponent<PlatformEffector2D>() != null
            || other.GetComponentInParent<PlatformEffector2D>() != null;
    }

    private void RotateToMoveDirection(Vector2 moveDirection)
    {
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, HitRadius);
    }
}
