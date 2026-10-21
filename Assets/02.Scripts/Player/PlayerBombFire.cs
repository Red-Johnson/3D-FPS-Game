using UnityEngine;
using UnityEngine.UI;

// 마우스 오른쪽 버튼을 누르면 시선이 바라보는 방향으로 수류탄을 던지고 싶다.

public class PlayerBombFire : MonoBehaviour
{
    private const int MagazineSize = 10;
    private int _currentAmmo = MagazineSize;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Bomb _bombPrefab;
    private BombPool _bombPool;
    [SerializeField] private float _throwPower = 3f;
    [SerializeField] private float _minThrowPower = 3f;
    [SerializeField] private float _maxThrowPower = 15f;
    [SerializeField] private float _chargeSpeed = 5f;

    [Header("UI")] [SerializeField] private Text _ammoText;

    private void Awake()
    {
        _bombPool = BombPool.Create(_bombPrefab);
        UpdateAmmoUI();
    }

    private void OnDestroy()
    {
        if (_bombPool != null) Destroy(_bombPool.gameObject);
    }

    private void Update()
    {
        // 1. 마우스 오른쪽 버튼 입력받기
        if (Input.GetButton("Fire2"))
        {
            ThrowCharge();
            Debug.Log($"_throwPower: {_throwPower}");
        }

        if (Input.GetButtonUp("Fire2"))
        {
            Throw();
        }
    }

    private void Throw()
    {
        if (_currentAmmo <= 0)
        {
            _throwPower = _minThrowPower;
            return;
        }

        // 풀에서 수류탄을 꺼내 시선 방향으로 던진다.
        _bombPool.Throw(_firePoint.position, Camera.main.transform.forward, _throwPower);

        _throwPower = _minThrowPower;
        _currentAmmo--;
        UpdateAmmoUI();
    }

    private void UpdateAmmoUI()
    {
        if (_ammoText != null)
        {
            _ammoText.text = $"{_currentAmmo}/{MagazineSize}";
        }
    }

    private void ThrowCharge()
    {
        _throwPower = Mathf.Min(_throwPower + _chargeSpeed * Time.deltaTime, _maxThrowPower);
    }
}
