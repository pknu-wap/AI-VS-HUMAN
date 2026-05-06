// 방패를 든 적의 이동, 방어 판정, 방사형 탄막 공격을 담당하는 스크립트
// 플레이어가 방패 방향에서 공격하면 데미지를 막고, 감지 범위 안에서는 접근 후 공격한다.
using UnityEngine;
using System.Collections;

public class ShieldEnemy : EnemyBase
{
    [Header("이동")]
    public float moveSpeed = 2f;
    public float stopDistance = 2.5f;

    [Header("방패")]
    public Transform shieldTransform;
    public float shieldOffsetX = 0.6f;

    [Header("탄막")]
    public GameObject bulletPrefab;
    public int bulletCount = 12;
    public float bulletSpeed = 5f;
    public float bulletDamage = 1f;
    public float attackCooldown = 3f;
    public float windupTime = 0.8f;

    private SpriteRenderer shieldSr;
    private float attackTimer = 0f;
    private bool isAttacking = false;

    protected override void Start()
    {
        base.Start();
        attackTimer = attackCooldown * 0.5f;

        if (shieldTransform != null)
        {
            shieldSr = shieldTransform.GetComponent<SpriteRenderer>();
            shieldTransform.localPosition = new Vector3(-shieldOffsetX, 0f, 0f);

            if (shieldSr != null)
                shieldSr.flipX = true;
        }
    }

    void Update()
    {
        if (isDead || player == null) return;

        FacePlayer();

        if (!IsPlayerInDetectionRange()) return;

        if (!IsPlayerInAttackRange())
            MoveTowardPlayer();

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown && !isAttacking)
        {
            attackTimer = 0f;
            StartCoroutine(FanAttack());
        }
    }

    public override void TakeDamage(float damage)
    {
        if (isDead) return;

        if (IsAttackBlocked())
        {
            StartCoroutine(ShieldBlockFlash());
            return;
        }

        base.TakeDamage(damage);
    }

    private void FacePlayer()
    {
        if (spriteRenderer == null || player == null) return;
        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    private void MoveTowardPlayer()
    {
        float dirX = player.position.x > transform.position.x ? 1f : -1f;
        transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);
    }

    private bool IsAttackBlocked()
    {
        if (player == null) return false;

        // 현재 방패는 왼쪽 고정이므로 플레이어가 왼쪽에서 공격하면 막는다.
        return player.position.x < transform.position.x;
    }

    private IEnumerator FanAttack()
    {
        isAttacking = true;

        if (spriteRenderer != null)
            spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(windupTime);

        if (isDead)
        {
            isAttacking = false;
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
            yield break;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        FireFanBullets();
        isAttacking = false;
    }

    private void FireFanBullets()
    {
        if (bulletPrefab == null || bulletCount <= 0) return;

        float step = 360f / bulletCount;
        for (int i = 0; i < bulletCount; i++)
        {
            float angle = step * i;
            Vector2 dir = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject obj = Instantiate(
                bulletPrefab,
                transform.position,
                Quaternion.Euler(0f, 0f, angle));

            Bullet bullet = obj.GetComponent<Bullet>();
            if (bullet != null)
                bullet.Init(dir, bulletDamage, bulletSpeed);
        }
    }

    private IEnumerator ShieldBlockFlash()
    {
        if (shieldSr == null) yield break;

        Color originalShieldColor = shieldSr.color;
        shieldSr.color = Color.cyan;
        yield return new WaitForSeconds(0.12f);
        shieldSr.color = originalShieldColor;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, Vector3.left * 1.5f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
