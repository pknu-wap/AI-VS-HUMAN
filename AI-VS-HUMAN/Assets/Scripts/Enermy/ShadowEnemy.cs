using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Shadow 몬스터
/// - 시작 시 같은 룸에 플레이어 있으면 즉시 텔레포트
/// - recordDelay 동안 기록 수집 + 충돌 판정 없음
/// - recordDelay 후 충돌 판정 활성화 + 정상 이동
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

    private Transform      player;
    private Rigidbody2D    rb;
    private SpriteRenderer sr;
    private Collider2D     col;
    private Room           myRoom;
    private bool           isDead        = false;
    private bool           isInitialized = false;

    private LayerMask playerLayer;

    void Start()
    {
        sr          = GetComponent<SpriteRenderer>();
        rb          = GetComponent<Rigidbody2D>();
        col         = GetComponent<Collider2D>();
        player      = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerLayer = LayerMask.GetMask("Player"); // Player 레이어만 감지

        if (rb != null)
        {
            rb.bodyType               = RigidbodyType2D.Kinematic;
            rb.gravityScale           = 0f;
            rb.constraints            = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.useFullKinematicContacts = true; // 충돌 감지 유지
        }

        // 콜라이더 처음부터 켜둠 (Ground 충돌 필요)
        // isTrigger는 false → Ground/벽 충돌 유지
        if (col != null)
        {
            col.isTrigger = false;
            col.enabled   = true;
        }

        // Room 탐색
        Room[] allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        foreach (Room room in allRooms)
        {
            if (room.GetBounds().Contains(transform.position))
            {
                myRoom = room;
                break;
            }
        }

        StartCoroutine(ActivateAfterDelay());
    }

    private bool canDamagePlayer = false; // recordDelay 후 플레이어 피격 가능

    IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(recordDelay);
        if (!isDead)
        {
            canDamagePlayer = true;
            Debug.Log("Shadow 충돌 활성화");
        }
    }

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

        Vector3 targetPos = transform.position;

        while (_records.Count > 0 && Time.time - _records.Peek().time >= recordDelay)
            targetPos = _records.Dequeue().position;

        if (!isInitialized)
        {
            isInitialized      = true;
            transform.position = targetPos;
            rb.position        = targetPos;
        }
        else
        {
            rb.MovePosition(targetPos);
        }

        if (sr != null && player != null)
            sr.flipX = player.position.x < transform.position.x;
    }

    // Trigger 방식으로 플레이어 감지
    void OnCollisionEnter2D(Collision2D other)
    {
        if (isDead || !canDamagePlayer) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        PlayerHealth ph = other.collider.GetComponent<PlayerHealth>();
        if (ph == null) return;

        ph.TakeDamage((int)damage);
        isDead = true;
        StartCoroutine(Die());
    }

    IEnumerator Die()
    {
        // 콜라이더 비활성화 (중복 충돌 방지)
        if (col != null) col.enabled = false;
        if (rb != null)  rb.linearVelocity = Vector2.zero;

        // 페이드 아웃
        float fadeDuration = 0.5f;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            if (sr != null)
            {
                Color c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / fadeDuration));
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
