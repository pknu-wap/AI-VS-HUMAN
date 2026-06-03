using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Stage2BossRoomController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Room bossRoom;
    public RoomCameraController roomCameraController;
    public CoreXBoss boss;

    [Header("Boss Activation")]
    public bool hideSceneBossUntilRoomEntered = true;
    public bool enterWhenPlayerInsideRoom = true;
    public bool spawnOnlyOnce = true;
    public bool resetOnPlayerDeath = true;

    [Header("Fallback Boss Spawn")]
    public CoreXBoss bossPrefab;
    public Transform bossSpawnPoint;
    public Vector3 bossSpawnPosition = new Vector3(32f, 4f, 0f);

    [Header("Boss Room Lock")]
    public bool createBossLockWalls = true;
    public float lockWallThickness = 1f;
    public float lockWallPadding = 0f;
    public string lockWallLayerName = "Ground";

    [Header("Boss Clear Cleanup")]
    public bool destroyEnemiesInsideRoomOnBossDeath = true;
    public List<GameObject> enemiesToDestroyOnBossDeath = new List<GameObject>();

    private CoreXBoss activeBoss;
    private PlayerHealth playerHealth;
    private bool bossSpawned;
    private bool bossCleared;
    private bool activeBossWasInstantiated;
    private bool hasBossStartPosition;
    private Vector3 bossStartPosition;
    private readonly List<GameObject> bossLockWalls = new List<GameObject>();

    
    [Header("Boss Servers")]
    public ServerNode[] phase1Servers;
    public ServerNode[] phase2Servers;
    
    
    private void Awake()
    {
        ResolveReferences();
        CacheBossStartPosition();
        HideSceneBossUntilEnter();
    }

    private void OnEnable()
    {
        ResolveReferences();
        PlayerHealth.PlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        PlayerHealth.PlayerDied -= HandlePlayerDied;
        UnsubscribeFromBoss();
        RemoveBossLockWalls();
    }

    private void Reset()
    {
        bossRoom = GetComponent<Room>();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!enterWhenPlayerInsideRoom || bossRoom == null || player == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (bossCleared)
            return;

        if (bossRoom.GetBounds().Contains(player.position))
            EnterBossRoom();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            EnterBossRoom();
    }

    public void EnterBossRoom()
    {
        if (bossCleared)
            return;

        if (bossSpawned && spawnOnlyOnce)
            return;

        ActivateBossIfNeeded();
    }

    private void ActivateBossIfNeeded()
    {
        if (activeBoss != null)
            return;

        if (boss != null)
        {
            RestoreBossStartPosition();
            activeBoss = boss;
            activeBossWasInstantiated = false;
            activeBoss.deactivateOnDeathInsteadOfDestroy = true;
            activeBoss.gameObject.SetActive(true);
        }
        else if (bossPrefab != null)
        {
            activeBoss = Instantiate(bossPrefab, GetBossSpawnPosition(), Quaternion.identity);
            activeBoss.name = bossPrefab.name;
            activeBossWasInstantiated = true;
        }
        else
        {
            Debug.LogWarning("Stage2BossRoomController has no CoreXBoss assigned.", this);
            return;
        }

        bossSpawned = true;
        bossCleared = false;

        ConfigureActiveBoss();

        if (createBossLockWalls)
            CreateBossLockWalls();
    }

    private Vector3 GetBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
            return bossSpawnPoint.position;

        return bossSpawnPosition;
    }

    private void ConfigureActiveBoss()
    {
        if (activeBoss == null)
            return;

        BossHpBar hpBar = activeBoss.GetComponent<BossHpBar>();
        activeBoss.PrepareForBossRoomActivation(bossRoom, player, hpBar, phase1Servers, phase2Servers);
        SubscribeToBoss();

        BossSafeZonePattern safeZonePattern = activeBoss.GetComponent<BossSafeZonePattern>();
        if (safeZonePattern != null)
        {
            safeZonePattern.bossTransform = activeBoss.transform;
            safeZonePattern.bossRoom = bossRoom;
            safeZonePattern.player = player;

            Camera targetCamera = null;
            if (roomCameraController != null)
                targetCamera = roomCameraController.GetComponent<Camera>();

            safeZonePattern.targetCamera = targetCamera != null ? targetCamera : Camera.main;
        }
    }

    private void SubscribeToBoss()
    {
        if (activeBoss == null)
            return;

        activeBoss.Died -= HandleBossDied;
        activeBoss.Died += HandleBossDied;
    }

    private void UnsubscribeFromBoss()
    {
        if (activeBoss != null)
            activeBoss.Died -= HandleBossDied;
    }

    private void ResolveReferences()
    {
        if (bossRoom == null)
            bossRoom = GetComponent<Room>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        if (roomCameraController == null && Camera.main != null)
            roomCameraController = Camera.main.GetComponent<RoomCameraController>();

        if (boss == null)
            boss = FindSceneBoss();

        CacheBossStartPosition();
    }

    private CoreXBoss FindSceneBoss()
    {
        CoreXBoss[] bosses = FindObjectsByType<CoreXBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CoreXBoss candidate in bosses)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid())
                return candidate;
        }

        return null;
    }

    private void CacheBossStartPosition()
    {
        if (hasBossStartPosition || boss == null)
            return;

        bossStartPosition = boss.transform.position;
        hasBossStartPosition = true;
    }

    private void RestoreBossStartPosition()
    {
        if (!hasBossStartPosition || boss == null)
            return;

        boss.transform.position = bossStartPosition;
        Physics2D.SyncTransforms();
    }

    private void HideSceneBossUntilEnter()
    {
        if (!hideSceneBossUntilRoomEntered || boss == null || bossSpawned)
            return;

        if (boss.gameObject.scene.IsValid())
            boss.gameObject.SetActive(false);
    }

    private void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (!resetOnPlayerDeath)
            return;

        if (bossCleared)
            return;

        if (deadPlayer == null || playerHealth != null && deadPlayer != playerHealth)
            return;

        UnsubscribeFromBoss();

        if (activeBoss != null)
        {
            if (activeBossWasInstantiated)
            {
                Destroy(activeBoss.gameObject);
            }
            else
            {
                RestoreBossStartPosition();
                activeBoss.ResetForBossRoomRetry();

                if (hideSceneBossUntilRoomEntered)
                    activeBoss.gameObject.SetActive(false);
            }
        }

        activeBoss = null;
        activeBossWasInstantiated = false;
        bossSpawned = false;
        RemoveBossLockWalls();
        ResetBossServersForRetry();
    }

    public void ResetForDebugTeleport()
    {
        UnsubscribeFromBoss();

        CoreXBoss targetBoss = activeBoss != null ? activeBoss : boss;
        if (targetBoss != null)
        {
            if (activeBossWasInstantiated)
                Destroy(targetBoss.gameObject);
            else
            {
                RestoreBossStartPosition();
                targetBoss.ResetForBossRoomRetry();

                if (hideSceneBossUntilRoomEntered)
                    targetBoss.gameObject.SetActive(false);
            }
        }

        activeBoss = null;
        activeBossWasInstantiated = false;
        bossSpawned = false;
        bossCleared = false;
        RemoveBossLockWalls();
        ResetBossServersForRetry();
    }

    private void HandleBossDied()
    {
        UnsubscribeFromBoss();
        bossCleared = true;
        bossSpawned = true;

        ClearBossRoomEnemies();
        RemoveBossLockWalls();
        activeBoss = null;
        activeBossWasInstantiated = false;
    }

    private void ClearBossRoomEnemies()
    {
        foreach (GameObject enemy in enemiesToDestroyOnBossDeath)
            DestroyIfPresent(enemy);

        DestroyServers(phase1Servers);
        DestroyServers(phase2Servers);

        if (!destroyEnemiesInsideRoomOnBossDeath || bossRoom == null)
            return;

        DestroyComponentsInsideRoom<EnemyBase>();
        DestroyComponentsInsideRoom<GhostEnemy>();
        DestroyComponentsInsideRoom<ShadowEnemy>();
        DestroyComponentsInsideRoom<HealDrone>();
        DestroyComponentsInsideRoom<ServerNode>();
    }

    private void DestroyServers(ServerNode[] servers)
    {
        if (servers == null)
            return;

        foreach (ServerNode server in servers)
        {
            if (server != null)
                DestroyIfPresent(server.gameObject);
        }
    }

    private void DestroyComponentsInsideRoom<T>() where T : Component
    {
        Bounds bounds = bossRoom.GetBounds();
        T[] components = FindObjectsByType<T>(FindObjectsSortMode.None);

        foreach (T component in components)
        {
            if (component != null && bounds.Contains(component.transform.position))
                DestroyIfPresent(component.gameObject);
        }
    }

    private void DestroyIfPresent(GameObject obj)
    {
        if (obj == null)
            return;

        if (activeBoss != null && obj == activeBoss.gameObject)
            return;

        if (boss != null && obj == boss.gameObject)
            return;

        Destroy(obj);
    }

    private void ResetBossServersForRetry()
    {
        ResetServers(phase1Servers);
        ResetServers(phase2Servers);
    }

    private void ResetServers(ServerNode[] servers)
    {
        if (servers == null)
            return;

        foreach (ServerNode server in servers)
        {
            if (server != null)
                server.ResetServer();
        }
    }

    private void CreateBossLockWalls()
    {
        RemoveBossLockWalls();

        if (bossRoom == null)
            return;

        Bounds bounds = bossRoom.GetBounds();
        float thickness = Mathf.Max(0.05f, lockWallThickness);
        float padding = Mathf.Max(0f, lockWallPadding);
        float width = bounds.size.x + padding * 2f + thickness * 2f;
        float height = bounds.size.y + padding * 2f + thickness * 2f;

        CreateBossLockWall("Stage 2 Boss Wall Top",
            new Vector3(bounds.center.x, bounds.max.y + padding + thickness * 0.5f, 0f),
            new Vector2(width, thickness));

        CreateBossLockWall("Stage 2 Boss Wall Bottom",
            new Vector3(bounds.center.x, bounds.min.y - padding - thickness * 0.5f, 0f),
            new Vector2(width, thickness));

        CreateBossLockWall("Stage 2 Boss Wall Left",
            new Vector3(bounds.min.x - padding - thickness * 0.5f, bounds.center.y, 0f),
            new Vector2(thickness, height));

        CreateBossLockWall("Stage 2 Boss Wall Right",
            new Vector3(bounds.max.x + padding + thickness * 0.5f, bounds.center.y, 0f),
            new Vector2(thickness, height));
    }

    private void CreateBossLockWall(string wallName, Vector3 position, Vector2 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(transform, true);
        wall.transform.position = position;

        int layer = LayerMask.NameToLayer(lockWallLayerName);
        if (layer >= 0)
            wall.layer = layer;

        BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = size;
        wallCollider.isTrigger = false;

        bossLockWalls.Add(wall);
    }

    private void RemoveBossLockWalls()
    {
        foreach (GameObject wall in bossLockWalls)
        {
            if (wall != null)
                Destroy(wall);
        }

        bossLockWalls.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 position = boss != null ? boss.transform.position : GetBossSpawnPosition();
        Gizmos.DrawWireSphere(position, 0.35f);
    }
}
