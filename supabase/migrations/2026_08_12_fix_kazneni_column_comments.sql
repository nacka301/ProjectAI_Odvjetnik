-- Ispravak komentara na kaznenim stupcima.
--
-- 2026_02_26_add_kazneni_fields.sql opisao je is_zastupnik_djeteta kao "70% of Tbr.4",
-- što je bilo nedorečeno, a kod je uz to stopu množio s Tbr. 4. toč. 8 i dobivao 35%.
-- Pravilnik o visini nagrade za rad odvjetnika prilikom obrana po službenoj dužnosti
-- (NN 22/2022, br. 271) ima dvije stope, obje od ISTE osnovice — "nagrade za rad koja bi
-- odvjetniku pripadala za pojedine radnje u postupku prema Tarifi":
--
--   čl. 1 — obrana po sl. dužnosti i obrana na teret proračunskih sredstava → 50%
--   čl. 2 — opunomoćenik djeteta žrtve kaznenog djela                       → 70%
--
-- Uloge se međusobno isključuju (obrana okrivljenika vs. zastupanje djeteta žrtve).
-- Samo komentari — struktura i podaci se ne diraju.

COMMENT ON COLUMN troskovnici.is_po_sluzbenoj_duznosti IS
  'Obrana po službenoj dužnosti – 50% nagrade po Tarifi za pojedinu radnju (Pravilnik NN 22/2022, čl. 1). Isključuje is_zastupnik_djeteta.';

COMMENT ON COLUMN troskovnici.is_na_teret_proracuna IS
  'Obrana na teret proračunskih sredstava – 50% nagrade po Tarifi za pojedinu radnju (Pravilnik NN 22/2022, čl. 1). Isključuje is_zastupnik_djeteta.';

COMMENT ON COLUMN troskovnici.is_zastupnik_djeteta IS
  'Opunomoćenik djeteta žrtve kaznenog djela – 70% nagrade po Tarifi za pojedinu radnju (Pravilnik NN 22/2022, čl. 2). Isključuje obje zastavice iz čl. 1.';
