# SandCastleMaker 🏖️🏰

**SandCastleMaker** es un juego arcade táctil en 2D con estética **Pixel Art** desarrollado en **Unity 2022.3 LTS** para Android (orientación vertical), donde tenés que esculpir castillos de arena a mano alzada antes de que suba la marea y las olas destruyan tu obra.

> **Ramas del repositorio**
> - `Main`: versión **MVP** inicial del proyecto (prototipo con física de bloques).
> - `release-v1.0` *(rama actual)*: **versión completa de lanzamiento**, rediseñada con esculpido en lienzo pixel art, marea viva, Gira Mundial, Pack Brasil, menús/tienda animados, audio dinámico y pulido mobile.

---

## 🎮 Mecánicas de Gameplay (v1.0)

- **Esculpido Táctil en Tiempo Real (`SandCanvasController` + `SandSculpt.shader`):**
  - Dibujás libremente con el dedo sobre una grilla pixel art 1:1 con relieve 2.5D, grano orgánico y evaluación en vivo de exactitud (meta ≥ 90% de la silueta objetivo con control de exceso fuera del molde).
  - **Guía adaptativa en la arena:** utiliza trazos cálidos sutiles en playas claras y marcadores oscuros estilo piedritas/tierra húmeda en mapas nocturnos y tormentosos (detección automática según luminancia del fondo).
- **Dos Tipos de Arena (Estrategia frente al Mar):**
  - **Arena Seca:** rápida y ancha para armar el cuerpo del castillo, pero vulnerable a la erosión de las olas.
  - **Arena Húmeda:** más compacta y precisa; funciona como **dique/escudo real** que frena el avance del agua hasta saturarse y desmoronarse.
  - *Atajos en PC:* **Espacio** alterna Seca/Húmeda y **Tab** abre/cierra el Plano Objetivo.
- **Marea Viva, Olas y Viento Costero (`TideController` + `TideWater.shader`):**
  - El avance del mar actúa como timer diegético con rompiente, espuma, rastro húmedo al retroceder y ráfagas climáticas dinámicas (`¡RÁFAGA!` / `¡HURACÁN!`).
- **Plano Objetivo y Tutorial Interactivo:**
  - Pergamino desplegable con la silueta del nivel y guía interactiva paso a paso en los primeros mapas.
- **Sistema de 3 Estrellas:** progresión desbloqueable por mundo basada en completar la silueta (≥ 90%), tiempo objetivo y prolijidad del trazo.

---

## 🌍 Gira Mundial, Mundos y Tienda Animada

- **8 niveles jugables** distribuidos en 2 destinos activos + 2 destinos anunciados (*Próximamente*):
  - **Playa Local (Base):** *Soleado*, *Atardecer*, *Noche Ventosa* y *Tormenta Suave*.
  - **Pack Brasil — Río Caliente (DLC):** *Sol Tropical*, *Ocaso Tropical*, *Noche de Ráfagas* y *Tormenta Tropical*.
  - **Próximamente:** *Japón — Playa Okinawa* e *Islandia — Playa Helada*.
- **Menú Principal y Créditos Diorama 60 FPS (`InteractiveMenuDiorama` + `MenuOceanUI.shader`):**
  - Escena viva con mar en perspectiva, paralaje con acelerómetro, nubes, gaviotas, sol y palmera interactivos al tacto (ondas en el agua, cocos cayendo y partículas de arena).
- **Selector de Mundos y Tienda Coleccionable (`WorldBackgroundAnimator`, `StoreCardAnimator`):**
  - Fondos y postales animados en GPU (`WorldBgLiveUI.shader` y `StoreCardLiveUI.shader`) con brillo foil, partículas por bioma recortadas con `RectMask2D`, animaciones de desbloqueo y acceso para testers.

---

## 🎵 Música, Ambiente Vivo y SFX

- **Música con Crossfade por Región (`MusicManager`):** transición suave entre `Island Bounce` (Menú y Playa Local) y un tema 16-bit estilo Samba/Bossa playero (`Brasil_Beach_Theme`) al explorar y jugar Brasil. El botón de mute controla exclusivamente la música.
- **Ambiente Dinámico de Mar y Viento (`AudioManager`):** loops estéreo de rompiente y viento costero que modulan volumen y tono en tiempo real según el avance de la marea, cada ola y las ráfagas climáticas, atenuándose automáticamente en Pausa y Fin de Nivel.
- **Biblioteca de SFX Pixel Art:** efectos dedicados para interacciones del diorama (palmera/coco, chapoteo, sol, gaviotas, castillo), navegación, cambio de balde, esculpido seco/húmedo, bloqueo de ola, pergamino, desbloqueo de packs, victoria, derrota y pop musical ascendente en cada estrella.

---

## 🛠️ Arquitectura y Pulido Android

- **Datos desacoplados en `ScriptableObject`:** `LevelData`, `WorldPackData` y `DLCData` para configurar niveles, peligros climáticos, paletas y packs sin tocar código.
- **Shaders Custom Pre-registrados:** `SandSculpt`, `TideWater`, `MenuOceanUI`, `WorldBgLiveUI`, `StoreCardLiveUI` y `TMP_Bitmap` incluidos en `GraphicsSettings` para builds Android sin materiales faltantes.
- **Responsive & Mobile Ready:**
  - `SafeAreaFitter` + `ResponsiveViewportCamera` para pantallas con notch y distintas relaciones de aspecto.
  - `Application.targetFrameRate = 60` y `Screen.sleepTimeout = SleepTimeout.NeverSleep`.
  - Soporte nativo del botón/gesto **Atrás** de Android (`KeyCode.Escape`) en menús, tienda y gameplay.
  - Configurado con **IL2CPP + ARMv7/ARM64** y orientación bloqueada a Portrait.

---

## 🔜 Próximos Pasos (Lanzamiento en Google Play)

- [ ] Integrar **Google Play Billing** (Unity IAP v5 / GPBL v8+) para la compra real del **Pack Brasil** (`sandcastle_dlc_brasil`) y restauración automática de compras.
- [ ] Configurar Adaptive Icon y publicación del `.aab` en Google Play Console (Prueba Interna → Producción).
- [ ] Futuras actualizaciones de contenido: **Pack Japón (Playa Okinawa)** y **Pack Islandia (Playa Helada)**.

---

Desarrollado por **Bruno Borgazzi** (Borgly).
