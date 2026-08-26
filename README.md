# HowFishControlsDemo

Маленькое landscape iOS-приложение для проверки мобильного HUD перед интеграцией в порт How to Fish.

## Что внутри

- SwiftUI host + `WKWebView`
- landscape-only
- fullscreen HUD prototype
- реальные actions/bindings, восстановленные из игры
- отдельная логика short press / hold для Drop (Q)
- press/hold/release для LMB/RMB
- hold-кнопки для Sprint/Crouch/PTT

## Сборка

GitHub Actions собирает unsigned IPA на macOS runner без code signing и кладёт его в artifact `HowFishControlsDemo-unsigned-ipa`.

Локально: открыть `HowFishControlsDemo.xcodeproj` в Xcode и выбрать iPhone target.
