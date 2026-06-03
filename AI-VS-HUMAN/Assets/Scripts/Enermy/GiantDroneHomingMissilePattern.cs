using UnityEngine;

[RequireComponent(typeof(GiantDrone))]
public class GiantDroneHomingMissilePattern : MonoBehaviour
{
    private const float TurnSpeed = 180f;
    private const float HomingDuration = 1.5f;
    private const float Lifetime = 4f;
    private const float SpawnOffset = 1.2f;

    [Header("유도탄")]
    public GameObject homingMissilePrefab;
    public float damage = 1f;
    public float speed = 5f;
    public float cooldown = 3f;

    public void Fire(GiantDrone boss)
    {
        if (boss == null)
            return;

        GameObject prefab = homingMissilePrefab != null
            ? homingMissilePrefab
            : boss.uDashPattern != null ? boss.uDashPattern.fanBulletPrefab : null;

        if (prefab == null || boss.player == null)
            return;

        Vector2[] directions =
        {
            new Vector2(1f, 1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(1f, -1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        foreach (Vector2 direction in directions)
        {
            Vector3 spawnPosition = transform.position + (Vector3)(direction * SpawnOffset);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject obj = Instantiate(prefab, spawnPosition, Quaternion.Euler(0f, 0f, angle));

            HomingMissileBullet missile = obj.GetComponent<HomingMissileBullet>();
            if (missile == null)
                missile = obj.AddComponent<HomingMissileBullet>();

            missile.Init(direction, boss.player, damage, speed, TurnSpeed, HomingDuration, Lifetime);
        }
    }

    public void StopPattern()
    {
        StopAllCoroutines();
    }
}
