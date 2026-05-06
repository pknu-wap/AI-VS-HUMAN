// 일반 병사 적의 시야 감지와 연사 공격을 담당하는 스크립트
// 플레이어가 감지 범위 안에 있고 벽에 막히지 않았을 때 조준 딜레이 후 총알을 발사한다.
using UnityEngine;
using System.Collections;

public class SoldierEnemy : EnemyBase
{
    [Header("병사 - 공격")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletDamage = 1f;
    public float bulletSpeed = 8f;
    public int bulletsPerBurst = 3;
    public float burstInterval = 0.15f;
    public float attackCooldown = 2.5f;

    [Header("병사 - 조준 연출")]
    public float aimDelay = 0.6f;           // 첫 감지 후 조준 딜레이 (경고 시간)
    public bool useLineOfSight = true;      // 장애물 감지 여부

    private float attackTimer = 0f;
    private bool isAttacking = false;
    private bool playerInSight = false;

    protected override void Start()
    {
        base.Start();
        attackTimer = attackCooldown;
    }

    void Update()
    {
        if (isDead || player == null) return;

        FacePlayer();
        CheckPlayerSight();

        if (playerInSight)
        {
            attackTimer += Time.deltaTime;

            if (attackTimer >= attackCooldown && !isAttacking)
            {
                attackTimer = 0f;
                StartCoroutine(AimAndFire());
            }
        }
    }

    // 플레이어가 감지 범위 안에 있고 장애물에 막히지 않았는지 확인한다.
    void CheckPlayerSight()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
        {
            playerInSight = false;
            return;
        }

        if (!useLineOfSight)
        {
            playerInSight = true;
            return;
        }

        // 장애물 레이캐스트 - 벽 너머는 못 봄
        Vector2 dirToPlayer = DirectionToPlayer();
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;

        RaycastHit2D hit = Physics2D.Raycast(origin, dirToPlayer, dist, obstacleLayer);

        // 장애물에 막히지 않으면 시야 확보
        playerInSight = hit.collider == null;
    }

    // 조준 딜레이 동안 노란색 경고를 보여준 뒤 연사한다.
    IEnumerator AimAndFire()
    {
        isAttacking = true;

        // 조준 중 - 노란색으로 경고
        if (spriteRenderer != null)
            spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(aimDelay);

        // 딜레이 중 시야 잃으면 취소
        if (!playerInSight)
        {
            isAttacking = false;
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
            yield break;
        }

        // 원래 색으로 복귀 후 발사
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            if (isDead || !playerInSight) break;
            FireBullet();
            yield return new WaitForSeconds(burstInterval);
        }

        isAttacking = false;
    }

    // 플레이어 방향으로 총알 한 발을 발사한다.
    void FireBullet()
    {
        if (bulletPrefab == null || firePoint == null) return;

        Vector2 dir = DirectionToPlayer();

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
            bullet.Init(dir, bulletDamage, bulletSpeed);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    // 플레이어 방향으로 스프라이트를 뒤집는다.
    void FacePlayer()
    {
        if (spriteRenderer == null || player == null) return;
        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 플레이 중 시야선 표시 (초록=감지, 빨강=차단)
        if (player != null && Application.isPlaying)
        {
            Gizmos.color = playerInSight ? Color.green : Color.red;
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Gizmos.DrawLine(origin, player.position);
        }
    }
}
