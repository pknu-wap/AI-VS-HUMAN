using System.Collections;
using UnityEngine;

/// <summary>
/// 꽃잎(Petal) 탄막 패턴
/// </summary>
public class PetalBulletPattern : MonoBehaviour
{
    [Header("발사 설정")]
    public GameObject bulletPrefab;
    public Vector2 shootDirection = Vector2.up;
    public float bulletSpeed = 4f;
    public int bulletsPerArm = 14;
    public float fireInterval = 0.07f;

    [Header("곡률 설정")]
    public float maxCurvature = 1.4f;
    public float curvatureOscillateSpeed = 1.0f;

    [Header("반복 설정")]
    public bool loop = true;
    public float loopDelay = 0.8f;

    private float _time = 0f;

    private void Start()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("PetalBulletPattern: bulletPrefab이 비어 있어서 패턴을 실행하지 않습니다.", this);
            enabled = false;
            return;
        }

        StartCoroutine(FireLoop());
    }

    private void Update()
    {
        _time += Time.deltaTime;
    }

    private IEnumerator FireLoop()
    {
        while (true)
        {
            yield return StartCoroutine(FireOnePetal());

            if (!loop)
                yield break;

            yield return new WaitForSeconds(loopDelay);
        }
    }

    private IEnumerator FireOnePetal()
    {
        float curvature = Mathf.Sin(_time * curvatureOscillateSpeed) * maxCurvature;

        for (int b = 0; b < bulletsPerArm; b++)
        {
            SpawnBullet(curvature, b);
            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void SpawnBullet(float curvature, int index)
    {
        if (bulletPrefab == null)
            return;

        GameObject go = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

        PetalBullet bullet = go.GetComponent<PetalBullet>();
        if (bullet == null)
            bullet = go.AddComponent<PetalBullet>();

        bullet.Init(
            direction:   shootDirection.normalized,
            speed:       bulletSpeed,
            curvature:   curvature,
            maxLifetime: 2.5f
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.47f, 0.87f, 0.8f);
        Gizmos.DrawRay(transform.position, (Vector3)shootDirection.normalized * 1.5f);
    }
}

/// <summary>
/// 개별 탄환 동작
/// </summary>
public class PetalBullet : MonoBehaviour
{
    private Vector2 _dir;
    private Vector2 _perp;
    private float   _speed;
    private float   _curvature;
    private float   _maxLifetime;
    private float   _age      = 0f;
    private float   _maxDist  = 15.5f;
    private Vector2 _startPos;
    private float   _damage   = 1f;

    private LayerMask _playerMask;

    public void Init(Vector2 direction, float speed, float curvature, float maxLifetime)
    {
        _dir         = direction.normalized;
        _perp        = new Vector2(-_dir.y, _dir.x);
        _speed       = speed;
        _curvature   = curvature;
        _maxLifetime = maxLifetime;
        _startPos    = transform.position;
        _playerMask  = LayerMask.GetMask("Player");
        _age         = 0f;

        Destroy(gameObject, maxLifetime);
    }

    private void Update()
    {
        _age += Time.deltaTime;

        float progress = _age * _speed;
        float t        = progress / _maxDist;

        if (t > 1f)
        {
            Destroy(gameObject);
            return;
        }

        float   bulge  = Mathf.Sin(t * Mathf.PI) * _curvature;
        Vector2 newPos = _startPos
                       + _dir  * progress
                       + _perp * bulge;

        transform.position = newPos;

        Collider2D hit = Physics2D.OverlapCircle(newPos, 0.2f, _playerMask);
        if (hit != null)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null)
                ph.TakeDamage(1);

            Destroy(gameObject);
        }
    }
}
