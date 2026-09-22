# Bongo Cat 도우미

**내 상자는 자동으로, 다른 사람의 상자는 원할 때만.**

Windows Steam판 Bongo Cat의 일반 상자·감정표현 상자를 자동으로 여는 비공식 도우미입니다. 실행 파일을 더블클릭하고 화면의 안내를 따라 설정하세요. 명령어 입력, 환경 변수 설정, 별도 모드 로더 설치가 필요하지 않습니다.

## 시작하기

1. [최신 버전 다운로드](https://github.com/misosiruda/bongo-cat-auto-chest/releases/latest)에서 **`BongoAutoChest.exe`**를 받으세요.
2. Steam에서 Bongo Cat 설치와 업데이트를 마치고, 게임을 종료하세요.
3. 실행 파일을 더블클릭하세요. 마법사가 게임을 찾으면 **개봉 설정으로**를 누르세요.
4. 원하는 항목을 선택한 뒤 **설정 저장하고 게임 실행**을 누르세요.

압축 해제 없이 실행 파일 하나로 사용할 수 있습니다. 필요한 파일은 사용자 계정의 앱 데이터 폴더에 자동으로 준비합니다. Windows 10/11 x64와 기본 Windows PowerShell / .NET Framework를 사용합니다.

## 원하는 상자만 선택하세요

| 설정 | 처음 실행할 때 |
| --- | --- |
| 자동 개봉 사용 | 켜짐 |
| 내 상자 자동 개봉 | 켜짐 |
| 다른 사람의 상자도 개봉 | **꺼짐** |
| 개봉 사이 대기 시간 | 3~8초 |

내 상자와 다른 사람의 상자를 각각 선택할 수 있습니다. 다른 사람의 상자를 선택하면 **내 포인트를 소비**해 로비에 보이는 상자를 열어 줍니다. 숨긴 플레이어와 표시되지 않는 상자는 제외합니다. 게임의 기존 비용과 구매 기능을 그대로 사용합니다.

기존 사용자는 저장된 설정을 불러옵니다. 이전에 다른 사람의 상자를 켜 두었다면 선택 상태가 유지되므로 화면에서 끌 수 있습니다.

## 나중에 설정 바꾸기

도우미를 다시 열면 됩니다. 바탕화면 바로 가기를 선택해 두면 다운로드한 파일을 옮겨도 **Bongo Cat 도우미** 바로 가기로 실행할 수 있습니다.

- **자동 개봉 사용**을 끄면 자동 개봉을 일시 중지합니다.
- 게임 중에는 **Ctrl+Alt+F9**로 켜고 끌 수 있습니다.
- 대기 시간은 화면에서 바꾸며, 최소 시간보다 최대 시간이 짧으면 적용하지 않습니다.
- 기존 설정 파일에 대한 수동 변경도 실행 중 약 2초마다 반영됩니다.

## 업데이트와 복원

Steam 업데이트가 끝나면 게임을 종료하고 도우미를 다시 실행하세요. 새 게임 파일에 맞춰 호환성을 검사하고 다시 적용합니다. 게임 구조가 바뀌어 호환되지 않으면 안내를 표시합니다.

첫 화면의 **자동 개봉 제거 / 원본 복원**으로 되돌릴 수 있습니다. 설정과 백업은 보관합니다. 현재 게임과 맞지 않는 예전 백업을 덮어씌우지 않습니다.

## 문제가 생겼나요?

- **게임을 못 찾아요:** 첫 화면의 **폴더 변경**에서 `BongoCat.exe`가 있는 폴더를 선택하세요.
- **게임이 종료되지 않아요:** Bongo Cat 메뉴 또는 트레이에서 종료한 뒤 **다시 시도**를 누르세요.
- **게임 폴더에 쓸 권한이 없어요:** 오류 화면의 **관리자 권한으로 다시 열기**를 사용하세요.
- **복원할 원본이 없어요:** Steam의 **설치된 파일 → 게임 파일 무결성 확인**으로 복구한 뒤 다시 실행하세요.
- **고양이가 설정 창을 가려요:** 게임의 항상 위 표시가 원인일 수 있습니다. 게임을 종료하고 도우미를 실행하세요.

오류 화면의 **자세한 내용 보기**에서 진단 내용을 확인할 수 있습니다. 게임 폴더의 `BongoAutoChest.log`에는 플레이어 식별자가 포함될 수 있으니 공개 이슈에 원문을 올리지 마세요.

## 프로젝트 정보

게임 DLL, 디컴파일한 게임 소스, 계정 정보는 배포하지 않습니다. 각 PC의 게임 파일을 검사하고 원본을 백업한 뒤 두 곳에 호출을 추가합니다. 공식 프로그램이 아니며 운영사와 관계가 없습니다. 탐지 회피 기능은 없습니다.

검증한 게임: Steam build **25400987**, Unity **6000.2.8f1**, Windows Mono. 향후 모든 게임 버전과 다른 실제 PC에서의 동작을 보장하지 않습니다. 자세한 검증 범위는 [VALIDATION.md](VALIDATION.md)를 참고하세요.

## 개발

```powershell
.\tests\Test.ps1
.\tests\Test.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\BongoCat'
.\Package.ps1
```

`Package.ps1`은 Windows 기본 .NET Framework 컴파일러로 설정 마법사를 빌드하고 실행에 필요한 파일을 EXE에 포함합니다. Mono.Cecil 0.11.6은 공식 NuGet에서 내려받아 고정 SHA-256을 확인합니다. 기존 PowerShell / CMD 도구는 소스 이용자를 위해 유지합니다. 게임 의존 통합 검사는 임시 복사본에서만 실행합니다.

도우미 소스: [MIT](LICENSE). Mono.Cecil: [MIT, Jb Evain / Novell](vendor/Mono.Cecil.LICENSE.txt). 게임과 게임 파일의 권리는 해당 권리자에게 있습니다.

## English

Download **BongoAutoChest.exe** from the latest release, finish Steam updates, close the game and double-click the EXE. The Korean-language setup wizard finds the game, lets you select your own and/or visible lobby chests, saves settings and launches through Steam. **Other players' chests are off by default** and consume your in-game points when enabled. Existing choices are preserved. A desktop shortcut and original-file restoration are available inside the wizard. No terminal commands, environment-variable setup or game-file downloads are required.
