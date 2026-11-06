# CONTEXTO MAESTRO DEL PROYECTO — DON'T LOOK BACK

Desarrollo de un videojuego de terror 3D en primera persona en Unity.

> **Principio final:** un hospital abandonado, una presencia detrás del jugador y una regla que nunca debe romperse: no mires hacia atrás. Cada nivel, fantasma, documento, sonido, puerta, luz y objetivo debe contribuir a esa experiencia.

## 0. Estado técnico actual (verificado)

- Unity **6.5** (6000.5.10f1), **URP** (`com.unity.render-pipelines.universal` 17.5), **Input System nuevo** activo (`activeInputHandler: 1`). Usar `UnityEngine.InputSystem`, no `Input.GetKey`. No cambiar de pipeline.
- Claude Code está conectado al Editor con el **Unity MCP oficial** (`com.unity.ai.assistant`). No usar las herramientas `Unity_AssetGeneration_*` ni `Unity_PackageManager_ExecuteAction`.
- Ya existe un **prototipo temporal** hecho con cubos, creado solo para una demostración rápida a clase:
  - `Assets/Scripts/`: GameManager, PlayerController, PlayerInteract, FootstepsEvent, CodeManager, Note, Padlock, ProjectileLauncher, Projectile, DemoHUD, HeartsUI, Checkpoint, DamageZone.
  - `Assets/Editor/DemoSceneBuilder.cs` (menú "Don't Look Back > Crear escena demo"), y posiblemente `Assets/Scenes/Demo.unity`, `Assets/Prefabs/` y `Assets/Materials/` si se ejecutó el menú.
  - Estos archivos deben evaluarse en la inspección inicial (sección 17): pueden servir como base, reorganizarse o retirarse. **No borrarlos sin consultarme.**
- Restos de la plantilla de Unity: `Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity`, `Assets/InputSystem_Actions.inputactions`, `Assets/Settings/` (configuración de URP: **no tocar sin revisar**).
- Contexto académico: el proyecto es para una materia de desarrollo de videojuegos donde se vio creación de objetos (`Instantiate`), simulación de un cañón (tiro parabólico) y pérdida de vida. Conviene que el juego permita mostrar esos temas.

## 1. Identidad y visión general

Estoy desarrollando un videojuego de terror en primera persona llamado **DON'T LOOK BACK**, utilizando Unity.

Quiero crear una experiencia de terror ambientada en un hospital abandonado, con una atmósfera oscura, misteriosa, inquietante y psicológicamente perturbadora. El jugador deberá explorar diferentes zonas del hospital, resolver pequeños desafíos, encontrar objetos importantes y avanzar hasta conseguir escapar.

El juego estará inspirado en la sensación de persecución, vulnerabilidad y tensión de títulos como Outlast, pero NO quiero replicar su complejidad técnica ni desarrollar un proyecto de esa magnitud.

El objetivo es crear un videojuego de terror independiente de alcance moderado, bien diseñado, funcional, visualmente atractivo y suficientemente pulido para presentarlo y sustentarlo académicamente. La prioridad no es crear un juego enorme, sino uno pequeño, interesante, coherente, estable y bien terminado.

## 2. Tu papel como asistente de desarrollo

Actúa como un desarrollador experimentado de Unity, especializado en videojuegos 3D de terror, programación en C#, organización de proyectos, iluminación, diseño de niveles y optimización. Trabaja de forma metódica, ordenada y profesional.

No te limites a escribir código. Ayúdame a mantener el proyecto organizado, evitar errores innecesarios, detectar archivos que no sirven, diseñar sistemas reutilizables y asegurarte de que todas las mecánicas estén conectadas correctamente.

Reglas:

- Analiza primero el estado actual del proyecto antes de modificarlo.
- Identifica qué elementos existentes son útiles, cuáles están incompletos y cuáles parecen innecesarios.
- Elimina o limpia lo que claramente no aporte, pero solo después de verificar que no tenga dependencias importantes.
- No borres archivos importantes, escenas, configuraciones, paquetes o recursos sin comprobar su función.
- Si algo es dudoso, consúltame antes de eliminarlo.
- No reemplaces sistemas funcionales por otros nuevos sin una razón clara.
- Evita crear múltiples scripts que hagan prácticamente lo mismo.
- Mantén el código legible, modular, reutilizable y fácil de mantener.
- No desarrolles sistemas innecesariamente complejos.
- Comprueba las referencias entre componentes, los nombres de los objetos y las dependencias.
- Cuando modifiques algo, explica brevemente qué cambiaste, dónde y por qué.
- Prioriza soluciones que pueda comprender, probar y explicar durante una sustentación.

## 3. Concepto principal

La mecánica central es sencilla de entender, pero debe generar mucha tensión: **el jugador nunca debe mirar hacia atrás cuando escucha o percibe determinadas señales de que algo lo está siguiendo.**

El jugador está atrapado en un hospital abandonado. A medida que avance, escuchará pasos, respiraciones, golpes, sonidos de arrastre, puertas que se mueven y otros fenómenos extraños. En determinados momentos, una entidad aparecerá detrás del jugador o comenzará a perseguirlo. Cuando se active uno de estos momentos, el jugador deberá seguir mirando hacia delante y continuar avanzando o completar la tarea necesaria para escapar.

Si el jugador gira la cámara y mira hacia atrás cuando la amenaza está activa, la entidad lo atacará, ocurrirá un jumpscare y el nivel se reiniciará.

Esta mecánica debe ser la identidad del juego, no una penalización aleatoria. El jugador debe sentir constantemente la duda entre comprobar qué está detrás de él y obedecer la regla.

No todos los sonidos significan peligro inmediato: algunos crean tensión, otros anticipan una amenaza y otros engañan. Las señales deben permitir que el jugador aprenda las reglas y sienta que sus decisiones importan.

## 4. Estructura por niveles

El hospital está dividido en niveles. Cada uno representa una zona del edificio con su propio diseño, iluminación, atmósfera, objetivos y momentos de terror. La progresión va desde las partes inferiores del hospital hasta la salida.

1. **El sótano.** El jugador despierta en el sótano sin saber cómo llegó. Lugar oscuro, deteriorado, con sonidos extraños. Debe encontrar una llave, tarjeta de acceso, fusible u otro objeto sencillo y abrir la puerta hacia las escaleras. Enseña gradualmente movimiento, interacción, exploración y la regla de no mirar atrás.
2. **Pasillos de servicio.** Zona de mantenimiento con corredores estrechos, tuberías, almacenes y puertas bloqueadas. Los sonidos detrás del jugador son más frecuentes. Hay que encontrar un objeto o activar un mecanismo.
3. **Habitaciones y salas de hospitalización.** Habitaciones abandonadas, camas, cortinas, archivadores y pistas narrativas. Los fantasmas se manifiestan más, pero no todos los encuentros terminan en persecución.
4. **Urgencias y zona médica.** Camillas, instrumental, iluminación irregular y salas restringidas. Acertijos sencillos, documentos con historia y secuencias más intensas.
5. **La salida.** Se combinan las mecánicas aprendidas en un encuentro final. La salida no debe sentirse automática: debe haber un último objetivo o decisión que mantenga la tensión.

Es una propuesta inicial, no obligatoria: ajustar cantidad y alcance a los recursos y al tiempo. Cada nivel debe tener identidad visual y jugable propia.

## 5. Mecánica de no mirar hacia atrás (la más importante)

El sistema debe contemplar:

1. Detección de cuándo el jugador está en una zona o situación peligrosa.
2. Activación de la amenaza mediante eventos, disparadores o condiciones definidas.
3. Sonidos posicionales que den la impresión de que algo está detrás.
4. Aparición de la entidad en el momento apropiado.
5. Detección de cuándo el jugador gira suficientemente la cámara para mirar atrás durante una amenaza activa.
6. Jumpscare.
7. Muerte y reinicio del nivel actual.
8. Desactivación correcta cuando el encuentro termina o el jugador llega a una zona segura.

- La detección debe tener una **tolerancia configurable**: el jugador no debe morir por un movimiento mínimo, una vibración del ratón o una detección mal implementada.
- Definir con precisión qué es "mirar atrás", por ejemplo el ángulo horizontal de la cámara respecto a la dirección de avance con un umbral ajustable. Los encuentros especiales pueden tener condiciones distintas.
- La amenaza no está activa permanentemente: alternar exploración tranquila con momentos de reacción.
- Los encuentros deben configurarse (duración, intensidad, sonidos, posición del fantasma, condiciones de fin) sin reescribir la lógica.
- La muerte reinicia **solo el nivel actual**, no el juego completo.

## 6. Fantasmas y NPC

Los NPC son principalmente entidades sobrenaturales. No se necesitan humanos complejos, diálogos extensos, IA avanzada ni árboles de comportamiento.

Comportamientos sencillos posibles:

- Aparece al fondo de un pasillo y desaparece.
- Se aproxima lentamente por detrás durante una secuencia.
- Figura inmóvil en una habitación.
- Aparece en el jumpscare si el jugador mira atrás.
- Presencia que solo se manifiesta con sonidos y sombras.
- Aparece brevemente y desaparece cuando el jugador sigue avanzando.

Pueden usarse secuencias controladas, animaciones, posiciones predeterminadas, activadores y sistemas simples de aparición. Si hay persecuciones, deben ser simples, técnicamente predecibles y visualmente creíbles. Cada entidad se controla de forma independiente, sin afectar a las demás.

## 7. Exploración, objetos y objetivos

Objetivos posibles: llaves, tarjetas de acceso, fusibles, interruptores, restaurar parcialmente la electricidad, abrir puertas bloqueadas, códigos o pistas, examinar documentos, acertijos cortos, acceder a escalera, ascensor o salida.

- Acertijos sencillos y comprensibles; el juego no debe quedarse detenido.
- Los objetos interactivos deben tener una función real en la progresión.
- Sistema de interacción **reutilizable**: una misma estructura para recoger una llave, leer una carta, accionar un interruptor o abrir una puerta, con comportamiento específico por objeto.
- Mensajes de interacción discretos y claros.

## 8. Cartas, documentos y narrativa ambiental

Cartas, notas médicas, expedientes, informes y mensajes cuentan la historia de forma fragmentada, sin cinemáticas largas. Deben ser inquietantes y hablar del pasado del hospital, pacientes, personal, desapariciones, experimentos o sucesos extraños. Algunos explican, otros generan dudas o anticipan peligros.

Ejemplos de tono (no definitivos):

- Un expediente de un paciente que oficialmente nunca ingresó.
- Una nota de un enfermero que insiste en no mirar cuando se escuchan pasos detrás.
- Un informe sobre una habitación que debe permanecer cerrada.
- Una carta que afirma que el hospital sigue teniendo pacientes.
- Un registro de seguridad con una hora que se repite en varios documentos.
- Una advertencia escrita apresuradamente por alguien que intentó escapar.

Los documentos se recogen y leen con una interfaz sencilla. Historia coherente, misteriosa y no excesivamente complicada.

## 9. Atmósfera, sonido e iluminación

El hospital debe transmitir abandono, deterioro, aislamiento y peligro, apoyándose en: iluminación tenue y controlada, sombras, luces defectuosas o intermitentes, pasillos estrechos, siluetas y objetos deteriorados, sonido ambiental bajo, pasos, respiraciones, golpes, silencios intencionales, sonido posicional y jumpscares puntuales no repetitivos.

- Iluminación atractiva pero **estable y fiable**: el juego debe funcionar sin problemas en la sustentación. Evitar configuraciones costosas o efectos que causen errores visuales.
- Trabajar con el pipeline existente (URP). No cambiar a Built-in o HDRP sin una razón acordada.
- Cuidar materiales oscuros, luces, sombras, reflejos, rendimiento y legibilidad. El terror depende de la atmósfera, no de una pantalla completamente negra.

## 10. Jumpscares, muerte y reinicio

Secuencia posible: aparición repentina del fantasma, movimiento brusco hacia la cámara, sonido fuerte sincronizado, animación o transición corta, efecto de muerte y reinicio del nivel.

- Sincronizados con la amenaza; nunca repetidos cada fotograma ni bloqueados por errores de estado.
- Impedir múltiples reinicios simultáneos.
- El reinicio debe restaurar objetos recogidos, puertas, interruptores, fantasmas y estados temporales, sin referencias destruidas ni duplicados.
- Pantalla de muerte o transición breve, sencilla y coherente.

## 11. Regla fundamental: los assets los proporciono yo

**No descargues, importes ni instales assets externos por tu cuenta.** Tampoco incorpores modelos, texturas, paquetes, personajes, sonidos, escenas de demostración u otros recursos sin mi autorización, ni añadas dependencias que no haya aprobado.

Cuando haga falta un recurso:

1. Revisa si ya hay uno adecuado en el proyecto.
2. Si no, indícame exactamente qué hace falta.
3. Explica para qué se usará.
4. Espera a que lo proporcione.
5. Prepara el código para usarlo cuando esté disponible.

Mientras tanto se pueden usar objetos temporales simples de Unity para probar mecánicas, **claramente identificados como temporales** y fáciles de reemplazar.

## 12. Organización estricta del proyecto

Estructura de carpetas propuesta (adaptar a lo existente, sin crear carpetas vacías innecesarias ni mover recursos indiscriminadamente):

```
Assets/
  _Project/
    Art/        Models/ Materials/ Textures/ Animations/
    Audio/      Ambience/ Music/ SFX/ Voices/
    Prefabs/    Characters/ Environment/ Interactables/ Gameplay/ UI/
    Scenes/
    Scripts/    Core/ Player/ Interaction/ Horror/ Enemies/ Objectives/ UI/
    UI/         Fonts/ Sprites/
    Data/
    Settings/
```

- Si ya hay una estructura razonable, consérvala y mejórala.
- Nombres claros y consistentes; se pueden usar nombres en inglés para la convención técnica.
- Nada de `New GameObject`, `Cube (3)`, `Script1` en la versión final.

Jerarquía de referencia de una escena (ejemplo, no crear ahora; adaptar a lo que la escena necesite):

```
Level01_Basement
  _Systems      GameManager, LevelManager, AudioManager
  Environment   Architecture, Floors, Walls, Ceilings, Props, Lighting
  Player        PlayerCamera, InteractionOrigin
  Gameplay      Interactables, Doors, Objectives, Triggers
  Horror        Ghosts, EncounterTriggers, JumpscarePoints
  Audio         AmbientSources, SpatialSFX
  UI            HUD, InteractionPrompts, Documents
  SpawnPoints
  Debug
```

- No crear objetos vacíos por costumbre.
- Agrupar por función: entorno, sistemas, interactivos, amenazas, iluminación e interfaz separados.
- Evitar jerarquías demasiado profundas y nombres ambiguos.
- Usar prefabs cuando haya una razón clara de reutilización.

## 13. Arquitectura de programación

Sencilla, modular y reutilizable; sin arquitectura empresarial ni patrones innecesarios.

Sistemas probables: controlador del jugador en primera persona, cámara, interacción, objetos recogibles, puertas y cerraduras, objetivos y progreso del nivel, activación de encuentros, detección de mirada atrás, controlador de fantasmas, jumpscares, muerte y reinicio, notas y documentos, audio básico, interfaz, estados y transiciones del nivel.

- No tienen que ser scripts separados si separarlos no aporta. Evitar scripts gigantes y fragmentación excesiva.
- Un cambio en una mecánica no debe romper otras.
- Preferir referencias serializadas a búsquedas globales por nombre.
- Valores ajustables expuestos en el Inspector cuando tenga sentido.
- No agregar guardado, inventarios complejos, multijugador, diálogos avanzados, árboles de habilidades ni IA sofisticada.

## 14. Calidad y rendimiento

Prioridades: rendimiento estable, sin errores críticos, escenas mantenibles, iluminación consistente, transiciones correctas entre niveles, interacciones fiables, reinicios sin errores, audio sincronizado, detección justa de la mirada, referencias asignadas, sin dependencias innecesarias, experiencia comprensible.

- Evitar sistemas costosos si una solución sencilla logra lo mismo.
- Un sistema no está terminado solo porque compila: hay que comprobarlo en Unity.
- No afirmar que algo funciona si no se ha verificado.
- Preparar una lista corta de pruebas para cada sistema importante.

## 15. Forma de trabajo

Por etapas, con cambios controlados y fáciles de revisar. No desarrollar todo de una vez. Cada etapa tiene objetivo claro, implementación limitada y criterio de terminado.

Antes de cambios importantes: inspeccionar archivos afectados, identificar dependencias, explicar el cambio previsto, conservar lo que funciona y no tocar lo que no esté relacionado.

Después de cada cambio: comprobar errores de compilación (consola de Unity), revisar referencias y dependencias, verificar compatibilidad con la configuración existente, indicar qué debo probar manualmente en Unity y documentar lo que deba configurarse en el Inspector.

Si aparece una limitación o decisión que cambie el alcance, explicarla antes de implementarla.

## 16. Alcance realista

Una experiencia de terror 3D compacta con: un concepto reconocible, una mecánica principal memorable, varios niveles con ambientes diferentes, fantasmas interesantes, exploración y objetivos sencillos, notas que desarrollen la historia, sonido e iluminación bien trabajados, jumpscares bien implementados, un final satisfactorio y una estructura técnica organizada.

Es preferible terminar tres o cuatro niveles bien diseñados que diez vacíos. El contenido se ajusta a los assets disponibles y al tiempo.

## 17. Etapa actual: inspeccionar y preparar, NO desarrollar

> Actualizar esta sección cuando cambie la etapa.

Por ahora **NO** crees escenas, **NO** construyas niveles, **NO** programes mecánicas y **NO** importes recursos. Realiza solo:

1. **Inspeccionar** la estructura de carpetas, escenas, scripts, paquetes instalados, configuración de renderizado y recursos disponibles.
2. **Evaluar** y clasificar los elementos en: útiles y necesarios; potencialmente reutilizables; incompletos o pendientes; claramente innecesarios; dudosos (requieren mi autorización antes de eliminarlos).
3. **Limpiar con cuidado**: eliminar solo lo confirmado como innecesario y seguro. Si hay duda, conservar y consultar. No eliminar configuraciones esenciales, archivos de proyecto, paquetes necesarios, archivos `.meta` ni recursos de función desconocida. Si hay control de versiones, revisar su estado antes de modificar.
4. **Proponer la organización definitiva** de carpetas y jerarquía (solo propuesta, sin crear escenas ni objetos).
5. **Identificar los recursos necesarios**, separando imprescindibles y opcionales. No descargarlos.
6. **Entregar un informe inicial**: qué encontraste, qué limpiaste, qué conservaste, qué problemas detectaste y cuál es el siguiente paso.

No afirmes haber revisado o eliminado nada que no hayas comprobado. **Detente al terminar esta etapa** y espera instrucciones antes de programar o construir escenas.

## 18. Principio final

Un hospital abandonado, una presencia detrás del jugador y una regla que nunca debe romperse: no mires hacia atrás. No necesitamos la mayor cantidad de contenido ni la tecnología más compleja. Necesitamos buenas decisiones, una identidad clara, una ejecución cuidadosa y un proyecto que podamos entender, mantener y presentar con confianza.
