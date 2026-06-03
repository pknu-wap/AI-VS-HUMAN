using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Room))]
public class RoomEnemyEncounter : MonoBehaviour
{
    private const float WallThickness = 1f;
    private const float WallPadding = 0f;
    private const string WallLayerName = "Ground";
    private static readonly bool DrawEnemyPositionGizmos = true;
    private const float EnemyPositionRadius = 0.35f;
    private static readonly Color EnemyPositionColor = new Color(1f, 0.25f, 0.15f, 0.85f);

    [Header("참조")]
    public Transform player;
    public Room room;

    [Header("적")]
    public List<GameObject> enemies = new List<GameObject>();
    public bool deactivateEnemiesOnStart = true;
    public bool triggerOnce = true;
    public bool resetOnPlayerDeath = true;

    [Header("잠금 벽")]
    public bool createLockWalls = true;

    private readonly List<GameObject> enemyTemplates = new List<GameObject>();
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<GameObject> lockWalls = new List<GameObject>();
    private PlayerHealth playerHealth;
    private bool encounterStarted;
    private bool encounterCleared;

    private void Awake()
    {
        ResolveReferences();
        CacheEnemyTemplates();

        if (deactivateEnemiesOnStart && !encounterStarted)
            SetEnemyTemplatesActive(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        PlayerHealth.PlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        PlayerHealth.PlayerDied -= HandlePlayerDied;
        RemoveLockWalls();
    }

    private void Update()
    {
        ResolveReferences();

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (encounterCleared && triggerOnce)
            return;

        if (!encounterStarted)
        {
            if (IsPlayerInsideRoom())
                StartEncounter();

            return;
        }

        if (!HasRemainingActiveEnemies())
            ClearEncounter();
    }

    private void Reset()
    {
        room = GetComponent<Room>();
    }

    private void ResolveReferences()
    {
        if (room == null)
            room = GetComponent<Room>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void CacheEnemyTemplates()
    {
        if (enemyTemplates.Count > 0)
            return;

        foreach (GameObject enemy in enemies)
        {
            if (enemy != null && !enemyTemplates.Contains(enemy))
                enemyTemplates.Add(enemy);
        }
    }

    private bool IsPlayerInsideRoom()
    {
        return room != null && player != null && room.GetBounds().Contains(player.position);
    }

    private void StartEncounter()
    {
        encounterStarted = true;
        encounterCleared = false;

        SpawnActiveEnemies();

        if (createLockWalls && HasRemainingActiveEnemies())
            CreateLockWalls();
    }

    private void ClearEncounter()
    {
        encounterCleared = true;
        encounterStarted = false;
        activeEnemies.Clear();
        RemoveLockWalls();
    }

    private void SpawnActiveEnemies()
    {
        DestroyActiveEnemies();
        CacheEnemyTemplates();

        foreach (GameObject template in enemyTemplates)
        {
            if (template == null)
                continue;

            template.SetActive(false);

            GameObject enemy = Instantiate(
                template,
                template.transform.position,
                template.transform.rotation,
                template.transform.parent);

            enemy.name = template.name;
            enemy.SetActive(true);
            activeEnemies.Add(enemy);
        }
    }

    private void SetEnemyTemplatesActive(bool active)
    {
        CacheEnemyTemplates();

        foreach (GameObject enemy in enemyTemplates)
        {
            if (enemy != null)
                enemy.SetActive(active);
        }
    }

    private bool HasRemainingActiveEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }

        return activeEnemies.Count > 0;
    }

    private void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (!resetOnPlayerDeath || encounterCleared)
            return;

        if (deadPlayer == null || playerHealth != null && deadPlayer != playerHealth)
            return;

        if (!encounterStarted && activeEnemies.Count == 0)
            return;

        ResetEncounterAfterPlayerDeath();
    }

    private void ResetEncounterAfterPlayerDeath()
    {
        encounterStarted = false;
        encounterCleared = false;

        DestroyActiveEnemies();
        SetEnemyTemplatesActive(false);
        RemoveLockWalls();
    }

    public void ResetForDebugTeleport()
    {
        encounterStarted = false;
        encounterCleared = false;

        DestroyActiveEnemies();
        SetEnemyTemplatesActive(false);
        RemoveLockWalls();
    }

    private void DestroyActiveEnemies()
    {
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }

        activeEnemies.Clear();
    }

    private void CreateLockWalls()
    {
        RemoveLockWalls();

        if (room == null)
            return;

        Bounds bounds = room.GetBounds();
        float thickness = Mathf.Max(0.05f, WallThickness);
        float padding = Mathf.Max(0f, WallPadding);
        float width = bounds.size.x + padding * 2f + thickness * 2f;
        float height = bounds.size.y + padding * 2f + thickness * 2f;

        CreateWall("Encounter Wall Top",
            new Vector3(bounds.center.x, bounds.max.y + padding + thickness * 0.5f, 0f),
            new Vector2(width, thickness));

        CreateWall("Encounter Wall Bottom",
            new Vector3(bounds.center.x, bounds.min.y - padding - thickness * 0.5f, 0f),
            new Vector2(width, thickness));

        CreateWall("Encounter Wall Left",
            new Vector3(bounds.min.x - padding - thickness * 0.5f, bounds.center.y, 0f),
            new Vector2(thickness, height));

        CreateWall("Encounter Wall Right",
            new Vector3(bounds.max.x + padding + thickness * 0.5f, bounds.center.y, 0f),
            new Vector2(thickness, height));
    }

    private void CreateWall(string wallName, Vector3 position, Vector2 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(transform, true);
        wall.transform.position = position;

        int layer = LayerMask.NameToLayer(WallLayerName);
        if (layer >= 0)
            wall.layer = layer;

        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = false;

        lockWalls.Add(wall);
    }

    private void RemoveLockWalls()
    {
        foreach (GameObject wall in lockWalls)
        {
            if (wall != null)
                Destroy(wall);
        }

        lockWalls.Clear();
    }

    private void OnDrawGizmos()
    {
        if (!DrawEnemyPositionGizmos)
            return;

        Gizmos.color = EnemyPositionColor;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null)
                continue;

            Vector3 position = enemy.transform.position;
            Gizmos.DrawWireSphere(position, EnemyPositionRadius);
            Gizmos.DrawLine(position + Vector3.left * EnemyPositionRadius, position + Vector3.right * EnemyPositionRadius);
            Gizmos.DrawLine(position + Vector3.down * EnemyPositionRadius, position + Vector3.up * EnemyPositionRadius);
        }
    }
}
