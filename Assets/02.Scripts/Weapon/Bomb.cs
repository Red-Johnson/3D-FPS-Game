using UnityEngine;


// 무언가에 부딛히면 폭발 이펙트를 남기고 사라지는 폭탄

public class Bomb : MonoBehaviour
{
    [SerializeField] private ParticleSystem _explosionEffectPrefab;

    // 다른 콜라이더와 부딛힌 순간 실행되는 메서드
    private void OnCollisionEnter(Collision collision)
    {
        ParticleSystem effect = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
        effect.Play();

        // Todo. 오브젝트 풀링 적용하기
        Destroy(gameObject);
    }
}