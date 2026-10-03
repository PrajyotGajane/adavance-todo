# Publishing to the Microsoft Store

1. Register a developer account at https://partner.microsoft.com/dashboard (one-time fee; individual accounts are fine).
2. Create a new app and reserve the name (e.g. "Task & Notes").
3. Open **Product management > Product identity** and copy:
   - `Package/Identity/Name`  -> repo variable `MSIX_IDENTITY_NAME`
   - `Package/Identity/Publisher` (the `CN=...` string) -> `MSIX_PUBLISHER`
   - `Package/Properties/PublisherDisplayName` -> `MSIX_PUBLISHER_DISPLAY_NAME`

   Set them in GitHub: Settings > Secrets and variables > Actions > **Variables**.
4. Push a tag (`git tag v0.1.0 && git push origin v0.1.0`). The release workflow attaches
   `DesktopCompanion-v0.1.0-x64.msix` to the GitHub release.
5. In Partner Center create a submission and upload the `.msix` (the Store signs it; no certificate is needed).
6. Fill in the listing: description, screenshots (1366x768 or larger), the 1024x1024 logo
   (`packaging/Assets/StoreIcon1024.png`), age rating, privacy policy URL (state that all data stays on-device) and
   the `runFullTrust` justification: "Desktop app that registers global hotkeys and a tray icon."

To test locally, install the Windows SDK and run
`./packaging/build-msix.ps1 -PublishDir <publish folder> -Version 0.0.1`; sideloading needs a signed package.

Known limitation in the packaged build: the in-app "Start with Windows" toggle writes the classic Run registry key,
which MSIX virtualizes, so it will not start the app at login. Adding a `windows.startupTask` extension is the fix.
