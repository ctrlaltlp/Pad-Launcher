# Xbox app integration

Reads installed main packages and Start-menu app registrations for the current Windows account using Windows PowerShell's `Get-AppxPackage` and `Get-StartApps`. It does not require administrator rights or scan game drives. Discovery runs during library refresh when **Include Xbox app games** is enabled.

Games are identified by `MicrosoftGame.config`, a Gaming Services package dependency, an Xbox Live `ms-xbl-` protocol, or the Xbox app's cached product catalog. Catalog detection reads only `product_summary` rows from `LocalState/AsyncCache.db` using Windows SQLite in read-only mode. Entries must have `productKind: GAME` and a matching `PACKAGEFAMILYNAME` alternate ID. This catches Store games such as Candy Crush Saga that have no Xbox-specific manifest markers. Only installed, visible applications with a current-user Start registration are included. Infrastructure apps and non-launchable content packages are excluded. Family name plus application ID provides a stable identity across package versions and repeated refreshes. Launching uses `explorer.exe shell:AppsFolder\<AUMID>` rather than directly starting a protected game executable.

Names come from Windows' localized Start entries. Suitable portrait artwork from the package is used when available; otherwise the existing SteamGridDB fallback applies. Reimport restores the Xbox source list using the existing launcher restore workflow.

Limits: installed games only, not cloud games or the user's entire owned/subscription catalog. Games delivered by EA or Battle.net use those integrations. Packages that lack recognizable game metadata, a matching cached product, or a visible Windows registration may not be detected. The Xbox cache is an internal format and may change; manifest detection remains available when the cache is missing. Live discovery found Candy Crush Saga and Solitaire & Casual Games without warnings. Actual game activation remains untested.

Microsoft references:

- [Installed app package query](https://learn.microsoft.com/en-us/powershell/module/appx/get-appxpackage)
- [Get-StartApps](https://learn.microsoft.com/en-us/powershell/module/startlayout/get-startapps)
- [Application user model IDs](https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid)
- [MicrosoftGame.config](https://learn.microsoft.com/en-us/gaming/gdk/docs/features/common/game-config/microsoftgameconfig-overview)
