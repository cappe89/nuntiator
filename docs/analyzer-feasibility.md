# Valutazione di fattibilità: Roslyn Analyzer / Source Generator per Nuntiator

Questo documento valuta l'introduzione di strumenti di compile-time (analyzer diagnostici e/o source generator) per Nuntiator. **Nessun codice è stato scritto in questa iterazione** — è una valutazione di fattibilità, costi/benefici e raccomandazione.

## 1. Problema che si vuole risolvere

Attualmente molte classi di errore di configurazione (handler mancante, handler duplicato, middleware che non implementa l'interfaccia corretta) vengono rilevate solo a runtime, nel momento in cui:
- viene chiamato `AddNuntiator` (con la validazione eager introdotta in questa iterazione), oppure
- viene inviato un comando specifico tramite `SendAsync`.

Un Roslyn Analyzer potrebbe spostare parte di questi controlli al momento della compilazione, dando un feedback immediato nell'IDE (squiggles, Error List) invece che al primo avvio dell'applicazione o alla prima richiesta.

## 2. Candidati diagnostici per un Analyzer "semplice"

Un analyzer di tipo `DiagnosticAnalyzer` (senza modifica del codice, solo diagnostica) potrebbe intercettare, staticamente, scenari come:

| Diagnostica | Descrizione | Fattibilità |
|---|---|---|
| `NUNT001` | Una classe che implementa `ICommandMiddleware<TCommand,TResponse>` o `IPipelineBehavior<TCommand,TResponse>` con generici chiusi ma mai registrata tramite `AddMiddleware`/`AddOpenMiddleware` nel progetto | Media — richiede analisi cross-file delle chiamate a `AddNuntiator`, fragile con configurazioni dinamiche |
| `NUNT002` | Un tipo passato a `AddMiddleware(typeof(X))` che non implementa `ICommandMiddleware<,>` o `IPipelineBehavior<,>` | **Alta** — analisi locale della call-site, nessuna necessità di cross-project analysis |
| `NUNT003` | Più classi `ICommandHandler<TCommand,TResponse>` per lo stesso `TCommand` nello stesso progetto (se la registrazione è sempre per scansione automatica) | Media — richiede di correlare tutte le implementazioni nel Compilation, possibile ma con falsi positivi se si usano factory custom o esclusioni manuali |
| `NUNT004` | Nessuna classe implementa `ICommandHandler<TCommand,...>` per un comando definito nel progetto | Bassa/Media — alto rischio di falsi positivi (l'handler potrebbe essere in un altro progetto/assembly non ancora compilato insieme) |

Le diagnostiche **NUNT002** e, con cautela, **NUNT003**, sono le più fattibili con un rischio di falsi positivi contenuto. **NUNT001** e **NUNT004** richiedono euristiche più complesse e rischiano di generare rumore.

## 3. Costo stimato per un analyzer diagnostico semplice

- Nuovo progetto `Nuntiator.Analyzers` (netstandard2.0, richiesto per compatibilità con host Roslyn/VS).
- Dipendenze: `Microsoft.CodeAnalysis.CSharp` (versione allineata al Roslyn SDK supportato da VS/MSBuild in uso).
- Packaging: l'analyzer va distribuito come dipendenza di `analyzers` all'interno del pacchetto NuGet `Nuntiator` (cartella `analyzers/dotnet/cs` nel `.nupkg`), così da essere "silenzioso" a runtime (nessuna dipendenza aggiuntiva nel grafo di dipendenze dell'app).
- Test: richiede `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` o framework equivalente, con una suite dedicata di snippet di codice da verificare.
- Stima indicativa: 3-5 giorni/persona per NUNT002 + NUNT003 con test, documentazione, e integrazione nel pacchetto NuGet.

## 4. Source Generator (eliminazione della reflection a runtime)

Un **Incremental Source Generator** potrebbe generare a compile-time:
- Le registrazioni di handler/middleware (sostituendo `RegisterDiscoveredServices`), eliminando `Assembly.GetTypes()` e la cache di scansione introdotta in questa iterazione.
- Gli invoker della pipeline (sostituendo `PipelineInvokerCache`/`Activator.CreateInstance` con codice generato strongly-typed), migliorando le performance e la compatibilità con Native AOT/trimming.

### Benefici
- Nessuna reflection a runtime → migliori performance di startup e compatibilità con trimming/AOT.
- Errori di configurazione (handler mancanti/duplicati) diventano **errori di compilazione**, non eccezioni a runtime.

### Rischi e complessità
- Un Incremental Generator è significativamente più complesso di un analyzer diagnostico: richiede la gestione della `IncrementalGeneratorInitializationContext`, caching dei pipeline di generazione, e attenzione alle performance IDE (rigenerazione ad ogni keystroke).
- Cambia il modello di registrazione attuale (`AddNuntiator` con scansione assembly dinamica) in un modello ibrido: bisognerebbe mantenere compatibilità all'indietro con lo scanning a runtime per gli scenari plugin/multi-assembly (dove gli handler non sono noti al momento della compilazione del progetto che chiama `AddNuntiator`).
- Necessita di una strategia chiara per progetti multi-assembly (il generator vede solo il progetto corrente; se gli handler sono in un assembly separato, serve comunque uno scan a runtime o un meccanismo di "aggregazione" tra generator di progetti diversi).
- Manutenzione futura più onerosa: ogni nuova funzionalità (es. nuovo tipo di pipeline, notifiche) richiede aggiornamenti sia al generator sia al runtime library.
- Stima indicativa: 3-4 settimane/persona per un MVP funzionante con test, più tempo per gestire gli edge case multi-assembly.

## 5. Raccomandazione

1. **Breve termine**: non investire ancora nel source generator. La complessità e il rischio di regressioni (specialmente per scenari multi-assembly, già supportati oggi) superano il beneficio immediato, soprattutto ora che la cache di scanning (introdotta in questa iterazione) e la validazione eager allo startup già mitigano i due problemi principali (costo della reflection ripetuta e mancata diagnosi precoce degli errori di configurazione).
2. **Medio termine**: valutare un analyzer diagnostico "leggero" limitato a `NUNT002` (tipo non valido passato a `AddMiddleware`) come primo passo a basso rischio, distribuito tramite il pacchetto NuGet esistente. Questo fornisce feedback immediato in IDE senza alterare il modello di esecuzione a runtime.
3. **Lungo termine**: se il progetto cresce e la registrazione tramite scansione assembly diventa un collo di bottiglia reale (misurato, non ipotizzato) o è richiesto il supporto Native AOT, rivalutare il source generator con una due-diligence dedicata sul modello multi-assembly.

In sintesi: **procedere con cautela, partendo da un analyzer diagnostico mirato solo dopo aver raccolto feedback d'uso**; rimandare il source generator a una fase successiva con requisiti più chiari (es. richiesta esplicita di supporto AOT/trimming).
