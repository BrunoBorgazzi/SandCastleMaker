# SandCastleMaker 🏖️

**SandCastleMaker** es un juego en 2D desarrollado en Unity donde el objetivo principal es construir castillos y estructuras de arena utilizando físicas y gravedad. Actualmente el proyecto se encuentra en su versión **MVP (Minimum Viable Product)**, con el núcleo (core loop) jugable completamente implementado.

## 🎮 Mecánicas Principales

- **Spawneo de Arena:** Los jugadores pueden colocar arena dinámicamente haciendo clic/tap en la pantalla.
- **Dos Tipos de Arena:**
  - **Arena Seca:** Simulada con físicas estándar de Unity, tiende a desmoronarse. Optimizada con un sistema de *Object Pooling* para garantizar un alto rendimiento.
  - **Arena Húmeda:** Tiene mayor fricción y es ideal para construir bases sólidas. Cada nivel cuenta con una cantidad limitada (recurso estratégico).
  - *Atajo en PC:* Pulsando la **Barra Espaciadora** se puede alternar rápidamente entre arena seca y húmeda.
- **Manipulación de Gravedad (`GyroGravity`):** La gravedad del entorno puede ser manipulada, permitiendo que las estructuras reaccionen de formas creativas.
- **Zonas de Interacción:** 
  - **Goal Area:** Áreas designadas que la arena del jugador debe tocar para superar el nivel.
  - **Kill Zone:** Límites del mapa; si demasiada arena cae ahí, se pierde.

## 🛠️ Arquitectura y Sistemas Técnicos

El proyecto está diseñado pensando en la escalabilidad y el rendimiento:

- **Managers (Patrón Singleton):** Sistemas como `LevelManager`, `AudioManager` y `MusicManager` son globales y persisten entre escenas (`DontDestroyOnLoad`), centralizando la lógica del juego.
- **Object Pooling (`SandPoolManager`):** Reutiliza los objetos físicos de la arena en lugar de instanciarlos y destruirlos constantemente, ahorrando recursos valiosos de CPU y memoria.
- **Scriptable Objects (`LevelData`):** Los niveles están configurados como datos independientes, permitiendo a los diseñadores editar los requisitos de victoria y cantidad de arena húmeda sin tocar código.
- **UI Responsiva:** Sistema de menú principal, selección de niveles y un sistema de preferencias de usuario persistentes (`PlayerPrefs`) para la música.

## 🚀 Estado Actual (MVP)

- Loop de juego completo: Menú -> Selección de Nivel -> Gameplay -> Victoria/Derrota.
- Sonidos y música implementados y unificados, con sistema de silenciado en el inicio.
- Múltiples niveles configurados.

## 🔜 Próximos Pasos (Roadmap)

- [ ] Implementación de **Shaders** personalizados para darle a la arena un aspecto visual más fluido y realista.
- [ ] Sistema de **Partículas (VFX)** para reaccionar al colocar bloques o al contacto.
- [ ] Testeo exhaustivo (QA) y balance de la curva de dificultad en los niveles.
