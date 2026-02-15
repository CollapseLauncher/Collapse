@echo off
:: Set the Toolchain variable to empty to use MSVC instead.
set Toolchain=-T ClangCL
set BuildType=Release
set OutputDir=%~dp0..\

set GenericCMAKEParam=-DCMAKE_BUILD_TYPE=%BuildType% %Toolchain% -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded

:: Test Toolchain
call :ToolchainTest
if NOT %errorlevel% == 0 ( goto :EOF )

:: Start building
call :Build_libomp  || goto :EOF
call :Build_libheif || goto :EOF
call :Build_libjxl  || goto :EOF
call :Build_libzstd || goto :EOF
call :Build_libwebp || goto :EOF
call :Build_libheif || goto :EOF

:: Suicide
goto :COMPLETE

:: Building functions - libomp
:Build_libomp
    title=Building libomp
    git clone --no-checkout --depth=1 --filter=tree:0 https://github.com/llvm/llvm-project
    cd llvm-project
    git sparse-checkout set --no-cone /openmp /cmake && git checkout
    git fetch --all && git pull --all
    copy /Y ..\patch\openmp\runtime\CMakeLists.txt openmp\runtime\CMakeLists.txt
    cd openmp
    cmake . %GenericCMAKEParam% -DENABLE_CHECK_TARGETS=FALSE -DOPENMP_STANDALONE_BUILD=1 -DLIBOMP_ENABLE_SHARED=FALSE -B vclatestbuild || goto :ERR
    cd vclatestbuild
    msbuild ALL_BUILD.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    move runtime\src\Release\libomp.lib.lib runtime\src\Release\libomp.lib
    call :CallCopy runtime\src\Release\libomp.lib || goto :ERR
    goto :BACKTOROOT

:: Building functions - libjxl
:Build_libjxl
    title=Building libjxl
    git clone --recursive https://github.com/libjxl/libjxl
    cd libjxl
    git fetch --all && git pull --all
    cmake . %GenericCMAKEParam% -DBUILD_TESTING=OFF -DJPEGXL_STATIC=1 -B vclatestbuild || goto :ERR
    cd vclatestbuild
    msbuild ALL_BUILD.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    call :CallCopy lib\Release\jxl.lib || goto :ERR
    call :CallCopy lib\Release\jxl_cms.lib || goto :ERR
    call :CallCopy third_party\highway\Release\hwy.lib || goto :ERR
    goto :BACKTOROOT

:: Building functions - libzstd
:Build_libzstd
    title=Building libzstd
    git clone --recursive https://github.com/facebook/zstd
    cd zstd
    git fetch --all && git pull --all
    cmake . %GenericCMAKEParam% -DBUILD_TESTING=OFF -DZSTD_USE_STATIC_RUNTIME=ON -DZSTD_BUILD_STATIC=ON -DZSTD_BUILD_PROGRAMS=OFF -DZSTD_MULTITHREAD_SUPPORT=ON -B vclatestbuild || goto :ERR
    cd vclatestbuild\build\cmake\lib
    msbuild libzstd_static.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    move Release\zstd_static.lib Release\libzstd_static.lib
    call :CallCopy Release\libzstd_static.lib || goto :ERR
    goto :BACKTOROOT

:: Building functions - libwebp
:Build_libwebp
    title=Building libwebp
    git clone --recursive https://chromium.googlesource.com/webm/libwebp
    cd libwebp
    git fetch --all && git pull --all
    cmake . %GenericCMAKEParam% -DBUILD_SHARED_LIBS=0 -DWEBP_LINK_STATIC=1 -DEMSCRIPTEN=OFF -DWEBP_USE_THREAD=ON ^
    -DWEBP_BUILD_ANIM_UTILS=OFF -DWEBP_BUILD_CWEBP=OFF -DWEBP_BUILD_DWEBP=OFF -DWEBP_BUILD_GIF2WEBP=OFF ^
    -DWEBP_BUILD_IMG2WEBP=OFF -DWEBP_BUILD_VWEBP=OFF -DWEBP_BUILD_WEBPINFO=OFF ^
    -DWEBP_BUILD_WEBPMUX=OFF -DWEBP_BUILD_EXTRAS=OFF -B vclatestbuild || goto :ERR
    cd vclatestbuild
    msbuild ALL_BUILD.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    call :CallCopy Release\libsharpyuv.lib || goto :ERR
    call :CallCopy Release\libwebp.lib || goto :ERR
    call :CallCopy Release\libwebpdecoder.lib || goto :ERR
    call :CallCopy Release\libwebpdemux.lib || goto :ERR
    call :CallCopy Release\libwebpmux.lib || goto :ERR
    goto :BACKTOROOT

:: Building functions - libheif
:Build_libheif
    title=Building libheif dependencies - dav1d
    git clone --recursive https://code.videolan.org/videolan/dav1d
    cd dav1d
    git fetch --all && git pull --all
    call :SETCLANGCC
    mkdir build && cd build
    meson setup .. --wipe --default-library=static
    ninja
    copy src\libdav1d.a src\libdav1d.lib
    call :CallCopy build\src\libdav1d.lib || goto :ERR
    call :UNSETCLANGCC
    cd "%~dp0"

    title=Building libheif dependencies - libde265
    git clone --recursive https://github.com/strukturag/libde265
    cd libde265
    git fetch --all && git pull --all
    xcopy /E /S /Y  ..\patch\libde265 .
    copy /Y extra\libde265\de265-version.h libde265\de265-version.h
    set CXXFLAGS=-msse4.1 -mssse3 -msse2
    cmake . %GenericCMAKEParam% -DBUILD_SHARED_LIBS=0 -DENABLE_SDL=OFF -DSUPPORTS_SSE4_1=1 -DSUPPORTS_SSSE3=1 -DSUPPORTS_SSE2=1 -B vclatestbuild
    cd vclatestbuild
    msbuild ALL_BUILD.vcxproj -p:Configuration=%BuildType%  /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    set CXXFLAGS=
    call :CallCopy libde265\Release\libde265.lib || goto :ERR
    cd "%~dp0
    
    title=Building libheif dependencies - zlib
    git clone --recursive https://github.com/madler/zlib
    cd zlib
    git fetch --all && git pull --all
    cmake . %GenericCMAKEParam% -DZLIB_BUILD_STATIC=1 -DZLIB_BUILD_SHARED=0 -B vclatestbuild
    cd vclatestbuild
    msbuild zlibstatic.vcxproj -p:Configuration=%BuildType%  /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    move Release\zs.lib Release\zlib.lib
    call :CallCopy Release\zlib.lib || goto :ERR
    cd "%~dp0

    title=Building libheif
    git clone --recursive https://github.com/strukturag/libheif
    cd libheif
    git fetch --all && git pull --all
    cd third-party
    call libsharpyuv
    cd ..\
    set thisdir=%~dp0
    copy /Y ..\patch\libheif\CMakeLists.txt CMakeLists.txt
    cmake . %GenericCMAKEParam% --preset=release-noplugins -DBUILD_SHARED_LIBS=0 -B vclatestbuild -DENABLE_EXPERIMENTAL_FEATURES=1 -DWITH_REDUCED_VISIBILITY=1 ^
    -DWITH_LIBDE265=1 -DLIBDE265_INCLUDE_DIR=..\libde265 -DLIBDE265_LIBRARY=%thisdir%\libde265\vclatestbuild\libde265\Release\libde265.lib ^
    -DWITH_DAV1D=1 -DDAV1D_INCLUDE_DIR=..\dav1d\include -DDAV1D_LIBRARY=%thisdir%\dav1d\build\src\libdav1d.lib ^
    -DWITH_LIBSHARPYUV=1 -DLIBSHARPYUV_INCLUDE_DIR=third-party\libwebp -DLIBSHARPYUV_LIBRARY=%thisdir%\libheif\third-party\libwebp\build\libsharpyuv.lib ^
    -DWITH_HEADER_COMPRESSION=1 -DWITH_UNCOMPRESSED_CODEC=1 -DZLIB_INCLUDE_DIR=..\zlib -DZLIB_LIBRARY=%thisdir%\zlib\vclatestbuild\Release\zlib.lib || goto :ERR
    copy /Y ..\patch\libheif\vclatestbuild\libheif\heif_version.h vclatestbuild\libheif\heif_version.h
    cd vclatestbuild\libheif
    msbuild heif.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    call :CallCopy Release\heif.lib || goto :ERR
    goto :BACKTOROOT

:CallCopy
    if /I not exist "%OutputDir%" ( mkdir "%OutputDir%" )
    echo Copying file "%~dpnx1" to "%OutputDir%\%~nx1"
    1>nul copy /Y "%~dpnx1" "%OutputDir%\%~nx1" || call :ERR " while copying file %~1 to %OutputDir%" && goto :EOF
    goto :EOF

:ERR
    title=Error %errorlevel%
    echo An error has occurred with code: %errorlevel%%~1
    (pause > nul) | echo Press any key to exit...
    goto :EOF

:BACKTOROOT
    cd "%~dp0"
    goto :EOF

:COMPLETE
    echo Compilation has been completed! All libraries are stored in "%OutputDir%"
    goto :EOF

:SETCLANGCC
    for /f "tokens=*" %%a in ('where clang-cl') do (
        set CC=%%a
    )
    for /f "tokens=*" %%a in ('where clang++') do (
        set CXX=%%a
    )
    for /f "tokens=*" %%a in ('where lld-link') do (
        set LD=%%a
    )
    goto :EOF

:UNSETCLANGCC
    set CC=
    set CXX=
    set LD=
    goto :EOF

:ToolchainTest
    :: Check basic compiler toolchains
    if "%VCToolsVersion%" == "" (
        set errorlevel=69420
        call :ERR ". Make sure you are running this script from 'x64 Native Tools Command Prompt for VS'" && goto :EOF
    )
    (>NUL cmake --version)    || call :ERR ". Make sure you have 'C++ CMake tools for Windows' installed via 'Visual Studio Installer'" && goto :EOF
    (>NUL git version)        || call :ERR ". Make sure you have 'Git for Windows' installed via 'Visual Studio Installer'" && goto :EOF
    (>NUL clang-cl --version) || call :ERR ". Make sure you have 'C++ Clang Compiler for Windows' and 'MSBuild support for LLVM (clang-cl) toolset' installed via 'Visual Studio Installer'" && goto :EOF

    :: Check python dependencies
    for /f "tokens=*" %%a in ('python --version') do (
        set pythonVer=%%a
    )
    
    if /I not "%pythonVer:~0,8%" == "Python 3" (
        set errorlevel=67
        call :ERR ". Make sure you have installed any Python 3 interpreter" && goto :EOF
    )

    (>NUL meson --version) || call :ERR ". Make sure you have meson installed by using 'pip install meson' system-wide" && goto :EOF
    (>NUL ninja --version) || call :ERR ". Make sure you have ninja installed by using 'pip install ninja' system-wide" && goto :EOF

    (2>NUL 1>NUL where nasm) || if /I exist "C:\Program Files\NASM\nasm.exe" (
        set "PATH=%PATH%;C:\Program Files\NASM"
    ) else if /I exist "%appdata%\..\Local\bin\NASM\nasm.exe" (
        set "PATH=%PATH%;%appdata%\..\Local\bin\NASM"
    )

    (>NUL nasm --version) || call :ERR ". Make sure you have installed NASM 3.x or above and installed in either 'C:\Program Files\NASM' or '%appdata%\..\Local\bin' directory" && goto :EOF