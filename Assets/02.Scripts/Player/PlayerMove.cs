using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    // 중력은 수직 속도를 변화시키는 가속도다.
    private const float Gravity = -20f;

    // 바닥에 있을 때 작은 하향 속도를 주어 접지를 유지한다.
    private const float GroundedVelocity = -2f;

    [SerializeField, Min(0f)] private float _walkSpeed = 7f;

    [SerializeField, Min(0f)] private float _sprintSpeed = 12f;


    // 점프 높이가 아니라 점프 시작 시의 위쪽 속도다.
    [SerializeField, Min(0f)] private float _jumpPower = 10f;

    // 2이면 지상 점프 1회 + 공중 점프 1회가 가능하다.
    [SerializeField, Min(1)] private int _maxJumpCount = 2;

    // 현재까지 사용한 점프 횟수와 현재 수직 속도.
    private int _jumpCount;
    private float _yVelocity;

    [SerializeField, Min(0f)] private float _maxStamina = 100f;

    // 질주는 초당 소비하고, 점프는 성공할 때마다 소비한다.
    [SerializeField, Min(0f)] private float _sprintDrainPerSecond = 25f;
    [SerializeField, Min(0f)] private float _jumpStaminaCost = 20f;

    // 0으로 설정하면 자연 회복을 사용하지 않는다.
    [SerializeField, Min(0f)] private float _staminaRecoveryPerSecond = 15f;

    // 실행 중 Inspector에서 현재 스태미나를 확인할 수 있다.
    [SerializeField] private float _currentStamina;

    [SerializeField] private CharacterController _characterController;

    private void Awake()
    {
        // Inspector에 연결하지 않았다면 같은 오브젝트에서 가져온다.
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        // 게임 시작 시 스태미나와 이동 상태를 초기화한다.
        _currentStamina = _maxStamina;
        _jumpCount = 0;
        _yVelocity = 0f;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // 일시정지 상태에서는 입력·소비·이동을 처리하지 않는다.
        if (deltaTime <= 0f)
        {
            return;
        }

        // ─────────────────────────────
        // 1. 키보드 입력과 이동 방향 계산
        // ─────────────────────────────

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v);

        // 대각선 입력에서도 이동 속도가 빨라지지 않게 한다.
        direction.Normalize();

        // 플레이어 기준 앞·오른쪽 방향을
        // CharacterController.Move에 사용할 월드 방향으로 변환한다.
        direction = transform.TransformDirection(direction);

        bool hasMoveInput = direction.sqrMagnitude > 0.001f;
        bool sprintHeld = Input.GetKey(KeyCode.LeftShift);
        bool jumpPressed = Input.GetButtonDown("Jump");

        // 점프한 프레임에는 스태미나를 회복하지 않기 위한 변수다.
        bool jumpedThisFrame = false;

        // ─────────────────────────────
        // 2. 접지 상태와 점프 횟수 정리
        // ─────────────────────────────

        // isGrounded는 마지막 Move의 접지 결과다.
        // 상승 중에는 접지 정보가 남아 있어도 착지로 처리하지 않는다.
        bool grounded =
            _characterController.isGrounded && _yVelocity <= 0f;

        if (grounded)
        {
            // 지상에서는 점프 기회를 다시 사용할 수 있다.
            _jumpCount = 0;
            _yVelocity = GroundedVelocity;
        }
        else if (_jumpCount == 0)
        {
            // 점프하지 않고 낭떠러지에서 떨어진 경우다.
            // 지상 점프 기회를 사용한 것으로 처리하여
            // 공중 추가 점프만 남긴다.
            _jumpCount = 1;
        }

        // ─────────────────────────────
        // 3. 점프와 스태미나 소비
        // ─────────────────────────────

        // 점프 횟수가 남아 있고 비용을 지불할 수 있어야 점프한다.
        // 점프가 실패하면 스태미나와 점프 횟수를 소비하지 않는다.
        if (jumpPressed &&
            _jumpCount < _maxJumpCount &&
            TryUseStamina(_jumpStaminaCost))
        {
            // +=로 더하지 않고 =로 대입한다.
            // 하강 중이어도 낙하 속도를 지우고 다시 위로 뛰게 한다.
            _yVelocity = _jumpPower;

            _jumpCount++;
            jumpedThisFrame = true;
        }

        // ─────────────────────────────
        // 4. 질주와 스태미나 소비
        // ─────────────────────────────

        float currentSpeed = _walkSpeed;

        // 초당 소비량을 이번 프레임의 소비량으로 바꾼다.
        float sprintCost = _sprintDrainPerSecond * deltaTime;

        // Shift를 누르면서 이동 입력이 있고,
        // 이번 프레임 비용을 지불할 수 있을 때만 질주한다.
        // Shift만 누르고 가만히 있으면 소비하지 않는다.
        if (sprintHeld &&
            hasMoveInput &&
            TryUseStamina(sprintCost))
        {
            currentSpeed = _sprintSpeed;
        }

        // ─────────────────────────────
        // 5. 중력과 최종 이동 처리
        // ─────────────────────────────

        // CharacterController.Move는 중력을 자동 적용하지 않는다.
        // 스태미나가 없어도 중력은 계속 적용해야 한다.
        _yVelocity += Gravity * deltaTime;

        // 수평 속도와 수직 속도를 하나의 벡터로 합친다.
        Vector3 velocity = direction * currentSpeed;
        velocity.y = _yVelocity;

        // 속도 × 시간 = 이번 프레임의 이동 거리.
        // 수평·수직 이동을 합쳐 Move를 한 번 호출한다.
        CollisionFlags flags =
            _characterController.Move(velocity * deltaTime);

        // 상승 중 천장에 닿으면 남아 있는 상승 속도를 제거한다.
        if ((flags & CollisionFlags.Above) != 0 &&
            _yVelocity > 0f)
        {
            _yVelocity = 0f;
        }

        // 이번 이동에서 착지했다면 점프 횟수를 초기화한다.
        if ((flags & CollisionFlags.Below) != 0 &&
            _yVelocity < 0f)
        {
            _yVelocity = GroundedVelocity;
            _jumpCount = 0;
        }

        // ─────────────────────────────
        // 6. 스태미나 회복
        // ─────────────────────────────

        // Shift를 놓았고 이번 프레임에 점프하지 않았다면 회복한다.
        // Shift를 계속 누른 상태에서는 회복하지 않는다.
        if (!sprintHeld && !jumpedThisFrame)
        {
            _currentStamina = Mathf.Min(
                _maxStamina,
                _currentStamina + _staminaRecoveryPerSecond * deltaTime);
        }
    }

    // 필요한 스태미나를 소비할 수 있는지 확인한다.
    // 성공: 차감하고 true 반환.
    // 실패: 값을 변경하지 않고 false 반환.
    private bool TryUseStamina(float amount)
    {
        // 스태미나가 없거나 비용이 부족하면 실행할 수 없다.
        if (_currentStamina <= 0f || _currentStamina < amount)
        {
            return false;
        }

        _currentStamina = Mathf.Max(0f, _currentStamina - amount);
        return true;
    }
}