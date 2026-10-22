using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;

public class ListExample : MonoBehaviour
{
    // 자료구조(Data Structure)
    // 각종 형태의 데이터들을 효율적으로 관리(CRUDS)하기 위해 저장하는 구조
    // C - Create
    // R - Read
    // U - Update
    // D - Delete
    // S - Search

    // 선형 자료구조: 배열, 리스트, 큐, 스택 등등... 사람 눈으로 봤을 때 나란히 저장되는 자료구조들
    // 비선형 자료구조: 트리, 그래프, 해쉬테이블 등등... 사람 눈으로 봤을 때 나란히 저장되지 않는 자료구조들

    private void Start()
    {
        int[] ages = new int[] { 22, 21, 20, 35 }; // 배열의 선언과 정의/초기화

        // 1. 배열은 처음 선언할 때 지정한 배열의 크기를 변경할 수 없다.
        // -> 배열에 새로운 데이터를 추가하거나 삭제하려면 새로운 배열을 다시 만들어야 한다.
        // -> 데이터 개수 수정 시 비효율적인 과정!
        /*ages[0] = 17;
        ages[1] = 40;
        ages[2] = 27;
        ages[3] = 32;
        ages[4] = 24;*/ // 지정한 배열의 크기(범위)를 벗어났기 때문에 IndexOutOfRange 오류가 발생한다.

        // 이러한 배열의 단점을 보완하기 위해 자료구조는 동적 배열 혹은 연결 리스트를 사용
        // C#은 이것을 "콜렉션" 네임스페이스 안에 List로 구현해놓았다.
        // Collection: 컴퓨터공학의 자료구조를 C#에서 구조화하여 클래스로 구현해 놓은 것들

        // List -> 배열과 거의 똑같지만 필요에 따라 크기가 동적으로 증감하는 유동적인 특성을 갖고 있음.

        List<int> ages2 = new List<int>() { 22, 21, 20, 35 }; // 리스트의 선언과 정의
        ages2[0] = 17;
        ages2[1] = 40;
        ages2[2] = 27;
        ages2[3] = 32;

        ages2.Add(24); // 맨 마지막에 데이터 추가
        ages2.Insert(1, 12); // 데이터를 지정한 인덱스에 삽입(중간으로 삽입 가능)
        ages2.Remove(40); // 제거할 데이터의 값을 매개변수로 전달해 삭제
        ages2.RemoveAt(3); // 제거할 데이터의 인덱스를 매개변수로 전달해 삭제
        // ages2.Find() - 찾는 방법...

        for (int i = 0; i < ages2.Count; i++)
        {
            Debug.Log(ages2[i]); // 인덱스를 활용한 조회
        }

        foreach (int age in ages2)
        {
            Debug.Log(age); // foreach를 활용한 조회
        }
    }
}