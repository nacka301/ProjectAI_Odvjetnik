# Kontekst — zalijepi kao prvu poruku u novi Cowork razgovor

Stanje na 12.8.2026.

---

## Tko sam

David iz Osijeka, razvijam software. Radim web aplikaciju **Odvjetnički troškovnik**
koja izračunava troškovnike po hrvatskoj Odvjetničkoj tarifi i sudske pristojbe.
Šaljem hladne mailove odvjetnicima. Cilj: prvi plaćeni korisnik u rujnu 2026.

Cijena: **19 € mjesečno ili 190 € godišnje.** Prvim korisnicima zaključana.
Alat otvaram korisnicima u rujnu.

## Kako želim da mi odgovaraš

- Kratko i izravno. Bez uvoda i bez hvaljenja.
- Reci mi kad griješim, i drži se toga ako si provjerio.
- Hrvatski.
- Kad predlažeš mail ili poruku — napiši gotov tekst, ne opis.
- **Ne pitaj me smiješ li nešto napraviti — napravi pa mi reci.** Ako je nešto što
  stvarno moram odlučiti ja, pitaj kratko.
- Ako mogu odgovor naći u propisu ili na internetu, **nađi ga sam** umjesto da mi
  daš pitanje.
- Potpisujem se samo **David**, bez prezimena. Nikad broj mobitela — sve ide mailom.

## Kako radim s kodom

PRD prije koda. Do 30 pitanja prije PRD-a. Jedna značajka po razgovoru.
Pregled koda ide u zaseban razgovor.

## Kako se posao (kod) završava — dodano 19.8.2026.

19.8.2026. su dva gotova popravka (uklanjanje Revizije, zaključavanje modula)
stajala spojena samo sa sobom, na svojim granama, dok je produkcija tjednima
išla s neke treće grane koju je netko ručno promovirao. `main` sam nije bio
pushan na GitHub 15 commitova unatrag — ni Vercel ni nitko drugi nije mogao
znati da tamo išta postoji.

**Zadatak nije završen kad testovi prođu na grani. Završen je kad je:**

1. Grana spojena u `main` (ne "spremna za spajanje" — stvarno spojena).
2. `main` pushan na `origin` (`git push origin main` ili ekvivalent). Provjeri
   `git log --oneline origin/main..main` — ako nešto ispiše, ništa od ovoga
   nije stiglo dalje od tvog diska.
3. Deploy pokrenut s `main` i dočekan da završi (READY), ne samo pokrenut.
4. Provjereno na živoj stranici (pravotek.com), u anonimnom prozoru, ne u
   lokalnom buildu — lokalni build ne dokazuje ništa o tome što korisnik vidi.

Ako se zadatak prekida prije 4. koraka — kaže se to izričito, ne "gotovo je".
Grana bez merga, main bez pusha i deploy bez provjere izgledaju identično
sljedećem chatu kao i pravi završetak, dok netko ručno ne prekopa git log.

---

# STANJE MAILOVA

**Poslano ~115 hladnih mailova** odvjetnicima, od 28.7. do 12.8. Ritam 20 dnevno.

**Problem s tempom:** šaljem svih 20 unutar 30-90 sekundi. To Gmailu izgleda kao
skripta. Treba ih razbacati kroz dan.

**Nova lista: 344 adrese** u `nova-lista-odvjetnika.xlsx`, već očišćene od poslanih.
70 ih ima gotovu skicu u Gmailu (stupac „Skica u Gmailu"). Ostalo ~274.

## Odgovori

| Tko | Što | Status |
|---|---|---|
| **Gabriela Banić** (Zagreb) | Pitala cijenu, dobila 19 €, **11.8. odgovorila „da"** | **PRVI LEAD.** Javiti se prvi tjedan rujna s pristupom |
| **Tomi Miladinović** (Pula) | „Djeluje ispravno postavljeno i bilo bi korisno." Tražio pristojbe i predujam | Skica čeka slanje |
| **Tomislav Bogdan** (Osijek) | „Za velike urede, meni bi ušteda bila neznatna." Prijavio da nedostaje datum uz radnju | Skica čeka slanje |
| Marina Aralica Dodig (Zagreb) | Ne, ali me drži u pričuvi. Za rujan | Odgovoreno |
| Maja Opašić (Poreč) | Ne — „svaki predmet je individualan i nepredvidiv" | Odgovoreno |
| Božena Budimir | Ne | Zatvoreno |
| Renata Horvatin (Našice) | Ne — „mali ured, mali troškovnici, nedovoljno za program" | — |
| Stephani Jelčić (Pula) | Automatski, izvan ureda do 17.8. | Javiti se nakon 17.8. |

**Tri skice čekaju slanje: Gabriela, Bogdan, Miladinović.**

## Otvoreno pitanje koje nijedan propis ne rješava

**Rade li samostalni uredi dovoljno troškovnika?** Bogdan i Horvatin kažu ne.
Zvjezdana (odvjetnica, teta) kaže da varira i da se rade u naletima. Ako se prigovor
ponovi još dvaput, ciljanje treba prebaciti na odvjetnička društva.

## Tekst hladnog maila — ne mijenjati bez pitanja

> Predmet: Troškovnici po Tarifi — kratko pitanje
>
> Poštovani gospodine [PREZIME], / Poštovana gospođo [PREZIME],
>
> ja sam David i razvijam software.
>
> Napravio sam alat koji izrađuje troškovnike po Odvjetničkoj tarifi — unesu se
> radnje i vrijednost predmeta, a alat složi troškovnik.
>
> Pišem vam jer vodite samostalnu praksu, pa troškovnike vjerojatno radite sami,
> bez tajnice koja to preuzme.
>
> Snimio sam kratku snimku na kojoj se vidi kako radi (52 sekunde):
> https://youtu.be/z360IAhXcrM
>
> Zanima me samo bi li vam ovakvo što uštedjelo vrijeme — i gdje griješi.
>
> Srdačan pozdrav,
> David

Napomena: rečenica „vodite samostalnu praksu" vrijedi samo za samostalne urede.
Za odvjetnička društva i zajedničke urede treba drugi tekst.

## Alat

Spojen **Gmail** — čitanje, pretraga, izrada i izmjena skica. Ne mogu se brisati
mailovi ni skice.

Korisne pretrage: `in:sent "Troškovnici po Tarifi"` · `in:draft` ·
`label:pozitivan_odgovor`

---

# STANJE PROIZVODA

## Potvrđeno 12.8. kod odvjetnice — ovo su odluke, ne pretpostavke

**Struktura troškovnika:**

1. Odvjetničke radnje s datumima
2. Zbroj nagrade
3. PDV (samo ako je odvjetnik u sustavu PDV-a)
4. Sudske pristojbe — bez PDV-a
5. Predujmovi — bez PDV-a
6. Napomena

**Ostalo potvrđeno:**

- Datum uz svaku radnju **načelno mora stajati**
- Tri ročišta na tri datuma → **svako svoj redak**, ne količina 3
- Redoslijed radnji **nije propisan**, korisnik bira; Rbr. se renumerira
- Zaokruživanje **na dvije decimale**
- **Nema propisanog oblika** troškovnika
- Vrijednost boda **2,00 €** (Tbr. 54 Tarife)
- Verzija Tarife ovisi o **dospijeću nagrade**, ne o datumu radnje (Tbr. 52)
- Napomena o zakonskoj zateznoj kamati (čl. 29 ZOO) — dobra praksa, dodati kao polje
- PDV: nagrada **DA**, putni troškovi i dnevnice **DA**, sudske pristojbe **NE**,
  predujmovi **NE** (čl. 33 Zakona o PDV-u — prolazne stavke)

## Greške u kodu koje su utvrđene, a nisu popravljene (na 12.8. — provjeri je li ovo još točno)

> **Napomena 19.8.2026.:** stavke 1–6 iz ovog popisa (postoci u kaznenom, mrtvi
> propis NN 37/2023 → NN 50/2026, Tar. br. 1 na sve postupke, nedostaje Tar. br. 3,
> polovica za elektroničke podneske, PDV prekidač) izgledaju popravljene u
> međuvremenu kroz integraciju troškovnika na `main` — provjeriti prije nego se
> netko opet baci na isti popravak. Detalji u internoj bilježnici, ne ovdje.

1. **Postoci u kaznenom se množe** — 70 % × 50 % = 17,5 %. Trebaju biti međusobno
   isključivi: 50 % branitelj po sl. dužnosti, 70 % opunomoćenik djeteta žrtve
   (Pravilnik NN 22/2022)
2. **Sudske pristojbe računaju se po mrtvom propisu** — NN 37/2023 prestao važiti
   1.6.2026., zamijenio ga **NN 50/2026**, iznosi otprilike udvostručeni, maksimum
   s 663,61 € na 1.500,00 €
3. **Tar. br. 1 se primjenjuje na sve postupke** — a vrijedi samo za parnični i
   ovršni. Upravni spor, izvanparnični, ostavinski, zemljišnoknjižni, stečaj i
   registar imaju svoja poglavlja s fiksnim iznosima
4. **Nedostaje Tar. br. 3** — žalba protiv presude je puna pristojba **uvećana za
   100 %**, ne polovica
5. **Nema polovice za elektroničke podneske** (čl. 7 Zakona o sudskim pristojbama) —
   a odvjetnici gotovo sve predaju kroz e-Komunikaciju
6. **Nema PDV prekidača** — Zvjezdana ga traži od veljače, ona nije u sustavu PDV-a
7. **Nema datuma uz radnju** — u tijeku
8. **Nema oslobođenja od pristojbi** (čl. 11 ZSP, 21 kategorija)
9. **Kazneni postupak** — nema Tbr. 2 ni Tbr. 3, nedostaju stavke iz Tbr. 1 i Tbr. 4

---

# DATOTEKE U MAPI

| Datoteka | Što je |
|---|---|
| `promptovi-izmjene.md` | **8 promptova za Claude Code**, jedan po razgovoru, poredani po prioritetu |
| `prompt-datum-uz-radnju.md` | Prompt za značajku koja je u tijeku |
| `uredba-pristojbe-NN-50-2026.md` | Nova Uredba, svih 35 tarifnih brojeva i iznosa |
| `zakon-o-sudskim-pristojbama.md` | Oslobođenja, utvrđivanje VPS-a, rokovi, čl. 7 |
| `odgovori-zvjezdana-12-8-2026.md` | Sve što je odvjetnica potvrdila |
| `nova-lista-odvjetnika.xlsx` | 344 nove adrese + list poslanih |
| `kontekst-mailovi.md` | Detaljna povijest kampanje |
| `kontekst-planinarenje.md` | Odvojena ideja, drugi projekt, ne miješati |

---

# ŠTO JE SLJEDEĆE

1. Poslati tri skice koje čekaju — Gabriela, Bogdan, Miladinović
2. Prompt 1 iz `promptovi-izmjene.md` — postoci u kaznenom, najbrži popravak
3. Promptovi 2–5 — sudske pristojbe, elektronički podnesci, PDV, struktura
4. Slati dalje po 20 dnevno, razbacano kroz dan
5. Prvi tjedan rujna: Gabriela dobiva pristup

Obrisati 6 starih skica `pitanje o rezervacijama` od 21.7. — moram sam, Claude ne
može brisati skice.
