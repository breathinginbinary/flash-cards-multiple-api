# Flash Card Multiple — User Guide

Flash Card Multiple lets you build small series of flash cards (1–3 cards per topic) and study them by flipping the cards over.

## Starting the app

1. Start the API: `dotnet run --project flash-cards-multiple-api --launch-profile https` (from `flash-cards-multiple-api`; it listens on https://localhost:7100).
2. Start the Angular app: `ng serve` (from `flash-card-multiple`), then open the address it prints (normally http://localhost:4200).

## Getting an account

- **Register** (top navigation): enter a display name (up to 50 characters), your email, and a password of 8–100 characters with at least one letter and one digit. Type the password again to confirm, then click **Register**.
- **Log in**: use the email and password you registered with. Too many attempts in a minute are blocked temporarily.
- New accounts start with their own **Dog** and **Cat** sample series, so you can try studying straight away.
- Your name appears at the top right once you are logged in. **Log out** is in the navigation bar. Your login is remembered in the browser until it expires; if it does, you are sent back to the login page.

Every series belongs to your account; other users cannot see or change yours.

## Navigation

When logged in the top bar has **Study**, **Edit cards** and **Log out**. When logged out it shows **Log in** and **Register**. The moon/sun button at the far right switches between dark and light mode.

## Studying cards

Open **Study**. The page shows one series at a time: its topic, an optional picture, and its cards, with "Series *n* of *N*" above.

- **Flip a card:** click it to see the back; click again to return to the front.
- **← Previous / Next →:** move between series.
- **Flip all:** flips every card in the current series.
- **Reset:** turns all cards back to the front.
- **Shuffle:** randomises the order of the series.

Keyboard shortcuts: `1`, `2`, `3` flip that card; `←` / `→` previous / next series; `F` flip all; `R` reset.

If you have no series yet, the page offers a **Create your first series** button.

## Managing series (Edit cards)

Open **Edit cards** to see all your series, each with its topic, first card's front, and last-updated date.

- **Filter by topic:** type in the filter box to narrow the list.
- **Study this:** jumps to the Study page on that series.
- **Edit:** opens the series editor.
- **Delete:** asks for confirmation, then permanently removes the series (it cannot be undone).
- **New series:** opens an empty editor.

### Creating or editing a series

1. **Source topic** — the series title (required, up to 200 characters).
2. **Image** (optional) — choose an image file; a preview appears, and **Remove** clears it.
3. **Number of cards** — choose 1, 2 or 3. The matching number of card boxes appears.
4. For each card fill in the **Front** (up to 500 characters) and **Back** (up to 1000 characters). Leading and trailing spaces are trimmed.
5. Click **Save**. The button stays disabled until the form is valid. A green "Saved." message confirms success.

If the series was changed elsewhere (another tab or device) while you were editing, you will see a conflict message: **Reload** discards your edits and loads the latest version, while **Overwrite** saves your version over it.

## Troubleshooting

- **"Your card series could not be loaded":** check the API is running on https://localhost:7100, then click **Retry**.
- **Pages show a gateway error (502):** the API is probably running on the HTTP-only profile; use the `https` launch profile.
- **Sent back to Log in unexpectedly:** your session expired; log in again.
