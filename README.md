# komoband
Simplistic workspace switcher deskband for [komorebi](https://github.com/LGUG2Z/komorebi)

<!-- prettier-ignore -->
> [!CAUTION]
> **Nothing is configurable in its current state!** Colors and fonts are hardcoded currently.

![Screenshot of komoband](.assets/normal.png)

![Screenshot of komoband with mixed name styles for workspaces](.assets/mixed_names.png)

## Install
```
dotnet build
cd bin/Debug/net462
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
sudo regasm /codebase komoband.dll
```

## To-Do
- Configuration
- Layout switcher

## Known Issues
- Sometimes fails to reconnect properly when komorebi is restarted, and can cause Explorer to crash as a result
- Probably some race condition issues
