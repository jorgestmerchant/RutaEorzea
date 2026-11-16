# Ruta Eorzea — experimental 0.11.0

Guía visual para FFXIV con Dalamud. Utiliza vnavmesh para calcular caminos. No mueve al personaje. Consulta INSTALAR.txt para instalar el paquete en otro equipo.

## Uso

1. Marca la **casilla a la derecha del título de una misión**, en la lista de la derecha o junto a la misión principal en la esquina superior izquierda. Aparece una marca verde en la misión seguida.
2. Verás una línea verde en el minimapa y en el mapa grande (M), además de una flecha con la distancia por el camino. La línea sobre el terreno viene apagada. Si la misión está en otra zona conectada a pie, la guía te lleva primero a la salida correspondiente.
3. Cierra el mapa y camina siguiendo la guía.

Pulsa de nuevo la casilla para detener la ruta, o marca otra misión para cambiar de destino. Solo una misión guía la ruta a la vez. No hace falta abrir el mapa. Las casillas acompañan la posición actual de la lista de misiones y se ocultan cuando otra ventana del juego cubre el título. Puedes ocultarlas en `/ruta` → «Casillas a la derecha de cada misión».

Sigue disponible el método anterior: pulsar el texto del objetivo debajo del título para abrir el mapa y activar la ruta.

## Rutas personalizadas desde el mapa

Abre el mapa con **M**, haz **clic derecho** sobre un punto o icono y elige **«Crear ruta hasta aquí»**. Se reemplaza la ruta anterior. La línea y la flecha guían hasta el destino elegido; el personaje se mueve manualmente. Al pulsar un icono cercano se intenta utilizar su posición exacta y su nombre.

Para volver a una misión, marca su casilla. Para detener la ruta personalizada escribe `/ruta parar`. Las rutas personalizadas no se guardan al recargar el plugin. Las misiones marcadas sí se guardan.

El clic derecho se puede desactivar en `/ruta` → «Crear rutas con clic derecho en el mapa». **Ctrl + clic derecho** conserva la bandera nativa. El mapa general de regiones no sirve como destino; abre el mapa detallado de la zona. Si hay varios pisos superpuestos, el punto del mapa puede no indicar el piso deseado. Si no hay suelo en el punto, se intenta un punto transitable cercano; puede no existir un camino completo.

La casilla de la misión principal también funciona cuando el panel muestra la **siguiente** misión aún sin aceptar: en ese caso guía hasta el PNJ que la inicia. Los requisitos para aceptarla siguen siendo los del juego.

## Niveles de misión y colores

El nivel base de cada misión aparece entre paréntesis integrado en su título, con fuente del juego y contorno oscuro para resaltar. En las secundarias se antepone al título; en el panel de la principal aparece después del nombre, dentro de su barra.

- **Verde:** desde 5 niveles por debajo hasta 2 por encima del nivel actual de tu personaje.
- **Naranja:** 3 o 4 niveles por encima.
- **Rojo:** 5 o más niveles por encima.
- **Gris:** 6 o más niveles por debajo.

Los colores cambian según el nivel de la clase que estás usando. Puedes ocultarlos en `/ruta` → «Nivel de misión con color según tu nivel». Comparan niveles; no indican una recompensa de EXP ni comprueban todos los requisitos para aceptar una misión. Se muestra el nivel base de la misión, no una promesa sobre la dificultad de cada combate.

## Guía visual y misiones con varios objetivos

### Ficha de recompensas

Pasa el cursor sobre el título, nivel o casilla de una misión principal o secundaria. La ficha muestra EXP base aproximada, gil, objetos garantizados y recompensas a elegir, con iconos, cantidades y HQ, además de desbloqueos disponibles. «Elige UNA recompensa» significa que no recibes todas las opciones. Se puede desactivar desde los ajustes.

La EXP excluye bonificaciones y puede cambiar por sincronización. Las recompensas especiales según clase o reputación se indican para consultar el diario. Los nombres de los objetos proceden del idioma del cliente; no se traducen mediante Gubal desde este plugin.

La guía tiene un medallón con aguja dorada en relieve, marco oscuro con borde dorado y fuentes del juego (Axis y Jupiter). Muestra la misión, la distancia del tramo y una indicación breve. La aguja gira suavemente con la cámara y sigue indicando la dirección aunque el siguiente punto quede detrás de ella. Arrastra el panel para cambiarlo de sitio.

Desde la versión 0.10, la orientación predeterminada usa exactamente el siguiente tramo del rastro del minimapa, con su misma proyección y giro. La aguja apunta a la siguiente curva; no se adelanta a puntos situados después de ella. Si te separas del camino, se dibuja un tramo de regreso y la aguja lo sigue. Puedes desactivar «Flecha en la dirección del rastro del minimapa» para volver al modo de cámara o personaje. Cuando el minimapa no está disponible, se utiliza ese modo alternativo.

Misiones como «Incienso y sensibilidad» tienen varios objetivos a la vez. Al marcar la misión se selecciona automáticamente el más cercano de la zona actual, sin abrir los ajustes. Cuando el juego cambia o retira sus marcadores, la guía actualiza el destino. Si prefieres otro, puedes elegirlo desde los ajustes. Si no hay un marcador disponible, se muestra un estado de espera.

## Ajustes rápidos y misión guardada

Pulsa el **engranaje flotante junto al minimapa** para abrir o cerrar los ajustes sin escribir comandos. Su posición inicial está abajo a la izquierda del círculo. Se puede arrastrar alrededor del minimapa; se guarda el ángulo elegido. La casilla «Botón flotante de ajustes en el minimapa» permite ocultarlo; `/ruta` sigue abriendo los ajustes.

La misión marcada y el objetivo elegido se guardan. Al recargar el plugin se busca esa misión entre las activas y se recupera la guía. Si no aparece, se pide marcar otra misión. Durante la preparación de una zona se mantiene visible el destino y un mensaje de estado, en vez de dejar la guía vacía. Al llegar se muestra el mensaje de llegada hasta detener o cambiar el seguimiento.

## Misiones de otra zona

Marca su casilla de la misma manera. El recorrido se divide en tramos:

1. La flecha indica «Salida hacia…» y la distancia hasta la salida.
2. Camina hasta ella y cruza el límite de zona. Cerca de la salida se muestra «Cruza la salida…».
3. Al cargar el mapa y quedar listo vnavmesh, se calcula el tramo siguiente, conservando el destino final de la misión.

Si hay varias zonas intermedias, el proceso se repite. La distancia mostrada corresponde **al tramo actual**, no a todo el viaje. Se prioriza un menor número de cambios de zona y después una salida cercana; no garantiza el trayecto total más corto.

También puedes abrir `/ruta` → «Guiar hasta una zona vecina» para pedir una guía hasta otra zona sin seleccionar una misión. Este modo termina al entrar en la zona elegida.

**La ruta de prueba a Nueva Gridania utilizaba este modo de viaje a una zona**: terminaba al entrar, porque ese era el destino. Para continuar hasta un PNJ u objetivo después de cruzar, marca la misión correspondiente. Los viajes manuales a una zona no se guardan como misión seguida.

También puedes colocar una bandera con **Ctrl + clic derecho** en el mapa. La guía detecta las banderas nuevas; este modo está implementado pero no se verificó colocando una bandera durante la prueba en el juego.

| Comando en el chat del juego | Acción |
| --- | --- |
| `/ruta` | Abre o cierra los ajustes. |
| `/ruta mapa` | Vuelve a leer la misión del mapa abierto. |
| `/ruta bandera` | Selecciona la bandera existente. |
| `/ruta parar` | Borra la ruta actual. |

En los ajustes puedes apagar por separado la línea del terreno, la del minimapa, la del mapa grande (M) y la flecha, además de cambiar color y grosor.

## Límites

- Las conexiones entre zonas se obtienen de los marcadores normales de salida a pie del juego. Barcos, teletransportes, PNJ y entradas especiales requieren intervención manual; no todas las zonas están conectadas de esta forma. Tampoco comprueba todos los requisitos de desbloqueo de las salidas.
- Las áreas de búsqueda usan un destino aproximado. Para varios destinos se prioriza la zona actual y la distancia directa al personaje; el trayecto transitable se calcula después. Esta elección no garantiza el objetivo de menor distancia por camino.
- El minimapa utiliza la escala, el zoom y el giro nativos del juego. No necesita iconos cercanos para mantener la línea visible, incluso en modo giratorio. Se oculta durante la carga o cuando el minimapa no está disponible.
- El mapa grande muestra el tramo calculado en tu mapa actual. Al consultar otra zona o un mapa general, esa línea se oculta; no dibuja el viaje completo a través de todas las zonas.
- No todas las misiones proporcionan destinos compatibles. No calcula vuelo ni rutas por teletransportes. Si no encuentra suelo transitable cerca de una salida o una conexión conocida, muestra un mensaje en la guía.
- La ruta sobre el terreno es una superposición: puede verse encima de personajes u objetos.
- Es una versión experimental. Las actualizaciones del juego, Dalamud o vnavmesh pueden requerir cambios.

## Verificación realizada

- Carga en tu sesión de Steam Free Trial con Dalamud API 15 y vnavmesh 1.2.3.15; motor de caminos en estado Ready.
- Selección automática de «Por la amistad», traducida por Gubal: camino de unos 13 m, flecha y línea del minimapa visibles.
- Cambio del zoom del minimapa y restauración de su escala inicial.
- Detección de misiones cuyo destino está en otra zona.
- Casillas en las cinco misiones visibles: activar ruta de «Por la amistad» (unos 24 m en la prueba de esta versión), desmarcar para detener y cambiar entre misiones, incluyendo una de otra zona.
- Versión 0.3: cálculo en el juego de un tramo de unos 101 m desde Vieja Gridania hasta la salida hacia Nueva Gridania, con flecha y ruta sobre el terreno.
- Versión 0.4: engranaje visible junto al minimapa y apertura de ajustes con un clic. «La senda del conjurador» conservó la casilla marcada y recuperó su ruta de unos 27 m después de recargar la DLL.
- Versión 0.5: «Incienso y sensibilidad» seleccionó automáticamente uno de sus dos incensarios sin abrir ajustes, con una ruta de unos 31 m. Se revisaron en el juego el marco dorado, la aguja con relieve y las fuentes nativas. Se contrastó la dirección detrás de la cámara con la proyección del objetivo.
- Versión 0.6: se reemplazó el cálculo por ángulo de cámara, que daba orientaciones incorrectas, por la proyección real del tramo dibujado. Se comprobaron cámaras desde el sur, desde el este y desde arriba, puntos detrás de la cámara, curvas y regreso al camino.
- Versión 0.7: línea en el mapa grande (M), con zoom y recorte al marco. Verificada visualmente en Vieja Gridania a varios niveles de zoom; el arrastre automático no permitió verificar el desplazamiento manual. La proyección utiliza el desplazamiento nativo del mapa.
- Versión 0.8: casilla de la misión principal verificada con «Hacia el Bannock», con un tramo de unos 94 m hacia Vieja Gridania. Menú de clic derecho verificado en el Bosque del Norte: punto personalizado de unos 62 m e icono de «La nariz lo sabe» de unos 87 m. No se recorrieron esos caminos completos. Se añadieron comprobaciones de inversión de coordenadas con distintos zooms, desplazamientos, escalas y offsets; 57 comprobaciones aprobadas.
- Versión 0.8.1: se eliminó la calibración que dependía de iconos cercanos y ocultaba la línea al moverse. Se leen la escala y el giro nativos y la posición del jugador usada por el renderizador del minimapa. La proyección se contrastó con coordenadas reales de un minimapa giratorio en el Bosque del Norte y se comprobaron cambios de zoom, escala de interfaz, giro y datos inválidos. No se recorrió manualmente una ruta completa en esta revisión.
- Versión 0.9: nivel entre paréntesis integrado junto al nombre real de cada título, sin recuadro flotante. Se comprobó en el juego la principal «Dar la talla» (4) y las secundarias de niveles 3, 4 y 5, en verde con el personaje de nivel 7. Se probaron los límites de los cuatro colores y el cambio al subir de nivel; 71 comprobaciones aprobadas.
- Versión 0.10: flecha alineada con la línea del minimapa, con pruebas de giros, curvas, pendientes y regreso al camino; 79 comprobaciones aprobadas. Se observó alineada durante un tramo de unos 22 m en «Bébete las judías, cariño». Los objetivos de misión se ajustan a suelo transitable cercano antes de calcular. Se añade icono de brújula, README y flujo de publicación en GitHub; su publicación remota requiere un repositorio accesible.
- Versión 0.11: ficha de recompensas con datos locales y cálculo de EXP base aproximada. Se contrastaron datos de misiones de niveles 4 y 5, objetos garantizados, opciones, cristales y HQ. Se añadieron pruebas de EXP, redondeo, desbordamiento y comparación con un nivel completo; 88 comprobaciones aprobadas antes de publicar.
- Compilación sin errores ni advertencias; comprobaciones de matemáticas, proyección, selección de salidas, selección de objetivos y orientación de la flecha aprobadas, incluyendo la coincidencia de la aguja con el tramo dibujado y la proyección del mapa grande.

No se verificaron recorridos completos, el cruce real de zona seguido hasta otro objetivo de misión, todas las zonas, todos los tipos de misión ni el modo de minimapa rotatorio durante esta sesión.

## Archivos y mantenimiento

La DLL que está cargada se encuentra en `bin/Release/RutaEorzea.dll`, junto al manifiesto `RutaEorzea.json`. Conserva esa carpeta mientras siga configurada en Dalamud. El código fuente y las pruebas están en esta carpeta.

Para compilar con .NET SDK 10 y referencias locales de Dalamud:

```text
dotnet build RutaEorzea.csproj -c Release -p:DalamudPath="RUTA_A_LAS_DLL_DE_DALAMUD"
dotnet run --project tests/RouteMath.Tests.csproj -c Release
```

El botón «Guardar diagnóstico local» escribe únicamente información de esta guía y del mapa del juego en la carpeta de configuración de RutaEorzea. No la envía a ningún servicio.

Dependencia: [vnavmesh](https://github.com/awgil/ffxiv_navmesh). Estructuras del cliente: [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs).
