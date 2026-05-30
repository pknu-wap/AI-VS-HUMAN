using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Shadow 몬스터 - Celeste Badeline 방식 (수정 버전)
/// - Rigidbody2D를 Kinematic으로 변경하여 물리 충돌로 인한 엉킴 방지
/// - rb.MovePosition을 사용하여 물리 보간과 함께 부드러운 위치 수렴
/// - 미세한 딜레이 오프셋을 추가하여 여러 마리가 완전히 하나로 겹치는 현상 방지
/// </summary>
public class ShadowEnemy : MonoBehaviour
{
    [Header("설정")]
    public float recordDelay  = 3f;     // 몇 초 전 경로를 따라갈지
    public float damage       = 1f;
    public float detectRadius = 0.4f;
    public float fadeInTime   = 1f;

    private struct PositionRecord
    {
        public Vector3 position;
        public float   time;
        public PositionRecord(Vector3 pos, float t) { position = pos; time = t; }
    }

    private Queue<PositionRecord> _records = new Queue<PositionRecord>();

    private Transform      player;
    private Rigidbody2D    rb;
    private SpriteRenderer sr;
    private bool           isDead          = false;
    private bool           isInitialized   = false;
    private bool           canDamagePlayer = false;
    private LayerMask      playerMask;
    private Color          originalColor;

    void Start()
    {
        sr            = GetComponent<SpriteRenderer>();
        rb            = GetComponent<Rigidbody2D>();
        player        = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerMask    = LayerMask.GetMask("Player");
        originalColor = sr != null ? sr.color : Color.white;

        if (rb != null)
        {
            // [수정] Dynamic에서 Kinematic으로 변경
            // 물리 엔진에 의해 밀려나거나 엉키는 현상을 원천 차단합니다.
            rb.bodyType               = RigidbodyType2D.Kinematic;
            rb.constraints            = RigidbodyConstraints2D.FreezeRotation;
            rb.simulated              = true;
        }

        // [수정] 여러 마리가 수학적으로 완벽히 일치하는 좌표를 가지는 것을 방지하기 위해 
        // 각 인스턴스마다 미세한 시간 차이(오프셋)를 부여합니다.
        recordDelay += Random.Range(-0.03f, 0.03f);

        if (sr != null)
            sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        StartCoroutine(FadeIn());
        StartCoroutine(ActivateAfterDelay());
    }

    // ── 페이드인 ────────────────────────────────
    IEnumerator FadeIn()
    {
        for (float t = 0f; t < fadeInTime; t += Time.deltaTime)
        {
            if (sr != null)
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b,
                                     Mathf.Lerp(0f, 1f, t / fadeInTime));
            yield return null;
        }
        if (sr != null)
            sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
    }

    // ── recordDelay 후 피격 활성화 ──────────────
    IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(recordDelay);
        if (!isDead) canDamagePlayer = true;
    }

    // ── 경로 기록 + 플레이어 감지 ───────────────
    void Update()
    {
        if (player == null || isDead) return;

        _records.Enqueue(new PositionRecord(player.position, Time.time));

        if (!canDamagePlayer) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerMask);
        if (hit == null) return;

        PlayerHealth ph = hit.GetComponent<PlayerHealth>();
        if (ph == null) return;

        ph.TakeDamage((int)damage);
        isDead = true;
        StartCoroutine(Die());
    }

    // ── 경로 재생 ───────────────────────────────
    void FixedUpdate()
    {
        if (rb == null || isDead) return;
        if (_records.Count == 0) return;

        Vector3 targetPos = transform.position;
        bool hasTarget = false;

        // [수정] recordDelay만큼 시간이 지난 기록 중 가장 최근 데이터를 추출
        while (_records.Count > 0 && Time.time - _records.Peek().time >= recordDelay)
        {
            targetPos = _records.Dequeue().position;
            hasTarget = true;
        }

        // 이번 물리 프레임에 갱신할 타겟 좌표가 없다면 이동 단계를 건너뜁니다.
        if (!hasTarget) return;

        if (!isInitialized)
        {
            isInitialized      = true;
            rb.position        = targetPos;
        }
        else
        {
            // [수정] 기존 linearVelocity 속도 주입 방식을 버리고 MovePosition을 사용합니다.
            // 인스펙터 창에서 Rigidbody2D의 Interpolate(보간)를 'Interpolate'로 설정하면 더욱 부드럽게 움직입니다.
            rb.MovePosition(targetPos);
        }

        if (sr != null && player != null)
            sr.flipX = player.position.x < transform.position.x;
    }

    // ── 사망 ────────────────────────────────────
    IEnumerator Die()
    {
        // [수정] Kinematic이므로 속도를 제로로 만드는 대신 물리 시뮬레이션을 정지하거나 그대로 둡니다.
        float fadeDuration = 0.5f;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            if (sr != null)
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b,
                                     Mathf.Lerp(1f, 0f, t / fadeDuration));
            yield return null;
        }
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}