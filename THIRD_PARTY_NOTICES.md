# Third-party components

## FFmpeg

The Windows portable package includes FFmpeg 9.0.2, distributed as a separate executable. FFmpeg and its included libraries retain their own licenses; the MIT license for QingZhuan does not replace those licenses.

- Build provider: https://www.gyan.dev/ffmpeg/builds/
- Build configuration and source revision: `bin/FFmpeg-BUILD.txt` in the portable package
- License: GPL version 3, included as `bin/FFmpeg-LICENSE.txt`
- Corresponding FFmpeg revision: https://github.com/FFmpeg/FFmpeg/commit/946fcce07b
- Source tree archive: https://github.com/FFmpeg/FFmpeg/archive/946fcce07b.tar.gz
- Build provider repository: https://github.com/GyanD/codexffmpeg

The application invokes FFmpeg as an external process. The source repository does not store FFmpeg binaries or music files. The dependency archive is checked with SHA-256 before packaging.

## NCM format research

Compatibility research reference: https://github.com/taurusxin/ncmdump. The NCM parser and test fixtures in this repository are implemented in C# using the .NET cryptography APIs. No account credentials, song download service, or network cover fetching are included.

## PWA runtime

The PWA uses @ffmpeg/core 0.12.10 (single-thread WebAssembly, GPL-2.0-or-later), aes-js 3.1.2 (MIT), and fflate 0.8.2 (MIT). See `web/THIRD_PARTY_NOTICES.txt` and `scripts/pwa-dependencies.json` for sources, versions, and pinned hashes. Their license files are included in the deployed `vendor/` directory. Browser runtime dependencies are downloaded by `scripts/prepare-pwa.mjs`; music files are never sent to those services.
