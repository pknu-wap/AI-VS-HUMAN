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
    private const float EnemyCheckInterval = 0.25f;
    private const float FadeDuration = 0.5f;

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
    private Rigidbody2D    rb;
    private float          hoverTime   = 0f;
    private float          damageTimer = 0f;
    private float          enemyCheckTimer = 0f;
    private bool           isDead = false;
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

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.angularVelocity = 0f;
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        if (isDead) return;

        KeepUpright();
        CheckDisappearIfNoOtherEnemies();

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

    private void KeepUpright()
    {
        if (rb != null)
            rb.angularVelocity = 0f;

        transform.rotation = Quaternion.identity;
    }

    // Enter/Stay 
    void OnTriggerEnter2D(Collider2D other) => TryDamagePlayer(other);
    void OnTriggerStay2D(Collider2D other)  => TryDamagePlayer(other);

    void TryDamagePlayer(Collider2D other)
    {
        if (isDead) return;

        if (!other.CompareTag("Player"))       return;
        if (damageTimer < damageCooldown)      return;

        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph == null) return;

        ph.TakeDamage((int)damage);
        damageTimer = 0f;
    }

    private void CheckDisappearIfNoOtherEnemies()
    {
        enemyCheckTimer += Time.deltaTime;
        if (enemyCheckTimer < EnemyCheckInterval)
            return;

        enemyCheckTimer = 0f;

        if (myRoom == null)
            ResolveRoom();

        if (myRoom != null && !HasOtherEnemyInRoom())
            StartCoroutine(Die());
    }

    private bool HasOtherEnemyInRoom()
    {
        Bounds bounds = myRoom.GetBounds();

        foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy && bounds.Contains(enemy.transform.position))
                return true;
        }

        foreach (CoreXBoss boss in FindObjectsByType<CoreXBoss>(FindObjectsSortMode.None))
        {
            if (boss != null && boss.gameObject.activeInHierarchy && !boss.IsDead && bounds.Contains(boss.transform.position))
                return true;
        }

        foreach (GiantDrone boss in FindObjectsByType<GiantDrone>(FindObjectsSortMode.None))
        {
            if (boss != null && boss.gameObject.activeInHierarchy && !boss.isDead && bounds.Contains(boss.transform.position))
                return true;
        }

        foreach (ServerNode server in FindObjectsByType<ServerNode>(FindObjectsSortMode.None))
        {
            if (server != null && server.gameObject.activeInHierarchy && bounds.Contains(server.transform.position))
                return true;
        }

        foreach (HealDrone healDrone in FindObjectsByType<HealDrone>(FindObjectsSortMode.None))
        {
            if (healDrone != null && healDrone.gameObject.activeInHierarchy && bounds.Contains(healDrone.transform.position))
                return true;
        }

        return false;
    }

    private void ResolveRoom()
    {
        Room[] allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        foreach (Room room in allRooms)
        {
            if (room != null && room.GetBounds().Contains(transform.position))
            {
                myRoom = room;
                return;
            }
        }
    }

    private System.Collections.IEnumerator Die()
    {
        isDead = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        Color startColor = sr != null ? sr.color : Color.white;
        for (float t = 0f; t < FadeDuration; t += Time.deltaTime)
        {
            if (sr != null)
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t / FadeDuration));

            yield return null;
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
