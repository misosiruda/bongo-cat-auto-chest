# Bongo Cat Auto Chest

Windows Steam판 Bongo Cat의 일반 상자·감정표현 상자를 자동으로 여는 비공식 도우미입니다. 내 상자와 현재 로비에서 보이는 다른 플레이어의 상자 열기를 지원합니다.

**게임 업데이트 후에는 Steam 다운로드가 끝난 상태에서 `Start.cmd`를 실행하세요.** 현재 설치된 게임 DLL을 검사·백업하고, 그 버전에 맞춰 도우미를 빌드하고 적용합니다. 이전 버전의 게임 DLL을 덮어씌우지 않습니다.

## 다른 PC에서 사용하기

1. Steam으로 Bongo Cat을 설치하고 한 번 실행합니다. 진행 중인 업데이트를 마칩니다.
2. [최신 릴리스](https://github.com/misosiruda/bongo-cat-auto-chest/releases/latest)에서 `bongo-cat-auto-chest-windows.zip`을 다운로드하고 **압축을 모두 풉니다**. 쓰기 가능한 일반 폴더를 사용하세요.
3. 게임을 종료한 다음 `Start.cmd`를 더블클릭합니다. Steam 라이브러리에서 게임을 찾아 적용한 후 실행합니다.
4. 이후에도 `Start.cmd`로 실행하면 게임 업데이트로 패치가 없어졌을 때 다시 적용합니다. 바탕화면에 `Start.cmd`의 바로 가기를 만들어 두면 편합니다.

Windows 10/11 x64, Windows PowerShell 5.1, .NET Framework 4.x, Steam이 필요합니다. 별도 .NET SDK나 BepInEx는 필요하지 않습니다. 릴리스 ZIP에는 Mono.Cecil이 들어 있습니다. 소스를 받은 경우 첫 실행 시 고정된 버전의 Mono.Cecil을 NuGet에서 내려받아 SHA-256을 검사합니다.

자동으로 찾지 못하면 PowerShell에서 설치 경로를 지정할 수 있습니다.

```powershell
.\Launcher.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\BongoCat'
```

게임 폴더에 쓸 권한이 없어 실패하면 쓰기 가능한 Steam 라이브러리를 사용하거나 실행 도우미를 관리자 권한으로 실행하세요. 종료 대기 중 실패하면 게임 메뉴/트레이에서 직접 종료하고 다시 실행하세요.

## 설정

게임 설치 폴더의 `BongoAutoChest.ini`를 편집합니다. 기존 설정은 재설치와 업데이트 시 보존하며, 실행 중 약 2초마다 다시 읽습니다.

```ini
Enabled=true
AutoOwn=true
AutoOthers=true
MinDelaySeconds=3
MaxDelaySeconds=8
```

- `Ctrl+Alt+F9`: 전체 자동 개봉 켜기/끄기. INI에도 저장됩니다.
- 기본 대기 시간은 작업 사이 3~8초입니다. 설정값은 1~300초 범위로 제한합니다.
- 내 상자를 우선 처리하며, 게임의 기존 구매·선물 기능과 비용을 그대로 사용합니다. **다른 사람의 상자를 열 때도 내 포인트를 소비합니다.**
- 숨긴 플레이어와 표시되지 않는 상자는 제외합니다. 로비 상자 표시 설정이 켜져 있어야 합니다.
- 동일한 준비 상태에서는 한 번만 시도합니다. 내 구매 응답 대기 중에는 추가 자동 구매를 보내지 않습니다.
- 로그는 게임 폴더의 `BongoAutoChest.log`에 기록됩니다. `SENT`는 요청 전송 기록이며 상대방의 아이템 수령을 보장하지 않습니다. 로그에는 플레이어 식별자가 들어갈 수 있으므로 공개 이슈에 원문을 올리지 마세요.

## 업데이트와 원상 복구

게임 내부의 필드·메서드와 실행에 필요한 참조를 검사한 뒤 두 메서드에만 호출을 추가합니다. 모든 기존 메서드 본문이 보존되는지 확인합니다. 원본은 게임 폴더의 `.bongo-auto-chest` 안에 SHA-256별로 보관합니다.

게임 코드 구조가 바뀌어 검사를 통과하지 못하면 자동 적용을 중단합니다. 구조 검사는 향후 버전의 동작까지 보장하지 않습니다. Steam이 **도우미 실행 후** 업데이트를 시작해 파일을 교체한 경우에는 업데이트가 끝난 후 게임을 종료하고 도우미를 다시 실행하세요.

- `Restore.cmd`: 현재 패치와 대응하는 원본으로 복원합니다. 게임이 이미 새 원본으로 바뀌었다면 예전 백업을 적용하지 않습니다.
- 설정과 백업은 복원 후에도 남습니다. 도우미 DLL은 원본 게임에서 호출되지 않습니다.
- 백업이 없거나 손상되었으면 Steam의 **설치된 파일 → 게임 파일 무결성 확인**으로 복구한 뒤 도우미를 다시 실행하세요.

```powershell
# 게임 파일을 수정하지 않고 현재 설치의 호환성 검사
.\Launcher.ps1 -Action Check
# 설치만 수행 (게임 실행 생략)
.\Launcher.ps1 -Action Install
# 복원
.\Launcher.ps1 -Action Restore
```

## 개발·검증

```powershell
.\Build.ps1
.\tests\Test.ps1
# 설치된 게임을 읽어 임시 복사본에서만 설치/업데이트/복원을 검증
.\tests\Test.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\BongoCat'
.\Package.ps1
```

게임 DLL, 디컴파일한 게임 소스, 계정 정보, 실행 로그는 배포하지 않습니다. `src/`는 도우미와 패처 소스입니다. 게임 DLL은 각 PC에서만 읽고 수정하며 저장소에 넣지 않습니다.

현재 검증 기준: Steam build **25400987**, Unity **6000.2.8f1**, Windows Mono. 기존 자동 개봉 구현에서 내 일반·감정표현 상자 수령과 로비 요청 전송을 확인했습니다. 실행 도우미의 업데이트 처리는 실제 게임 복사본의 버전을 변경해 검사합니다. 다른 실제 PC와 아직 출시되지 않은 게임 버전은 검증하지 않았습니다.

공식 프로그램이 아니며 게임 운영사와 관계가 없습니다. 탐지 회피 기능은 없고 제재 여부도 보장하지 않습니다.

## License

Authored helper code: [MIT](LICENSE). Mono.Cecil 0.11.6: [MIT, Jb Evain / Novell](vendor/Mono.Cecil.LICENSE.txt), obtained from the [official NuGet package](https://www.nuget.org/packages/Mono.Cecil/0.11.6). Bongo Cat and its game files belong to their respective owners and are not included.

## English quick start

Unofficial auto-opener for your own normal/emote chests and visible lobby chest gift actions. Extract the Windows release ZIP, finish Steam updates, close Bongo Cat, then double-click `Start.cmd`. The helper discovers Steam libraries, builds against the locally installed game, backs up the original, verifies compatibility, applies two hooks, and launches through Steam. Use `Restore.cmd` to undo the patch. Settings are in the game's `BongoAutoChest.ini`. Opening others' chests spends your in-game points. No game binaries or decompiled game sources are distributed. Future game versions may require helper changes.
