// 플레이어의 좌우 이동, 점프, 대시, 지면 감지를 담당하는 스크립트
// Ground와 Platform 레이어를 따로 검사해서 일반 바닥과 일방향 플랫폼 위에서 점프를 충전한다.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(InputController))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpd = 5f;
    [SerializeField] private float _jumpPower = 12f;
    [SerializeField] private float _stopThreshold = 0.1f;

    [Header("Dash Settings")]
    [SerializeField] private float _dashPower = 20f;
    [SerializeField] private float _dashTime = 0.2f;
    [SerializeField] private float _dashCooldown = 1f;
    private bool _canDash = true;
    private bool _isDashing;
    private Coroutine _dashCoroutine;

    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _platformLayer;
    [SerializeField] private int _maxJumpCount = 2;
    [SerializeField] private float _groundCheckRadius = 0.2f;  // 발 감지 원 크기
    [SerializeField] private Vector2 _groundCheckOffset = new Vector2(0f, -0.5f); // 발 위치 오프셋

    private Rigidbody2D _rigid;
    private Collider2D _collider;
    private InputController _input;
    private PlayerDashKnockback _dashKnockback;
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[6];

    private float _currentX;
    private float _facingDir = 1f;
    private int _jumpRemain;
    private bool _isGrounded;

    private void Awake()
    {
        _input         = GetComponent<InputController>();
        _rigid         = GetComponent<Rigidbody2D>();
        _collider      = GetComponent<Collider2D>();
        _dashKnockback = GetComponent<PlayerDashKnockback>();

        if (_input == null || _rigid == null)
        {
            Debug.LogError("PlayerMove needs Rigidbody2D and InputController on the same GameObject.", this);
            enabled = false;
            return;
        }

        _input.OnMoveEvent += HandleMove;
        _input.OnJumpEvent += HandleJump;
        _input.OnDashEvent += HandleDash;
    }

    private void FixedUpdate()
    {
        if (!_isDashing)
            CheckGrounded();

        if (_isDashing) return;

        if (Mathf.Abs(_currentX) < _stopThreshold)
            _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocity.y);
        else
            _rigid.linearVelocity = new Vector2(_currentX * _moveSpd, _rigid.linearVelocity.y);
    }

    private void CheckGrounded()
    {
        // 발 기준 세 지점에서 아래로 검사해서 모서리에서도 안정적으로 착지 판정한다.
        Vector2 feetPos = (Vector2)transform.position + _groundCheckOffset;

        bool wasGrounded = _isGrounded;
        _isGrounded = IsGroundBelow(feetPos);

        if (!wasGrounded && _isGrounded)
            _jumpRemain = _maxJumpCount;

        Debug.DrawRay(feetPos, Vector2.down * _groundCheckRadius, Color.red);
    }

    private bool IsGroundBelow(Vector2 feetPos)
    {
        // 상승 중에는 플랫폼 옆면이나 아래쪽 접촉으로 점프가 충전되지 않게 한다.
        if (_rigid.linearVelocity.y > 0.1f)
            return false;

        float halfWidth = 0.2f;
        float playerBottomY = feetPos.y;
        if (_collider != null)
        {
            halfWidth = Mathf.Max(0.05f, _collider.bounds.extents.x * 0.8f);
            playerBottomY = _collider.bounds.min.y;
        }

        return HasGroundHit(feetPos, playerBottomY)
            || HasGroundHit(feetPos + Vector2.left * halfWidth, playerBottomY)
            || HasGroundHit(feetPos + Vector2.right * halfWidth, playerBottomY);
    }

    private bool HasGroundHit(Vector2 origin, float playerBottomY)
    {
        // Ground와 Platform을 함께 검사하되, Platform은 위에서 밟았을 때만 착지로 인정한다.
        int hitCount = Physics2D.RaycastNonAlloc(
            origin + Vector2.up * 0.12f,
            Vector2.down,
            _groundHits,
            _groundCheckRadius + 0.2f,
            _groundLayer.value | _platformLayer.value);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = _groundHits[i].collider;
            if (hitCollider == null || hitCollider == _collider)
                continue;

            if (_groundHits[i].normal.y < 0.5f)
                continue;

            if (IsPlatform(hitCollider) && playerBottomY < _groundHits[i].point.y - 0.12f)
                continue;

            return true;
        }

        return false;
    }

    private bool IsPlatform(Collider2D target)
    {
        return ((_platformLayer.value & (1 << target.gameObject.layer)) != 0)
            || target.GetComponent<PlatformEffector2D>() != null
            || target.GetComponentInParent<PlatformEffector2D>() != null;
    }

    private void HandleMove(float x)
    {
        _currentX = x;
        if (Mathf.Abs(x) > _stopThreshold)
            _facingDir = Mathf.Sign(x);
    }

    private void HandleJump()
    {
        if (_isDashing) return;

        if (_isGrounded)
        {
            _rigid.linearVelocity = new Vector2(_rigid.linearVelocity.x, 0);
            _rigid.AddForce(Vector2.up * _jumpPower, ForceMode2D.Impulse);
            _jumpRemain = _maxJumpCount - 1;
        }
        else if (_jumpRemain > 0)
        {
            _rigid.linearVelocity = new Vector2(_rigid.linearVelocity.x, 0);
            _rigid.AddForce(Vector2.up * _jumpPower, ForceMode2D.Impulse);
            _jumpRemain--;
        }
    }

    private void HandleDash()
    {
        if (!_canDash) return;

        if (_dashCoroutine != null)
            StopCoroutine(_dashCoroutine);

        _dashCoroutine = StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        // 대시 중에는 중력을 잠시 끄고 수평 속도를 강제로 넣는다.
        _canDash   = false;
        _isDashing = true;

        // 대쉬 넉백 활성화
        if (_dashKnockback != null) _dashKnockback.IsDashing = true;

        float originalGravity   = _rigid.gravityScale;
        _rigid.gravityScale     = 0f;

        float dashDir = Mathf.Abs(_currentX) > _stopThreshold
            ? Mathf.Sign(_currentX)
            : _facingDir;

        _rigid.linearVelocity = new Vector2(dashDir * _dashPower, 0f);

        yield return new WaitForSeconds(_dashTime);

        // 대쉬 넉백 비활성화
        if (_dashKnockback != null) _dashKnockback.IsDashing = false;

        _rigid.gravityScale = originalGravity;
        _isDashing          = false;

        yield return new WaitForSeconds(_dashCooldown);
        _canDash       = true;
        _dashCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_input != null)
        {
            _input.OnMoveEvent -= HandleMove;
            _input.OnJumpEvent -= HandleJump;
            _input.OnDashEvent -= HandleDash;
        }
    }
}
