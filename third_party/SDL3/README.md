# SDL controller driver

Unmodified SDL 3.4.20 Windows x64 library, distributed under the zlib license in LICENSE.txt.

- Official binary: https://github.com/libsdl-org/SDL/releases/download/release-3.4.20/SDL3-3.4.20-win32-x64.zip
- Official source: https://github.com/libsdl-org/SDL/tree/release-3.4.20
- Archive SHA-256: `368bed8b24e44ad9ea372990814f2343fcfb9e468abb7f8f4a3bc146c7304752`
- Uncompressed SDL3.dll SHA-256: `66844f10251cc1c10619ed5d8e4e3224f564900ee6d69ba87d63de782addae40`

SDL3.dll.gz contains that DLL compressed with gzip. build.ps1 embeds the compressed driver and its license into PadLauncher.exe. The app verifies the uncompressed checksum before loading it from PadLauncher.Data/runtime/SDL3/3.4.20-x64. No download or separate installation is needed at runtime.
