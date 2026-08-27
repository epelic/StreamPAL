# Architettura StreamPAL

## Principio

Ogni istanza apre una sorgente unica e crea un fan-out verso piu encoder. Ogni encoder e' una pipeline isolata con coda limitata. Un rallentamento di rete o una
riconnessione non puo' bloccare acquisizione e altri encoder. I buffer vengono riutilizzati
per evitare pause del garbage collector.

## Moduli previsti

1. `SourceHub`: WASAPI shared/exclusive e loopback, DirectSound, ASIO multicanale, file, URL.
2. `ChannelRouter`: stereo, sinistro duplicato, destro duplicato, downmix mono.
3. `AudioProcessor`: conversione float32, resampling e limiter di sicurezza.
4. `CodecWorker`: MP3, AAC-LC, HE-AAC/AAC+, Ogg Vorbis, caricati come moduli.
5. `BroadcastSink`: Icecast 2 e SHOUTcast v1/v2, TLS, timeout e backoff con jitter.
6. `MetadataBus`: manuale, file TXT/XML/JSON, URL HTTP e API locale.
7. `Supervisor`: avvio/arresto indipendente, telemetria, log circolare e watchdog.

La sorgente fisica della singola istanza viene aperta una sola volta e alimenta tutte le sue pipeline tramite fan-out. Ogni uscita puo' avere routing canali, codec, bitrate, sample rate, server e metadata differenti. Istanze diverse restano completamente indipendenti.

## Distribuzione

Applicazione .NET self-contained x64 e installer firmabile. Le librerie codec native saranno
incluse nell'installer con licenze e avvisi richiesti. HE-AAC richiede una scelta esplicita del
codec/distributore e una verifica licenze prima della release commerciale.
