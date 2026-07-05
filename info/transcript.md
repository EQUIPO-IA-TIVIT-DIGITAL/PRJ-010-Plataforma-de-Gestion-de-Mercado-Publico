# 📝 Las notas

jun 22, 2026

## \[CU010\] Alineamientos

Invitado [Matias Alejandro Mendez Cabrejos](mailto:matias.mendez@tivit.com) [Marjorie Ann Guerra Neira](mailto:marjorie.guerra@tivit.com) [Manuel Juda Aliaga Aliaga](mailto:manuel.aliaga@tivit.com) [juda.professional@gmail.com](mailto:juda.professional@gmail.com)

Archivos adjuntos [\[CU010\] Alineamientos](https://calendar.google.com/calendar/event?eid=Nm9qbjRrbmt0dGg1cnRjZzAzNHFvYmowYjIgbWFudWVsLmFsaWFnYUB0aXZpdC5jb20)

Registros de la reunión [Transcripción](https://docs.google.com/document/d/19qzDBtBFaWZY0GjTs9D841ldFMFwGMGRhoor5K078w4/edit?usp=drive_web&tab=t.yjqn5ypji9zu) [Grabación](https://drive.google.com/file/d/1vowqURSKZJS9p0Jf0FpolNKXmWGv1Clv/view?usp=drive_web) 

### Resumen

El equipo definió la automatización de licitaciones y migró su entorno de desarrollo hacia GitLab.

**Análisis automatizado de licitaciones**  
El agente automatizado extraerá documentos de Mercado Público para comparar ofertas y analizar factores de éxito frente a competidores. Esto elimina la necesidad de realizar descargas manuales.

**Migración de desarrollo tecnológico**  
Se decidió migrar el código a GitLab para garantizar mayores niveles de seguridad y cumplir con los estándares técnicos exigidos para el marco de inteligencia artificial.

**Estabilidad mediante casos uso**  
La culminación de los casos de uso resulta vital para la estabilidad operativa del equipo. Completar estos objetivos facilita la apertura de nuevos proyectos futuros.

### Próximos pasos

- [ ] \[Manuel Juda Aliaga Aliaga\] Compartir repositorio: Enviar el archivo comprimido del repositorio de código a Matias.

- [ ] \[Manuel Juda Aliaga Aliaga\] Gestionar GitLab: Conversar con el equipo sobre las máquinas necesarias para el funcionamiento de la integración y despliegue continuo en GitLab.

- [ ] \[Matias Alejandro Mendez Cabrejos\] Definir servicios: Analizar el código para listar los servicios de Google Cloud Platform requeridos para la presentación del jueves.

- [ ] \[Matias Alejandro Mendez Cabrejos\] Migrar código: Subir el repositorio de proyecto a la plataforma GitLab.

### Detalles

* **Objetivo del agente de análisis de licitaciones**: Manuel solicita la creación de un agente automatizado capaz de analizar licitaciones, específicamente para entender las razones detrás de las propuestas perdidas y proporcionar un seguimiento detallado de las participaciones activas. El agente debe ser capaz de responder a consultas de negocio complejas, tales como la diferencia de ingresos en ventas frente a competidores como Sonda, eliminando la necesidad de que el usuario descargue documentos manualmente de la plataforma Mercado Público.

* **Flujo de trabajo en la plataforma Mercado Público**: Manuel detalla el proceso necesario para la automatización, que consiste en navegar en Mercado Público, realizar búsquedas avanzadas filtradas por estado (adjudicada) y región, y acceder a los archivos adjuntos ([00:01:56](#00:01:56)) ([00:08:00](#00:08:00)). El objetivo es que el agente extraiga automáticamente el "acta de evaluación" de los PDFs obtenidos, lo cual es fundamental para realizar un análisis comparativo, identificar a los principales competidores y determinar los factores que contribuyen al éxito o fracaso en las licitaciones ([00:03:05](#00:03:05)) ([00:08:58](#00:08:58)).

* **Requerimientos de infraestructura y plazos**: Manuel asigna a Matias la tarea urgente de definir los servicios de Google Cloud necesarios para el proyecto, con el fin de tenerlos listos para la tarde del mismo día. Matias debe revisar el repositorio que Manuel entregará para especificar los requerimientos técnicos, permitiendo que Manuel coordine con Nicolás la configuración del proyecto y la contextualización de cara a una presentación programada para el jueves ([00:04:11](#00:04:11)). Se acuerda excluir el Caso 9 por el momento y centrarse únicamente en el Caso 1 ([00:05:06](#00:05:06)).

* **Expectativas para la presentación del jueves**: Manuel subraya la necesidad de alcanzar la excelencia en la demostración programada para el jueves a las 8:00 AM, donde estarán presentes altos directivos como Leonardo, Pablo, Fernando y Francesco. Matias se compromete a asistir y trabajar en los avances necesarios, considerando que la consistencia y la calidad de estas demostraciones han sido clave para el éxito del equipo en los últimos dos meses de reuniones ([00:05:54](#00:05:54)).

* **Importancia de cerrar casos de uso**: Manuel explica que la culminación exitosa de los casos de uso es vital para la estabilidad del equipo, advirtiendo que la falta de resultados podría poner en riesgo la continuidad de los miembros del grupo. Completar estos casos permite demostrar el valor del equipo ante los clientes y facilita la apertura de nuevos proyectos, como Excalien, asegurando la viabilidad operativa a futuro ([00:07:06](#00:07:06)).

* **Gestión del repositorio y estándares de desarrollo**: Matias recomienda migrar todo el código de desarrollo a GitLab en lugar de GitHub, argumentando que esto garantiza mayores niveles de seguridad y cumple con los estándares exigidos para el marco de trabajo de inteligencia artificial. Manuel acepta la propuesta y se compromete a gestionar las soluciones necesarias para resolver los problemas de las máquinas en el flujo de integración continua (CI/CD), procediendo a entregar el repositorio a Matias para iniciar el trabajo en Google Cloud Platform ([00:05:06](#00:05:06)) ([00:09:54](#00:09:54)).

*Revisa las notas de Gemini para asegurarte de que sean precisas. [Obtén sugerencias y descubre cómo Gemini toma notas](https://support.google.com/meet/answer/14754931)*

*Cómo es la calidad de **estas notas específicas?** [Responde una breve encuesta](https://google.qualtrics.com/jfe/form/SV_9vK3UZEaIQKKE7A?confid=npnZYah_NjNSbj3bSH_6DxIUOAIIigIgABgFCA&detailid=standard&screenshot=false) para darnos tu opinión; por ejemplo, cuán útiles te resultaron las notas.*

# 📖 Transcripción

jun 22, 2026

## \[CU010\] Alineamientos \- Transcripción

### 00:00:00

**Manuel Juda Aliaga Aliaga:** agente que primero puedas consultar el análisis de las licitaciones, por ejemplo, otros participantes son licitaciones, ¿okay? Y hay licitaciones que obviamente no ganamos. Quiero tener un análisis de por qué esas licitaciones hemos perdido. Mercado público te da información, eh, no por API, pero te da información dentro de la página. Entonces, tú tienes que crear un agente que ya hay un agente que se encarga de hacer eso que yo le he creado. Falta mejoras. Ahora, después eso es primero con la parte de análisis de las licitaciones. También hay otros factores que quieren ver. Quieren ver, por ejemplo, seguimiento de licitaciones que estamos participando, notificaciones a a cuando una licitación pide que aclaremos algo, un historial de licitaciones, todo lo que tenga que ver con análisis, seguimientos, con licitaciones que nosotros participamos y responder ciertas preguntas. Por ejemplo, una de las preguntas que Francisco mucho me hace. Francisco es el gerente en Chile que me dice, "Oye, yo quiero saber por qué Sonda vendió $,000,00000 de nosotros 20\. ¿Dónde están esos 80 millones de dólares de diferencia? ¿Me entiendes?

### 00:00:59

**Manuel Juda Aliaga Aliaga:** Hasta ahí estás completamente alineado

**Matias Alejandro Mendez Cabrejos:** Y ya, a ver, lo que se quiere es uno eh es parecido al al uno, ¿no? lo que lo que iba a entender de que es analizar,

**Manuel Juda Aliaga Aliaga:** en análisis.

**Matias Alejandro Mendez Cabrejos:** claro, es análisis, analizar documentos, ver el por qué, por ejemplo,

**Manuel Juda Aliaga Aliaga:** Pero acá cabe algo importante,

**Matias Alejandro Mendez Cabrejos:** A le ganó B,

**Manuel Juda Aliaga Aliaga:** amigo,

**Matias Alejandro Mendez Cabrejos:** el Todos ya están disponibles.

**Manuel Juda Aliaga Aliaga:** que acá tú no vas a subir ningún documento, ¿no? Tú tienes que buscar mediante un agente los documentos. Es decir, lo que no se quiere es que tú entres a la plataforma de de, por ejemplo, de mercado, descargues los PDFs y recién hagas el análisis. Un agente ya lo tiene que hacer.

**Matias Alejandro Mendez Cabrejos:** Ya tiene

**Manuel Juda Aliaga Aliaga:** ¿Me entiendes?

**Matias Alejandro Mendez Cabrejos:** ya

**Manuel Juda Aliaga Aliaga:** Por ejemplo, te pongo una te hago una explicación rapidito. Compartir mi pantalla.

**Matias Alejandro Mendez Cabrejos:** por eso lo bueno de poner de tomar notas Acá

### 00:01:56 {#00:01:56}

**Manuel Juda Aliaga Aliaga:** Uy, ¿qué pasó? Espérate, ¿me escuchas?

**Matias Alejandro Mendez Cabrejos:** te escucho

**Manuel Juda Aliaga Aliaga:** Se me ha lagado mi Chrome.

**Matias Alejandro Mendez Cabrejos:** que

**Manuel Juda Aliaga Aliaga:** Ahí está. este mercado público. Entonces, lo que se necesita es, por ejemplo, vamos acá iniciar sesión pasar mi cuenta, ¿no? extranjeros ingresar ahora. Entonces, por ejemplo, vamos acá. Entonces, lo que primero que se quiere hacer es, por ejemplo, licitaciones, búsquedas de licitaciones para ofertar. Okay,

**Matias Alejandro Mendez Cabrejos:** Mhm.

**Manuel Juda Aliaga Aliaga:** te aparece esto. Entonces, por ejemplo, yo puedo hacer esto, puedo hacer clic acá y me voy a enero, es el uno. Okay. Buscada por fecha de publicación, nombre. Puedo buscar por todo, todas las regiones, sería bacán poder analizarlo por regiones. Estado, en este caso solo adjudicada y ponem buscar. Como verás, tengo ricos audios de de

### 00:03:05 {#00:03:05}

**Matias Alejandro Mendez Cabrejos:** Ya Porque Mario le gustan los

**Manuel Juda Aliaga Aliaga:** Mari. Sí, sí,

**Matias Alejandro Mendez Cabrejos:** audios.

**Manuel Juda Aliaga Aliaga:** sí. Y acá salen todas las situaciones adjudicadas en las cuales hemos participado, ¿me entiendes? Esto es lo que tenemos que buscar, ¿me entiendes? Entonces, eso es lo que exacto.

**Matias Alejandro Mendez Cabrejos:** Pero automatizado.

**Manuel Juda Aliaga Aliaga:** Automatizado. Por ejemplo, cuando acá a ver ficha, vamos acá a ver ficha, mira, vas acá a ver adjuntos y en ver adjuntos te sale todo ese tipo de información. Entonces, la única información importante es esto, acta evaluación de licitación e ir recién descargar y a partir de ese documento hacer el análisis. Ahora imagínate con tantas licitaciones que tenemos, con tantas licitaciones que existen, dar un seguimiento de esa data obtenida, verificar en qué hemos acertado y en qué hemos cerrado, cuál es la diferencia entre las licitaciones que hemos ganado y con las que hemos perdido, quiénes son nuestros más rivales, dónde estar nuestros dichos. Todo ese tipo de preguntas y todo ese tipo de análisis tenemos que hacernos en este en este campo, amigo.

### 00:04:11 {#00:04:11}

**Matias Alejandro Mendez Cabrejos:** Ya me gusta.

**Manuel Juda Aliaga Aliaga:** ¿Me entiendes ahora?

**Matias Alejandro Mendez Cabrejos:** Ya estoy pensando cómo sacar los datos. Voy a tener que hacer un growing a toda esa pobre página.

**Manuel Juda Aliaga Aliaga:** Sí. Ahora, amigo, yo confío plenamente en ti. Yo te voy a dar libertad del equipo. Tú eres el que más confío, ya sabes. Ahora, acá hay un reto. Okay. Necesito que para hoy día en la tarde, más tardar en la tarde me digas, "Manuel, esto es lo que necesito en Google Cloud porque el jueves te pienso unir a la presentación." Ese es tu reto hoy día. Ese es tu reto de esta semana, amigo. Okay.

**Matias Alejandro Mendez Cabrejos:** Okay, no entendí eso.

**Manuel Juda Aliaga Aliaga:** Ah, ya. Decito, hoy día yo te voy a pasar lo que ya he avanzado, que la tengo en mi repo, te la comparto, analizas y me vas diciendo, "Manuel, necesito estos servicios de Google." Okay. Para que veas cuántos audios hay. Solo te voy a decir,

### 00:05:06 {#00:05:06}

**Matias Alejandro Mendez Cabrejos:** Sí, ya estoy

**Manuel Juda Aliaga Aliaga:** "Estoy muriendo." Ahora necesito que analices y me digas,

**Matias Alejandro Mendez Cabrejos:** viendo.

**Manuel Juda Aliaga Aliaga:** "Manuel, estos son los servicios de Google que necesito para yo poder darle contextualización a Nicolás y crearte este proyecto aparte." Okay. Empieza.

**Matias Alejandro Mendez Cabrejos:** Te paso,

**Manuel Juda Aliaga Aliaga:** Sí.

**Matias Alejandro Mendez Cabrejos:** aprovecha y pídamelos para el uno y el nueve.

**Manuel Juda Aliaga Aliaga:** El nueve lo voy a discutir. El un nueve lo sacamos y que Squad y el Atan solo sea del uno. Okay.

**Matias Alejandro Mendez Cabrejos:** Listo.

**Manuel Juda Aliaga Aliaga:** Entonces, yo te voy a pasar la repo. Al pasarte la repo me lo analizas y me dices, "Ya, Manuel, necesito esto, esto, esto, pero necesito en tiempo récord, amigo. Disculpa por pedírte simple, necesito tiempo récord, pero quiero aprovechar a Nicolás para solicitarle los servicios y darle la contextualización. Nos dan la repo y empieza a trabajar en en GSP. Empezamos a trabajar en GCP y a partir de eso ya tenemos una respuesta.

### 00:05:54 {#00:05:54}

**Manuel Juda Aliaga Aliaga:** Entonces podemos hacer una presentación y esto tiene que ver avance todos los jueves, amigo, porque los jueves hay la reunión con 8 de la

**Matias Alejandro Mendez Cabrejos:** Uy, ya. ¿Qué hora de qué hora?

**Manuel Juda Aliaga Aliaga:** mañana.

**Matias Alejandro Mendez Cabrejos:** Ya 8 de la mañana estoy, pero los jueves yo no estoy hasta la 1\. Te paso mi horario a partir de donde estoy Ah,

**Manuel Juda Aliaga Aliaga:** Pero no estás a las 8, no estás a las 8\.

**Matias Alejandro Mendez Cabrejos:** sí,

**Manuel Juda Aliaga Aliaga:** No estás disponible a las 8\.

**Matias Alejandro Mendez Cabrejos:** sí estoy. Sí, sí, estoy a las 8\.

**Manuel Juda Aliaga Aliaga:** Ya,

**Matias Alejandro Mendez Cabrejos:** Las 8\.

**Manuel Juda Aliaga Aliaga:** máximo de 8 a 9 de la mañana,

**Matias Alejandro Mendez Cabrejos:** No,

**Manuel Juda Aliaga Aliaga:** amigo. No te preocupes. Eso es lo que dura esa reunión, que es Ah,

**Matias Alejandro Mendez Cabrejos:** ya.

**Manuel Juda Aliaga Aliaga:** eso sí, amigo, tiene que estar perfecto. Hasta ahorita no hemos fallado. Llevamos más de dos meses en esas reuniones y yo me encargaba de hacer las demostraciones.

### 00:06:26

**Manuel Juda Aliaga Aliaga:** No hemos fallado. Necesito, amigo, literalmente excelencia. ¿Por qué? Porque están literalmente las cabezas grandes, Leonardo, Pablo, Fernando, Francesco. Okay, entonces confío en ti, amigo. Y aparte para sacarte el caso 01\. Sé que estás aburrido. Ya.

**Matias Alejandro Mendez Cabrejos:** Está aburrido, pero que es el único caso esta habilidad.

**Manuel Juda Aliaga Aliaga:** Sí, pero igual para es que al final, amigo, A o B, cierra un caso 01\.

**Matias Alejandro Mendez Cabrejos:** Ese

**Manuel Juda Aliaga Aliaga:** ¿Qué haces? ¿Qué haces tú? ¿Te quedas en el aire?

**Matias Alejandro Mendez Cabrejos:** caso,

**Manuel Juda Aliaga Aliaga:** Prefiero,

**Matias Alejandro Mendez Cabrejos:** ese caso,

**Manuel Juda Aliaga Aliaga:** prefiero que te amigo aquí,

**Matias Alejandro Mendez Cabrejos:** ese caso nunca va a cerrar, siempre van a meter una

**Manuel Juda Aliaga Aliaga:** aquí entra dos. Aquí entra dos.

**Matias Alejandro Mendez Cabrejos:** cor encerrar el uno

**Manuel Juda Aliaga Aliaga:** No están seguro. Sí,

### 00:07:06 {#00:07:06}

**Matias Alejandro Mendez Cabrejos:** al fin.

**Manuel Juda Aliaga Aliaga:** pero el problema es que amigo al fin de todo, escúchame. Hemos detectado que, por ejemplo, si quieres cerrar porque ya qué pasa si cierran casos de uso, amigo? ¿Qué pasa con el equipo?

**Matias Alejandro Mendez Cabrejos:** Huele alto.

**Manuel Juda Aliaga Aliaga:** Exacto. Desaparece gente. Entonces, ahora la pregunta es, son seis trainies.

**Matias Alejandro Mendez Cabrejos:** No.

**Manuel Juda Aliaga Aliaga:** Si cierran casos de uso, van a cerrar más trains. Entonces, por eso es mejor que los casos sean excelentes para poder promover y que en verdad vean, porque eso tiene que estar ya haciendo clientes. José P ya está viendo clientes con Soccer. Tú espero que ya esté todo finalizado para que ya estén viendo clientes al menos del mismo Tibi. Jesús ya está viendo clientes de A esa y otros lados, ¿me entiendes? Ahora va a venir también Excalien otros casos de uso donde vamos a necesitar gente, por eso necesito ya cerrar casos de uso, ¿me entiendes?

**Matias Alejandro Mendez Cabrejos:** Ya,

**Manuel Juda Aliaga Aliaga:** Okay, amigo.

### 00:08:00 {#00:08:00}

**Manuel Juda Aliaga Aliaga:** Entonces,

**Matias Alejandro Mendez Cabrejos:** ya sí me puede hacer el flujo otra vez porque recién le he puesto a grabar esto.

**Manuel Juda Aliaga Aliaga:** quedamos así. Ah, ya, no hay problema. Vamos acá ya al acceder ya tú lo sabes. Vas acá a licitaciones, búsqueda de licitaciones para ofertar. Vas acá a búsqueda de licitaciones, búsqueda avanzada. Vas acá por todas las regiones, estado, adjudicada. Puedes investigar también qué significa las otras. Después de adjudicada, vas a fecha de publicación y algo importante, licitaciones en las que he ofertado,

**Matias Alejandro Mendez Cabrejos:** Ok.

**Manuel Juda Aliaga Aliaga:** ¿me entiendes? Porque al final te va a aparecer de todos y va a sacar la fecha que tú quieres analizar. Francisco quiere desde el 2025, así que tienes que analizar bien eso. Vas acá y pones buscar y te aparecen 10 licitaciones. A partir de estas 10 licitaciones, haces clic, por ejemplo, en esta de acá, te aparece la información de acá.

### 00:08:58 {#00:08:58}

**Manuel Juda Aliaga Aliaga:** Puedes ir acá a ver adjuntos. En Veras adjuntos te sendrá 1000 PDFs y siempre el que diga acta de evaluación es el importante que es como una tesis donde te explica las razones por las que hemos ganado y por las que no. ¿Okay? Entonces a partir de esa tesis haces un análisis con resultados y que tienes que ser extraída en PDF. Pero esto es en caso de adjudicadas. Ahora también queremos un análisis de ganadas, queremos un análisis de casas o perdidas, de las que me entiendes, buscar más campos para analizar. Okay. Pero si te das cuenta, ¿dónde está la información rica, amigo? Es en lo más y se extrae el documento después full análisis con inteligencia artificial. Okay,

**Matias Alejandro Mendez Cabrejos:** Ya listo.

**Manuel Juda Aliaga Aliaga:** quedamos así. Ya.

**Matias Alejandro Mendez Cabrejos:** Sí.

**Manuel Juda Aliaga Aliaga:** Entonces yo te comparto ahorita el repo. Te lo paso por un zip ya porque no lo tengo ni en repo. Todo

**Matias Alejandro Mendez Cabrejos:** Ya lo subo al verad presiona para el gitlap porque la gente no está que lo

### 00:09:54 {#00:09:54}

**Manuel Juda Aliaga Aliaga:** amigo consulta.

**Matias Alejandro Mendez Cabrejos:** usa.

**Manuel Juda Aliaga Aliaga:** ¿Y si usamos?

**Matias Alejandro Mendez Cabrejos:** Yo prefiero el Gitlap. Me me gusta GitHub,

**Manuel Juda Aliaga Aliaga:** Así.

**Matias Alejandro Mendez Cabrejos:** pero el GitLab es como que es es más que nada por la seguridad, ¿no? O sea, estamos presionando, están presionando siempre con el framework de IA y y que hay que mantener los estándares y esto y si lo vamos a pasar a GitHub va a ser peor.

**Manuel Juda Aliaga Aliaga:** Okay. No, mal normal,

**Matias Alejandro Mendez Cabrejos:** Lo mejor es yo yo pienso que todos hay que movernos a a Gitlap porque creo que hay gente usando

**Manuel Juda Aliaga Aliaga:** normal. Ya,

**Matias Alejandro Mendez Cabrejos:** GitHub y todos trabajar en el GitHub. El tema,

**Manuel Juda Aliaga Aliaga:** ya.

**Matias Alejandro Mendez Cabrejos:** el Gitlab, el tema es el CCD que no funciona porque no hay máquinas.

**Manuel Juda Aliaga Aliaga:** Porque no funciona de una. Ya yo voy a hablar eso con yo me encargo.

**Matias Alejandro Mendez Cabrejos:** Ya,

**Manuel Juda Aliaga Aliaga:** Y amigo,

**Matias Alejandro Mendez Cabrejos:** ya,

**Manuel Juda Aliaga Aliaga:** entonces yo te mando acá el repositorio,

**Matias Alejandro Mendez Cabrejos:** ya.

**Manuel Juda Aliaga Aliaga:** te mando por un

**Matias Alejandro Mendez Cabrejos:** Y ya.

**Manuel Juda Aliaga Aliaga:** OK

**Matias Alejandro Mendez Cabrejos:** Ojalá se me mande la grava, si no te me la me la

**Manuel Juda Aliaga Aliaga:** ya. Ya, amigo,

**Matias Alejandro Mendez Cabrejos:** rompí.

**Manuel Juda Aliaga Aliaga:** amigos.

**Matias Alejandro Mendez Cabrejos:** Listo.

**Manuel Juda Aliaga Aliaga:** Alo. Cha. Chao.

### La transcripción finalizó después de 00:11:04

*Esta transcripción editable se generó por computadora y puede contener errores. Los usuarios también pueden cambiar el texto después de que se cree.*