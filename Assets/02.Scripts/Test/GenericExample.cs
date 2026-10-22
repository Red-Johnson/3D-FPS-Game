using UnityEngine;

// 제네릭(Generic)이란 클래스나 메서드에서 사용할 데이터 타입을 미리 특정하지 않고, 사용할 때 외부에서 지정할 수 있도록 일반화하는 기법

public class Cup<T>
{
    public T Drink;

    public Cup(T drink)
    {
        Drink = drink;
    }
}


public class GenericExample : MonoBehaviour
{
    private void Start()
    {
        Cup<int> cup1 = new Cup<int>(17);
        Cup<float> cup2 = new Cup<float>(0.2f);
        Cup<string> cup3 = new Cup<string>("콜라");
        Cup<bool> cup4 = new Cup<bool>(true);
    }
}