using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Room))]
public class RoomEnemyEncounter : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Room room;

    [Header("Enemies")]
    public List<GameObject> enemies = new List<GameObject>();
    public bool deactivateEnemiesOnStart = true;
    public bool triggerOnce = true;

    [Header("Lock Walls")]
    public bool createLockWalls = true;
    public float wallThickness = 1f;
    public float wallPadding = 0f;
    public string wallLayerName = "Ground";

    [Header("Editor Gizmos")]
    public bool drawEnemyPositionGizmos = true;
    public Color enemyPositionColor = new Color(1f, 0.25f, 0.15f, 0.85f);
    public float enemyPositionRadius = 0.35f;

    private readonly List<GameObject> lockWalls = new List<GameObject>();
    private bool encounterStarted;
    private bool encounterCleared;

    private void Awake()
    {
        ResolveReferences();

        if (deactivateEnemiesOnStart && !encounterStarted)
            SetEnemiesActive(false);
    }

    private void Update()
    {
        ResolveReferences();

        if (encounterCleared && triggerOnce)
            return;

        if (!encounterStarted)
        {
            if (IsPlayerInsideRoom())
                StartEncounter();

            return;
        }

        if (!HasRemainingEnemies())
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
    }

    private bool IsPlayerInsideRoom()
    {
        return room != null && player != null && room.GetBounds().Contains(player.position);
    }

    private void StartEncounter()
    {
        encounterStarted = true;
        encounterCleared = false;

        SetEnemiesActive(true);

        if (createLockWalls && HasRemainingEnemies())
            CreateLockWalls();
    }

    private void ClearEncounter()
    {
        encounterCleared = true;
        encounterStarted = false;
        RemoveLockWalls();
    }

    private void SetEnemiesActive(bool active)
    {
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
                enemy.SetActive(active);
        }
    }

    private bool HasRemainingEnemies()
    {
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
                return true;
        }

        return false;
    }

    private void CreateLockWalls()
    {
        RemoveLockWalls();

        if (room == null)
            return;

        Bounds bounds = room.GetBounds();
        float thickness = Mathf.Max(0.05f, wallThickness);
        float padding = Mathf.Max(0f, wallPadding);
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

        int layer = LayerMask.NameToLayer(wallLayerName);
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

    private void OnDisable()
    {
        RemoveLockWalls();
    }

    private void OnDrawGizmos()
    {
        if (!drawEnemyPositionGizmos)
            return;

        Gizmos.color = enemyPositionColor;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null)
                continue;

            Vector3 position = enemy.transform.position;
            Gizmos.DrawWireSphere(position, enemyPositionRadius);
            Gizmos.DrawLine(position + Vector3.left * enemyPositionRadius, position + Vector3.right * enemyPositionRadius);
            Gizmos.DrawLine(position + Vector3.down * enemyPositionRadius, position + Vector3.up * enemyPositionRadius);
        }
    }
}
