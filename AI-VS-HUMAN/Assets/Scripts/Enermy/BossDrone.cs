using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BossDrone : MonoBehaviour, IDamageable
{
    [Header("체력")]
    public float maxHp        = 600f;
    public float fadeDuration = 2f;
    private float currentHp;

    [Header("감지")]
    public float detectionRange = 25f;

    [Header("이동")]
    public float moveSpeed      = 2.5f;
    public float keepDistanceX  = 6f;
    public float keepDistanceY  = 6f;
    public float hoverAmplitude = 0.4f;
    public float hoverFrequency = 1.2f;

    [Header("이동 - 평소 좌우 흔들기")]
    public float swaySpeed     = 1.5f;
    public float swayAmplitude = 3f;

    [Header("이동 - U자 돌진")]
    public float dashSpeed = 8f;
    public float dashDropY = 6f;
    public float dashWidth = 8f;

    [Header("벽 회피")]
    public float wallAvoidDistance = 1.8f;
    public float wallSafeDistance  = 1.2f;
    public float wallAvoidSpeed    = 5f;
    public float wallCheckRadius   = 0.45f;

    [Header("부채꼴 탄막")]
    public GameObject fanBulletPrefab;
    public int   fanBulletCount  = 16;
    public float fanSpreadAngle  = 150f;
    public float fanBulletSpeed  = 6f;
    public float fanBulletDamage = 1f;
    public float fanCooldown     = 4f;

    [Header("꽃잎 탄막")]
    public GameObject petalBulletPrefab;
    public int   petalArmCount       = 6;
    public int   petalBulletsPerArm  = 14;
    public float petalBulletSpeed    = 4f;
    public float petalFireInterval   = 0.07f;
    public float petalMaxCurvature   = 1.4f;
    public float petalCurvatureSpeed = 1.0f;
    public float petalLoopDelay      = 3f;

    [Header("HP 바")]
    public Color hpBarColor = new Color(0.9f, 0.1f, 0.1f);

    [Header("힐링 드론")]
    public GameObject healDronePrefab;
    public int   healDroneCount       = 2;
    public float healDroneTimer       = 30f;
    public float healDroneFirstDelay  = 30f;
    public float healDroneRepeatDelay = 30f;
    public float healAmount           = 300f;
    public float healDroneOffsetX     = 3f;
    public float healDroneOffsetY     = -1f;

    private int   healDroneAliveCount = 0;
    private bool  isHealPhase         = false;
    private float healPhaseDelay      = 3f;

    private Transform      player;
    private bool           isDead       = false;
    private bool           isActive     = false;
    private bool           isDoingUDash = false;
    private bool           isDoingPetal = false;
    private float          hoverTime    = 0f;
    private float          petalTime    = 0f;
    private float          swayTime     = 0f;
    private float          swayBaseX    = 0f;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D    rb;
    private Coroutine      hitFlashCoroutine;
    private Slider         hpSlider;
    private Canvas         bossCanvas;

    void Start()
    {
        ClearExistingHealDrones();

        currentHp      = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        CreateHpBarUI();
        UpdateHpBar();
    }

    void ClearExistingHealDrones()
    {
        HealDrone[] drones = FindObjectsOfType<HealDrone>();

        foreach (HealDrone drone in drones)
        {
            if (drone != null)
                Destroy(drone.gameObject);
        }

        healDroneAliveCount = 0;
        isHealPhase = false;
    }

    void Update()
    {
        if (isDead || player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > detectionRange) return;

        if (!isActive)
        {
            isActive  = true;
            swayBaseX = transform.position.x;
            StartCoroutine(PatternLoop());
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = player.position.x < transform.position.x;

        hoverTime += Time.deltaTime;
        petalTime += Time.deltaTime;

        if (isDoingUDash || isDoingPetal) return;

        swayTime += Time.deltaTime;

        float bob = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;
        float targetX = swayBaseX + Mathf.Sin(swayTime * swaySpeed) * swayAmplitude;

        float idealY  = player.position.y + keepDistanceY + bob;
        float minY    = transform.position.y - 0.5f * Time.deltaTime;
        float targetY = Mathf.Max(idealY, minY);

        float newX = Mathf.MoveTowards(transform.position.x, targetX, moveSpeed * Time.deltaTime);
        float newY = Mathf.MoveTowards(transform.position.y, targetY, moveSpeed * Time.deltaTime);

        LayerMask groundMask = LayerMask.GetMask("Ground");
        Vector3 nextPosition = new Vector3(newX, newY, 0f);

        nextPosition = GetSafePosition(nextPosition, groundMask);

        transform.position = nextPosition;
    }

    Vector3 GetSafePosition(Vector3 wantedPosition, LayerMask groundMask)
    {
        Vector2 avoidDir = Vector2.zero;

        if (Physics2D.Raycast(wantedPosition, Vector2.right, wallAvoidDistance, groundMask))
            avoidDir += Vector2.left;

        if (Physics2D.Raycast(wantedPosition, Vector2.left, wallAvoidDistance, groundMask))
            avoidDir += Vector2.right;

        if (Physics2D.Raycast(wantedPosition, Vector2.up, wallAvoidDistance, groundMask))
            avoidDir += Vector2.down;

        if (Physics2D.Raycast(wantedPosition, Vector2.down, wallAvoidDistance, groundMask))
            avoidDir += Vector2.up;

        if (Physics2D.OverlapCircle(wantedPosition, wallCheckRadius, groundMask) != null)
            avoidDir += ((Vector2)transform.position - (Vector2)wantedPosition).normalized;

        if (avoidDir != Vector2.zero)
        {
            Vector3 safePosition = wantedPosition + (Vector3)(avoidDir.normalized * wallAvoidSpeed * Time.deltaTime);

            if (Physics2D.OverlapCircle(safePosition, wallCheckRadius, groundMask) == null)
            {
                swayBaseX = safePosition.x;
                return safePosition;
            }

            return transform.position;
        }

        return wantedPosition;
    }

    bool IsNearWall(LayerMask groundMask)
    {
        return Physics2D.Raycast(transform.position, Vector2.right, wallAvoidDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.left,  wallAvoidDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.up,    wallAvoidDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.down,  wallAvoidDistance, groundMask)
            || Physics2D.OverlapCircle(transform.position, wallCheckRadius, groundMask) != null;
    }

    IEnumerator MoveAwayFromWall(LayerMask groundMask)
    {
        float moveTime = 0.8f;
        float elapsed = 0f;

        while (elapsed < moveTime && !isDead)
        {
            elapsed += Time.deltaTime;

            Vector3 safePosition = GetSafePosition(transform.position, groundMask);

            if (safePosition == transform.position)
                yield break;

            transform.position = Vector3.MoveTowards(
                transform.position,
                safePosition,
                wallAvoidSpeed * Time.deltaTime);

            swayBaseX = transform.position.x;

            yield return null;
        }
    }

    IEnumerator PatternLoop()
    {
        yield return new WaitForSeconds(2f);

        StartCoroutine(HealDroneLoop());

        int[] patterns = new int[] { 0, 1 };

        while (!isDead)
        {
            for (int i = patterns.Length - 1; i > 0; i--)
            {
                int j   = Random.Range(0, i + 1);
                int tmp = patterns[i];
                patterns[i] = patterns[j];
                patterns[j] = tmp;
            }

            foreach (int p in patterns)
            {
                if (isDead) yield break;

                switch (p)
                {
                    case 0:
                        yield return StartCoroutine(UDashAndFire());
                        yield return new WaitForSeconds(fanCooldown);
                        break;

                    case 1:
                        isDoingPetal = true;
                        yield return StartCoroutine(FirePetalPattern());
                        isDoingPetal = false;
                        yield return new WaitForSeconds(petalLoopDelay);
                        break;
                }
            }
        }
    }

    IEnumerator HealDroneLoop()
    {
        yield return new WaitForSeconds(healDroneFirstDelay);

        while (!isDead)
        {
            yield return StartCoroutine(HealDronePattern());
            yield return new WaitForSeconds(healPhaseDelay);
            yield return new WaitForSeconds(healDroneRepeatDelay);
        }
    }

    IEnumerator UDashAndFire()
    {
        if (isDead) yield break;

        LayerMask groundMask = LayerMask.GetMask("Ground");

        if (IsNearWall(groundMask))
        {
            yield return StartCoroutine(MoveAwayFromWall(groundMask));
            yield break;
        }

        isDoingUDash = true;

        Vector3 startPos  = transform.position;
        float   checkDist = 0.5f;

        float dirX = swayTime % 2f < 1f ? 1f : -1f;
        float endX = startPos.x + dirX * dashWidth;

        float idealMidY = startPos.y - dashDropY;

        RaycastHit2D floorHit = Physics2D.Raycast(
            new Vector2((startPos.x + endX) * 0.5f, startPos.y),
            Vector2.down,
            dashDropY + 5f,
            groundMask);

        float midY = floorHit.collider != null
            ? Mathf.Max(idealMidY, floorHit.point.y + 1.5f)
            : idealMidY;

        float   midX   = (startPos.x + endX) * 0.5f;
        Vector3 midPos = new Vector3(midX, midY, 0f);
        Vector3 endPos = new Vector3(endX, startPos.y, 0f);

        bool patternFired = false;

        float elapsed  = 0f;
        float duration = Mathf.Max(Vector3.Distance(startPos, midPos) / dashSpeed, 0.1f);

        while (elapsed < duration && !isDead)
        {
            elapsed += Time.deltaTime;

            float   t       = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPos = Vector3.Lerp(startPos, midPos, t);
            Vector2 moveDir = ((Vector2)nextPos - (Vector2)transform.position).normalized;

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                moveDir,
                Vector2.Distance(transform.position, nextPos) + checkDist,
                groundMask);

            if (hit.collider != null)
            {
                transform.position = (Vector3)hit.point - (Vector3)(moveDir * checkDist);
                break;
            }

            transform.position = GetSafePosition(nextPos, groundMask);

            if (t > 0.8f && !patternFired)
            {
                patternFired = true;
                FireFanBullets();
            }

            yield return null;
        }

        if (!patternFired && !isDead)
            FireFanBullets();

        elapsed  = 0f;
        duration = Mathf.Max(Vector3.Distance(midPos, endPos) / dashSpeed, 0.1f);

        while (elapsed < duration && !isDead)
        {
            elapsed += Time.deltaTime;

            float   t       = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPos = Vector3.Lerp(midPos, endPos, t);
            Vector2 moveDir = ((Vector2)nextPos - (Vector2)transform.position).normalized;

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                moveDir,
                Vector2.Distance(transform.position, nextPos) + checkDist,
                groundMask);

            if (hit.collider != null)
            {
                transform.position = (Vector3)hit.point - (Vector3)(moveDir * checkDist);
                break;
            }

            transform.position = GetSafePosition(nextPos, groundMask);

            yield return null;
        }

        if (!isDead)
        {
            Vector3 swingTarget = new Vector3(
                transform.position.x + dirX * 2f,
                startPos.y + keepDistanceY,
                0f);

            Vector3 swingStart = transform.position;
            elapsed  = 0f;
            duration = Mathf.Max(Vector3.Distance(swingStart, swingTarget) / (dashSpeed * 0.7f), 0.2f);

            while (elapsed < duration && !isDead)
            {
                elapsed += Time.deltaTime;

                float   t       = Mathf.Clamp01(elapsed / duration);
                Vector3 nextPos = Vector3.Lerp(swingStart, swingTarget, t);
                Vector2 moveDir = ((Vector2)nextPos - (Vector2)transform.position).normalized;

                RaycastHit2D hit = Physics2D.Raycast(
                    transform.position,
                    moveDir,
                    Vector2.Distance(transform.position, nextPos) + checkDist,
                    groundMask);

                if (hit.collider != null)
                    break;

                transform.position = GetSafePosition(nextPos, groundMask);

                yield return null;
            }
        }

        swayBaseX    = transform.position.x;
        isDoingUDash = false;
    }

    IEnumerator HealDronePattern()
    {
        if (healDronePrefab == null) yield break;

        isHealPhase         = true;
        healDroneAliveCount = 0;

        Vector3[] spawnOffsets = new Vector3[]
        {
            new Vector3(-healDroneOffsetX, healDroneOffsetY, 0f),
            new Vector3( healDroneOffsetX, healDroneOffsetY, 0f),
        };

        int spawnCount = Mathf.Min(healDroneCount, spawnOffsets.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = transform.position + spawnOffsets[i];

            LayerMask groundMask = LayerMask.GetMask("Ground");
            Vector2   spawnDir   = spawnOffsets[i].x > 0f ? Vector2.right : Vector2.left;
            float     spawnDist  = Mathf.Abs(spawnOffsets[i].x);

            RaycastHit2D wallHit = Physics2D.Raycast(
                transform.position,
                spawnDir,
                spawnDist,
                groundMask);

            if (wallHit.collider != null)
            {
                spawnPos = transform.position + new Vector3(
                    -spawnOffsets[i].x,
                    spawnOffsets[i].y,
                    0f);

                RaycastHit2D oppositeHit = Physics2D.Raycast(
                    transform.position,
                    -spawnDir,
                    spawnDist,
                    groundMask);

                if (oppositeHit.collider != null)
                {
                    spawnPos = transform.position + new Vector3(
                        spawnOffsets[i].x * 0.3f,
                        spawnOffsets[i].y,
                        0f);
                }
            }

            spawnPos = GetSafePosition(spawnPos, groundMask);

            GameObject go = Instantiate(healDronePrefab, spawnPos, Quaternion.identity);

            HealDrone hd = go.GetComponent<HealDrone>();
            if (hd == null)
                hd = go.AddComponent<HealDrone>();

            hd.Init(this);

            healDroneAliveCount++;

            StartCoroutine(HealDroneTimer(hd));
        }

        while (healDroneAliveCount > 0 && !isDead)
            yield return null;

        isHealPhase = false;
    }

    IEnumerator HealDroneTimer(HealDrone drone)
    {
        yield return new WaitForSeconds(healDroneTimer);

        if (drone != null)
            drone.OnTimerExpired();
    }

    public void OnHealDroneDestroyed(bool healBoss)
    {
        if (healBoss)
        {
            currentHp = Mathf.Clamp(currentHp + healAmount, 0f, maxHp);
            UpdateHpBar();
        }

        healDroneAliveCount--;

        if (healDroneAliveCount < 0)
            healDroneAliveCount = 0;
    }

    void FireFanBullets()
    {
        if (fanBulletPrefab == null || player == null) return;

        Vector2 baseDir    = ((Vector2)player.position - (Vector2)transform.position).normalized;
        float   baseAngle  = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float   startAngle = baseAngle - fanSpreadAngle * 0.5f;
        float   step       = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + step * i;

            Vector2 dir = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject bulletObj = Instantiate(
                fanBulletPrefab,
                transform.position,
                Quaternion.Euler(0f, 0f, angle));

            Bullet b = bulletObj.GetComponent<Bullet>();
            if (b != null)
                b.Init(dir, fanBulletDamage, fanBulletSpeed);
        }
    }

    IEnumerator FirePetalPattern()
    {
        if (petalBulletPrefab == null) yield break;

        float curvature = Mathf.Sin(petalTime * petalCurvatureSpeed) * petalMaxCurvature;
        float angleStep = 360f / petalArmCount;

        for (int arm = 0; arm < petalArmCount; arm++)
        {
            float angle = angleStep * arm;

            Vector2 dir = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            StartCoroutine(FireOneArm(dir, curvature));
        }

        yield return new WaitForSeconds(petalBulletsPerArm * petalFireInterval);
    }

    IEnumerator FireOneArm(Vector2 direction, float curvature)
    {
        for (int b = 0; b < petalBulletsPerArm; b++)
        {
            if (isDead) yield break;

            GameObject go = Instantiate(petalBulletPrefab, transform.position, Quaternion.identity);

            PetalBullet bullet = go.GetComponent<PetalBullet>();
            if (bullet == null)
                bullet = go.AddComponent<PetalBullet>();

            bullet.Init(direction, petalBulletSpeed, curvature, 3.5f);

            yield return new WaitForSeconds(petalFireInterval);
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp = Mathf.Clamp(currentHp - damage, 0f, maxHp);
        UpdateHpBar();

        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(HitFlash());

        if (currentHp <= 0f)
            StartCoroutine(Die());
    }

    IEnumerator HitFlash()
    {
        if (spriteRenderer == null) yield break;

        spriteRenderer.color = Color.red;

        yield return new WaitForSeconds(0.1f);

        if (!isDead)
            spriteRenderer.color = Color.white;

        hitFlashCoroutine = null;
    }

    IEnumerator Die()
    {
        isDead = true;
        StopAllCoroutines();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        if (bossCanvas != null)
            Destroy(bossCanvas.gameObject);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            if (spriteRenderer != null)
                spriteRenderer.color = new Color(1f, 1f, 1f, alpha);

            yield return null;
        }

        Destroy(gameObject);
    }

    void CreateHpBarUI()
    {
        GameObject canvasObj = new GameObject("BossHpCanvas");
        bossCanvas = canvasObj.AddComponent<Canvas>();
        bossCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        bossCanvas.sortingOrder = 999;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(canvasObj.transform, false);

        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin        = new Vector2(0.1f, 1f);
        bgRt.anchorMax        = new Vector2(0.9f, 1f);
        bgRt.pivot            = new Vector2(0.5f, 1f);
        bgRt.anchoredPosition = new Vector2(0f, -40f);
        bgRt.sizeDelta        = new Vector2(0f, 26f);

        bg.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        GameObject slObj = new GameObject("HpSlider");
        slObj.transform.SetParent(canvasObj.transform, false);

        hpSlider = slObj.AddComponent<Slider>();

        RectTransform slRt = slObj.GetComponent<RectTransform>();
        slRt.anchorMin        = new Vector2(0.1f, 1f);
        slRt.anchorMax        = new Vector2(0.9f, 1f);
        slRt.pivot            = new Vector2(0.5f, 1f);
        slRt.anchoredPosition = new Vector2(0f, -40f);
        slRt.sizeDelta        = new Vector2(0f, 26f);

        GameObject fa = new GameObject("Fill Area");
        fa.transform.SetParent(slObj.transform, false);

        RectTransform faRt = fa.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fa.transform, false);

        RectTransform fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        fill.AddComponent<Image>().color = hpBarColor;

        hpSlider.fillRect     = fillRt;
        hpSlider.minValue     = 0f;
        hpSlider.maxValue     = maxHp;
        hpSlider.value        = maxHp;
        hpSlider.interactable = false;

        GameObject txtObj = new GameObject("BossName");
        txtObj.transform.SetParent(canvasObj.transform, false);

        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin        = new Vector2(0.1f, 1f);
        txtRt.anchorMax        = new Vector2(0.9f, 1f);
        txtRt.pivot            = new Vector2(0.5f, 1f);
        txtRt.anchoredPosition = new Vector2(0f, -12f);
        txtRt.sizeDelta        = new Vector2(0f, 24f);

        var txt = txtObj.AddComponent<UnityEngine.UI.Text>();
        txt.text      = "거대 드론";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;
        txt.fontSize  = 18;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void UpdateHpBar()
    {
        if (hpSlider != null)
            hpSlider.value = currentHp;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, keepDistanceX);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, wallAvoidDistance);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, wallCheckRadius);
    }
}
