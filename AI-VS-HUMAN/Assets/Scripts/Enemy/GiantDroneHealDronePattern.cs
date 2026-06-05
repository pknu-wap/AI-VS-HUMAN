using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GiantDrone))]
public class GiantDroneHealDronePattern : MonoBehaviour
{
    private const float FirstDelay = 3f;
    private const float AttachOffsetX = 3f;
    private const float AttachOffsetY = -1f;
    private const float SpawnOutsidePadding = 2f;
    private const float MinDuration = 5f;

    [Header("회복 드론")]
    public GameObject healDronePrefab;
    public int healDroneCount = 2;
    public float repeatDelay = 20f;
    public float healAmount = 30f;

    private GiantDrone boss;
    private Coroutine patternCoroutine;

    public void StartPattern(GiantDrone owner)
    {
        boss = owner;

        if (patternCoroutine != null)
            return;

        patternCoroutine = StartCoroutine(Loop());
    }

    public void StopPattern()
    {
        if (patternCoroutine != null)
        {
            StopCoroutine(patternCoroutine);
            patternCoroutine = null;
        }
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(FirstDelay);

        while (!boss.isDead)
        {
            if (healDronePrefab == null)
                yield break;

            if (boss.currentHp < boss.maxHp)
                yield return StartCoroutine(SpawnHealDrones());

            yield return new WaitForSeconds(repeatDelay);
        }

        patternCoroutine = null;
    }

    private IEnumerator SpawnHealDrones()
    {
        if (healDronePrefab == null)
            yield break;

        boss.healDroneAliveCount = 0;
        float elapsed = 0f;
        float[] sides = { -1f, 1f };
        int spawnCount = Mathf.Min(Mathf.Max(0, healDroneCount), sides.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            float side = sides[i];
            Vector3 spawnPos = GetHealDroneSpawnPosition(side);
            GameObject go = Instantiate(healDronePrefab, spawnPos, Quaternion.identity);
            HealDrone hd = go.GetComponent<HealDrone>() ?? go.AddComponent<HealDrone>();
            hd.Init(boss, side);
            boss.healDroneAliveCount++;
        }

        while (!boss.isDead && (boss.healDroneAliveCount > 0 || elapsed < MinDuration))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Vector3 GetHealDroneSpawnPosition(float side)
    {
        Camera cam = boss.mainCamera != null ? boss.mainCamera : Camera.main;
        Vector3 bossCenter = boss.GetBossVisualCenter();

        if (cam == null || !cam.orthographic)
            return bossCenter + new Vector3(side * (12f + SpawnOutsidePadding), AttachOffsetY, 0f);

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        float spawnX = cam.transform.position.x + Mathf.Sign(side) * (halfWidth + SpawnOutsidePadding);
        return new Vector3(spawnX, bossCenter.y + AttachOffsetY, 0f);
    }

    public Vector3 GetHealDroneAttachPosition(float side)
    {
        if (boss == null)
            boss = GetComponent<GiantDrone>();

        Vector3 bossCenter = boss.GetBossVisualCenter();
        return bossCenter + new Vector3(Mathf.Sign(side) * AttachOffsetX, AttachOffsetY, 0f);
    }

    public void HealFromAttachedDrone(float deltaTime)
    {
        if (boss == null)
            boss = GetComponent<GiantDrone>();

        if (boss == null || boss.isDead)
            return;

        boss.currentHp = Mathf.Clamp(boss.currentHp + healAmount * deltaTime, 0f, boss.maxHp);
        boss.UpdateHpBar();
    }

    public void OnHealDroneDestroyed()
    {
        if (boss == null)
            boss = GetComponent<GiantDrone>();

        if (boss != null)
            boss.healDroneAliveCount = Mathf.Max(0, boss.healDroneAliveCount - 1);
    }
}
