using UnityEngine;

public class Player : MonoBehaviour
{
    public float MaxHealth = 100f;
    public float Health;

    private void Awake()
    {
        Health = MaxHealth;
    }

    public bool TryTakeDamage(float damage)
    {
        if (Health <= 0f)
        {
            return false;
        }

        Health = Mathf.Max(0f, Health - damage);

        if (Health <= 0f)
        {
            Debug.Log("플레이어 사망");
        }

        return true;
    }
}