using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoreXElectricTrapPattern : MonoBehaviour
{
    private const int BoltCount = 4;
    private static readonly Color TrapColor = new Color(0.3f, 0.7f, 1f, 1f);

    [Header("전기 함정")]
    public int trapCount = 2;
    public float interval = 10f;
    public float duration = 5f;
    public float bindDuration = 2f;
    public float width = 3f;

    private CoreXBoss boss;
    private Coroutine loopCoroutine;
    private PlayerMove boundPlayer;
    private readonly List<GameObject> activeTraps = new List<GameObject>();

    public void StartPattern(CoreXBoss boss)
    {
        StopPattern();
        this.boss = boss;
        loopCoroutine = StartCoroutine(TrapLoop());
    }

    public void StopPattern()
    {
        StopAllCoroutines();
        loopCoroutine = null;

        if (boundPlayer != null)
        {
            boundPlayer.enabled = true;
            boundPlayer = null;
        }

        ClearActiveTraps();
    }

    private IEnumerator TrapLoop()
    {
        yield return new WaitForSeconds(interval * 0.5f);

        while (boss != null && !boss.IsDead)
        {
            SpawnElectricTraps();
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnElectricTraps()
    {
        if (boss == null || boss.BossRoom == null)
            return;

        Bounds bounds = boss.BossRoom.GetBounds();
        float margin = 1.5f;
        float minX = bounds.min.x + margin;
        float maxX = bounds.max.x - margin;
        float minY = bounds.min.y + margin;
        float maxY = bounds.max.y - margin;

        for (int i = 0; i < trapCount; i++)
        {
            Vector2 pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            StartCoroutine(ElectricTrapRoutine(pos));
        }
    }

    private IEnumerator ElectricTrapRoutine(Vector2 pos)
    {
        GameObject trap = new GameObject("ElectricTrap");
        trap.transform.position = pos;
        activeTraps.Add(trap);

        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(trap.transform);
        glowObj.transform.localPosition = Vector3.zero;
        SpriteRenderer glow = glowObj.AddComponent<SpriteRenderer>();
        glow.sprite = CreateWhiteSprite();
        glow.color = new Color(0f, 0.4f, 1f, 0.25f);
        glow.sortingOrder = 5;
        glowObj.transform.localScale = new Vector3(width, 0.3f, 1f);

        List<LineRenderer> bolts = new List<LineRenderer>();
        for (int i = 0; i < BoltCount; i++)
        {
            GameObject boltObj = new GameObject($"Bolt_{i}");
            boltObj.transform.SetParent(trap.transform);
            LineRenderer lr = boltObj.AddComponent<LineRenderer>();

            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            lr.material = new Material(shader);
            lr.startColor = TrapColor;
            lr.endColor = new Color(TrapColor.r, TrapColor.g, TrapColor.b, 0f);
            lr.startWidth = 0.04f;
            lr.endWidth = 0.01f;
            lr.positionCount = 8;
            lr.useWorldSpace = true;
            lr.sortingOrder = 10;
            bolts.Add(lr);
        }

        float elapsed = 0f;
        bool bound = false;
        float nextAnim = 0f;

        while (elapsed < duration && boss != null && !boss.IsDead)
        {
            elapsed += Time.deltaTime;

            if (Time.time >= nextAnim)
            {
                nextAnim = Time.time + 0.05f;
                foreach (LineRenderer bolt in bolts)
                    UpdateLightningBolt(bolt, pos, width);

                if (glow != null)
                    glow.color = new Color(0f, 0.4f, 1f, Random.Range(0.1f, 0.35f));
            }

            if (!bound)
            {
                Collider2D hit = Physics2D.OverlapBox(pos, new Vector2(width, 0.4f), 0f, LayerMask.GetMask("Player"));
                if (hit != null)
                {
                    PlayerMove playerMove = hit.GetComponent<PlayerMove>();
                    if (playerMove != null)
                    {
                        bound = true;
                        StartCoroutine(BindPlayer(playerMove, bolts, glow));
                    }
                }
            }

            yield return null;
        }

        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float alpha = Mathf.Lerp(1f, 0f, t / 0.3f);

            foreach (LineRenderer bolt in bolts)
            {
                if (bolt != null)
                    bolt.startColor = new Color(TrapColor.r, TrapColor.g, TrapColor.b, alpha);
            }

            if (glow != null)
                glow.color = new Color(0f, 0.4f, 1f, alpha * 0.25f);

            yield return null;
        }

        if (trap != null)
        {
            activeTraps.Remove(trap);
            Destroy(trap);
        }
    }

    private void UpdateLightningBolt(LineRenderer lr, Vector2 center, float width)
    {
        if (lr == null)
            return;

        float halfWidth = width * 0.5f;
        Vector3 startPos = center + new Vector2(-halfWidth, Random.Range(-0.1f, 0.1f));
        Vector3 endPos = center + new Vector2(halfWidth, Random.Range(-0.1f, 0.1f));

        for (int i = 0; i < 8; i++)
        {
            float t = (float)i / 7f;
            Vector3 point = Vector3.Lerp(startPos, endPos, t);
            point.y += Random.Range(-0.15f, 0.15f);
            lr.SetPosition(i, point);
        }
    }

    private IEnumerator BindPlayer(PlayerMove playerMove, List<LineRenderer> bolts, SpriteRenderer glow)
    {
        foreach (LineRenderer bolt in bolts)
        {
            if (bolt != null)
                bolt.startColor = Color.red;
        }

        if (glow != null)
            glow.color = new Color(1f, 0.2f, 0.2f, 0.3f);

        boundPlayer = playerMove;
        playerMove.enabled = false;

        yield return new WaitForSeconds(bindDuration);

        if (playerMove != null)
            playerMove.enabled = true;

        if (boundPlayer == playerMove)
            boundPlayer = null;
    }

    private Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private void ClearActiveTraps()
    {
        foreach (GameObject trap in activeTraps)
        {
            if (trap != null)
                Destroy(trap);
        }

        activeTraps.Clear();
    }
}
