using UnityEngine;
using System.Collections;

/// <summary>
/// 드론 적
/// - 공중에 떠있으며 플레이어 감지 시 곡선 부채꼴 탄막 발사
/// - 천천히 플레이어 위쪽으로 호버링
/// </summary>
public class DroneEnemy : EnemyBase
{
    [Header("드론 - 탄막 (곡선 부채꼴)")]
    public GameObject droneBulletPrefab;    // 드론 전용 탄막 프리팹
    public Transform firePoint;
    public float bulletDamage = 1f;
    public float attackCooldown = 3f;

    [Space]
    [Header("부채꼴 설정")]
    public int bulletCount = 7;             // 발사 탄환 수
    public float spreadAngle = 80f;         // 부채꼴 총 각도 (좌우 40도)
    public float bulletSpeed = 4f;

    [Space]
    [Header("곡선 설정")]
    public float curveStrength = 0.3f;   // 0=직선, 0.3=살짝 휨 (Inspector에서 조절)
    public bool curveLeft = true;           // true: 왼쪽으로 휨, 교대 발동 가능

    [Header("드론 - 호버링")]
    public float hoverHeight = 3f;          // 플레이어 위 호버링 높이
    public float hoverSpeed = 1.5f;         // 이동 속도
    public float hoverAmplitude = 0.4f;     // 상하 부유 크기
    public float hoverFrequency = 1.5f;     // 부유 속도

    private float attackTimer = 0f;
    private float hoverTime = 0f;
    private Vector2 hoverTargetPos;

    protected override void Start()
    {
        base.Start();
        attackTimer = attackCooldown * 0.5f;
    }

    void Update()
    {
        if (isDead || player == null) return;

        hoverTime += Time.deltaTime;

        if (IsPlayerInDetectionRange())
        {
            // 플레이어 위쪽으로 호버링
            HoverAbovePlayer();

            // 공격 타이머
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackCooldown)
            {
                attackTimer = 0f;
                StartCoroutine(FireCurveFanPattern());
            }
        }
        else
        {
            // 감지 범위 밖이면 제자리 부유
            IdleHover();
        }

        // 플레이어 방향으로 스프라이트 뒤집기
        FacePlayer();
    }

    /// <summary>플레이어 위쪽으로 천천히 이동 + 상하 부유</summary>
    void HoverAbovePlayer()
    {
        // 목표 위치: 플레이어 바로 위
        float targetX = player.position.x;
        float targetY = player.position.y + hoverHeight;

        hoverTargetPos = new Vector2(targetX, targetY);

        // 상하 부유 오프셋
        float bobOffset = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;
        Vector2 finalTarget = hoverTargetPos + Vector2.up * bobOffset;

        // 부드럽게 이동
        transform.position = Vector2.MoveTowards(
            transform.position,
            finalTarget,
            hoverSpeed * Time.deltaTime
        );
    }

    /// <summary>감지 범위 밖일 때 제자리 부유</summary>
    void IdleHover()
    {
        float bobOffset = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;
        transform.position = new Vector3(
            transform.position.x,
            transform.position.y + bobOffset * Time.deltaTime,
            transform.position.z
        );
    }

    void FacePlayer()
    {
        if (spriteRenderer == null || player == null) return;
        spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    /// <summary>
    /// 곡선 부채꼴 탄막
    /// 플레이어 방향 기준으로 spreadAngle 만큼 퍼지게 동시 발사
    /// 각 탄환은 발사 방향 기준으로 좌 또는 우로 휘어서 날아감
    /// </summary>
    IEnumerator FireCurveFanPattern()
    {
        if (droneBulletPrefab == null || firePoint == null) yield break;

        Vector2 baseDir   = DirectionToPlayer();
        float   baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float   startAngle = baseAngle - spreadAngle / 2f;
        float   angleStep  = bulletCount > 1 ? spreadAngle / (bulletCount - 1) : 0f;

        // 교대로 좌우 곡선
        bool currentCurveLeft = curveLeft;
        curveLeft = !curveLeft;

        // 딜레이 없이 동시에 전부 발사
        for (int i = 0; i < bulletCount; i++)
        {
            float   angle = startAngle + angleStep * i;
            Vector2 dir   = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );

            GameObject bulletObj = Instantiate(
                droneBulletPrefab,
                firePoint.position,
                Quaternion.Euler(0f, 0f, angle)
            );

            CurveBullet cb = bulletObj.GetComponent<CurveBullet>();
            if (cb != null)
                cb.Init(dir, bulletDamage, bulletSpeed, curveStrength, currentCurveLeft);
        }

        yield break;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 호버 높이 표시
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(new Vector3(player.position.x, player.position.y + hoverHeight, 0), 0.3f);
        }
    }
}
