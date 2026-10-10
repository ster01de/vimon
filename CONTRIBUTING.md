# Contributing

Vimon is intentionally minimal.

Before adding a metric:
1. Does it help the user make a decision?
2. Can it degrade gracefully when unavailable?
3. Can it be described without vendor jargon?
4. Is it platform-specific or should it live in Core?

Keep platform collection behind backend interfaces.
Keep diagnostics independent from rendering.
