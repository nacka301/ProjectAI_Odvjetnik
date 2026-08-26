-- FK troskovnici.case_id nema pokrivajuci indeks (Supabase performance advisor).
--
-- Aditivno i nula-rizicno: ubrzava dohvat troskovnika po predmetu i provjeru
-- kaskade pri brisanju predmeta (case_id ima ON DELETE SET NULL). Pri danasnjoj
-- kolicini podataka (4 troskovnika) ne mijenja mjerljivo nista, ali sprjecava
-- sekvencijalni scan kad podataka bude vise. Ostala tri FK-a na tablici
-- (client_id, law_firm_id, user_id) vec imaju indeks.
create index if not exists idx_troskovnici_case_id on public.troskovnici (case_id);
