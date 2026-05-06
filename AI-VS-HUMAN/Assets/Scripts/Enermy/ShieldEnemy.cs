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

        if (IsAttackBlocked())
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

    // 360도 방사형 탄막을 발사한다.
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

    // 방패로 공격을 막았을 때 방패를 시안색으로 잠깐 깜빡인다.
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
