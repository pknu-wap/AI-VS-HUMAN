using UnityEngine;

public class HomingMissileBullet : MonoBehaviour
{
    private Vector2 direction;
    private Transform target;
    private float damage;
    private float speed;
    private float turnSpeed;
    private float homingDuration;
    private float age;
    private bool initialized;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void Init(Vector2 initialDirection, Transform targetTransform, float damageAmount,
                     float moveSpeed, float turnDegreesPerSecond, float followDuration, float lifetime)
    {
        direction = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector2.down;
        target = targetTransform;
        damage = damageAmount;
        speed = Mathf.Max(0f, moveSpeed);
        turnSpeed = Mathf.Max(0f, turnDegreesPerSecond);
        homingDuration = Mathf.Max(0f, followDuration);
        age = 0f;
        initialized = true;

        RotateToDirection(direction);
        Destroy(gameObject, Mathf.Max(0.1f, lifetime));
    }

    private void Update()
    {
        if (!initialized)
            return;

        age += Time.deltaTime;

        if (target != null && age <= homingDuration)
        {
            Vector2 desiredDirection = ((Vector2)target.position - (Vector2)transform.position).normalized;
            if (desiredDirection.sqrMagnitude > 0.001f)
                direction = Vector2.MoveTowards(direction, desiredDirection, turnSpeed * Mathf.Deg2Rad * Time.deltaTime).normalized;
        }

        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        RotateToDirection(direction);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || other.isTrigger)
            return;

        if (IsPlatform(other))
            return;

        if (other.GetComponent<EnemyBase>() != null || other.GetComponent<IDamageable>() != null)
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(Mathf.RoundToInt(damage));
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void RotateToDirection(Vector2 moveDirection)
    {
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool IsPlatform(Collider2D other)
    {
        return other.gameObject.layer == LayerMask.NameToLayer("Platform")
            || other.GetComponent<PlatformEffector2D>() != null
            || other.GetComponentInParent<PlatformEffector2D>() != null;
    }
}
