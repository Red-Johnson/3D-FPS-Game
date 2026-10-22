using UnityEngine;

// FSM(유한 상태 머신): 유한한 종류의 상태를 정의하고, 상태마다 행동을 다르게 함으로써 AI를 구현하는 기초적인 방법
// - 상태: 객체가 가질 수 있는 어떤 조건이나 상황
// - 전이: 조건에 따라 현재 상태에서 다른 상태로 넘어가는 것
// - 그리고 위 방식에 따라 규칙을 세워 둔 것을 "머신"이라 함.

// 좀비가 가질 수 있는 상태
public enum ZombieState
{
    Idle, // 대기
    Trace, // 추격
    Comeback, // 복귀
    Attack, // 공격
    Hit, // 피격
    Death, // 죽음
}

public class Zombie : MonoBehaviour
{
    [Header("스탯")] [SerializeField] private float _detectDistance = 0;
    [SerializeField] private float _moveSpeed = 5f;

    private CharacterController _controller;
    private GameObject _player;
    private ZombieState _state = ZombieState.Idle;

    private void Start()
    {
        _controller = GetComponent<CharacterController>();

        _player = GameObject.FindGameObjectWithTag("Player");
    }

    private void Update()
    {
        switch (_state)
        {
            case ZombieState.Idle:
                // Todo. 제자리에서 플레이어의 접근을 대기
                Idle();
                break;

            case ZombieState.Trace:
                // Todo. 플레이어를 추격
                Trace();
                break;

            case ZombieState.Comeback:
                Comeback();
                break;

            case ZombieState.Attack:
                Attack();
                break;

            case ZombieState.Hit:
                Hit();
                break;
            case ZombieState.Death:
                Death();

                break;
        }
    }

    private void Idle()
    {
        // Todo. Idle 애니메이션을 실행

        // 대기하다가 플레이어와 일정 거리 내로 가까워지면 Trace()로 전이
        float distance = Vector3.Distance(_player.transform.position, transform.position);
        if (distance <= _detectDistance)
        {
            Debug.Log($"{_state} -> Trace");
            _state = ZombieState.Trace;
        }
    }

    private void Trace()
    {
        // Todo. Run 애니메이션 실행

        Vector3 direction = _player.transform.position - transform.position;
        direction.y = 0f;
        direction.Normalize();

        transform.forward = direction;
        _controller.SimpleMove(direction * _moveSpeed);
    }

    private void Comeback()
    {
    }

    private void Attack()
    {
    }

    private void Hit()
    {
    }

    private void Death()
    {
    }
}