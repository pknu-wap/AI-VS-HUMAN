using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GiantDrone))]
public class GiantDroneUDashPattern : MonoBehaviour
{
    [Header("돌진")]
    public float dashDropY = 6f;
    public float dashWidth = 10f;
    public float dashSpeed = 8f;

    [Header("부채꼴 탄막")]
    public GameObject fanBulletPrefab;
    public int fanBulletCount = 16;
    public float fanSpreadAngle = 150f;
    public int fanDashVolleyCount = 4;
    public float fanDashFireDelay = 0.25f;
    public float fanFireOffset = 0.8f;
    public float fanBulletSpeed = 6f;
    public float fanBulletDamage = 1f;
    public float fanCooldown = 4f;

    public IEnumerator Run(GiantDrone boss)
    {
        if (boss == null || boss.isDead || boss.player == null)
            yield break;

        boss.isDoingUDash = true;
        LayerMask groundMask = LayerMask.GetMask("Ground");

        Vector3 p0 = transform.position;
        float dirX = boss.player.position.x > transform.position.x ? 1f : -1f;
        Vector3 p2 = new Vector3(p0.x + dashWidth * dirX, p0.y, 0f);
        Vector3 p1 = new Vector3((p0.x + p2.x) * 0.5f, p0.y - (dashDropY * 2f), 0f);

        float duration = Mathf.Max(dashWidth / dashSpeed, 1.2f);
        float elapsed = 0f;
        int firedCount = 0;
        float nextFireTime = 0f;

        while (elapsed < duration && !boss.isDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 m1 = Vector3.Lerp(p0, p1, easeT);
            Vector3 m2 = Vector3.Lerp(p1, p2, easeT);
            Vector3 targetPosition = Vector3.Lerp(m1, m2, easeT);
            bool reachedTarget = boss.MoveToSafePosition(targetPosition, groundMask);

            if (!reachedTarget)
            {
                boss.ResolveCurrentWallOverlap(groundMask);
                break;
            }

            if (easeT >= 0.2f && firedCount < fanDashVolleyCount)
            {
                if (Time.time >= nextFireTime)
                {
                    FireFanBullets(boss);
                    firedCount++;
                    nextFireTime = Time.time + fanDashFireDelay;
                }
            }

            yield return null;
        }

        if (firedCount == 0 && !boss.isDead)
            FireFanBullets(boss);

        boss.swayBaseX = transform.position.x;
        boss.isDoingUDash = false;
    }

    public void StopPattern()
    {
        StopAllCoroutines();
    }

    private void FireFanBullets(GiantDrone boss)
    {
        if (fanBulletPrefab == null || boss.player == null)
            return;

        Vector2 aimDir = ((Vector2)boss.player.position - (Vector2)transform.position).normalized;
        if (aimDir.sqrMagnitude < 0.001f)
            aimDir = Vector2.down;

        Vector3 firePos = transform.position + (Vector3)(aimDir * fanFireOffset);
        float baseAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - fanSpreadAngle * 0.5f;
        float step = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + step * i;
            Vector2 dir = AngleToDir(angle);
            SpawnBullet(fanBulletPrefab, firePos, dir, fanBulletDamage, fanBulletSpeed, angle);
        }
    }

    private Vector2 AngleToDir(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void SpawnBullet(GameObject prefab, Vector3 spawnPos, Vector2 dir, float damage, float speed, float angle)
    {
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
        Bullet bullet = obj.GetComponent<Bullet>();
        if (bullet != null)
            bullet.Init(dir, damage, speed);
    }
}
