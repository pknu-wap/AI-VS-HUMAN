// 드론이 발사하는 탄환을 제어하는 스크립트
// 현재는 실제 궤적을 휘게 만들지 않고, 발사 각도 배치로 곡선처럼 보이는 패턴을 만든다.
using UnityEngine;

public class CurveBullet : MonoBehaviour
{
    public float damage   = 1f;
    public float speed    = 5f;
    public float lifetime = 2.5f;

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

    public void Init(Vector2 dir, float dmg, float spd, float curve, bool left)
    {
        // curve/left는 예전 곡선 이동 실험용 파라미터다. 현재 패턴에서는 호환을 위해 받기만 한다.
        direction = dir.normalized;
        damage    = dmg;
        speed     = spd;
        isInit    = true;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!isInit) return;
        transform.position += new Vector3(direction.x, direction.y, 0f)
                              * speed * Time.deltaTime;
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
