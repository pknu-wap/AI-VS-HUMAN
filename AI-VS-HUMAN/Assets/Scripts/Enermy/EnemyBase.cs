// 일반 적들이 공통으로 사용하는 체력, 감지, 피격, 사망 연출을 담당하는 기반 클래스
// SoldierEnemy, ShieldEnemy, DroneEnemy는 이 클래스를 상속해서 개별 공격 패턴만 구현한다.
using UnityEngine;
using System.Collections;

// 모든 적이 공통으로 사용하는 기본 클래스
//
// 담당 기능:
// - 체력 관리
// - 플레이어 감지 거리 계산
// - 플레이어 방향 계산
// - 피격 시 빨간색 깜빡임
// - 사망 시 충돌 비활성화
// - 사망 후 페이드아웃 삭제
//
// SoldierEnemy, ShieldEnemy 같은 적들은 이 클래스를 상속해서 사용한다.
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("기본 스탯")]
    public float maxHp = 30f;
    public float fadeDuration = 1.5f;

    [Header("감지 범위")]
    public float detectionRange = 8f;
    public float attackRange = 6f;
    public LayerMask obstacleLayer;

    // 자식 클래스에서도 사용할 수 있는 공통 정보
    protected float currentHp;
    protected Transform player;
    protected bool isDead = false;
    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;

    // 내부에서만 사용하는 컴포넌트와 코루틴
    private Rigidbody2D rb;
    private Collider2D col;
    private Coroutine hitFlashCoroutine;

    // 피격 색상 유지 시간
    private const float HitFlashDuration = 0.1f;

    protected virtual void Start()
    {
        InitStats();
        CacheComponents();
        CachePlayer();
        SetupRigidbody();
    }

    // 체력 같은 기본 수치를 초기화한다.
    private void InitStats()
    {
        currentHp = maxHp;
    }

    // 자주 사용하는 컴포넌트를 미리 찾아 저장한다.
    // 매번 GetComponent를 부르지 않기 위한 최적화.
    private void CacheComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
    }

    // 플레이어 Transform을 한 번만 찾아 저장한다.
    private void CachePlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
            player = playerObj.transform;
    }

    // 적의 Rigidbody 설정을 정리한다.
    // 중력이 있는 적은 불필요하게 떨어지지 않도록 Kinematic으로 바꾼다.
    private void SetupRigidbody()
    {
        if (rb == null) return;

        if (rb.gravityScale > 0f)
            rb.bodyType = RigidbodyType2D.Kinematic;
    }

    // 플레이어가 감지 범위 안에 있는지 확인한다.
    protected bool IsPlayerInDetectionRange()
    {
        if (player == null) return false;

        float distance = Vector2.Distance(transform.position, player.position);
        return distance <= detectionRange;
    }

    // 플레이어가 공격 범위 안에 있는지 확인한다.
    protected bool IsPlayerInAttackRange()
    {
        if (player == null) return false;

        float distance = Vector2.Distance(transform.position, player.position);
        return distance <= attackRange;
    }

    // 현재 적 위치에서 플레이어를 향하는 방향을 반환한다.
    // 플레이어가 없으면 Vector2.zero를 반환한다.
    protected Vector2 DirectionToPlayer()
    {
        if (player == null) return Vector2.zero;

        return ((Vector2)player.position - (Vector2)transform.position).normalized;
    }

    // 데미지를 받았을 때 호출된다.
    // IDamageable 인터페이스 때문에 public이어야 한다.
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;

        if (currentHp <= 0f)
        {
            Die();
            return;
        }

        PlayHitFlash();
    }

    // 피격 시 빨간색으로 잠깐 깜빡이게 한다.
    // 이미 깜빡이는 중이면 기존 코루틴을 멈추고 다시 시작한다.
    private void PlayHitFlash()
    {
        if (spriteRenderer == null) return;

        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        spriteRenderer.color = Color.red;

        yield return new WaitForSeconds(HitFlashDuration);

        if (!isDead && spriteRenderer != null)
            spriteRenderer.color = originalColor;

        hitFlashCoroutine = null;
    }

    // 적 사망 처리.
    // 자식 클래스에서 특별한 사망 연출이 필요하면 override해서 확장할 수 있다.
    protected virtual void Die()
    {
        if (isDead) return;

        isDead = true;

        StopHitFlash();
        DisableCollider();
        StopPhysics();

        StartCoroutine(FadeOutAndDestroy());
    }

    // 피격 깜빡임 코루틴을 정리하고 색상을 원래대로 돌린다.
    private void StopHitFlash()
    {
        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
    }

    // 죽은 적이 더 이상 공격이나 충돌 판정을 하지 않도록 콜라이더를 끈다.
    private void DisableCollider()
    {
        if (col != null)
            col.enabled = false;
    }

    // 죽은 적의 물리 움직임을 정지한다.
    private void StopPhysics()
    {
        if (rb == null) return;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    // 사망 후 서서히 투명해진 뒤 오브젝트를 삭제한다.
    private IEnumerator FadeOutAndDestroy()
    {
        if (spriteRenderer == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Color startColor = spriteRenderer.color;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }

        Destroy(gameObject);
    }

    // Unity 에디터에서 적의 감지 범위와 공격 범위를 시각적으로 보여준다.
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
