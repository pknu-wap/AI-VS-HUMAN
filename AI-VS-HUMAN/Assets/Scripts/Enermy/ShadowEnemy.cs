using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shadow 몬스터 - 이펙트 없는 버전
/// - 플레이어 이동 경로를 recordDelay 초 후 그대로 재생
/// - 플레이어와 충돌 시 데미지 1 + 즉시 소멸
/// </summary>
public class ShadowEnemy : MonoBehaviour
{
    [Header("설정")]
    public float recordDelay = 3f;
    public float damage      = 1f;

    private struct PositionRecord
    {
        public Vector3 position;
        public float   time;
        public PositionRecord(Vector3 pos, float t) { position = pos; time = t; }
    }

    private Queue<PositionRecord> _records = new Queue<PositionRecord>();

    private Transform   player;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private bool        isDead = false;

    void Start()
    {
        sr     = GetComponent<SpriteRenderer>();
        rb     = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (rb != null)
        {
            rb.bodyType               = RigidbodyType2D.Dynamic;
            rb.constraints            = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
    }

    private bool isInitialized = false; // 첫 위치 설정 여부

    void Update()
    {
        if (player == null || isDead) return;
        _records.Enqueue(new PositionRecord(player.position, Time.time));
    }

    void FixedUpdate()
    {
        if (rb == null || isDead) return;
        if (_records.Count == 0) return;
        if (Time.time - _records.Peek().time < recordDelay) return;

        while (_records.Count > 0 && Time.time - _records.Peek().time >= recordDelay)
        {
            PositionRecord record = _records.Dequeue();

            // 첫 이동은 MovePosition이 아닌 텔레포트로 처리
            if (!isInitialized)
            {
                isInitialized      = true;
                transform.position = record.position;
                rb.position        = record.position;
            }
            else
            {
                rb.MovePosition(record.position);
            }
        }

        if (sr != null && player != null)
            sr.flipX = player.position.x < transform.position.x;
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (isDead) return;
        if (!other.collider.CompareTag("Player")) return;

        PlayerHealth ph = other.collider.GetComponent<PlayerHealth>();
        if (ph == null) return;

        ph.TakeDamage((int)damage);
        isDead = true;
        Destroy(gameObject); // 데미지 주고 즉시 소멸
    }
}
