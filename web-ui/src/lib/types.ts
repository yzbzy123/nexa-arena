export type Game = "CS2" | "VALORANT"
export type Mode = { width: number; height: number }
export type DisplayState = {
  width: number
  height: number
  refresh: number
  saved: { width: number; height: number; refresh: number } | null
  stage: number
  selected: Mode | null
  autoRestore: boolean
  status: string
  restoring: boolean
  switching: boolean
  configRepaired: boolean
}
export type CrosshairData = {
  screenHeight: number; style: number; r: number; g: number; b: number; a: number;
  outlineR: number; outlineG: number; outlineB: number; outlineA: number;
  thickness: number; outlineMode: number; gap: number; length: number;
  dynamicSpreadLimit: number; splitDistance: number; innerSplitAlpha: number;
  outerSplitAlpha: number; splitSizeRatio: number; scopeDotScale: number;
  followRecoil: boolean; centerDot: boolean; tStyle: boolean; scopeDotUseColor: boolean;
}
export type Crosshair = { name: string; code: string; resolution?: string; description?: string; preview?: CrosshairData }
export type Filter = { name: string; description: string; color: string }
export type Hotkey = { action: string; enabled: boolean; modifiers: number; key: number }
export type Settings = {
  minimizeToTray: boolean
  sourceGame: Game
  targetGame: Game
  sourceResolution: string
  targetResolution: string
  method: string
  ratio: number
  sourceDpi: number
  targetDpi: number
  sensitivity: number
  hotkeys: Hotkey[]
}
export type Bootstrap = {
  optimizationGame?: Game
  display: DisplayState
  modes: Mode[]
  cs2: Crosshair[]
  valorant: Crosshair[]
  filters: Filter[]
  sensitivity: {
    settings: Settings
    resolutions: { key: string; label: string }[]
    methods: { id: string; label: string }[]
  }
  buyItems: { id: string; name: string }[]
  preview: boolean
}
export type AudioDevice = {
  Id: string
  Name: string
  Flow: number
  IsDefault: boolean
  Muted: boolean
  Volume: number
}
export type AudioSession = {
  DeviceId: string
  SessionId: string
  Name: string
  Muted: boolean
  Volume: number
}
export const demo: Bootstrap = {
  display: {
    width: 1920, height: 1080, refresh: 280, saved: null, stage: 0,
    selected: { width: 1440, height: 1080 }, autoRestore: true,
    status: "网页布局预览：请在桌面程序中使用系统功能。", restoring: false, switching: false,
    configRepaired: false,
  },
  modes: [
    { width: 1280, height: 960 }, { width: 1280, height: 1024 },
    { width: 1440, height: 1080 }, { width: 1568, height: 1080 },
    { width: 2088, height: 1440 },
  ],
  cs2: [], valorant: [], filters: [],
  sensitivity: {
    settings: {
      minimizeToTray: true, sourceGame: "CS2", targetGame: "VALORANT",
      sourceResolution: "1920x1080", targetResolution: "1920x1080", method: "screen-center",
      ratio: 3.5, sourceDpi: 800, targetDpi: 800, sensitivity: 1,
      hotkeys: [
        { action: "switch", enabled: true, modifiers: 3, key: 83 },
        { action: "restore", enabled: true, modifiers: 3, key: 72 },
        { action: "window", enabled: true, modifiers: 3, key: 78 },
        { action: "microphone", enabled: false, modifiers: 3, key: 77 },
      ],
    },
    resolutions: [
      { key: "1920x1080", label: "1920 × 1080 · 16:9" },
      { key: "1440x1080", label: "1440 × 1080 · 4:3" },
      { key: "1280x1024", label: "1280 × 1024 · 5:4" },
    ],
    methods: [
      { id: "screen-center", label: "水平微调匹配（MDH 0%）" },
      { id: "turn-distance", label: "等转身距离（cm/360）" },
      { id: "custom-ratio", label: "自定义经验比例" },
    ],
  },
  buyItems: [], preview: true,
}
