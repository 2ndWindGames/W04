# SWGUnity2DCore

Unity 6용 공통 코드 저장소입니다. Git 서브모듈로 커밋을 고정하고, 내부 UPM 패키지를 프로젝트에서 로컬 경로로 연결합니다.

| 패키지 | 내용 | 외부 의존성 |
| --- | --- | --- |
| `com.secondwind.core` | UI 바인딩·팝업, 리소스, 사운드, 풀링, 타이머, 로깅 | Unity uGUI/TMP, 엔진 모듈 |
| `com.secondwind.ads.admob` | Android 광고 동의, SDK 초기화, 배너·전면 광고, 재시도 | Google Mobile Ads 11.5.0 |
| `com.secondwind.leaderboards.ugs` | 익명 인증, 메타데이터, 리더보드 조회·제출 | UGS Authentication/Core/Leaderboards, Newtonsoft JSON |

각 패키지는 독립적인 `asmdef`로 컴파일됩니다. Core는 광고/UGS 또는 게임의 `Assembly-CSharp`를 참조하지 않습니다. 선택 패키지끼리도 의존하지 않습니다.

## W04에 연결

먼저 이 저장소의 변경을 커밋·푸시한 다음, W04 루트에서 실행합니다.

```sh
git submodule add https://github.com/2ndWindGames/SWGUnity2DCore.git LocalPackages/SWGUnity2DCore
```

W04의 `Packages/manifest.json`의 기존 `dependencies`에 필요한 항목만 병합합니다. 경로는 `Packages` 폴더 기준입니다.

```json
{
  "dependencies": {
    "com.secondwind.core": "file:../LocalPackages/SWGUnity2DCore/Packages/com.secondwind.core"
  }
}
```

광고/랭킹을 사용할 때만 다음 항목을 추가합니다.

```json
{
  "com.secondwind.ads.admob": "file:../LocalPackages/SWGUnity2DCore/Packages/com.secondwind.ads.admob",
  "com.secondwind.leaderboards.ugs": "file:../LocalPackages/SWGUnity2DCore/Packages/com.secondwind.leaderboards.ugs"
}
```

AdMob을 추가하면 프로젝트 manifest의 `scopedRegistries`에도 아래 레지스트리가 필요합니다. 기존 항목을 덮어쓰지 말고 병합하세요.

```json
{
  "name": "OpenUPM",
  "url": "https://package.openupm.com",
  "scopes": ["com.google"]
}
```

패키지의 `package.json`이 SDK 버전을 선언하므로, 새 프로젝트에서 해당 SDK를 직접 중복 선언할 필요는 없습니다. Unity가 생성한 `Packages/packages-lock.json`을 함께 커밋합니다.

## Core 사용

```csharp
using SWGUnity2DCore.Manager;
using SWGUnity2DCore.Util;

// 게임 시작 때, 팝업을 열기 전에 설정합니다.
CoreServices.UI.Configure(initialSortingOrder: 0, planeDistance: 1f);
CoreServices.ButtonClicked = () =>
    CoreServices.Sound.Play(Define.Sound.Effect, "SFX/Button_Click", 0.65f);

CoreServices.Sound.Play(Define.Sound.Bgm, "BGM/Main", 0.4f);
// CoreServices.UI.ShowPopupUI<MyPopup>();
```

`CoreServices`는 온라인 SDK를 초기화하지 않습니다. 사운드는 최초 사용 시 초기화됩니다. 버튼 클릭 콜백은 설정하지 않으면 아무 소리도 내지 않습니다.

현재 리소스 규칙:

- 사운드: `Assets/Resources/Sounds/`
- 팝업: `Assets/Resources/Prefabs/UI/Popup/`
- 서브 아이템: `Assets/Resources/Prefabs/UI/SubItem/`
- 씬 UI: `Assets/Resources/Prefabs/UI/Scene/`
- UI는 기본적으로 Screen Space Camera이며, 게임에서 Camera와 EventSystem을 준비합니다.

프리팹·음원은 게임 프로젝트가 제공합니다. `UI_Popup`은 기존 프리팹 호환을 위해 전역 네임스페이스를 유지합니다. 공통 `Managers` 싱글턴은 제공하지 않습니다. 게임에서 서비스 생명주기를 구성하세요.

## AdMob 사용

```csharp
var ads = new AdsManager(new AdMobOptions()); // 기본은 Google 테스트 ID + 테스트 모드
ads.Init();
// 영속적인 게임 부트스트랩의 Update에서 ads.Tick();
ads.ShowBanner();
// 안전한 결과 화면 등 게임이 정한 시점에서 ads.ShowInterstitialAds();
// 배너가 필요 없는 화면에 진입하면 ads.HideBanner();
```

- Google Mobile Ads Settings에는 **새 앱의 Android App ID**를 설정해야 합니다.
- 실광고를 적용할 때 `AndroidBannerId`, `AndroidInterstitialId`와 `ForceTestAds = false`를 전달합니다. Editor/Development Build는 계속 테스트 ID를 사용합니다.
- 현재 지원하는 타깃은 Android와 Unity Editor입니다. iOS 구현은 포함하지 않습니다.
- 몇 판마다 표시할지, 최소 간격, 배너 위치를 위한 게임 화면 여백은 게임 정책입니다. `BannerHeightChanged`, `BannerHeightPixels`, `IsShowingInterstitial`, `InterstitialOpened`를 이용합니다.
- `PrivacyOptionsRequired`가 참이면 게임 설정에 `ShowPrivacyOptions()`로 연결되는 항목을 제공합니다.
- Android SDK/EDM 의존성 해결, 동의 메시지, AdMob 앱 승인·app-ads.txt, 서명·버전은 게임별로 설정합니다.
- Google Mobile Ads 11.5.0 Standard SDK에서 R8이 미사용 Next-Gen 클래스 누락을 보고하면, Custom Proguard File을 활성화하고 아래 규칙을 적용합니다. Next-Gen SDK 사용 시에는 이 규칙을 적용하지 않습니다.

```proguard
-dontwarn com.google.android.libraries.ads.mobile.sdk.**
```

## UGS 랭킹 사용

```csharp
var ranking = new RankManager("W04에서 만든 리더보드 ID");
ranking.Init();
ranking.SetProfile("nickname", "KR", "default");
ranking.SubmitScore(100);
var entries = await ranking.GetTopNPlayers(10);
```

새 게임의 Unity Cloud 프로젝트 연결, 익명 인증, 리더보드 생성은 별도 설정입니다. 조회 결과는 성공 시 목록, 데이터 없음은 빈 목록, 오류는 `null`입니다. 기존 `Init`/`SubmitScore`는 오류를 로그로 처리하는 `async void` API이므로 완료를 기다리는 용도로 사용하지 마세요.

## 버전 관리

- 패키지 수정은 서브모듈 저장소에서 커밋하고 먼저 푸시합니다.
- 그 후 게임 저장소에서 서브모듈의 변경된 커밋 포인터와 manifest/lock 파일을 커밋합니다.
- 다른 PC에서는 `git clone --recurse-submodules ...` 또는 `git submodule update --init --recursive`를 사용합니다.
- 배포 프로젝트에서 매번 최신 브랜치를 자동으로 따라가지 말고 검증한 커밋을 고정합니다.
- 스크립트 이동 시 `.meta`를 함께 옮깁니다. 임의로 재생성하면 프리팹 참조가 끊어집니다.
- W01의 `GameConfig`, 씬 종류, 데이터 모델, `Managers`, 광고 주기 및 실제 ID는 공통 패키지에 포함하지 않습니다.
