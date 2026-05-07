// 플레이어의 좌우 이동, 점프, 대시, 지면 감지, 애니메이터 상태 전환을 담당하는 스크립트입니다.
// Ground와 Platform 레이어를 아래 방향으로 검사해서 일반 바닥과 일방향 플랫폼 위에서 점프를 충전합니다.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(InputController))]
[RequireComponent(typeof(Animator))]
public class PlayerMove : MonoBehaviour
{
    private const string IdleRightState = "IdleRight";
    private const string WalkRightState = "WalkRight";
    private const string JumpRightState = "JumpRight";
    private const string IdleLeftState = "IdleLeft";
    private const string WalkLeftState = "WalkLeft";
    private const string JumpLeftState = "JumpLeft";

    [Header("이동")]
    [SerializeField] private float _moveSpd = 5f;
    [SerializeField] private float _jumpPower = 12f;
    [SerializeField] private float _stopThreshold = 0.1f;

    [Header("대시")]
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
    [SerializeField] private float _groundCheckRadius = 0.2f; // 발밑을 검사하는 거리입니다.
    [SerializeField] private Vector2 _groundCheckOffset = new Vector2(0f, -0.5f); // 플레이어 중심에서 발밑까지의 오프셋입니다.

    private Rigidbody2D _rigid;
    private Collider2D _collider;
    private InputController _input;
    private Animator _animator;
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[6];

    private float _currentX;
    private float _facingDir = 1f;
    private int _jumpRemain;
    private bool _isGrounded;
    private string _currentAnimationState;
    private float _defaultGravityScale;

    private void Awake()
    {
        _input = GetComponent<InputController>();
        _rigid = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _animator = GetComponent<Animator>();
        _defaultGravityScale = _rigid != null ? _rigid.gravityScale : 1f;

        if (_input == null || _rigid == null || _collider == null || _animator == null)
        {
            Debug.LogError("PlayerMove needs Rigidbody2D, Collider2D, InputController, and Animator on the same GameObject.", this);
            enabled = false;
            return;
        }

        _input.OnMoveEvent += HandleMove;
        _input.OnJumpEvent += HandleJump;
        _input.OnDashEvent += HandleDash;
    }

    public void ResetForRespawn()
    {
        // 사망 중 남아 있을 수 있는 입력, 대시, 속도를 초기화해 부활 직후 움직임이 튀지 않게 합니다.
        if (_dashCoroutine != null)
        {
            StopCoroutine(_dashCoroutine);
            _dashCoroutine = null;
        }

        _currentX = 0f;
        _canDash = true;
        _isDashing = false;
        _jumpRemain = _maxJumpCount;
        _isGrounded = false;

        if (_rigid != null)
        {
            _rigid.gravityScale = _defaultGravityScale;
            _rigid.linearVelocity = Vector2.zero;
            _rigid.angularVelocity = 0f;
        }

        UpdateAnimationState();
    }

    private void FixedUpdate()
    {
        if (_isDashing)
        {
            UpdateAnimationState();
            return;
        }

        CheckGrounded();

        // 입력이 거의 없으면 X 속도를 바로 0으로 만들어 미끄러짐을 줄입니다.
        float velX = Mathf.Abs(_currentX) < _stopThreshold ? 0f : _currentX * _moveSpd;
        _rigid.linearVelocity = new Vector2(velX, _rigid.linearVelocity.y);

        UpdateAnimationState();
    }

    private void CheckGrounded()
    {
        // 발 기준 세 지점을 아래로 검사해서 모서리 위에서도 안정적으로 착지 판정을 합니다.
        Vector2 feetPos = (Vector2)transform.position + _groundCheckOffset;

        bool wasGrounded = _isGrounded;
        _isGrounded = IsGroundBelow(feetPos);

        // 공중에서 바닥에 처음 닿는 순간에만 점프 횟수를 회복합니다.
        if (!wasGrounded && _isGrounded)
            _jumpRemain = _maxJumpCount;

        Debug.DrawRay(feetPos, Vector2.down * _groundCheckRadius, Color.red);
        Debug.DrawRay(feetPos + Vector2.left * 0.15f, Vector2.down * _groundCheckRadius, Color.red);
        Debug.DrawRay(feetPos + Vector2.right * 0.15f, Vector2.down * _groundCheckRadius, Color.red);
    }

    private bool IsGroundBelow(Vector2 feetPos)
    {
        // 상승 중에는 플랫폼 옆면이나 아래쪽 접촉으로 점프가 충전되지 않게 합니다.
        if (_rigid.linearVelocity.y > 0.1f)
            return false;

        float halfWidth = Mathf.Max(0.05f, _collider.bounds.extents.x * 0.8f);
        float playerBottomY = _collider.bounds.min.y;

        return HasGroundHit(feetPos, playerBottomY)
            || HasGroundHit(feetPos + Vector2.left * halfWidth, playerBottomY)
            || HasGroundHit(feetPos + Vector2.right * halfWidth, playerBottomY);
    }

    private bool HasGroundHit(Vector2 origin, float playerBottomY)
    {
        // Ground와 Platform을 함께 검사하되, Platform은 위에서 밟았을 때만 착지로 인정합니다.
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

        bool canJump = _isGrounded || _jumpRemain > 0;
        if (!canJump) return;

        _rigid.linearVelocity = new Vector2(_rigid.linearVelocity.x, 0f);
        _rigid.AddForce(Vector2.up * _jumpPower, ForceMode2D.Impulse);

        _jumpRemain = _isGrounded ? _maxJumpCount - 1 : _jumpRemain - 1;
        _isGrounded = false;
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        if (_animator == null) return;

        bool facingRight = _facingDir >= 0f;
        bool isMoving = Mathf.Abs(_currentX) >= _stopThreshold;

        string nextState;
        if (!_isGrounded)
            nextState = facingRight ? JumpRightState : JumpLeftState;
        else if (isMoving)
            nextState = facingRight ? WalkRightState : WalkLeftState;
        else
            nextState = facingRight ? IdleRightState : IdleLeftState;

        if (_currentAnimationState == nextState)
            return;

        _animator.Play(nextState);
        _currentAnimationState = nextState;
    }

    private void HandleDash()
    {
        if (!_canDash) return;
        if (_dashCoroutine != null) StopCoroutine(_dashCoroutine);
        _dashCoroutine = StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        // 대시 중에는 중력을 잠깐 끄고 바라보는 방향으로 수평 속도를 강제합니다.
        _canDash = false;
        _isDashing = true;

        float originalGravity = _rigid.gravityScale;
        _rigid.gravityScale = 0f;

        float dir = Mathf.Abs(_currentX) > _stopThreshold ? Mathf.Sign(_currentX) : _facingDir;
        _rigid.linearVelocity = new Vector2(dir * _dashPower, 0f);

        yield return new WaitForSeconds(_dashTime);

        _rigid.gravityScale = originalGravity;
        _isDashing = false;

        yield return new WaitForSeconds(_dashCooldown);
        _canDash = true;
        _dashCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_input == null) return;
        _input.OnMoveEvent -= HandleMove;
        _input.OnJumpEvent -= HandleJump;
        _input.OnDashEvent -= HandleDash;
    }
}
