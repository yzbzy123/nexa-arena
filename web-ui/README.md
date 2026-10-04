# Nexa Arena UI

React、TypeScript、Vite、Tailwind CSS 和 shadcn/ui（Radix）构建的桌面工具界面。

```powershell
npm ci
npm run build
```

src/components/pages 包含功能页面，src/lib/bridge.ts 与 C# 宿主通信。npm run dev 提供网页预览；原生操作需要桌面宿主。

设计规范见 [DESIGN.md](DESIGN.md)，产品边界见 [PRODUCT.md](PRODUCT.md)。
