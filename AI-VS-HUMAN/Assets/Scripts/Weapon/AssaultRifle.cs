// 플레이어의 라이플 발사, 탄환 궤적 시각화, 적/벽 충돌 판정을 담당하는 스크립트
// 실제 물리 탄환 대신 LineRenderer를 빠르게 움직여 총알처럼 보이게 만든다.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(InputController))]
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

    [Header("성능 설정")]
    [SerializeField] private int _maxBullets = 10;      // 동시에 존재할 수 있는 최대 총알 수
    [SerializeField] private float _bulletLifetime = 0.5f; // 총알 최대 수명 (초)

    private InputController _input;
    private Camera _cam;
    private bool _canFire = true;
    private static Material _lineMaterial;

    // 현재 살아있는 총알 추적
    private List<Coroutine> _activeBullets = new List<Coroutine>();
    private int _currentBulletCount = 0;

    private void Awake()
    {
        _input = GetComponent<InputController>();
        _cam   = Camera.main;

        if (_input == null)
        {
            Debug.LogError("AssaultRifle needs an InputController on the same GameObject.", this);
            enabled = false;
            return;
        }

        _input.OnFireEvent += HandleFire;
    }

    private void HandleFire(Vector2 mousePos)
    {
        // 발사 속도와 동시에 존재 가능한 탄환 수를 제한해 과도한 연사를 막는다.
        if (!_canFire) return;

        // 최대 총알 수 초과 시 발사 안 함
        if (_currentBulletCount >= _maxBullets) return;

        Vector2 origin = new Vector2(transform.position.x, transform.position.y);
        Vector2 dir    = (mousePos - origin).normalized;

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
        // 매 프레임 짧은 레이캐스트를 쏴서 빠른 탄환이 적/벽을 뚫고 지나가지 않게 한다.
        _currentBulletCount++;

        GameObject   bullet = new GameObject("Bullet");
        LineRenderer lr     = bullet.AddComponent<LineRenderer>();

        lr.sharedMaterial = GetLineMaterial();
        lr.startColor    = _bulletColor;
        lr.endColor      = new Color(_bulletColor.r, _bulletColor.g, _bulletColor.b, 0f);
        lr.startWidth    = _bulletWidth;
        lr.endWidth      = _bulletWidth * 0.3f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.sortingOrder  = 100;

        float traveledDistance = 0f;
        bool  shouldDestroy    = false;
        float timeAlive        = 0f;

        while (traveledDistance < _maxDistance && timeAlive < _bulletLifetime)
        {
            float step        = _bulletSpeed * Time.deltaTime;
            traveledDistance += step;
            timeAlive        += Time.deltaTime;

            Vector2 currentPos = origin + dir * traveledDistance;
            Vector2 tailPos    = currentPos - dir * _bulletLength;

            if (bullet != null)
            {
                bullet.transform.position = new Vector3(currentPos.x, currentPos.y, 0f);
                lr.SetPosition(0, new Vector3(tailPos.x,    tailPos.y,    0f));
                lr.SetPosition(1, new Vector3(currentPos.x, currentPos.y, 0f));
            }

            // 적 충돌
            RaycastHit2D enemyHit = Physics2D.Raycast(
                currentPos - dir * step, dir, step, _enemyLayer);

            if (enemyHit.collider != null)
            {
                if (enemyHit.collider.TryGetComponent<IDamageable>(out var target))
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

        _currentBulletCount--;

        if (bullet != null)
        {
            if (shouldDestroy)
                Destroy(bullet);
            else
            {
                // 수명 다 됐거나 최대 거리 도달 시 빠르게 페이드
                yield return StartCoroutine(FadeBullet(lr));
                if (bullet != null) Destroy(bullet);
            }
        }
    }

    private IEnumerator FadeBullet(LineRenderer lr)
    {
        if (lr == null) yield break;

        float elapsed  = 0f;
        float fadeTime = 0.05f;     // 0.1 → 0.05 로 단축

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            if (lr != null)
            {
                lr.startColor = new Color(_bulletColor.r, _bulletColor.g, _bulletColor.b, alpha);
                lr.endColor   = new Color(_bulletColor.r, _bulletColor.g, _bulletColor.b, 0f);
            }
            yield return null;
        }
    }

    private IEnumerator HitEffect(Vector2 pos)
    {
        GameObject   effect = new GameObject("HitEffect");
        effect.transform.position = new Vector3(pos.x, pos.y, 0f);

        LineRenderer lr  = effect.AddComponent<LineRenderer>();
        lr.sharedMaterial = GetLineMaterial();
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
            if (lr != null)
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

    private static Material GetLineMaterial()
    {
        // 총알과 피격 이펙트가 같은 런타임 Material을 공유하도록 캐싱한다.
        if (_lineMaterial == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
            _lineMaterial = new Material(shader);
            _lineMaterial.name = "AssaultRifle Line Material";
        }

        return _lineMaterial;
    }
}
