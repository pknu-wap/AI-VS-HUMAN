// 일반 병사 적의 시야 감지와 연사 공격을 담당하는 스크립트
// 플레이어가 감지 범위 안에 있고 벽에 막히지 않았을 때 조준 딜레이 후 총알을 발사한다.
using UnityEngine;
using System.Collections;

// 병사 적
// - 제자리에서 플레이어 감지 시 사격
// - 조준 딜레이(경고 연출) 후 연사
// - 장애물 감지로 벽 너머 사격 방지
public class SoldierEnemy : EnemyBase
{
    [Header("공격")]
    public GameObject bulletPrefab;
    public Transform  firePoint;
    public float bulletDamage   = 1f;
    public float bulletSpeed    = 8f;
    public float attackCooldown = 2.5f;
    public int   bulletsPerBurst = 3;
    public float burstInterval  = 0.15f;

    [Header("조준")]
    public float aimDelay       = 0.6f;   // 발사 전 경고 시간
    public bool  useLineOfSight = true;   // 장애물 감지 여부

    [Header("예측샷")]
    [Range(0f, 1f)] public float predictiveShotChance = 0.3f;
    public float predictiveAimStrength = 0.65f;
    public float maxPredictionTime = 0.8f;

    [Header("이동")]
    public float moveSpeed = 1.2f;
    public float stopDistance = 3.5f;
    public float gravityScale = 1f;

    private float attackTimer  = 0f;
    private bool  isAttacking  = false;
    private bool  playerInSight = false;
    private Rigidbody2D soldierRigidbody;
    private Rigidbody2D playerRigidbody;

    protected override void Start()
    {
        base.Start();
        soldierRigidbody = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
        CachePlayerRigidbody();
        attackTimer = attackCooldown; // 시작하자마자 첫 발 가능
    }

    void Update()
    {
        if (isDead || player == null) return;

        // 스프라이트 방향
        if (spriteRenderer != null)
            spriteRenderer.flipX = player.position.x < transform.position.x;

        CheckPlayerSight();

        if (!playerInSight) return;

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown && !isAttacking)
        {
            attackTimer = 0f;
            StartCoroutine(AimAndFire());
        }
    }

    private void FixedUpdate()
    {
        if (soldierRigidbody == null || isDead)
            return;

        if (!playerInSight || isAttacking || player == null)
        {
            soldierRigidbody.linearVelocity = new Vector2(0f, soldierRigidbody.linearVelocity.y);
            return;
        }

        float distanceX = player.position.x - transform.position.x;
        if (Mathf.Abs(distanceX) <= stopDistance)
        {
            soldierRigidbody.linearVelocity = new Vector2(0f, soldierRigidbody.linearVelocity.y);
            return;
        }

        soldierRigidbody.linearVelocity = new Vector2(Mathf.Sign(distanceX) * moveSpeed, soldierRigidbody.linearVelocity.y);
    }

    // 거리 + 장애물 기반 시야 판정
    void CheckPlayerSight()
    {
        if (!IsPlayerInDetectionRange()) { playerInSight = false; return; }
        if (!useLineOfSight)             { playerInSight = true;  return; }

        Vector2      origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        float        dist   = Vector2.Distance(origin, player.position);
        RaycastHit2D hit    = Physics2D.Raycast(origin, DirectionToPlayer(), dist, obstacleLayer);
        playerInSight       = hit.collider == null;
    }

    // 노란색 경고 → 연사. 조준 중 시야 잃으면 취소
    IEnumerator AimAndFire()
    {
        isAttacking = true;
        if (spriteRenderer != null) spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(aimDelay);

        if (!playerInSight || isDead)
        {
            isAttacking = false;
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            yield break;
        }

        if (spriteRenderer != null) spriteRenderer.color = originalColor;

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            if (isDead || !playerInSight) break;
            FireBullet();
            yield return new WaitForSeconds(burstInterval);
        }

        isAttacking = false;
    }

    void FireBullet()
    {
        if (bulletPrefab == null || firePoint == null) return;

        Vector2 dir = GetShotDirection();

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
            bullet.Init(dir, bulletDamage, bulletSpeed);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private Vector2 GetShotDirection()
    {
        if (player == null)
            return Vector2.right;

        if (Random.value > predictiveShotChance)
            return DirectionToPlayer();

        CachePlayerRigidbody();

        if (playerRigidbody == null)
            return DirectionToPlayer();

        Vector2 firePosition = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 playerPosition = player.position;
        float distance = Vector2.Distance(firePosition, playerPosition);
        float safeBulletSpeed = Mathf.Max(0.01f, bulletSpeed);
        float predictionTime = Mathf.Min(distance / safeBulletSpeed, Mathf.Max(0f, maxPredictionTime));
        Vector2 predictedPosition = playerPosition + playerRigidbody.linearVelocity * predictionTime * predictiveAimStrength;

        return (predictedPosition - firePosition).normalized;
    }

    // 플레이어 방향으로 스프라이트를 뒤집는다.
    void FacePlayer()
    {
        if (spriteRenderer == null || player == null) return;
        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    private void ConfigureRigidbody()
    {
        if (soldierRigidbody == null)
            return;

        soldierRigidbody.bodyType = RigidbodyType2D.Dynamic;
        soldierRigidbody.gravityScale = gravityScale;
        soldierRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void CachePlayerRigidbody()
    {
        if (playerRigidbody == null && player != null)
            playerRigidbody = player.GetComponent<Rigidbody2D>();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (player != null && Application.isPlaying)
        {
            Gizmos.color = playerInSight ? Color.green : Color.red;
            Gizmos.DrawLine(firePoint != null ? firePoint.position : transform.position, player.position);
        }
    }
}
