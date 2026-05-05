using UnityEngine;
using System.Collections;

/// <summary>
/// 방패 병사
/// - 방패는 왼쪽/오른쪽만 (X축 기준)
/// - 플레이어가 방패 방향에 있으면 데미지 무효
/// - 플레이어 감지 시 접근
/// - 충격파는 방패 방향으로 부채꼴 탄막
/// </summary>
public class ShieldEnemy : EnemyBase
{
    [Header("이동")]
    public float moveSpeed = 2f;
    public float stopDistance = 2.5f;

    [Header("방패")]
    public Transform shieldTransform;
    public float shieldOffsetX = 0.6f;
    // 방패 왼쪽 고정
    private bool shieldOnRight = false;

    [Header("360도 탄막")]
    public GameObject bulletPrefab;
    public int bulletCount = 12;
    public float bulletSpeed = 5f;
    public float bulletDamage = 1f;
    public float attackCooldown = 3f;
    public float windupTime = 0.8f;

    private float attackTimer = 0f;
    private bool isAttacking = false;

    protected override void Start()
    {
        base.Start();
        attackTimer = attackCooldown * 0.5f;
        // 방패 방향 고정 (Start 시점에 한 번만 설정)
        UpdateShield();
    }

    void Update()
    {
        if (isDead || player == null) return;

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        FacePlayer();

        if (distToPlayer <= detectionRange)
        {
            // 플레이어에게 접근
            if (distToPlayer > stopDistance)
                MoveTowardPlayer();

            // 공격 타이머
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackCooldown && !isAttacking)
            {
                attackTimer = 0f;
                StartCoroutine(FanAttack());
            }
        }
    }

    /// <summary>
    /// 방패 위치를 왼쪽/오른쪽으로만 배치
    /// 위아래는 없고 X축만 이동
    /// </summary>
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

    /// <summary>몸통 스프라이트 플레이어 방향으로 뒤집기</summary>
    void FacePlayer()
    {
        if (spriteRenderer == null || player == null) return;
        // 플레이어가 왼쪽이면 왼쪽을 봄 (방패랑 무관)
        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    /// <summary>플레이어 방향으로 이동</summary>
    void MoveTowardPlayer()
    {
        float dirX = player.position.x > transform.position.x ? 1f : -1f;
        transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 데미지 받기 오버라이드
    /// 플레이어가 방패 방향(같은 X방향)에 있으면 막기
    /// 위아래나 반대쪽에서 오면 피격
    /// </summary>
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

    /// <summary>
    /// X축 기준으로만 방패 막기 판정
    /// 플레이어가 방패와 같은 방향(X축)에 있으면 막힘
    /// </summary>
    bool IsAttackBlocked()
    {
        if (player == null) return false;

        bool playerOnRight = player.position.x > transform.position.x;

        // 방패 방향과 플레이어 방향이 X축 기준으로 같으면 막힘
        return shieldOnRight == playerOnRight;
    }

    /// <summary>
    /// 부채꼴 탄막 공격
    /// 방패 방향으로 spreadAngle 만큼 퍼지게 발사
    /// </summary>
    IEnumerator FanAttack()
    {
        isAttacking = true;

        // 경고 연출
        if (spriteRenderer != null)
            spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(windupTime);

        if (isDead)
        {
            isAttacking = false;
            yield break;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        FireFanBullets();
        isAttacking = false;
    }

    /// <summary>360도 방사형 탄막 발사</summary>
    void FireFanBullets()
    {
        if (bulletPrefab == null) return;

        float angleStep = 360f / bulletCount;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = angleStep * i;
            Vector2 dir = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );

            GameObject bulletObj = Instantiate(
                bulletPrefab,
                transform.position,
                Quaternion.Euler(0f, 0f, angle)
            );

            Bullet bullet = bulletObj.GetComponent<Bullet>();
            if (bullet != null)
                bullet.Init(dir, bulletDamage, bulletSpeed);
        }
    }

    /// <summary>방패 막기 시 방패가 시안색으로 깜빡임</summary>
    IEnumerator ShieldBlockFlash()
    {
        if (shieldTransform == null) yield break;

        SpriteRenderer shieldSr = shieldTransform.GetComponent<SpriteRenderer>();
        if (shieldSr == null) yield break;

        Color original = shieldSr.color;
        shieldSr.color = Color.cyan;
        yield return new WaitForSeconds(0.12f);
        shieldSr.color = original;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 방패 방향 표시
        Gizmos.color = Color.blue;
        Vector3 dir = shieldOnRight ? Vector3.right : Vector3.left;
        Gizmos.DrawRay(transform.position, dir * 1.5f);

        // 정지 거리
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
