# Code Citations

## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```


## License: unknown
https://github.com/jeffreyziegler/CompLeg/blob/96960036d662dd94323a79acb7a2f58b18f44141/data/canadaTextParsing/Questions/Questionsurl/questions_42_1_167.html

```
Pushed as `13ae5ec`. Evo što je implementirano — 3-slojna obrana protiv keširanja:

**Sloj 1: [next.config.ts](frontend/next.config.ts#L15) — HTTP headeri za HTML stranice**
```
Cache-Control: no-cache, no-store, must-revalidate, max-age=0
Pragma: no-cache
Expires: 0
```
Primjenjuje se na sve rute **osim** `_next/static/*` (JS/CSS bundlovi — ti su content-hashed, svaki deploy generira nove hasheve pa im dugi cache ne smeta).

**Sloj 2: [middleware.ts](frontend/middleware.ts#L44) — Injekcija na svaki response**
Middleware postavlja iste 3 headera na svaki odgovor koji prolazi kroz Supabase auth. Ovo je najjači sloj jer se izvršava server-side na svakoj navigaciji — neovisno o tome što browser "misli" o cacheu.

**Sloj 3: [layout.tsx](frontend/app/layout.tsx#L27) — Meta tagovi u `<head>`**
```html
<meta name="Cache-Control" content="no-cache, no-store, must-revalidate" />
<meta name="Pragma" content="no-cache" />
<meta name="Expires" content="0" />
```
Ovo target
```

