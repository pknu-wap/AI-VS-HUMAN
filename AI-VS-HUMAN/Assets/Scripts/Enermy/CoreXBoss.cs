using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 2스테이지 보스 - CORE-X
/// 1페이즈: 서버 4개 파괴 기믹 + Ghost/Shadow 소환 방해
/// - 서버 4개 살아있는 동안 무적
/// - 서버 파괴될수록 소환 몬스터 수 증가
/// </summary>
public class CoreXBoss : MonoBehaviour, IDamageable
{
    // ── 기본 스탯 ───────────────────────────────
    [Header("체력")]
    public float maxHp        = 500f;
    public float fadeDuration = 2f;

    // ── 서버 기믹 ───────────────────────────────
    [Header("서버")]
    public GameObject serverPrefab;
    public int        serverCount      = 4;     // 소환할 서버 수
    public float      serverMinDist    = 3f;    // 서버끼리 최소 거리 (뭉치지 않게)
    public float      serverEdgeMargin = 1.5f;  // 방 경계에서 안쪽 여백

    // ── 소환 방해 ───────────────────────────────
    [Header("방해 소환")]
    public GameObject ghostPrefab;
    public GameObject shadowPrefab;
    public float      summonInterval      = 8f;   // 소환 주기
    public int        baseSummonCount     = 1;    // 기본 소환 수 (서버 파괴마다 +1)
    public int        ghostSummonCount    = 1;    // 한 번에 소환할 Ghost 수
    public int        shadowSummonCount   = 1;    // 한 번에 소환할 Shadow 수

    [Header("피격 타이밍")]
    public float      vulnerableTime      = 5f;   // 서버 전부 파괴 후 피격 가능 시간

    // ── HP 바 ────────────────────────────────────
    [Header("HP 바")]
    public BossHpBar hpBar;  // Inspector에서 연결

    // ── 내부 변수 ───────────────────────────────
    private float          currentHp;
    private bool           isDead       = false;
    private bool           isInvincible = true;
    private int            serversAlive = 0;
    private int            serversDestroyed = 0;
    private Transform      player;

    private Room           myRoom;
    private SpriteRenderer sr;
    private Rigidbody2D    rb;
    private Coroutine      hitFlashCoroutine;
    private Color          originalColor;
    private LayerMask      groundMask;

    // ═══════════════════════════════════════════
    void Start()
    {
        currentHp     = maxHp;
        sr            = GetComponent<SpriteRenderer>();
        rb            = GetComponent<Rigidbody2D>();
        groundMask    = LayerMask.GetMask("Ground");
        originalColor = sr != null ? sr.color : Color.white;
        player        = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType     = RigidbodyType2D.Kinematic;
        }

        // 소환 위치 기준으로 속한 Room 자동 탐색
        Room[] allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        foreach (Room room in allRooms)
        {
            if (room.GetBounds().Contains(transform.position))
            {
                myRoom = room;
                break;
            }
        }

        // HP 바 초기화
        if (hpBar != null)
        {
            hpBar.SetMaxHp(maxHp);
            hpBar.Hide();
        }

        StartCoroutine(Phase1Loop());
    }

    // ═══════════════════════════════════════════
    //  1페이즈 메인 루프
    // ═══════════════════════════════════════════
    IEnumerator Phase1Loop()
    {
        yield return new WaitForSeconds(1f);

        // 보스 등장 시 HP 바 표시
        if (hpBar != null) hpBar.Show();

        while (!isDead)
        {
            // 서버 소환
            SpawnServers();
            isInvincible = true;

            // 소환 방해 루프 시작
            Coroutine summonLoop = StartCoroutine(SummonLoop());

            // 서버가 전부 파괴될 때까지 대기
            yield return new WaitUntil(() => serversAlive <= 0 || isDead);

            StopCoroutine(summonLoop);

            if (isDead) yield break;

            // 피격 가능 타이밍
            isInvincible = false;
            yield return new WaitForSeconds(vulnerableTime);

            isInvincible = true;
            serversDestroyed = 0;
        }
    }

    // ═══════════════════════════════════════════
    //  서버 소환 - 룸 안에 뭉치지 않게 퍼트림
    // ═══════════════════════════════════════════
    void SpawnServers()
    {
        if (serverPrefab == null || myRoom == null)
        {
            Debug.LogWarning("CoreXBoss: serverPrefab 또는 myRoom 미설정!");
            return;
        }

        serversAlive     = 0;
        Bounds bounds    = myRoom.GetBounds();

        // 방 경계에서 여백 적용한 실제 소환 범위
        float minX = bounds.min.x + serverEdgeMargin;
        float maxX = bounds.max.x - serverEdgeMargin;
        float minY = bounds.min.y + serverEdgeMargin;
        float maxY = bounds.max.y - serverEdgeMargin;

        List<Vector2> spawnedPositions = new List<Vector2>();

        for (int i = 0; i < serverCount; i++)
        {
            Vector2 spawnPos = Vector2.zero;
            bool    found    = false;

            // 최대 30번 시도해서 다른 서버와 겹치지 않는 위치 탐색
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float   x       = Random.Range(minX, maxX);
                float   y       = Random.Range(minY, maxY);
                Vector2 tryPos  = new Vector2(x, y);
                bool    tooClose = false;

                // 이미 소환된 서버와 최소 거리 체크
                foreach (Vector2 existing in spawnedPositions)
                {
                    if (Vector2.Distance(tryPos, existing) < serverMinDist)
                    {
                        tooClose = true;
                        break;
                    }
                }

                // 보스 위치와도 최소 거리 체크 (보스 안에 소환 방지)
                if (Vector2.Distance(tryPos, (Vector2)transform.position) < serverMinDist)
                    tooClose = true;

                // 바닥 위에 있는지 체크 (바닥 바로 위에 소환)
                RaycastHit2D ground = Physics2D.Raycast(tryPos, Vector2.down, 3f, groundMask);
                if (!tooClose && ground.collider != null)
                {
                    spawnPos = ground.point + Vector2.up * 0.5f;
                    found    = true;
                    break;
                }
            }

            // 바닥을 못 찾으면 그냥 랜덤 위치에 소환
            if (!found)
                spawnPos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));

            spawnedPositions.Add(spawnPos);

            GameObject  obj    = Instantiate(serverPrefab, spawnPos, Quaternion.identity);
            ServerNode  server = obj.GetComponent<ServerNode>();
            if (server != null) server.Init(this);

            serversAlive++;
        }
    }

    // ═══════════════════════════════════════════
    //  방해 소환 루프
    //  서버 파괴될수록 소환 수 증가
    // ═══════════════════════════════════════════
    IEnumerator SummonLoop()
    {
        yield return new WaitForSeconds(summonInterval * 0.5f); // 첫 소환은 절반 주기 후

        while (!isDead)
        {
            // 파괴된 서버 수만큼 보너스 소환
            SummonMinions(serversDestroyed);

            yield return new WaitForSeconds(summonInterval);
        }
    }

    void SummonMinions(int bonusCount)
    {
        if (myRoom == null) return;

        Bounds bounds = myRoom.GetBounds();
        float  minX   = bounds.min.x + serverEdgeMargin;
        float  maxX   = bounds.max.x - serverEdgeMargin;
        float  minY   = bounds.min.y + serverEdgeMargin;
        float  maxY   = bounds.max.y - serverEdgeMargin;

        // Ghost 소환
        int totalGhost = ghostSummonCount + bonusCount;
        for (int i = 0; i < totalGhost; i++)
        {
            if (ghostPrefab == null) break;
            Vector2 pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            Instantiate(ghostPrefab, pos, Quaternion.identity);
        }

        // Shadow 소환 - 플레이어 위치에 소환
        int totalShadow = shadowSummonCount;
        for (int i = 0; i < totalShadow; i++)
        {
            if (shadowPrefab == null || player == null) break;
            // 살짝 오프셋으로 겹침 방지
            Vector2 offset   = Random.insideUnitCircle * 0.5f;
            Vector3 spawnPos = player.position + new Vector3(offset.x, 0f, 0f);
            Instantiate(shadowPrefab, spawnPos, Quaternion.identity);
        }
    }

    /// <summary>서버가 파괴됐을 때 ServerNode에서 호출</summary>
    public void OnServerDestroyed()
    {
        serversAlive     = Mathf.Max(0, serversAlive - 1);
        serversDestroyed++;
    }

    // ═══════════════════════════════════════════
    //  데미지 / 사망
    // ═══════════════════════════════════════════
    public void TakeDamage(float damage)
    {
        if (isDead || isInvincible) return;

        currentHp = Mathf.Clamp(currentHp - damage, 0f, maxHp);
        if (hpBar != null) hpBar.SetHp(currentHp);

        if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());

        if (currentHp <= 0f) StartCoroutine(Die());
    }

    IEnumerator HitFlash()
    {
        if (sr == null) yield break;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!isDead) sr.color = originalColor;
        hitFlashCoroutine = null;
    }

    IEnumerator Die()
    {
        isDead = true;
        StopAllCoroutines();

        if (rb != null) rb.linearVelocity = Vector2.zero;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (hpBar != null) hpBar.DestroyBar();

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t / fadeDuration));
            yield return null;
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
