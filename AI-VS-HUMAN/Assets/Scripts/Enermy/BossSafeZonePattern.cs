using System.Collections;
using UnityEngine;

public class BossSafeZonePattern : MonoBehaviour
{
    private const float BorderThickness = 0.12f;
    private const float FlashMinAlpha = 0.25f;
    private const float FlashFrequency = 10f;
    private const float DangerFadeInDuration = 0.35f;
    private const float DangerLeadTime = 0.8f;
    private const float SafeZoneFadeInDuration = 0.6f;
    private const float SafeZonePreviewAlpha = 0.35f;
    private const int SortingOrder = 5;
    private static readonly Color WarningColor = new Color(0.2f, 1f, 0.35f, 0.28f);
    private static readonly Color BorderColor = new Color(0.1f, 1f, 0.35f, 0.95f);
    private static readonly Color DangerOutsideColor = new Color(1f, 0.05f, 0.02f, 0.38f);

    [Header("참조")]
    public GiantDrone boss;
    public Transform bossTransform;
    public Room bossRoom;
    public Transform player;
    public Camera targetCamera;

    [Header("활성화")]
    public bool startWhenBossDetectsPlayer = true;
    public bool requirePlayerInsideRoom = true;
    public float bossDetectionRange = 20f;
    public float initialDelay = 2f;
    public float patternInterval = 8f;

    [Header("안전 구역")]
    public float safeZoneSize = 6f;
    public float safeZoneMinSize = 3f;
    public float safeZoneShrinkPerUse = 0f;
    public float safeZoneWarningDuration = 3f;
    public int safeZoneDamage = 1;
    public float safeZoneRoomPadding = 1f;

    private GameObject safeZoneMarker;
    private SpriteRenderer safeZoneRenderer;
    private readonly SpriteRenderer[] borderRenderers = new SpriteRenderer[4];
    private readonly SpriteRenderer[] dangerRenderers = new SpriteRenderer[4];
    private Sprite safeZoneSprite;
    private Texture2D safeZoneTexture;
    private PlayerHealth playerHealth;
    private Coroutine patternLoop;
    private Vector2 currentZoneSize;
    private float currentSafeZoneSize;

    private void Awake()
    {
        ResolveReferences();
        currentSafeZoneSize = Mathf.Max(safeZoneMinSize, safeZoneSize);
    }

    private void OnEnable()
    {
        if (patternLoop != null)
            StopCoroutine(patternLoop);

        patternLoop = StartCoroutine(PatternLoop());
    }

    private void OnDisable()
    {
        if (patternLoop != null)
        {
            StopCoroutine(patternLoop);
            patternLoop = null;
        }

        DestroySafeZoneMarker();
    }

    private IEnumerator PatternLoop()
    {
        while (true)
        {
            ResolveReferences();

            while (!CanRunPattern())
            {
                yield return null;
                ResolveReferences();
            }

            yield return new WaitForSeconds(Mathf.Max(0f, initialDelay));

            while (CanRunPattern())
            {
                yield return StartCoroutine(SafeZonePattern());
                yield return new WaitForSeconds(Mathf.Max(0.05f, patternInterval));
            }
        }
    }

    private IEnumerator SafeZonePattern()
    {
        Bounds zoneBounds = GetRandomSafeZoneBounds();
        ShowSafeZoneMarker(zoneBounds, WarningColor);

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, safeZoneWarningDuration);
        float leadTime = Mathf.Clamp(DangerLeadTime, 0f, duration);
        float fadeInDuration = Mathf.Clamp(SafeZoneFadeInDuration, 0.01f, Mathf.Max(0.01f, duration - leadTime));

        while (elapsed < duration)
        {
            if (!CanRunPattern())
            {
                DestroySafeZoneMarker();
                yield break;
            }

            elapsed += Time.deltaTime;
            UpdateSafeZoneVisual(elapsed, duration, leadTime, fadeInDuration);

            yield return null;
        }

        if (playerHealth != null && !playerHealth.IsDead && !zoneBounds.Contains(player.position))
            playerHealth.TakeDamage(safeZoneDamage);

        DestroySafeZoneMarker();
        ShrinkNextSafeZone();
    }

    private bool CanRunPattern()
    {
        if (player == null)
            return false;

        if (playerHealth != null && playerHealth.IsDead)
            return false;

        Transform activeBossTransform = GetActiveBossTransform();
        if (activeBossTransform != null)
        {
            if (!activeBossTransform.gameObject.activeInHierarchy)
                return false;

            if (startWhenBossDetectsPlayer)
            {
                float range = boss != null && boss.phase1 != null ? boss.phase1.detectionRange : bossDetectionRange;
                float distance = Vector2.Distance(activeBossTransform.position, player.position);
                if (distance > range)
                    return false;
            }
        }

        if (requirePlayerInsideRoom && bossRoom != null && !bossRoom.GetBounds().Contains(player.position))
            return false;

        return true;
    }

    private Bounds GetRandomSafeZoneBounds()
    {
        Bounds roomBounds = GetSafeZoneRoomBounds();
        float zoneSize = Mathf.Max(safeZoneMinSize, currentSafeZoneSize);
        float halfSize = zoneSize * 0.5f;
        float padding = Mathf.Max(0f, safeZoneRoomPadding);

        float minX = roomBounds.min.x + halfSize + padding;
        float maxX = roomBounds.max.x - halfSize - padding;
        float minY = roomBounds.min.y + halfSize + padding;
        float maxY = roomBounds.max.y - halfSize - padding;

        float centerX = minX > maxX ? roomBounds.center.x : Random.Range(minX, maxX);
        float centerY = minY > maxY ? roomBounds.center.y : Random.Range(minY, maxY);

        return new Bounds(new Vector3(centerX, centerY, 0f), new Vector3(zoneSize, zoneSize, 1f));
    }

    private Bounds GetSafeZoneRoomBounds()
    {
        if (bossRoom == null)
            ResolveBossRoom();

        if (bossRoom != null)
        {
            Bounds roomBounds = bossRoom.GetBounds();
            Bounds visibleBounds = GetCameraVisibleBounds();

            if (visibleBounds.size.x > 0f && visibleBounds.size.y > 0f)
                return GetBoundsIntersection(roomBounds, visibleBounds);

            return roomBounds;
        }

        Bounds cameraBounds = GetCameraVisibleBounds();
        if (cameraBounds.size.x > 0f && cameraBounds.size.y > 0f)
            return cameraBounds;

        return new Bounds(transform.position, new Vector3(16f, 9f, 1f));
    }

    private Bounds GetCameraVisibleBounds()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic)
            return new Bounds(Vector3.zero, Vector3.zero);

        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        return new Bounds(cam.transform.position, new Vector3(width, height, 1f));
    }

    private Bounds GetBoundsIntersection(Bounds a, Bounds b)
    {
        float minX = Mathf.Max(a.min.x, b.min.x);
        float maxX = Mathf.Min(a.max.x, b.max.x);
        float minY = Mathf.Max(a.min.y, b.min.y);
        float maxY = Mathf.Min(a.max.y, b.max.y);

        if (minX >= maxX || minY >= maxY)
            return a;

        Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 1f);
        return new Bounds(center, size);
    }

    private void ShowSafeZoneMarker(Bounds zoneBounds, Color color)
    {
        DestroySafeZoneMarker();

        safeZoneMarker = new GameObject("Boss Safe Zone");
        safeZoneMarker.transform.position = zoneBounds.center;

        Vector2 size = zoneBounds.size;
        currentZoneSize = size;
        safeZoneRenderer = CreateZoneSprite("Fill", Vector2.zero, size, color, SortingOrder);
        CreateDangerOutsideSprites(zoneBounds);
        CreateBorderSprites(size);
        UpdateSafeZoneVisual(0f, Mathf.Max(0.05f, safeZoneWarningDuration), Mathf.Max(0f, DangerLeadTime), Mathf.Max(0.01f, SafeZoneFadeInDuration));
    }

    private SpriteRenderer CreateZoneSprite(string objectName, Vector2 localPosition, Vector2 size, Color color, int order)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(safeZoneMarker.transform, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSafeZoneSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    private void CreateBorderSprites(Vector2 size)
    {
        borderRenderers[0] = CreateZoneSprite(
            "Border Top",
            Vector2.zero,
            Vector2.zero,
            BorderColor,
            SortingOrder + 1);

        borderRenderers[1] = CreateZoneSprite(
            "Border Left",
            Vector2.zero,
            Vector2.zero,
            BorderColor,
            SortingOrder + 1);

        borderRenderers[2] = CreateZoneSprite(
            "Border Bottom",
            Vector2.zero,
            Vector2.zero,
            BorderColor,
            SortingOrder + 1);

        borderRenderers[3] = CreateZoneSprite(
            "Border Right",
            Vector2.zero,
            Vector2.zero,
            BorderColor,
            SortingOrder + 1);
    }

    private void CreateDangerOutsideSprites(Bounds zoneBounds)
    {
        Bounds areaBounds = GetSafeZoneRoomBounds();
        Color color = DangerOutsideColor;
        color.a = 0f;

        float leftWidth = Mathf.Max(0f, zoneBounds.min.x - areaBounds.min.x);
        float rightWidth = Mathf.Max(0f, areaBounds.max.x - zoneBounds.max.x);
        float bottomHeight = Mathf.Max(0f, zoneBounds.min.y - areaBounds.min.y);
        float topHeight = Mathf.Max(0f, areaBounds.max.y - zoneBounds.max.y);

        dangerRenderers[0] = CreateDangerSprite(
            "Danger Left",
            new Vector2(areaBounds.min.x + leftWidth * 0.5f, areaBounds.center.y) - (Vector2)zoneBounds.center,
            new Vector2(leftWidth, areaBounds.size.y),
            color);

        dangerRenderers[1] = CreateDangerSprite(
            "Danger Right",
            new Vector2(zoneBounds.max.x + rightWidth * 0.5f, areaBounds.center.y) - (Vector2)zoneBounds.center,
            new Vector2(rightWidth, areaBounds.size.y),
            color);

        dangerRenderers[2] = CreateDangerSprite(
            "Danger Bottom",
            new Vector2(zoneBounds.center.x, areaBounds.min.y + bottomHeight * 0.5f) - (Vector2)zoneBounds.center,
            new Vector2(zoneBounds.size.x, bottomHeight),
            color);

        dangerRenderers[3] = CreateDangerSprite(
            "Danger Top",
            new Vector2(zoneBounds.center.x, zoneBounds.max.y + topHeight * 0.5f) - (Vector2)zoneBounds.center,
            new Vector2(zoneBounds.size.x, topHeight),
            color);
    }

    private SpriteRenderer CreateDangerSprite(string objectName, Vector2 localPosition, Vector2 size, Color color)
    {
        if (size.x <= 0.01f || size.y <= 0.01f)
            return null;

        return CreateZoneSprite(objectName, localPosition, size, color, SortingOrder - 1);
    }

    private void UpdateSafeZoneVisual(float elapsed, float duration, float leadTime, float fadeInDuration)
    {
        float previewFade = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, DangerFadeInDuration));
        float fullFade = Mathf.Clamp01((elapsed - leadTime) / fadeInDuration);
        float previewAlpha = Mathf.Clamp01(SafeZonePreviewAlpha) * previewFade;
        float zoneAlphaScale = Mathf.Lerp(previewAlpha, 1f, fullFade);
        float borderAlphaScale = fullFade;

        if (safeZoneRenderer != null)
            safeZoneRenderer.color = WithScaledAlpha(WarningColor, zoneAlphaScale);

        float timerProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        UpdateTimerBorder(timerProgress, Mathf.Max(previewFade, borderAlphaScale));

        float alphaPulse = (Mathf.Sin(Time.time * FlashFrequency) + 1f) * 0.5f;
        float dangerFade = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, DangerFadeInDuration));
        Color dangerColor = DangerOutsideColor;
        dangerColor.a = Mathf.Lerp(Mathf.Clamp01(FlashMinAlpha), DangerOutsideColor.a, alphaPulse) * dangerFade;

        for (int i = 0; i < dangerRenderers.Length; i++)
        {
            if (dangerRenderers[i] != null)
                dangerRenderers[i].color = dangerColor;
        }
    }

    private void UpdateTimerBorder(float progress, float alphaScale)
    {
        float thickness = Mathf.Max(0.02f, BorderThickness);
        float halfWidth = currentZoneSize.x * 0.5f;
        float halfHeight = currentZoneSize.y * 0.5f;

        float topProgress = Mathf.Clamp01(progress * 4f);
        float leftProgress = Mathf.Clamp01(progress * 4f - 1f);
        float bottomProgress = Mathf.Clamp01(progress * 4f - 2f);
        float rightProgress = Mathf.Clamp01(progress * 4f - 3f);

        SetHorizontalBorder(borderRenderers[0], topProgress, true, halfWidth, halfHeight, thickness, alphaScale);
        SetVerticalBorder(borderRenderers[1], leftProgress, true, halfWidth, halfHeight, thickness, alphaScale);
        SetHorizontalBorder(borderRenderers[2], bottomProgress, false, halfWidth, halfHeight, thickness, alphaScale);
        SetVerticalBorder(borderRenderers[3], rightProgress, false, halfWidth, halfHeight, thickness, alphaScale);
    }

    private void SetHorizontalBorder(SpriteRenderer renderer, float progress, bool rightToLeft, float halfWidth, float halfHeight, float thickness, float alphaScale)
    {
        if (renderer == null)
            return;

        float length = currentZoneSize.x * Mathf.Clamp01(progress);
        float centerX = rightToLeft
            ? halfWidth - length * 0.5f
            : -halfWidth + length * 0.5f;
        float centerY = rightToLeft
            ? halfHeight - thickness * 0.5f
            : -halfHeight + thickness * 0.5f;

        renderer.transform.localPosition = new Vector2(centerX, centerY);
        renderer.transform.localScale = new Vector3(length, thickness, 1f);
        renderer.color = WithScaledAlpha(BorderColor, length <= 0.001f ? 0f : alphaScale);
    }

    private void SetVerticalBorder(SpriteRenderer renderer, float progress, bool topToBottom, float halfWidth, float halfHeight, float thickness, float alphaScale)
    {
        if (renderer == null)
            return;

        float length = currentZoneSize.y * Mathf.Clamp01(progress);
        float centerX = topToBottom
            ? -halfWidth + thickness * 0.5f
            : halfWidth - thickness * 0.5f;
        float centerY = topToBottom
            ? halfHeight - length * 0.5f
            : -halfHeight + length * 0.5f;

        renderer.transform.localPosition = new Vector2(centerX, centerY);
        renderer.transform.localScale = new Vector3(thickness, length, 1f);
        renderer.color = WithScaledAlpha(BorderColor, length <= 0.001f ? 0f : alphaScale);
    }

    private Color WithScaledAlpha(Color color, float alphaScale)
    {
        color.a *= Mathf.Clamp01(alphaScale);
        return color;
    }

    private Sprite GetSafeZoneSprite()
    {
        if (safeZoneSprite != null)
            return safeZoneSprite;

        safeZoneTexture = new Texture2D(1, 1);
        safeZoneTexture.SetPixel(0, 0, Color.white);
        safeZoneTexture.Apply();

        safeZoneSprite = Sprite.Create(safeZoneTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return safeZoneSprite;
    }

    private void DestroySafeZoneMarker()
    {
        if (safeZoneMarker == null)
            return;

        Destroy(safeZoneMarker);
        safeZoneMarker = null;
        safeZoneRenderer = null;

        for (int i = 0; i < borderRenderers.Length; i++)
            borderRenderers[i] = null;

        for (int i = 0; i < dangerRenderers.Length; i++)
            dangerRenderers[i] = null;
    }

    private void ShrinkNextSafeZone()
    {
        if (safeZoneShrinkPerUse <= 0f)
            return;

        currentSafeZoneSize = Mathf.Max(safeZoneMinSize, currentSafeZoneSize - safeZoneShrinkPerUse);
    }

    private void ResolveReferences()
    {
        if (boss == null)
            boss = GetComponent<GiantDrone>();

        if (bossTransform == null)
        {
            if (boss != null)
                bossTransform = boss.transform;
            else
                bossTransform = transform;
        }

        if (boss == null)
            boss = FindFirstObjectByType<GiantDrone>();

        if (bossTransform == null && boss != null)
            bossTransform = boss.transform;

        if (bossRoom == null)
            ResolveBossRoom();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void ResolveBossRoom()
    {
        Stage1BossRoomController stage1Controller = FindFirstObjectByType<Stage1BossRoomController>();
        if (stage1Controller != null)
        {
            bossRoom = stage1Controller.bossRoom;
            return;
        }

        Stage2BossRoomController stage2Controller = FindFirstObjectByType<Stage2BossRoomController>();
        if (stage2Controller != null)
        {
            bossRoom = stage2Controller.bossRoom;
            return;
        }

        bossRoom = FindFirstObjectByType<Room>();
    }

    private Transform GetActiveBossTransform()
    {
        if (boss != null)
            return boss.transform;

        return bossTransform;
    }

    private void OnDestroy()
    {
        DestroySafeZoneMarker();

        if (safeZoneTexture != null)
            Destroy(safeZoneTexture);
    }
}
