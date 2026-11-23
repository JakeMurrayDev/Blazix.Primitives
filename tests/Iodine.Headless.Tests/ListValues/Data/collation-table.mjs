// The expected column of the collation table (UTL-I18N-08..12, ALG-05; M5 track 4, chosen by the user 2026-11-03, plan
// decision 10 (a)): each row's three predicates computed by Base UI's filter (internals/filter.ts at v1.9.0, its window of
// `query.length` UTF-16 code units) over `Intl.Collator(locale, { usage: 'search', sensitivity: 'base',
// ignorePunctuation: true })`. Run once and commit the output; CollationTableTests reads it.
//
//   node tests/Blazix.Primitives.Tests/ListValues/Data/collation-table.mjs > tests/Blazix.Primitives.Tests/ListValues/Data/collation-table.json
const rows = [
  ['en-US', 'Résumé', 'resume', 'accents (filter.test.ts)'],
  ['en-US', 'RESUME', 'resume', 'case'],
  ['en-US', 'Résumé', 'resume', 'decomposed accents: the window counts code units'],
  ['en-US', 'Jean-Luc', 'jean luc', 'punctuation against whitespace, whole string'],
  ['en-US', 're-sume', 'resume', 'punctuation inside the item: the window counts code units'],
  ['en-US', 'New York', 'new york', 'whitespace, whole string'],
  ['en-US', 'New York', 'newyork', 'whitespace inside the item'],
  ['de-DE', 'Straße', 'strasse', 'ß against ss, whole string'],
  ['de-DE', 'Strasse', 'straße', 'ss against ß, whole string'],
  ['de-DE', 'Großstraße', 'strasse', 'ß against ss inside the item'],
  ['en-US', 'Æther', 'aether', 'æ against ae'],
  ['en-US', 'encyclopædia', 'paedia', 'æ against ae inside the item'],
  ['tr-TR', 'İstanbul', 'istanbul', 'dotted capital I under tr-TR'],
  ['tr-TR', 'ISPARTA', 'isparta', 'dotless capital I under tr-TR'],
  ['en-US', 'ISPARTA', 'isparta', 'dotless capital I under en-US'],
  ['en-US', 'ＡＢＣ', 'abc', 'full-width letters'],
  ['ja-JP', 'カタカナ', 'かたかな', 'katakana against hiragana'],
  ['ja-JP', 'ｶﾀｶﾅ', 'カタカナ', 'half-width katakana against katakana'],
  ['en-US', 'apple', '', 'empty query'],
  ['en-US', 'pie', 'apple pie', 'query longer than the item'],
];

function filter(locale) {
  const collator = new Intl.Collator(locale, { usage: 'search', sensitivity: 'base', ignorePunctuation: true });
  return {
    contains(text, query) {
      if (!query) return true;
      for (let i = 0; i <= text.length - query.length; i += 1) {
        if (collator.compare(text.slice(i, i + query.length), query) === 0) return true;
      }
      return false;
    },
    startsWith(text, query) {
      if (!query) return true;
      return collator.compare(text.slice(0, query.length), query) === 0;
    },
    endsWith(text, query) {
      if (!query) return true;
      return text.length >= query.length && collator.compare(text.slice(text.length - query.length), query) === 0;
    },
  };
}

const table = {
  generatedBy: `Node ${process.version}, ICU ${process.versions.icu}, CLDR ${process.versions.cldr}`,
  rows: rows.map(([locale, text, query, case_]) => {
    const f = filter(locale);
    return { locale, text, query, case: case_, contains: f.contains(text, query), startsWith: f.startsWith(text, query), endsWith: f.endsWith(text, query) };
  }),
};
process.stdout.write(`${JSON.stringify(table, null, 2)}\n`);
