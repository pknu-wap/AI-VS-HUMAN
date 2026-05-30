using UnityEngine;

[DisallowMultipleComponent]
public class Stage2BossRoomController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Room bossRoom;
    public RoomCameraController roomCameraController;

    [Header("Boss Spawn")]
    public CoreXBoss bossPrefab;
    public Transform bossSpawnPoint;
    public Vector3 bossSpawnPosition = new Vector3(32f, 4f, 0f);
    public bool enterWhenPlayerInsideRoom = true;
    public bool spawnOnlyOnce = true;
    public bool resetOnPlayerDeath = true;

    private CoreXBoss spawnedBoss;
    private PlayerHealth playerHealth;
    private bool bossSpawned;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        PlayerHealth.PlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        PlayerHealth.PlayerDied -= HandlePlayerDied;
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
        if (bossSpawned && spawnOnlyOnce)
            return;

        SpawnBossIfNeeded();
    }

    private void SpawnBossIfNeeded()
    {
        if (spawnedBoss != null)
            return;

        if (bossPrefab == null)
        {
            Debug.LogWarning("Stage2BossRoomController has no CoreXBoss prefab to spawn.", this);
            return;
        }

        spawnedBoss = Instantiate(bossPrefab, GetBossSpawnPosition(), Quaternion.identity);
        spawnedBoss.name = bossPrefab.name;
        bossSpawned = true;

        ConfigureSpawnedBoss();
    }

    private Vector3 GetBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
            return bossSpawnPoint.position;

        return bossSpawnPosition;
    }

    private void ConfigureSpawnedBoss()
    {
        if (spawnedBoss == null)
            return;

        BossHpBar hpBar = spawnedBoss.GetComponent<BossHpBar>();
        spawnedBoss.ConfigureForBossRoom(bossRoom, player, hpBar);

        BossSafeZonePattern safeZonePattern = spawnedBoss.GetComponent<BossSafeZonePattern>();
        if (safeZonePattern != null)
        {
            safeZonePattern.bossTransform = spawnedBoss.transform;
            safeZonePattern.bossRoom = bossRoom;
            safeZonePattern.player = player;

            Camera targetCamera = null;
            if (roomCameraController != null)
                targetCamera = roomCameraController.GetComponent<Camera>();

            safeZonePattern.targetCamera = targetCamera != null ? targetCamera : Camera.main;
        }
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
    }

    private void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (!resetOnPlayerDeath)
            return;

        if (deadPlayer == null || playerHealth != null && deadPlayer != playerHealth)
            return;

        if (spawnedBoss != null)
            Destroy(spawnedBoss.gameObject);

        spawnedBoss = null;
        bossSpawned = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetBossSpawnPosition(), 0.35f);
    }
}
