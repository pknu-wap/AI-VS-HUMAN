using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shadow 몬스터 - 2스테이지
/// - 플레이어 이동 경로를 recordDelay 초 후 그대로 재생
/// - 플레이어와 동일한 물리 판정 (중력, 벽 충돌)
/// - 피격 불가, 닿으면 플레이어 체력 1 감소
/// </summary>
public class ShadowEnemy : MonoBehaviour
{
    [Header("설정")]
    public float recordDelay    = 3f;   // 몇 초 전 경로를 따라갈지
    public float damage         = 1f;
    public float damageCooldown = 1f;

    private struct PositionRecord
    {
        public Vector3 position;
        public float   time;
        public PositionRecord(Vector3 pos, float t) { position = pos; time = t; }
    }

    private Queue<PositionRecord> _records = new Queue<PositionRecord>();

    private Transform      player;
    private Rigidbody2D    rb;
    private SpriteRenderer sr;
    private float          damageTimer = 0f;

    void Start()
    {
        sr     = GetComponent<SpriteRenderer>();
        rb     = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // 플레이어와 동일한 물리 판정
        // Dynamic + 중력 유지 → 바닥에 서고 벽에 막힘
        if (rb != null)
        {
            rb.bodyType        = RigidbodyType2D.Dynamic;
            rb.constraints     = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // isTrigger false → 벽/바닥 충돌 O, 플레이어 충돌 판정은 OnCollisionEnter2D로
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
    }

    void Update()
    {
        if (player == null) return;

        damageTimer += Time.deltaTime;

        // 플레이어 위치 매 프레임 기록
        _records.Enqueue(new PositionRecord(player.position, Time.time));
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // recordDelay 이상 된 기록을 MovePosition으로 이동
        // MovePosition은 물리 충돌을 유지하며 이동 (벽에 막힘)
        while (_records.Count > 0 && Time.time - _records.Peek().time >= recordDelay)
        {
            PositionRecord record = _records.Dequeue();
            rb.MovePosition(record.position);
        }

        // 스프라이트 방향
        if (sr != null && player != null)
            sr.flipX = player.position.x < transform.position.x;
    }

    // isTrigger false → OnCollision으로 데미지 처리
    void OnCollisionEnter2D(Collision2D other) => TryDamagePlayer(other.collider);
    void OnCollisionStay2D(Collision2D other)  => TryDamagePlayer(other.collider);

    void TryDamagePlayer(Collider2D other)
    {
        if (!other.CompareTag("Player"))  return;
        if (damageTimer < damageCooldown) return;

        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph == null) return;

        ph.TakeDamage((int)damage);
        damageTimer = 0f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
