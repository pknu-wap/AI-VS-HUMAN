using UnityEngine;

/// <summary>
/// 유령 몬스터 - 2스테이지
/// - 벽/바닥 무시하고 플레이어를 천천히 따라다님
/// - sin 파형으로 부유하는 느낌
/// - 닿으면 플레이어 데미지
/// - 현재는 피격 불가 (isTrigger = true)
/// </summary>
public class GhostEnemy : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed      = 1.2f;
    public float hoverAmplitude = 0.3f;
    public float hoverFrequency = 1.5f;

    [Header("공격")]
    public float damage         = 1f;
    public float damageCooldown = 1f;

    [Header("감지")]
    public float detectionRange = 20f;

    private Transform      player;
    private SpriteRenderer sr;
    private float          hoverTime   = 0f;
    private float          damageTimer = 0f;
    private float          detectionRangeSqr;
    private Room           myRoom;

    void Start()
    {
        sr                = GetComponent<SpriteRenderer>();
        player            = GameObject.FindGameObjectWithTag("Player")?.transform;
        detectionRangeSqr = detectionRange * detectionRange;

        // 소환된 위치 기준으로 속한 Room 자동 탐색
        Room[] allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        foreach (Room room in allRooms)
        {
            if (room.GetBounds().Contains(transform.position))
            {
                myRoom = room;
                break;
            }
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType     = RigidbodyType2D.Kinematic;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        if (player == null) return;

        // 플레이어가 이 방 밖에 있으면 추적 중단
        if (myRoom != null && !myRoom.GetBounds().Contains(player.position)) return;

        // sqrMagnitude로 거리 비교 (sqrt 계산 없이 빠름)
        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        if (toPlayer.sqrMagnitude > detectionRangeSqr) return;

        hoverTime   += Time.deltaTime;
        damageTimer += Time.deltaTime;

        Vector2 dir  = toPlayer.normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float   sway = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;

        transform.position += (Vector3)((dir * moveSpeed + perp * sway) * Time.deltaTime);

        if (sr != null) sr.flipX = player.position.x < transform.position.x;
    }

    // Enter/Stay 
    void OnTriggerEnter2D(Collider2D other) => TryDamagePlayer(other);
    void OnTriggerStay2D(Collider2D other)  => TryDamagePlayer(other);

    void TryDamagePlayer(Collider2D other)
    {
        if (!other.CompareTag("Player"))       return;
        if (damageTimer < damageCooldown)      return;

        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph == null) return;

        ph.TakeDamage((int)damage);
        damageTimer = 0f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
