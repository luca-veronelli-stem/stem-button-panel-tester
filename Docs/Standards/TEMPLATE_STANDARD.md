# Standard Template Documents - Meta-Standard per Documenti Template

> **Versione:** 1.0  
> **Data:** 2026-04-01  
> **Riferimento Issue:** -  
> **Stato:** Active

---

## Scopo

Questo standard definisce la struttura che ogni documento **template** deve seguire nel progetto **Stem.ButtonPanel.Tester**, garantendo:

- **Uniformità** tra tutti i template del progetto
- **Completezza** delle informazioni per l'utilizzo
- **Facilità d'uso** per chi deve applicare il template

Si applica a: tutti i file `*_TEMPLATE.md` in `Docs/Standards/`.

---

## Principi Guida

1. **Self-contained:** Ogni template deve contenere tutte le informazioni necessarie per essere usato
2. **Esempi concreti:** Il template completo deve essere copiabile e usabile direttamente
3. **Checklist operative:** Includere sempre checklist per validare l'uso corretto
4. **Varianti documentate:** Se esistono varianti (es. per tipo progetto), documentarle chiaramente
5. **Consistenza visiva:** Usare gli stessi simboli e convenzioni tra template

---

## Regole

### Struttura Documento

| Regola | Livello | Descrizione |
|--------|---------|-------------|
| TMP-001 | ✅ DEVE | Avere header con Versione, Data, Stato |
| TMP-002 | ✅ DEVE | Includere sezione "Scopo" che spiega quando usare il template |
| TMP-003 | ✅ DEVE | Contenere il template completo copiabile |
| TMP-004 | ✅ DEVE | Avere tabella sezioni obbligatorie vs opzionali |
| TMP-005 | ✅ DEVE | Includere checklist di validazione |
| TMP-006 | ✅ DEVE | Avere sezione Changelog |
| TMP-007 | ⚠️ DOVREBBE | Includere esempi concreti di uso |
| TMP-008 | 💡 PUÒ | Avere sezione "Varianti" per casi specifici |

### Contenuto Template

| Regola | Livello | Descrizione |
|--------|---------|-------------|
| TMP-010 | ✅ DEVE | Usare placeholder chiari: `{NomeComponente}`, `YYYY-MM-DD` |
| TMP-011 | ✅ DEVE | Marcare sezioni obbligatorie con `[OBBLIGATORIA]` |
| TMP-012 | ✅ DEVE | Marcare sezioni opzionali con `[Opzionale]` o `[Opzionale - condizione]` |
| TMP-013 | ⚠️ DOVREBBE | Includere commenti esplicativi nel template |
| TMP-014 | ❌ NON DEVE | Contenere testo Lorem Ipsum o placeholder non significativi |

### Convenzioni

| Regola | Livello | Descrizione |
|--------|---------|-------------|
| TMP-020 | ✅ DEVE | Date in formato ISO 8601 (`YYYY-MM-DD`) |
| TMP-021 | ✅ DEVE | Encoding UTF-8 |
| TMP-022 | ⚠️ DOVREBBE | Usare box-drawing Unicode per diagrammi (`┌─┐ │ └─┘`) |
| TMP-023 | ⚠️ DOVREBBE | Usare emoji standard per status: ✅ ⚠️ ❌ 💡 |

---

## Struttura Standard per Template

Ogni file `*_TEMPLATE.md` DEVE seguire questa struttura:

```markdown
# {Nome} Template - {Descrizione Breve}

> **Versione:** X.Y  
> **Data:** YYYY-MM-DD  
> **Stato:** [Draft | Active | Deprecated]

---

## Scopo

{Descrizione di quando e perché usare questo template}

---

## Convenzioni                             [Se applicabile]

{Tabelle con convenzioni di naming, formattazione, etc.}

---

## Template Completo

```markdown
{Il template vero e proprio, copiabile}
``

---

## Sezioni in Dettaglio                    [Se il template è complesso]

{Spiegazione di ogni sezione del template}

---

## Sezioni Obbligatorie vs Opzionali

| # | Sezione | Obbligatoria | Condizione |
|---|---------|--------------|------------|
| 1 | Sezione1 | ✅ | Sempre |
| 2 | Sezione2 | ⚪ | Solo se {condizione} |

---

## Varianti                                [Se esistono varianti]

{Tabelle o descrizioni delle varianti per tipo/contesto}

---

## Checklist

- [ ] Item 1
- [ ] Item 2
- [ ] ...

---

## Changelog

| Data | Versione | Descrizione |
|------|----------|-------------|
| YYYY-MM-DD | X.Y | Descrizione |
```

---

## Template Esistenti nel Progetto

| File | Scopo | Stato |
|------|-------|-------|
| `Templates/STANDARD_TEMPLATE.md` | Template per documenti di standard (`*_STANDARD.md`) | Active |
| `TEMPLATE_STANDARD.md` | Meta-standard per documenti template (questo file) | Active |
| `Templates/README_TEMPLATE.md` | Template per README di progetti | Active |
| `Templates/ISSUES_TEMPLATE.md` | Template per file ISSUES.md | Active |

---

## Esempi

### ✅ Header Corretto

```markdown
# README Template - Struttura Standard per README

> **Versione:** 1.0  
> **Data:** 2026-02-24  
> **Stato:** Active
```

### ❌ Header Scorretto

```markdown
# Readme template

Ultimo aggiornamento: 24/02/2026
```

### ✅ Placeholder Corretti

```markdown
# {ComponentName} - ISSUES

> **Ultimo aggiornamento:** YYYY-MM-DD

## Sezione                                 [OBBLIGATORIA]

{Descrizione della sezione e cosa inserire}
```

### ❌ Placeholder Scorretti

```markdown
# XXX - ISSUES

> **Ultimo aggiornamento:** ...

## Sezione

Lorem ipsum dolor sit amet...
```

---

## Eccezioni

| Template | Eccezione | Motivazione |
|----------|-----------|-------------|
| `STANDARD_TEMPLATE.md` | Struttura leggermente diversa | È un meta-standard che definisce standard, non un template generico |

---

## Enforcement

- [ ] **Code Review:** Verificare che nuovi template seguano questo standard
- [ ] **Checklist:** Ogni template deve avere una checklist di validazione propria

---

## Riferimenti

- `Docs/Standards/` - Cartella standard e template

---

## Changelog

| Data | Versione | Descrizione |
|------|----------|-------------|
| 2026-04-01 | 1.0 | Adattato per Stem.ButtonPanel.Tester |
