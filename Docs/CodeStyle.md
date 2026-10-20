# C# 서식 검사

게임 코드는 `.editorconfig`를 기준으로 검사한다. Header 필드에는 다음 배치 규칙을 추가로 적용한다.

```csharp
// Header가 없으면 기존 한 줄 표기를 사용한다.
[SerializeField, Min(0f)] private float _jumpPower = 10f;

// Header가 있으면 속성 목록 사이에 공백 한 칸을 두고,
// 필드 선언은 같은 들여쓰기의 다음 줄에 둔다.
[Header("점프 설정")] [SerializeField, Min(0f)]
private float _jumpPower = 10f;
```

`dotnet format`의 기본 공백 검사는 위의 두 속성 목록을 다른 줄로 나눈다. 따라서 커밋 훅은 `Tools/CodeStyleCheck`로 공백을 검사하고, `dotnet format style`과 `dotnet format analyzers`로 기존 이름 및 분석 규칙을 계속 검사한다.

## 실행

프로젝트 루트에서 실행한다. .NET 10 SDK가 필요하며 최초 실행 시 고정된 버전의 Roslyn/MSBuild NuGet 패키지를 복원한다. Unity가 생성한 `3D FPS Game.sln`과 프로젝트 파일을 사용하므로 파일이 없거나 새 스크립트가 누락되면 Unity의 External Tools에서 프로젝트 파일을 다시 생성한다.

```shell
# 공백 및 Header 배치 검사: 파일을 수정하지 않는다.
dotnet run --project Tools/CodeStyleCheck --

# 공백 및 Header 배치 수정: 검토 후 직접 스테이징한다.
dotnet run --project Tools/CodeStyleCheck -- --fix

# 이름 및 코드 스타일 검사
dotnet format style "3D FPS Game.sln" --verify-no-changes --severity warn --include Assets/02.Scripts/

# 분석기 검사
dotnet format analyzers "3D FPS Game.sln" --verify-no-changes --severity warn --include Assets/02.Scripts/

# 서식 회귀 테스트
dotnet run --project Tools/CodeStyleCheck -- --self-test
```

다른 디렉터리에서 실행할 때는 `--root <Unity 프로젝트 경로>`를 전달한다. 검사 도구의 종료 코드는 정상 또는 수정 완료 `0`, 서식 위반 `1`, 인자·구문·프로젝트 로딩 오류 `2`이다. 훅은 실패 시 커밋을 차단하며 자동 수정이나 자동 스테이징을 하지 않는다.

## 적용 범위와 보존 규칙

- 대상은 솔루션에 포함된 `Assets/02.Scripts/**/*.cs`이다. 나머지 공백 규칙은 Roslyn 및 `.editorconfig`를 그대로 적용한다.
- `Header`, `HeaderAttribute`, `UnityEngine.Header`처럼 이름을 명시한 필드 속성을 처리한다. 속성 순서와 묶음, 인자, 문자열, 필드 값은 바꾸지 않는다. 속성 타입의 별칭은 별도 인식하지 않는다.
- 속성 목록 사이 또는 선언 앞에 주석·전처리 지시문이 있으면 해당 경계는 합치지 않는다. 주석 보존을 우선하며 나머지 경계에만 배치 규칙을 적용한다.
- 원래 인코딩과 BOM을 유지한다. `.editorconfig`에 `end_of_line`이 없으면 파일의 첫 줄바꿈 형식(LF/CRLF)을 따른다.
- 이름 위반은 `--fix`의 수정 대상이 아니다. Header 추가 위치와 제목, 게임 로직은 직접 결정한다.

기본 `dotnet format` 전체 실행은 Header 배치를 다시 바꿀 수 있다. 공백 수정에는 위의 `--fix` 명령을 사용한다. IDE의 기본 서식 기능 역시 별도 설정 없이는 이 사용자 지정 배치를 지원하지 않으므로, 커밋 전 검사 명령을 기준으로 확인한다.
