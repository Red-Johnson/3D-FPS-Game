using UnityEngine;
using UnityEngine.Pool;


// 무언가에 부딛히면 폭발 이펙트를 남기고 사라지는 폭탄

[RequireComponent(typeof(Rigidbody))]
public class Bomb : MonoBehaviour
{
    [SerializeField] private ParticleSystem _explosionEffectPrefab;
    [SerializeField, Min(0.1f)] private float _maxLifetime = 10f;

    private Rigidbody _rigidbody;
    private IObjectPool<Bomb> _pool;
    private ParticleEffectPool _explosionEffectPool;
    private float _remainingLifetime;
    private bool _isInUse;

    public ParticleSystem ExplosionEffectPrefab => _explosionEffectPrefab;

    public void Initialize(IObjectPool<Bomb> pool, ParticleEffectPool explosionEffectPool)
    {
        _rigidbody = GetComponent<Rigidbody>();
        _pool = pool;
        _explosionEffectPool = explosionEffectPool;
    }

    public void Launch(Vector3 direction, float power)
    {
        _remainingLifetime = _maxLifetime;
        _isInUse = true;
        gameObject.SetActive(true);
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.WakeUp();
        _rigidbody.AddForce(direction.normalized * power, ForceMode.Impulse);
    }

    public void ResetForPool()
    {
        _isInUse = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_isInUse) return;

        // 맵 밖으로 떨어져 충돌하지 않은 수류탄도 풀에 회수한다.
        _remainingLifetime -= Time.deltaTime;
        if (_remainingLifetime <= 0f)
        {
            ReturnToPool();
        }
    }

    // 다른 콜라이더와 부딛힌 순간 실행되는 메서드
    private void OnCollisionEnter(Collision collision)
    {
        if (!_isInUse) return;

        _explosionEffectPool.Play(transform.position, Quaternion.identity);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (!_isInUse) return;

        _isInUse = false;
        _pool.Release(this);
    }
}
