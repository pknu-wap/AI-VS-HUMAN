// 보스가 소환하는 회복 드론의 이동, 피격, 제한 시간, 사망 처리를 담당하는 스크립트
// 드론이 제한 시간까지 살아남으면 보스를 회복시키고, 플레이어에게 파괴되면 회복 없이 사라진다.
using UnityEngine;
using System.Collections;

public class HealDrone : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    public float maxHp        = 50f;
    public float fadeDuration = 1f;

    [Header("이동")]
    public float moveSpeed       = 2.5f;
    public float hoverAmplitude  = 0.4f;
    public float hoverFrequency  = 1.5f;
    public float swayAmplitude   = 1.5f;
    public float swayFrequency   = 0.8f;

    [Header("자유 이동")]
    public float freeMoveRadius     = 3f;
    public float changeTargetDelay  = 1.5f;
    public float uMoveWidth         = 2.5f;
    public float uMoveHeight        = 1.8f;
    public float uMoveSpeed         = 1.8f;

    [Header("벽 회피")]
    public float wallCheckDistance = 1.2f;
    public float wallSafeDistance  = 1.5f;
    public float wallAvoidSpeed    = 4f;
    public float spawnCheckRadius  = 0.35f;
    public LayerMask groundMask;

    private float currentHp;
    private bool isDead = false;
    private bool isDoingUMove = false;

    private BossDrone boss;
    private Vector3 basePos;
    private Vector3 moveTarget;

    private float hoverTime = 0f;
    private float swayTime  = 0f;
    private float targetTimer = 0f;

    private SpriteRenderer sr;
    private Coroutine hitFlashCoroutine;

    public void Init(BossDrone bossRef)
    {
        // 보스가 런타임에 생성한 뒤 자신을 넘겨주면, 드론은 이 참조로 회복/사망 결과를 알려준다.
        boss = bossRef;

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");

        currentHp = maxHp;
        sr = GetComponent<SpriteRenderer>();

        basePos = FindSafeSpawnPosition(transform.position);
        transform.position = basePos;

        PickNewMoveTarget();
    }

    void Awake()
    {
        currentHp = maxHp;
        sr = GetComponent<SpriteRenderer>();

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    void Update()
    {
        if (isDead) return;

        hoverTime += Time.deltaTime;
        swayTime  += Time.deltaTime;
        targetTimer += Time.deltaTime;

        if (IsTooCloseToWall())
        {
            MoveAwayFromWall();
            return;
        }

        if (!isDoingUMove && targetTimer >= changeTargetDelay)
        {
            targetTimer = 0f;

            if (Random.value < 0.45f)
                StartCoroutine(UMove());
            else
                PickNewMoveTarget();
        }

        if (!isDoingUMove)
            FreeMove();
    }

    void FreeMove()
    {
        // 기본 이동은 목표 지점으로 이동하면서 상하 부유와 좌우 흔들림을 더한다.
        float bob = Mathf.Sin(hoverTime * hoverFrequency) * hoverAmplitude;
        float sway = Mathf.Sin(swayTime * swayFrequency) * swayAmplitude * 0.25f;

        Vector3 target = moveTarget + new Vector3(sway, bob, 0f);

        Vector3 nextPos = Vector3.MoveTowards(
            transform.position,
            target,
            moveSpeed * Time.deltaTime);

        if (!WouldHitWall(nextPos))
            transform.position = nextPos;
        else
            PickNewMoveTarget();
    }

    IEnumerator UMove()
    {
        // 가끔 U자 모양으로 움직여서 단순 직선 이동보다 예측하기 어렵게 만든다.
        isDoingUMove = true;

        Vector3 start = transform.position;

        float dirX = Random.value < 0.5f ? -1f : 1f;

        if (Physics2D.Raycast(transform.position, Vector2.right, wallCheckDistance, groundMask))
            dirX = -1f;
        else if (Physics2D.Raycast(transform.position, Vector2.left, wallCheckDistance, groundMask))
            dirX = 1f;

        Vector3 p1 = start + new Vector3(dirX * uMoveWidth * 0.5f, -uMoveHeight, 0f);
        Vector3 p2 = start + new Vector3(dirX * uMoveWidth, 0f, 0f);

        yield return MoveToPoint(p1);
        yield return MoveToPoint(p2);

        basePos = transform.position;
        PickNewMoveTarget();

        isDoingUMove = false;
    }

    IEnumerator MoveToPoint(Vector3 target)
    {
        while (!isDead && Vector3.Distance(transform.position, target) > 0.05f)
        {
            if (IsTooCloseToWall())
            {
                MoveAwayFromWall();
                yield return null;
                continue;
            }

            Vector3 nextPos = Vector3.MoveTowards(
                transform.position,
                target,
                uMoveSpeed * Time.deltaTime);

            if (WouldHitWall(nextPos))
                yield break;

            transform.position = nextPos;
            yield return null;
        }
    }

    void PickNewMoveTarget()
    {
        // 보스 근처를 중심으로 안전한 랜덤 이동 지점을 고른다.
        Vector2 randomOffset = Random.insideUnitCircle * freeMoveRadius;

        Vector3 center = basePos;

        if (boss != null)
            center = Vector3.Lerp(basePos, boss.transform.position, 0.35f);

        Vector3 candidate = center + new Vector3(randomOffset.x, randomOffset.y, 0f);

        moveTarget = FindSafeSpawnPosition(candidate);
    }

    bool IsTooCloseToWall()
    {
        return Physics2D.Raycast(transform.position, Vector2.right, wallCheckDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.left,  wallCheckDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.up,    wallCheckDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.down,  wallCheckDistance, groundMask);
    }

    void MoveAwayFromWall()
    {
        Vector2 avoidDir = Vector2.zero;

        RaycastHit2D rightHit = Physics2D.Raycast(transform.position, Vector2.right, wallCheckDistance, groundMask);
        RaycastHit2D leftHit  = Physics2D.Raycast(transform.position, Vector2.left,  wallCheckDistance, groundMask);
        RaycastHit2D upHit    = Physics2D.Raycast(transform.position, Vector2.up,    wallCheckDistance, groundMask);
        RaycastHit2D downHit  = Physics2D.Raycast(transform.position, Vector2.down,  wallCheckDistance, groundMask);

        if (rightHit.collider != null) avoidDir += Vector2.left;
        if (leftHit.collider  != null) avoidDir += Vector2.right;
        if (upHit.collider    != null) avoidDir += Vector2.down;
        if (downHit.collider  != null) avoidDir += Vector2.up;

        if (avoidDir == Vector2.zero) return;

        Vector3 nextPos = transform.position + (Vector3)(avoidDir.normalized * wallAvoidSpeed * Time.deltaTime);

        if (!WouldHitWall(nextPos))
        {
            transform.position = nextPos;
            basePos = transform.position;
            moveTarget = transform.position;
        }
    }

    bool WouldHitWall(Vector3 position)
    {
        return Physics2D.OverlapCircle(position, spawnCheckRadius, groundMask) != null;
    }

    Vector3 FindSafeSpawnPosition(Vector3 wantedPos)
    {
        // 원하는 위치가 벽과 겹치면 주변 방향을 넓혀가며 안전한 위치를 찾는다.
        if (!WouldHitWall(wantedPos) && HasEnoughWallDistance(wantedPos))
            return wantedPos;

        Vector3[] directions = new Vector3[]
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            new Vector3(1f, 1f, 0f).normalized,
            new Vector3(-1f, 1f, 0f).normalized,
            new Vector3(1f, -1f, 0f).normalized,
            new Vector3(-1f, -1f, 0f).normalized,
        };

        for (int distanceStep = 1; distanceStep <= 8; distanceStep++)
        {
            float distance = wallSafeDistance * distanceStep * 0.5f;

            foreach (Vector3 dir in directions)
            {
                Vector3 candidate = wantedPos + dir * distance;

                if (!WouldHitWall(candidate) && HasEnoughWallDistance(candidate))
                    return candidate;
            }
        }

        return wantedPos;
    }

    bool HasEnoughWallDistance(Vector3 position)
    {
        return !Physics2D.Raycast(position, Vector2.right, wallSafeDistance, groundMask)
            && !Physics2D.Raycast(position, Vector2.left,  wallSafeDistance, groundMask)
            && !Physics2D.Raycast(position, Vector2.up,    wallSafeDistance, groundMask)
            && !Physics2D.Raycast(position, Vector2.down,  wallSafeDistance, groundMask);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;

        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(HitFlash());

        if (currentHp <= 0f)
            StartCoroutine(Die(false));
    }

    public void OnTimerExpired()
    {
        if (isDead) return;

        StartCoroutine(Die(true));
    }

    IEnumerator HitFlash()
    {
        if (sr == null) yield break;

        sr.color = Color.red;

        yield return new WaitForSeconds(0.1f);

        if (!isDead)
            sr.color = Color.white;

        hitFlashCoroutine = null;
    }

    IEnumerator Die(bool healBoss)
    {
        // healBoss가 true면 제한 시간 생존으로 처리되어 보스에게 회복 결과를 전달한다.
        isDead = true;

        if (boss != null)
            boss.OnHealDroneDestroyed(healBoss);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, a);

            yield return null;
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, wallSafeDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, freeMoveRadius);
    }
}
