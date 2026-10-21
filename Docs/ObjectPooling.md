# 수류탄과 이펙트 풀링

## 적용 대상

- 수류탄: `Assets/03.Prefabs/Weapon/Bomb.prefab`
- Raycast 피격: `Assets/Workshop/Effects/Stone_BulletImpact.prefab`
- 수류탄 폭발: `Assets/Workshop/Effects/Explosion.prefab`

총은 기존 Raycast 판정을 유지한다. 별도의 총알 GameObject는 만들지 않는다.
수류탄도 기존처럼 충돌 즉시 폭발한다. 충전, 탄약 차감, 재장전과 UI 갱신은 기존 처리를 유지한다.

## 연결과 실행 흐름

`PlayerGunFire.Awake()`는 기존 `_hitEffectPrefab` 참조로 `ParticleEffectPool`을 만든다.
`PlayerBombFire.Awake()`는 기존 `_bombPrefab` 참조로 `BombPool`을 만든다.
`BombPool`은 수류탄 프리팹의 `_explosionEffectPrefab`으로 폭발 이펙트 풀도 만든다.
씬과 프리팹에서 참조를 다시 연결할 필요는 없다.

```text
PlayerGunFire.Shoot
  → 기존 Raycast 명중 판정
  → ParticleEffectPool.Play(명중 위치, 표면 법선 방향)
  → PooledParticleEffect가 모든 자식 파티클의 종료를 확인
  → Release로 반납하고 비활성화

PlayerBombFire.Throw
  → BombPool.Throw
  → Bomb.Launch: 수명·속도·회전 속도 초기화 후 힘 적용
  → 충돌 시 폭발 이펙트 풀에 재생 요청
  → 수류탄 반납
  → 폭발 이펙트는 독립적으로 재생을 마친 뒤 반납
```

풀은 플레이어의 자식으로 두지 않고 월드 공간에 생성한다. 따라서 플레이어가 움직여도
이미 발사된 수류탄과 피격 이펙트가 따라 움직이지 않는다.
플레이어 무기 컴포넌트가 제거될 때 해당 풀도 제거하며, 사용 중인 인스턴스는 풀의 자식으로 함께 정리한다.

## 재사용 상태 처리

- 수류탄은 대여할 때 위치·회전·선속도·각속도·수명·사용 여부를 초기화한다.
- 수류탄의 사용 여부를 검사하여 중복 충돌에 의한 이중 반납을 막는다.
- 충돌하지 않은 수류탄은 기본 10초 후 폭발 없이 회수한다. `Bomb._maxLifetime`으로 조절한다.
- 파티클 인스턴스의 모든 자식에 `stopAction = None`, `playOnAwake = false`를 적용한다.
  원본 프리팹의 자동 삭제 설정은 수정하지 않는다.
- 재생 전 `StopEmittingAndClear`로 이전 파티클을 지운다.
- `IsAlive(true)`로 자식의 연기와 파편까지 끝난 뒤 반납한다.
- 현재 두 이펙트는 반복 재생하지 않는 프리팹이다. 무한 반복하는 이펙트는 자동 반납 조건에 맞지 않는다.

## 용량 정책

| 풀 | 초기 내부 용량 | 최대 보관 개수 |
| --- | ---: | ---: |
| 수류탄 | 10 | 20 |
| 피격 이펙트 | 10 | 50 |
| 폭발 이펙트 | 10 | 50 |

초기 내부 용량은 오브젝트를 미리 생성하는 개수가 아니다. 현재는 필요할 때 생성하고 이후 재사용한다.
사용 가능한 인스턴스가 없으면 추가로 생성하며, 반납 시 보관 한도를 넘은 인스턴스만 제거한다.
따라서 최대 보관 개수는 동시 발사나 탄약의 제한과 관계없다.

## 검증

Unity 6000.3.22f1의 별도 프로젝트 복사본에서 `PoolingSmokeCheck.Run`을 배치 모드로 실행했다.
원본 프로젝트를 열어 둔 에디터에는 테스트 입력을 보내지 않았다.

- 실제 GameScene의 Raycast 발사 처리: 명중 위치·표면 법선·탄약 차감.
- 피격 이펙트 재사용: Instance ID 일치와 6개 동시 재생 및 반납.
- 실제 수류탄 투척 처리: 탄약 차감·충전 초기화·물리 충돌·폭발 재생.
- 수류탄과 폭발 이펙트의 Instance ID 재사용.
- 수류탄 속도·회전 속도 초기화, 충돌하지 않은 수류탄의 시간 초과 회수.
- 탄약이 없을 때 발사 차단 및 충전 초기화.
- 55개 동시 피격 후 최대 50개 보관.
- 사용 중인 인스턴스가 있는 상태에서 소유자 제거 시 풀 정리.

검사 도구는 입력 대신 기존 무기 메서드를 호출하며, Unity의 실제 Update·물리·파티클 재생을 사용한다.
복사본을 처음 여는 과정에서 발생한 Unity 검색 인덱스 초기화 예외만 검사 대상에서 제외한다.
게임 오류와 검증 실패는 실패로 처리한다.
성공 로그는 `POOLING CHECK PASSED: all integration checks completed.`이다.

게임 스크립트 11개에 대한 프로젝트 서식 검사, `dotnet format style`,
`dotnet format analyzers`도 통과했다. 그래픽 출력 없는 검증이므로 외관 및 FPS 개선 수치는 측정하지 않았다.

배치 검사 진입점은 `Assets/Editor/PoolingSmokeCheck.cs`이며, 검사 후 해당 Unity 프로세스를 종료한다.
원본을 다른 에디터에서 열어 둔 경우 별도 복사본에서 실행한다.
