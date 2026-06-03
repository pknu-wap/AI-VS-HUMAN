using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class AssaultRifle : MonoBehaviour
{
    private const float BulletWidth = 0.08f;
    private const float BulletLength = 0.3f;
    private const int MaxBullets = 10;
    private const float BulletLifetime = 0.5f;
    private const bool PollMouseInput = true;
    private static readonly Color BulletColor = Color.yellow;

    [Header("소총")]
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _bulletSpeed = 25f;
    [SerializeField] private float _maxDistance = 20f;
    [SerializeField] private float _fireRate = 0.1f;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private LayerMask _wallLayer;

    private InputController _input;
    private Camera _cam;
    private bool _canFire = true;
    private static Material _lineMaterial;
    private int _currentBulletCount;

    private void Awake()
    {
        _input = GetComponent<InputController>();
        _cam = Camera.main;

        if (_input != null)
            _input.OnFireEvent += HandleFire;
    }

    private void Update()
    {
        if (!PollMouseInput || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        HandleFire(GetMouseWorldPosition());
    }

    private void HandleFire(Vector2 mousePos)
    {
        if (!_canFire || _currentBulletCount >= MaxBullets)
            return;

        Vector2 origin = transform.position;
        Vector2 dir = (mousePos - origin).normalized;

        StartCoroutine(MoveBullet(origin, dir));
        StartCoroutine(FireCooldown());
    }

    private IEnumerator FireCooldown()
    {
        _canFire = false;
        yield return new WaitForSeconds(_fireRate);
        _canFire = true;
    }

    private IEnumerator MoveBullet(Vector2 origin, Vector2 dir)
    {
        _currentBulletCount++;

        GameObject bullet = new GameObject("Bullet");
        LineRenderer lineRenderer = bullet.AddComponent<LineRenderer>();
        ConfigureBulletLine(lineRenderer);

        float traveledDistance = 0f;
        float timeAlive = 0f;
        bool shouldDestroy = false;

        while (traveledDistance < _maxDistance && timeAlive < BulletLifetime)
        {
            float step = _bulletSpeed * Time.deltaTime;
            traveledDistance += step;
            timeAlive += Time.deltaTime;

            Vector2 currentPos = origin + dir * traveledDistance;
            Vector2 tailPos = currentPos - dir * BulletLength;

            if (bullet != null)
            {
                bullet.transform.position = new Vector3(currentPos.x, currentPos.y, 0f);
                lineRenderer.SetPosition(0, new Vector3(tailPos.x, tailPos.y, 0f));
                lineRenderer.SetPosition(1, new Vector3(currentPos.x, currentPos.y, 0f));
            }

            if (TryHitTarget(currentPos - dir * step, dir, step))
            {
                shouldDestroy = true;
                break;
            }

            yield return null;
        }

        _currentBulletCount--;

        if (bullet == null)
            yield break;

        if (shouldDestroy)
        {
            Destroy(bullet);
            yield break;
        }

        yield return StartCoroutine(FadeBullet(lineRenderer));
        if (bullet != null)
            Destroy(bullet);
    }

    private void ConfigureBulletLine(LineRenderer lineRenderer)
    {
        lineRenderer.material = GetLineMaterial();
        lineRenderer.startColor = BulletColor;
        lineRenderer.endColor = new Color(BulletColor.r, BulletColor.g, BulletColor.b, 0f);
        lineRenderer.startWidth = BulletWidth;
        lineRenderer.endWidth = BulletWidth * 0.3f;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 100;
    }

    private bool TryHitTarget(Vector2 origin, Vector2 dir, float distance)
    {
        RaycastHit2D enemyHit = Physics2D.Raycast(origin, dir, distance, _enemyLayer);
        if (enemyHit.collider != null)
        {
            if (enemyHit.collider.TryGetComponent<IDamageable>(out var target))
                target.TakeDamage(_damage);

            StartCoroutine(HitEffect(enemyHit.point));
            return true;
        }

        RaycastHit2D wallHit = Physics2D.Raycast(origin, dir, distance, _wallLayer);
        if (wallHit.collider != null)
        {
            StartCoroutine(HitEffect(wallHit.point));
            return true;
        }

        return false;
    }

    private IEnumerator FadeBullet(LineRenderer lineRenderer)
    {
        if (lineRenderer == null)
            yield break;

        const float fadeTime = 0.05f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            if (lineRenderer != null)
            {
                lineRenderer.startColor = new Color(BulletColor.r, BulletColor.g, BulletColor.b, alpha);
                lineRenderer.endColor = new Color(BulletColor.r, BulletColor.g, BulletColor.b, 0f);
            }
            yield return null;
        }
    }

    private IEnumerator HitEffect(Vector2 pos)
    {
        GameObject effect = new GameObject("HitEffect");
        effect.transform.position = new Vector3(pos.x, pos.y, 0f);

        LineRenderer lineRenderer = effect.AddComponent<LineRenderer>();
        lineRenderer.sharedMaterial = GetLineMaterial();
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = new Color(1f, 1f, 0f, 0f);
        lineRenderer.startWidth = BulletWidth * 3f;
        lineRenderer.endWidth = 0f;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 100;
        lineRenderer.SetPosition(0, new Vector3(pos.x, pos.y, 0f));
        lineRenderer.SetPosition(1, new Vector3(pos.x, pos.y + 0.3f, 0f));

        const float fadeTime = 0.1f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            if (lineRenderer != null)
                lineRenderer.startColor = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        Destroy(effect);
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnFireEvent -= HandleFire;
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (_cam == null)
            _cam = Camera.main;

        if (_cam == null)
            return transform.position;

        Vector3 screenPosition = Mouse.current.position.ReadValue();
        screenPosition.z = GetScreenToWorldDepth(_cam);
        return _cam.ScreenToWorldPoint(screenPosition);
    }

    private float GetScreenToWorldDepth(Camera targetCamera)
    {
        float depth = Mathf.Abs(targetCamera.transform.position.z - transform.position.z);
        return Mathf.Max(depth, targetCamera.nearClipPlane + 0.01f);
    }

    private static Material GetLineMaterial()
    {
        if (_lineMaterial != null)
            return _lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
        if (shader == null)
            shader = Shader.Find("UI/Default");

        _lineMaterial = new Material(shader);
        _lineMaterial.name = "AssaultRifle Line Material";
        return _lineMaterial;
    }
}
