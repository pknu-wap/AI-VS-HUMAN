using UnityEngine;

public class CurveBullet : MonoBehaviour
{
    private const float DefaultDamage = 1f;
    private const float DefaultSpeed = 5f;
    private const float Lifetime = 2.5f;
    private const bool PassThroughPlatforms = true;

    private float damage = DefaultDamage;
    private float speed = DefaultSpeed;
    private Vector2 direction;
    private bool isInit;

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

    public void Init(Vector2 dir, float dmg, float spd, float curve, bool left)
    {
        direction = dir.normalized;
        damage = dmg;
        speed = spd;
        isInit = true;
        Destroy(gameObject, Lifetime);
    }

    private void Update()
    {
        if (!isInit)
            return;

        transform.position += new Vector3(direction.x, direction.y, 0f) * speed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInit || other.isTrigger)
            return;

        if (PassThroughPlatforms && IsPlatform(other))
            return;

        if (other.GetComponent<EnemyBase>() != null)
            return;

        if (other.GetComponent<IDamageable>() != null)
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(Mathf.RoundToInt(damage));
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private bool IsPlatform(Collider2D other)
    {
        return other.gameObject.layer == LayerMask.NameToLayer("Platform")
            || other.GetComponent<PlatformEffector2D>() != null
            || other.GetComponentInParent<PlatformEffector2D>() != null;
    }
}
