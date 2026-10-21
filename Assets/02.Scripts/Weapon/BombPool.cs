using UnityEngine;
using UnityEngine.Pool;

public class BombPool : MonoBehaviour
{
    private const int DefaultCapacity = 10;
    private const int MaxSize = 20;

    private Bomb _prefab;
    private ObjectPool<Bomb> _pool;
    private ParticleEffectPool _explosionEffectPool;

    public static BombPool Create(Bomb prefab)
    {
        // 플레이어의 이동과 회전이 날아가는 수류탄에 전달되지 않는 월드 루트.
        GameObject poolObject = new GameObject("Bomb Pool");
        BombPool bombPool = poolObject.AddComponent<BombPool>();
        bombPool._prefab = prefab;
        bombPool._explosionEffectPool = ParticleEffectPool.Create(
            prefab.ExplosionEffectPrefab, "Explosion Effect Pool", poolObject.transform);
        bombPool._pool = new ObjectPool<Bomb>(
            bombPool.CreateBomb,
            actionOnGet: null,
            actionOnRelease: bomb => bomb.ResetForPool(),
            actionOnDestroy: bomb =>
            {
                if (bomb != null) Destroy(bomb.gameObject);
            },
            collectionCheck: true,
            defaultCapacity: DefaultCapacity,
            maxSize: MaxSize);

        return bombPool;
    }

    private Bomb CreateBomb()
    {
        Bomb bomb = Instantiate(_prefab, transform);
        bomb.Initialize(_pool, _explosionEffectPool);
        bomb.ResetForPool();
        return bomb;
    }

    public void Throw(Vector3 position, Vector3 direction, float power)
    {
        Bomb bomb = _pool.Get();
        bomb.transform.SetPositionAndRotation(position, Quaternion.identity);
        bomb.Launch(direction, power);
    }

    private void OnDestroy()
    {
        _pool?.Clear();
    }
}
