// 보스가 발사하는 꽃잎 탄환의 곡선 이동과 충돌 처리를 담당하는 스크립트
// 기본 진행 방향으로 날아가면서 좌우로 휘고, 플레이어나 지형에 닿으면 사라진다.
using UnityEngine;

public class PetalBullet : MonoBehaviour
{
    [Header("충돌")]
    public float damage = 1f;
    public float hitRadius = 0.2f;
    public LayerMask playerMask;
    public LayerMask groundMask;

    [Header("이동")]
    public float maxDistance = 15.5f;
    public float waveCount = 1f;

    // ── 내부 변수 ─────────────────────────────
    private Vector2 startPos;      // 탄환 시작 위치
    private Vector2 direction;     // 기본 진행 방향
    private Vector2 perpendicular; // 진행 방향의 수직 방향

    private float speed;           // 이동 속도
    private float curvature;       // 좌우로 휘어지는 정도
    private float maxLifetime;     // 최대 생존 시간
    private float age = 0f;        // 현재 생존 시간

    private bool initialized = false; // Init 호출 여부

    // ── 초기화 ───────────────────────────────
    void Awake()
    {
        if (playerMask.value == 0)
            playerMask = LayerMask.GetMask("Player");

        if (groundMask.value == 0)
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

    // 보스가 탄환을 생성한 직후 호출하는 초기화 함수
    // direction: 탄환 진행 방향
    // speed: 탄환 속도
    // curvature: 휘어지는 정도
    // maxLifetime: 생존 시간
    // spawnOffset: 보스 몸에서 떨어져 생성되는 거리
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

    // ── 이동 / 충돌 ───────────────────────────
    void Update()
    {
        if (!initialized) return;

        age += Time.deltaTime;

        float progress = age * speed;
        float t = progress / maxDistance;

        if (age >= maxLifetime || t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float wave = Mathf.Sin(t * Mathf.PI * waveCount);

        Vector2 currentPos = transform.position;

        Vector2 nextPos = startPos
                        + direction * progress
                        + perpendicular * wave * curvature;

        Vector2 moveDir = nextPos - currentPos;

        transform.position = nextPos;

        if (moveDir.sqrMagnitude > 0.001f)
            RotateToMoveDirection(moveDir.normalized);

        CheckHit(nextPos);
    }

    void CheckHit(Vector2 position)
    {
        // 플레이어 충돌 검사
        Collider2D playerHit = Physics2D.OverlapCircle(position, hitRadius, playerMask);
        if (playerHit != null)
        {
            PlayerHealth playerHealth = playerHit.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = playerHit.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
                playerHealth.TakeDamage(Mathf.RoundToInt(damage));

            Destroy(gameObject);
            return;
        }

        // 벽 / 바닥 충돌 검사
        Collider2D groundHit = Physics2D.OverlapCircle(position, hitRadius, groundMask);
        if (groundHit != null)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized) return;

        // 플레이어와 닿으면 데미지
        if (((1 << other.gameObject.layer) & playerMask) != 0)
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = other.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
                playerHealth.TakeDamage(Mathf.RoundToInt(damage));

            Destroy(gameObject);
            return;
        }

        // 벽 / 바닥에 닿으면 삭제
        if (((1 << other.gameObject.layer) & groundMask) != 0)
            Destroy(gameObject);
    }

    // ── 보조 함수 ─────────────────────────────
    void RotateToMoveDirection(Vector2 moveDirection)
    {
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ── 에디터 시각화 ─────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
