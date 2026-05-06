using UnityEngine;
using System.Collections;

/// <summary>
/// 방패 병사
/// - 방패가 왼쪽에 고정 → 왼쪽 공격 무효
/// - 감지 시 플레이어에게 접근
/// - 360도 탄막 (경고 연출 후 발사)
/// </summary>
public class ShieldEnemy : EnemyBase
{
    [Header("이동")]
    public float moveSpeed    = 2f;
    public float stopDistance = 2.5f;

    [Header("방패")]
    public Transform shieldTransform;
    public float     shieldOffsetX = 0.6f;

    [Header("탄막")]
    public GameObject bulletPrefab;
    public int   bulletCount    = 12;
    public float bulletSpeed    = 5f;
    public float bulletDamage   = 1f;
    public float attackCooldown = 3f;
    public float windupTime     = 0.8f;

    // 방패는 왼쪽 고정
    private SpriteRenderer shieldSr;
    private float attackTimer = 0f;
    private bool  isAttacking = false;

    protected override void Start()
    {
        base.Start();
        attackTimer = attackCooldown * 0.5f;

        if (shieldTransform != null)
        {
            shieldSr = shieldTransform.GetComponent<SpriteRenderer>();
            shieldTransform.localPosition = new Vector3(-shieldOffsetX, 0f, 0f);
            if (shieldSr != null) shieldSr.flipX = true;
        }
    }

    void Update()
    {
        if (isDead || player == null) return;

        if (spriteRenderer != null)
            spriteRenderer.flipX = player.position.x < transform.position.x;

        if (!IsPlayerInDetectionRange()) return;

        // 접근
        if (!IsPlayerInAttackRange())
        {
            float dirX = player.position.x > transform.position.x ? 1f : -1f;
            transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);
        }

        // 공격
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown && !isAttacking)
        {
            attackTimer = 0f;
            StartCoroutine(FanAttack());
        }
    }

    /// <summary>
    /// 방패 막기 판정 오버라이드
    /// 플레이어가 왼쪽(방패 방향)에서 공격하면 무효
    /// </summary>
    public override void TakeDamage(float damage)
    {
        if (isDead) return;

        // 방패 방향(왼쪽)에서 오는 공격 막기
        bool playerOnLeft = player != null && player.position.x < transform.position.x;
        if (playerOnLeft)
        {
            StartCoroutine(ShieldBlockFlash());
            return;
        }

        base.TakeDamage(damage);
    }

    /// <summary>노란색 경고 후 360도 탄막 발사</summary>
    IEnumerator FanAttack()
    {
        isAttacking = true;
        if (spriteRenderer != null) spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(windupTime);

        if (isDead)
        {
            isAttacking = false;
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            yield break;
        }

        if (spriteRenderer != null) spriteRenderer.color = originalColor;

        // 360도 균등 분할 발사
        float step = 360f / bulletCount;
        for (int i = 0; i < bulletCount; i++)
        {
            float   angle = step * i;
            Vector2 dir   = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject obj = Instantiate(bulletPrefab, transform.position, Quaternion.Euler(0f, 0f, angle));
            Bullet b = obj.GetComponent<Bullet>();
            if (b != null) b.Init(dir, bulletDamage, bulletSpeed);
        }

        isAttacking = false;
    }

    /// <summary>막혔을 때 방패 시안색 깜빡임</summary>
    IEnumerator ShieldBlockFlash()
    {
        if (shieldSr == null) yield break;
        shieldSr.color = Color.cyan;
        yield return new WaitForSeconds(0.12f);
        shieldSr.color = Color.white;
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
