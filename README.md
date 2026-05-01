# AR PC Disassembly Guide

> **3D технологии и виртуална реалност**

AR приложение тип **"Ръководство на потребител"**: разпознава 3 маркера (дънна
платка, графична карта, захранване), показва върху всеки 3D модел на
съответния PC компонент, и позволява tap-to-disassemble с анимация и панел
със спецификации.

## Tech stack

| Слой             | Решение                                                   |
| ---------------- | --------------------------------------------------------- |
| Engine           | Unity **6.4** (6000.4.4f1), Built-In Render Pipeline      |
| AR Framework     | **Vuforia Engine 11.4.4** (Image Target tracking)         |
| Target платформа | **iOS** (тествано на iPhone 15 Pro / A17 Pro)             |
| Input            | New Input System (`UnityEngine.InputSystem`)              |
| UI               | TextMeshPro в Screen Space Overlay Canvas (singleton HUD) |
| Scripting        | C# 9, IL2CPP backend за iOS                               |
| Source control   | Git + Git LFS (за Vuforia .tgz пакета)                    |

---

## Функционалност

- **3 едновременно tracked маркери** (`Max Simultaneous Tracked Images = 3` в Vuforia config)
- **3D модели от примитиви**:
  - **Motherboard** — PCB + CPU socket/heatsink + 4 RAM stickа + капацитори + Northbridge/Southbridge + I/O backplate + PCIe слот
  - **GPU** — PCB + cooler shroud + 2 fans + heatsink + GPU die + 8 VRAM чипа + PCIe edge connector + display ports
  - **PSU** — bottom plate + 4 walls + top fan grille + internal fan + main capacitor + secondary capacitor + heatsink + power switch
- **Tap-to-disassemble** — раздалечава всички части от assembled позиция към exploded view с easing анимация (Coroutine-based, ~1.2s default)
- **Screen-space spec HUD** — fade-in панел при дъното на екрана, auto-grow според съдържанието (`ContentSizeFitter` + `VerticalLayoutGroup`), показва име + 6 специфики (form factor, chipset, wattage, и т.н.). Singleton с owner tracking — ако са разглобени два компонента едновременно, панелът показва специфики на последно тапнатия и не се скрива от Hide() на друг компонент.
- **Tap-to-reassemble** — повторен tap връща частите в assembled state и hide-ва spec panel-а

## Getting started

### Изисквания

- macOS (Apple Silicon)
- Unity Hub + Unity Editor **6000.4.4f1** с **iOS Build Support** module
- Xcode 15+ (за iOS deploy)
- iPhone (iOS 15+)
- Apple ID (free personal team е достатъчен)
- Vuforia developer акаунт + Basic license key (безплатен) от https://developer.vuforia.com
- Git + **Git LFS**

### Clone

```bash
brew install git-lfs
git lfs install                       # one-time setup в global git config
git clone git@github.com:hugovasko/3dtvr.git
cd 3dtvr
git lfs pull                          # извлича Vuforia .tgz binary от LFS storage
```

> ⚠️ **Без `git lfs pull`** Vuforia пакетът ще е 134-byte LFS pointer вместо
> реалните 138MB и Unity ще даде грешка `not a valid package tarball`.

### Vuforia license key

1. Регистрирай се на https://developer.vuforia.com
2. **Plan & Licenses** → **Get Basic**
3. Copy License Key
4. В Unity → отвори `Assets/Resources/VuforiaConfiguration.asset`
5. Постави ключа в полето **App License Key**

### Marker database

Маркерите вече са включени в проекта (`Assets/StreamingAssets/Vuforia/pc-disassembly.dat`).

## Build & Run

### iOS (Xcode workflow)

1. **File → Build Profiles** → избери **iOS** → **Switch Platform** (ако вече не е активна)
2. **Build** → избери output папка (например `iOSBuild/`)
3. Unity генерира Xcode workspace
4. Xcode се отваря автоматично с проекта
5. Top-left → проектът → **Signing & Capabilities** → избери Apple ID team
6. Свържи iPhone с USB
7. Top toolbar → избери iPhone като target device
8. Натисни ▶ Play → app се build-ва, инсталира и стартира на iPhone-а
9. Първият път iOS може да поиска **Trust Developer**: Settings → General → VPN & Device Management

### macOS Editor (за разработка/демо без iPhone)

1. Отвори проекта в Unity Editor
2. Натисни ▶ Play в toolbar
3. Webcam-ът на Mac-а става AR камера
4. Покажи printed маркер (или маркер на втори екран/телефон) → 3D модел изскача
5. Click с мишката върху модел → disassemble + spec panel
6. Click отново → assemble

## Custom Editor tooling

Проектът включва **PCComponentBuilder** Editor script който генерира трите
компонента като prefabs от примитиви. След clone + setup:

**Top menu** → **Tools** → **Build PC Components**

Това (re)генерира:

- 3 prefabs в `Assets/Prefabs/Components/` (MotherboardModel, GPUModel, PSUModel)
- Споделени Materials в `Assets/Materials/Components/`
- Auto-добавя SpecUI child към всеки prefab

Промени в дизайна (цветове, scale, disassemble offsets, specs текст) се правят
директно в `Assets/Editor/PCComponentBuilder.cs` и след re-run се reflect-ват
автоматично - scene instances остават linked чрез prefab GUID.

## Git LFS info

Vuforia Engine package е 138MB (.tgz) - над GitHub-ското 100MB single file
лимит. Затова проектът използва **Git LFS** за `*.tgz` файлове (виж
`.gitattributes`).

Всеки Клониращ репото трябва задължително:

```bash
brew install git-lfs && git lfs install
git lfs pull
```

Без `git lfs pull` Unity ще покаже грешка
`com.ptc.vuforia.engine: ... is not a valid package tarball`.

## Credits

- **Vuforia Engine** © PTC Inc. - Used under Basic Developer License
- **Unity 6** © Unity Technologies
- **TextMeshPro** © Unity Technologies
