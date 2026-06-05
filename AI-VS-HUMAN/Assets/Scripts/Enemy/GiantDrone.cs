// 거대 드론 보스의 이동, 탄막 패턴, 회복 드론 소환, 체력 UI, 페이즈 이벤트를 담당하는 스크립트
// 플레이어를 감지하면 패턴 루프를 시작하고, 체력이 절반 이하가 되면 1스테이지 보스룸 컨트롤러에 이벤트를 보낸다.
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Action = System.Action;

// 거대 드론 보스
// - 플레이어 감지 후 패턴 시작
// - 베지에 곡선을 이용한 부드러운 U자 돌진
// - 돌진 중 설정된 딜레이 간격으로 부채꼴 탄막 발사
// - 고정된 Y축 높이 유지 (벽 회피 및 플레이어 Y추적 제거)
[RequireComponent(typeof(GiantDronePhase1))]
[RequireComponent(typeof(GiantDronePhase2))]
[RequireComponent(typeof(GiantDroneUDashPattern))]
[RequireComponent(typeof(GiantDronePetalPattern))]
[RequireComponent(typeof(GiantDroneHomingMissilePattern))]
[RequireComponent(typeof(GiantDroneHealDronePattern))]
public class GiantDrone : MonoBehaviour, IDamageable
{
    private const float DefaultFadeDuration = 2f;
    private const float DefaultDetectionRange = 25f;
    private const float DefaultHoverAmplitude = 0.4f;
    private const float DefaultHoverFrequency = 1.2f;
    private const float DefaultWallAvoidDistance = 1.8f;
    private const float DefaultWallAvoidSpeed = 5f;
    private const float DefaultWallCheckRadius = 0.45f;
    private const float DefaultWallStopPadding = 0.2f;
    private const float DefaultWallUnstuckPadding = 0.05f;
    private const float DefaultWallSafeStepDistance = 0.2f;
    private const int DefaultWallResolveIterations = 4;
    private const float DefaultCameraEdgePadding = 0.5f;
    private const float DefaultHpBarHeight = 26f;
    private static readonly Color DefaultHpBarColor = new Color(0.9f, 0.1f, 0.1f);

    [Header("체력")]
    public float maxHp = 600f;
    internal float currentHp;

    public event Action HalfHealthReached;
    public event Action Died;
    public float CurrentHp => currentHp;
    public float HealthRatio => maxHp <= 0f ? 0f : currentHp / maxHp;

    [Header("체력바")]
    public float hpBarPosY = -425f;

    [Header("사망")]
    public bool deactivateOnDeathInsteadOfDestroy;

    internal int   healDroneAliveCount = 0;

    internal Transform      player;
    internal bool           isDead       = false;
    internal bool           isActive     = false;
    internal bool           isDoingUDash = false;
    internal bool           isDoingPetal = false;
    internal bool           halfHealthNotified = false;
    internal float          hoverTime    = 0f;
    internal float          swayTime     = 0f;
    internal float          swayBaseX    = 0f;
    internal float          baseY        = 0f;
    internal float          petalBaseAngle = 0f;
    internal SpriteRenderer spriteRenderer;
    internal Rigidbody2D    rb;
    internal Collider2D     bossCollider;
    internal Camera         mainCamera;
    internal Coroutine      hitFlashCoroutine;
    internal Slider         hpSlider;
    internal Canvas         bossCanvas;
    internal Color          originalColor;
    internal bool           hasOriginalColor;
    [Header("페이즈")]
    [SerializeField] internal GiantDronePhase1 phase1;
    [SerializeField] internal GiantDronePhase2 phase2;

    [Header("패턴")]
    [SerializeField] internal GiantDroneUDashPattern uDashPattern;
    [SerializeField] internal GiantDronePetalPattern petalPattern;
    [SerializeField] internal GiantDroneHomingMissilePattern homingMissilePattern;
    [SerializeField] internal GiantDroneHealDronePattern healDronePattern;
    private readonly Collider2D[] wallOverlapHits = new Collider2D[8];
    private readonly RaycastHit2D[] wallCastHits = new RaycastHit2D[8];
    internal Vector3        lastSafePosition;

    private float WallAvoidDistance => DefaultWallAvoidDistance;
    private float WallAvoidSpeed => DefaultWallAvoidSpeed;
    private float WallCheckRadius => DefaultWallCheckRadius;
    private float WallStopPadding => DefaultWallStopPadding;
    private float WallUnstuckPadding => DefaultWallUnstuckPadding;
    private float WallSafeStepDistance => DefaultWallSafeStepDistance;
    private int WallResolveIterations => DefaultWallResolveIterations;
    private bool KeepInsideCameraView => false;
    private float CameraEdgePadding => DefaultCameraEdgePadding;

    void Start()
    {
        // 씬에 남아 있을 수 있는 이전 회복 드론을 정리하고 보스 상태를 초기화한다.
        ClearExistingHealDrones();

        currentHp      = maxHp;
        halfHealthNotified = currentHp <= maxHp * 0.5f;
        CacheComponents();
        ConfigureRigidbody();
        mainCamera = Camera.main;
        baseY = transform.position.y;
        lastSafePosition = transform.position;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        ClearExistingHealDrones();
        if (bossCanvas == null)
            CreateHpBarUI();
        UpdateHpBar();

        if (bossCanvas != null) bossCanvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (phase1 != null)
            phase1.Tick(this);
    }

    private void LateUpdate()
    {
        if (phase1 != null)
            phase1.LateTick(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            CacheComponents();
    }
#endif

    public void PrepareForBossRoomSpawn(Vector3 spawnPosition, Camera bossRoomCamera = null)
    {
        // 비활성화된 씬 보스를 입장 순간 켜면 Start보다 먼저 체력 체크가 일어날 수 있으므로, 소환 직후 필요한 상태를 즉시 맞춘다.
        CacheComponents();
        StopCombatComponents();
        ConfigureRigidbody();
        MoveVisualCenterTo(spawnPosition);
        currentHp = maxHp;
        halfHealthNotified = false;
        isDead = false;
        isActive = false;
        isDoingUDash = false;
        isDoingPetal = false;
        healDroneAliveCount = 0;
        hoverTime = 0f;
        swayTime = 0f;
        swayBaseX = transform.position.x;
        baseY = transform.position.y;
        lastSafePosition = transform.position;
        mainCamera = bossRoomCamera != null ? bossRoomCamera : Camera.main;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (bossCanvas == null)
            CreateHpBarUI();

        UpdateHpBar();

        if (bossCanvas != null)
            bossCanvas.gameObject.SetActive(false);
    }

    public void PrepareForBossRoomActivation(Camera bossRoomCamera = null)
    {
        // 씬에 배치된 보스를 그대로 켤 때는 에디터 위치를 유지한 채 전투 상태만 초기화한다.
        CacheComponents();
        StopCombatComponents();
        ConfigureRigidbody();
        currentHp = maxHp;
        halfHealthNotified = false;
        isDead = false;
        isActive = false;
        isDoingUDash = false;
        isDoingPetal = false;
        healDroneAliveCount = 0;
        hoverTime = 0f;
        swayTime = 0f;
        swayBaseX = transform.position.x;
        baseY = transform.position.y;
        lastSafePosition = transform.position;
        mainCamera = bossRoomCamera != null ? bossRoomCamera : Camera.main;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
        }

        if (bossCollider != null)
            bossCollider.enabled = true;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (bossCanvas == null)
            CreateHpBarUI();

        UpdateHpBar();

        if (bossCanvas != null)
            bossCanvas.gameObject.SetActive(false);
    }

    public void ResetForBossRoomRetry()
    {
        // 플레이어가 죽어 보스전을 다시 시작해야 할 때 보스의 런타임 상태와 UI를 입장 전 상태로 되돌립니다.
        StopAllCoroutines();
        StopCombatComponents();
        ClearExistingHealDrones();

        currentHp = maxHp;
        halfHealthNotified = false;
        isDead = false;
        isActive = false;
        isDoingUDash = false;
        isDoingPetal = false;
        healDroneAliveCount = 0;
        hoverTime = 0f;
        swayTime = 0f;
        swayBaseX = transform.position.x;
        baseY = transform.position.y;
        lastSafePosition = transform.position;
        petalBaseAngle = 0f;

        if (hitFlashCoroutine != null)
            hitFlashCoroutine = null;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
        }

        if (bossCollider != null)
            bossCollider.enabled = true;

        ConfigureRigidbody();
        DestroyBossCanvas();
    }

    public void TakeDamage(float damage)
    {
        // 데미지를 받은 뒤 체력바를 갱신하고, 절반 체력 이벤트가 필요한지 확인한다.
        if (isDead) return;
        currentHp = Mathf.Clamp(currentHp - damage, 0f, maxHp);
        UpdateHpBar();
        CheckHalfHealthReached();

        if (currentHp <= 0f)
        {
            StartDeath();
            return;
        }

        if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());
    }

    IEnumerator HitFlash()
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!isDead) spriteRenderer.color = originalColor;
        hitFlashCoroutine = null;
    }

    private void StartDeath()
    {
        if (isDead)
            return;

        isDead = true;
        Died?.Invoke();
        StopAllCoroutines();
        StopCombatComponents();
        StartCoroutine(Die());
    }

    IEnumerator Die()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (bossCollider != null) bossCollider.enabled = false;
        DestroyBossCanvas();
        ClearExistingHealDrones();

        float elapsed = 0f;
        Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        while (elapsed < DefaultFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / DefaultFadeDuration);
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        if (deactivateOnDeathInsteadOfDestroy)
        {
            gameObject.SetActive(false);
            yield break;
        }

        Destroy(gameObject);
    }

    private void CheckHalfHealthReached()
    {
        // 체력 절반 이벤트는 한 번만 발생해야 보스룸 2페이즈가 중복 시작되지 않는다.
        if (halfHealthNotified)
            return;

        if (currentHp > maxHp * 0.5f)
            return;

        halfHealthNotified = true;
        HalfHealthReached?.Invoke();
        if (phase2 != null)
            phase2.StartPhase(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        float range = phase1 != null ? phase1.detectionRange : DefaultDetectionRange;
        Gizmos.DrawWireSphere(transform.position, range);
    }


    public void FollowBossRoomCamera(Camera bossRoomCamera, float cameraYOffset, float followSpeed)
    {
        // 2페이즈에서 카메라가 위로 올라갈 때 보스의 기준 높이도 함께 올려 화면 안에 머물게 한다.
        if (isDead || bossRoomCamera == null)
            return;

        mainCamera = bossRoomCamera;
        baseY = bossRoomCamera.transform.position.y + cameraYOffset;

        if (isDoingUDash)
            return;

        float currentHoverFrequency = phase1 != null ? phase1.HoverFrequency : DefaultHoverFrequency;
        float currentHoverAmplitude = phase1 != null ? phase1.HoverAmplitude : DefaultHoverAmplitude;
        float targetY = baseY + Mathf.Sin(hoverTime * currentHoverFrequency) * currentHoverAmplitude;
        float nextY = Mathf.MoveTowards(transform.position.y, targetY, Mathf.Max(0f, followSpeed) * Time.deltaTime);
        Vector3 nextPosition = new Vector3(transform.position.x, nextY, transform.position.z);

        MoveToSafePosition(nextPosition, LayerMask.GetMask("Ground"));
    }

    private void CacheComponents()
    {
        // 비활성화 상태에서 다시 켜질 때도 소환 보정에 필요한 컴포넌트를 즉시 확보한다.
        phase1 = GetOrAddComponent(phase1);
        phase2 = GetOrAddComponent(phase2);
        uDashPattern = GetOrAddComponent(uDashPattern);
        petalPattern = GetOrAddComponent(petalPattern);
        homingMissilePattern = GetOrAddComponent(homingMissilePattern);
        healDronePattern = GetOrAddComponent(healDronePattern);

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null && !hasOriginalColor)
        {
            originalColor = spriteRenderer.color;
            hasOriginalColor = true;
        }

        if (bossCollider == null)
            bossCollider = GetComponent<Collider2D>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }

    private T GetOrAddComponent<T>(T current) where T : Component
    {
        if (current != null)
            return current;

        T existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private void StopCombatComponents()
    {
        if (phase1 != null)
            phase1.StopPhase();

        if (phase2 != null)
            phase2.StopPhase();

        if (uDashPattern != null)
            uDashPattern.StopPattern();

        if (petalPattern != null)
            petalPattern.StopPattern();

        if (homingMissilePattern != null)
            homingMissilePattern.StopPattern();

        if (healDronePattern != null)
            healDronePattern.StopPattern();

        isDoingUDash = false;
        isDoingPetal = false;
    }

    private void ConfigureRigidbody()
    {
        // 소환 직후 물리엔진이 보스를 벽이나 바닥 쪽으로 밀지 않도록 보스는 직접 이동 방식에 맞춰 Kinematic으로 고정한다.
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void MoveVisualCenterTo(Vector3 centerPosition)
    {
        // Transform 피벗이 스프라이트 중심과 달라도, 인스펙터의 소환 좌표가 보스의 실제 중심으로 작동하게 한다.
        transform.position = centerPosition;
        Physics2D.SyncTransforms();

        Vector3 visualCenter = GetBossVisualCenter();
        Vector3 offset = visualCenter - transform.position;
        transform.position -= offset;
        Physics2D.SyncTransforms();
    }

    internal Vector3 GetBossVisualCenter()
    {
        if (bossCollider != null)
            return bossCollider.bounds.center;

        if (spriteRenderer != null)
            return spriteRenderer.bounds.center;

        return transform.position;
    }

    internal void ClearExistingHealDrones()
    {
        HealDrone[] drones = FindObjectsByType<HealDrone>(FindObjectsSortMode.None);
        foreach (HealDrone drone in drones)
        {
            if (drone != null) Destroy(drone.gameObject);
        }
        healDroneAliveCount = 0;
    }

    Vector3 GetSafePosition(Vector3 wantedPosition, LayerMask groundMask)
    {
        Vector2 avoidDir = Vector2.zero;

        if (Physics2D.Raycast(wantedPosition, Vector2.right, WallAvoidDistance, groundMask))
            avoidDir += Vector2.left;

        if (Physics2D.Raycast(wantedPosition, Vector2.left, WallAvoidDistance, groundMask))
            avoidDir += Vector2.right;

        if (Physics2D.Raycast(wantedPosition, Vector2.up, WallAvoidDistance, groundMask))
            avoidDir += Vector2.down;

        if (Physics2D.Raycast(wantedPosition, Vector2.down, WallAvoidDistance, groundMask))
            avoidDir += Vector2.up;

        if (Physics2D.OverlapCircle(wantedPosition, WallCheckRadius, groundMask) != null)
            avoidDir += ((Vector2)transform.position - (Vector2)wantedPosition).normalized;

        if (avoidDir != Vector2.zero)
        {
            Vector3 safePosition = wantedPosition + (Vector3)(avoidDir.normalized * WallAvoidSpeed * Time.deltaTime);

            if (Physics2D.OverlapCircle(safePosition, WallCheckRadius, groundMask) == null)
            {
                swayBaseX = safePosition.x;
                return safePosition;
            }

            return transform.position;
        }

        return wantedPosition;
    }

    internal bool MoveToSafePosition(Vector3 wantedPosition, LayerMask wallMask)
    {
        // Transform 직접 이동과 물리 쿼리가 어긋나지 않도록 현재 위치를 물리 월드에 먼저 반영한다.
        Physics2D.SyncTransforms();

        if (IsOverlappingWall(wallMask))
        {
            bool escaped = ResolveCurrentWallOverlap(wallMask);
            Physics2D.SyncTransforms();

            if (!escaped || IsOverlappingWall(wallMask))
            {
                transform.position = lastSafePosition;
                Physics2D.SyncTransforms();
                return false;
            }
        }

        Vector3 startPosition = transform.position;
        float moveDistance = Vector2.Distance(startPosition, wantedPosition);
        float stepDistance = Mathf.Max(0.05f, WallSafeStepDistance);
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(moveDistance / stepDistance));

        for (int i = 1; i <= stepCount; i++)
        {
            Vector3 stepTarget = Vector3.Lerp(startPosition, wantedPosition, i / (float)stepCount);
            Vector3 safePosition = GetWallLimitedPosition(stepTarget, wallMask);
            bool reachedStepTarget = ((Vector2)safePosition - (Vector2)stepTarget).sqrMagnitude < 0.001f;

            transform.position = safePosition;
            Physics2D.SyncTransforms();

            bool wasUnstuck = ResolveCurrentWallOverlap(wallMask);
            if (wasUnstuck)
                Physics2D.SyncTransforms();

            if (IsOverlappingWall(wallMask))
            {
                transform.position = lastSafePosition;
                Physics2D.SyncTransforms();
                return false;
            }

            lastSafePosition = transform.position;
            swayBaseX = transform.position.x;

            if (!reachedStepTarget || wasUnstuck)
                return false;
        }

        return true;
    }

    private Vector3 GetWallLimitedPosition(Vector3 wantedPosition, LayerMask wallMask)
    {
        // 실제 보스 Collider2D를 Cast해서 복잡한 콜라이더/코너에서도 벽에 닿기 직전에 멈춘다.
        Vector2 move = (Vector2)wantedPosition - (Vector2)transform.position;
        float distance = move.magnitude;

        if (distance <= 0.001f)
            return GetOverlapSafePosition(wantedPosition, wallMask);

        Vector2 direction = move / distance;
        RaycastHit2D wallHit = GetNearestWallCastHit(direction, distance + WallStopPadding, wallMask);

        if (wallHit.collider != null)
        {
            float safeDistance = Mathf.Max(0f, wallHit.distance - WallStopPadding);
            Vector3 blockedPosition = transform.position + (Vector3)(direction * safeDistance);
            blockedPosition.z = wantedPosition.z;
            return GetOverlapSafePosition(blockedPosition, wallMask);
        }

        return GetOverlapSafePosition(wantedPosition, wallMask);
    }

    private RaycastHit2D GetNearestWallCastHit(Vector2 direction, float distance, LayerMask wallMask)
    {
        if (bossCollider == null)
            return Physics2D.BoxCast(GetBossColliderCenter(), GetBossCastSize(), 0f, direction, distance, wallMask);

        ContactFilter2D wallFilter = new ContactFilter2D();
        wallFilter.SetLayerMask(wallMask);
        wallFilter.useTriggers = false;

        int hitCount = bossCollider.Cast(direction, wallFilter, wallCastHits, distance);
        RaycastHit2D nearestHit = default;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = wallCastHits[i];
            if (hit.collider == null || hit.collider == bossCollider)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        return nearestHit;
    }

    private Vector3 GetOverlapSafePosition(Vector3 wantedPosition, LayerMask wallMask)
    {
        Vector2 safeCenter = (Vector2)wantedPosition + GetBossColliderCenterOffset();
        Vector2 paddedSize = GetBossCastSize() + Vector2.one * (WallStopPadding * 2f);

        // 최종 위치에서 히트박스가 벽과 겹치면 이전에 검증된 안전 위치로 되돌려 벽 안에서 멈추지 않게 한다.
        if (Physics2D.OverlapBox(safeCenter, paddedSize, 0f, wallMask) != null)
            return lastSafePosition;

        return wantedPosition;
    }

    private bool IsOverlappingWall(LayerMask wallMask)
    {
        if (bossCollider == null)
            return false;

        ContactFilter2D wallFilter = new ContactFilter2D();
        wallFilter.SetLayerMask(wallMask);
        wallFilter.useTriggers = false;

        int hitCount = bossCollider.Overlap(wallFilter, wallOverlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D wallCollider = wallOverlapHits[i];
            if (wallCollider != null && wallCollider != bossCollider)
                return true;
        }

        return false;
    }

    internal bool ResolveCurrentWallOverlap(LayerMask wallMask)
    {
        if (bossCollider == null)
            return false;

        ContactFilter2D wallFilter = new ContactFilter2D();
        wallFilter.SetLayerMask(wallMask);
        wallFilter.useTriggers = false;
        bool moved = false;

        for (int iteration = 0; iteration < Mathf.Max(1, WallResolveIterations); iteration++)
        {
            int hitCount = bossCollider.Overlap(wallFilter, wallOverlapHits);
            Vector2 bestCorrection = Vector2.zero;
            float bestCorrectionMagnitude = 0f;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D wallCollider = wallOverlapHits[i];
                if (wallCollider == null || wallCollider == bossCollider)
                    continue;

                ColliderDistance2D distance = bossCollider.Distance(wallCollider);
                if (!distance.isOverlapped)
                    continue;

                // distance.normal은 보스 콜라이더에서 벽 콜라이더를 향한다. 음수 distance와 함께 쓰면 벽 바깥 방향 보정값이 된다.
                Vector2 correction = distance.normal * (distance.distance - Mathf.Max(0.01f, WallUnstuckPadding));
                float correctionMagnitude = correction.sqrMagnitude;
                if (correctionMagnitude > bestCorrectionMagnitude)
                {
                    bestCorrectionMagnitude = correctionMagnitude;
                    bestCorrection = correction;
                }
            }

            if (bestCorrectionMagnitude <= 0.0001f)
                break;

            transform.position += (Vector3)bestCorrection;
            Physics2D.SyncTransforms();
            moved = true;
        }

        if (moved)
            swayBaseX = transform.position.x;

        return moved;
    }

    private Vector2 GetBossColliderCenter()
    {
        if (bossCollider != null)
            return bossCollider.bounds.center;

        if (spriteRenderer != null)
            return spriteRenderer.bounds.center;

        return transform.position;
    }

    private Vector2 GetBossColliderCenterOffset()
    {
        if (bossCollider != null)
            return (Vector2)bossCollider.bounds.center - (Vector2)transform.position;

        if (spriteRenderer != null)
            return (Vector2)spriteRenderer.bounds.center - (Vector2)transform.position;

        return Vector2.zero;
    }

    private Vector2 GetBossCastSize()
    {
        if (bossCollider != null)
            return bossCollider.bounds.size;

        if (spriteRenderer != null)
            return spriteRenderer.bounds.size;

        return Vector2.one;
    }

    internal void ClampInsideCameraView()
    {
        // 보스가 카메라 밖으로 나가지 않게 콜라이더 크기와 여백을 고려해 위치를 제한한다.
        if (!KeepInsideCameraView)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || !mainCamera.orthographic)
            return;

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 cameraPosition = mainCamera.transform.position;

        Vector2 bossExtents = GetBossExtents();
        float minX = cameraPosition.x - halfWidth + bossExtents.x + CameraEdgePadding;
        float maxX = cameraPosition.x + halfWidth - bossExtents.x - CameraEdgePadding;
        float minY = cameraPosition.y - halfHeight + bossExtents.y + CameraEdgePadding;
        float maxY = cameraPosition.y + halfHeight - bossExtents.y - CameraEdgePadding;

        Vector2 colliderOffset = GetBossColliderCenterOffset();
        Vector3 clampedCenter = GetBossVisualCenter();
        clampedCenter.x = minX > maxX
            ? cameraPosition.x
            : Mathf.Clamp(clampedCenter.x, minX, maxX);
        clampedCenter.y = minY > maxY
            ? cameraPosition.y
            : Mathf.Clamp(clampedCenter.y, minY, maxY);

        Vector3 clampedPosition = new Vector3(
            clampedCenter.x - colliderOffset.x,
            clampedCenter.y - colliderOffset.y,
            transform.position.z);

        MoveToSafePosition(clampedPosition, LayerMask.GetMask("Ground"));
    }

    private Vector2 GetBossExtents()
    {
        if (bossCollider != null)
            return bossCollider.bounds.extents;

        if (spriteRenderer != null)
            return spriteRenderer.bounds.extents;

        return Vector2.one * 0.5f;
    }

    bool IsNearWall(LayerMask groundMask)
    {
        return Physics2D.Raycast(transform.position, Vector2.right, WallAvoidDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.left,  WallAvoidDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.up,    WallAvoidDistance, groundMask)
            || Physics2D.Raycast(transform.position, Vector2.down,  WallAvoidDistance, groundMask)
            || Physics2D.OverlapCircle(transform.position, WallCheckRadius, groundMask) != null;
    }

    IEnumerator MoveAwayFromWall(LayerMask groundMask)
    {
        float moveTime = 0.8f;
        float elapsed = 0f;

        while (elapsed < moveTime && !isDead)
        {
            elapsed += Time.deltaTime;

            Vector3 safePosition = GetSafePosition(transform.position, groundMask);

            if (safePosition == transform.position)
                yield break;

            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                safePosition,
                WallAvoidSpeed * Time.deltaTime);
            MoveToSafePosition(nextPosition, groundMask);

            yield return null;
        }
    }


    void CreateHpBarUI()
    {
        // 보스 체력바는 런타임에 화면 상단 UI로 생성한다.
        GameObject canvasObj = new GameObject("BossHpCanvas");
        bossCanvas = canvasObj.AddComponent<Canvas>();
        bossCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        bossCanvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.1f, 1f); bgRt.anchorMax = new Vector2(0.9f, 1f);
        bgRt.pivot = new Vector2(0.5f, 1f); bgRt.anchoredPosition = new Vector2(0f, hpBarPosY);
        bgRt.sizeDelta = new Vector2(0f, DefaultHpBarHeight);
        bgObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        CreateSlider(canvasObj);
    }

    void CreateSlider(GameObject parent)
    {
        GameObject slObj = new GameObject("HpSlider");
        slObj.transform.SetParent(parent.transform, false);
        hpSlider = slObj.AddComponent<Slider>();
        RectTransform slRt = slObj.GetComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0.1f, 1f); slRt.anchorMax = new Vector2(0.9f, 1f);
        slRt.pivot = new Vector2(0.5f, 1f); slRt.anchoredPosition = new Vector2(0f, hpBarPosY);
        slRt.sizeDelta = new Vector2(0f, DefaultHpBarHeight);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(slObj.transform, false);
        SetFullRect(fillArea.AddComponent<RectTransform>());

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        SetFullRect(fill.AddComponent<RectTransform>());
        fill.AddComponent<Image>().color = DefaultHpBarColor;

        hpSlider.fillRect = fill.GetComponent<RectTransform>();
        hpSlider.minValue = 0f; hpSlider.maxValue = maxHp;
        hpSlider.value = maxHp; hpSlider.interactable = false;
    }

    void CreateLabel(GameObject parent)
    {
        GameObject obj = new GameObject("BossName");
        obj.transform.SetParent(parent.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 1f); rt.anchorMax = new Vector2(0.9f, 1f);
        rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = new Vector2(0f, -12f);
        rt.sizeDelta = new Vector2(0f, 24f);

        Text txt = obj.AddComponent<Text>();
        txt.text = "거대 드론";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white; txt.fontSize = 18;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void SetFullRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    internal void UpdateHpBar()
    {
        if (hpSlider != null) hpSlider.value = currentHp;
    }

    private void DestroyBossCanvas()
    {
        if (bossCanvas == null)
            return;

        Destroy(bossCanvas.gameObject);
        bossCanvas = null;
        hpSlider = null;
    }

    public Vector3 GetHealDroneAttachPosition(float side)
    {
        if (healDronePattern == null)
            CacheComponents();

        return healDronePattern != null
            ? healDronePattern.GetHealDroneAttachPosition(side)
            : GetBossVisualCenter();
    }

    public void HealFromAttachedDrone(float deltaTime)
    {
        if (healDronePattern == null)
            CacheComponents();

        if (healDronePattern != null)
            healDronePattern.HealFromAttachedDrone(deltaTime);
    }

    public void OnHealDroneDestroyed()
    {
        if (healDronePattern == null)
            CacheComponents();

        if (healDronePattern != null)
            healDronePattern.OnHealDroneDestroyed();
    }

}
