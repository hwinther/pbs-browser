import { base, ignores, react, testingLibrary, vitest } from "@wshno/eslint-config";

// @wshno/eslint-config: JS/TS and SonarJS recommended, @eslint-react (incl. the hooks rules) with
// React Refresh, and the Vitest + Testing Library rules for the unit tests.
/** @type {import('eslint').Linter.Config[]} */
export default [
  ...ignores,
  ...base,
  ...react,
  ...vitest(),
  ...testingLibrary(),
  {
    rules: {
      // This small app fetches in an effect on snapshot change (set loading + reset, then async
      // fetch), a standard pattern with no data library. The perf-hint rule fights it; disable it.
      '@eslint-react/set-state-in-effect': 'off',
    },
  },
];
