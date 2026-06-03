using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GiantDrone))]
public class GiantDronePetalPattern : MonoBehaviour
{
    private const float FireInterval = 0.14f;
    private const float Curvature = 1.2f;
    private const float RotatePerShot = 8f;
    private const float SpawnOffset = 1.5f;
    private const float PetalMoveSpeedMultiplier = 0.45f;

    [Header("꽃잎 탄막")]
    public GameObject petalBulletPrefab;
    public int armCount = 6;
    public int bulletsPerArm = 14;
    public float bulletSpeed = 3f;
    public float loopDelay = 3f;

    internal float MoveSpeedMultiplier => PetalMoveSpeedMultiplier;

    public IEnumerator Run(GiantDrone boss)
    {
        if (boss == null || petalBulletPrefab == null)
            yield break;

        boss.isDoingPetal = true;
        int safeArmCount = Mathf.Max(1, armCount);
        float angleStep = 360f / safeArmCount;

        for (int shot = 0; shot < bulletsPerArm; shot++)
        {
            if (boss.isDead)
                break;

            for (int arm = 0; arm < safeArmCount; arm++)
            {
                float angle = boss.petalBaseAngle + angleStep * arm;
                Vector2 dir = AngleToDir(angle);
                float curveDir = arm % 2 == 0 ? 1f : -1f;

                GameObject go = Instantiate(petalBulletPrefab, transform.position, Quaternion.identity);
                PetalBullet pb = go.GetComponent<PetalBullet>() ?? go.AddComponent<PetalBullet>();
                pb.Init(dir, bulletSpeed, Curvature * curveDir, 3.5f, SpawnOffset);
            }

            boss.petalBaseAngle -= RotatePerShot;
            yield return new WaitForSeconds(FireInterval);
        }

        boss.isDoingPetal = false;
    }

    public void StopPattern()
    {
        StopAllCoroutines();
    }

    private Vector2 AngleToDir(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
