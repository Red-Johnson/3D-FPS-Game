# C# 서식 검사

게임 코드는 `.editorconfig`를 기준으로 검사한다. Header가 있는 필드에서는 속성 목록 사이와 마지막 속성 목록부터 선언 시작까지의 공백·줄바꿈 배치를 허용한다. Rider의 저장 시 Reformat Code 설정을 유지할 수 있다.

```csharp
// Header가 없으면 기존 한 줄 표기를 사용한다.
[SerializeField, Min(0f)] private float _jumpPower = 10f;

// Rider가 저장 시 만드는 한 줄 표기도 허용한다.
[Header("점프 설정")] [SerializeField, Min(0f)] private float _jumpPower = 10f;

// 기존 두 줄 표기도 허용한다.
[Header("점프 설정")] [SerializeField, Min(0f)]
private float _jumpPower = 10f;

// 속성 목록을 각각 다른 줄에 두어도 된다.
[Header("점프 설정")]
[SerializeField, Min(0f)]
private float _jumpPower = 10f;
```

`dotnet format`의 기본 공백 검사와 Rider의 속성 배치가 다를 수 있다. 커밋 훅은 `Tools/CodeStyleCheck`로 Header 주변 배치를 보존하면서 나머지 공백을 검사하고, `dotnet format style`과 `dotnet format analyzers`로 기존 이름 및 분석 규칙을 계속 검사한다.

예외 범위는 Header 필드의 속성 배치뿐이다. 속성 인자의 공백, 필드의 들여쓰기, 선언과 `=` 주변 공백, 이름 규칙, C# 구문 오류는 계속 검사한다. Header가 없는 필드와 메서드의 검사는 그대로 유지한다.

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

# 검사/수정의 파일 보존, 이름 규칙 차단 및 실제 훅 통합 테스트
pwsh -NoProfile -File Tools/CodeStyleCheck/Test-Integration.ps1
```

다른 디렉터리에서 실행할 때는 `--root <Unity 프로젝트 경로>`를 전달한다. 검사 도구의 종료 코드는 정상 또는 수정 완료 `0`, 서식 위반 `1`, 인자·구문·프로젝트 로딩 오류 `2`이다. 훅은 실패 시 커밋을 차단하며 자동 수정이나 자동 스테이징을 하지 않는다.

## 적용 범위와 보존 규칙

- 대상은 솔루션에 포함된 `Assets/02.Scripts/**/*.cs`이다. 나머지 공백 규칙은 Roslyn 및 `.editorconfig`를 그대로 적용한다.
- `Header`, `HeaderAttribute`, `UnityEngine.Header`처럼 이름을 명시한 필드 속성을 처리한다. 속성 순서와 묶음, 인자, 문자열, 필드 값은 바꾸지 않는다. 속성 타입의 별칭은 별도 인식하지 않는다.
- Header 필드의 속성 목록 사이와 마지막 속성 목록부터 선언 시작까지의 공백·줄바꿈은 보존한다. 줄바꿈 뒤의 들여쓰기는 `.editorconfig`에 맞춘다. `--fix`도 이 배치를 강제로 바꾸지 않는다.
- 속성 목록 사이 또는 선언 앞에 주석·전처리 지시문이 있으면 해당 경계는 합치지 않는다. 주석과 지시문의 배치는 기본 포맷터 규칙을 따른다.
- 원래 인코딩과 BOM을 유지한다. `.editorconfig`에 `end_of_line`이 없으면 파일의 첫 줄바꿈 형식(LF/CRLF)을 따른다.
- 이름 위반은 `--fix`의 수정 대상이 아니다. Header 추가 위치와 제목, 게임 로직은 직접 결정한다.

기본 `dotnet format` 전체 실행은 Header 배치를 다시 바꿀 수 있다. 공백 수정에는 위의 `--fix` 명령을 사용한다. Rider의 저장 시 리포맷은 계속 사용해도 된다. Rider 자체의 속성 배치를 조정하려면 [공식 속성 배치 설정](https://www.jetbrains.com/help/rider/EditorConfig_CSHARP_LineBreaksPageSchema.html#place-field-attribute-on-the-same-line)을 참고한다. 이 호환 처리를 위해 Rider 설정을 바꿀 필요는 없다.
