# Kako se posao završava

Napisan kod na grani koja nikad nije spojena je isto što i kod koji ne postoji.
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
