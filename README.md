# Nebe Peklo Ráj

FKT výzva z Nebeské Rybné přes Peklo do Českého ráje (cca 163 km). Spuštění květen 2027.

Zatím tu je statická landing page s waitlistem (`index.html` + `assets/styles.css`). Později web poběží na ASP.NET a tahle stránka se přesune do `wwwroot`.

## Jak vložit Tally formulář

1. Založ si účet na [tally.so](https://tally.so) (zdarma).
2. **Create form** → přidej pole **Email** (nastav jako povinné). Volitelně jméno.
3. Na konec přidej pole **Checkbox** se souhlasem, např. „Souhlasím se zasíláním informací o spuštění výzvy.“ a nastav ho jako povinné.
4. **Publish** → **Share** → **Embed** → zvol **Standard** a zapni *Transparent background* a *Dynamic height*.
5. Zkopíruj kód (`<iframe …>` a `<script …>`).
6. V `index.html` najdi komentář `TALLY` a celý blok `<div class="form-placeholder">…</div>` nahraď zkopírovaným kódem.
7. V `index.html` doplň do GDPR poznámky své jméno (místo `JMÉNO PŘÍJMENÍ`).

Odpovědi pak najdeš v Tally v záložce **Submissions** a jdou exportovat do CSV.

## Jak to zveřejnit přes GitHub Pages

Repo → **Settings** → **Pages** → *Source: Deploy from a branch* → větev `main`, složka `/ (root)` → **Save**. Za pár minut bude web na `https://polik9000.github.io/Nebe-peklo-raj/`.

## Design

Styl je schválně neutrální. Barvy, písma a rozestupy jsou jako proměnné v `:root` na začátku `assets/styles.css`, takže design z Figmy se dá převzít hlavně tam. Třídy v HTML jsou pojmenované podle obsahu (`hero`, `route`, `facts`, …).
