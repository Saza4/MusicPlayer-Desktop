# Principios de diseño

Criterios propios de MusicPlayer-Desktop para decidir cómo se ve y cómo se comporta la interfaz. No son reglas
cerradas: sirven para elegir entre opciones cuando dos cosas compiten. Las reglas concretas (colores, botones,
diseño adaptable) están en CLAUDE.md y en las specs de `docs/specs/`.

## Resumen

- **Propósito.** Cada pantalla sirve para escuchar y ordenar tu música. Lo que no ayuda a eso sobra.
- **Control.** La persona decide. La app explica lo que va a pasar, deja deshacer y no actúa por su cuenta sin avisar.
- **Confianza.** Tus archivos y tus datos son tuyos: la app es 100 % local, no se conecta a internet y nunca pone en
  riesgo un archivo sin una copia segura.
- **Familiaridad.** Se usan gestos y controles que ya conoces de cualquier reproductor de escritorio.
- **Adaptabilidad.** La ventana funciona igual de bien pequeña o a pantalla completa, con ratón o con teclado.
- **Sencillez.** Pocas opciones, bien nombradas, en su sitio.
- **Cuidado del detalle.** Las transiciones, los textos y los avisos están revisados uno a uno.
- **Accesibilidad.** Todo se puede usar con teclado, todo control tiene nombre para los lectores de pantalla y el
  texto se lee con buen contraste.

## Propósito

**Escuchar primero.** Lo que suena, los controles de reproducción y la letra tienen prioridad sobre cualquier otra
cosa en pantalla.

**Una función, un sitio.** Antes de añadir algo, preguntarse qué problema resuelve a quien escucha música y si ya
hay otro lugar donde se resuelve.

## Control

**Avisar antes, no después.** Si una acción escribe en un archivo o lo borra, el panel lo dice antes de confirmar.

**Poder volver atrás.** Las acciones que cambian muchas cosas a la vez tienen vista previa, confirmación o forma de
deshacer: «Limpiar etiquetas» muestra lo que va a cambiar y se puede deshacer; quitar o borrar canciones pide
confirmación.

**Respuesta clara.** La línea de estado dice lo que acaba de pasar, sobre todo cuando algo falla, con palabras y no
con códigos.

**Sin sorpresas.** La app no cambia de vista, no se cierra y no borra nada por su cuenta. La única excepción, los
temporales que ella misma dejó, está documentada y se informa en pantalla.

## Confianza

**Tus archivos, intactos.** Toda escritura se hace sobre una copia, se verifica y solo entonces reemplaza al
original.

**Tus datos, en tu equipo.** Nada sale de la máquina: ni la biblioteca, ni los ajustes, ni el registro de errores.

**Transparencia.** Las notas de cada versión dicen qué se probó y qué no.

## Familiaridad

**Lo esperable.** Doble clic reproduce, Espacio pausa, Esc cierra, las flechas mueven. Los iconos son los habituales
de cualquier reproductor.

**Constancia.** Un mismo botón se ve y se comporta igual en toda la app. Un estilo nuevo se aplica en todas partes o
en ninguna.

## Adaptabilidad

**Cabe en cualquier tamaño.** Entre 640×400 y pantalla completa no se recorta nada: lo secundario se compacta antes
de esconderse.

**Ratón y teclado por igual.** Ninguna función depende solo de uno de los dos.

## Sencillez

**Menos, pero claro.** Si una opción no la pide nadie, no se añade. Si se añade, empieza con el comportamiento de
siempre y en la vista de Configuración.

**Jerarquía visible.** Título grande, datos secundarios más tenues, acciones agrupadas a la derecha.

## Cuidado del detalle

**Movimiento con sentido.** Las animaciones acompañan un cambio (la isla que se despliega, la letra que avanza); no
decoran ni distraen, y no gastan recursos cuando no se ven.

**Textos revisados.** En español, sin tecnicismos innecesarios y con el nombre real de cada botón.

## Accesibilidad

- Todo control interactivo tiene un nombre accesible y una descripción emergente.
- El foco con teclado siempre se ve, con un anillo que contrasta con el fondo.
- El texto se lee con buen contraste sobre su fondo. Un tema de usuario cuyo texto no llega a 3:1 se rechaza, y el
  motivo aparece en la línea de estado.
- El color nunca es la única señal: lo que se marca con color se marca también con forma, icono o texto.

## Color

- Los colores salen siempre del tema activo, nunca de valores sueltos en la interfaz.
- Un solo color principal para lo que se puede pulsar y para lo que está activo.
- El significado de un color no cambia de una pantalla a otra.

## Tema oscuro y claro

- Oscuro es el tema por defecto: escuchar música suele ocurrir con poca luz.
- Claro, Oscuro y los temas de usuario se ven igual de bien; cada cambio visual se comprueba con al menos dos temas.
- La opción «Sistema» sigue el tema del sistema operativo.
