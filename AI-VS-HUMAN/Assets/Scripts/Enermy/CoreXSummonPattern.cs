using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoreXSummonPattern : MonoBehaviour
{
    [Header("Summon")]
    public GameObject ghostPrefab;
    public GameObject shadowPrefab;
    public float interval = 8f;
    public int ghostCount = 1;
    public int shadowCount = 1;

    private CoreXBoss boss;
    private Coroutine loopCoroutine;
    private readonly List<GameObject> spawnedMinions = new List<GameObject>();

    public void StartPattern(CoreXBoss boss)
    {
        StopPattern();
        this.boss = boss;
        loopCoroutine = StartCoroutine(SummonLoop());
    }

    public void StopPattern()
    {
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }
    }

    public void ClearSpawnedMinions()
    {
        foreach (GameObject obj in spawnedMinions)
        {
            if (obj != null)
                Destroy(obj);
        }

        spawnedMinions.Clear();
    }

    private IEnumerator SummonLoop()
    {
        yield return new WaitForSeconds(interval * 0.5f);

        while (boss != null && !boss.IsDead)
        {
            SummonMinions();
            yield return new WaitForSeconds(interval);
        }
    }

    private void SummonMinions()
    {
        if (boss == null || boss.BossRoom == null)
            return;

        Bounds bounds = boss.BossRoom.GetBounds();
        float margin = 1.5f;
        float minX = bounds.min.x + margin;
        float maxX = bounds.max.x - margin;
        float minY = bounds.min.y + margin;
        float maxY = bounds.max.y - margin;

        for (int i = 0; i < ghostCount + boss.ServersDestroyed; i++)
        {
            if (ghostPrefab == null)
                break;

            Vector2 pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            GameObject obj = Instantiate(ghostPrefab, pos, Quaternion.identity);
            spawnedMinions.Add(obj);

            GhostEnemy ghost = obj.GetComponent<GhostEnemy>();
            if (ghost != null)
                ghost.moveSpeed = Random.Range(0.8f, 1.4f);
        }

        StartCoroutine(SummonShadowsSequential());
    }

    private IEnumerator SummonShadowsSequential()
    {
        for (int i = 0; i < shadowCount; i++)
        {
            if (boss == null || boss.IsDead || shadowPrefab == null || boss.Player == null)
                yield break;

            float dirX = i % 2 == 0 ? 3f : -3f;
            Vector3 spawnPos = boss.Player.position + new Vector3(dirX, 0f, 0f);
            GameObject obj = Instantiate(shadowPrefab, spawnPos, Quaternion.identity);
            spawnedMinions.Add(obj);

            ShadowEnemy shadow = obj.GetComponent<ShadowEnemy>();
            if (shadow != null)
                shadow.recordDelay = 3f + i * 3f;

            yield return new WaitForSeconds(2f);
        }
    }
}
