using UnityEngine;

[RequireComponent(typeof(GiantDrone))]
public class GiantDroneHomingMissilePattern : MonoBehaviour
{
    [Header("유도탄 패턴")]
    public GameObject homingMissilePrefab;
    public float damage = 1f;
    public float speed = 5f;
    public float turnSpeed = 180f;
    public float homingDuration = 1.5f;
    public float lifetime = 4f;
    public float spawnOffset = 1.2f;
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
            Vector3 spawnPosition = transform.position + (Vector3)(direction * spawnOffset);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject obj = Instantiate(prefab, spawnPosition, Quaternion.Euler(0f, 0f, angle));

            HomingMissileBullet missile = obj.GetComponent<HomingMissileBullet>();
            if (missile == null)
                missile = obj.AddComponent<HomingMissileBullet>();

            missile.Init(
                direction,
                boss.player,
                damage,
                speed,
                turnSpeed,
                homingDuration,
                lifetime);
        }
    }

    public void StopPattern()
    {
        StopAllCoroutines();
    }
}
