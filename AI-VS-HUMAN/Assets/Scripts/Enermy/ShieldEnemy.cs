// 방패를 든 적의 이동, 방어 판정, 방사형 탄막 공격을 담당하는 스크립트
// 플레이어가 방패 방향에서 공격하면 데미지를 막고, 감지 범위 안에서는 접근 후 공격한다.
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

    // 방패 위치를 X축 기준 왼쪽/오른쪽으로만 배치한다.
    void UpdateShield()
    {
        if (shieldTransform == null) return;

        // 방패는 X축으로만 이동 (Y는 고정)
        float offsetX = shieldOnRight ? shieldOffsetX : -shieldOffsetX;
        shieldTransform.localPosition = new Vector3(offsetX, 0f, 0f);

        // 방패 스프라이트 뒤집기
        SpriteRenderer shieldSr = shieldTransform.GetComponent<SpriteRenderer>();
        if (shieldSr != null)
            shieldSr.flipX = !shieldOnRight;
    }

    // 몸통 스프라이트를 플레이어 방향으로 뒤집는다.
    void FacePlayer()
    {
        if (spriteRenderer == null || player == null) return;
        // 플레이어가 왼쪽이면 왼쪽을 봄 (방패랑 무관)
        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    // 플레이어 방향으로 수평 이동한다.
    void MoveTowardPlayer()
    {
        float dirX = player.position.x > transform.position.x ? 1f : -1f;
        transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);
    }

    // 방패 방향에서 들어온 공격이면 막고, 그 외 방향이면 EnemyBase의 피격 처리를 사용한다.
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

    // X축 기준으로 플레이어가 방패와 같은 방향에 있는지 확인한다.
    bool IsAttackBlocked()
    {
        if (player == null) return false;

        bool playerOnRight = player.position.x > transform.position.x;

        // 방패 방향과 플레이어 방향이 X축 기준으로 같으면 막힘
        return shieldOnRight == playerOnRight;
    }

    // 공격 전 경고 색을 보여준 뒤 탄막을 발사한다.
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

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        FireFanBullets();
        isAttacking = false;
    }

    // 360도 방사형 탄막을 발사한다.
    void FireFanBullets()
    {
        if (bulletPrefab == null) return;

        float angleStep = 360f / bulletCount;

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

    // 방패로 공격을 막았을 때 방패를 시안색으로 잠깐 깜빡인다.
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
