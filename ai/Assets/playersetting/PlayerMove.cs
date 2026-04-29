using System.Collections;
using UnityEngine;

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
    private Coroutine _dashCoroutine; // 코루틴 참조 저장

    [Header("Ground Check")]
    [SerializeField] private float _rayDistance = 0.6f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private int _maxJumpCount = 2;

    private Rigidbody2D _rigid;
    private InputController _input;

    private float _currentX;
    private float _facingDir = 1f; // localScale 대신 별도 방향 변수
    private int _jumpRemain;
    private bool _isGrounded;

    private void Awake()
    {
        _input = GetComponent<InputController>();
        _rigid = GetComponent<Rigidbody2D>();

        _input.OnMoveEvent += HandleMove;
        _input.OnJumpEvent += HandleJump;
        _input.OnDashEvent += HandleDash;
    }

    private void FixedUpdate()
    {
        // 바닥 체크를 FixedUpdate로 이동 (물리와 동기화)
        if (!_isDashing)
        {
            CheckGrounded();
        }

        if (_isDashing) return;

        if (Mathf.Abs(_currentX) < _stopThreshold)
        {
            _rigid.linearVelocity = new Vector2(0, _rigid.linearVelocity.y);
        }
        else
        {
            _rigid.linearVelocity = new Vector2(_currentX * _moveSpd, _rigid.linearVelocity.y);
        }
    }

    private void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, Vector2.down, _rayDistance, _groundLayer);

        bool wasGrounded = _isGrounded;
        _isGrounded = hit.collider != null;

        // 착지 순간에만 점프 횟수 리셋 (매 프레임 덮어쓰기 방지)
        if (!wasGrounded && _isGrounded)
        {
            _jumpRemain = _maxJumpCount;
        }

        Debug.DrawRay(transform.position, Vector2.down * _rayDistance, Color.red);
    }

    private void HandleMove(float x)
    {
        _currentX = x;
        // 입력이 있을 때만 바라보는 방향 갱신
        if (Mathf.Abs(x) > _stopThreshold)
        {
            _facingDir = Mathf.Sign(x);
        }
    }

    private void HandleJump()
    {
        if (_isDashing) return;

        // 땅에서 점프
        if (_isGrounded)
        {
            _rigid.linearVelocity = new Vector2(_rigid.linearVelocity.x, 0);
            _rigid.AddForce(Vector2.up * _jumpPower, ForceMode2D.Impulse);
            _jumpRemain = _maxJumpCount - 1; // 1회 소모
        }
        // 공중에서 추가 점프
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

        // 기존 코루틴이 남아있으면 정리 후 재시작
        if (_dashCoroutine != null)
            StopCoroutine(_dashCoroutine);

        _dashCoroutine = StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        _canDash = false;
        _isDashing = true;

        float originalGravity = _rigid.gravityScale;
        _rigid.gravityScale = 0f;

        // localScale 대신 _facingDir 사용
        float dashDir = Mathf.Abs(_currentX) > _stopThreshold
            ? Mathf.Sign(_currentX)
            : _facingDir;

        _rigid.linearVelocity = new Vector2(dashDir * _dashPower, 0f);

        yield return new WaitForSeconds(_dashTime);

        _rigid.gravityScale = originalGravity;
        _isDashing = false;

        yield return new WaitForSeconds(_dashCooldown);
        _canDash = true;
        _dashCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_input != null)
        {
            _input.OnMoveEvent -= HandleMove;
            _input.OnJumpEvent -= HandleJump; // 오타 수정
            _input.OnDashEvent -= HandleDash;
        }
    }
}