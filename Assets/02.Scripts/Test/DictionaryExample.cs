using UnityEngine;
using System.Collections.Generic;

public class DictionaryExample : MonoBehaviour
{
    // 지금까지 배웠던 배열이나 리스트는 "인덱스"를 이용해 요소에 접근하는 방식을 사용함.
    // 반면 딕셔너리는 요소를 저장할 때 "키"와 "값"을 쌍으로 저장함.
    // HashTable(=Dictionary)은 키와 값이 쌍으로 이루어진 데이터를 관리(CRUDS)할 때 사용하는 자료구조!

    private void Start()
    {
        List<int> ages = new List<int>() { 22, 21, 20, 35 }; // 리스트의 선언 및 초기화

        Dictionary<string, int> students = new Dictionary<string, int>()
            { { "오나현", 22 }, { "박철규", 21 }, { "최재원", 20 } }; // 딕셔너리의 선언 및 초기화

        Debug.Log(students["오나현"]);
        students["박철규"] = 23; // 값 수정
        students.Add("송재욱", 17);
        students.Remove("최재원");
        bool has = students.ContainsKey("송재욱");

        foreach (KeyValuePair<string, int> item in students)
        {
            Debug.Log($"{item.Key}의 나이: {item.Value}");
        }
    }
}