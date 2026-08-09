# PROMPT MAESTRO — DOTA AI COACH

Quiero que actúes como un equipo completo compuesto por:

* Staff Software Engineer
* Software Architect
* Senior .NET Engineer
* Senior Python/ML Engineer
* Data Engineer
* Machine Learning Engineer
* Computer Vision Engineer
* Dota 2 Data Analyst
* Dota 2 high-MMR coach
* UX/UI Designer especializado en gaming overlays
* MLOps Engineer
* QA Engineer
* Security Engineer
* Product Manager

Tu misión es diseñar y construir desde cero un proyecto llamado provisionalmente:

# Dota AI Coach

Una aplicación de escritorio para Windows que funcione mientras el usuario juega Dota 2 y que utilice exclusivamente información legítimamente disponible para el jugador para ofrecer coaching y recomendaciones en tiempo real.

La meta del sistema es:

> Maximizar la probabilidad de victoria del jugador mediante mejores decisiones antes, durante y después de cada partida.

No debe limitarse a recomendar builds estáticas.

Debe entender:

* quién es el jugador;
* qué tan bueno es;
* su medalla;
* su experiencia;
* sus héroes favoritos;
* sus fortalezas;
* sus debilidades;
* el parche actual;
* el draft;
* aliados;
* enemigos;
* matchup;
* situación económica;
* power spikes;
* items;
* habilidades;
* kills;
* deaths;
* assists;
* farm;
* XP;
* oro;
* torres;
* buyback;
* timings;
* estado de la partida;
* condición de victoria;
* condición de victoria rival;
* y cualquier otra variable legítimamente observable que aporte capacidad predictiva.

La aplicación debe aprender del jugador con el tiempo.

---

# PRINCIPIO FUNDAMENTAL

La aplicación NO debe ser un cheat.

Debe existir una separación estricta entre:

## Información permitida

Puede utilizar:

* Dota 2 Game State Integration oficial;
* APIs públicas;
* OpenDota;
* STRATZ u otras fuentes públicas legítimas;
* Steam OpenID;
* historial público del propio usuario;
* replays y datos de partidas terminadas;
* información visible en la pantalla del propio jugador;
* Computer Vision aplicada exclusivamente sobre información que el propio jugador puede ver;
* datos introducidos voluntariamente por el usuario;
* metadatos públicos de héroes/items/parches.

## Información prohibida

NO implementar:

* lectura de memoria de Dota;
* DLL injection;
* hooks al proceso de Dota;
* manipulación de paquetes;
* acceso a información bajo fog of war;
* posición oculta de enemigos;
* wards enemigos no observados;
* items enemigos que el jugador nunca vio;
* cooldowns enemigos ocultos;
* automatización de movimiento;
* automatización de skills;
* automatización de compra;
* inputs automáticos;
* scripts que jueguen por el usuario;
* identificación de jugadores enemigos mediante técnicas que Valve deliberadamente haya intentado ocultar;
* abuso de APIs internas;
* explotación de interfaces privadas;
* cualquier mecánica comparable a Overplus u otros sistemas considerados ventaja ilegítima.

Crear desde el primer día un módulo llamado conceptualmente:

FairPlayPolicy

Cada feature de producción debe declarar:

* nombre;
* fuente;
* cuándo se conoce;
* quién puede observarla;
* si puede usarse live;
* si puede usarse solo postgame;
* nivel de riesgo;
* justificación.

Principio:

> Si un jugador humano mirando legítimamente su propia pantalla podría conocer el dato, podemos analizarlo. Si Dota deliberadamente lo oculta, nuestro sistema también debe ocultarlo.

---

# OBJETIVO GENERAL DEL PRODUCTO

La aplicación debe cubrir cinco momentos:

1. antes de buscar partida;
2. draft;
3. laning;
4. mid/late game;
5. postgame.

Flujo deseado:

Usuario
↓
Perfil histórico
↓
Hero pool
↓
Meta del parche
↓
Queue preparation
↓
Draft
↓
Recomendación de héroe
↓
Plan de línea
↓
Live game
↓
Recomendaciones dinámicas
↓
Postgame
↓
Análisis
↓
Perfil actualizado
↓
Mejores recomendaciones futuras

---

# OBJETIVOS DEL SISTEMA

Responder continuamente preguntas como:

* ¿Qué héroes debería jugar hoy?
* ¿Qué héroe debería elegir en este draft?
* ¿Cuál es la probabilidad estimada de victoria?
* ¿Cuál es mi matchup?
* ¿Cómo debería jugar los primeros 10 minutos?
* ¿Qué item debería comprar ahora?
* ¿Debo cambiar la build?
* ¿Necesito BKB antes que otro item?
* ¿Me conviene Linken?
* ¿Necesito MKB?
* ¿Necesito Shiva?
* ¿Necesito Vessel?
* ¿Necesito Dispel?
* ¿Debo farmear o pelear?
* ¿Cuál es mi próximo power spike?
* ¿Cuánto oro falta para ese power spike?
* ¿Cuánto tardaré aproximadamente?
* ¿Debo guardar buyback?
* ¿Debemos ir Roshan?
* ¿Debemos tomar torre?
* ¿Conviene high ground?
* ¿Cuánto cuesta morir ahora?
* ¿Estoy por debajo o encima de mi farm esperado?
* ¿Cuál es la amenaza enemiga principal?
* ¿Cuál es nuestra condición de victoria?
* ¿Cuál es la condición de victoria enemiga?
* ¿Qué error estoy repitiendo?
* ¿Qué patrón de mis partidas reduce más mi winrate?

---

# NOMBRE DE LA ARQUITECTURA

Diseñar una arquitectura modular local-first.

Arquitectura conceptual:

Dota 2
→ Game State Integration
→ Live State Collector

Pantalla
→ Screen Capture
→ Computer Vision
→ Observable State Collector

OpenDota / STRATZ / Steam
→ Historical Data Collector

Todos:
→ State Merger
→ Feature Engine
→ Recommendation Engines
→ Decision Orchestrator
→ Overlay
→ Voice
→ Storage
→ Analytics
→ Learning

---

# STACK TECNOLÓGICO PROPUESTO

Priorizar Windows.

## Aplicación desktop

Preferir:

.NET 8 o superior

Evaluar:

* WPF;
* WinUI 3;
* Avalonia si aporta ventajas reales.

Elegir uno y justificar.

Debe permitir:

* ventana transparente;
* click-through;
* always-on-top;
* múltiples monitores;
* DPI scaling;
* overlay sin robar focus;
* hotkeys;
* consumo bajo;
* actualizaciones rápidas.

## Backend local

Preferentemente:

C#/.NET

Responsabilidades:

* GSI listener;
* state engine;
* reglas;
* inference;
* event bus;
* overlay communication;
* persistence.

## Machine Learning

Python.

Usar inicialmente:

* pandas;
* Polars si mejora rendimiento;
* DuckDB;
* scikit-learn;
* LightGBM;
* XGBoost;
* PyTorch cuando realmente sea necesario;
* Optuna;
* MLflow opcional;
* ONNX.

## Inference en desktop

Preferir:

ONNX Runtime

Evitar depender de un proceso Python durante una partida si no es necesario.

## Base local

Evaluar:

SQLite + DuckDB.

Posible esquema:

SQLite:
datos transaccionales/locales.

DuckDB:
analítica pesada/datasets.

## Computer Vision

Evaluar:

* OpenCV;
* YOLO;
* clasificación de portraits;
* template matching;
* OCR únicamente cuando aporte valor;
* ONNX Runtime.

No usar OCR indiscriminadamente.

## APIs

* OpenDota
* STRATZ
* Steam OpenID

Crear abstracciones para no acoplar el dominio a un proveedor.

---

# ESTRUCTURA DEL MONOREPO

Propón una estructura profesional como:

/apps
/desktop
/local-api

/services
/gsi
/vision
/recommendation
/analytics

/ml
/datasets
/features
/draft-model
/win-model
/item-model
/personalization
/training
/evaluation
/export

/packages
/domain
/contracts
/dota-constants
/fairplay
/telemetry

/data
/raw
/interim
/processed

/docs
/architecture
/product
/ml
/fairplay
/research
/api
/adr

/tests
/unit
/integration
/fixtures
/ml
/e2e

/scripts

Define exactamente la estructura final.

---

# MÓDULO 1 — PLAYER PROFILE

Crear un perfil histórico del usuario.

Variables:

* Steam account;
* account ID;
* rank;
* medal;
* estimated MMR si puede obtenerse legítimamente;
* total matches;
* ranked matches;
* first public match;
* active months;
* matches last 7 days;
* matches last 30 days;
* matches last 90 days;
* main roles;
* hero pool;
* hero experience;
* hero winrate;
* hero KDA;
* hero GPM;
* hero XPM;
* LH;
* lane performance;
* recent form;
* patch performance;
* role performance;
* matchup performance;
* duration performance;
* Radiant/Dire performance;
* party/solo performance cuando sea posible;
* item timing behavior;
* buyback behavior;
* average deaths;
* deaths before minute 10;
* deaths after minute 35;
* objective conversion;
* Roshan conversion;
* tower damage;
* hero damage;
* warding para supports;
* teamfight participation;
* comeback performance;
* throw patterns;
* highground failures;
* hero comfort.

No asumir que años jugando implica habilidad.

Años de experiencia es solo una feature más.

Dar mayor peso a:

* partidas recientes;
* partidas con el héroe;
* partidas con el rol;
* parche actual;
* bracket comparable.

---

# HERO COMFORT SCORE

Crear un Hero Comfort Score de 0 a 100.

Debe utilizar como mínimo:

* cantidad de partidas;
* partidas recientes;
* winrate ajustado;
* rendimiento individual;
* role familiarity;
* patch familiarity;
* matchup familiarity;
* consistency;
* mechanical complexity;
* recent inactivity.

No usar winrate bruto con muestras pequeñas.

Aplicar técnicas como:

* Bayesian shrinkage;
* empirical Bayes;
* confidence intervals.

Ejemplo:

2 partidas / 2 victorias

NO debe producir comfort 100.

---

# PLAYER DOTA DNA

Crear un perfil conceptual:

LANING
FARM
FIGHTING
POSITIONING
OBJECTIVES
ITEMIZATION
SURVIVAL
VISION
TEMPO
LATE GAME
BUYBACK DISCIPLINE

0-100 cada uno.

Además generar:

* fortalezas principales;
* debilidades principales;
* tendencias;
* mejores héroes;
* peores héroes;
* mejores roles;
* condiciones en las que gana;
* condiciones en las que pierde.

No inventar psicología.

Nunca afirmar:

“estás tilt”

basado únicamente en derrotas.

Sí se puede afirmar:

“tu LH@10 está 18% debajo de tu baseline de las últimas 50 partidas”.

---

# MÓDULO 2 — META ENGINE

La aplicación debe mantener contexto del parche actual.

Registrar:

* patch_id;
* timestamp;
* hero changes;
* item changes;
* map changes;
* economy changes;
* facets;
* talents;
* hero winrates;
* pickrates;
* item tendencies;
* role tendencies;
* bracket-specific tendencies.

Nunca mezclar datos de parches antiguos sin ponderación.

Diseñar una decay function.

Ejemplo conceptual:

current patch = 1.00

previous minor = 0.65–0.80

previous major = 0.10–0.30

más antiguo = peso mínimo.

Pero no fijar arbitrariamente esos pesos.

Entrenarlos/validarlos.

---

# MÓDULO 3 — BRACKET-AWARE MODELS

Separar o condicionar modelos por:

* Herald
* Guardian
* Crusader
* Archon
* Legend
* Ancient
* Divine
* Immortal

No asumir que una estrategia óptima en Immortal es óptima en Crusader.

Evaluar:

1. modelos separados;
2. un modelo global con rank embedding;
3. mixture of experts.

Comparar resultados.

---

# MÓDULO 4 — QUEUE PREPARATION

Antes de buscar partida mostrar:

* rol recomendado;
* héroes recomendados;
* héroes que conviene evitar;
* comfort;
* meta compatibility;
* reciente rendimiento;
* hero pool health.

Ejemplo:

READY

Role:
Mid

Recommended:

1. Puck 82
2. Storm 76
3. Ember 71

Avoid:

Invoker 43

Reason:
personal performance on current patch is below baseline.

La información debe ser concisa.

---

# MÓDULO 5 — DRAFT COACH

Debe analizar:

* héroes aliados;
* enemigos;
* bans si están disponibles legítimamente;
* hero roles;
* facets;
* lanes probables;
* comfort del jugador;
* counters;
* synergies;
* lane matchup;
* scaling;
* execution difficulty;
* damage distribution;
* disables;
* initiation;
* counter-initiation;
* saves;
* sustain;
* dispels;
* wave clear;
* split push;
* Roshan;
* tower damage;
* high ground;
* high-ground defense;
* teamfight;
* pickoff;
* mobility;
* vision;
* greedy cores;
* farm distribution;
* timing compatibility.

---

# REPRESENTACIÓN DEL DRAFT

Cada héroe debe estar representado con un vector semántico/estructurado:

damage:

* physical;
* magical;
* pure;
* burst;
* sustained;
* AoE;
* single-target.

control:

* stun;
* root;
* silence;
* hex;
* leash;
* fear;
* slow;
* disarm;
* break;
* BKB piercing disable.

defense:

* heal;
* sustain;
* dispel;
* hard dispel;
* save;
* armor;
* magic resistance;
* evasion;
* mobility.

macro:

* tower damage;
* Roshan damage;
* wave clear;
* split push;
* catch;
* initiation;
* counter-initiation;
* high-ground offense;
* high-ground defense;
* map mobility;
* vision.

economy:

* farm dependency;
* greedy score;
* timing;
* scaling.

Diseñar una representación mantenible que pueda cambiar con parches.

---

# DRAFT SCORE

No usar un simple counter score.

Calcular:

DraftScore = función de:

* synergy;
* counter;
* personal comfort;
* lane matchup;
* execution difficulty;
* patch strength;
* damage profile;
* disable profile;
* scaling;
* tempo;
* objectives.

Mostrar:

BEST PICK FOR YOU

1. Puck 81
2. Storm 76
3. Ember 68

Explicar brevemente.

---

# DETECCIÓN AUTOMÁTICA DEL DRAFT

Como GSI no necesariamente expone todos los jugadores mientras el usuario juega, usar Computer Vision únicamente sobre la pantalla visible.

Detectar portraits de héroes en:

* draft;
* top bar;
* scoreboard;
* HUD cuando corresponda.

Preferir:

* clasificación de regiones conocidas;
* template matching robusto;
* modelo pequeño de visión.

No usar un detector pesado si la posición de los portraits es estable.

Implementar:

* DPI awareness;
* diferentes resoluciones;
* 16:9;
* 16:10;
* ultrawide;
* UI scales;
* diferentes idiomas.

Crear calibración automática.

---

# MÓDULO 6 — LANE COACH

Variables:

* hero matchup;
* role;
* starting items;
* skill build;
* level;
* XP;
* LH;
* denies;
* deaths;
* rune timings;
* item timing;
* lane outcome histórico.

Generar benchmarks del tipo:

Puck Mid
Ancient
Current Patch

Expected LH @5
Expected LH @10
Expected Net Worth @10

Comparar contra el usuario.

Ejemplo:

LH @10:

Expected: 62
You: 51

Delta: -17.7%

No sobrecargar al usuario.

---

# MÓDULO 7 — LIVE GAME STATE

Implementar Dota 2 Game State Integration.

Configurar automáticamente cuando sea posible.

Crear:

GSI HTTP Listener

Debe manejar:

* reconexión;
* errores;
* eventos parciales;
* cambios de partida;
* stale data;
* desincronización;
* duplicate events;
* high frequency updates.

No recalcular ML innecesariamente a 60 FPS.

Crear un State Engine con eventos.

Variables permitidas a capturar según disponibilidad oficial:

MAP:

* match_id;
* game_time;
* clock_time;
* game_state;
* radiant_score;
* dire_score;
* day/night;
* paused;
* buildings;
* win_team cuando termine.

PLAYER:

* kills;
* deaths;
* assists;
* last hits;
* denies;
* gold;
* reliable gold;
* unreliable gold;
* GPM;
* XPM.

HERO:

* name;
* level;
* XP;
* health;
* max health;
* mana;
* max mana;
* alive;
* respawn timer;
* position;
* buyback cost;
* buyback cooldown;
* facet;
* Aghanim;
* shard;
* debuff states permitidos.

ITEMS:

* inventory;
* backpack;
* neutral;
* TP;
* cooldown;
* charges.

ABILITIES:

* levels;
* cooldowns;
* cast state cuando exista.

Confirmar contra documentación actual antes de implementar cada campo.

---

# STATE SNAPSHOT

Crear un esquema normalizado:

MatchState

Ejemplo conceptual:

match:
id
patch
game_time
score
side

player:
hero
role
level
XP
KDA
LH
denies
gold
GPM
XPM

inventory:
items
backpack
neutral

resources:
HP
mana

buyback:
available
cost
cooldown

map:
towers
barracks
day/night

observed:
visible_enemy_items
heroes
other_screen-derived data

predictions:
win_probability
next_item
fight_readiness
objective

Crear versionado del schema.

---

# MÓDULO 8 — OBSERVABLE STATE / COMPUTER VISION

Agregar únicamente información legítimamente visible.

Detectar:

* heroes;
* score;
* alive/dead states visibles;
* visible enemy items;
* selected unit items;
* kill feed cuando aporte valor;
* Roshan context visible;
* scoreboard data visible;
* minimap information únicamente si es visible al jugador.

NO inferir información oculta.

Pipeline:

Desktop Duplication API o Windows Graphics Capture
↓
crop región
↓
preprocessing
↓
classifier/detector
↓
confidence
↓
temporal smoothing
↓
state update

No almacenar screenshots por defecto.

Procesar en memoria.

Agregar setting opt-in para guardar frames de debugging.

---

# MÓDULO 9 — FEATURE ENGINE

Crear features de:

## Personal

* comfort;
* role experience;
* matchup experience;
* patch experience;
* recent form.

## Match

* time;
* KDA;
* score differential;
* level;
* XP;
* farm;
* GPM;
* XPM;
* gold;
* item timings;
* power spikes;
* buyback;
* towers;
* lineup.

## Derived

* estimated farm percentile;
* timing delay;
* item timing percentile;
* death cost;
* fight readiness;
* threat score;
* objective opportunity;
* farm deficit;
* farm surplus;
* scaling advantage;
* damage balance;
* disable imbalance.

Todas las features deben documentarse.

Crear un Feature Registry.

---

# MÓDULO 10 — WIN PROBABILITY MODEL

Objetivo:

P(win | información visible actualmente)

Evitar leakage.

IMPORTANTE:

No entrenar el live model con datos que el jugador no puede observar durante la partida.

Crear dos modelos:

## FullStateWinModel

Solo para:

* research;
* postgame;
* replay analytics.

Puede utilizar estado completo de la partida.

## PlayerVisibleWinModel

Producción live.

Solo puede usar datos conocidos legítimamente en ese instante.

Al crear dataset histórico, enmascarar información no observable.

Evaluar:

* Logistic Regression baseline;
* Random Forest baseline;
* XGBoost;
* LightGBM;
* CatBoost si aporta;
* temporal models después.

Métricas:

* ROC-AUC;
* PR-AUC;
* log loss;
* Brier Score;
* calibration error;
* calibration curve.

La calibración importa mucho.

Una predicción:

70%

debe significar aproximadamente 70% en grupos similares.

---

# MÓDULO 11 — ITEM RECOMMENDATION ENGINE

Este es uno de los componentes centrales.

NO recomendar items simplemente por winrate.

Evitar survivor bias.

Ejemplo:

Divine Rapier puede tener alto winrate porque se compra en contextos especiales.

Modelar:

P(next_item | current_state)

y, si es posible:

Expected Win Uplift(item | state)

Features:

* hero;
* role;
* facet;
* patch;
* rank;
* minute;
* level;
* gold;
* current items;
* item order;
* ally heroes;
* enemy heroes;
* visible enemy items;
* damage composition;
* disables;
* score;
* structures;
* farm;
* personal profile;
* comfort;
* power spike;
* objective state.

Baseline:

* frequency conditioned;
* rules;
* nearest historical states.

Segundo modelo:

* LightGBM ranker;
* LambdaMART.

Posteriormente:

* sequence Transformer;
* player/item embeddings.

Mostrar Top 3.

Ejemplo:

NEXT ITEM

BKB 86
Shiva 63
Linken 52

Cada recomendación debe incluir razón.

---

# ITEM EXPLANATION ENGINE

No dejar que un LLM invente la decisión.

Separar:

Decision Engine
→ produce datos estructurados.

Ejemplo:

item = BKB
score = .86

reasons:

* enemy_magic_burst = high
* hard_disable = high
* current_item_synergy = high
* timing = strong

Después un componente de lenguaje puede generar:

“Prioriza BKB: Lion e Invoker pueden impedir tu iniciación y retrasarlo debilitaría tu próximo power spike.”

El LLM explica.

El ML/reglas deciden.

---

# BUILD CHANGE DETECTOR

La recomendación debe cambiar cuando cambia el contexto.

Ejemplo:

Antes:

BKB.

Después aparece una amenaza diferente:

MKB.

Mostrar:

BUILD CHANGED

Previous:
BKB

New priority:
MKB

Reason:
enemy evasion became the primary threat.

Evitar cambios demasiado frecuentes.

Implementar hysteresis/debounce.

---

# MÓDULO 12 — THREAT ENGINE

Calcular amenazas enemigas.

ThreatScore por héroe visible/conocido.

Variables:

* kills;
* items visibles;
* hero scaling;
* damage type;
* control;
* ability to kill player hero;
* current timings;
* matchup;
* level cuando sea observable;
* role;
* player vulnerability.

No fabricar información no visible.

Mostrar máximo 1–3 amenazas.

---

# MÓDULO 13 — POWER SPIKE ENGINE

Detectar power spikes de:

* niveles;
* ultimates;
* talents;
* items;
* item combinations;
* shard;
* Aghanim.

Ejemplo:

Current:

Blink + Witch Blade

Next spike:

BKB

Missing:
1,247 gold

Current GPM:
512

Estimated:
~2:26

La estimación debe usar income reciente, no solo GPM global.

Crear rolling gold rate:

1 min
3 min
5 min.

---

# MÓDULO 14 — FARM OR FIGHT ENGINE

Generar:

FARM
NEUTRAL
FIGHT

o score 0–100.

Features:

* HP;
* mana;
* ult;
* cooldowns propios;
* gold unspent;
* distance to power spike;
* buyback;
* towers;
* score;
* visible deaths;
* objective opportunities;
* hero timing;
* farm dependency;
* enemy timing;
* ally readiness disponible legítimamente.

Ejemplo:

NO FIGHT

Reason:

BKB is 320 gold away.

Do:
1 wave + camp
→ buy BKB
→ fight.

No generar demasiados mensajes.

---

# MÓDULO 15 — BUYBACK ENGINE

Variables:

* current gold;
* buyback cost;
* cooldown;
* game minute;
* base state;
* respawn duration;
* item proximity;
* win probability;
* objective risk.

Recomendaciones:

BUYBACK AVAILABLE

KEEP 2,700 GOLD

o:

SAFE TO SPEND

No buyback needed under current risk.

Ser extremadamente conservador con afirmaciones.

---

# MÓDULO 16 — OBJECTIVE ENGINE

Después de pelea o ventaja:

Ranking:

1. Roshan
2. Tower
3. Barracks
4. Farm
5. Reset

Variables permitidas:

* visible hero deaths;
* respawn timers propios;
* structures;
* map state;
* time;
* team composition;
* Roshan capability;
* push capability;
* HP;
* mana;
* buyback own state;
* visible information.

No usar enemigos ocultos.

Mostrar:

BEST CONVERSION

Roshan 84
T2 Mid 73
Farm 31

---

# MÓDULO 17 — DEATH COST

Calcular qué tan costosa sería una muerte ahora.

Variables:

* respawn duration;
* buyback;
* buyback cost;
* game time;
* structures;
* current advantage;
* power spike;
* gold at risk;
* objectives.

Estados:

LOW
MEDIUM
HIGH
CRITICAL

Ejemplo:

CRITICAL

No buyback
47:18

Avoid showing alone.

---

# MÓDULO 18 — TEAMFIGHT PREP

NO intentar microgestionar habilidades durante una pelea.

Antes de la pelea mostrar como máximo:

Target
Avoid
Save

Ejemplo:

TARGET:
Lion

AVOID:
frontlining PA

SAVE:
BKB for initiation.

Usar audio solo si realmente ayuda.

---

# MÓDULO 19 — VOICE COACH

TTS corto.

Mensajes permitidos:

* “BKB en 400.”
* “Power spike listo.”
* “Buyback disponible.”
* “No tienes buyback.”
* “Evita pelea hasta BKB.”
* “Tres muertos. Busca objetivo.”

No generar discursos.

Crear prioridades:

P0:
audio + visual.

P1:
visual.

P2:
panel secundario.

Agregar cooldown para voz.

---

# MÓDULO 20 — OVERLAY UX

Principio:

El motor puede analizar 200 variables.

El jugador debe ver 1–3 decisiones.

Overlay principal:

DOTA COACH
────────────

Win: 64%

NEXT ITEM
BKB 86

POWER SPIKE
842g remaining

PLAN
FARM → BKB → FIGHT

BUYBACK
AVAILABLE

Nada más por defecto.

Permitir:

* compact mode;
* detailed mode;
* draggable;
* opacity;
* scale;
* monitor selection;
* corner selection;
* hotkey hide/show.

No tapar elementos importantes de Dota.

---

# MÓDULO 21 — POSTGAME ANALYZER

Después de terminar:

usar match_id.

Consultar datos disponibles.

Generar análisis más profundo.

Buscar:

* laning;
* farm;
* item timings;
* deaths;
* teamfights;
* objectives;
* buyback;
* itemization;
* power spikes;
* turning points.

Ejemplo:

LOSS

Maximum win probability:
71%

TURNING POINT:
34:12

Before:
68%

After:
42%

Likely issue:
death without buyback.

ITEMIZATION:

BKB recommended:
22:10

Purchased:
26:41

Delay:
+4:31

No afirmar causalidad sin suficiente evidencia.

Usar:

“associated with”

o

“likely contributed”

cuando corresponda.

---

# MÓDULO 22 — PERSONAL LEARNING

Crear historial de recomendaciones:

Recommendation

* match_id;
* timestamp;
* game_time;
* state snapshot;
* recommendation type;
* suggested action;
* alternatives;
* confidence;
* actual user action;
* delay;
* outcome;
* final match result.

Después evaluar:

¿qué recomendaciones suelen funcionar para este jugador?

Pero evitar:

post hoc fallacy.

No concluir:

“compraste BKB y ganaste, por eso BKB causó la victoria.”

Aplicar métodos estadísticos adecuados.

---

# MÓDULO 23 — WEEKLY PERSONAL REPORT

Ejemplo:

LAST 20 MATCHES

12 W
8 L

WHEN LH@10 > BASELINE
65% WR

WHEN >1 DEATH BEFORE 10
28% WR

BKB MEDIAN TIMING
24:42

YOUR BEST
22:51

BIGGEST LEAK

Deaths immediately before major item timings.

Generar máximo 3 insights accionables.

---

# MÓDULO 24 — RECOMMENDATION ORCHESTRATOR

Todos los modelos pueden recomendar cosas simultáneamente.

Necesitamos un sistema que decida qué mostrar.

Ejemplo:

Item engine:
Buy BKB.

Farm engine:
Farm.

Buyback engine:
Keep gold.

Objective engine:
Roshan.

No mostrar los cuatro necesariamente.

Crear Priority Score:

Impact
× Confidence
× Urgency
× Actionability

menos:

Cognitive Load
Message Recency
Conflict.

Crear reglas para resolver contradicciones.

---

# MÓDULO 25 — RECOMMENDATION CONFIDENCE

Cada recomendación debe incluir internamente:

* prediction score;
* confidence;
* data completeness;
* model version;
* source quality.

Si no tenemos suficiente información:

mostrar:

LOW CONFIDENCE

o no recomendar.

No fingir precisión.

---

# DATA MODEL

Diseñar entidades:

Player
PlayerProfile
PlayerHeroStats
PlayerRoleStats
PlayerPatchStats
Hero
HeroPatchStats
HeroMatchup
HeroSynergy
HeroFacet
Item
ItemPatchStats
Match
MatchPlayer
MatchTimeline
MatchState
ObservedState
Recommendation
RecommendationOutcome
ModelPrediction
ModelVersion
FairPlayFeature
PersonalBaseline

Crear ERD.

---

# DATA PIPELINE

Pipeline:

raw API/replays
↓
bronze
↓
cleaned
↓
normalized
↓
features
↓
training
↓
evaluation
↓
model registry
↓
ONNX
↓
desktop app

Usar reproducibilidad.

Cada dataset debe tener:

* dataset version;
* patch range;
* generated_at;
* feature schema;
* data source.

---

# DATA QUALITY

Validar:

* duplicate matches;
* abandoned games;
* turbo;
* custom games;
* bots;
* incomplete parses;
* smurfs no identificables;
* role mismatch;
* wrong patches;
* invalid items;
* missing timeline;
* public/private data.

Separar modos:

Ranked
Unranked
Turbo

No mezclarlos por defecto.

---

# TRAIN/VALIDATION/TEST

No hacer random split simple si genera temporal leakage.

Usar:

train:
pasado.

validation:
periodo posterior.

test:
periodo todavía posterior.

Además evaluar por:

* hero;
* bracket;
* patch;
* role.

Crear backtesting temporal.

---

# ML BASELINES

Antes de modelos complejos, crear baselines.

## Draft

* hero global winrate;
* matchup matrix;
* logistic regression;
* LightGBM.

## Items

* most common next item;
* hero + minute;
* hero + role + current item sequence;
* ranker.

## Win probability

* score difference baseline;
* logistic regression;
* LightGBM.

## Personalized hero selection

* global hero strength;
* comfort-only;
* combined.

Comparar.

---

# MODEL EXPLAINABILITY

Usar:

* SHAP offline;
* feature contributions;
* structured reason codes.

No ejecutar SHAP pesado continuamente live si afecta FPS.

Generar reason codes durante inference.

---

# PERFORMANCE REQUIREMENTS

La app no puede reducir FPS significativamente.

Metas iniciales:

GSI processing:
< 5 ms typical.

ML inference:
< 20 ms por actualización.

CV:
no necesita 60 FPS.

Ejecutar por ejemplo:

2–5 FPS

si es suficiente.

Overlay:
fluido.

CPU:
mínimo posible.

RAM:
objetivo razonable <500 MB para MVP, salvo razones justificadas.

GPU:
CV opcional.

Agregar benchmarks reales.

---

# EVENT-DRIVEN ARCHITECTURE

No recalcular todo con cada paquete GSI.

Eventos conceptuales:

HeroSelected
MatchStarted
ItemPurchased
LevelUp
Death
Respawn
GoldThresholdCrossed
PowerSpikeReached
TowerDestroyed
BuybackChanged
DraftChanged
EnemyItemObserved
MatchEnded

Cada engine se suscribe a eventos relevantes.

---

# STATE CHANGE DETECTION

Evitar spam.

Ejemplo:

BKB score:

81
82
83
82
84

No generar cinco mensajes.

Solo emitir si:

* cambia item recomendado;
* cambia mucho confidence;
* cruza un threshold;
* aparece nueva threat;
* power spike alcanza condición.

---

# CACHE

Cachear:

* hero metadata;
* item metadata;
* patch metadata;
* matchups;
* synergies;
* player historical stats.

TTL según dato.

La app debe seguir funcionando offline durante la partida.

No depender de internet para funciones críticas live.

---

# OFFLINE-FIRST

Antes del match descargar/cachear:

* player profile;
* hero stats;
* current patch;
* item knowledge;
* model files.

Durante match:

Dota
→ local app
→ local ML
→ overlay.

Internet no debe ser obligatorio.

---

# SECURITY

Steam authentication:

usar Steam OpenID.

Nunca pedir:

* contraseña Steam;
* Steam Guard code;
* session cookies manuales.

Datos locales:

* encryption si es necesario;
* secrets fuera del repo;
* API keys en secure storage.

Crear:

.env.example

pero nunca commitear secretos.

---

# PRIVACY

Por defecto:

* no screenshots guardados;
* no audio grabado;
* no keylogging;
* no process memory;
* no intrusive telemetry.

Telemetry opt-in.

Explicar claramente al usuario qué datos se guardan.

---

# LOGGING

Structured logging.

Campos:

timestamp
match_id
event
module
duration
model_version
error

No loggear información sensible innecesariamente.

Rotating logs.

---

# TESTING

Crear:

## Unit tests

* feature calculations;
* power spikes;
* item rules;
* comfort;
* patch weighting;
* FairPlayPolicy.

## Integration

* GSI payload fixtures;
* API clients;
* database;
* ONNX model.

## CV

dataset con distintas:

* resoluciones;
* UI scale;
* heroes;
* patches.

## ML

* leakage tests;
* feature availability tests;
* calibration;
* reproducibility.

## E2E

simulador de partida mediante replay de payloads GSI.

---

# GSI REPLAY SIMULATOR

Crear una herramienta interna.

Debe permitir grabar/simular secuencias de estados GSI.

Ejemplo:

fixtures/match_001/

000001.json
000002.json
...

Luego:

ReplayRunner

para desarrollar sin tener Dota abierto.

Debe poder reproducir:

0.5x
1x
2x
10x.

---

# FAIR PLAY TESTS

Crear tests que fallen si un live feature usa información marcada como:

PostGameOnly
SpectatorOnly
HiddenFromPlayer

Ejemplo:

AssertLiveFeatureAllowed(featureName)

Esto es obligatorio.

---

# OBSERVABILITY

Panel de desarrollo con:

* GSI state;
* CV detections;
* feature values;
* model output;
* recommendation candidates;
* reason codes;
* FPS;
* latency.

Solo modo dev.

---

# CONFIGURACIÓN

settings:

General
Overlay
Voice
ComputerVision
Privacy
DataSources
Models
Advanced

Agregar “Safe Mode”:

Solo GSI + información propia.

CV apagado.

---

# MODO DE DESARROLLO

Crear una app adicional o sección:

Developer Console

Mostrar:

LiveState
VisibleState
CurrentFeatures
CandidateRecommendations
SelectedRecommendation
FairPlayStatus

Esto facilitará debug.

---

# UI PRINCIPAL

Pantallas:

1. Onboarding
2. Steam connection
3. Player dashboard
4. Queue preparation
5. Hero pool
6. Live overlay
7. Match history
8. Match analysis
9. Personal Dota DNA
10. Insights
11. Settings
12. Developer mode

---

# ONBOARDING

Explicar:

1. conectar Steam;
2. configurar GSI;
3. seleccionar rol principal;
4. importar historial;
5. calibrar overlay;
6. opcionalmente calibrar CV.

Mostrar claramente:

“This application does not read Dota process memory and does not reveal hidden game information.”

---

# DESIGN

Look:

dark gaming UI.

No copiar literalmente la interfaz de Valve.

Usar:

* fondo oscuro;
* tipografía altamente legible;
* información jerárquica;
* pocos colores.

Semántica:

green:
ready/good.

yellow:
attention.

red:
critical.

blue/purple:
informational.

Pero garantizar accesibilidad para daltonismo.

No depender únicamente de color.

---

# PRODUCT PRINCIPLE

No convertir al jugador en esclavo del overlay.

Meta:

el usuario debe aprender.

Cada recomendación debe enseñar progresivamente.

Pero durante la partida:

máxima concisión.

Postgame:

explicaciones profundas.

---

# TIPO DE RECOMENDACIONES

Crear enum:

HeroPick
StartingItems
SkillBuild
Talent
NextItem
BuildChange
Farm
Fight
PowerSpike
Buyback
Objective
Risk
Threat
Lane
Teamfight
Positioning
PostGameInsight

---

# MODO DE EXPLICACIÓN

Live:

máximo aproximadamente 10–20 palabras.

Postgame:

explicación completa.

Ejemplo live:

“BKB first: Lion + Invoker can stop your initiation.”

Postgame:

explicar contexto estadístico.

---

# CONDICIÓN DE VICTORIA

Crear módulo para estimar:

our_win_condition
enemy_win_condition

Ejemplos estructurados:

ours:
protect carry until timing;
take Roshan;
force teamfights.

enemy:
PA reaches 3 items;
split push;
long fights.

No usar un LLM como única fuente.

Usar reglas + composition features + model.

---

# DIFFICULTY / EXECUTION SCORE

Una recomendación óptima teóricamente puede ser demasiado difícil para el jugador.

Crear ExecutionDifficulty.

Personalizar.

Ejemplo:

Meepo theoretically:
82

pero personal comfort:
22.

Puck:
79

comfort:
91.

Resultado:
Puck recomendado.

---

# PERSONALIZED EXPECTED SUCCESS

Conceptualmente:

ExpectedSuccess =
MetaStrength

* DraftFit
* LaneFit
* PersonalComfort
* RecentPerformance
* RoleExperience
* ExecutionFit
* CurrentGameState

No fijar los pesos manualmente permanentemente.

Usar ML para aprenderlos.

---

# CONTRAFACTUALIDAD

Objetivo futuro:

estimar:

¿Qué habría pasado estadísticamente si hubiera comprado BKB en lugar de Aghanim?

No afirmar causalidad sin técnica.

Investigar:

* inverse propensity weighting;
* causal forests;
* doubly robust estimation;
* uplift modeling.

No incluir esto en MVP si no es confiable.

---

# FASES DE DESARROLLO

## FASE 0 — Research & foundation

Entregables:

* investigación APIs;
* documentación GSI;
* FairPlayPolicy;
* arquitectura;
* ADRs;
* dataset exploration;
* OpenDota client;
* hero/item metadata;
* schema.

## FASE 1 — Data platform

* match downloader;
* caching;
* normalization;
* DuckDB;
* data quality;
* patch filtering.

## FASE 2 — Player profile

* Steam login;
* profile;
* hero stats;
* role stats;
* comfort;
* Dota DNA v1.

## FASE 3 — ML offline

* draft baseline;
* win probability baseline;
* item recommendation baseline.

## FASE 4 — Desktop MVP

* overlay;
* GSI listener;
* live state;
* item recommendations;
* power spike;
* buyback;
* farm/fight.

## FASE 5 — Draft CV

* screen capture;
* hero detection;
* draft analysis.

## FASE 6 — Postgame

* match analysis;
* turning points;
* item timing;
* personalized insights.

## FASE 7 — Advanced personalization

* recommendation outcomes;
* personal models;
* bracket models.

## FASE 8 — Macro coach

* objective engine;
* Roshan;
* highground;
* death cost.

## FASE 9 — Advanced CV

* enemy visible items;
* scoreboard;
* selected units;
* kill feed.

## FASE 10 — Refinement

* ONNX optimization;
* latency;
* installer;
* auto updater;
* UX polish.

---

# MVP ESTRICTO

No intentar construir todo a la vez.

El MVP debe contener:

1. Windows desktop app.
2. GSI integration.
3. Steam account linking.
4. Player profile.
5. Hero comfort.
6. Current patch cache.
7. Match detection.
8. Own hero detection through GSI.
9. Own KDA.
10. Own gold.
11. GPM/XPM.
12. LH/denies.
13. own inventory.
14. ability levels.
15. buyback.
16. buildings/state.
17. next item recommendation.
18. power spike.
19. farm/fight recommendation.
20. compact overlay.
21. match end detection.
22. postgame data ingestion.
23. recommendation logging.

Para Draft MVP:

permitir inicialmente que el usuario seleccione lineup manualmente si CV todavía no está listo.

Después automatizarlo.

---

# RESEARCH TASKS INICIALES

Antes de escribir mucho código, verificar con fuentes actuales:

1. Game State Integration actual de Dota 2.
2. Campos disponibles mientras se juega.
3. Diferencias playing vs spectating.
4. OpenDota API actual.
5. Rate limits.
6. STRATZ API actual.
7. Steam authentication actual.
8. políticas públicas relevantes de Valve sobre third-party tools.
9. disponibilidad de rank data.
10. disponibilidad de replay/timeline.
11. cambios recientes de Dota que afecten facets/items/draft.

Documentar enlaces y fecha de verificación.

No confiar solamente en conocimiento previo.

---

# DOCUMENTACIÓN OBLIGATORIA

Crear:

README.md

docs/PRODUCT.md
docs/ARCHITECTURE.md
docs/FAIR_PLAY.md
docs/DATA_SOURCES.md
docs/ML.md
docs/FEATURES.md
docs/GSI.md
docs/CV.md
docs/PRIVACY.md
docs/SECURITY.md
docs/TESTING.md
docs/ROADMAP.md

Y ADRs:

ADR-001 Desktop technology
ADR-002 Storage
ADR-003 ML inference
ADR-004 Fair play data model
ADR-005 CV strategy
ADR-006 OpenDota/STRATZ
ADR-007 Model versioning

---

# CODING STANDARDS

C#:

* nullable enabled;
* analyzers;
* async/await correcto;
* dependency injection;
* domain-oriented architecture;
* no God classes;
* records donde correspondan;
* cancellation tokens;
* structured logging.

Python:

* Python 3.12+;
* type hints;
* Ruff;
* Black;
* pytest;
* Pydantic;
* uv o Poetry;
* reproducible environments.

Frontend:

si existe XAML, mantener componentes reutilizables.

---

# CI/CD

GitHub Actions:

* build .NET;
* run tests;
* Python tests;
* lint;
* ML feature tests;
* packaging.

Posteriormente:

* signed installer;
* release artifacts.

No publicar automáticamente hasta que el proyecto esté estable.

---

# VERSIONADO DE MODELOS

Cada modelo:

model_name
version
training_dataset
patch_range
training_date
feature_schema
metrics
checksum

La desktop app debe saber qué versión utiliza.

---

# FALLBACKS

Si OpenDota falla:

usar cache.

Si STRATZ falla:

usar OpenDota/cache.

Si Internet falla:

live game debe continuar.

Si CV falla:

usar GSI.

Si modelo falla:

usar reglas.

Nunca dejar overlay inutilizable por una dependencia remota.

---

# RULE ENGINE

Construir reglas como respaldo.

Ejemplos conceptuales:

IF enemy_evasion_high
AND hero_uses_right_click
THEN MKB candidate += x

IF magic_disable_threat_high
AND BKB viable
THEN BKB += x

IF high_healing_enemy
THEN anti_heal candidates += x

IF close_to_major_item
AND current_gold_gap < threshold
THEN farm_before_fight.

Las reglas deben estar versionadas y testeadas.

No hardcodear lógica dispersa por toda la app.

---

# ITEM KNOWLEDGE GRAPH

Crear relaciones:

Item
→ counters
→ synergies
→ hero archetypes
→ threats
→ timing.

Ejemplo:

BKB

counters:
magic burst
many disables

synergies:
initiators
right-click cores

weakness:
physical burst remains.

Esto puede complementar ML.

---

# HERO KNOWLEDGE GRAPH

Hero:

damage profile
control
mobility
farm pattern
power spikes
counters
synergies
objective capability
scaling
execution difficulty.

Actualizar por parche.

---

# PERSONAL BENCHMARKS

Nunca comparar únicamente contra pros.

Comparar:

player
vs
same hero
same role
same bracket
same patch.

Mostrar percentiles.

Ejemplo:

GPM:
61st percentile.

LH@10:
43rd percentile.

Deaths:
28th percentile.

---

# TURNING POINT DETECTOR

Postgame:

buscar cambios grandes en win probability.

Ejemplo:

68%
→
41%

en 40 segundos.

Analizar evento.

Puede ser:

* death;
* lost fight;
* tower;
* Roshan;
* buyback;
* item timing.

Mostrar 1–3 turning points.

---

# INSIGHT QUALITY

Cada insight debe pasar filtros:

* statistically meaningful;
* actionable;
* not obvious;
* sufficient sample.

No mostrar:

“You win more when you have more kills.”

Sí mostrar:

“Across 42 Puck games, delaying your first defensive item beyond minute 25 is associated with a 14-point lower win rate.”

Si sample bajo:

“No hay suficientes partidas para una conclusión sólida.”

---

# SAMPLE SIZE

Siempre registrar N.

Ejemplo:

12 matches

LOW CONFIDENCE.

87 matches

MODERATE.

400 matches

HIGHER.

No inventar confidence exclusivamente a partir de N; combinar con variance.

---

# UI DE POSTGAME

Secciones:

Result
Turning Point
Lane
Farm
Items
Deaths
Objectives
Build decisions
Top improvement
What you did well

No crear una pantalla negativa.

El objetivo es mejora.

---

# ÉTICA DEL PRODUCTO

No crear mensajes manipuladores.

No incentivar sesiones infinitas.

No usar dark patterns.

La app es coach, no casino.

---

# DEFINITION OF DONE DEL MVP

Se considera MVP funcional cuando:

1. instala en Windows;
2. detecta Dota;
3. configura GSI;
4. detecta una partida;
5. detecta tu héroe;
6. lee estado propio;
7. mantiene state actualizado;
8. muestra overlay;
9. recomienda siguiente item;
10. calcula power spike;
11. indica buyback;
12. recomienda farm/fight;
13. registra recomendaciones;
14. detecta final;
15. importa análisis postgame;
16. funciona sin leer memoria;
17. pasa FairPlay tests;
18. tiene tests;
19. tiene documentación.

---

# RESULTADO QUE ESPERO DE TI

No quiero únicamente explicaciones.

Quiero que trabajes como un agente de ingeniería real.

Tu proceso debe ser:

## PASO 1

Inspeccionar el repositorio actual.

Si está vacío, inicializarlo correctamente.

## PASO 2

Investigar las fuentes actuales necesarias.

## PASO 3

Crear:

docs/RESEARCH.md

con hallazgos y decisiones.

## PASO 4

Crear arquitectura.

## PASO 5

Crear ROADMAP técnico con tareas.

## PASO 6

Implementar el esqueleto completo del proyecto.

## PASO 7

Implementar primero el MVP vertical.

Flujo mínimo:

Dota GSI
→ State Engine
→ Rule Engine
→ Recommendation
→ Overlay.

## PASO 8

Agregar persistencia.

## PASO 9

Agregar player profile.

## PASO 10

Agregar data ingestion.

## PASO 11

Crear ML baselines.

## PASO 12

Integrar ONNX.

## PASO 13

Agregar postgame.

## PASO 14

Agregar CV.

No saltar directamente a CV o Transformers.

---

# FORMA DE TRABAJAR

Antes de realizar cambios grandes:

1. inspecciona;
2. entiende;
3. diseña;
4. implementa;
5. ejecuta tests;
6. corrige;
7. documenta.

No crear código ficticio que no compile.

No decir:

“aquí iría...”

si puedes implementarlo.

No dejar TODOs genéricos salvo que exista una razón documentada.

Después de cada fase:

* ejecutar build;
* ejecutar tests;
* mostrar errores;
* corregirlos.

---

# AUTONOMÍA

Tienes permiso para tomar decisiones razonables de arquitectura sin preguntarme continuamente.

Si existen varias opciones:

1. compáralas brevemente;
2. elige una;
3. documenta la decisión en ADR.

Solo pregunta si hay una decisión realmente bloqueante que no pueda inferirse.

---

# PRIMER OBJETIVO INMEDIATO

Empieza realizando lo siguiente:

1. inspecciona el repositorio;
2. investiga el estado actual de:

   * Dota 2 GSI;
   * OpenDota;
   * STRATZ;
   * Steam OpenID;
   * políticas Valve relevantes;
3. crea docs/RESEARCH.md;
4. crea docs/FAIR_PLAY.md;
5. propone la arquitectura definitiva;
6. define el monorepo;
7. crea los proyectos base;
8. implementa un GSI listener real;
9. crea un MatchState;
10. crea un GSI replay simulator;
11. crea tests;
12. crea un overlay mínimo que pueda mostrar:

    * hero;
    * tiempo;
    * KDA;
    * gold;
    * GPM;
    * XPM;
    * LH;
    * current items;
    * buyback status;
13. crea un RuleRecommendationEngine inicial;
14. implementar al menos reglas básicas para:

    * next item;
    * close-to-item farm;
    * buyback warning;
    * power spike;
15. documenta cómo ejecutar todo.

Al finalizar esta primera fase, dame:

* archivos creados;
* decisiones tomadas;
* comandos de ejecución;
* estado del build;
* tests;
* limitaciones;
* próximo bloque recomendado.

El proyecto debe quedar ejecutable, no solamente diseñado.
