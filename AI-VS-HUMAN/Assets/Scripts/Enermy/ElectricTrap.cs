using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 전기 장판
/// - LineRenderer로 번개 이펙트 생성
/// - 플레이어가 밟으면 속박
/// - duration 후 자동 제거
/// </summary>
public class ElectricTrap : MonoBehaviour
{
    [Header("설정")]
    public float duration     = 5f;   // 장판 지속 시간
    public float bindDuration = 2f;   // 속박 시간
    public Vector2 trapSize   = new Vector2(2f, 0.3f); // 장판 크기

    [Header("번개 이펙트")]
    public Color lightningColor  = new Color(0.3f, 0.7f, 1f, 1f);  // 번개 색상
    public Color glowColor       = new Color(0f, 0.4f, 1f, 0.3f);  // 배경 발광
    public int   boltCount       = 5;    // 번개 줄기 수
    public int   segmentCount    = 8;    // 번개 꺾임 수
    public float amplitude       = 0.15f; // 번개 흔들림 크기
    public float updateInterval  = 0.05f; // 번개 갱신 속도 (빠를수록 지직거림)
    public float lineWidth       = 0.04f;

    private List<LineRenderer> _bolts = new List<LineRenderer>();
    private SpriteRenderer     _glow;
    private LayerMask          _playerMask;
    private bool               _isActive = false;
    private bool               _isBound  = false;

    void Start()
    {
        _playerMask = LayerMask.GetMask("Player");

        CreateGlow();
        CreateLightningBolts();

        _isActive = true;
        StartCoroutine(AnimateLightning());
        StartCoroutine(LifetimeRoutine());
    }

    // ── 배경 발광 생성 ──────────────────────────
    void CreateGlow()
    {
        GameObject obj = new GameObject("Glow");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        _glow         = obj.AddComponent<SpriteRenderer>();
        _glow.sprite  = CreateWhiteSprite();
        _glow.color   = glowColor;
        _glow.sortingOrder = 5;

        obj.transform.localScale = new Vector3(trapSize.x, trapSize.y, 1f);
    }

    // ── 번개 줄기 생성 ──────────────────────────
    void CreateLightningBolts()
    {
        for (int i = 0; i < boltCount; i++)
        {
            GameObject   obj = new GameObject($"Bolt_{i}");
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;

            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.material          = CreateLightningMaterial();
            lr.startColor        = lightningColor;
            lr.endColor          = new Color(lightningColor.r, lightningColor.g, lightningColor.b, 0f);
            lr.startWidth        = lineWidth;
            lr.endWidth          = lineWidth * 0.3f;
            lr.positionCount     = segmentCount;
            lr.useWorldSpace     = true;
            lr.sortingOrder      = 10;

            _bolts.Add(lr);
        }
    }

    // ── 번개 애니메이션 ─────────────────────────
    IEnumerator AnimateLightning()
    {
        while (_isActive)
        {
            foreach (LineRenderer bolt in _bolts)
                UpdateBolt(bolt);

            // 배경 발광 깜빡임
            if (_glow != null)
            {
                float alpha = Random.Range(0.1f, 0.4f);
                _glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
            }

            yield return new WaitForSeconds(updateInterval);
        }
    }

    void UpdateBolt(LineRenderer lr)
    {
        if (lr == null) return;

        // 장판 왼쪽 끝 → 오른쪽 끝으로 번개 생성
        float   halfW    = trapSize.x * 0.5f;
        float   startY   = Random.Range(-trapSize.y * 0.3f, trapSize.y * 0.3f);
        float   endY     = Random.Range(-trapSize.y * 0.3f, trapSize.y * 0.3f);
        Vector3 startPos = transform.position + new Vector3(-halfW, startY, 0f);
        Vector3 endPos   = transform.position + new Vector3( halfW, endY,   0f);

        for (int i = 0; i < segmentCount; i++)
        {
            float   t      = (float)i / (segmentCount - 1);
            Vector3 point  = Vector3.Lerp(startPos, endPos, t);
            point.y       += Random.Range(-amplitude, amplitude);
            lr.SetPosition(i, point);
        }
    }

    // ── 플레이어 감지 ───────────────────────────
    void Update()
    {
        if (!_isActive || _isBound) return;

        Collider2D hit = Physics2D.OverlapBox(
            transform.position,
            trapSize,
            0f,
            _playerMask);

        if (hit == null) return;

        PlayerMove pm = hit.GetComponent<PlayerMove>();
        if (pm == null) return;

        _isBound = true;
        StartCoroutine(BindPlayer(pm));
    }

    IEnumerator BindPlayer(PlayerMove playerMove)
    {
        if (playerMove == null) yield break;

        // 속박 시 번개 빨갛게
        foreach (LineRenderer bolt in _bolts)
            if (bolt != null) { bolt.startColor = Color.red; bolt.endColor = new Color(1f,0f,0f,0f); }
        if (_glow != null) _glow.color = new Color(1f, 0.2f, 0.2f, 0.3f);

        playerMove.enabled = false;
        yield return new WaitForSeconds(bindDuration);

        if (playerMove != null) playerMove.enabled = true;

        _isActive = false;
        StartCoroutine(FadeOutAndDestroy());
    }

    // ── 수명 관리 ───────────────────────────────
    IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(duration);
        _isActive = false;
        StartCoroutine(FadeOutAndDestroy());
    }

    IEnumerator FadeOutAndDestroy()
    {
        float fadeTime = 0.3f;
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            float alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            foreach (LineRenderer bolt in _bolts)
            {
                if (bolt == null) continue;
                Color c = bolt.startColor;
                bolt.startColor = new Color(c.r, c.g, c.b, alpha);
            }
            if (_glow != null)
            {
                Color c = _glow.color;
                _glow.color = new Color(c.r, c.g, c.b, alpha * 0.3f);
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ── 유틸 ────────────────────────────────────
    Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    Material CreateLightningMaterial()
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return new Material(shader);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(trapSize.x, trapSize.y, 0f));
    }
}
