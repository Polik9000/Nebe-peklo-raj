# Nebe Peklo Ráj

FKT výzva z Nebeské Rybné přes Peklo do Českého ráje (cca 163 km). Spuštění květen 2027.

Statický web (čisté HTML/CSS, bez buildu), hostovaný přes GitHub Pages. Později poběží na ASP.NET a soubory se přesunou do `wwwroot`.

## Stránky

| Soubor | Obsah |
|---|---|
| `index.html` | úvod, varianty, jak to funguje, časté otázky, waitlist (Tally) |
| `trasa.html` | body trasy se souřadnicemi, mapa a výškový profil, etapy, koridor, bezpečnost |
| `pravidla.html` | pravidla v1: čas, styly, pauzy, GPX, všechny otázky |
| `zebricek.html` | připravujeme (spuštění květen 2027) |
| `kontakt.html` | kdo za tím stojí, kontakt, info pro média |
| `ochrana-udaju.html` | zásady ochrany osobních údajů k waitlistu |
| `404.html` | stránka nenalezena |

Hlavička a patička jsou v každé stránce zkopírované. Při změně menu je potřeba upravit všechny stránky.

Mapa a výškový profil na `trasa.html` jsou SVG vygenerované z předběžného GPX. Po finální úpravě trasy je potřeba je vygenerovat znovu.

## GPX parser

Kontrola nahraných pokusů je C# knihovna (.NET 8), oddělená od webu:

| Složka | Obsah |
|---|---|
| `src/NebePekloRaj.GpxValidation` | načtení GPX, kontroly pravidel, výpočet časů |
| `tests/NebePekloRaj.GpxValidation.Tests` | unit testy, i na předběžné trase |
| `data/trasa.gpx` | oficiální trasa (zatím předběžná) |
| `data/challenge.json` | souřadnice startu, Pekla a cíle, zóny, všechny limity, datum spuštění a verze pravidel |

Posun cíle nebo nová trasa = upravit `data/challenge.json` nebo nahradit `data/trasa.gpx`, kód se nemění. Zadání kontrol je v dokumentu [GPX parser – zadání](https://claude.ai/artifact/HLPhBwi1GfbCPqk4sHFbwi).

Použití:

```csharp
var (config, route) = ChallengeConfig.LoadWithRoute("data/challenge.json");
var validator = new AttemptValidator(config, route);
var result = validator.Validate(
    [File.OpenRead("hodinky.gpx"), File.OpenRead("telefon.gpx")],
    RouteVariant.Full,
    new ValidationOptions(
        KnownAttempts: existing,                        // Id, Fingerprint, start, cíl a uložený Shape
        IsRecordTime: (variant, time) => isFirst(variant, style, category, time),
        DetourExplained: runnerSentNote));             // objížďka uzavírky jde k člověku místo zamítnutí
// result.Verdict: Accepted / ManualReview / Rejected
// result.Elapsed, result.Stages, result.Findings, result.Fingerprint
// result.AttemptDateUtc (odchod ze startovní zóny), result.RulesVersion
// result.RecordVariants: varianty, kde by čas byl rekord (i etapa z celé trasy)
// result.Shape.Encode(): tvar stopy, ulož k pokusu kvůli hledání kopií
// result.Stage1Fallback: Etapa 1 z nedokončeného pokusu o celou trasu
```

Co kontrola pozná a co ne:

| Pravidlo | Automat | Člověk |
|---|---|---|
| Auto, vlak (záznam běží) | rychlost nad 30 km/h na 500 m = zamítnutí | – |
| Kolo, podezřele rychlý úsek | nad 18 km/h na 3 km, nebo víc než 2× vlastní tempo (aspoň 12 km/h) | posoudí |
| Kopie cizího záznamu | stejný soubor = zamítnutí; stejný tvar stopy a mezičasy = ruční kontrola | posoudí |
| Zkratka mimo koridor | zamítnutí; s vysvětlením (uzavírka) ruční kontrola | posoudí |
| Zkratky v koridoru | u rekordu opakované nebo jedna nad 400 m | posoudí |
| Odvoz autem při vypnutém záznamu | nepozná; u rekordu každá mezera od 30 min jde k člověku | zeptá se na nocleh |
| Rekord (i etapy z celé trasy) | pozná přes `IsRecordTime` | schvaluje vždy |
| Víc souborů | vždy k člověku; soubory ze stejné doby daleko od sebe = upozornění | posoudí |
| Styl (unsupported…), skupina, pacer, věk | nepozná | jen podle poctivosti a namátkou |

Testy: `dotnet test` v kořeni repa.

## Jak vložit Tally formulář

1. Založ si účet na [tally.so](https://tally.so) (zdarma).
2. **Create form** → přidej pole **Email** (nastav jako povinné). Volitelně jméno.
3. Na konec přidej pole **Checkbox** se souhlasem, např. „Souhlasím se zasíláním informací o spuštění výzvy.“ a nastav ho jako povinné.
4. **Publish** → **Share** → **Embed** → zvol **Standard** a zapni *Transparent background* a *Dynamic height*.
5. Zkopíruj kód (`<iframe …>` a `<script …>`).
6. V `index.html` najdi komentář `TALLY` a celý blok `<div class="form-placeholder">…</div>` nahraď zkopírovaným kódem.

Odpovědi pak najdeš v Tally v záložce **Submissions** a jdou exportovat do CSV.

## Jak to zveřejnit přes GitHub Pages

Repo → **Settings** → **Pages** → *Source: Deploy from a branch* → větev `main`, složka `/ (root)` → **Save**. Za pár minut bude web na `https://polik9000.github.io/Nebe-peklo-raj/`.

## Design

Styl je schválně neutrální. Všechny barvy, písma, velikosti, rozestupy, zaoblení a stíny jsou proměnné v `:root` na začátku `assets/styles.css`. Návod, kterou proměnnou změnit podle Figmy, je v komentáři na začátku souboru. Třídy v HTML jsou pojmenované podle obsahu (`hero`, `route`, `variants`, `card`, …).
