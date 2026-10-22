using System.Collections;
using UnityEngine;

// FSM(유한 상태 머신) : 유한개의 상태가 있고, 상태마다 행동을 다르게 함으로써 AI를 구현하는 유명한 방법
// - "상태": 객체가 가질 수 있는 어떤 조건이나 상황
// - "전이": 조건에 따라 현재 상태에서 다른 상태로 넘어가는 것
// - 그리고 위 방식에 따라 규칙을 세워둔 것을 "머신"


// 좀비가 가질 수 있는 상태
public enum ZombieState
{
    Idle, // 대기: 제자리에서 플레이어를 기다린다.
    Trace, // 추격: 플레이어를 쫓아간다.
    Comeback, // 복귀: 처음 자리로 돌아간다.
    Attack, // 공격: 일정 시간마다 플레이어를 때린다.
    Hit, // 피격: 공격(총알, 폭탄)을 받아 잠깐 멈칫한다.
    Death, // 죽음: 쓰러지고 잠시 뒤 사라진다.
    Roaming, // 배회: 주변의 무작위 목적지로 걸어간다. 기존 상태의 번호는 유지한다.
}

[RequireComponent(typeof(CharacterController))]
public class Zombie : MonoBehaviour
{
    private const int RoamTargetAttempts = 12;

    [Header("스탯")] [SerializeField] private float _detectDistance = 8; // 플레이어를 발견하는 거리
    [SerializeField] private float _combackDistance = 15; // 처음 자리에서 이만큼 멀어지면 돌아가는 거리
    [SerializeField] private float _attackRange = 1.5f; // 공격 범위
    [SerializeField] private float _attackInterval = 1.2f; // 공격 간격
    [SerializeField] private float _moveSpeed = 5; // 이동속도
    [SerializeField] private float _health = 30; // 체력

    [Header("배회")] [SerializeField, Min(0f)] private float _roamStepDistance = 2f; // 현재 위치에서 목적지를 고르는 반경
    [SerializeField, Min(0f)] private float _roamRadius = 4f; // 처음 위치를 중심으로 한 배회 영역의 반경
    [SerializeField, Min(0f)] private float _roamMoveSpeed = 2f;
    [SerializeField] private Vector2 _idleWaitRange = new Vector2(1f, 3f); // 대기 시간의 최솟값과 최댓값
    [SerializeField, Min(0.01f)] private float _roamArrivalDistance = 0.25f;
    [SerializeField, Min(0.1f)] private float _roamTimeout = 3f; // 벽에 막혀도 이 시간이 지나면 다시 기다린다.

    private float _idleTimer;
    private float _roamTimer;
    private Vector3 _roamTarget; // 복귀 기준점인 _startPosition과 별도로 관리한다.

    private float _attackTimer = 0f; // 지난 공격 뒤로 흐른 시간
    private CharacterController _controller;
    private Vector3 _startPosition; // 처음 자리
    private GameObject _player;


    private ZombieState _state = ZombieState.Idle;

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _startPosition = transform.position;

        // 게임을 시작하면 플레이어를 찾는다.
        _player = GameObject.FindGameObjectWithTag("Player");
        ChangeState(ZombieState.Idle);
    }


    private void Update()
    {
        switch (_state)
        {
            case ZombieState.Idle:
                Idle();
                break;

            case ZombieState.Roaming:
                Roaming();
                break;

            case ZombieState.Trace:
                Trace();
                break;

            case ZombieState.Comeback:
                Comeback();
                break;

            case ZombieState.Attack:
                Attack();
                break;
        }
    }

    // 상태 전환 시 배회 목적지와 타이머를 정리한다.
    private void ChangeState(ZombieState nextState)
    {
        if (_state != nextState)
        {
            Debug.Log($"{_state} -> {nextState}");
        }

        _state = nextState;

        if (nextState != ZombieState.Roaming)
        {
            _roamTarget = transform.position;
            _roamTimer = 0f;
        }

        switch (nextState)
        {
            case ZombieState.Idle:
                float minWait = Mathf.Max(0.1f, _idleWaitRange.x);
                float maxWait = Mathf.Max(minWait, _idleWaitRange.y);
                _idleTimer = Random.Range(minWait, maxWait);
                break;

            case ZombieState.Roaming:
                _roamTimer = Mathf.Max(0.1f, _roamTimeout);
                break;

            case ZombieState.Attack:
                _attackTimer = 0f;
                break;
        }
    }

    // 대미지를 받는다.
    public void TakeDamage(float damage)
    {
        if (_state == ZombieState.Death)
        {
            return;
        }

        _health -= damage;

        StopAllCoroutines();

        if (_health > 0f)
        {
            // 잠깐 멈칫 하고 싶다!
            ChangeState(ZombieState.Hit);
            StartCoroutine(Hit_Coroutine());
        }
        else
        {
            ChangeState(ZombieState.Death);
            StartCoroutine(Death_Coroutine());
        }
    }

    private IEnumerator Hit_Coroutine()
    {
        yield return new WaitForSeconds(0.3f); // 잠깐 멈칫한다!

        // 맞았으니 쫓아온다.
        ChangeState(ZombieState.Trace);
    }

    private IEnumerator Death_Coroutine()
    {
        _controller.enabled = false;
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    // 대기: 플레이어를 감지하거나, 잠시 기다렸다가 배회를 시작한다.
    private void Idle()
    {
        // Todo. Idle 애니메이션 실행

        // 플레이어 가까워지면 Trace 상태로 전이한다.
        if (IsPlayerDetected())
        {
            ChangeState(ZombieState.Trace);
            return;
        }

        _idleTimer -= Time.deltaTime;
        if (_idleTimer > 0f) return;

        if (TryGetRoamTarget(out Vector3 target))
        {
            _roamTarget = target;
            ChangeState(ZombieState.Roaming);
            return;
        }

        // 후보가 없으면 다음 프레임마다 재시도하지 않고 잠시 기다린다.
        ChangeState(ZombieState.Idle);
    }

    private bool IsPlayerDetected()
    {
        return _player != null &&
               Vector3.Distance(transform.position, _player.transform.position) <= _detectDistance;
    }

    private bool TryGetRoamTarget(out Vector3 target)
    {
        target = transform.position;

        // Inspector에서 배회 영역을 크게 잡아도 복귀 거리 안에 여유를 둔다.
        float radius = Mathf.Max(0f, Mathf.Min(_roamRadius, _combackDistance - 1f));
        float stepDistance = Mathf.Max(0f, _roamStepDistance);
        float arrivalDistance = Mathf.Max(0.01f, _roamArrivalDistance);
        if (radius <= 0f || stepDistance <= arrivalDistance) return false;

        for (int i = 0; i < RoamTargetAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * stepDistance;
            if (offset.sqrMagnitude <= arrivalDistance * arrivalDistance) continue;

            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            Vector3 fromStart = candidate - _startPosition;
            fromStart.y = 0f;
            if (fromStart.sqrMagnitude > radius * radius) continue;

            target = candidate;
            return true;
        }

        return false;
    }

    // 배회: 이동보다 플레이어 감지를 먼저 처리한다.
    private void Roaming()
    {
        if (IsPlayerDetected())
        {
            ChangeState(ZombieState.Trace);
            return;
        }

        Vector3 direction = _roamTarget - transform.position;
        direction.y = 0f;
        float distance = direction.magnitude;
        _roamTimer -= Time.deltaTime;

        if (distance <= Mathf.Max(0.01f, _roamArrivalDistance) || _roamTimer <= 0f)
        {
            ChangeState(ZombieState.Idle);
            return;
        }

        // 프레임이 길어져도 목적지를 지나쳐 배회 영역 밖으로 나가지 않는다.
        float speed = Mathf.Min(Mathf.Max(0f, _roamMoveSpeed), distance / Mathf.Max(Time.deltaTime, 0.0001f));
        MoveTo(_roamTarget, speed);
    }

    // 추격: 플레이어를 쫓아간다.
    private void Trace()
    {
        // Todo. Run 애니메이션 실행


        // 처음 자리에서 너무 멀어지면 돌아간다.
        if (Vector3.Distance(transform.position, _startPosition) > _combackDistance)
        {
            ChangeState(ZombieState.Comeback);
            return;
        }

        // 플레이어가 공격 거리 안이면 공격한다.
        if (Vector3.Distance(transform.position, _player.transform.position) <= _attackRange)
        {
            ChangeState(ZombieState.Attack);
            return;
        }


        MoveTo(_player.transform.position, _moveSpeed);
    }

    // 복귀: 처음 자리로 돌아간다.
    private void Comeback()
    {
        // 처음 자리에 도착하면 Idle
        if (Vector3.Distance(transform.position, _startPosition) < 1f)
        {
            ChangeState(ZombieState.Idle);
            return;
        }

        // 처음 자리로 돌아간다
        MoveTo(_startPosition, _moveSpeed);
    }

    // 공격: 일정 시간마다 플레이어를 때린다.
    private void Attack()
    {
        // 플레이어가 공격 사정 거리 밖으로 나가면 쫓아!
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (distance > _attackRange)
        {
            ChangeState(ZombieState.Trace);
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _attackInterval)
        {
            // Todo. 공격 애니메이션 실행
            // Todo. 플레이어를 때려!
            Debug.Log("플레이어를 때렸습니다.");
            _attackTimer = 0f;
        }
    }

    private void MoveTo(Vector3 target, float speed)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        direction.Normalize();

        if (direction.sqrMagnitude > 0f)
        {
            transform.forward = direction;
        }

        _controller.SimpleMove(direction * speed);
    }
}
