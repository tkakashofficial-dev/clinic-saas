# Registration flow note

## What was changed
- Fixed the post-registration redirect so a successful signup now sends the user to the dashboard route.
- Kept the register form in a loading state while the request is in progress.
- Removed the earlier redirect behavior that could leave the user on the register screen without a visible transition.

## Files involved
- frontend/src/app/features/auth/register.ts
- frontend/src/app/features/auth/register.html
- frontend/src/app/features/auth/auth-layout.scss

## Current behavior
- When a new clinic owner completes registration, the app redirects to /dashboard after the API call succeeds.
- If registration fails, the form shows the returned error message and remains editable.

## Important note
- No code was pushed.
- No unrelated changes were made.
