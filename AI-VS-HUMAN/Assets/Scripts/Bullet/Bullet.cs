// 적이 발사하는 기본 직선 탄환을 제어하는 스크립트
// 발사 시 Init으로 방향/데미지/속도를 받은 뒤 움직이고, 플레이어 또는 벽에 닿으면 사라진다.
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage   = 1f;
    public float speed    = 8f;
    public float lifetime = 2f;

    public LayerMask ignoreLayer;

    // Init이 호출되기 전에는 움직이거나 충돌 처리하지 않는다.
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
        // 프리팹 하나를 여러 적이 공유할 수 있게 발사 시점에 값을 넣어준다.
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

        // 플레이어는 하트 기반 체력이라 탄환 데미지와 무관하게 한 칸만 깎는다.
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