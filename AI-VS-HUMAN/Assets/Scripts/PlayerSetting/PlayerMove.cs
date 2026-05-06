using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 이동 / 점프 / 대쉬
/// </summary>
public class PlayerMove : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float _moveSpd       = 5f;
    [SerializeField] private float _jumpPower      = 12f;
    [SerializeField] private float _stopThreshold  = 0.1f;

    [Header("대쉬")]
    [SerializeField] private float _dashPower    = 20f;
    [SerializeField] private float _dashTime     = 0.2f;
    [SerializeField] private float _dashCooldown = 1f;

    [Header("지면 감지")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private int     _maxJumpCount      = 2;
    [SerializeField] private float   _groundCheckRadius = 0.2f;
    [SerializeField] private Vector2 _groundCheckOffset = new Vector2(0f, -0.5f);

    private Rigidbody2D    _rigid;
    private InputController _input;
    private float  _currentX;
    private float  _facingDir  = 1f;
    private int    _jumpRemain;
    private bool   _isGrounded;
    private bool   _canDash    = true;
    private bool   _isDashing;
    private Coroutine _dashCoroutine;

    void Awake()
    {
        _rigid = GetComponent<Rigidbody2D>();
        _input = GetComponent<InputController>();

        _input.OnMoveEvent += HandleMove;
        _input.OnJumpEvent += HandleJump;
        _input.OnDashEvent += HandleDash;
    }

    void FixedUpdate()
    {
        if (_isDashing) return;

        CheckGrounded();

        // 입력이 거의 없으면 X 속도 0으로 즉시 정지
        float velX = Mathf.Abs(_currentX) < _stopThreshold ? 0f : _currentX * _moveSpd;
        _rigid.linearVelocity = new Vector2(velX, _rigid.linearVelocity.y);
    }

    // ── 지면 감지 ──────────────────────────────
    void CheckGrounded()
    {
        Vector2 feet      = (Vector2)transform.position + _groundCheckOffset;
        bool    wasGrounded = _isGrounded;

        // 발 좌/중/우 세 곳을 아래로 레이캐스트 → 벽 오감지 방지
        _isGrounded = Physics2D.Raycast(feet,                         Vector2.down, _groundCheckRadius, _groundLayer)
                   || Physics2D.Raycast(feet + Vector2.left  * 0.15f, Vector2.down, _groundCheckRadius, _groundLayer)
                   || Physics2D.Raycast(feet + Vector2.right * 0.15f, Vector2.down, _groundCheckRadius, _groundLayer);

        // 착지 순간에만 점프 횟수 리셋
        if (!wasGrounded && _isGrounded)
            _jumpRemain = _maxJumpCount;

        // 에디터 시각화
        Debug.DrawRay(feet,                          Vector2.down * _groundCheckRadius, Color.red);
        Debug.DrawRay(feet + Vector2.left  * 0.15f, Vector2.down * _groundCheckRadius, Color.red);
        Debug.DrawRay(feet + Vector2.right * 0.15f, Vector2.down * _groundCheckRadius, Color.red);
    }

    // ── 입력 핸들러 ────────────────────────────
    void HandleMove(float x)
    {
        _currentX = x;
        if (Mathf.Abs(x) > _stopThreshold)
            _facingDir = Mathf.Sign(x);
    }

    void HandleJump()
    {
        if (_isDashing) return;

        bool canJump = _isGrounded || _jumpRemain > 0;
        if (!canJump) return;

        _rigid.linearVelocity = new Vector2(_rigid.linearVelocity.x, 0f);
        _rigid.AddForce(Vector2.up * _jumpPower, ForceMode2D.Impulse);

        _jumpRemain = _isGrounded ? _maxJumpCount - 1 : _jumpRemain - 1;
    }

    void HandleDash()
    {
        if (!_canDash) return;
        if (_dashCoroutine != null) StopCoroutine(_dashCoroutine);
        _dashCoroutine = StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        _canDash   = false;
        _isDashing = true;

        float originalGravity = _rigid.gravityScale;
        _rigid.gravityScale   = 0f;

        // 이동 중이면 그 방향으로, 아니면 바라보는 방향으로 대쉬
        float dir = Mathf.Abs(_currentX) > _stopThreshold ? Mathf.Sign(_currentX) : _facingDir;
        _rigid.linearVelocity = new Vector2(dir * _dashPower, 0f);

        yield return new WaitForSeconds(_dashTime);

        _rigid.gravityScale = originalGravity;
        _isDashing          = false;

        yield return new WaitForSeconds(_dashCooldown);
        _canDash       = true;
        _dashCoroutine = null;
    }

    void OnDestroy()
    {
        if (_input == null) return;
        _input.OnMoveEvent -= HandleMove;
        _input.OnJumpEvent -= HandleJump;
        _input.OnDashEvent -= HandleDash;
    }
}
