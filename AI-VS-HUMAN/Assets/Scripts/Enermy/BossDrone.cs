using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 거대 드론 보스
/// - 플레이어 감지 후 패턴 시작
/// - 베지에 곡선을 이용한 부드러운 U자 돌진
/// - 돌진 중 설정된 딜레이 간격으로 부채꼴 탄막 발사
/// - 고정된 Y축 높이 유지 (벽 회피 및 플레이어 Y추적 제거)
/// </summary>
public class BossDrone : MonoBehaviour, IDamageable
{
    [Header("체력")]
    public float maxHp = 600f;
    public float fadeDuration = 2f;

    [Header("감지")]
    public float detectionRange = 25f;

    [Header("이동")]
    public float moveSpeed = 2.5f;
    public float hoverAmplitude = 0.4f;
    public float hoverFrequency = 1.2f;
    public float swaySpeed = 1.5f;
    public float swayAmplitude = 3f;

    [Header("U자 돌진 (베지에 곡선)")]
    public float dashSpeed = 8f;
    public float dashDropY = 6f;
    public float dashWidth = 10f;

    [Header("부채꼴 탄막")]
    public GameObject fanBulletPrefab;
    public int fanBulletCount = 16;
    public float fanSpreadAngle = 150f;
    public float fanBulletSpeed = 6f;
    public float fanBulletDamage = 1f;
    public float fanCooldown = 4f;

    [Header("부채꼴 탄막 - 돌진 연동")]
    public int fanDashVolleyCount = 4;
    public float fanDashFireDelay = 0.25f; // 발사 사이의 시간 간격 (초)
    public float fanFireOffset = 0.8f;

    [Header("꽃잎 탄막")]
    public GameObject petalBulletPrefab;
    public int petalArmCount = 6;
    public int petalBulletsPerArm = 14;
    public float petalBulletSpeed = 3f;
    public float petalFireInterval = 0.14f;
    public float petalCurvature = 1.2f;
    public float petalRotatePerShot = 8f;
    public float petalSpawnOffset = 1.5f;
    public float petalLoopDelay = 3f;
    public float petalMoveSpeedMultiplier = 0.45f;

    [Header("힐링 드론")]
    public GameObject healDronePrefab;
    public int healDroneCount = 2;
    public float healDroneTimer = 30f;
    public float healDroneFirstDelay = 30f;
    public float healDroneRepeatDelay = 30f;
    public float healAmount = 300f;
    public float healDroneOffsetX = 3f;
    public float healDroneOffsetY = -1f;

    [Header("HP 바")]
    public Color hpBarColor = new Color(0.9f, 0.1f, 0.1f);

    // ── 내부 변수 ─────────────────────────────
    private float currentHp;
    private Transform player;
    private float baseY; 

    private bool isDead = false;
    private bool isActive = false;
    private bool isDoingUDash = false;
    private bool isDoingPetal = false;

    private float hoverTime = 0f;
    private float swayTime = 0f;
    private float swayBaseX = 0f;
    private float petalBaseAngle = 0f;

    private int healDroneAliveCount = 0;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Collider2D bossCollider;
    private Coroutine hitFlashCoroutine;
    private Slider hpSlider;
    private Canvas bossCanvas;
    private Color originalColor;

    void Start()
    {
        currentHp = maxHp;
        baseY = transform.position.y;

        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        bossCollider = GetComponent<Collider2D>();
        originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        ClearExistingHealDrones();
        CreateHpBarUI();
        UpdateHpBar();

        if (bossCanvas != null) bossCanvas.gameObject.SetActive(false);
    }

    void ClearExistingHealDrones()
    {
        HealDrone[] drones = FindObjectsOfType<HealDrone>();
        foreach (HealDrone drone in drones)
        {
            if (drone != null) Destroy(drone.gameObject);
        }
        healDroneAliveCount = 0;
    }

    void Update()
    {
        if (isDead || player == null) return;

        if (Vector2.Distance(transform.position, player.position) > detectionRange)
            return;

        if (!isActive)
        {
            isActive = true;
            swayBaseX = transform.position.x;
            if (bossCanvas != null) bossCanvas.gameObject.SetActive(true);
            StartCoroutine(PatternLoop());
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = player.position.x < transform.position.x;

        hoverTime += Time.deltaTime;
        if (isDoingUDash) return;

        swayTime += Time.deltaTime;
        swayBaseX = Mathf.MoveTowards(swayBaseX, player.position.x, moveSpeed * 0.5f * Time.deltaTime);

        float bob = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;
        float targetX = swayBaseX + Mathf.Sin(swayTime * swaySpeed) * swayAmplitude;
        float targetY = baseY + bob;
        
        float currentMoveSpeed = isDoingPetal ? moveSpeed * petalMoveSpeedMultiplier : moveSpeed;

        transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, targetY, 0f), currentMoveSpeed * Time.deltaTime);
    }

    IEnumerator PatternLoop()
    {
        yield return new WaitForSeconds(2f);
        StartCoroutine(HealDroneLoop());

        int[] patterns = { 0, 1 };
        while (!isDead)
        {
            ShufflePatterns(patterns);
            foreach (int pattern in patterns)
            {
                if (isDead) yield break;
                if (pattern == 0)
                {
                    yield return StartCoroutine(SmoothUDashAndFire());
                    yield return new WaitForSeconds(fanCooldown);
                }
                else
                {
                    isDoingPetal = true;
                    yield return StartCoroutine(FirePetalPattern());
                    isDoingPetal = false;
                    yield return new WaitForSeconds(petalLoopDelay);
                }
            }
        }
    }

    void ShufflePatterns(int[] patterns)
    {
        for (int i = patterns.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = patterns[i];
            patterns[i] = patterns[j];
            patterns[j] = temp;
        }
    }

    // ── 개선된 U자 돌진 + 시간차 부채꼴 발사 ────────────────
    IEnumerator SmoothUDashAndFire()
    {
        if (isDead || player == null) yield break;

        isDoingUDash = true;

        Vector3 p0 = transform.position;
        float dirX = player.position.x > transform.position.x ? 1f : -1f;

        // 베지에 곡선 제어점 (U자형)
        Vector3 p2 = new Vector3(p0.x + dashWidth * dirX, p0.y, 0f);
        Vector3 p1 = new Vector3((p0.x + p2.x) * 0.5f, p0.y - (dashDropY * 2f), 0f);

        float duration = Mathf.Max(dashWidth / dashSpeed, 1.2f);
        float elapsed = 0f;
        
        int firedCount = 0;
        float nextFireTime = 0f;

        while (elapsed < duration && !isDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            // 2차 베지에 곡선 위치 업데이트
            Vector3 m1 = Vector3.Lerp(p0, p1, easeT);
            Vector3 m2 = Vector3.Lerp(p1, p2, easeT);
            transform.position = Vector3.Lerp(m1, m2, easeT);

            // 곡선 진행도가 20%를 넘으면 설정한 간격(fanDashFireDelay)마다 발사
            if (easeT >= 0.2f && firedCount < fanDashVolleyCount)
            {
                if (Time.time >= nextFireTime)
                {
                    FireFanBullets();
                    firedCount++;
                    nextFireTime = Time.time + fanDashFireDelay;
                }
            }

            yield return null;
        }

        if (firedCount == 0 && !isDead) FireFanBullets();

        swayBaseX = transform.position.x;
        isDoingUDash = false;
    }

    void FireFanBullets()
    {
        if (fanBulletPrefab == null || player == null) return;

        Vector2 aimDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector2.down;

        Vector3 firePos = transform.position + (Vector3)(aimDir * fanFireOffset);
        float baseAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - fanSpreadAngle * 0.5f;
        float step = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + step * i;
            Vector2 dir = AngleToDir(angle);
            SpawnBullet(fanBulletPrefab, firePos, dir, fanBulletDamage, fanBulletSpeed, angle);
        }
    }

    IEnumerator FirePetalPattern()
    {
        if (petalBulletPrefab == null) yield break;
        float angleStep = 360f / petalArmCount;

        for (int shot = 0; shot < petalBulletsPerArm; shot++)
        {
            for (int arm = 0; arm < petalArmCount; arm++)
            {
                float angle = petalBaseAngle + angleStep * arm;
                Vector2 dir = AngleToDir(angle);
                float curveDir = arm % 2 == 0 ? 1f : -1f;

                GameObject go = Instantiate(petalBulletPrefab, transform.position, Quaternion.identity);
                PetalBullet pb = go.GetComponent<PetalBullet>() ?? go.AddComponent<PetalBullet>();
                pb.Init(dir, petalBulletSpeed, petalCurvature * curveDir, 3.5f, petalSpawnOffset);
            }
            petalBaseAngle -= petalRotatePerShot;
            yield return new WaitForSeconds(petalFireInterval);
        }
    }

    IEnumerator HealDroneLoop()
    {
        yield return new WaitForSeconds(healDroneFirstDelay);
        while (!isDead)
        {
            if (healDronePrefab == null) yield break;
            yield return StartCoroutine(HealDronePattern());
            yield return new WaitForSeconds(healDroneRepeatDelay);
        }
    }

    IEnumerator HealDronePattern()
    {
        healDroneAliveCount = 0;
        Vector3[] offsets = { new Vector3(-healDroneOffsetX, healDroneOffsetY, 0f), new Vector3(healDroneOffsetX, healDroneOffsetY, 0f) };
        int spawnCount = Mathf.Min(healDroneCount, offsets.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = transform.position + offsets[i];
            GameObject go = Instantiate(healDronePrefab, spawnPos, Quaternion.identity);
            HealDrone hd = go.GetComponent<HealDrone>() ?? go.AddComponent<HealDrone>();
            hd.Init(this);
            healDroneAliveCount++;
            StartCoroutine(HealDroneTimer(hd));
        }
        while (healDroneAliveCount > 0 && !isDead) yield return null;
    }

    IEnumerator HealDroneTimer(HealDrone drone)
    {
        yield return new WaitForSeconds(healDroneTimer);
        if (drone != null) drone.OnTimerExpired();
    }

    public void OnHealDroneDestroyed(bool doHeal)
    {
        if (doHeal)
        {
            currentHp = Mathf.Clamp(currentHp + healAmount, 0f, maxHp);
            UpdateHpBar();
        }
        healDroneAliveCount = Mathf.Max(0, healDroneAliveCount - 1);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        currentHp = Mathf.Clamp(currentHp - damage, 0f, maxHp);
        UpdateHpBar();
        if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());
        if (currentHp <= 0f) StartCoroutine(Die());
    }

    IEnumerator HitFlash()
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!isDead) spriteRenderer.color = originalColor;
        hitFlashCoroutine = null;
    }

    IEnumerator Die()
    {
        isDead = true;
        StopAllCoroutines();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (bossCollider != null) bossCollider.enabled = false;
        if (bossCanvas != null) Destroy(bossCanvas.gameObject);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }
        Destroy(gameObject);
    }

    Vector2 AngleToDir(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    void SpawnBullet(GameObject prefab, Vector3 spawnPos, Vector2 dir, float damage, float speed, float angle)
    {
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
        Bullet bullet = obj.GetComponent<Bullet>();
        if (bullet != null) bullet.Init(dir, damage, speed);
    }

    void CreateHpBarUI()
    {
        GameObject canvasObj = new GameObject("BossHpCanvas");
        bossCanvas = canvasObj.AddComponent<Canvas>();
        bossCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        bossCanvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.1f, 1f); bgRt.anchorMax = new Vector2(0.9f, 1f);
        bgRt.pivot = new Vector2(0.5f, 1f); bgRt.anchoredPosition = new Vector2(0f, -40f);
        bgRt.sizeDelta = new Vector2(0f, 26f);
        bgObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        CreateSlider(canvasObj);
        CreateLabel(canvasObj);
    }

    void CreateSlider(GameObject parent)
    {
        GameObject slObj = new GameObject("HpSlider");
        slObj.transform.SetParent(parent.transform, false);
        hpSlider = slObj.AddComponent<Slider>();
        RectTransform slRt = slObj.GetComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0.1f, 1f); slRt.anchorMax = new Vector2(0.9f, 1f);
        slRt.pivot = new Vector2(0.5f, 1f); slRt.anchoredPosition = new Vector2(0f, -40f);
        slRt.sizeDelta = new Vector2(0f, 26f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(slObj.transform, false);
        SetFullRect(fillArea.AddComponent<RectTransform>());

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        SetFullRect(fill.AddComponent<RectTransform>());
        fill.AddComponent<Image>().color = hpBarColor;

        hpSlider.fillRect = fill.GetComponent<RectTransform>();
        hpSlider.minValue = 0f; hpSlider.maxValue = maxHp;
        hpSlider.value = maxHp; hpSlider.interactable = false;
    }

    void CreateLabel(GameObject parent)
    {
        GameObject obj = new GameObject("BossName");
        obj.transform.SetParent(parent.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 1f); rt.anchorMax = new Vector2(0.9f, 1f);
        rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = new Vector2(0f, -12f);
        rt.sizeDelta = new Vector2(0f, 24f);

        Text txt = obj.AddComponent<Text>();
        txt.text = "거대 드론";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white; txt.fontSize = 18;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void SetFullRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void UpdateHpBar()
    {
        if (hpSlider != null) hpSlider.value = currentHp;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}