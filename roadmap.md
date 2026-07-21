
## Phase 6: Advanced Widget Mode
- **6.1 Mini Widget Window:** Secondary always-on-top HWND/viewport, compact CPU+RAM sparkline, borderless/draggable.
- **6.2 Impact List:** Top-N processes by impact score (High/Medium/Low) inside widget, End/Priority/Properties actions.
- **6.3 Widget<->Main Sync:** Shared ProcessSnapshot read (no duplicate enumeration thread), toggle to launch/dock widget from main window.

### 5.1 Note (Revised Target)
Original <20MB RAM target revised to <100MB after profiling showed DX11 driver/
runtime overhead (~40-80MB fixed cost) makes <20MB unachievable with the chosen
ImGui+DX11 rendering backend. CPU target (<2%) confirmed met (measured: 1.0%).
