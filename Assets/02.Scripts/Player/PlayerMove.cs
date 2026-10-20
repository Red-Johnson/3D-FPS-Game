using UnityEngine;

// 키보드 방향키에 따라 그 방향으로 이동하고 싶다.
public class PlayerMove : MonoBehaviour
{
    private const float Gravity = -20f; // 중력의 크기
    private float _yVelocity = 0f; // 누적될 중력가속도 (매 프레임 더 빨리 떨어짐)


    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _jumpPower = 10f;
    [SerializeField] private CharacterController _characterController;

    private void Update()
    {
        // 1. 키보드 입력을 받는다.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 2. 입력받은 값으로 방향을 구한다.
        Vector3 direction = new Vector3(h, 0, v);
        direction.Normalize(); // 대각선 이동에서도 방향의 길이를 1로 바꾸기 위해
        direction = transform.TransformDirection(direction);
        // 로컬 좌표계: 한 대상을 중심으로 함
        // 월드 좌표계: 게임 내 세계를 중심으로 함

        // 3. 땅에 서 있지 않을 때만 중력을 누적한다.
        if (!_characterController.isGrounded)
        {
            _yVelocity += Gravity * Time.deltaTime;
        }

        // 4. (점프 키를 누르면 && 땅에 있다면) 플레이어의 y축 속도를 _jumpPower로 바꾼다.
        // 키 연결을 하드코딩하지 않고 Jump와 같은 기능으로 명시하고 관리하면 다른 조작방법과의 연동이 편리해짐.
        if (Input.GetButtonDown("Jump") && _characterController.isGrounded)
        {
            _yVelocity = _jumpPower;
        }

        // 5. 속도를 구한다.
        Vector3 velocity = direction * _moveSpeed;
        velocity.y = _yVelocity;

        // 6. 속도(방향과 속력)에 따라 위치를 이동시킨다.
        // transform.position += direction * _moveSpeed * Time.deltaTime;
        _characterController.Move(velocity * Time.deltaTime);
    }
}