using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage   = 1f;
    public float speed    = 8f;
    public float lifetime = 2f;

    public LayerMask ignoreLayer;

    private Vector2 direction;
    private bool    isInit = false;

    void Awake()
    {
        // 콜라이더 자동으로 Trigger 설정
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        // Rigidbody2D 없으면 자동 추가
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic;
    }

    public void Init(Vector2 dir, float dmg, float spd)
    {
        direction = dir.normalized;
        damage    = dmg;
        speed     = spd;
        isInit    = true;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!isInit) return;
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInit)         return;
        if (other.isTrigger) return;

        // Enemy 레이어 통과
        if (ignoreLayer != 0 && ((1 << other.gameObject.layer) & ignoreLayer) != 0)
            return;

        // 적 오브젝트 통과
        if (other.GetComponent<EnemyBase>()   != null) return;
        if (other.GetComponent<IDamageable>() != null) return;

        // 플레이어 피격
        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(1);
            Destroy(gameObject);
            return;
        }

        // 벽/바닥
        Destroy(gameObject);
    }
}