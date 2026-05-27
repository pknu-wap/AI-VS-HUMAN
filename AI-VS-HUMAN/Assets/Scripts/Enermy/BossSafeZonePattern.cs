using System.Collections;
using UnityEngine;

public class BossSafeZonePattern : MonoBehaviour
{
    [Header("References")]
    public BossDrone boss;
    public Room bossRoom;
    public Transform player;
    public Camera targetCamera;

    [Header("Activation")]
    public bool startWhenBossDetectsPlayer = true;
    public bool requirePlayerInsideRoom = true;
    public float initialDelay = 2f;
    public float patternInterval = 8f;

    [Header("Safe Zone")]
    public float safeZoneSize = 6f;
    public float safeZoneMinSize = 3f;
    public float safeZoneShrinkPerUse = 0f;
    public float safeZoneWarningDuration = 3f;
    public int safeZoneDamage = 1;
    public float safeZoneRoomPadding = 1f;

    [Header("Visual")]
    public Color warningColor = new Color(0.2f, 1f, 0.35f, 0.28f);
    public Color finalColor = new Color(1f, 0.95f, 0.2f, 0.5f);
    public int sortingOrder = 5;

    private GameObject safeZoneMarker;
    private SpriteRenderer safeZoneRenderer;
    private Sprite safeZoneSprite;
    private Texture2D safeZoneTexture;
    private PlayerHealth playerHealth;
    private Coroutine patternLoop;
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
        ShowSafeZoneMarker(zoneBounds, warningColor);

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, safeZoneWarningDuration);
        while (elapsed < duration)
        {
            if (!CanRunPattern())
            {
                DestroySafeZoneMarker();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (safeZoneRenderer != null)
                safeZoneRenderer.color = Color.Lerp(warningColor, finalColor, t);

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

        if (boss != null)
        {
            if (!boss.gameObject.activeInHierarchy)
                return false;

            if (startWhenBossDetectsPlayer)
            {
                float distance = Vector2.Distance(boss.transform.position, player.position);
                if (distance > boss.detectionRange)
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
        safeZoneMarker.transform.localScale = new Vector3(zoneBounds.size.x, zoneBounds.size.y, 1f);

        safeZoneRenderer = safeZoneMarker.AddComponent<SpriteRenderer>();
        safeZoneRenderer.sprite = GetSafeZoneSprite();
        safeZoneRenderer.color = color;
        safeZoneRenderer.sortingOrder = sortingOrder;
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
            boss = GetComponent<BossDrone>();

        if (boss == null)
            boss = FindFirstObjectByType<BossDrone>();

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
        BossRoomController controller = FindFirstObjectByType<BossRoomController>();
        if (controller != null)
        {
            bossRoom = controller.bossRoom;
            return;
        }

        bossRoom = FindFirstObjectByType<Room>();
    }

    private void OnDestroy()
    {
        DestroySafeZoneMarker();

        if (safeZoneTexture != null)
            Destroy(safeZoneTexture);
    }
}
