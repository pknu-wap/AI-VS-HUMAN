using System.Collections;
using UnityEngine;

public class GiantDroneBoss : EnemyBase
{
    private enum BossPattern
    {
        FanToPlayer,
        SpiralBurst
    }

    [Header("Boss References")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    public GameObject healingMiniDronePrefab;
    public BossRoomController bossRoomController;

    [Header("Boss Movement")]
    public float hoverSpeed = 2.2f;
    public float hoverHeight = 4f;
    public float hoverAmplitude = 0.35f;
    public float hoverFrequency = 1.4f;
    public float phaseTwoRiseDistance = 5f;
    public float phaseTwoRiseSpeed = 3f;

    [Header("Pattern Cycle")]
    public float patternCooldown = 1.4f;
    public float windupTime = 0.45f;

    [Header("Pattern 1 - Fan Barrage")]
    public int fanBulletCount = 11;
    public float fanSpreadAngle = 90f;
    public float fanBulletSpeed = 5.5f;
    public float fanBulletDamage = 1f;
    public int fanVolleyCount = 3;
    public float fanVolleyInterval = 0.22f;

    [Header("Pattern 2 - Spiral Barrage")]
    public int spiralArms = 4;
    public int spiralWaves = 24;
    public float spiralAngleStep = 13f;
    public float spiralWaveInterval = 0.05f;
    public float spiralBulletSpeed = 4.7f;
    public float spiralBulletDamage = 1f;

    [Header("Pattern 3 - Recovery Phase")]
    [Range(0.1f, 0.9f)] public float recoveryHealthThreshold = 0.5f;
    public int healingDroneCount = 2;
    public float recoveryCooldown = 10f;
    public float healingDroneOffscreenMargin = 1.5f;
    public float healingDroneVerticalSpacing = 1.2f;
    public float healingDroneAttachDistance = 1.25f;
    public float healingDroneSpawnDelay = 0.25f;

    private float hoverTime;
    private float recoveryCooldownTimer;
    private bool isRunningPattern;
    private bool recoveryPhaseStarted;
    private int activeHealingDrones;
    private BossPattern nextPattern = BossPattern.FanToPlayer;

    protected override void Start()
    {
        base.Start();

        if (firePoint == null)
            firePoint = transform;
    }

    private void Update()
    {
        if (isDead || player == null)
            return;

        hoverTime += Time.deltaTime;
        recoveryCooldownTimer = Mathf.Max(0f, recoveryCooldownTimer - Time.deltaTime);

        FacePlayer();

        if (!recoveryPhaseStarted)
            HoverNearPlayer();

        if (CanStartRecoveryPhase())
            StartCoroutine(StartRecoveryPhase());

        if (IsPlayerInDetectionRange() && !isRunningPattern && !recoveryPhaseStarted)
            StartCoroutine(RunNextPattern());
    }

    public override void TakeDamage(float damage)
    {
        if (isDead)
            return;

        base.TakeDamage(damage);

        if (CanStartRecoveryPhase())
            StartCoroutine(StartRecoveryPhase());
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        currentHp = Mathf.Clamp(currentHp + amount, 0f, maxHp);
    }

    public bool IsDead()
    {
        return isDead;
    }

    public void NotifyHealingMiniDroneDestroyed()
    {
        activeHealingDrones = Mathf.Max(0, activeHealingDrones - 1);
    }

    private bool CanStartRecoveryPhase()
    {
        return !isDead
            && !recoveryPhaseStarted
            && activeHealingDrones <= 0
            && recoveryCooldownTimer <= 0f
            && currentHp <= maxHp * recoveryHealthThreshold;
    }

    private void HoverNearPlayer()
    {
        Vector2 target = new Vector2(player.position.x, player.position.y + hoverHeight);
        float bobOffset = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;
        target += Vector2.up * bobOffset;

        transform.position = Vector2.MoveTowards(
            transform.position,
            target,
            hoverSpeed * Time.deltaTime
        );
    }

    private IEnumerator RunNextPattern()
    {
        isRunningPattern = true;

        if (spriteRenderer != null)
            spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(windupTime);

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        if (nextPattern == BossPattern.FanToPlayer)
        {
            yield return StartCoroutine(FireFanBarrage());
            nextPattern = BossPattern.SpiralBurst;
        }
        else
        {
            yield return StartCoroutine(FireSpiralBarrage());
            nextPattern = BossPattern.FanToPlayer;
        }

        yield return new WaitForSeconds(patternCooldown);
        isRunningPattern = false;
    }

    private IEnumerator FireFanBarrage()
    {
        for (int volley = 0; volley < fanVolleyCount; volley++)
        {
            if (isDead || player == null)
                yield break;

            FireFanOnce();
            yield return new WaitForSeconds(fanVolleyInterval);
        }
    }

    private void FireFanOnce()
    {
        if (bulletPrefab == null || firePoint == null || player == null)
            return;

        Vector2 baseDir = DirectionToPlayer();
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - fanSpreadAngle * 0.5f;
        float angleStep = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + angleStep * i;
            SpawnBullet(angle, fanBulletDamage, fanBulletSpeed);
        }
    }

    private IEnumerator FireSpiralBarrage()
    {
        if (bulletPrefab == null || firePoint == null)
            yield break;

        float baseAngle = Random.Range(0f, 360f);
        float armStep = 360f / Mathf.Max(1, spiralArms);

        for (int wave = 0; wave < spiralWaves; wave++)
        {
            if (isDead)
                yield break;

            float waveAngle = baseAngle + wave * spiralAngleStep;

            for (int arm = 0; arm < spiralArms; arm++)
                SpawnBullet(waveAngle + armStep * arm, spiralBulletDamage, spiralBulletSpeed);

            yield return new WaitForSeconds(spiralWaveInterval);
        }
    }

    private IEnumerator StartRecoveryPhase()
    {
        recoveryPhaseStarted = true;
        isRunningPattern = true;

        if (bossRoomController != null)
            bossRoomController.StartPhase2();

        Vector3 start = transform.position;
        Vector3 target = start + Vector3.up * phaseTwoRiseDistance;

        if (spriteRenderer != null)
            spriteRenderer.color = Color.cyan;

        while (!isDead && Vector3.Distance(transform.position, target) > 0.03f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                phaseTwoRiseSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        yield return StartCoroutine(SpawnHealingDrones());

        recoveryCooldownTimer = recoveryCooldown;
        recoveryPhaseStarted = false;
        isRunningPattern = false;
    }

    private IEnumerator SpawnHealingDrones()
    {
        if (healingMiniDronePrefab == null)
            yield break;

        int count = Mathf.Max(0, healingDroneCount);

        for (int i = 0; i < count; i++)
        {
            if (isDead)
                yield break;

            Vector2 spawnPosition = GetHealingDroneSpawnPosition(i, count);
            Vector2 attachOffset = GetHealingDroneAttachOffset(i, count);

            GameObject droneObj = Instantiate(
                healingMiniDronePrefab,
                spawnPosition,
                Quaternion.identity
            );

            HealingMiniDrone miniDrone = droneObj.GetComponent<HealingMiniDrone>();
            if (miniDrone != null)
            {
                activeHealingDrones++;
                miniDrone.Init(this, attachOffset);
            }

            yield return new WaitForSeconds(healingDroneSpawnDelay);
        }
    }

    private Vector2 GetHealingDroneSpawnPosition(int index, int count)
    {
        Camera cam = Camera.main;
        int side = index % 2 == 0 ? -1 : 1;
        float yOffset = (index - (count - 1) * 0.5f) * healingDroneVerticalSpacing;
        float spawnY = transform.position.y + yOffset;

        if (cam == null)
            return (Vector2)transform.position + new Vector2(side * 12f, yOffset);

        float distanceFromCamera = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 viewportPoint = new Vector3(side < 0 ? 0f : 1f, 0.5f, distanceFromCamera);
        Vector3 worldEdge = cam.ViewportToWorldPoint(viewportPoint);

        return new Vector2(
            worldEdge.x + side * healingDroneOffscreenMargin,
            spawnY
        );
    }

    private Vector2 GetHealingDroneAttachOffset(int index, int count)
    {
        int side = index % 2 == 0 ? -1 : 1;
        float yOffset = (index - (count - 1) * 0.5f) * healingDroneVerticalSpacing;
        return new Vector2(side * healingDroneAttachDistance, yOffset);
    }

    private void SpawnBullet(float angle, float damage, float speed)
    {
        Vector2 dir = new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        );

        GameObject bulletObj = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.Euler(0f, 0f, angle)
        );

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
            bullet.Init(dir, damage, speed);
    }

    private void FacePlayer()
    {
        if (spriteRenderer == null || player == null)
            return;

        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, healingDroneAttachDistance);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * phaseTwoRiseDistance);
    }
}
