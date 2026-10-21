using UnityEngine;
using UnityEngine.Pool;

public class PooledParticleEffect : MonoBehaviour
{
    private ParticleSystem _particles;
    private IObjectPool<PooledParticleEffect> _pool;
    private bool _isPlaying;

    public void Initialize(ParticleSystem particles, IObjectPool<PooledParticleEffect> pool)
    {
        _particles = particles;
        _pool = pool;

        // 원본 프리팹의 Destroy 설정이 풀의 인스턴스를 삭제하지 않도록 한다.
        foreach (ParticleSystem child in GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = child.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.playOnAwake = false;
        }

        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void Play()
    {
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _isPlaying = true;
        gameObject.SetActive(true);
        _particles.Play(true);
    }

    private void Update()
    {
        // 루트뿐 아니라 자식의 연기와 파편까지 사라진 뒤 반납한다.
        if (!_isPlaying || _particles.IsAlive(true)) return;

        _isPlaying = false;
        _pool.Release(this);
    }
}
