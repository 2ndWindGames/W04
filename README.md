# 소복 · SOBOK

다섯 가지 쌓기 게임을 한 실행 파일에서 비교할 수 있는 프로토타입입니다. 시작 화면에서 카드를 누르거나 숫자 1–5를 눌러 선택합니다. 플레이 중에는 왼쪽 위 **MENU / Esc**로 선택 화면에 돌아가고, **RETRY / R**로 현재 모드를 다시 시작합니다.

| 선택 | 게임 | 목표와 규칙 | 조작 |
| --- | --- | --- | --- |
| 1 | Original Stack / 기존 스택 | X/Z 방향으로 번갈아 놓으며 최고 기록에 도전합니다. 튀어나온 부분은 잘리고, 정확한 배치로 얻은 빛 3개로 폭을 회복합니다. | 클릭·터치·Space로 배치, E로 폭 회복 |
| 2 | Flower Garden / 꽃 정원 | 블록을 놓을 때 꽃이 피고, 정확하게 놓으면 더 많은 꽃이 핍니다. 15층을 완성하면 꽃다발과 완성 화면이 나옵니다. | 클릭·터치·Space로 배치, E로 폭 회복 |
| 3 | Wall Breach / 벽 돌파 | 블록 5개를 쌓아 탑 전체를 발사합니다. 구멍 바깥 부분은 잘리고 살아남은 탑에 다시 쌓습니다. 파편을 제때 회수하면 다음 블록을 넓힙니다. | 클릭·터치·Space로 배치 → 발사 → 회수 |
| 4 | Tower Bridge / 탑 다리 | 탑을 눕혀 실제 다리로 만들고 여행자가 건너갑니다. 짧으면 추락하고, 충분히 길면 통과합니다. 금색 구역에 끝을 맞추면 추가 점수를 얻습니다. 5번 건너면 완주합니다. | 클릭·터치·Space로 배치, Enter 또는 TIP 버튼으로 눕히기 |
| 5 | Tower Battle / 탑 전투 | 한 턴에 블록 5개로 공격을 준비합니다. 정확한 배치와 남은 폭이 공격력을 정합니다. 적이 살아남으면 반격합니다. 3개의 적 탑을 쓰러뜨리면 승리합니다. | 클릭·터치·Space로 배치 → 발사 → 다음 턴 |

사운드는 오른쪽 위 이미지 버튼 또는 M으로 켜고 끕니다. 모드를 바꿀 때 탑·카메라·배경·효과음·UI를 새로 준비하므로 서로 다른 게임 상태가 섞이지 않습니다. 기존 스택과 정원의 기록은 서로 다른 저장 키를 사용합니다.

## 실행

Unity **6000.3.23f1**에서 `Assets/Scenes/SampleScene.unity`를 열고 Play를 누르면 다섯 게임 선택 화면이 열립니다. `SobokArcadeHub`가 각 모드를 실행합니다. 큐브·꽃·다리·적 탑 등 게임 지형은 코드로 생성하며 기존 파스텔 블록 소재와 배경을 공유합니다.

Windows 실행 파일: **`Builds/FiveGames/SobokFiveGames.exe`**. 다른 PC에서 실행하려면 `FiveGames` 폴더 전체를 복사하세요.

`SobokArcadePreviewBuild.Build`는 `SOBOK_PREVIEW_EXE` 환경 변수에 지정한 경로로 Windows 개발 빌드를 만듭니다. 별도 복사 프로젝트를 사용해 열려 있는 Unity 프로젝트의 빌드 설정과 제품명을 바꾸지 않습니다. 미리보기 제품명은 `SOBOK Five Games Preview`이며 실제 앱과 기록을 분리합니다.

## 검증

`-sobok-arcade-check` 실행 인수와 `SOBOK_ARCADE_CAPTURE` 출력 디렉터리를 지정하면 선택 메뉴, 다섯 모드의 핵심 동작, 승리·실패·재시작, 모드 전환 후 객체 정리, 실제 플레이 화면을 검사합니다. 결과와 화면은 `resources/five-games-preview/`에 저장합니다.

기존 벽 돌파 전용 `SobokBreachPreviewBuild.Build`와 `-sobok-breach-check` 검사는 계속 사용할 수 있습니다. 해당 플래그는 선택 메뉴에서 벽 돌파를 자동 실행합니다. 기존 `Builds/GardenPreview`, `Builds/RecoveryPreview`, `Builds/BreachPreview`와 해당 이미지 폴더는 이전 버전 기록입니다.

수동으로는 다섯 카드를 각각 눌러 진입하고, 각 게임에서 메뉴·재시작·사운드 버튼이 배치 입력과 겹치지 않는지 확인합니다. 특히 다리 모드의 짧은 다리 실패/금색 착지, 전투 모드의 반격/패배/최종 승리, 꽃 정원의 15층 완성 화면을 비교해보세요.

## 이미지와 사운드

무광 파스텔 블록과 낮·노을·밤·새벽 배경을 공유합니다. `SobokSky`와 `Resources/SobokSky.shader`가 배경을 그리며, `SobokAudio`의 합성 배경음과 배치·회수·발사·충돌음을 사용합니다.

SOBOK 앱 이름과 `Assets/Art/Branding/Sobok-Icon.png`를 기본 및 Android 아이콘으로 사용합니다. 아이콘은 image_gen으로 제작했으며 생성 기록은 같은 폴더의 `SobokGenerationNotes.md`에 있습니다.
