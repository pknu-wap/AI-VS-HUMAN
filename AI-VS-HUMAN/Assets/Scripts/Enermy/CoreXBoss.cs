using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 2스테이지 보스 - CORE-X
///
/// [인트로]  서버 4개 소환 → 플레이어가 전부 파괴 → 보스 깨어남
/// [1페이즈] HP바 표시 + SafeZone 패턴 반복 → HP 0
/// [2페이즈] HP 2배 부활 + 서버 재소환 + 몬스터 소환 + 전기장판 → HP 0 → 사망
/// </summary>
public class CoreXBoss : MonoBehaviour, IDamageable
{
    // ── 스탯 ────────────────────────────────────
    [Header("체력")]
    public float maxHp        = 500f;
    public float fadeDuration = 2f;

    // ── 방해 소환 ───────────────────────────────
    [Header("방해 소환 (2페이즈)")]
    public GameObject ghostPrefab;
    public GameObject shadowPrefab;
    public float      summonInterval   = 8f;
    public int        ghostSummonCount = 1;
    public int        shadowSummonCount = 1;

    // ── 전기 장판 ───────────────────────────────
    [Header("전기 장판 (2페이즈)")]
    public int   trapCount        = 2;
    public float trapInterval     = 10f;
    public float trapDuration     = 5f;
    public float trapBindDuration = 2f;
    public float trapWidth        = 3f;
    public int   trapBoltCount    = 4;
    public Color trapColor        = new Color(0.3f, 0.7f, 1f, 1f);

    // ── 2페이즈 돌진 ────────────────────────────
    [Header("2페이즈 돌진")]
    public float dashWindupTime   = 0.8f;
    public float dashMoveTime     = 0.3f;
    public float dashSpeed        = 15f;
    public float dashCooldown     = 1.5f;
    public float groggyDuration   = 1f;
    public int   dashDamage       = 1;
    public float phase2MaxHp          = 1000f;
    public Color phase2Color          = new Color(0.3f, 0f, 0.5f, 1f);
    public float phaseTransitionTime  = 2f;
    public float hpFillDuration       = 2f;    // HP 차오르는 시간

    // ── 내부 변수 ───────────────────────────────
    private float            currentHp;
    private bool             isDead          = false;
    private bool             isInvincible    = true;
    private bool             isPhase2        = false;
    private int              serversAlive    = 0;
    private int              serversDestroyed = 0;

    private Room             myRoom;
    private Transform        player;
    private BossHpBar        hpBar;
    private ServerNode[]     phase1Servers;
    private ServerNode[]     phase2Servers;

    private SpriteRenderer   sr;
    private Rigidbody2D      rb;
    private Coroutine        hitFlashCoroutine;
    private Color            originalColor;
    private List<GameObject> spawnedMinions = new List<GameObject>();

    // ═══════════════════════════════════════════
    //  Stage2BossRoomController에서 주입
    // ═══════════════════════════════════════════
    public void ConfigureForBossRoom(Room room, Transform playerTransform, BossHpBar bar,
                                     ServerNode[] p1Servers, ServerNode[] p2Servers)
    {
        myRoom        = room;
        player        = playerTransform;
        hpBar         = bar;
        phase1Servers = p1Servers;
        phase2Servers = p2Servers;

        if (hpBar != null)
        {
            hpBar.SetMaxHp(maxHp);
            hpBar.Hide();
        }
    }

    void Start()
    {
        currentHp     = maxHp;
        sr            = GetComponent<SpriteRenderer>();
        rb            = GetComponent<Rigidbody2D>();
        originalColor = sr != null ? sr.color : Color.white;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType     = RigidbodyType2D.Kinematic;
        }

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        StartCoroutine(BossBattleFlow());
    }

    // ═══════════════════════════════════════════
    //  전체 보스전 흐름
    // ═══════════════════════════════════════════
    IEnumerator BossBattleFlow()
    {
        // ── 인트로: 서버만 소환, 보스는 잠자는 상태 ──
        yield return StartCoroutine(IntroPhase());
        if (isDead) yield break;

        // ── 1페이즈 시작 ──
        yield return StartCoroutine(Phase1());
        if (isDead) yield break;

        // ── 2페이즈 전환 ──
        yield return StartCoroutine(PhaseTransition());
        if (isDead) yield break;

        // ── 2페이즈 시작 ──
        yield return StartCoroutine(Phase2());
    }

    // ═══════════════════════════════════════════
    //  인트로: 서버 소환 → 전부 파괴 대기
    // ═══════════════════════════════════════════
    IEnumerator IntroPhase()
    {
        isInvincible = true;

        yield return new WaitForSeconds(1f);

        // 서버 소환 (몬스터 없음)
        ActivateServers(phase1Servers);

        // 서버 전부 파괴될 때까지 대기
        yield return new WaitUntil(() => serversAlive <= 0 || isDead);
    }

    // ═══════════════════════════════════════════
    //  1페이즈: 보스 깨어남 + SafeZone 패턴
    // ═══════════════════════════════════════════
    IEnumerator Phase1()
    {
        yield return StartCoroutine(WakeUpEffect());

        if (hpBar != null) hpBar.Show();
        isInvincible = false;

        // 1페이즈에만 SafeZone 활성화
        BossSafeZonePattern safeZone = GetComponent<BossSafeZonePattern>();
        if (safeZone != null) safeZone.enabled = true;

        yield return new WaitUntil(() => currentHp <= 0f || isDead);

        // 1페이즈 끝 → SafeZone 비활성화
        if (safeZone != null) safeZone.enabled = false;
    }

    IEnumerator WakeUpEffect()
    {
        isInvincible = true;
        for (float t = 0f; t < 1.5f; t += 0.15f)
        {
            if (sr != null) sr.color = t % 0.3f < 0.15f ? Color.white : originalColor;
            yield return new WaitForSeconds(0.15f);
        }
        if (sr != null) sr.color = originalColor;
    }

    // ═══════════════════════════════════════════
    //  페이즈 전환 + 2페이즈
    // ═══════════════════════════════════════════
    IEnumerator PhaseTransition()
    {
        isInvincible = true;
        isPhase2     = true;

        // 색상 변경 깜빡임
        float step = phaseTransitionTime / 8f;
        for (int i = 0; i < 8; i++)
        {
            if (sr != null) sr.color = i % 2 == 0 ? Color.white : phase2Color;
            yield return new WaitForSeconds(step);
        }
        originalColor = phase2Color;
        if (sr != null) sr.color = phase2Color;

        // 2페이즈 HP 세팅 (0에서 시작)
        maxHp     = phase2MaxHp;
        currentHp = 0f;
        if (hpBar != null)
        {
            hpBar.SetMaxHp(maxHp);
            hpBar.SetHp(0f);
        }

        // 2페이즈 서버 소환
        serversDestroyed = 0;
        ActivateServers(phase2Servers);

        // 몬스터 + 장판 소환 시작 (서버 깨는 동안 계속)
        Coroutine summonLoop = StartCoroutine(SummonLoop());
        Coroutine trapLoop   = StartCoroutine(TrapLoop());

        // 서버 깨는 동안 HP가 차오름
        StartCoroutine(FillHpWhileServersAlive());

        // 서버 전부 파괴될 때까지 대기
        yield return new WaitUntil(() => serversAlive <= 0 || isDead);

        if (isDead)
        {
            StopCoroutine(summonLoop);
            StopCoroutine(trapLoop);
            yield break;
        }

        // 서버 파괴 완료 → HP 완전히 채움
        currentHp = maxHp;
        if (hpBar != null) hpBar.SetHp(currentHp);

        // 몬스터/장판은 유지하고 2페이즈 돌진 시작
        isInvincible = false;
        yield return StartCoroutine(Phase2());

        StopCoroutine(summonLoop);
        StopCoroutine(trapLoop);
    }

    /// <summary>서버가 살아있는 동안 HP가 서서히 차오름</summary>
    IEnumerator FillHpWhileServersAlive()
    {
        while (serversAlive > 0 && !isDead)
        {
            currentHp = Mathf.MoveTowards(currentHp, maxHp, (maxHp / hpFillDuration) * Time.deltaTime);
            if (hpBar != null) hpBar.SetHp(currentHp);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════
    //  2페이즈: 돌진 반복
    // ═══════════════════════════════════════════
    IEnumerator Phase2()
    {
        while (!isDead && currentHp > 0f)
        {
            yield return StartCoroutine(DashAtPlayer());
            yield return new WaitForSeconds(dashCooldown);
        }

        if (!isDead) StartCoroutine(Die());
    }

    /// <summary>
    /// 돌진 패턴
    /// 1. 전조: 노란색 + 멈춤 (dashWindupTime)
    /// 2. 돌진: 빨간색 + 플레이어 방향 이동
    /// 3. 그로기: 회색 + 멈춤 (groggyDuration)
    /// </summary>
    IEnumerator DashAtPlayer()
    {
        if (player == null) yield break;

        // ── 1. 전조 (노란색) ──
        if (sr != null) sr.color = Color.yellow;
        yield return new WaitForSeconds(dashWindupTime);
        if (isDead) yield break;

        // ── 2. 돌진 (빨간색) ──
        if (sr != null) sr.color = Color.red;

        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;

        // DashDrone 방식 - velocity로 돌진
        if (rb != null)
        {
            rb.bodyType       = RigidbodyType2D.Dynamic;
            rb.gravityScale   = 0f;
            rb.linearVelocity = dir * dashSpeed;
        }

        // dashMoveTime 동안 이동하면서 플레이어 감지
        float     elapsed    = 0f;
        bool      hitPlayer  = false;
        LayerMask playerMask = LayerMask.GetMask("Player");

        while (elapsed < dashMoveTime && !isDead && !hitPlayer)
        {
            elapsed += Time.deltaTime;

            Collider2D hit = Physics2D.OverlapCircle(transform.position, 1f, playerMask);
            if (hit != null)
            {
                PlayerHealth ph = hit.GetComponent<PlayerHealth>();
                if (ph != null) ph.TakeDamage(dashDamage);
                hitPlayer = true;
            }

            yield return null;
        }

        // 돌진 종료 → Kinematic으로 복귀
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType       = RigidbodyType2D.Kinematic;
        }

        // ── 3. 그로기 (회색) ──
        if (sr != null) sr.color = Color.gray;
        yield return new WaitForSeconds(groggyDuration);
        if (sr != null) sr.color = originalColor;
    }

    // ═══════════════════════════════════════════
    //  서버 활성화
    // ═══════════════════════════════════════════
    void ActivateServers(ServerNode[] servers)
    {
        if (servers == null || servers.Length == 0)
        {
            Debug.LogWarning("CoreXBoss: 서버가 비어있어요!");
            return;
        }

        serversAlive = 0;
        foreach (ServerNode server in servers)
        {
            if (server == null) continue;
            server.ResetServer();
            server.gameObject.SetActive(true);
            server.Init(this);
            serversAlive++;
        }
    }

    public void OnServerDestroyed()
    {
        serversAlive     = Mathf.Max(0, serversAlive - 1);
        serversDestroyed++;
    }

    // ═══════════════════════════════════════════
    //  몬스터 소환 루프 (2페이즈)
    // ═══════════════════════════════════════════
    IEnumerator SummonLoop()
    {
        yield return new WaitForSeconds(summonInterval * 0.5f);
        while (!isDead)
        {
            SummonMinions();
            yield return new WaitForSeconds(summonInterval);
        }
    }

    void SummonMinions()
    {
        if (myRoom == null) return;

        Bounds bounds = myRoom.GetBounds();
        float  margin = 1.5f;
        float  minX   = bounds.min.x + margin;
        float  maxX   = bounds.max.x - margin;
        float  minY   = bounds.min.y + margin;
        float  maxY   = bounds.max.y - margin;

        // Ghost 소환
        for (int i = 0; i < ghostSummonCount + serversDestroyed; i++)
        {
            if (ghostPrefab == null) break;
            Vector2    pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            GameObject obj = Instantiate(ghostPrefab, pos, Quaternion.identity);
            spawnedMinions.Add(obj);
            GhostEnemy ge = obj.GetComponent<GhostEnemy>();
            if (ge != null) ge.moveSpeed = Random.Range(0.8f, 1.4f);
        }

        // Shadow 시간차 소환
        StartCoroutine(SummonShadowsSequential());
    }

    IEnumerator SummonShadowsSequential()
    {
        for (int i = 0; i < shadowSummonCount; i++)
        {
            if (isDead || shadowPrefab == null || player == null) yield break;
            float      dirX     = (i % 2 == 0) ? 3f : -3f;
            Vector3    spawnPos = player.position + new Vector3(dirX, 0f, 0f);
            GameObject obj      = Instantiate(shadowPrefab, spawnPos, Quaternion.identity);
            spawnedMinions.Add(obj);
            ShadowEnemy se = obj.GetComponent<ShadowEnemy>();
            if (se != null) se.recordDelay = 3f + i * 3f;
            yield return new WaitForSeconds(2f);
        }
    }

    // ═══════════════════════════════════════════
    //  전기 장판 (LineRenderer 번개 이펙트)
    // ═══════════════════════════════════════════
    IEnumerator TrapLoop()
    {
        yield return new WaitForSeconds(trapInterval * 0.5f);
        while (!isDead)
        {
            SpawnElectricTraps();
            yield return new WaitForSeconds(trapInterval);
        }
    }

    void SpawnElectricTraps()
    {
        if (myRoom == null) return;

        Bounds bounds = myRoom.GetBounds();
        float  margin = 1.5f;
        float  minX   = bounds.min.x + margin;
        float  maxX   = bounds.max.x - margin;
        float  minY   = bounds.min.y + margin;
        float  maxY   = bounds.max.y - margin;

        for (int i = 0; i < trapCount; i++)
        {
            Vector2 pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            StartCoroutine(ElectricTrapRoutine(pos));
        }
    }

    /// <summary>
    /// 전기 장판 코루틴
    /// - LineRenderer로 번개 이펙트 생성
    /// - 플레이어 밟으면 속박
    /// - trapDuration 후 제거
    /// </summary>
    IEnumerator ElectricTrapRoutine(Vector2 pos)
    {
        // 장판 오브젝트 생성
        GameObject trap = new GameObject("ElectricTrap");
        trap.transform.position = pos;

        // 배경 발광 (SpriteRenderer)
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(trap.transform);
        glowObj.transform.localPosition = Vector3.zero;
        SpriteRenderer glow = glowObj.AddComponent<SpriteRenderer>();
        glow.sprite       = CreateWhiteSprite();
        glow.color        = new Color(0f, 0.4f, 1f, 0.25f);
        glow.sortingOrder = 5;
        glowObj.transform.localScale = new Vector3(trapWidth, 0.3f, 1f);

        // 번개 줄기 생성
        List<LineRenderer> bolts = new List<LineRenderer>();
        for (int i = 0; i < trapBoltCount; i++)
        {
            GameObject   boltObj = new GameObject($"Bolt_{i}");
            boltObj.transform.SetParent(trap.transform);
            LineRenderer lr = boltObj.AddComponent<LineRenderer>();

            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lr.material      = new Material(shader);
            lr.startColor    = trapColor;
            lr.endColor      = new Color(trapColor.r, trapColor.g, trapColor.b, 0f);
            lr.startWidth    = 0.04f;
            lr.endWidth      = 0.01f;
            lr.positionCount = 8;
            lr.useWorldSpace = true;
            lr.sortingOrder  = 10;
            bolts.Add(lr);
        }

        // 번개 애니메이션 + 플레이어 감지
        float   elapsed  = 0f;
        bool    bound    = false;
        float   nextAnim = 0f;

        while (elapsed < trapDuration && !isDead)
        {
            elapsed += Time.deltaTime;

            // 번개 갱신
            if (Time.time >= nextAnim)
            {
                nextAnim = Time.time + 0.05f;
                foreach (LineRenderer bolt in bolts)
                    UpdateLightningBolt(bolt, pos, trapWidth);

                // 발광 깜빡임
                if (glow != null)
                    glow.color = new Color(0f, 0.4f, 1f, Random.Range(0.1f, 0.35f));
            }

            // 플레이어 감지 (속박)
            if (!bound)
            {
                Collider2D hit = Physics2D.OverlapBox(pos, new Vector2(trapWidth, 0.4f),
                                                      0f, LayerMask.GetMask("Player"));
                if (hit != null)
                {
                    PlayerMove pm = hit.GetComponent<PlayerMove>();
                    if (pm != null)
                    {
                        bound = true;
                        StartCoroutine(BindPlayer(pm, bolts, glow));
                    }
                }
            }

            yield return null;
        }

        // 페이드아웃
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float a = Mathf.Lerp(1f, 0f, t / 0.3f);
            foreach (LineRenderer bolt in bolts)
                if (bolt != null)
                    bolt.startColor = new Color(trapColor.r, trapColor.g, trapColor.b, a);
            if (glow != null)
                glow.color = new Color(0f, 0.4f, 1f, a * 0.25f);
            yield return null;
        }

        if (trap != null) Destroy(trap);
    }

    void UpdateLightningBolt(LineRenderer lr, Vector2 center, float width)
    {
        if (lr == null) return;

        float   halfW    = width * 0.5f;
        Vector3 startPos = center + new Vector2(-halfW, Random.Range(-0.1f, 0.1f));
        Vector3 endPos   = center + new Vector2( halfW, Random.Range(-0.1f, 0.1f));

        for (int i = 0; i < 8; i++)
        {
            float   t     = (float)i / 7f;
            Vector3 point = Vector3.Lerp(startPos, endPos, t);
            point.y += Random.Range(-0.15f, 0.15f);
            lr.SetPosition(i, point);
        }
    }

    IEnumerator BindPlayer(PlayerMove pm, List<LineRenderer> bolts, SpriteRenderer glow)
    {
        // 속박 시 빨간색으로
        foreach (LineRenderer bolt in bolts)
            if (bolt != null) bolt.startColor = Color.red;
        if (glow != null) glow.color = new Color(1f, 0.2f, 0.2f, 0.3f);

        pm.enabled = false;
        yield return new WaitForSeconds(trapBindDuration);
        if (pm != null) pm.enabled = true;
    }

    Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void ClearMinions()
    {
        foreach (GameObject obj in spawnedMinions)
            if (obj != null) Destroy(obj);
        spawnedMinions.Clear();
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
        ClearMinions();

        if (rb != null) rb.linearVelocity = Vector2.zero;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (hpBar != null) hpBar.DestroyBar();

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            if (sr != null)
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b,
                                     Mathf.Lerp(1f, 0f, t / fadeDuration));
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
