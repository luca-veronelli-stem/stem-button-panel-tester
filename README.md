# ButtonPanelTester

[![CI](https://github.com/luca-veronelli-stem/stem-button-panel-tester/actions/workflows/ci.yml/badge.svg)](https://github.com/luca-veronelli-stem/stem-button-panel-tester/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Proprietary-red)](#license)

> **Bench tool for testing STEM button-panel hardware over CAN.**
> **Standard:** v1.2.1 — see [`docs/Standards/`](./docs/Standards/).

---

## Overview

ButtonPanelTester is a Windows desktop bench tool used by STEM technicians to baptize and run protocol-level tests against the company's button-panel hardware over CAN. It drives Peak PCAN-USB adapters via the `Peak.PCANBasic.NET` vendor SDK, reads protocol/variable dictionaries from Excel via ClosedXML, and is currently a WinForms app (Avalonia + FuncUI migration tracked under [`CLAUDE.md`](./CLAUDE.md) Phase 4).

## Quick Start

```powershell
dotnet build
dotnet test
dotnet run --project src/ButtonPanelTester.GUI
```

## Solution Structure

```
src/
├── ButtonPanelTester.Core/                domain types + ports
├── ButtonPanelTester.Services/            use cases
├── ButtonPanelTester.Infrastructure/      adapters (EF Core, drivers, IO)
└── ButtonPanelTester.GUI/                 Avalonia + FuncUI
tests/
└── ButtonPanelTester.Tests/               xUnit + FsCheck + Avalonia.Headless
specs/                           Lean 4 formal specs
docs/                            documentation (Standards/ tracked here)
eng/                             build / release scripts
```

## Documentation

- Standards followed: [`docs/Standards/`](./docs/Standards/) — pinned to `v1.2.1`.
- Changelog: [`CHANGELOG.md`](./CHANGELOG.md).
- Repo-specific notes: [`CLAUDE.md`](./CLAUDE.md).

## License

- **Owner:** STEM E.m.s.
- **Author:** Luca Veronelli
- **Creation Date:** 2026
- **License:** Proprietary — All rights reserved.
