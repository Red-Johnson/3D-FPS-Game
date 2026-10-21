using UnityEngine;
using UnityEngine.Pool;

// 피격과 폭발처럼 재생이 끝나면 반납하는 파티클 풀.
public class ParticleEffectPool : MonoBehaviour
{
    private const int DefaultCapacity = 10;
    private const int MaxSize = 50;

    private ParticleSystem _prefab;
    private ObjectPool<PooledParticleEffect> _pool;

    public static ParticleEffectPool Create(ParticleSystem prefab, string poolName, Transform parent = null)
    {
        GameObject poolObject = new GameObject(poolName);
        poolObject.transform.SetParent(parent, false);

        ParticleEffectPool effectPool = poolObject.AddComponent<ParticleEffectPool>();
        effectPool._prefab = prefab;
        effectPool._pool = new ObjectPool<PooledParticleEffect>(
            effectPool.CreateEffect,
            actionOnGet: null,
            actionOnRelease: effect => effect.gameObject.SetActive(false),
            actionOnDestroy: effect =>
            {
                if (effect != null) Destroy(effect.gameObject);
            },
            collectionCheck: true,
            defaultCapacity: DefaultCapacity,
            maxSize: MaxSize);

        return effectPool;
    }

    private PooledParticleEffect CreateEffect()
    {
        ParticleSystem particles = Instantiate(_prefab, transform);
        PooledParticleEffect effect = particles.gameObject.AddComponent<PooledParticleEffect>();
        effect.Initialize(particles, _pool);
        particles.gameObject.SetActive(false);
        return effect;
    }

    public void Play(Vector3 position, Quaternion rotation)
    {
        PooledParticleEffect effect = _pool.Get();
        effect.transform.SetPositionAndRotation(position, rotation);
        effect.Play();
    }

    private void OnDestroy()
    {
        // 사용 중인 인스턴스는 이 풀의 자식이므로 함께 제거된다.
        _pool?.Clear();
    }
}
