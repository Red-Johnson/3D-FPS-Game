using UnityEngine;
using UnityEngine.UI;

// 마우스 왼쪽 버튼을 누르면 바라보는 방향으로 총알을 발사하고 싶다.
public class PlayerGunFire : MonoBehaviour
{
    private const int MagazineSize = 50;
    private int _currentAmmo = MagazineSize;
    private bool _isReloading;
    private float _reloadTimer;

    [Header("스탯")] private float _range = 100f;
    private float _damage = 10f;
    private float _fireInterval = 0.1f;
    private float _fireTimer = 0f;
    [SerializeField, Min(0f)] private float _reloadDuration = 1.5f;

    [SerializeField] private Transform _firePoint;
    [SerializeField] private ParticleSystem _hitEffectPrefab;
    [Header("UI")] [SerializeField] private Text _ammoText;

    private void Awake()
    {
        UpdateAmmoUI();
    }

    private void Update()
    {
        // 0. 발사 타이머를 지난 시간만큼 차감.
        _fireTimer -= Time.deltaTime;

        // 재장전 중에는 발사와 추가 재장전 입력을 받지 않는다.
        if (_isReloading)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                _currentAmmo = MagazineSize;
                _isReloading = false;
                UpdateAmmoUI();
            }
            return;
        }

        // R키로 재장전을 시작한다. 예비 탄약과 재장전 횟수에는 제한이 없다.
        if (Input.GetKeyDown(KeyCode.R))
        {
            Reload();
            return;
        }

        // 1. 마우스 왼쪽 버튼 입력받기
        // Input.GetMouseButtonDown(0) -> 0~2 사이의 숫자를 매개변수로 받는다. 0: 좌클릭, 1: 우클릭, 2: 휠

        if (Input.GetButton("Fire1") && _fireTimer <= 0f)
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (_isReloading || _currentAmmo <= 0) return;

        _fireTimer = _fireInterval;

        // 명중 여부와 관계없이 발사할 때마다 한 발을 소모한다.
        _currentAmmo--;
        UpdateAmmoUI();

        // Ray: 레이저(시작 위치, 날아갈 방향)
        // Physics.RayCast: 레이저를 발사
        // RayCastHit: 레이저가 물체와 부딛히면 그 정보를 저장하는 구조체

        // 2. Ray(광선)을 생성하고 발사될 위치와 진행 방향 설정하기
        Transform cameraTransform = Camera.main.transform;
        Ray ray = new Ray(_firePoint.position, cameraTransform.forward);

        // 3. Ray가 부딛힌 대상의 정보를 저장할 변수 생성하기
        RaycastHit hitInfo = new RaycastHit();

        // 4. Ray를 발사하고, 만일 부딛힌 물체가 있다면 이펙트 표시하기
        bool isHit = Physics.Raycast(ray, out hitInfo, _range);
        if (isHit)
        {
            // Debug.Log($"{hitInfo.collider.name}이 총알을 맞았습니다. 데미지: {_damage}");

            ParticleSystem effect = Instantiate(_hitEffectPrefab);
            effect.transform.position = hitInfo.point; // 맞은 자리
            effect.transform.forward = hitInfo.normal; // 수직 위치로 회전
            effect.Play();
        }
    }

    private void Reload()
    {
        if (_isReloading || _currentAmmo == MagazineSize) return;

        _isReloading = true;
        _reloadTimer = _reloadDuration;
    }

    private void UpdateAmmoUI()
    {
        if (_ammoText != null)
        {
            _ammoText.text = $"{_currentAmmo}/{MagazineSize}";
        }
    }
}
