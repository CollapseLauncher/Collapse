@echo off
:: Set the Toolchain variable to empty to use MSVC instead.
:: set Toolchain=-T ClangCL
set Toolchain=-T ClangCL
:: Either can be Release (/O2 /Ob2), MinSizeRel (/O1 /Ob1) or RelWithDebInfo (/O2 /Ob1 /Zi)
set BuildType=MinSizeRel
set OutputDir=%~dp0..\
set RuntimeLibrary=MultiThreaded

:: If you want to enables LTO using Clang, replaces LTOArgs with defined one below (very experimental)
:: set LTOArgs=-flto
:: set CXXFLAGS=%LTOArgs% -EHsc
:: set CFLAGS=%LTOArgs% -EHsc
:: set LDFLAGS=%LTOArgs% /LTCG
:: Or use the one below if you're using MSVC.
:: set LTOArgs=
:: set CXXFLAGS=-EHsc /GL
:: set CFLAGS=-EHsc /GL
:: set LDFLAGS=/LTCG /GENPROFILE
set LTOArgs=
set CXXFLAGS=-EHsc
set CFLAGS=-EHsc
set LDFLAGS=

set GenericCMAKEParamNoFlagsNoIntOpt=-DCMAKE_BUILD_TYPE=%BuildType% %Toolchain% -DCMAKE_MSVC_RUNTIME_LIBRARY=%RuntimeLibrary%
set GenericCMAKEParamNoFlags=%GenericCMAKEParamNoFlagsNoIntOpt% -DCMAKE_INTERPROCEDURAL_OPTIMIZATION=true
set GenericCMAKEParam=%GenericCMAKEParamNoFlags% -DCMAKE_CXX_FLAGS="%CXXFLAGS%" -DCMAKE_C_FLAGS="%CFLAGS%" -DCMAKE_EXE_LINKER_FLAGS="%LDFLAGS%" -DCMAKE_EXE_LINKER_FLAGS_INIT="%LDFLAGS%"

:: Test Toolchain
call :ToolchainTest
if NOT %errorlevel% == 0 ( goto :EOF )

:: Start building
call :Build_waifu2x || goto :EOF
call :Build_libwebp || goto :EOF
call :Build_libheif || goto :EOF
call :Build_libjxl  || goto :EOF
call :Build_libzstd || goto :EOF
call :Build_libomp  || goto :EOF

:: Suicide
goto :COMPLETE

:: Building functions - waifu2x
:Build_waifu2x
    title=Building waifu2x
    git clone --recursive https://github.com/shatyuka/waifu2x-ncnn-vulkan
    cd waifu2x-ncnn-vulkan
    git fetch --all && git pull --all
    copy /Y ..\patch\waifu2x-ncnn-vulkan\src\CMakeLists.txt src\CMakeLists.txt
    cmake . %GenericCMAKEParam% -B vclatestbuild -DCMAKE_POLICY_VERSION_MINIMUM=3.5 || goto :ERR
    cd vclatestbuild
    msbuild ALL_BUILD.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    call :CallCopy src\%BuildType%\waifu2x-ncnn-vulkan-static.lib || goto :ERR
    call :CallCopy src\external\ncnn\glslang\glslang\%BuildType%\GenericCodeGen.lib Waifu2x || goto :ERR
    call :CallCopy src\external\ncnn\glslang\glslang\%BuildType%\glslang.lib Waifu2x || goto :ERR
    call :CallCopy src\external\ncnn\glslang\glslang\%BuildType%\MachineIndependent.lib Waifu2x || goto :ERR
    call :CallCopy src\external\ncnn\glslang\glslang\OSDependent\Windows\%BuildType%\OSDependent.lib Waifu2x || goto :ERR
    call :CallCopy src\external\ncnn\glslang\OGLCompilersDLL\%BuildType%\OGLCompiler.lib Waifu2x || goto :ERR
    call :CallCopy src\external\ncnn\glslang\SPIRV\%BuildType%\SPIRV.lib Waifu2x || goto :ERR
    call :CallCopy src\external\ncnn\src\%BuildType%\ncnn.lib Waifu2x || goto :ERR
    goto :BACKTOROOT

:: Building functions - libomp
:Build_libomp
    title=Building libomp
    git clone --recursive https://github.com/llvm/llvm-project
    cd llvm-project
    git fetch --all && git pull --all
    copy /Y ..\patch\openmp\runtime\CMakeLists.txt openmp\runtime\CMakeLists.txt
    cd runtimes
    set OldCXXFLAGS=%CXXFLAGS%
    set OldCFLAGS=%CFLAGS%
    set OldLDFLAGS=%LDFLAGS%
    set CXXFLAGS=
    set CFLAGS=
    set LDFLAGS=
    cmake . %GenericCMAKEParamNoFlagsNoIntOpt% -DENABLE_CHECK_TARGETS=FALSE -DLLVM_ENABLE_RUNTIMES=openmp -DLIBOMP_ENABLE_SHARED=FALSE -B vclatestbuild || goto :ERR
    cd vclatestbuild
    msbuild ALL_BUILD.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    set CXXFLAGS=%OldCXXFLAGS%
    set CFLAGS=%OldCFLAGS%
    set LDFLAGS=%OldLDFLAGS%
    move openmp\runtime\src\%BuildType%\libomp.lib.lib openmp\runtime\src\%BuildType%\libomp.lib
    call :CallCopy openmp\runtime\src\%BuildType%\libomp.lib || goto :ERR
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
    call :CallCopy %BuildType%\libsharpyuv.lib || goto :ERR
    call :CallCopy %BuildType%\libwebp.lib || goto :ERR
    call :CallCopy %BuildType%\libwebpdemux.lib || goto :ERR
    call :CallCopy %BuildType%\libwebpmux.lib || goto :ERR
    goto :BACKTOROOT

:: Building functions - libheif
:Build_libheif
    title=Building libheif dependencies - dav1d
    git clone --recursive https://code.videolan.org/videolan/dav1d
    cd dav1d
    git fetch --all && git pull --all
    set OLDCXXFLAGS=%CXXFLAGS%
    set OLDCFLAGS=%CFLAGS%
    set OLDLDFLAGS=%LDFLAGS%
    set CXXFLAGS=
    set CFLAGS=
    set LDFLAGS=
    call :SETCLANGCC
    mkdir build
    cd build
    meson setup .. --wipe --default-library=static || goto :ERR
    ninja
    set CXXFLAGS=%OLDCXXFLAGS%
    set CFLAGS=%OLDCFLAGS%
    set LDFLAGS=%OLDLDFLAGS%
    copy src\libdav1d.a src\libdav1d.lib
    call :CallCopy src\libdav1d.lib || goto :ERR
    call :UNSETCLANGCC
    cd "%~dp0"

    title=Building libheif dependencies - libde265
    git clone --recursive https://github.com/strukturag/libde265
    cd libde265
    git fetch --all && git pull --all
    xcopy /E /S /Y  ..\patch\libde265 .
    copy /Y extra\libde265\de265-version.h libde265\de265-version.h
    set OLD_CXXFLAGS=%CXXFLAGS%
    set OLD_CFLAGS=%CFLAGS%
    set CXXFLAGS=%CXXFLAGS% -msse4.1 -mssse3 -msse2
    set CFLAGS=%CFLAGS% -msse4.1 -mssse3 -msse2
    cmake . %GenericCMAKEParamNoFlags% -DBUILD_SHARED_LIBS=0 -DENABLE_SDL=OFF -B vclatestbuild ^
    -DSUPPORTS_SSE4_1=1 -DSUPPORTS_SSSE3=1 -DSUPPORTS_SSE2=1 ^
    -DCMAKE_CXX_FLAGS="%CXXFLAGS%" -DCMAKE_C_FLAGS="%CFLAGS%"
    cd vclatestbuild
    msbuild ALL_BUILD.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    set CXXFLAGS=%OLD_CXXFLAGS%
    set CFLAGS=%OLD_CFLAGS%
    call :CallCopy libde265\%BuildType%\libde265.lib || goto :ERR
    cd "%~dp0
    
    title=Building libheif dependencies - zlib
    git clone --recursive https://github.com/madler/zlib
    cd zlib
    git fetch --all && git pull --all
    cmake . %GenericCMAKEParam% -DZLIB_BUILD_STATIC=1 -DZLIB_BUILD_SHARED=0 -B vclatestbuild
    cd vclatestbuild
    msbuild zlibstatic.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    move %BuildType%\zs.lib %BuildType%\zlib.lib
    cd "%~dp0

    title=Building libheif
    git clone --recursive https://github.com/strukturag/libheif
    cd libheif
    git fetch --all && git pull --all
    copy /Y ..\patch\libheif\third-party\libsharpyuv.cmd third-party\libsharpyuv.cmd
    cd third-party
    call libsharpyuv
    cd ..\
    set thisdir=%~dp0
    copy /Y ..\patch\libheif\CMakeLists.txt CMakeLists.txt
    cmake . %GenericCMAKEParam% --preset=release-noplugins -DBUILD_SHARED_LIBS=0 -B vclatestbuild -DENABLE_EXPERIMENTAL_FEATURES=1 -DWITH_REDUCED_VISIBILITY=1 ^
    -DWITH_LIBDE265=1 -DLIBDE265_INCLUDE_DIR=..\libde265 -DLIBDE265_LIBRARY=%thisdir%\libde265\vclatestbuild\libde265\%BuildType%\libde265.lib ^
    -DWITH_DAV1D=1 -DDAV1D_INCLUDE_DIR=..\dav1d\include -DDAV1D_LIBRARY=%thisdir%\dav1d\build\src\libdav1d.lib ^
    -DWITH_LIBSHARPYUV=1 -DLIBSHARPYUV_INCLUDE_DIR=..\libwebp -DLIBSHARPYUV_LIBRARY=%thisdir%\libwebp\vclatestbuild\%BuildType%\libsharpyuv.lib ^
    -DWITH_HEADER_COMPRESSION=1 -DWITH_UNCOMPRESSED_CODEC=1 -DZLIB_INCLUDE_DIR=..\zlib -DZLIB_LIBRARY=%thisdir%\zlib\vclatestbuild\%BuildType%\zlib.lib || goto :ERR
    copy /Y ..\patch\libheif\vclatestbuild\libheif\heif_version.h vclatestbuild\libheif\heif_version.h
    cd vclatestbuild\libheif
    msbuild heif.vcxproj -p:Configuration=%BuildType% /m:%NUMBER_OF_PROCESSORS% || goto :ERR
    call :CallCopy %BuildType%\heif.lib || goto :ERR
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
    call :CallCopy lib\%BuildType%\jxl.lib || goto :ERR
    call :CallCopy lib\%BuildType%\jxl_cms.lib || goto :ERR
    call :CallCopy third_party\highway\%BuildType%\hwy.lib || goto :ERR
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
    move %BuildType%\zstd_static.lib %BuildType%\libzstd_static.lib
    call :CallCopy %BuildType%\libzstd_static.lib || goto :ERR
    goto :BACKTOROOT

:CallCopy
    if /I not exist "%OutputDir%\%~2" ( mkdir "%OutputDir%\%~2" )
    set copyToPath=%OutputDir%\%~2\%~nx1
    echo Copying file "%~dpnx1" to "%copyToPath%"
    1>nul copy /Y "%~dpnx1" "%copyToPath%" || call :ERR " while copying file %~1 to %copyToPath%" && goto :EOF
    goto :EOF

:ERR
    title=Error %errorlevel%
    echo An error has occurred with code: %errorlevel%%~1
    (pause > nul) | echo Press any key to exit...
    if %errorlevel% EQU 0 (
        set errorlevel=1
    )
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
    for /f "tokens=*" %%a in ('where lld') do (
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
    
    :: Check for Vulkan SDK dependencies (for Waifu2X)
    if /I not exist "%VULKAN_SDK%\Lib\vulkan-1.lib" (
        set errorlevel=67
        call :ERR ". Make sure you have Vulkan SDK installed and ^%VULKAN_SDK^% variable defined." && goto :EOF
    )