# Project instructions

These instructions apply to the entire `the-tower-of-babel` repository.

## Project isolation

- Treat `D:\PersonalProjects\the-tower-of-babel` as this project's workspace root.
- Build project context from this repository and the current conversation. Do not import codebase assumptions, project-specific preferences, configuration, or remembered decisions from other projects.
- Verify that file paths, command working directories, and connected Unity Editor instances belong to this project before reading project data or making changes.
- Keep project-specific notes, context, and instructions inside this repository. Do not write them to shared or global memory or another project's files.
- Shared tools and general-purpose skills may be used, but their examples and defaults are not evidence about this project.

## Unity Editor

- Never enter Play Mode while a feature or fix is still being implemented, including through a test runner.
- Once implementation is complete, entering Play Mode is allowed only through the automated test runner to execute PlayMode tests as final verification. Manual or ad hoc Play Mode sessions remain prohibited.
- If final verification requires further implementation changes, finish those changes before running PlayMode tests again.

## Automated tests

- Always create or update automated tests for feature implementations and bug fixes, including small or reversible code changes.
- Always run the relevant automated tests after completing a feature or fix and after completing subsequent implementation changes.
- Use Unity EditMode tests or other checks that do not enter Play Mode during implementation. Run relevant PlayMode tests through the automated test runner only after implementation is complete.
- If tests cannot run, report the blocker and the tests that remain unexecuted. Do not claim validation succeeded.
- Documentation-only or project-context changes do not require artificial code tests; verify the resulting instructions directly.
