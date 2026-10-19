using UnityEngine;


// 역할: 마우스를 움직이면 카메라를 그 방향으로 회전시키고 싶다.
public class CameraRotate : MonoBehaviour
{
    [SerializeField] private float _rotationSpeed = 200f;

    private float _accumulationX = 0f; // 좌우 회전 각도
    private float _accumulationY = 0f; // 위아래 회전 각도

    private void Update()
    {
        // 1. 마우스 입력을 받는다.
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        // Debug.Log($"mouseX: {mouseX}, mouseY: {mouseY}");

        // 2. 마우스 입력에 따라 회전할 방향을 구한다.
        // Vector3 direction = new Vector3(-mouseY, mouseX, 0);

        // 2. 마우스 입력만큼 회전값을 누적한다.
        _accumulationX += mouseX * _rotationSpeed * Time.deltaTime;
        _accumulationY += -mouseY * _rotationSpeed * Time.deltaTime;

        // 3. 누적하기 전에 내 변수에서 먼저 -90 ~ 90도로 제한
        _accumulationY = Mathf.Clamp(_accumulationY, -90, 90);

        // 4. 누적한 회전값을 카메라에 적용한다.
        transform.eulerAngles = new Vector3(_accumulationY, _accumulationX, 0f);

        /*// 3. 그 방향으로 회전시킨다.
        // 새로운 각도 = 현재 각도 + 회전 속도 + 시간
        transform.eulerAngles += _rotationSpeed * Time.deltaTime * direction;

        // 4. 위 아래 회전값을 -90 ~ 90도로 제한한다.
        Vector3 rotation = transform.eulerAngles;
        // 정면: 0도
        // 하늘을 살짝 1도만큼 쳐다보게 되면: -1 -> 359 -> 90
        // 유니티는 0~360 각도 체계를 사용하기 때문에 음수 각도는 자동으로 변환되어 반환됨!
        rotation.x = Mathf.Clamp(rotation.x, -90, 90);
        transform.eulerAngles = rotation;
        */
    }
}