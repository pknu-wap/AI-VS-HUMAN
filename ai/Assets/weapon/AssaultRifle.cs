using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AssaultRifle : MonoBehaviour
{
    [Header("Rifle Settings")]
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _bulletSpeed = 25f;
    [SerializeField] private float _maxDistance = 20f;
    [SerializeField] private float _fireRate = 0.1f;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private LayerMask _wallLayer;

    [Header("Bullet Visual")]
    [SerializeField] private Color _bulletColor = Color.yellow;
    [SerializeField] private float _bulletWidth = 0.08f;
    [SerializeField] private float _bulletLength = 0.3f;

    private InputController _input;
    private Camera _cam;
    private bool _canFire = true;

    private void Awake()
    {
        _input = GetComponent<InputController>();
        _cam   = Camera.main;

        // ✅ 클릭할 때마다 한 발
        _input.OnFireEvent += HandleFire;
    }

    private void HandleFire(Vector2 mousePos)
    {
        if (!_canFire) return;

        Vector2 origin = new Vector2(
            transform.position.x,
            transform.position.y);

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
        GameObject   bullet = new GameObject("Bullet");
        LineRenderer lr     = bullet.AddComponent<LineRenderer>();

        lr.material = new Material(
            Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lr.startColor    = _bulletColor;
        lr.endColor      = new Color(
            _bulletColor.r, _bulletColor.g, _bulletColor.b, 0f);
        lr.startWidth    = _bulletWidth;
        lr.endWidth      = _bulletWidth * 0.3f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.sortingOrder  = 100;

        float traveledDistance = 0f;
        bool  shouldDestroy    = false;

        while (traveledDistance < _maxDistance)
        {
            float step        = _bulletSpeed * Time.deltaTime;
            traveledDistance += step;

            Vector2 currentPos = origin + dir * traveledDistance;
            Vector2 tailPos    = currentPos - dir * _bulletLength;

            bullet.transform.position = new Vector3(
                currentPos.x, currentPos.y, 0f);

            lr.SetPosition(0, new Vector3(tailPos.x,    tailPos.y,    0f));
            lr.SetPosition(1, new Vector3(currentPos.x, currentPos.y, 0f));

            // 적 충돌
            RaycastHit2D enemyHit = Physics2D.Raycast(
                currentPos - dir * step, dir, step, _enemyLayer);

            if (enemyHit.collider != null)
            {
                if (enemyHit.collider.TryGetComponent<IDamageable>(
                    out var target))
                    target.TakeDamage(_damage);

                StartCoroutine(HitEffect(enemyHit.point));
                shouldDestroy = true;
                break;
            }

            // 벽 충돌
            RaycastHit2D wallHit = Physics2D.Raycast(
                currentPos - dir * step, dir, step, _wallLayer);

            if (wallHit.collider != null)
            {
                StartCoroutine(HitEffect(wallHit.point));
                shouldDestroy = true;
                break;
            }

            yield return null;
        }

        if (shouldDestroy)
        {
            Destroy(bullet);
        }
        else
        {
            yield return StartCoroutine(FadeBullet(lr));
            if (bullet != null) Destroy(bullet);
        }
    }

    private IEnumerator FadeBullet(LineRenderer lr)
    {
        if (lr == null) yield break;

        float elapsed    = 0f;
        float fadeTime   = 0.1f;
        Color startColor = _bulletColor;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);

            if (lr != null)
            {
                lr.startColor = new Color(
                    startColor.r, startColor.g, startColor.b, alpha);
                lr.endColor   = new Color(
                    startColor.r, startColor.g, startColor.b, 0f);
            }
            yield return null;
        }
    }

    private IEnumerator HitEffect(Vector2 pos)
    {
        GameObject   effect = new GameObject("HitEffect");
        effect.transform.position = new Vector3(pos.x, pos.y, 0f);

        LineRenderer lr  = effect.AddComponent<LineRenderer>();
        lr.material      = new Material(
            Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lr.startColor    = Color.white;
        lr.endColor      = new Color(1f, 1f, 0f, 0f);
        lr.startWidth    = _bulletWidth * 3f;
        lr.endWidth      = 0f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.sortingOrder  = 100;
        lr.SetPosition(0, new Vector3(pos.x, pos.y,        0f));
        lr.SetPosition(1, new Vector3(pos.x, pos.y + 0.3f, 0f));

        float elapsed  = 0f;
        float fadeTime = 0.1f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            lr.startColor = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        Destroy(effect);
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnFireEvent -= HandleFire;
    }
}