const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = tseslint.config(
  {
    ignores: ['src/app/_models/**', 'dist/**', 'out-tsc/**', '.angular/**'],
  },
  {
    files: ['src/**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'app', style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'app', style: 'kebab-case' },
      ],
    },
  },
  {
    // Preserve constructor injection in existing classes during this upgrade.
    // Requiring inject() would change their construction contract for style alone.
    files: [
      'src/app/busy/busy.component.ts',
      'src/app/shell/shell.component.ts',
      'src/app/todo-rest/todo-rest.component.ts',
      'src/app/todo-rest/todo-rest.service.ts',
      'src/app/todo-realtime/todo-realtime.component.ts',
      'src/app/todo-realtime/todo-realtime.service.ts',
    ],
    rules: { '@angular-eslint/prefer-inject': 'off' },
  },
  {
    // Angular22 migrated these components to Eager to preserve zone/RxJS updates.
    // Changing their detection strategy belongs to a separate behavior change.
    files: [
      'src/app/app.component.ts',
      'src/app/busy/busy.component.ts',
      'src/app/shell/shell.component.ts',
      'src/app/todo-realtime/todo-realtime.component.ts',
    ],
    rules: {
      '@angular-eslint/prefer-on-push-component-change-detection': 'off',
    },
  },
  {
    files: ['src/**/*.html'],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
    ],
  }
);
