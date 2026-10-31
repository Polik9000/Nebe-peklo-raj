> Poznámka: bod A3 (čísla) je vyřešený. Web používá čísla z předběžného GPX (163 / 48 / 114 km, +3 900 m), která nahrazují starší odhady z pravidel v1.

# Nebe Peklo Ráj – právní kontrola webu (stav 31. 10. 2026)

> **Nejsem advokát.** Jde o rešerši a návrh textů, ne o právní radu. Před spuštěním plateb (květen 2027) ať to zkontroluje český advokát. Kde jsem si nebyl jistý nebo zdroj nebyl dostupný, je to označené **[ověřit]**.

## 1. Zjištění podle závažnosti

### A. Opravit hned
1. **Věta „e-mail nikomu nepředáme“ pod formulářem není přesná.** Údaje dostává zpracovatel Tally, IP adresy vidí GitHub a Cloudflare (subdodavatel Tally). Informace musí být přesné (čl. 5 odst. 1 písm. a), čl. 13 GDPR). Lepší je: „neprodáváme, nepředáváme pro marketing třetích stran“.
2. **Zásady ochrany údajů neplní všechny body čl. 13 GDPR.** Chybí: GitHub Pages a logování IP, předávání mimo EU (US subdodavatelé), konkrétní doba uložení, právo na přenositelnost a námitku, věkový limit, poznámka, že poskytnutí údajů je dobrovolné. Text k vložení je v části 2.
   - GitHub sám uvádí: „the visitor's IP address is logged and stored for security purposes“ – https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages
   - GitHub je certifikován v EU-US DPF – https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement
3. **Čísla na webu nesedí s fakty.** index.html, trasa.html, pravidla.html a kontakt.html uvádějí +3 900 m, Etapu 1 cca 48 km a Etapu 2 cca 114 km. facts.md uvádí +3 481 m, 45 km a 118 km. Jde o budoucí placenou službu, takže nepřesný popis může být klamavá obchodní praktika (§ 5 zák. 634/1992 Sb.). Sjednoť to na jeden zdroj, ideálně na finální GPX.
4. **Cíl je zřejmě v Prachovských skalách [ověřit souřadnice].** Bod 50.46670 N, 15.28963 E a název „Vyhlídka Český ráj“ odpovídají vyhlídce v Prachovských skalách. Ty jsou soukromé (rodina Schliků), vstup je placený (150–200 Kč), otevírací doba 7–19 h, z okruhů je nutné odejít do 20:00, pes platí poplatek a musí být po celé ploše na vodítku (https://loudavymkrokem.cz/en/?p=388029). Jde také o přírodní rezervaci (https://www.ziva.avcr.cz/2025-6/prachovske-skaly-nejstarsi-prirodni-rezervace-ceskeho-raje.html).
   - Web přitom říká „vyraz kdykoli, i v noci“. Bez dohody se správou areálu by výzva vedla lidi k nočnímu vstupu bez placení.
   - Řešení: (a) dohodnout se se správou Prachovských skal, nebo (b) přesunout cílovou zónu na veřejně přístupné místo. V každém případě doplnit upozornění na vstupné a otevírací dobu.
5. **Rada k uzavírkám na trasa.html („obejdi ji nejkratší rozumnou cestou“) může svádět ke vstupu do uzavřených míst** (těžba dřeva, rezervace, soukromé pozemky). Doplň: „Do uzavřeného prostoru nikdy nevstupuj.“

### B. Do spuštění (květen 2027)
6. **E-maily z waitlistu.** Jakmile budou propagovat placený zápis nebo medaili, jsou to obchodní sdělení (§ 7 zák. 480/2004 Sb.).
   - Souhlas máš. Každý e-mail ale musí: být označený jako obchodní sdělení, uvádět odesílatele a obsahovat snadné bezplatné odhlášení.
   - Hromadně posílej přes rozesílací nástroj, nebo aspoň přes BCC. Při odeslání přes „Komu/Kopie“ by šlo o porušení zabezpečení údajů.
   - Až nástroj vybereš, doplň ho do zásad jako zpracovatele.
   - Ve Sněmovně je návrh zákona o digitální ekonomice (tisk 69), který má zákon 480/2004 nahradit. Požadavky na obsah e-mailů zachovává. Zatím ale není schválený (https://www.epravo.cz/top/clanky/navrh-noveho-zakona-o-digitalni-ekonomice-120958.html).
7. **Nastavení Tally.**
   - Vypni „self email notifications“, pokud je nepotřebuješ: jdou přes SendGrid v USA (https://tally.so/help/gdpr).
   - Vypni „save progress“: ukládá odpovědi v prohlížeči (https://tally.so/help/form-settings).
   - Zaškrtávací políčko souhlasu nesmí být předvyplněné. Text souhlasu v Tally musí odpovídat textu na webu.
   - DPA s Tally je součástí jejich Terms a přijímá se registrací účtu (https://tally.so/help/gdpr). Ulož si kopii a datum.
8. **Lidé mladší 15 let.** Souhlas se službou informační společnosti dává dítě samo až od 15 let, mladším ho dává rodič (čl. 8 GDPR, § 7 zák. 110/2019 Sb.). Do formuláře i zásad dej „je mi aspoň 15“. Pro samotný pokus doporučuji 18+, nebo mladší jen s dospělým; jde o 163 km a noční hory.
9. **Ochrana přírody a lesní zákon [ověřit u AOPK].**
   - Celá CHKO (§ 26 odst. 1 písm. b) zák. 114/1992 Sb.): zakázáno tábořit a rozdělávat oheň mimo vyhrazená místa. Do pravidel dej „spaní venku jen tam, kde je to dovolené, žádný oheň“ (shrnutí: https://www.epravo.cz/top/clanky/pravni-pruvodce-ceskou-krajinou-121364.html).
   - Národní přírodní rezervace: vstup jen po vyznačených cestách (§ 29).
   - Lesní zákon § 20 odst. 5 (zák. 289/1995 Sb., znění 2026): organizovaná akce v lese potřebuje písemný souhlas vlastníka lesa a písemné oznámení obci 30 dní předem (https://www.zakonyprolidi.cz/cs/1995-289). Výzva bez hromadného startu tím podle mě není. Placený zápis ale může vést úřad k jinému hodnocení. Doporučuji před spuštěním neformálně kontaktovat Správu CHKO Orlické hory a Český ráj a Lesy ČR.
   - „Pes je povolený“: obecný zákaz psů v CHKO jsem nenašel. Vodítko ukládají spíš vyhlášky obcí, řády rezervací nebo vlastníci (Prachov). Formuluj to opatrně, viz část 3.
   - Zda trasa vede přes CHKO Broumovsko, nevím. Tvrď jen to, co ověříš z GPX.
10. **Čísla 112 a 1210 platí.** 1210 je jednotné celostátní číslo Horské služby (https://ct24.ceskatelevize.cz/regiony/254546-jednotna-linka-1210-urychli-privolani-pomoci/, článek z roku 2013, pořád v užívání). Doplnit lze aplikaci Záchranka.
11. **Upozornění na vodu.** U stylu Unsupported („jen přírodní zdroje“) přidej, že voda ze studánek a potoků nemusí být pitná.

### C. Až začnou platby a žebříček
12. **Hosting.** GitHub Pages podle vlastních pravidel neslouží jako hosting pro web, který je „primarily directed at facilitating commercial transactions“ (https://docs.github.com/en/enterprise-server@3.20/pages/getting-started-with-github-pages/github-pages-limits). Platby a účty dej na ASP.NET hosting v EU, to už máš v plánu.
13. **GPX, jména a časy ve veřejném žebříčku jsou osobní údaje.** Z GPX jsou poznat polohové a časové vzorce, třeba nocleh.
    - Potřeba: nový právní základ (smlouva, čl. 6 odst. 1 písm. b)), volba přezdívky, ořez začátku a konce stopy, retence a DPIA-lite úvaha.
    - Uživatelský obsah: DSA (nařízení 2022/2065) ukládá i malému hostingu kontaktní místo (čl. 11–12), podmínky (čl. 14) a mechanismus oznámení obsahu (čl. 16) **[ověřit rozsah]**.
14. **Spotřebitelské právo, daně, živnost** – viz část 5.

## 2. Texty k vložení

### 2a. Poznámka pod formulářem (index.html) – nahradí celý odstavec
```html
Napíšeme ti při spuštění výzvy a při důležitých změnách (finální GPX, pravidla, ceny). Odhlásit se můžeš jedním klikem v každém e-mailu nebo zprávou na <a href="mailto:tobiasotta@seznam.cz">tobiasotta@seznam.cz</a>. Údaje neprodáváme a nepředáváme pro marketing nikomu dalšímu. Formulář technicky zajišťuje služba Tally (Tally BV, Belgie, data v EU). Správce: Tobiáš Otta. Formulář je pro lidi od 15 let. Víc v <a href="ochrana-udaju.html">zásadách ochrany osobních údajů</a>.
```

### 2b. ochrana-udaju.html – obsah `<main>` (sekce nahradí stávající)
```html
<h2>Kdo je správce</h2>
<p>Tobiáš Otta, soukromá osoba, provozovatel webu a výzvy Nebe Peklo Ráj. Kontakt: <a href="mailto:tobiasotta@seznam.cz">tobiasotta@seznam.cz</a>. Pověřence pro ochranu osobních údajů nemáme, protože to zákon v našem případě nevyžaduje.</p>

<h2>Waitlist: jaké údaje, proč a na jakém základě</h2>
<p>Když se přihlásíš na waitlist, zpracováváme tvůj e-mail a to, co do formuláře sám vyplníš navíc (jméno, preferovaná varianta), spolu s datem a časem souhlasu. Používáme je jen k tomu, abychom ti poslali zprávu o spuštění výzvy, finální GPX a pravidla, informace o cenách zápisu a medaile a o důležitých změnách. Právním základem je tvůj souhlas (čl. 6 odst. 1 písm. a) GDPR, u e-mailů s nabídkou placených služeb i § 7 zákona č. 480/2004 Sb.). Poskytnutí údajů je dobrovolné. Bez e-mailu ti ale nemůžeme nic poslat.</p>

<h2>Návštěva webu</h2>
<p>Web běží na GitHub Pages (GitHub, Inc., USA). GitHub při každé návštěvě zaznamenává IP adresu a technické údaje o prohlížeči a ukládá je z bezpečnostních důvodů, a to jako samostatný správce podle svých <a href="https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement">zásad</a>. My tyto záznamy nevidíme a nepoužíváme. Nepoužíváme analytické ani reklamní cookies. Při načtení formuláře se připojíš k serverům Tally. Ta může ukládat jen technické údaje nutné k vyplnění formuláře.</p>

<h2>Komu údaje svěřujeme</h2>
<ul>
  <li><strong>Tally BV</strong> (Gent, Belgie) – provozuje formulář a ukládá odpovědi naším jménem jako zpracovatel. Data ukládá v EU (Google Cloud, Belgie). Pro ochranu proti botům a pro DNS používá Cloudflare (USA).</li>
  <li><strong>Seznam.cz, a.s.</strong> (ČR) – poskytovatel naší e-mailové schránky, přes kterou s tebou komunikujeme.</li>
  <!-- až vybereš nástroj na rozesílku, doplň ho sem -->
</ul>
<p>Údaje neprodáváme a nepředáváme pro marketing nikomu dalšímu. Úřadům je předáme, jen když to ukládá zákon.</p>

<h2>Předání mimo EU</h2>
<p>Někteří poskytovatelé (GitHub, Cloudflare) sídlí v USA. Data k nim proto mohou putovat. Předání se opírá o rozhodnutí Evropské komise o odpovídající ochraně (EU-US Data Privacy Framework), pokud je poskytovatel certifikován, jinak o standardní smluvní doložky EU.</p>

<h2>Jak dlouho</h2>
<p>Údaje z waitlistu uchováváme do odvolání souhlasu, nejdéle do 31. 12. 2027. Pak waitlist smažeme. Pokud si mezitím založíš účet v žebříčku, platí pro něj samostatné zásady.</p>

<h2>Tvoje práva</h2>
<p>Máš právo kdykoli odvolat souhlas, a to stejně snadno, jako jsi ho dal (odhlašovacím odkazem nebo e-mailem). Dosavadní zpracování tím nepřestává být zákonné. Dál máš právo na přístup k údajům, jejich opravu, výmaz, omezení zpracování a přenositelnost a právo vznést námitku. Stačí napsat na <a href="mailto:tobiasotta@seznam.cz">tobiasotta@seznam.cz</a>. Odpovíme do jednoho měsíce. Pokud si myslíš, že s údaji zacházíme špatně, můžeš podat stížnost u Úřadu pro ochranu osobních údajů, Pplk. Sochora 27, 170 00 Praha 7, <a href="https://uoou.gov.cz">uoou.gov.cz</a>.</p>

<h2>Věk</h2>
<p>Na waitlist se mohou přihlásit lidé od 15 let. Za mladší musí souhlas dát rodič.</p>

<p><small>Platné od [datum nasazení]. Nic automaticky nevyhodnocujeme ani neprofilujeme. Před spuštěním žebříčku zásady doplníme o účty, platby a nahrané GPX záznamy a dáme ti vědět e-mailem.</small></p>
```
*Pozn.: větu „ukládá v EU (Google Cloud, Belgie)“ jsem převzal z https://tally.so/help/gdpr. Obecné zásady Tally (https://tally.so/privacy) uvádějí „servers within the European Union“. Certifikaci Cloudflare v DPF jsem neověřoval, proto je v textu formulace „pokud je certifikován, jinak SCC“.*

## 3. Upozornění pro trasa.html a pravidla.html
**Právní rámec:**
- Jednostranné oznámení, že někdo svou povinnost k náhradě újmy vylučuje nebo omezuje, se nebere v potaz (**§ 2896 OZ**).
- Ujednání, které předem vylučuje nebo omezuje náhradu újmy na zdraví a životě nebo újmy způsobené úmyslně či z hrubé nedbalosti, se nebere v potaz. Totéž platí pro omezení vůči slabší straně, tedy spotřebiteli (**§ 2898 OZ**).
- Kdo přijal nebezpečí, nevzdal se tím náhrady (**§ 2899 OZ**).
- Zdroj: https://www.podnikatel.cz/zakony/novy-obcansky-zakonik/f4588137/
- Upozornění tedy odpovědnost **nevyloučí**. Slouží jako prevence (§ 2900 OZ) a poctivá informace o riziku a může hrát roli při posuzování spoluzavinění. Hlavní obrana je nedávat lidem pokyny, které je vystaví riziku, a nic neslibovat (zabezpečení, stav cest).
- Do lesa vstupuje každý na vlastní nebezpečí (§ 19 zák. 289/1995 Sb.).

**Text (nahradí blok „Tohle není organizovaný závod“ na trasa.html a rozšíří bod „Na vlastní riziko“ v pravidlech):**
```html
<h3>Tohle není organizovaný závod</h3>
<p>Nebe Peklo Ráj je trvalá výzva, kterou si každý jde sám, kdy chce a jak chce. Nikdo pokus nepořádá, nedozoruje ani nezajišťuje: na trati nejsou pořadatelé, značení výzvy, občerstvení ani zdravotní služba. Trasa vede po veřejných cestách, silnicích a lesem v běžném provozu. Za stav cest, počasí, uzavírky ani za ostatní účastníky provozu neručíme.</p>
<p>Jdeš na vlastní nebezpečí a sám rozhoduješ, jestli na pokus stačíš zdravotně, kondičně i výbavou. 163 km, noc a horské hřebeny jsou opravdová zátěž. Když si nejsi jistý, poraď se s lékařem. Zvaž úrazové pojištění a pojištění odpovědnosti. Pokus můžeš a máš kdykoli ukončit. Žádný čas v žebříčku nestojí za zdraví.</p>
<p>Řiď se pravidly silničního provozu, pokyny vlastníků pozemků a správ chráněných území. V chráněné krajinné oblasti netábor a nerozdělávej oheň mimo vyhrazená místa, v rezervacích zůstaň na značených cestách a do uzavřených prostorů nikdy nevstupuj. Psa měj pod kontrolou a na vodítku všude, kde to vyžaduje vyhláška obce, řád rezervace nebo vlastník (např. Prachovské skály). Voda z přírodních zdrojů nemusí být pitná.</p>
<p>Cíl leží v areálu Prachovských skal, který je placený a má otevírací dobu. Vstupné a otevírací dobu respektuj. <!-- upravit podle dohody se správou --></p>
<p>V nouzi volej 112, na horských úsecích Horskou službu 1210. Svůj plán řekni někomu blízkému a sdílej polohu.</p>
<p><small>Tímto upozorněním nevylučujeme odpovědnost, kterou nelze vyloučit podle českého práva, zejména za újmu na zdraví nebo způsobenou úmyslně či z hrubé nedbalosti.</small></p>
```
Až budou platby, dej v obchodních podmínkách zaškrtávané prohlášení „jsem seznámen s riziky a jdu na vlastní nebezpečí“. Odpovědnost tím nevyloučíš (§ 2898), ale doložíš, že jsi účastníka informoval.

## 4. Cookies a lišta se souhlasem – zatím NE (s podmínkou)
- Platí § 89 odst. 3 zák. 127/2005 Sb. (čl. 5 odst. 3 směrnice ePrivacy). Souhlas není potřeba pro ukládání, které je nezbytné ke službě, o kterou si uživatel výslovně řekl.
- Web sám žádné cookies nemá. Nemá analytiku, fonty z Google ani jiná vložení třetích stran; ověřil jsem HTML a styles.css.
- Tally pro své stránky deklaruje jen nezbytné cookies (https://tally.so/help/cookie-policy). Nezávislý přehled uvádí jen relační cookies pro stav formuláře (https://consentstack.io/vendors/tally). Embed jsem nemohl ověřit sám, proxy blokovala tally.so.
- **Udělej:** otevři web v anonymním okně a v DevTools zkontroluj Application → Cookies/Storage a Network u tally.so.
  - Pokud uvidíš analytiku (např. Mixpanel, který Tally vede jako US subdodavatele „nezpracovávajícího odpovědi“), načítej iframe až po kliknutí na „Otevřít formulář“ (dvoukrokové vložení). Lištu pak nepotřebuješ a IP se k Tally dostane až po kliknutí.
  - Totéž dělej později s mapami a videi.
- Výhled: EU „Digital Omnibus“ (změny cookie pravidel) je k 10/2026 jen návrh, Rada přijala postoj 21. 10. 2026, platí dosavadní pravidla (https://www.heuking.de/en/news-events/newsletter-articles/detail/cookie-consent-under-the-digital-omnibus-planned-changes-and-technical-implementation.html).
- Platnost DPF potvrdil Tribunál 3. 9. 2025 (Latombe). Odvolání u SDEU je podané a podle dostupných zdrojů dosud nerozhodnuté **[ověřit]** (https://www.wilmerhale.com/en/insights/blogs/wilmerhale-privacy-and-cybersecurity-law/20251201-european-court-of-justice-to-review-challenge-to-eu-us-data-privacy-framework).

## 5. Co je potřeba před spuštěním plateb
- [ ] **Živnostenský list.** Opakovaný placený zápis a prodej medailí je podnikání. Pravděpodobně stačí volná živnost „Výroba, obchod a služby neuvedené v přílohách 1 až 3“ **[ověřit obory]**. IČO a adresu sídla uveď na webu a v podmínkách (§ 435 OZ). Pak přihláška k OSVČ (ČSSZ/ZP) a daňová registrace.
- [ ] **Obchodní podmínky.** Předsmluvní informace podle § 1811 a § 1820 OZ: cena včetně všech poplatků, popis služby (co je „zápis“, kdy a proč lze čas odmítnout nebo smazat, kdo rozhoduje spory o pravidla), doprava medaile, platby, reklamace a odpovědnost za vady (§ 2161 a násl., digitální obsah § 2389a a násl.).
- [ ] **Odstoupení do 14 dnů (§ 1829 OZ) + poučení a vzorový formulář.**
  - Výjimky (§ 1837 OZ): služba plně poskytnutá s výslovným souhlasem před uplynutím lhůty, když spotřebitel ví, že tím právo ztrácí (písm. a), digitální obsah s prvním plněním po souhlasu (písm. l), zboží upravené podle přání spotřebitele (písm. d, např. medaile se jménem nebo časem). Výjimky platí jen se správně zaškrtnutým souhlasem.
  - **Tlačítko pro odstoupení** (směrnice 2023/2673): česká „tlačítková novela“ má podle zdrojů účinnost od **1. 1. 2027** **[ověřit, zda vyšla ve Sbírce]** (https://www.epravo.cz/top/clanky/tlacitkovy-zakon-2026-novinky-pro-eshopy-a-online-podnikatele-121632.html, https://www.ey.com/cs_cz/technical/tax/tax-alerts/2026/04/nove-tlacitko-odstoupit-pri-online-smlouvach). Při spuštění v 5/2027 už bude platit.
- [ ] **Reklamační řád a ADR.** Informace o mimosoudním řešení u ČOI (§ 14 zák. 634/1992 Sb.). Odkaz na evropskou platformu ODR už není povinný, platforma skončila 20. 7. 2025 (nařízení 2024/3228; https://www.iubenda.com/en/blog/discontinuation-of-the-european-online-dispute-resolution-odr-platform/).
- [ ] **Tlačítko objednávky** „Objednávka zavazující k platbě“ nebo podobně (§ 1826a OZ).
- [ ] **DPH.** Registrace při obratu nad 2 000 000 Kč za kalendářní rok, plátcem se stáváš od 1. 1. dalšího roku. Při překročení 2 536 500 Kč jsi plátcem hned od následujícího dne (https://financnisprava.gov.cz/assets/cs/prilohy/d-seznam-dani/11977_25_Informace_ke_zmenam_v_platcovstv2025.pdf).
  - **OSS:** limit 10 000 EUR na prodej zboží a elektronických služeb spotřebitelům v jiných státech EU. Do limitu se DPH řeší jen jako česká; neplátce řeší až při registraci **[ověřit s daňovým poradcem]** (https://www.businessinfo.cz/clanky/jak-si-ulehcit-odvod-dph-pri-prodeji-do-zahranici-pomuze-vam-rezim-oss/).
- [ ] **EET** se nyní neuplatňuje. Platební brána (Stripe, GoPay, Comgate) jako samostatný správce nebo zpracovatel do zásad.
- [ ] **Bezpečnost výrobku.** Na medaili se vztahuje GPSR (nařízení 2023/988): údaje výrobce na výrobku nebo obalu, malé díly a děti **[ověřit]**.
- [ ] **Nové zásady GDPR** (účty, GPX, žebříček, platby, účetní archivace 10 let) a smlouva se zpracovatelem s hostingem.
- [ ] **Přístupnost.** European Accessibility Act (zák. 424/2023 Sb.) se mikropodniků poskytujících služby netýká **[ověřit]**. Dobrá přístupnost se přesto vyplatí.

## 6. Rizikové věty na webu
| Kde | Věta | Problém | Návrh |
|---|---|---|---|
| index (GDPR) | „e-mail nikomu nepředáme“ | nepřesné (Tally, GitHub) | text 2a |
| index/trasa/pravidla/kontakt | +3 900 m, 48 / 114 km | nesouhlasí s fakty (3 481 m, 45 / 118 km) | sjednotit |
| index | „Vyrazíš, kdy chceš“ + trasa „pokus je možný kdykoli“ | cíl v placeném areálu s otevírací dobou | doplnit omezení nebo změnit cíl |
| trasa | „obejdi [uzavírku] nejkratší rozumnou cestou“ | může vést do uzavřených prostorů | „nikdy nevstupuj do uzavřeného prostoru“ |
| pravidla FAQ | „Pes ano, podle pravidel CHKO… na vodítku“ | vodítko ukládá spíš obec, řád nebo vlastník než CHKO | text v části 3 |
| pravidla | Unsupported „voda jen z přírodních zdrojů“ | zdravotní riziko | varování o pitnosti |
| index FAQ / pravidla | „Pauzy i spánek kdekoliv“ | táboření v CHKO mimo vyhrazená místa je zakázané | „spi jen tam, kde je to dovolené“ |
| pravidla „Věříme ti“ / ruční kontrola rekordů | – | po zavedení plateb musí podmínky popsat, jak se rozhoduje, a dát možnost námitky | do OP |
| zebricek | „Zápis do žebříčku bude placený“ | zatím OK; cena musí být před objednávkou konečná | – |
| ochrana-udaju | „nejdéle do doby, kdy waitlist přestane sloužit“ | neurčité | pevné datum (2b) |
| patička | „© 2026 Nebe Peklo Ráj“ | OK; až bude podnikání, doplnit IČO | – |
