-- Keep-alive protiv Render cold starta.
--
-- Backend je na Render free tieru koji gasi instancu nakon ~15 min mirovanja;
-- prvi poziv nakon toga budi kontejner i traje 13-50 s (mjereno: 13.79 s prvi
-- poziv, 0.69 s topli). To je bio jedini uzrok koji vrijedi sekunde na
-- prijavi, dashboardu i troskovniku, jer svaki tok dodiruje backend.
--
-- pg_cron svakih 10 min zove javni /api/health, pa instanca nikad ne dosegne
-- prag uspavljivanja i cold start efektivno nestaje. Health je bez
-- autentikacije i vraca samo {status: healthy}, pa se ovime nista ne otkriva.
--
-- Timeout je 30 s (ne pg_net default od 5 s): kad backend jest hladan, ping s
-- 5 s istekne bas dok se instanca budi. S 30 s ping preziva cold start, doceka
-- 200 i drzi vezu dovoljno dugo da se buduci potvrdi. Kad je topao, ~0.5 s.
--
-- pg_net sam prunea odgovore starije od nekoliko sati, pa net._http_response
-- ne raste.
--
-- Trajniji popravak (izvan koda): Render Starter plan (~7 USD/mj) nema
-- spin-down, pa keep-alive ne bi ni trebao. Do tada ovo drzi backend toplim.

create extension if not exists pg_cron;
create extension if not exists pg_net;

-- Idempotentno: makni stari posao istog imena ako postoji, pa ga (ponovno) zakazi.
do $$
begin
  perform cron.unschedule('keep-backend-warm');
exception when others then
  null; -- posao jos ne postoji
end $$;

select cron.schedule(
  'keep-backend-warm',
  '*/10 * * * *',
  $ping$ select net.http_get(
    url := 'https://projectai-odvjetnik-fl8y.onrender.com/api/health',
    timeout_milliseconds := 30000
  ) $ping$
);
