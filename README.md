# NVIDIA 15 kHz Super Resolución (240p / 224p) para Windows 10 y Windows 11

[![Windows 10 / 11](https://img.shields.io/badge/Windows-10%20%2F%2011%20(64--bit)-0078D6?logo=windows)](https://github.com/Tupapa2712)
[![NVIDIA Turing](https://img.shields.io/badge/Probado%20en-NVIDIA%20GTX%201650%20LP-76B900?logo=nvidia)](https://github.com/Tupapa2712)
[![Modos 15kHz](https://img.shields.io/badge/Modos-2560x240p%20%7C%202560x224p%20%40%2060Hz-orange)](https://github.com/Tupapa2712)

Herramienta e instaladores automáticos para habilitar **super resoluciones progresivas reales de 15 kHz (`2560×240 @ 60 Hz` y `2560×224 @ 60 Hz`)** en televisores y monitores **CRT** usando una tarjeta gráfica **NVIDIA** en **Windows 10 y Windows 11 (64 bits)**.

> **Alcance del proyecto:** Este repositorio y sus instaladores incluyen **únicamente** los controladores/modos de resolución `240p`/`224p` y la utilidad `Crt240pSwitch`. **No incluye** reproductores de video (MPC-HC, VLC) ni emuladores/frontends (RetroBat, RetroArch); cada usuario configura libremente sus propios reproductores y emuladores.

---

## ¿Qué hace esto y por qué usa "Super Resoluciones" (`2560×240p` / `2560×224p`)?

Los televisores de tubo (CRT) clásicos esperan una señal analógica de **15.7 kHz** (aproximadamente 240 o 224 líneas verticales a 60 Hz). Sin embargo, las tarjetas gráficas modernas como la **NVIDIA GTX 1650** tienen un límite mínimo de velocidad en su salida HDMI (llamado *pixel clock mínimo*, alrededor de 25 MHz):

- **Analogía sencilla:** Imagina una autopista donde los autos no tienen permitido ir más lento de 25 km/h. Una resolución clásica como `320×240` es demasiado "lenta" (~6 MHz) y la salida HDMI de la tarjeta la rechaza.
- **La solución (Super Resolución Horizontal):** Multiplicamos solo el ancho horizontal por 8 (`320 × 8 = 2560`), manteniendo intactas las **240** o **224 líneas verticales reales**. Así la señal alcanza **~51 MHz** (pasa sin problemas por HDMI), pero el televisor CRT sigue recibiendo exactamente **15.72 kHz horizontales y 60 Hz verticales**, mostrando *scanlines* 100% reales y nítidas.

### Limitación importante en Windows 10 / 11 y NVIDIA Turing
- **Funciona de forma nativa y estable:** Modos progresivos **`2560×240 @ 60 Hz`** (PS1, NES, N64, Saturn, video 240p) y **`2560×224 @ 60 Hz`** (SNES, Sega Mega Drive/Genesis, Neo Geo, Arcade CPS1/2/3).
- **No soportado por HDMI físico en Turing / WDDM:** El modo **entrelazado `480i`** es bloqueado directamente por el controlador WDDM / NVAPI de NVIDIA en arquitectura Turing sobre Windows 10 y Windows 11. Este proyecto se centra exclusivamente en **240p y 224p progresivos reales**.

---

## Los 2 Instaladores Disponibles (Windows 10 y Windows 11 — 64 bits)

Ambos instaladores son **100% autónomos y guiados** (*Aceptar aviso → Siguiente → elegir/confirmar monitor CRT → Siguiente → esperar la barra de progreso → Listo*), sin necesidad de escribir comandos ni editar el Registro manualmente.

| Instalador | ¿Para quién es? | Comportamiento |
| :--- | :--- | :--- |
| **`Setup_GTX1650_15kHz_240p_224p_Win10_Win11.exe`** | Usuarios con **NVIDIA GeForce GTX 1650** (arquitectura Turing) | **Perfil Probado.** Verifica si tu GPU es una GTX 1650 (y te avisa con claridad si detecta otro modelo). |
| **`Setup_NVIDIA_Experimental_15kHz_240p_224p_Win10_Win11.exe`** | Usuarios que desean **experimentar en otras tarjetas NVIDIA** | **Perfil Experimental.** Permite probar los modos `240p`/`224p` en otras GPUs NVIDIA bajo tu propia responsabilidad, sin garantía de compatibilidad. |

> Descarga el instalador correspondiente desde la sección **[Releases](../../releases)** de este repositorio (o desde la carpeta `Instaladores\` si compilas localmente).

---

## Instalación Paso a Paso (Sin Conocimientos Técnicos)

1. Conecta tu televisor CRT a la salida HDMI de tu tarjeta NVIDIA mediante tu adaptador HDMI-a-VGA y transcoder YPbPr, y asegúrate de tener Windows en modo **Extender estas pantallas** (con tu monitor moderno como **Pantalla Principal**).
2. Ejecuta el instalador correspondiente (te pedirá permisos de **Administrador / UAC** para poder respaldar y configurar el EDID del CRT de forma segura).
3. **Lee y acepta** el aviso legal y descargo de responsabilidad.
4. En la pantalla de **Detección Automática de Monitores**, el asistente listará todas las pantallas conectadas a tu tarjeta gráfica:
   - Tu monitor principal aparecerá marcado como **`[PRINCIPAL - PROTEGIDO / HARD-LOCK]`** y **no podrá ser modificado**.
   - Tu adaptador CRT secundario quedará preseleccionado de forma segura.
5. Pulsa **Siguiente → Instalar** y espera a que termine la barra de progreso. El instalador:
   - Guarda una copia de seguridad automática del estado original de tu pantalla y de su EDID.
   - Instala `Crt240pSwitch` y aplica los modos **`2560×240 @ 60 Hz`** y **`2560×224 @ 60 Hz`** únicamente sobre tu pantalla CRT.
   - Si ocurre **cualquier fallo**, revierte automáticamente todos los cambios al estado original sin romper tu configuración y guarda un registro de diagnóstico en `%APPDATA%\Crt240pSwitch\installer.log`.
6. ¡Listo! Pulsa **Finalizar**. Desde **Crt240pSwitch** podrás alternar entre `240p` y `224p` en un clic o usar el acceso directo **"Restaurar EDID Original (1 Clic)"** cuando quieras volver atrás.

---

## Hardware y Adaptadores Exactos Probados

Esta configuración ha sido probada y validada en hardware real con la siguiente cadena de señal:

1. **Tarjeta Gráfica (GPU):** **NVIDIA GeForce GTX 1650 Low Profile** (arquitectura Turing).
2. **Conversor HDMI a VGA:** Adaptador activo con chip **`MACROSILICON`** (identificado en Windows como **`HJW9291` / `MACROSILICON`** — realiza conversión directa sin reescalado interno).
3. **Transcoder de Señal:** **`BitFunx VGA/SCART-to-YPbPr`** (convierte RGBHV de VGA a video por componentes YPbPr de 15 kHz).
4. **Pantalla CRT:** **`Sony Trinitron WEGA KV-29FA540`** (29 pulgadas, entrada por componentes YPbPr a 15 kHz).

> **Nota:** El software **no** tiene ningún identificador de pantalla ni ruta personal quemada en el código; detecta dinámicamente el monitor y adaptador que estén conectados en cada PC.

---

## Seguridad, Hard-Lock y Reversión Automática

- **Protección Hard-Lock del Monitor Principal:** Tanto el instalador como `Crt240pSwitch` bloquean cualquier intento de modificar tu monitor principal.
- **Respaldo y Rollback Automático ante Fallos:** Antes de tocar `EDID_OVERRIDE` o cambiar resoluciones, se guarda un respaldo automático (`backup\edid_backup_*.bin` / `.reg` y `pre_change_state.json`). Si cualquier paso falla, el sistema revierte automáticamente el EDID y la resolución previa.
- **Compatibilidad Windows 10 y Windows 11:** Si el reinicio del nodo PnP del monitor varía entre versiones de Windows 10/11, `Crt240pSwitch` usa una cadena automática de respaldo (`SetupAPI` → `pnputil /restart-device` → `pnputil /disable-device` + `/enable-device`).
- **Logs de diagnóstico:** Disponibles en `%APPDATA%\Crt240pSwitch\installer.log` y `%APPDATA%\Crt240pSwitch\crt240p.log`.

---

## Estructura del Repositorio

```text
├── src/
│   └── Crt240pSwitch/              # Código fuente C# (.NET 9 WinForms + NVAPI + EDID_OVERRIDE)
├── installer_scripts/
│   ├── AVISO_LEGAL_Y_CREDITOS.txt  # Aviso legal y descargo de responsabilidad del asistente
│   ├── Common_Monitor_Logic.iss    # Detección de GPU/monitores, Hard-Lock y rollback automático
│   ├── Setup_GTX1650_15kHz_240p_224p.iss
│   ├── Setup_NVIDIA_Experimental_15kHz_240p_224p.iss
│   └── Build_All_Installers.bat    # Script para compilar ambos instaladores con Inno Setup 6
├── .gitignore
└── README.md
```

---

## Créditos y Descargo de Responsabilidad

### Créditos
- **Autor / Desarrollador:** **Tupapa2712 / DJ Kendo**
- **Asistencia de desarrollo e ingeniería:** Este proyecto fue construido con la ayuda de **Gemini**.

### Descargo de Responsabilidad Total
> **IMPORTANTE — LEER ANTES DE USAR:**
>
> Este software se proporciona **"TAL CUAL" ("AS IS")**, sin garantías de ningún tipo, expresas ni implícitas. El uso de frecuencias horizontales de 15 kHz, super resoluciones personalizadas (`2560×240p` / `2560×224p`), inyección NVAPI y modificaciones `EDID_OVERRIDE` se realiza **exclusivamente bajo el propio riesgo y responsabilidad del usuario**.
>
> El autor y desarrollador (**Tupapa2712 / DJ Kendo**) **se absuelve totalmente y no asume responsabilidad alguna** por cualquier daño directo, indirecto, pérdida de configuración, fallo de sincronía, sobrecalentamiento o avería en tarjetas gráficas (GPU), adaptadores, transcoders, monitores LCD/OLED o televisores/monitores CRT utilizados por el usuario.
